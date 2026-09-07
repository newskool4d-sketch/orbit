using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Forms=System.Windows.Forms;
namespace Orbit;
internal static class Program
{
    [STAThread] public static int Main(string[] args) {
        if(args.Length==2 && args[0]=="--sqlite-reader")return SqliteProbe.Child(args[1]);
        if(args.Contains("--sqlite-test"))return SqliteProbe.Run().GetAwaiter().GetResult();
        if(args.Contains("--schema-probe")){foreach(var pair in new[]{("state_5.sqlite","threads"),("thread_history_1.sqlite","thread_turns")}){using var db=new ReadSqlite(Path.Combine(Settings.Load().Codex,pair.Item1));Console.WriteLine(JsonSerializer.Serialize(new {table=pair.Item2,columns=db.Query("PRAGMA table_info("+pair.Item2+")").Select(r=>r["name"])}));}return 0;}
        if(args.Contains("--apps-test"))return AppLinks.Probe();
        if(args.Contains("--credential-test"))return SelfTests.CredentialProbe();
        if(args.Contains("--self-test"))return SelfTests.Run().GetAwaiter().GetResult();
        if(args.Contains("--smoke-test")) {
            var data=LocalProviders.Read(Settings.Load());
            Console.WriteLine(JsonSerializer.Serialize(new {files=data.Files.Count,agents=data.Agents.Count,statuses=data.Statuses.Select(s=>new {source=s["source"]?.ToString(),state=s["state"]?.ToString()})}));
            return 0;
        }
        string key="Orbit.Windows."+WindowsIdentity.GetCurrent().User!.Value+"."+Process.GetCurrentProcess().SessionId;
        using var mutex=new Mutex(true,@"Local\"+key,out bool first);
        if(!first) {
            try {using var client=new NamedPipeClientStream(".",key,PipeDirection.Out,PipeOptions.CurrentUserOnly);client.Connect(2000);client.Write(Encoding.ASCII.GetBytes("show"));return 0;}
            catch{return 2;}
        }
        try {
            var app=new System.Windows.Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
            var window=new OrbitWindow(args.Contains("--ui-smoke"),app,args.Contains("--hold"));
            app.Startup+=(_,_)=>{window.ShowPanel();};
            using var stop=new CancellationTokenSource();
            _=Task.Run(async()=>{
                while(!stop.IsCancellationRequested) {
                    try {
                        using var server=new NamedPipeServerStream(key,PipeDirection.In,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
                        await server.WaitForConnectionAsync(stop.Token);using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);timeout.CancelAfter(2000);
                        byte[] buffer=new byte[5];int n=await server.ReadAsync(buffer,timeout.Token);
                        if(Encoding.ASCII.GetString(buffer,0,n)=="show")_ = app.Dispatcher.BeginInvoke(window.ShowPanel);
                    }catch(OperationCanceledException){}catch{await Task.Delay(100);}
                }
            });
            int result=app.Run();stop.Cancel();return result;
        }catch {Forms.MessageBox.Show("Orbit을 시작하지 못했습니다. WebView2 설치와 앱 파일을 확인하세요.","Orbit");return 1;}
        finally{mutex.ReleaseMutex();}
    }
}
internal sealed class OrbitWindow : Window
{
    readonly WebView2 web=new();
    readonly Forms.NotifyIcon tray;
    readonly System.Drawing.Icon orbitIcon;
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(60)};
    readonly Settings settings;
    readonly Google google;
    readonly System.Windows.Application app;
    readonly bool smoke;
    readonly bool hold;
    int showRequests;
    readonly HashSet<string> pending=new();
    LocalSnapshot local=new();
    bool ready,pinned,modal,refreshing,quitting;
    DateTime lastLocal=DateTime.MinValue;
    public OrbitWindow(bool smoke,System.Windows.Application app,bool hold=false)
    {
        this.smoke=smoke;this.app=app;this.hold=hold;
        settings=new();google=new(smoke?new MemorySecrets():null);
        if(smoke) {
            local.Agents.Add(new(){["id"]=Guid.NewGuid().ToString("N"),["provider"]="codex",["title"]="화면 점검용 작업",["project"]="합성 데이터",["updated"]=DateTimeOffset.UtcNow.ToUnixTimeSeconds(),["status"]="최근 기록",["openMode"]="appHome"});
            local.Agents.Add(new(){["id"]=Guid.NewGuid().ToString("N"),["provider"]="claude",["title"]="새 작업 안내 점검",["project"]="합성 데이터",["updated"]=DateTimeOffset.UtcNow.ToUnixTimeSeconds(),["status"]="최근 기록",["openMode"]="projectNew"});
        }
        Title="Orbit";Width=560;Height=700;ShowInTaskbar=false;ResizeMode=ResizeMode.NoResize;WindowStyle=WindowStyle.None;
        Background=System.Windows.Media.Brushes.White;Content=web;
        var iconPath=Path.Combine(AppContext.BaseDirectory,"Assets","orbit.ico");
        Icon=System.Windows.Media.Imaging.BitmapFrame.Create(new Uri(iconPath));
        orbitIcon=new System.Drawing.Icon(iconPath,SystemInformationIconSize());
        tray=new(){Text="Orbit",Icon=orbitIcon,Visible=!smoke};
        var menu=new Forms.ContextMenuStrip();menu.Items.Add("Orbit 열기",null,(_,_)=>ShowPanel());menu.Items.Add("종료",null,(_,_)=>Exit());tray.ContextMenuStrip=menu;
        tray.MouseClick+=(_,e)=>{if(e.Button==Forms.MouseButtons.Left){if(IsVisible&&!pinned)Hide();else ShowPanel();}};
        Deactivated+=(_,_)=>{if(!pinned&&!modal&&!smoke)Hide();};
        IsVisibleChanged+=(_,_)=>{if(IsVisible){timer.Start();if(ready&&!smoke)_=Refresh(false);}else timer.Stop();};
        timer.Tick+=async(_,_)=>await Refresh(false);
        Loaded+=async(_,_)=>await Initialize();
        Closing+=(_,e)=>{if(!quitting){e.Cancel=true;Hide();}};
    }
    static System.Drawing.Size SystemInformationIconSize()=>Forms.SystemInformation.SmallIconSize;
    public void ShowPanel()
    {
        showRequests++;
        var area=SystemParameters.WorkArea;Width=Math.Min(560,area.Width);Height=Math.Min(700,area.Height);
        Left=area.Right-Width-8;Top=Math.Max(area.Top,area.Bottom-Height-8);Show();Activate();
    }
    async Task Initialize()
    {
        if(ready)return;
        try {
            if(!smoke) {
                var loaded=await Task.Run(Settings.Load);
                settings.Theme=loaded.Theme;settings.Drive=loaded.Drive;settings.Vault=loaded.Vault;settings.Codex=loaded.Codex;settings.Claude=loaded.Claude;
            }
            _=CoreWebView2Environment.GetAvailableBrowserVersionString();
            var profile=smoke?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OrbitDevelopment","qa-profile"):Path.Combine(Settings.DataRoot,"WebView2");
            var env=await CoreWebView2Environment.CreateAsync(null,profile);
            var options=env.CreateCoreWebView2ControllerOptions();options.IsInPrivateModeEnabled=true;
            await web.EnsureCoreWebView2Async(env,options);
            var core=web.CoreWebView2;core.SetVirtualHostNameToFolderMapping("orbit.invalid",Path.Combine(AppContext.BaseDirectory,"Resources"),CoreWebView2HostResourceAccessKind.Deny);
            core.Settings.AreDevToolsEnabled=false;core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.IsPasswordAutosaveEnabled=false;core.Settings.IsGeneralAutofillEnabled=false;core.Settings.AreHostObjectsAllowed=false;core.Settings.AreBrowserAcceleratorKeysEnabled=false;core.Settings.IsStatusBarEnabled=false;
            core.NavigationStarting+=(_,e)=>{if(e.Uri!=BridgePolicy.Document)e.Cancel=true;};
            core.FrameNavigationStarting+=(_,e)=>e.Cancel=true;
            core.NewWindowRequested+=(_,e)=>e.Handled=true;
            core.DownloadStarting+=(_,e)=>e.Cancel=true;
            core.PermissionRequested+=(_,e)=>{e.State=CoreWebView2PermissionState.Deny;e.Handled=true;};
            core.WebMessageReceived+=async(_,e)=>{
                if(!BridgePolicy.Trusted(e.Source,core.Source)||!BridgePolicy.Validate(e.WebMessageAsJson,out var message))return;
                var request=message.GetProperty("request").GetString()!;
                if(!pending.Add(request))return;
                try {await Dispatch(message);Reply(request,null);}
                catch(OperationCanceledException){Reply(request,"작업이 취소되었습니다.");}
                catch {Reply(request,"요청을 완료하지 못했습니다. 연결 상태와 선택 항목을 확인하세요.");}
                finally{pending.Remove(request);}
            };
            core.NavigationCompleted+=async(_,e)=>{
                if(!e.IsSuccess)return;ready=true;Publish();
                if(smoke) {
                    Console.WriteLine("UI_READY");Console.Out.Flush();
                    await Task.Delay(hold?4000:500);
                    var result=await core.ExecuteScriptAsync("JSON.stringify({platform:window.orbitPlatform.name,shortcut:document.querySelector('[data-shortcut]').textContent,loginHidden:document.querySelector('#launch-login-option').hidden,hasReceive:typeof window.orbit.receive==='function',preview:!!document.querySelector('.preview-banner')})");
                    var decoded=JsonSerializer.Deserialize<string>(result)??"{}";
                    var node=JsonNode.Parse(decoded)!;bool pass=node["platform"]?.ToString()=="windows"&&node["shortcut"]?.ToString()=="Ctrl K"&&node["loginHidden"]?.GetValue<bool>()==true&&node["hasReceive"]?.GetValue<bool>()==true&&node["preview"]?.GetValue<bool>()==false;
                    await core.ExecuteScriptAsync("window.__orbitQaAck=false;window.chrome.webview.addEventListener('message',e=>{if(e.data.kind==='reply'&&e.data.request==='qa-roundtrip'&&!e.data.error)window.__orbitQaAck=true;});window.chrome.webview.postMessage({action:'ready',request:'qa-roundtrip'});");
                    bool bridgeAck=false;
                    for(int attempt=0;attempt<20&&!bridgeAck;attempt++){await Task.Delay(50);bridgeAck=await core.ExecuteScriptAsync("window.__orbitQaAck")== "true";}
                    pass&=bridgeAck;
                    var qa=Environment.GetEnvironmentVariable("ORBIT_QA_DIR");
                    if(!string.IsNullOrEmpty(qa)) {
                        Directory.CreateDirectory(qa);
                        foreach(var theme in new[]{"moss","pearl","cobalt"}) {
                            await core.ExecuteScriptAsync("state.theme='"+theme+"';render();");
                            await Task.Delay(100);
                            using var shot=File.Create(Path.Combine(qa,"windows-"+theme+".png"));
                            await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,shot);
                        }
                    }
                    await core.ExecuteScriptAsync("settings();");
                    await Task.Delay(50);
                    var rootsShown=await core.ExecuteScriptAsync("!document.querySelector('#agent-roots').hidden");
                    pass&=rootsShown=="true";
                    if(!string.IsNullOrEmpty(qa)) {
                        using(var shot=File.Create(Path.Combine(qa,"windows-settings.png")))await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,shot);
                        await core.ExecuteScriptAsync("settings(false);tab('agents');");
                        await Task.Delay(50);
                        using(var shot=File.Create(Path.Combine(qa,"windows-agents.png")))await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,shot);
                    }
                    if(hold)pass&=showRequests>=2;
                    Console.WriteLine(JsonSerializer.Serialize(new{test="webview-native-roundtrip",pass,result=node,bridgeAck,singleInstanceShowRequests=showRequests}));
                    Exit(pass?0:1);
                }
            };
            core.Navigate(BridgePolicy.Document);
        } catch (Exception error) {
            if(smoke){Console.WriteLine(JsonSerializer.Serialize(new {test="webview-native-roundtrip",pass=false,errorType=error.GetType().Name,errorCode=error.HResult}));Exit(1);return;}
            Forms.MessageBox.Show("WebView2를 초기화하지 못했습니다. Microsoft Edge WebView2 Runtime 설치 여부와 배포 파일을 확인하세요.","Orbit");
            Exit(1);
        }
    }
    static JsonArray Array(IEnumerable<JsonObject> items)=>new(items.Select(x=>(JsonNode)x.DeepClone()).ToArray());
    void Send(JsonObject message){if(web.CoreWebView2!=null)web.CoreWebView2.PostWebMessageAsJson(message.ToJsonString());}
    void Reply(string request,string? error)=>Send(new(){["kind"]="reply",["request"]=request,["error"]=error});
    void Publish()
    {
        if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(Publish);return;}
        if(!ready)return;
        var statuses=local.Statuses.Select(x=>(JsonObject)x.DeepClone()).ToList();
        foreach(var source in new[]{"calendar","tasks"})statuses.Add(new(){["source"]=source,["state"]=google.Error!=null?"error":google.Connected?"connected":"disconnected",["message"]=google.Error??(google.Connected?"Google 연결됨":"Google 연결이 필요합니다."),["updated"]=google.Updated,["stale"]=google.Error!=null});
        Send(new(){["kind"]="snapshot",["protocolVersion"]=2,["platform"]="windows",
            ["capabilities"]=new JsonObject{["launchAtLogin"]=false,["chooseAgentRoots"]=true,["agentDesktopOpen"]=true},
            ["theme"]=settings.Theme,["pinned"]=pinned,["refreshing"]=refreshing,
            ["files"]=Array(local.Files),["agents"]=Array(local.Agents),["events"]=Array(google.Events),["tasks"]=Array(google.Tasks),["statuses"]=Array(statuses),
            ["settings"]=new JsonObject{["drive"]=Leaf(settings.Drive),["vault"]=Leaf(settings.Vault),["codex"]=Leaf(settings.Codex),["claude"]=Leaf(settings.Claude),["googleConnected"]=google.Connected,["googleConfigured"]=google.Configured,["googleAuthorizing"]=google.Authorizing}});
    }
    static string Leaf(string path)=>string.IsNullOrWhiteSpace(path)?"":Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
    async Task Refresh(bool force)
    {
        if(refreshing||smoke)return;refreshing=true;Publish();
        try {
            if(force||DateTime.UtcNow-lastLocal>TimeSpan.FromSeconds(120)) {
                var captured=JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings))!;
                var result=await Task.Run(()=>LocalProviders.Read(captured));
                if(captured.Drive==settings.Drive&&captured.Vault==settings.Vault&&captured.Codex==settings.Codex&&captured.Claude==settings.Claude){local=result;lastLocal=DateTime.UtcNow;}
            }
            await google.Refresh();
        }finally{refreshing=false;Publish();}
    }
    async Task Dispatch(JsonElement message)
    {
        var action=message.GetProperty("action").GetString();string Id()=>message.GetProperty("id").GetString()!;bool Value()=>message.GetProperty("value").GetBoolean();
        if(smoke){if(action=="ready"){ready=true;Publish();}return;}
        switch(action)
        {
            case "ready":ready=true;await Refresh(false);break;
            case "refresh":await Refresh(true);break;
            case "theme":settings.Theme=message.GetProperty("value").GetString()!;settings.Save();Publish();break;
            case "pin":pinned=Value();Publish();break;
            case "close":Hide();break;
            case "quit":Exit();break;
            case "chooseDrive":case "chooseVault":case "chooseCodex":case "chooseClaude":
                modal=true;
                try {
                    var picker=new Microsoft.Win32.OpenFolderDialog{Title="Orbit에서 읽을 폴더 선택",Multiselect=false};
                    if(picker.ShowDialog(this)==true) {
                        var path=picker.FolderName;
                        if(!ReparsePolicy.Safe(path))throw new InvalidOperationException();
                        switch(action){case "chooseDrive":settings.Drive=path;break;case "chooseVault":settings.Vault=path;break;case "chooseCodex":settings.Codex=path;break;case "chooseClaude":settings.Claude=path;break;}
                        settings.Save();local=new();lastLocal=DateTime.MinValue;Publish();
                    }
                }finally{modal=false;}
                await Refresh(true);break;
            case "openFile":
                if(!local.FileMap.TryGetValue(Id(),out var target)||!PathPolicy.Within(target.Root,target.Path))throw new InvalidOperationException();
                Process.Start(new ProcessStartInfo(target.Path){UseShellExecute=true});Hide();break;
            case "openAgent":if(!local.AgentMap.TryGetValue(Id(),out var provider))throw new InvalidOperationException();AppLinks.Open(provider);Hide();break;
            case "openCalendar":Google.Open("https://calendar.google.com/");break;
            case "openTasks":Google.Open("https://tasks.google.com/");break;
            case "openEvent":
                if(!google.EventMap.TryGetValue(Id(),out var links))throw new InvalidOperationException();
                Google.Open(message.TryGetProperty("meeting",out var meeting)&&meeting.GetBoolean()?links.Meeting:links.Link);break;
            case "importGoogle":
                modal=true;
                try {var picker=new Microsoft.Win32.OpenFileDialog{Title="Desktop OAuth 클라이언트 JSON",Filter="JSON|*.json"};if(picker.ShowDialog(this)==true)await google.Import(picker.FileName);}
                finally{modal=false;Publish();}break;
            case "connectGoogle":await google.Connect(Publish);await Refresh(true);break;
            case "cancelGoogle":google.Cancel();break;
            case "disconnectGoogle":await google.Disconnect();Publish();break;
            case "completeTask":await google.Complete(Id(),Value(),Publish);break;
            case "googleHelp":Google.Open("https://developers.google.com/identity/protocols/oauth2/native-app");break;
        }
    }
    void Exit(int code=0){quitting=true;timer.Stop();tray.Visible=false;tray.Dispose();orbitIcon.Dispose();google.Dispose();web.Dispose();app.Shutdown(code);}
}
