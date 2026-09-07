using System;
using System.Collections.Generic;
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
    public bool LosePatch,FailGet;public int Patches;public string LastBody="";public string Status="needsAction";
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    {
        var path=request.RequestUri!.AbsolutePath;
        string json;
        if(path.EndsWith("/calendarList"))json="{\"items\":[]}";
        else if(path.EndsWith("/users/@me/lists"))json="{\"items\":[{\"id\":\"list\",\"title\":\"fixture\"}]}";
        else if(path.EndsWith("/tasks"))json="{\"items\":[{\"id\":\"task\",\"title\":\"fixture\",\"status\":\""+Status+"\"}]}";
        else if(request.Method==HttpMethod.Patch){Patches++;LastBody=await request.Content!.ReadAsStringAsync(ct);Status=JsonNode.Parse(LastBody)!["status"]!.ToString();if(LosePatch)throw new HttpRequestException("fixture");json="{\"status\":\""+Status+"\"}";}
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
            foreach(var bad in new[]{"{\"action\":\"launchAtLogin\",\"request\":\"1\"}","{\"action\":\"pin\",\"request\":\"1\",\"value\":\"true\"}","{\"action\":\"refresh\",\"request\":\"1\",\"path\":\"fixture\"}","{\"action\":\"refresh\",\"action\":\"quit\",\"request\":\"1\"}","{\"action\":\"openFile\",\"request\":\"1\",\"id\":\"../fixture\"}","{\"action\":\"theme\",\"request\":\"1\",\"value\":\"unknown\"}"})
                Check(!BridgePolicy.Validate(bad,out _),"invalid-message");
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
            Check(!PathPolicy.Https("file:///fixture")&&!PathPolicy.Https("https://user:pass@example.com"),"external-link-policy");

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
    public static int CredentialProbe()
    {
        var store=new Credentials();var key="compat-"+Guid.NewGuid().ToString("N");
        try {store.Write(key,new string('a',2560));bool ok=store.Read(key)?.Length==2560;Console.WriteLine(ok?"PASS credential-native-roundtrip":"FAIL credential-native-roundtrip");return ok?0:1;}
        finally{store.Delete(key);}
    }
}
