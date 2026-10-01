using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
namespace Orbit;
internal sealed class MemorySecrets : ISecrets
{
    public readonly Dictionary<string,string> Data=new();
    public int FailAt=-1;int writes;
    public string? Read(string key)=>Data.GetValueOrDefault(key);
    public void Write(string key,string value){if(++writes==FailAt)throw new IOException("fixture");Credentials.Encode(value);Data[key]=value;}
    public void Delete(string key)=>Data.Remove(key);
}
internal sealed class FakeHttp : HttpMessageHandler
{
    public bool LosePatch,FailGet,RejectPatch,LosePatchWithoutWrite,IncludeCalendar,FailCalendar,FailTasks;
    public int Patches,TaskReads;public string LastBody="",Status="needsAction",CalendarEvents="{\"items\":[]}";
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    {
        var path=request.RequestUri!.AbsolutePath;
        string json;
        if(path.EndsWith("/calendarList")) {
            if(FailCalendar)return new(HttpStatusCode.Forbidden){Content=new StringContent("{}")};
            json=IncludeCalendar?"{\"items\":[{\"id\":\"calendar\",\"summary\":\"fixture\",\"selected\":true}]}":"{\"items\":[]}";
        }
        else if(path.EndsWith("/events"))json=CalendarEvents;
        else if(path.EndsWith("/users/@me/lists")) {
            TaskReads++;if(FailTasks)return new(HttpStatusCode.Forbidden){Content=new StringContent("{}")};
            json="{\"items\":[{\"id\":\"list\",\"title\":\"fixture\"}]}";
        }
        else if(path.EndsWith("/tasks"))json="{\"items\":[{\"id\":\"task\",\"title\":\"fixture\",\"status\":\""+Status+"\"}]}";
        else if(request.Method==HttpMethod.Patch) {
            Patches++;LastBody=await request.Content!.ReadAsStringAsync(ct);
            if(RejectPatch)return new(HttpStatusCode.Forbidden){Content=new StringContent("{}")};
            if(LosePatchWithoutWrite)throw new HttpRequestException("fixture");
            Status=JsonNode.Parse(LastBody)!["status"]!.ToString();if(LosePatch)throw new HttpRequestException("fixture");json="{\"status\":\""+Status+"\"}";
        }
        else {if(FailGet)throw new HttpRequestException("fixture");json="{\"status\":\""+Status+"\"}";}
        return new(HttpStatusCode.OK){Content=new StringContent(json,Encoding.UTF8,"application/json")};
    }
}
internal static class SelfTests
{
    static int count;
    static void Check(bool pass,string name){if(!pass)throw new InvalidOperationException(name);count++;Console.WriteLine("PASS "+name);}
    public static async Task<int> Run()
    {
        try
        {
            Check(BridgePolicy.Trusted(BridgePolicy.Document,BridgePolicy.Document),"trusted-document");
            foreach(var bad in new[]{"https://orbit.invalid/","https://orbit.invalid/index.html?x=1","https://orbit.invalid/index.html#x","https://example.com/index.html","about:blank"})
                Check(!BridgePolicy.Trusted(bad,BridgePolicy.Document),"untrusted-document");
            Check(BridgePolicy.Validate("{\"action\":\"refresh\",\"request\":\"1\"}",out _),"valid-message");
            Check(BridgePolicy.Validate("{\"action\":\"textSize\",\"request\":\"1\",\"value\":\"large\"}",out _),"valid-text-size");
            Check(BridgePolicy.Validate("{\"action\":\"addDday\",\"request\":\"1\",\"title\":\"fixture\",\"targetDate\":\"2028-02-29\",\"pinned\":true}",out _),"valid-dday-message");
            foreach(var bad in new[]{"{\"action\":\"launchAtLogin\",\"request\":\"1\"}","{\"action\":\"pin\",\"request\":\"1\",\"value\":\"true\"}","{\"action\":\"refresh\",\"request\":\"1\",\"path\":\"fixture\"}","{\"action\":\"refresh\",\"action\":\"quit\",\"request\":\"1\"}","{\"action\":\"openFile\",\"request\":\"1\",\"id\":\"../fixture\"}","{\"action\":\"theme\",\"request\":\"1\",\"value\":\"unknown\"}","{\"action\":\"textSize\",\"request\":\"1\",\"value\":\"huge\"}"})
                Check(!BridgePolicy.Validate(bad,out _),"invalid-message");
            Check(!BridgePolicy.Validate("{\"action\":\"addDday\",\"request\":\"1\",\"title\":\"fixture\",\"targetDate\":\"2027-02-29\",\"pinned\":false}",out _),"invalid-dday-date");
            Check(!BridgePolicy.Validate(new string('x',65537),out _),"message-size");
            Check(OAuthPolicy.Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk")=="E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM","pkce-vector");
            bool rejected=false;try{OAuthPolicy.Parameters("?state=a&state=b");}catch(InvalidDataException){rejected=true;}Check(rejected,"duplicate-state");
            Check(Credentials.Encode(new string('a',2560)).Length==2560,"credential-boundary");
            rejected=false;try{Credentials.Encode(new string('한',854));}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected,"credential-utf8-boundary");
            var memory=new MemorySecrets();var session=new SecretSession(memory);
            session.Replace(new(){{"clientId","old"}});memory.FailAt=4;
            try{session.Replace(new(){{"clientId","new"},{"clientSecret","new"}});}catch(IOException){}
            Check(session.Get("clientId")=="old","credential-atomic-failure");
            var store=new MemorySecrets();new SecretSession(store).Replace(new(){{"clientId","fixture"},{"clientSecret","fixture"},{"refresh","fixture"},{"access","fixture"},{"expiry",DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString()}});
            var fake=new FakeHttp();using var google=new Google(store,fake);await google.Refresh();
            Check(google.Tasks.Count==1&&google.Error==null,"google-read-fixture");
            var id=google.Tasks[0]["id"]!.ToString();await google.Complete(id,true,()=>{});
            Check(google.Tasks[0]["completed"]!.GetValue<bool>(),"task-complete");
            await google.Complete(id,false,()=>{});
            var body=JsonNode.Parse(fake.LastBody)!;
            Check(body["status"]!.ToString()=="needsAction"&&body.AsObject().ContainsKey("completed")&&body["completed"]==null,"task-reopen-null");
            fake.LosePatch=true;await google.Complete(id,true,()=>{});
            Check(google.Tasks[0]["completed"]!.GetValue<bool>()&&google.Tasks[0]["mutationState"]!.ToString()=="idle","lost-response-reconcile");
            fake.FailGet=true;try{await google.Complete(id,false,()=>{});}catch(InvalidOperationException){}
            Check(google.Tasks[0]["mutationState"]!.ToString()=="unknown","ambiguous-write-lock");
            var before=fake.Patches;try{await google.Complete(id,true,()=>{});}catch(InvalidOperationException){}
            Check(before==fake.Patches,"no-repeat-unknown-write");
            await GoogleRegressionTests(store);
            Check(!PathPolicy.Https("file:///fixture")&&!PathPolicy.Https("https://user:pass@example.com"),"external-link-policy");

            DdaySelfTests.Run(Check);

            Check(ReparsePolicy.Cloud(0x9000001A)&&ReparsePolicy.Cloud(0x9000F01A)&&!ReparsePolicy.Cloud(0xA000000C),"cloud-tag-vs-symlink");
            var fixtureRoot=Path.Combine(Path.GetTempPath(),"Orbit-files-fixture-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(fixtureRoot);
            try {
                var document=Path.Combine(fixtureRoot,"한글 문서.md");File.WriteAllText(document,"fixture");
                File.WriteAllText(Path.Combine(fixtureRoot,"blocked.exe"),"fixture");
                Check(PathPolicy.Within(fixtureRoot,document),"file-root-boundary");
                Check(!PathPolicy.Within(fixtureRoot+"-other",document),"file-root-prefix-rejected");
                var files=LocalProviders.Read(new Settings{Drive=fixtureRoot,Vault="",Codex="",Claude=""});
                Check(files.Files.Count==1&&files.Files[0]["name"]!.ToString()=="한글 문서.md","file-metadata-filter");
            }finally{foreach(var p in Directory.GetFiles(fixtureRoot))File.Delete(p);Directory.Delete(fixtureRoot);}
Console.WriteLine($"RESULT passed={count} failed=0");return 0;
        }catch(Exception e){Console.WriteLine($"RESULT passed={count} failed=1 test={e.Message}");return 1;}
    }
    static JsonObject CalendarEvent(string title="fixture")=>new()
    {
        ["id"]="event",["summary"]=title,["htmlLink"]="https://example.com/event",
        ["start"]=new JsonObject{["dateTime"]="2030-01-02T09:00:00Z"},
        ["end"]=new JsonObject{["dateTime"]="2030-01-02T10:00:00Z"}
    };
    static string CalendarRows(params JsonObject[] events)=>new JsonObject{["items"]=new JsonArray(events.Select(e=>(JsonNode)e.DeepClone()).ToArray())}.ToJsonString();
    static async Task GoogleRegressionTests(MemorySecrets store)
    {
        var denied=new FakeHttp{RejectPatch=true};using var deniedClient=new Google(store,denied);
        await deniedClient.Refresh();var id=deniedClient.Tasks[0]["id"]!.ToString();
        bool rejected=false;try{await deniedClient.Complete(id,true,()=>{});}catch(InvalidOperationException){rejected=true;}
        Check(rejected&&!deniedClient.Tasks[0]["completed"]!.GetValue<bool>()&&deniedClient.Tasks[0]["mutationState"]!.ToString()=="idle","rejected-complete-reports-failure-and-server-state");
        denied.Status="completed";await deniedClient.Refresh();
        rejected=false;try{await deniedClient.Complete(id,false,()=>{});}catch(InvalidOperationException){rejected=true;}
        Check(rejected&&deniedClient.Tasks[0]["completed"]!.GetValue<bool>()&&deniedClient.Tasks[0]["mutationState"]!.ToString()=="idle","rejected-reopen-reports-failure-and-server-state");
        denied.RejectPatch=false;await deniedClient.Complete(id,false,()=>{});
        Check(!deniedClient.Tasks[0]["completed"]!.GetValue<bool>(),"retry-after-confirmed-write-failure");
        denied.LosePatchWithoutWrite=true;
        rejected=false;try{await deniedClient.Complete(id,true,()=>{});}catch(InvalidOperationException){rejected=true;}
        Check(rejected&&!deniedClient.Tasks[0]["completed"]!.GetValue<bool>()&&deniedClient.Tasks[0]["mutationState"]!.ToString()=="idle","lost-request-with-unchanged-server-is-not-success");

        var partial=new FakeHttp{IncludeCalendar=true,CalendarEvents=CalendarRows(CalendarEvent("before"))};
        using var partialClient=new Google(store,partial);await partialClient.Refresh();
        var oldTaskUpdated=partialClient.TasksUpdated;var oldTaskId=partialClient.Tasks[0]["id"]!.ToString();
        partial.FailTasks=true;partial.CalendarEvents=CalendarRows(CalendarEvent("after"));
        var published=new List<bool>();await partialClient.Refresh(()=>published.Add(partialClient.Events[0]["title"]!.ToString()=="after"&&partialClient.EventMap.ContainsKey(partialClient.Events[0]["id"]!.ToString())));
        Check(published.Count==2&&published.All(fresh=>fresh),"partial-refresh-publishes-matching-event-and-link-state");
        Check(partialClient.Events[0]["title"]!.ToString()=="after"&&partialClient.CalendarError==null&&partialClient.TasksError!=null,"tasks-failure-does-not-block-calendar");
        Check(partialClient.CalendarUpdated>0&&partialClient.TasksUpdated==oldTaskUpdated&&partialClient.Tasks[0]["id"]!.ToString()==oldTaskId,"tasks-failure-keeps-last-data-and-success-time");
        Check(partialClient.EventMap.ContainsKey(partialClient.Events[0]["id"]!.ToString()),"calendar-link-map-updates-on-partial-success");
        var oldEventId=partialClient.Events[0]["id"]!.ToString();var oldCalendarUpdated=partialClient.CalendarUpdated;
        partial.FailTasks=false;partial.FailCalendar=true;partial.Status="completed";await partialClient.Refresh();
        Check(partialClient.Tasks[0]["completed"]!.GetValue<bool>()&&partialClient.TasksError==null&&partialClient.CalendarError!=null,"calendar-failure-does-not-block-tasks");
        Check(partialClient.CalendarUpdated==oldCalendarUpdated&&partialClient.Events[0]["id"]!.ToString()==oldEventId&&partialClient.EventMap.ContainsKey(oldEventId),"calendar-failure-keeps-last-data-links-and-success-time");
        await partialClient.Complete(oldTaskId,false,()=>{});
        Check(!partialClient.Tasks[0]["completed"]!.GetValue<bool>(),"task-id-map-remains-usable-during-calendar-failure");
        partial.FailCalendar=false;await partialClient.Refresh();
        Check(partialClient.CalendarError==null&&partialClient.TasksError==null,"source-errors-clear-after-recovery");
        var firstTasksFail=new FakeHttp{IncludeCalendar=true,FailTasks=true,CalendarEvents=CalendarRows(CalendarEvent())};
        using var firstTasksClient=new Google(store,firstTasksFail);await firstTasksClient.Refresh();
        Check(firstTasksClient.Events.Count==1&&firstTasksClient.Tasks.Count==0&&firstTasksClient.CalendarUpdated>0&&firstTasksClient.TasksUpdated==0,"first-refresh-tasks-failure-keeps-calendar");
        var firstCalendarFail=new FakeHttp{FailCalendar=true};using var firstCalendarClient=new Google(store,firstCalendarFail);await firstCalendarClient.Refresh();
        Check(firstCalendarFail.TaskReads==1&&firstCalendarClient.Tasks.Count==1&&firstCalendarClient.CalendarUpdated==0&&firstCalendarClient.TasksUpdated>0,"first-refresh-calendar-failure-still-reads-tasks");

        var culture=CultureInfo.CurrentCulture;
        var expectedDay=new DateTimeOffset(new DateTime(2030,1,2,0,0,0,DateTimeKind.Local)).ToUnixTimeSeconds();
        var expectedTime=new DateTimeOffset(2030,1,2,0,0,0,TimeSpan.Zero).ToUnixTimeSeconds();
        try {
            foreach(var name in new[]{"ko-KR","th-TH","ar-SA"}) {
                CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(name);
                Check(Google.TryStamp(JsonValue.Create("2030-01-02"),true,out var day)&&day==expectedDay,"all-day-date-culture-independent-"+name);
                Check(Google.TryStamp(JsonValue.Create("2030-01-02T09:00:00+09:00"),false,out var stamp)&&stamp==expectedTime,"event-offset-culture-independent-"+name);
            }
        }finally{CultureInfo.CurrentCulture=culture;}
        Check(!Google.TryStamp(JsonValue.Create("2030-02-30"),true,out _)&&!Google.TryStamp(JsonValue.Create("not-a-date"),false,out _),"invalid-calendar-dates-rejected");
        var invalid=CalendarEvent();invalid["start"]=new JsonObject{["date"]="2030-02-30"};invalid["end"]=new JsonObject{["date"]="2030-03-01"};
        partial.CalendarEvents=CalendarRows(invalid,CalendarEvent());await partialClient.Refresh();
        Check(partialClient.Events.Count==1&&partialClient.CalendarError==null,"invalid-event-date-does-not-render-epoch");

        foreach(var (url,allowed) in new[]{("https://example.com/video",true),("http://example.com/video",false),("javascript:bad",false),("https://user:pass@example.com/video",false)}) {
            var e=CalendarEvent();e["conferenceData"]=new JsonObject{["entryPoints"]=new JsonArray(new JsonObject{["entryPointType"]="phone",["uri"]="tel:+10000000000"},new JsonObject{["entryPointType"]="video",["uri"]=url})};
            partial.CalendarEvents=CalendarRows(e);await partialClient.Refresh();var current=partialClient.Events[0];
            Check(current["hasMeeting"]!.GetValue<bool>()==allowed&&partialClient.EventMap[current["id"]!.ToString()].Meeting==(allowed?url:""),"conference-entry-point-https-policy");
        }
        var preferred=CalendarEvent();preferred["hangoutLink"]="https://example.com/primary";
        preferred["conferenceData"]=new JsonObject{["entryPoints"]=new JsonArray(new JsonObject{["entryPointType"]="video",["uri"]="https://example.com/fallback"})};
        partial.CalendarEvents=CalendarRows(preferred);await partialClient.Refresh();
        Check(partialClient.EventMap[partialClient.Events[0]["id"]!.ToString()].Meeting=="https://example.com/primary","hangout-link-remains-preferred");
        partial.CalendarEvents=CalendarRows(CalendarEvent());await partialClient.Refresh();
        Check(!partialClient.Events[0]["hasMeeting"]!.GetValue<bool>(),"event-without-meeting-remains-calendar-only");
    }
    public static int CredentialProbe()
    {
        var store=new Credentials();var key="compat-"+Guid.NewGuid().ToString("N");
        try {store.Write(key,new string('a',2560));bool ok=store.Read(key)?.Length==2560;Console.WriteLine(ok?"PASS credential-native-roundtrip":"FAIL credential-native-roundtrip");return ok?0:1;}
        finally{store.Delete(key);}
    }
}
