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
    readonly Dictionary<string,string> previousIds;
    readonly Dictionary<string,string> identities=new(StringComparer.OrdinalIgnoreCase);
    public LocalSnapshot(LocalSnapshot? previous=null) {previousIds=previous?.identities??new(StringComparer.OrdinalIgnoreCase);}
    public string DisplayId(string identity) {
        if(identities.TryGetValue(identity,out var current))return current;
        var id=previousIds.TryGetValue(identity,out var old)?old:Guid.NewGuid().ToString("N");
        identities[identity]=id;return id;
    }
    public List<JsonObject> Files {get;}=[];
    public List<JsonObject> Agents {get;}=[];
    public List<JsonObject> Statuses {get;}=[];
    public Dictionary<string,FileTarget> FileMap {get;}=new();
    public Dictionary<string,string> AgentMap {get;}=new();
    public void SortByRecency() {
        Files.Sort((a,b)=>b["modified"]!.GetValue<long>().CompareTo(a["modified"]!.GetValue<long>()));
        Agents.Sort((a,b)=>b["updated"]!.GetValue<long>().CompareTo(a["updated"]!.GetValue<long>()));
    }
}
internal static class LocalProviders
{
    // Same list as macOS and the Windows design: Google Drive shows Docs/Sheets/Slides as .gdoc/.gsheet/.gslides.
    static readonly HashSet<string> Extensions=new(StringComparer.OrdinalIgnoreCase){".md",".pdf",".hwpx",".hwp",".docx",".xlsx",".pptx",".txt",".csv",".html",".gdoc",".gsheet",".gslides"};
    public static JsonObject Status(string source,string state,string message,int count=0,bool limited=false)=>new(){["source"]=source,["state"]=state,["message"]=message,["updated"]=DateTimeOffset.UtcNow.ToUnixTimeSeconds(),["limited"]=limited,["stale"]=false};
    public static LocalSnapshot Read(Settings settings,LocalSnapshot? previous=null)
    {
        var result=new LocalSnapshot(previous);
        // A vault inside the Drive folder is listed once, as Obsidian, without using the Drive budget.
        Files(settings.Drive,"drive",result,null,settings.Vault);Files(settings.Vault,"obsidian",result);
        Claude(settings.Claude,result);
        CodexProvider.Read(settings.Codex,result);
        result.SortByRecency();
        return result;
    }
    static readonly HashSet<string> ExcludedFolders=new(StringComparer.OrdinalIgnoreCase){"node_modules","build","dist","vendor"};
    static string? FolderKey(string? path)
    {
        if(string.IsNullOrWhiteSpace(path))return null;
        try{return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));}catch{return null;}
    }
    internal static void Files(string root,string source,LocalSnapshot result,Func<string,IEnumerable<string>>? enumerate=null,string? exclude=null)
    {
        if(!Directory.Exists(root)){result.Statuses.Add(Status(source,"disconnected","폴더를 선택하세요."));return;}
        if(!ReparsePolicy.Safe(root)){result.Statuses.Add(Status(source,"error","폴더 접근을 확인하세요.",0,true));return;}
        var watch=Stopwatch.StartNew();int visited=0;bool limited=false,rootFailed=false;
        var excluded=FolderKey(exclude);
        var candidates=new List<(FileInfo File,long Modified)>();var stack=new Stack<(string Path,int Depth)>();stack.Push((root,0));
        enumerate??=Directory.EnumerateFileSystemEntries;
        while(stack.Count>0&&visited<20000&&watch.Elapsed.TotalSeconds<4)
        {
            var (folder,depth)=stack.Pop();
            try
            {
                foreach(var path in enumerate(folder))
                {
                    if(++visited>20000||watch.Elapsed.TotalSeconds>=4){limited=true;break;}
                    FileAttributes attributes;try{attributes=File.GetAttributes(path);}catch{limited=true;continue;}
                    var name=Path.GetFileName(path);
                    if(name.StartsWith('.')||(attributes&FileAttributes.Hidden)!=0)continue;
                    if(!ReparsePolicy.Safe(path)){limited=true;continue;}
                    if((attributes&FileAttributes.Directory)!=0){
                        if(ExcludedFolders.Contains(name)||excluded!=null&&string.Equals(FolderKey(path),excluded,StringComparison.OrdinalIgnoreCase))continue;
                        if(depth<7)stack.Push((path,depth+1));else limited=true;
                    }
                    else if(Extensions.Contains(Path.GetExtension(path))) {
                        try {
                            var file=new FileInfo(path);
                            if(!PathPolicy.Within(root,file.FullName)){limited=true;continue;}
                            candidates.Add((file,new DateTimeOffset(file.LastWriteTimeUtc).ToUnixTimeSeconds()));
                        }catch{limited=true;}
                    }
                }
            }
            catch {limited=true;rootFailed|=depth==0;}
        }
        limited|=stack.Count>0;
        foreach(var (f,modified) in candidates.OrderByDescending(f=>f.Modified).Take(30)) {
            if(!PathPolicy.Within(root,f.FullName)){limited=true;continue;}
            var id=result.DisplayId("file:"+source+":"+root+"\0"+f.FullName);result.FileMap[id]=new(root,f.FullName);
            result.Files.Add(new(){["id"]=id,["source"]=source,["name"]=f.Name,["parent"]=f.Directory?.Name??"",["modified"]=modified});
        }
        result.Statuses.Add(Status(source,rootFailed?"error":"connected",rootFailed?"폴더 접근을 확인하세요.":limited?"일부 범위만 검색됨 · 폴더 접근/동기화 상태 확인 필요":"최근 수정 파일 연결됨",0,limited));
    }
    static string Text(JsonNode? n)=>n is JsonValue v&&v.TryGetValue<string>(out var s)?s:"";
    internal const int ClaudeProjectLimit=200;
    internal static void Claude(string root,LocalSnapshot result)
    {
        if(!Directory.Exists(root)){result.Statuses.Add(Status("claude","disconnected","Claude Code 폴더를 선택하세요."));return;}
        var watch=Stopwatch.StartNew();var files=new List<FileInfo>();bool limited=false;
        try
        {
            // Directory order is unrelated to recency, so the cap applies to the newest project folders.
            var directories=Directory.EnumerateDirectories(root).Select(folder=>new DirectoryInfo(folder)).OrderByDescending(info=>info.LastWriteTimeUtc).ToList();
            limited|=directories.Count>ClaudeProjectLimit;
            foreach(var dir in directories.Take(ClaudeProjectLimit))
            {
                if(watch.Elapsed.TotalSeconds>4){limited=true;break;}
                if(!ReparsePolicy.Safe(dir.FullName)){limited=true;continue;}
                foreach(var path in Directory.EnumerateFiles(dir.FullName,"*.jsonl"))
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
                var id=result.DisplayId("claude:"+file.FullName);result.AgentMap[id]="claude";
                result.Agents.Add(new(){["id"]=id,["provider"]="claude",["title"]=title,["project"]=project,["updated"]=new DateTimeOffset(file.LastWriteTimeUtc).ToUnixTimeSeconds(),["status"]="최근 기록",["openMode"]=AppLinks.Registered("claude")?"appHome":"unavailable"});
            }
            result.Statuses.Add(Status("claude","connected","로컬 기록 연결됨 · 앱에서 작업 직접 선택",0,limited));
        }
        catch {result.Statuses.Add(Status("claude","error","Claude Code 기록 형식을 확인하세요."));}
    }
}
