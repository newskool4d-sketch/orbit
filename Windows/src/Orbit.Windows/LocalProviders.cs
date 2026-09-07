using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
namespace Orbit;
internal sealed record FileTarget(string Root,string Path);
internal sealed class LocalSnapshot
{
    public List<JsonObject> Files {get;}=[];
    public List<JsonObject> Agents {get;}=[];
    public List<JsonObject> Statuses {get;}=[];
    public Dictionary<string,FileTarget> FileMap {get;}=new();
    public Dictionary<string,string> AgentMap {get;}=new();
}
internal static class LocalProviders
{
    static readonly HashSet<string> Extensions=new(StringComparer.OrdinalIgnoreCase){".pdf",".hwpx",".hwp",".docx",".xlsx",".pptx",".md",".txt",".csv",".png",".jpg",".jpeg"};
    public static JsonObject Status(string source,string state,string message,int count=0,bool limited=false)=>new(){["source"]=source,["state"]=state,["message"]=message,["updated"]=DateTimeOffset.UtcNow.ToUnixTimeSeconds(),["limited"]=limited,["stale"]=false};
    public static LocalSnapshot Read(Settings settings)
    {
        var result=new LocalSnapshot();
        Files(settings.Drive,"drive",result);Files(settings.Vault,"obsidian",result);
        Claude(settings.Claude,result);
        CodexProvider.Read(settings.Codex,result);
        return result;
    }
    static void Files(string root,string source,LocalSnapshot result)
    {
        if(!Directory.Exists(root)){result.Statuses.Add(Status(source,"disconnected","폴더를 선택하세요."));return;}
        var watch=Stopwatch.StartNew();int visited=0;bool limited=false;
        var candidates=new List<FileInfo>();var stack=new Stack<(string Path,int Depth)>();stack.Push((root,0));
        try
        {
            while(stack.Count>0&&visited<20000&&watch.Elapsed.TotalSeconds<4)
            {
                var (folder,depth)=stack.Pop();
                foreach(var path in Directory.EnumerateFileSystemEntries(folder))
                {
                    if(++visited>20000||watch.Elapsed.TotalSeconds>=4){limited=true;break;}
                    FileAttributes attributes;try{attributes=File.GetAttributes(path);}catch{limited=true;continue;}
                    if(!ReparsePolicy.Safe(path)){limited=true;continue;}
                    if((attributes&FileAttributes.Directory)!=0){if(depth<7)stack.Push((path,depth+1));else limited=true;}
                    else if(Extensions.Contains(Path.GetExtension(path)))candidates.Add(new FileInfo(path));
                }
            }
            limited|=stack.Count>0;
            foreach(var f in candidates.OrderByDescending(f=>f.LastWriteTimeUtc).Take(30))
            {
                if(!PathPolicy.Within(root,f.FullName)){limited=true;continue;}
                var id=Guid.NewGuid().ToString("N");result.FileMap[id]=new(root,f.FullName);
                result.Files.Add(new(){["id"]=id,["source"]=source,["name"]=f.Name,["parent"]=f.Directory?.Name??"",["modified"]=new DateTimeOffset(f.LastWriteTimeUtc).ToUnixTimeSeconds()});
            }
            result.Statuses.Add(Status(source,"connected",limited?"일부 범위만 검색됨 · 링크/클라우드 파일 검증 필요":"최근 수정 파일 연결됨",0,limited));
        }
        catch { result.Statuses.Add(Status(source,"error","폴더 접근을 확인하세요.",0,true)); }
    }
    static string Text(JsonNode? n)=>n is JsonValue v&&v.TryGetValue<string>(out var s)?s:"";
    static void Claude(string root,LocalSnapshot result)
    {
        if(!Directory.Exists(root)){result.Statuses.Add(Status("claude","disconnected","Claude Code 폴더를 선택하세요."));return;}
        var watch=Stopwatch.StartNew();var files=new List<FileInfo>();int visited=0;bool limited=false;
        try
        {
            foreach(var dir in Directory.EnumerateDirectories(root).Take(201))
            {
                if(++visited>200||watch.Elapsed.TotalSeconds>4){limited=true;break;}
                if(!ReparsePolicy.Safe(dir)){limited=true;continue;}
                foreach(var path in Directory.EnumerateFiles(dir,"*.jsonl"))
                {
                    if(files.Count>=20000||watch.Elapsed.TotalSeconds>4){limited=true;break;}
                    if(PathPolicy.Within(root,path))files.Add(new(path));
                }
            }
            foreach(var file in files.OrderByDescending(f=>f.LastWriteTimeUtc).Take(24))
            {
                if(watch.Elapsed.TotalSeconds>4){limited=true;break;}
                using var stream=new FileStream(file.FullName,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
                var head=new byte[(int)Math.Min(128*1024,stream.Length)];int n=stream.Read(head);
                var chunks=new List<string>{Encoding.UTF8.GetString(head,0,n)};
                if(stream.Length>head.Length)
                {
                    var start=Math.Max(head.Length,stream.Length-256*1024);stream.Position=start;var tail=new byte[(int)(stream.Length-start)];n=stream.Read(tail);
                    var text=Encoding.UTF8.GetString(tail,0,n);var newline=text.IndexOf('\n');chunks.Add(newline>=0?text[(newline+1)..]:"");
                }
                string title="",project="",custom="";bool side=false;string last="";
                foreach(var line in chunks.SelectMany(c=>c.Split('\n')))
                {
                    JsonObject? item;try{item=JsonNode.Parse(line) as JsonObject;}catch{continue;}if(item==null)continue;
                    if(item["isSidechain"]?.ToString()=="true"){side=true;break;}
                    if(Text(item["cwd"]) is string cwd&&cwd.Length>0)project=Path.GetFileName(Path.TrimEndingDirectorySeparator(cwd));
                    if(Text(item["customTitle"]) is string t&&t.Length>0)custom=t;
                    var type=Text(item["type"]);if(type is "user" or "assistant")last=type;
                    if(title.Length==0&&type=="user")
                    {
                        var content=item["message"]?["content"];title=Text(content);
                        if(content is JsonArray array)title=string.Join(" ",array.OfType<JsonObject>().Where(x=>Text(x["type"])=="text").Select(x=>Text(x["text"])));
                    }
                }
                if(side)continue;if(custom.Length>0)title=custom;if(title.Length==0)title="제목 없는 작업";
                title=title.Replace('\r',' ').Replace('\n',' ');if(title.Length>160)title=title[..160];
                var id=Guid.NewGuid().ToString("N");result.AgentMap[id]="claude";
                result.Agents.Add(new(){["id"]=id,["provider"]="claude",["title"]=title,["project"]=project,["updated"]=new DateTimeOffset(file.LastWriteTimeUtc).ToUnixTimeSeconds(),["status"]="최근 기록",["openMode"]=AppLinks.Registered("claude")?"appHome":"unavailable"});
            }
            result.Statuses.Add(Status("claude","connected","로컬 기록 연결됨 · 앱에서 작업 직접 선택",0,limited));
        }
        catch {result.Statuses.Add(Status("claude","error","Claude Code 기록 형식을 확인하세요."));}
    }
}
