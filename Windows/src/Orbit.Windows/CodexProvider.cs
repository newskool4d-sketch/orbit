using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
namespace Orbit;
internal static class CodexProvider
{
    public static void Read(string root,LocalSnapshot result)
    {
        var path=Path.Combine(root,"state_5.sqlite");
        if(!File.Exists(path)){result.Statuses.Add(LocalProviders.Status("codex","disconnected","Codex 폴더를 선택하세요."));return;}
        try
        {
            if(!PathPolicy.Within(root,path))throw new IOException();
            using var db=new ReadSqlite(path);
            var rows=db.Query("SELECT id,COALESCE(NULLIF(name,''),title) AS title,cwd,updated_at,recency_at FROM threads WHERE archived=0 AND source IN ('cli','vscode','exec') AND (thread_source IS NULL OR thread_source='user') AND (agent_path IS NULL OR agent_path='/root') ORDER BY recency_at DESC LIMIT 24");
            foreach(var row in rows)
            {
                if(!Guid.TryParse(row["id"],out _))continue;
                var id=Guid.NewGuid().ToString("N");var title=row["title"];if(title.Length>160)title=title[..160];
                result.AgentMap[id]="codex";
                result.Agents.Add(new(){["id"]=id,["provider"]="codex",["title"]=title,["project"]=Path.GetFileName(Path.TrimEndingDirectorySeparator(row["cwd"])),["updated"]=long.TryParse(row["updated_at"],out var updated)?updated:0,["status"]="최근 기록",["openMode"]=AppLinks.Registered("codex")?"appHome":"unavailable"});
            }
            result.Statuses.Add(LocalProviders.Status("codex","connected","읽기 전용 로컬 기록 · 앱에서 작업 직접 선택"));
        }
        catch {result.Statuses.Add(LocalProviders.Status("codex","error","Codex DB 버전·경로·WAL 상태를 확인하세요."));}
    }
}
