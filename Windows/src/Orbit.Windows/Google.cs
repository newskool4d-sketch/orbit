using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
namespace Orbit;
internal sealed class Google : IDisposable
{
    readonly SecretSession secrets;
    readonly HttpClient http;
    readonly SemaphoreSlim gate=new(1);
    CancellationTokenSource? auth;
    readonly Dictionary<string,(string List,string Task)> ids=new();
    internal readonly Dictionary<string,(string Link,string Meeting)> EventMap=new();
    public List<JsonObject> Events {get;private set;}=[];
    public List<JsonObject> Tasks {get;private set;}=[];
    public string? Error {get;private set;}
    public double Updated {get;private set;}
    public bool Authorizing=>auth!=null;
    public bool Configured=>secrets.Get("clientId")!=null;
    public bool Connected=>secrets.Get("refresh")!=null;
    public Google(ISecrets? store=null,HttpMessageHandler? handler=null) {
        secrets=new(store??new Credentials());
        http=new(handler??new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(20)};
    }
    static string S(JsonNode? n)=>n is JsonValue v&&v.TryGetValue<string>(out var s)?s:"";
    void Clear(){Events=[];Tasks=[];ids.Clear();EventMap.Clear();Updated=0;Error=null;}
    public async Task Import(string path) {
        if(new FileInfo(path).Length>65536)throw new InvalidOperationException("OAuth JSON 파일 크기를 확인하세요.");
        var c=JsonNode.Parse(await File.ReadAllTextAsync(path))?["installed"];
        var id=S(c?["client_id"]);var secret=S(c?["client_secret"]);
        if(!id.EndsWith(".apps.googleusercontent.com",StringComparison.Ordinal)||string.IsNullOrWhiteSpace(secret))throw new InvalidOperationException("Desktop app OAuth JSON이 필요합니다.");
        await gate.WaitAsync();try{secrets.Replace(new(){{"clientId",id},{"clientSecret",secret}});Clear();}finally{gate.Release();}
    }
    public void Cancel()=>auth?.Cancel();
    public async Task Disconnect() {
        Cancel();await gate.WaitAsync();
        try{var values=secrets.Values();foreach(var key in new[]{"access","refresh","expiry"})values.Remove(key);secrets.Replace(values);Clear();}
        finally{gate.Release();}
    }
    public async Task Connect(Action changed) {
        if(Authorizing)throw new InvalidOperationException("이미 로그인 중입니다.");
        await gate.WaitAsync();
        try {
            if(!Configured)throw new InvalidOperationException("OAuth JSON을 먼저 가져오세요.");
            auth=new(TimeSpan.FromSeconds(180));changed();var ct=auth.Token;
            var verifier=OAuthPolicy.Base64Url(RandomNumberGenerator.GetBytes(32));var state=OAuthPolicy.Base64Url(RandomNumberGenerator.GetBytes(32));
            var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
            try {
                var port=((IPEndPoint)listener.LocalEndpoint).Port;var redirect=$"http://127.0.0.1:{port}/callback";
                var q=new Dictionary<string,string>{{"client_id",secrets.Get("clientId")!},{"redirect_uri",redirect},{"response_type","code"},{"scope","https://www.googleapis.com/auth/calendar.readonly https://www.googleapis.com/auth/tasks"},{"access_type","offline"},{"prompt","consent"},{"state",state},{"code_challenge",OAuthPolicy.Challenge(verifier)},{"code_challenge_method","S256"}};
                Open("https://accounts.google.com/o/oauth2/v2/auth?"+string.Join("&",q.Select(p=>Uri.EscapeDataString(p.Key)+"="+Uri.EscapeDataString(p.Value))));
                string? code=null;
                while(code==null) {
                    using var client=await listener.AcceptTcpClientAsync(ct);using var stream=client.GetStream();
                    using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(5));
                    try {
                        var bytes=new List<byte>();var buffer=new byte[1024];bool ended=false;
                        while(bytes.Count<16384) {int n=await stream.ReadAsync(buffer,deadline.Token);if(n==0)break;bytes.AddRange(buffer.AsSpan(0,n).ToArray());if(Encoding.ASCII.GetString(bytes.ToArray()).Contains("\r\n\r\n")){ended=true;break;}}
                        var lines=Encoding.ASCII.GetString(bytes.ToArray()).Split("\r\n");var first=lines[0].Split(' ');
                        var hosts=lines.Where(l=>l.StartsWith("Host:",StringComparison.OrdinalIgnoreCase)).ToArray();
                        if(!ended||bytes.Count>16384||first.Length!=3||first[0]!="GET"||!first[1].StartsWith("/callback?",StringComparison.Ordinal)||hosts.Length!=1||hosts[0][5..].Trim()!=$"127.0.0.1:{port}")continue;
                        var parameters=OAuthPolicy.Parameters(first[1]["/callback".Length..]);
                        if(!parameters.TryGetValue("state",out var actual)||actual!=state)continue;
                        if(parameters.ContainsKey("error"))throw new InvalidOperationException("Google 로그인이 취소되었습니다.");
                        if(!parameters.TryGetValue("code",out var found)||string.IsNullOrWhiteSpace(found))continue;
                        code=found;const string body="Orbit authentication received. Return to Orbit.";
                        await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nConnection: close\r\nContent-Length: {body.Length}\r\n\r\n{body}"),ct);
                    }catch(OperationCanceledException)when(!ct.IsCancellationRequested){continue;}catch(InvalidDataException){continue;}catch(IOException){continue;}catch(SocketException){continue;}
                }
                var tokens=await Token(new(){{"grant_type","authorization_code"},{"code",code},{"redirect_uri",redirect},{"code_verifier",verifier}},ct);
                var scope=S(tokens["scope"]).Split(' ');
                if(!scope.Contains("https://www.googleapis.com/auth/tasks")||!scope.Contains("https://www.googleapis.com/auth/calendar.readonly"))throw new InvalidOperationException("필수 Google 권한이 승인되지 않았습니다.");
                SaveTokens(tokens,true);Clear();
            }finally{listener.Stop();}
        }finally{auth?.Dispose();auth=null;gate.Release();changed();}
    }
    async Task<JsonObject> Token(Dictionary<string,string> values,CancellationToken ct=default) {
        values["client_id"]=secrets.Get("clientId")!;values["client_secret"]=secrets.Get("clientSecret")!;
        using var response=await http.PostAsync("https://oauth2.googleapis.com/token",new FormUrlEncodedContent(values),ct);
        if(!response.IsSuccessStatusCode)throw new InvalidOperationException("Google 인증을 갱신하지 못했습니다. 다시 연결하세요.");
        return JsonNode.Parse(await response.Content.ReadAsStringAsync(ct)) as JsonObject??throw new InvalidDataException();
    }
    void SaveTokens(JsonObject tokens,bool fresh) {
        var values=secrets.Values();var refresh=S(tokens["refresh_token"]);var access=S(tokens["access_token"]);
        if(access.Length==0||fresh&&refresh.Length==0)throw new InvalidOperationException("Google 토큰 응답이 불완전합니다.");
        values["access"]=access;if(refresh.Length>0)values["refresh"]=refresh;
        values["expiry"]=(DateTimeOffset.UtcNow.ToUnixTimeSeconds()+(tokens["expires_in"]?.GetValue<int>()??3600)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        secrets.Replace(values);
    }
    async Task<JsonObject> Api(string path,HttpMethod? method=null,JsonObject? body=null) {
        if(!long.TryParse(secrets.Get("expiry"),out var expiry)||expiry<DateTimeOffset.UtcNow.ToUnixTimeSeconds()+60)SaveTokens(await Token(new(){{"grant_type","refresh_token"},{"refresh_token",secrets.Get("refresh")??throw new InvalidOperationException("Google 연결이 필요합니다.")}}),false);
        using var request=new HttpRequestMessage(method??HttpMethod.Get,"https://www.googleapis.com/"+path);
        request.Headers.Authorization=new("Bearer",secrets.Get("access"));
        if(body!=null)request.Content=new StringContent(body.ToJsonString(),Encoding.UTF8,"application/json");
        using var response=await http.SendAsync(request);
        if(!response.IsSuccessStatusCode)throw new InvalidOperationException($"Google 요청 실패 ({(int)response.StatusCode}).");
        return JsonNode.Parse(await response.Content.ReadAsStringAsync()) as JsonObject??new();
    }
    async Task<List<JsonObject>> Pages(string path) {
        var items=new List<JsonObject>();var seen=new HashSet<string>();string? token=null;
        for(int page=0;page<100;page++) {
            var data=await Api(path+(path.Contains('?')?"&":"?")+"maxResults=100"+(token==null?"":"&pageToken="+Uri.EscapeDataString(token)));
            if(data["items"] is JsonArray array)items.AddRange(array.OfType<JsonObject>());
            token=S(data["nextPageToken"]);if(token.Length==0)return items;if(!seen.Add(token))throw new InvalidOperationException("Google 페이지 반복 오류");
        }
        throw new InvalidOperationException("Google 조회 범위 초과");
    }
    static double Stamp(JsonNode? n)=>DateTimeOffset.TryParse(S(n),out var dt)?dt.ToUnixTimeSeconds():0;
    public async Task Refresh() {
        if(Authorizing)return;
        await gate.WaitAsync();
        try {
            if(!Connected){Clear();return;}
            var events=new List<JsonObject>();var links=new Dictionary<string,(string,string)>();
            foreach(var calendar in (await Pages("calendar/v3/users/me/calendarList")).Where(c=>c["selected"]?.GetValue<bool>()==true)) {
                var start=Uri.EscapeDataString(new DateTimeOffset(DateTime.Today).ToString("o"));var end=Uri.EscapeDataString(new DateTimeOffset(DateTime.Today.AddDays(7)).ToString("o"));
                foreach(var e in await Pages($"calendar/v3/calendars/{Uri.EscapeDataString(S(calendar["id"]))}/events?singleEvents=true&orderBy=startTime&timeMin={start}&timeMax={end}")) {
                    if(S(e["status"])=="cancelled")continue;
                    if(e["attendees"] is JsonArray attendees&&attendees.OfType<JsonObject>().Any(a=>a["self"]?.GetValue<bool>()==true&&S(a["responseStatus"])=="declined"))continue;
                    var id=Guid.NewGuid().ToString("N");var meeting=S(e["hangoutLink"]);links[id]=(S(e["htmlLink"]),meeting);
                    events.Add(new(){["id"]=id,["title"]=S(e["summary"]),["calendar"]=S(calendar["summary"]),["start"]=Stamp(e["start"]?["dateTime"]??e["start"]?["date"]),["end"]=Stamp(e["end"]?["dateTime"]??e["end"]?["date"]),["allDay"]=e["start"]?["date"]!=null,["hasMeeting"]=PathPolicy.Https(meeting)});
                }
            }
            var tasks=new List<JsonObject>();var map=new Dictionary<string,(string,string)>();
            foreach(var list in await Pages("tasks/v1/users/@me/lists"))
            foreach(var task in await Pages($"tasks/v1/lists/{Uri.EscapeDataString(S(list["id"]))}/tasks?showCompleted=true&showHidden=true")) {
                if(task["deleted"]?.GetValue<bool>()==true)continue;
                var key=(S(list["id"]),S(task["id"]));var id=ids.FirstOrDefault(p=>p.Value==key).Key??Guid.NewGuid().ToString("N");map[id]=key;var due=S(task["due"]);
                tasks.Add(new(){["id"]=id,["title"]=S(task["title"]),["list"]=S(list["title"]),["due"]=due.Length>=10?due[..10]:"",["completed"]=S(task["status"])=="completed",["mutationState"]="idle"});
            }
            Events=events.OrderBy(e=>e["start"]!.GetValue<double>()).ToList();Tasks=tasks;ids.Clear();foreach(var (id,key) in map)ids[id]=key;
            EventMap.Clear();foreach(var (id,link) in links)EventMap[id]=link;Updated=DateTimeOffset.UtcNow.ToUnixTimeSeconds();Error=null;
        }catch{Error="Google 동기화 실패 · 마지막 데이터 유지. 연결 상태를 확인하세요.";}finally{gate.Release();}
    }
    public async Task Complete(string id,bool completed,Action changed) {
        if(Authorizing)throw new InvalidOperationException("로그인 중에는 변경할 수 없습니다.");
        await gate.WaitAsync();
        try {
            if(!ids.TryGetValue(id,out var key))throw new InvalidOperationException("목록을 새로고침하세요.");
            var task=Tasks.First(t=>S(t["id"])==id);if(S(task["mutationState"]) is "pending" or "unknown")throw new InvalidOperationException("서버 반영 여부를 먼저 확인하세요.");
            task["mutationState"]="pending";changed();var path=$"tasks/v1/lists/{Uri.EscapeDataString(key.List)}/tasks/{Uri.EscapeDataString(key.Task)}";
            try {
                JsonObject result;
                try{result=await Api(path,HttpMethod.Patch,completed?new(){["status"]="completed"}:new(){["status"]="needsAction",["completed"]=null});}
                catch{result=await Api(path);}
                task["completed"]=S(result["status"])=="completed";task["mutationState"]="idle";
            }catch{task["mutationState"]="unknown";throw new InvalidOperationException("반영 여부 확인 중입니다. 새로고침하세요.");}finally{changed();}
        }finally{gate.Release();}
    }
    public static void Open(string url) {
        if(!PathPolicy.Https(url))throw new InvalidOperationException("허용되지 않은 링크입니다.");
        Process.Start(new ProcessStartInfo(url){UseShellExecute=true});
    }
    public void Dispose(){Cancel();http.Dispose();}
}
