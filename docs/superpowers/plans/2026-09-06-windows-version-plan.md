# Orbit Windows 착수·실행 계획

> **For agentic workers:** superpowers:executing-plans로 아래 작업을 인라인 실행한다. 프로젝트 지침에 따라 하위 에이전트 위임은 하지 않는다. 이 문서는 계획이며 체크되지 않은 항목은 구현·검증 완료를 뜻하지 않는다.

**Goal:** 로그인 자동실행을 제외한 Orbit 기능을 Windows 11 x64 트레이 앱으로 제공하고 Codex·Claude 작업을 데스크톱 앱으로 연다.

**Architecture:** Windows/ 아래의 WPF 네이티브 호스트가 공유 Resources/를 WebView2로 표시한다. 파일·공급자 메타데이터·Google API·자격 증명·앱 열기는 네이티브에서 처리하고 화면에는 표시 데이터만 전달한다. macOS 구현을 별도로 유지한다.

**Tech Stack:** C# 14, .NET 10 LTS, WPF, NotifyIcon, Microsoft WebView2, Windows winsqlite3.dll, Credential Manager, Node 내장 테스트, Microsoft .NET 테스트 도구.

**Spec:** [재수립 설계서](../specs/2026-09-06-windows-version-design.md). 두 문서를 함께 읽고 실행한다.

## Global Constraints

- 대상은 Windows 11 x64·일반 사용자 권한이다.
- 로그인 자동실행 UI 및 Startup 폴더·Run 레지스트리·예약 작업 등록을 제외한다.
- AI 연결의 Terminal·PowerShell·CLI 실행, resume 명령 복사, 자동 메시지 전송을 제외한다.
- 기존 Moss·Pearl·Midnight 외관을 유지하고 Midnight 내부 키 cobalt를 바꾸지 않는다.
- 공급자 저장소는 읽기 전용이다. 문서 본문을 수집하지 않는다.
- 비밀은 Windows Credential Manager에만 영속 저장하며 WebView·settings.json·진단에 전달하지 않는다.
- 제3자 실행 런타임·로컬 UI 웹 서버를 추가하지 않는다. 테스트 전용 Microsoft 패키지는 배포하지 않는다.
- .NET 포함 self-contained win-x64 폴더 ZIP을 배포하며 WebView2 존재를 확인한다.
- Store·MSIX·공개 코드 서명·자동 업데이트는 v1에서 제외한다.
- 인라인 실행만 한다. 기존 사용자 변경을 보존하고 구현과 무관한 macOS 리팩터링은 하지 않는다.
- 실제 계정 쓰기 시험은 사용자가 지정한 테스트 항목만 대상으로 한다. 계획 승인을 임의의 실데이터 변경 승인으로 확대하지 않는다.

---

## 1. 현재 상태와 착수 판단

2026-09-06 현재 저장소에는 Swift macOS 구현과 공유 웹 리소스가 있고 Windows/ 구현은 없다. 이번 작업에서는 설계서와 계획만 작성한다. **Windows 빌드·앱 링크·Google 실계정 시험은 실행하지 않았다.**

기존 구현에서 그대로 복제하지 않을 부분:

| 기존 위치 | 이식 시 처리 |
|---|---|
| Resources/app.js의 send, ready, renderSettings, agentCard | WebView2 전송, capabilities, Windows 문구·버튼, 열기 방식 표시 추가 |
| Resources/index.html의 ⌘ K·자동실행·Terminal 안내 | 플랫폼별 표시. macOS 기존 기능 유지 |
| Sources/Bridge.swift의 snapshot | 선택적 protocolVersion/platform/capabilities를 추가하는 범위만 변경 |
| Sources/Models.swift의 display 경계 | Windows 이벤트/작업까지 내부 ID·URL과 표시 데이터를 분리 |
| Sources/LocalProviders.swift의 공급자 처리 | 의미는 유지하되 Windows 스키마·WAL·경로를 선행 검증 |
| Sources/GoogleClient.swift의 saveTokens | 신규 로그인과 기존 연결 refresh 경로 분리 |
| Sources/GoogleClient.swift의 showHidden=false | Windows는 showHidden=true로 공식 화면 완료 항목도 표시 |
| Google 변경 후 갱신 처리 | 계정 세대·쓰기 순서·응답 유실 재조회 추가 |

위 Swift의 기존 문제를 전면 수정하는 작업은 이번 Windows 계획에 포함하지 않는다. 공유 리소스를 바꾸는 데 필요한 호환성 수정과 macOS 회귀 검증은 포함한다.

## 2. 단계·의존성·공수

| 작업 | 산출물 | 선행 조건 | 순공수 |
|---|---|---|---:|
| T0 | Windows 호환성 증거와 고정 도구 버전 | Windows 11 x64 시험 환경 | 2일 |
| T1 | 프로젝트·공유 계약·오프라인 시험 기반 | G0-A, G0-C의 스키마 결정 | 1일 |
| T2 | 트레이·창·단일 인스턴스·보안 브리지 | T1, G0-B | 1–2일 |
| T3 | 공유 UI·Windows 설정·macOS 호환성 | T2 | 1일 |
| T4 | Drive·Obsidian·안전한 열기·캐시 세대 | T3, G0-F | 1–2일 |
| T5 | Codex·Claude 읽기와 검증된 앱 연결 | T4, G0-C, G0-D | 1–2일 |
| T6 | 분리 자격 증명·OAuth·계정 전환 | T3, G0-E | 2일 |
| T7 | Calendar·Tasks·불확실한 쓰기 재조회 | T6 | 1–2일 |
| T8 | 통합 수용 시험·ZIP·사용/제거 안내 | T4, T5, T7 | 2일 |
| 합계 | 모든 완료 기준 충족 | G0 결과 후 재산정 | **12–16일** |

이는 초기 개발 공수 추정이지 확정 납기가 아니다. 환경 준비·외부 앱 수정 대기·계정 재인증 대기·휴일은 제외한다. T0를 2일 안에 통과한 것으로 간주하지 않으며 미해결 항목은 원인과 후속 조치로 남긴다. 한 외부 연동이 막혀도 의존하지 않는 오프라인 테스트·셸 작업은 계속할 수 있다.

## 3. 파일 책임과 공통 실행 규칙

미리 생성하지 않고 각 작업에서 필요한 파일만 만든다.

| 책임 | 생성할 핵심 파일 |
|---|---|
| 계약 | Windows/src/Orbit.Windows/Models/Contracts.cs, DisplayModels.cs |
| 조립 | Windows/src/Orbit.Windows/App.xaml, App.xaml.cs, Orbit.Windows.csproj |
| 셸 | Shell/SingleInstance.cs, TrayController.cs, PanelWindow.xaml, PanelWindow.xaml.cs, PanelGeometry.cs |
| 브리지 | Bridge/BridgePolicy.cs, ActionDispatcher.cs, WebViewHost.cs, SnapshotPublisher.cs |
| 갱신 | Providers/RefreshCoordinator.cs, ProviderLimits.cs, SnapshotPolicy.cs |
| 파일 | Providers/RootDiscovery.cs, FileProvider.cs, Platform/PathPolicy.cs, FileLauncher.cs |
| AI | Providers/ReadOnlySqlite.cs, CodexProvider.cs, ClaudeProvider.cs, Platform/ApplicationLinkPolicy.cs, ApplicationLauncher.cs |
| 보안 | Security/CredentialStore.cs, CredentialSessionStore.cs, CredentialBlob.cs, DiagnosticPolicy.cs |
| Google | Google/OAuthFlow.cs, LoopbackCallbackServer.cs, GoogleSession.cs, GoogleHttpClient.cs, CalendarProvider.cs, TasksProvider.cs, TaskRequestPolicy.cs, TaskMutationCoordinator.cs |
| 설정 | Platform/SettingsStore.cs, SettingsModel.cs |
| 검증 | Windows/tests/Orbit.Windows.Tests/와 아래 작업별 Tests.cs, SelfTests.cs |
| 배포 | Windows/scripts/build.ps1, test.ps1, package.ps1, Windows/README.md |

표의 Shell/ 등 짧은 경로는 Windows/src/Orbit.Windows/ 아래다. 테스트는 Windows/tests/Orbit.Windows.Tests/ 아래다.

각 구현 작업은 “실패하는 테스트 추가 → 해당 실패 확인 → 최소 구현 → 해당 테스트와 기존 테스트 통과 → 변경 범위 검토” 순서로 진행한다. 커밋은 작업별 관련 파일만 대상으로 하고 전체 git add .를 사용하지 않는다. 실행자가 승인된 커밋 정책을 확인하기 전에는 커밋·푸시하지 않는다.

이 문서의 개발 명령은 RTK가 준비된 개발 환경에서 저장소 루트 기준으로 실행한다. 개발용 PowerShell 스크립트는 사용자가 AI 카드를 누를 때 터미널을 여는 동작과 무관하며 Orbit 런타임은 이를 호출하지 않는다. 배포 사용자는 .NET SDK·Node·RTK·PowerShell 개발 스크립트를 설치/실행할 필요가 없다.

공통 .NET 테스트 명령(T1 이후):

~~~powershell
rtk proxy dotnet test Windows/Orbit.Windows.sln -c Release --no-restore
~~~

공유 UI/Swift 변경 시 macOS 필수 명령:

~~~sh
rtk proxy bash scripts/build.sh
rtk proxy build/Orbit.app/Contents/MacOS/Orbit --self-test
rtk proxy node --test tests/web.test.cjs tests/platform.test.cjs
~~~

마지막 파일은 T3에서 생성한다. T3 이전에는 기존 tests/web.test.cjs만 실행한다.

## 4. T0 — 호환성 선행 검증

2026-09-07 진행 상태: 현재 호스트는 macOS arm64이며 .NET SDK·Windows 실행 환경이 확인되지 않았다. 기존 macOS build/self-test 32개 및 web tests 5개를 통과했다. Windows 게이트는 미실행이다. [착수 점검 결과](../specs/2026-09-07-windows-compatibility-results.md)에 확인 범위와 다음 조건을 기록했다.

**Files:** 생성 Windows/spikes/Orbit.Compatibility/Orbit.Compatibility.csproj, Program.cs, ProbeWindow.xaml, ProbeWindow.xaml.cs, docs/superpowers/specs/2026-09-07-windows-compatibility-results.md.

**Interfaces:** 본 제품이 호출하는 인터페이스를 만들지 않는다. 출력은 게이트 코드·버전·통과 여부·정제 오류 코드뿐이다. 테스트 파일/DB는 별도의 임시 디렉터리에 생성하고 공급자 저장소에는 작성하지 않는다.

- [ ] **1. 환경 확인:** Windows OS/프로세스 x64, SDK 목록을 아래 명령으로 확인한다. Windows가 아닌 환경에서는 실기기 통과 기록을 만들지 않는다.

~~~powershell
rtk proxy dotnet --list-sdks
rtk proxy powershell -NoProfile -Command "[Environment]::OSVersion.VersionString; [Environment]::Is64BitOperatingSystem; [Environment]::Is64BitProcess"
~~~

- [ ] **2. G0-A/B 최소 호스트:** WPF/WebView2 기본 창에 패키지 index.html만 매핑한다. 정확한 Source와 현재 문서 일치 시에만 합성 ready를 허용한다. 반례 /, 다른 HTML, query, fragment, 외부 host를 보내 모두 거부되는지 확인한다. 트레이·모달 선택창·두 번째 실행에서 기존 창 활성화를 수동 확인한다.
- [ ] **3. G0-C 합성 DB 먼저:** 아래 fixture를 각각 만든 뒤 읽기 프로세스의 Create/Write/Delete를 확인한다. DB와 sidecar 목록/해시 비교만으로는 쓰기 시도까지 입증할 수 없으므로 합성 폴더에서 Windows 파일 I/O 추적도 사용한다. 실제 공급자 저장소에서 잠금/파일 제거를 시도하지 않는다.

~~~text
fixture                     expected
closed-rollback-db          read success; reader creates/writes no file
wal-with-readable-sidecars  committed WAL rows included; no reader writes
wal-without-shm             no sidecar creation; clean unsupported/error if needed
busy-database               bounded busy error; UI remains responsive
unknown-schema             schema-unsupported; no migration/write
~~~

- [ ] **4. G0-C 실제 스키마 확인:** 필요한 테이블/컬럼의 존재와 행 개수만 확인한다. state_5.sqlite/thread_history_1.sqlite가 Windows에도 있다고 가정하지 않는다. 지원한 스키마를 결과 문서에 정리하고 실제 작업명·cwd·세션 ID를 넣지 않는다.
- [ ] **5. G0-D 수동 앱 연결:** 설치 버전·프로토콜 등록 유무를 읽기 전용으로 확인한다. 테스트용 작업에서 Codex exact/home, Claude 동일 프로젝트 새 작성 화면을 각각 열고 실제 도착 화면을 기록한다. URI 원문에는 개인 경로/ID가 있으므로 결과에는 방식과 성공 여부만 남긴다. 자동 메시지를 보내지 않는다.
- [ ] **6. G0-E 저장 경계:** Orbit 호환성 시험 전용 target에 2,560 bytes 성공과 2,561 bytes 사전 거부를 확인한다. 다중 바이트 한국어 입력도 UTF-8 크기로 검사한다. 부분 저장 실패/재시작 후 이전 active 세대 유지와 시험 target만의 정리를 확인한다.
- [ ] **7. G0-F 파일 형태:** Drive 스트리밍/미러링·Obsidian·한글/공백/중첩 루트·cloud tag를 시험한다. 지원하지 않는 환경을 통과로 표시하지 않는다.
- [ ] **8. 결과 기록 및 재산정:** 다음 스키마로 기록하고 G0 미통과의 영향을 후속 작업에 반영한다.

~~~json
{
  "gate": "G0-D",
  "environmentVersionRecorded": false,
  "status": "not-run",
  "method": "manual-destination-check",
  "containsPrivateMetadata": false
}
~~~

위 예는 미실행 상태 표현이며 통과 증거가 아니다. 실제 검증 시 status를 pass/limited/fail로 바꾸고 테스트 버전·정제된 관찰 결과를 추가한다. exact가 안 되어도 승인된 대안이 실제로 동작하면 해당 경로를 limited로 기록할 수 있다. 사용할 대안조차 검증되지 않은 상태는 출시 게이트 실패다.

**종료 기준:** 설계서 G0-A~F별 증거가 있고 기본 기능에 사용할 경로가 확인된다. 실제 쓰기 없는 DB 읽기가 불가능하면 immutable/단순 복사로 우회하지 않고 해당 작업을 막는다.

## 5. T1 — 프로젝트·계약·오프라인 테스트 기반

**Files:** 생성 Windows/AGENTS.md, global.json, Directory.Build.props, Orbit.Windows.sln, src/Orbit.Windows/Orbit.Windows.csproj, App.xaml, App.xaml.cs, Models/Contracts.cs, Models/DisplayModels.cs, Providers/ProviderLimits.cs, Providers/SnapshotPolicy.cs, tests/Orbit.Windows.Tests/Orbit.Windows.Tests.csproj, ContractTests.cs. SDK/패키지 lock 파일 포함.

**Interfaces:** 아래 네이티브 계약을 정의한다. 실제 경로와 비밀은 이 공용 결과에 넣지 않는다. 개별 공급자는 private native record를 내부에 유지하고 표시형으로 변환한다.

~~~csharp
public readonly record struct SourceKey(
    string Provider, string RootKey, long IdentityGeneration);

public enum SourceState { Connected, Disconnected, Error }

public sealed record ProviderResult<T>(
    SourceKey Key, IReadOnlyList<T> Items, SourceState State,
    bool Limited, bool Stale, string ErrorCode,
    DateTimeOffset? LastSuccess);

public interface IProvider<T>
{
    Task<ProviderResult<T>> ReadAsync(
        SourceKey key, CancellationToken cancellationToken);
}

public static class SnapshotPolicy
{
    public static bool CanReuse(SourceKey cached, SourceKey current)
        => cached == current;
}

public static class ProviderLimits
{
    public const int FileDepth = 7;
    public const int FileEntries = 20_000;
    public const int FilesPerSource = 30;
    public const int AgentEntries = 24;
    public const int ClaudeProjects = 200;
    public const int ClaudeHeadBytes = 128 * 1024;
    public const int ClaudeTailBytes = 256 * 1024;
    public static readonly TimeSpan ScanBudget = TimeSpan.FromSeconds(4);
}
~~~

RootKey는 원본 경로의 임의 소문자화가 아니라 PathPolicy가 확인한 루트 동일성의 내부 opaque 키다. ProviderResult 자체를 통째로 WebView에 직렬화하지 않는다.

- [ ] **1. 실패 테스트:** ContractTests.cs에 출처/루트/계정 세대가 하나라도 다르면 재사용을 거부하는 시험을 추가한다.

~~~csharp
[TestMethod]
public void CacheDoesNotCrossIdentity()
{
    var oldKey = new SourceKey("tasks", "google", 1);
    Assert.IsFalse(SnapshotPolicy.CanReuse(
        oldKey, new SourceKey("tasks", "google", 2)));
    Assert.IsTrue(SnapshotPolicy.CanReuse(oldKey, oldKey));
}
~~~

- [ ] **2. 실패 확인:** ContractTests만 실행해 제품 계약 부재로 실패하는지 확인한다. 테스트 프로젝트 설정 오류는 의미 있는 실패로 세지 않는다.
- [ ] **3. 최소 구성:** G0-A에서 확인한 .NET 10 SDK와 안정 WebView2/Microsoft 테스트 패키지 버전을 고정한다. 앱 설정의 핵심 값은 아래와 같다. test project는 IsTestProject=true이며 앱 publish에 포함하지 않는다.

~~~xml
<PropertyGroup>
  <TargetFramework>net10.0-windows</TargetFramework>
  <OutputType>WinExe</OutputType>
  <UseWPF>true</UseWPF>
  <UseWindowsForms>true</UseWindowsForms>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <SelfContained>true</SelfContained>
  <Nullable>enable</Nullable>
  <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  <PublishTrimmed>false</PublishTrimmed>
  <PublishSingleFile>false</PublishSingleFile>
</PropertyGroup>
~~~

Resources는 저장소의 원본을 링크해서 Resources/ 출력 경로로 복사한다. 별도 UI 원본을 만들지 않는다. Windows/AGENTS.md에는 Windows 스택·Credential Manager 예외, 공급자 읽기 전용, no-terminal, 인라인 실행, Windows/macOS 검증 명령을 명시한다.

- [ ] **4. 검증:** restore로 lock 파일을 만든 뒤 locked-mode restore, build, ContractTests를 실행한다. 합성 fixture만 사용하고 시작만으로 실제 공급자·계정에 접근하지 않는 테스트 모드를 확인한다.

~~~powershell
rtk proxy dotnet restore Windows/Orbit.Windows.sln
rtk proxy dotnet restore Windows/Orbit.Windows.sln --locked-mode
rtk proxy dotnet build Windows/Orbit.Windows.sln -c Release --no-restore
rtk proxy dotnet test Windows/Orbit.Windows.sln -c Release --no-restore --filter FullyQualifiedName~ContractTests
~~~

**종료 기준:** 재현 가능한 빈 Windows 앱과 공급자/표시 경계·단위시험이 빌드된다. Windows 외 환경의 교차 컴파일만으로 WPF 실행을 통과 처리하지 않는다.

## 6. T2 — 셸·단일 인스턴스·보안 브리지

**Files:** 생성 Shell의 6개 파일, Bridge의 4개 파일, Security/DiagnosticPolicy.cs, 테스트 ShellTests.cs, BridgeTests.cs. 수정 App.xaml.cs.

**Interfaces:** PanelGeometry.Place(Size requested, Point anchor, Rect workArea) → Rect; BridgePolicy.IsTrustedDocument(string sender, string current) → bool; ActionDispatcher.DispatchAsync(JsonElement request, CancellationToken ct) → Task; WebViewHost.Publish(JsonObject displayMessage) → void. UI 스레드 디스패치는 WebViewHost만 담당한다.

- [ ] **1. 실패 테스트:** 아래 시험과 shell/bridge 반례를 추가한다.

~~~csharp
[TestMethod]
public void BridgeRequiresTheExactMainDocument()
{
    const string document = "https://orbit.invalid/index.html";
    Assert.IsTrue(BridgePolicy.IsTrustedDocument(document, document));
    foreach (var bad in new[] {
        "https://orbit.invalid/", "https://orbit.invalid/index.html?x=1",
        "https://orbit.invalid/index.html#frame", "https://example.com/index.html"
    })
        Assert.IsFalse(BridgePolicy.IsTrustedDocument(bad, document));
    Assert.IsFalse(BridgePolicy.IsTrustedDocument(document, "about:blank"));
}
~~~

추가 반례는 unknown action, launchAtLogin, request 100자, 64 KiB 초과, wrong bool, expired ID, path/URL 추가 필드, 처리 중 request 중복이다. 모두 네이티브 동작 0회여야 한다. openAgent의 선택적 mode=default|fallback 외의 mode 값도 거부한다.

- [ ] **2. 실패 확인:** BridgeTests/ShellTests를 실행하고 보안 정책·창 배치 부재에 대한 실패를 확인한다.
- [ ] **3. 셸 구현:** 사용자+세션 mutex/pipe로 show만 전달한다. pipe 연결은 2초 제한이며 시작 경합에서는 제한된 재시도만 허용한다. 창 배치는 DIP 변환 후 작업 영역으로 clamp하고 폴더 picker의 owner를 지정해 숨김 억제 범위를 try/finally로 해제한다. 앱 종료 시 리소스를 역순으로 정리한다.

~~~csharp
public static Rect Place(Size requested, Point anchor, Rect workArea)
{
    double width = Math.Min(requested.Width, workArea.Width);
    double height = Math.Min(requested.Height, workArea.Height);
    double x = Math.Clamp(anchor.X - width, workArea.Left, workArea.Right - width);
    double y = Math.Clamp(anchor.Y - height, workArea.Top, workArea.Bottom - height);
    return new Rect(x, y, width, height);
}
~~~

- [ ] **4. 브리지 구현:** Uri.TryCreate로 파싱하고 설계서 §5의 URI 조건을 확인한다. 기존 평면 envelope를 action별 schema로 검사한다. 가상 호스트 mapping Deny, navigation/frame/new-window/download 차단, host object 없음, JSON 응답, InPrivate/전용 profile, 배포용 기능 제한을 적용한다.
- [ ] **5. 검증:** ShellTests/BridgeTests 통과 후 일반 권한으로 동시 2회 실행, 숨김 상태 재실행, 모달 취소, pin·Esc·종료, 악성 메시지, 프로필 정리, WebView2 누락 안내를 확인한다. 프로필/pipe 실패에서도 raw 개인 경로를 출력하지 않는다.

**종료 기준:** 실제 공급자 없이도 안전한 공유 UI 호스트가 동작하고 두 번째 실행이 새 인스턴스를 만들지 않는다.

## 7. T3 — 공유 UI와 설정

**Files:** 수정 Resources/app.js, index.html, style.css, google-setup.html, Sources/Bridge.swift. 생성 Resources/platform.js, tests/platform.test.cjs, Platform/SettingsStore.cs, SettingsModel.cs, 테스트 SettingsTests.cs. Resources/calendar.css는 실제 시각 회귀 해결이 필요한 경우만 수정한다.

**Interfaces:** platform.js는 window.orbitPlatform을 제공한다. methods: post(request) → void, canSend() → bool, shortcut(platform) → string, hasMeeting(event) → bool. 기존 app.js의 window.orbit.receive가 유일한 수신 렌더링 진입점이다. platform.js를 app.js 전에 defer 로드한다.

- [ ] **1. 실패 테스트:** Node vm의 가짜 window에 각 전송 객체를 넣고 실제 platform.js를 로드한다. 두 플랫폼 각각 post 1회, 수신 listener 1회, preview 미연결, shortcut, hasMeeting 호환성을 확인한다.

~~~javascript
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');

test('Windows sends once without a WebKit dependency', () => {
  const calls = [], listeners = [];
  const window = { chrome: { webview: {
    postMessage: value => calls.push(value),
    addEventListener: (name, handler) => listeners.push([name, handler])
  } } };
  const code = fs.readFileSync(path.join(__dirname, '../Resources/platform.js'), 'utf8');
  vm.runInNewContext(code, { window });
  window.orbitPlatform.post({ action: 'ready', request: '1' });
  assert.equal(calls.length, 1);
  assert.equal(listeners.length, 1);
  assert.equal(window.orbitPlatform.shortcut('windows'), 'Ctrl K');
  assert.equal(window.orbitPlatform.hasMeeting({ hasMeeting: true }), true);
});
~~~

- [ ] **2. 실패 확인:** Node 테스트가 platform.js 부재/전송 부재로 실패하는지 확인한다.
- [ ] **3. 전송 구현:** Windows listener는 event.data를 window.orbit?.receive로 전달한다. snapshot이 준비되기 전 실제 외부 메시지는 처리하지 않는다. macOS WebKit 수신 방식은 건드리지 않는다. hasMeeting은 새 불리언이 있으면 이를, 없으면 기존 meetingURL의 존재 여부를 반환한다.
- [ ] **4. UI/설정 구현:** 설계서 §4 계약으로 자동실행·AI roots·앱 연결 설명을 분기한다. capabilities 없는 macOS snapshot을 fixture로 유지한다. SettingsModel은 schemaVersion=1, theme, drive/vault/codex/claude 원본 루트와 탐지 방식만 영속화한다. UI에는 leaf label만 보낸다. atomic replacement 중 실패 시 기존 설정을 보존한다.
- [ ] **5. 검증:** 세 테마×두 플랫폼, Windows startup 숨김, macOS startup 유지, appHome/projectNew 설명, unknown Tasks 재클릭 차단, 태그 이스케이프, 탭/검색/포커스, NFC/NFD 검색을 검증한다. shared tests와 macOS 필수 3개 명령을 실행한다.

**종료 기준:** Windows에 터미널/맥 전용 안내가 노출되지 않고 macOS 기능·테마가 유지된다. 단순 HTML 정규식 시험 외에 VM 동작 시험과 두 실제 호스트 화면을 확인한다.

## 8. T4 — 파일 공급자·경로 정책·갱신

**Files:** 생성 Providers/RootDiscovery.cs, FileProvider.cs, RefreshCoordinator.cs, Platform/PathPolicy.cs, FileLauncher.cs, 테스트 FileProviderTests.cs, PathPolicyTests.cs, RefreshTests.cs. 수정 SnapshotPublisher.cs와 ActionDispatcher.cs의 파일/설정 action.

**Interfaces:** PathPolicy.ValidateFile(string root, string candidate) → FileValidation; FileValidation은 Allowed(bool), IdentityKey(string), ErrorCode(string)만 공개한다. FileLauncher.Open(string displayId, SourceKey current) → void는 현재 네이티브 ID 맵을 조회한다. RefreshCoordinator.Request(string provider, bool force) → void; ChangeIdentity(SourceKey next) → void. FileProvider는 IProvider<FileDisplay>를 구현한다.

FileDisplay는 id, name, source, modified(Unix seconds), parent의 표시 필드다. RootDiscovery는 저장 루트/알려진 후보만 확인하며 디스크 전체 검색을 하지 않는다.

- [ ] **1. 실패 테스트:** 임시 fixture에서 아래 사건별 호출 순서/개수와 결과를 확인한다. 대기 I/O는 통제 가능한 TaskCompletionSource로 모의하여 실제 장시간 대기하지 않는다.

~~~text
same source/root/generation + transient error -> old data retained, stale=true
root A -> root B -> delayed A result          -> A result and A IDs rejected
provider request 100 times while I/O pending  -> one active + at most one queued
file budget 20,001 / depth 8 / virtual 4 sec  -> limited=true, no further reads
overlapping Drive/vault root                 -> same file once, Obsidian label
root C:\fixture\vault + C:\fixture\vault2\x.md -> reject
junction/symlink/final-path escape           -> reject; shell calls=0
cloud placeholder approved in G0-F           -> metadata only; body reads=0
~~~

- [ ] **2. 실패 확인:** FileProviderTests/PathPolicyTests/RefreshTests를 실행한다.
- [ ] **3. 갱신 구현:** T1 SourceKey를 모든 작업 시작/게시/클릭에 결합한다. 출처마다 1개 worker와 coalesced 요청만 유지한다. 아래 수용 조건을 결과 게시에 적용한다.

~~~csharp
public static bool CanPublish(
    SourceKey started, SourceKey current, bool superseded)
    => started == current && !superseded;
~~~

위 함수는 Providers/SnapshotPolicy.cs에 추가한다. 실제 I/O가 끝나기 전 슬롯을 해제하지 않는다. 예산 만료의 UI 상태 게시와 worker 수명은 구분한다.

- [ ] **4. 파일 구현:** 파일 핸들 최종 경로/볼륨/경계로 검증하고 스캔 동안 확인한 private path와 표시 ID를 분리한다. reparse tag는 G0-F 결과를 허용 목록으로 사용한다. NFC 검색과 안전한 파일 동일성 중복 제거, 30개 정렬, 폴더 선택 시 세대 변경, 클릭 직전 재검증을 구현한다.
- [ ] **5. 검증:** 자동 fixture 통과 후 실제 Drive/Obsidian 파일을 사용자 클릭으로 연다. 공급자 파일/본문 읽기·쓰기 정책, 임의 URL/실행 파일 거부, disconnected/limited/stale/error, 강제 갱신, 숨김 상태 timer 중단을 확인한다.

**종료 기준:** 한 출처 지연이 화면·다른 출처를 막지 않으며 루트 변경 전 파일이 표시되거나 열리지 않는다.

## 9. T5 — Codex·Claude 공급자와 앱 열기

**Files:** 생성 Providers/ReadOnlySqlite.cs, CodexProvider.cs, ClaudeProvider.cs, Platform/ApplicationLinkPolicy.cs, ApplicationLauncher.cs, 테스트 CodexProviderTests.cs, ClaudeProviderTests.cs, ApplicationLinkTests.cs. 수정 SnapshotPublisher.cs/ActionDispatcher.cs.

**Interfaces:** 두 공급자는 IProvider<AgentDisplay>를 구현한다. AgentDisplay는 id/title/provider/updated/status/project/openMode의 표시형이다. ApplicationLinkPolicy.ClaudeNewProjectUri(string absoluteFolder) → Uri는 검증 완료된 절대 경로를 인코딩만 한다. ApplicationLauncher.Open(string displayId, SourceKey current, bool useFallback = false) → LaunchResult. LaunchResult는 Dispatched(bool), Mode(string), ErrorCode(string)이며 도착 화면 성공을 의미하지 않는다. openAgent mode=fallback은 useFallback=true로 변환하되 해당 카드에 검증된 대안이 없으면 거부한다.

- [ ] **1. 실패 테스트:** UUID·한글/공백/#/&가 있는 폴더·미등록 앱·미지원 exact 매핑·경로 이탈·작업 삭제를 시험한다. URI 인코딩 시험은 Shell을 호출하지 않는다.

~~~csharp
[TestMethod]
public void ClaudeFolderIsOneEncodedQueryValue()
{
    var uri = ApplicationLinkPolicy.ClaudeNewProjectUri(
        @"C:\fixture\한글 #& project");
    Assert.AreEqual("claude", uri.Scheme);
    Assert.AreEqual("", uri.Fragment);
    Assert.AreEqual(
        "?folder=" + Uri.EscapeDataString(@"C:\fixture\한글 #& project"),
        uri.Query);
}
~~~

- [ ] **2. 공급자 실패 시험:** 합성 SQLite에 user/archived/subagent/renamed/unknown-turn 사례를 넣어 최대 24개와 필터를 검증한다. Claude는 200개 디렉터리, 20,000개 방문 한도, 상위 24개, 최대 9 MiB, 잘린 JSONL 경계·sidechain·custom title·완료 상태의 시험을 추가한다.
- [ ] **3. 구현:** G0-C 스키마만 prepared read-only query로 지원한다. SQLITE_OPEN_READONLY=1, SQLITE_OPEN_FULLMUTEX=0x10000, busy timeout=1000ms를 사용하되 WAL 보조 파일 접근 조건을 별도로 검증한다. 지원하지 않는 스키마/sidecar 상태는 source error로 반환한다. 테스트용 DB writer를 제품 interop에 노출하지 않는다.
- [ ] **4. 열기 정책 구현:** G0-D의 확인된 URI만 네이티브 정책으로 사용한다. Claude 기본은 매핑 없는 기록의 projectNew이며 전역 “UUID 지원” 스위치를 만들지 않는다. Codex home 경로를 추측하지 않는다. 등록은 읽기만 하고 앱 설치/업데이트 후 다시 검사한다. stale ID는 거부하고 실패 시 대안 버튼을 표시하되 자동으로 다른 경로를 연속 실행하지 않는다.
- [ ] **5. 검증:** 자동 테스트·WAL 추적을 통과한 뒤 실제 앱의 도착 화면을 재확인한다. Terminal/PowerShell/CLI 프로세스 실행·클립보드 변경·자동 전송이 0회인지 확인한다. 결과에는 버전·모드·통과 여부만 남긴다.

**종료 기준:** 합성 데이터의 읽기 안전성과 실제 앱 연결이 모두 확인된다. exact/session 연속성을 보장할 수 없는 카드는 UI에서 이를 명확히 표시한다.

## 10. T6 — Credential Manager·OAuth·계정 전환

**Files:** 생성 Security/CredentialBlob.cs, CredentialStore.cs, CredentialSessionStore.cs, Google/OAuthFlow.cs, LoopbackCallbackServer.cs, GoogleSession.cs, 테스트 CredentialTests.cs, OAuthTests.cs, GoogleSessionTests.cs. 수정 Google 설정 action과 도움말.

**Interfaces:** CredentialBlob.Encode(string value) → byte[]. ICredentialStore는 아래 저장 인터페이스다. CredentialSessionStore의 신규/refresh/disconnect는 예상 identityGeneration을 받아 전환 경합을 거부한다. OAuthFlow.AuthorizeAsync(CancellationToken ct) → Task<OAuthTokens>. 다음 형식은 네이티브 전용이며 WebView 직렬화 대상이 아니다.

~~~csharp
public interface ICredentialStore
{
    byte[]? Read(string target);
    void Write(string target, byte[] value);
    void Delete(string target);
    IReadOnlyList<string> ListOrbitTargets();
}

public sealed record NativeClientConfig(string ClientId, string ClientSecret);
public sealed record OAuthTokens(
    string AccessToken, string? RefreshToken,
    DateTimeOffset ExpiresAt, IReadOnlySet<string> GrantedScopes);
public sealed record ActiveConnection(
    Guid ClientGeneration, Guid? TokenGeneration, long IdentityGeneration);
public interface ICredentialSessionStore
{
    ActiveConnection CommitNewConnection(
        NativeClientConfig client, OAuthTokens tokens, long expectedIdentityGeneration);
    ActiveConnection CommitRefresh(OAuthTokens tokens, long expectedIdentityGeneration);
    ActiveConnection Disconnect(long expectedIdentityGeneration);
}
~~~

CredentialSessionStore는 ICredentialSessionStore를 구현한다. ListOrbitTargets는 Orbit.Windows/v1/ 정확한 prefix의 target만 반환하며 다른 앱 항목을 열거 결과/진단에 노출하지 않는다. .NET record의 자동 ToString도 토큰/클라이언트 값을 유출할 수 있으므로 비밀형은 로그에서 사용하지 않고 정제된 ToString을 제공한다.

- [ ] **1. 실패 테스트:** blob 경계와 오류 발생 순서를 검증한다.

~~~csharp
[TestMethod]
public void CredentialLimitIsBytesNotCharacters()
{
    Assert.AreEqual(2560, CredentialBlob.Encode(new string('a', 2560)).Length);
    Assert.ThrowsException<ArgumentOutOfRangeException>(
        () => CredentialBlob.Encode(new string('a', 2561)));
    Assert.ThrowsException<ArgumentOutOfRangeException>(
        () => CredentialBlob.Encode(new string('한', 854)));
}
~~~

추가 시험에서 in-memory ICredentialStore의 첫/중간/active/cleanup 쓰기를 각각 실패시킨다. active 교체 전 실패는 이전 active 보존, 교체 후 cleanup 실패는 새 active 유지여야 한다. 새 로그인 응답에 refresh가 없으면 기존 refresh와 결합하지 않고 실패해야 한다.

- [ ] **2. 실패 확인:** CredentialTests/OAuthTests/GoogleSessionTests를 가짜 HTTP와 fake store로 실행한다.
- [ ] **3. 저장 구현:** CredentialBlob은 UTF8.GetBytes 후 2,560 bytes를 넘으면 위 예외를 던진다. target은 Orbit.Windows/v1/client/{generation}/id, /secret, token/{generation}/access, /refresh 및 active로 분리한다. active는 작은 세대 메타데이터만 포함한다. 같은 계정 refresh는 credential revision만 바꾸고 identityGeneration은 유지한다.
- [ ] **4. OAuth 구현:** 네이티브 loopback parser를 GET·정확한 host/path·중복 파라미터 거부·PKCE/state·16 KiB·180초 기준으로 구현한다. 느린 부분 요청도 전체 기한을 넘지 못하게 하고 유효 callback 전의 잘못된 요청 한 번이 전체 인증을 성공 처리하지 못하게 한다.
- [ ] **5. 상태 전환:** 새 연결은 모든 비밀 저장과 scope 확인 후 채택한다. authorizing 중 Tasks 쓰기를 차단한다. cancel/failure는 기존 계정 유지, 성공은 Google 세대·ID·캐시 초기화, disconnect는 token 연결 제거/삭제 및 화면 초기화다. client 설정 변경과 오류 원인을 사용자에게 구분해서 알린다.
- [ ] **6. 검증:** 합성 OAuth redirect/중복 state/code/다른 포트/재사용/timeout/cancel, 새 로그인 refresh 누락, token rotation, invalid_grant, 저장 중 종료, 계정 A→B의 늦은 A 응답을 시험한다. 실제 Credential Manager에는 합성 값만 round-trip하고 시험 target만 제거한다.

**종료 기준:** 비밀이 설정·WebView·진단에 없고 중간 실패로 서로 다른 계정의 토큰이 섞이지 않는다. 자동 테스트에서 실제 OAuth 로그인을 요구하지 않는다.

## 11. T7 — Calendar·Tasks와 서버 상태 재확인

**Files:** 생성 Google/GoogleHttpClient.cs, CalendarProvider.cs, TasksProvider.cs, TaskRequestPolicy.cs, TaskMutationCoordinator.cs, 테스트 GoogleHttpTests.cs, CalendarTests.cs, TasksTests.cs, TaskMutationTests.cs. 수정 GoogleSession.cs, SnapshotPublisher.cs, ActionDispatcher.cs와 공유 UI mutation 상태 처리.

**Interfaces:** TaskRequestPolicy.ListQuery() → string, ReopenBody() → string. TaskMutationCoordinator.SetCompletedAsync(string displayId, bool completed, long identityGeneration, CancellationToken ct) → Task; ReconcileAsync(string displayId, long identityGeneration, CancellationToken ct) → Task. 화면 결과는 스냅샷에 게시한다. HTTP 결과/오류를 임의 JavaScript로 반환하지 않는다.

- [ ] **1. 실패 테스트:** 공식 앱에서 완료한 hidden 작업을 목록에 포함하는지와 다시 열기 payload를 확인한다.

~~~csharp
[TestMethod]
public void TasksCanReopenFirstPartyCompletedItems()
{
    StringAssert.Contains(TaskRequestPolicy.ListQuery(), "showCompleted=true");
    StringAssert.Contains(TaskRequestPolicy.ListQuery(), "showHidden=true");
    using var body = JsonDocument.Parse(TaskRequestPolicy.ReopenBody());
    Assert.AreEqual("needsAction", body.RootElement.GetProperty("status").GetString());
    Assert.AreEqual(JsonValueKind.Null,
        body.RootElement.GetProperty("completed").ValueKind);
}
~~~

- [ ] **2. 실패 확인:** Google HTTP/Calendar/Tasks/mutation 네 종류 테스트를 실행한다. fixture는 페이지/시간대를 고정하고 실제 네트워크 없이 동작해야 한다.
- [ ] **3. 읽기 구현:** 고정 Google API 호스트·경로와 20초 timeout, redirect 금지, maxResults=100, 100페이지 및 반복 token 검출을 적용한다. Calendar는 종일/다일/DST/자정/취소/거절·캘린더별 오류를 시험한다. Tasks는 due를 날짜 문자열로 유지하고 hidden 완료를 표시하며 deleted만 제외한다.
- [ ] **4. 쓰기 구현:** 아래 상태 전이를 구현하고 읽기 revision과 계정 세대를 함께 검사해 오래된 refresh가 결과를 덮지 못하게 한다.

~~~text
idle -> explicit checkbox -> pending
pending -> PATCH response success -> confirmed server state -> idle
pending -> ambiguous network failure -> GET current task
GET success -> confirmed server state -> idle
GET failure -> unknown; checkbox locked; explicit refresh can reconcile
account generation changes -> discard old UI result; never send under new account
~~~

이미 전송된 요청을 취소했다고 서버 반영까지 취소됐다고 알리지 않는다. unknown을 JS request timeout에서 idle로 바꾸지 않는다. 자동 PATCH 반복은 하지 않는다.

- [ ] **5. 검증:** PATCH success/실제 반영 후 응답 유실/미반영 실패/GET 실패/중복 클릭/읽기와 쓰기 경합/계정 전환을 fake HTTP로 검증한다. 이어서 사용자가 지정한 테스트 항목으로 로그인, 7일 일정, 완료, Google 공식 화면 완료 항목 다시 열기, disconnect, 재로그인을 확인한다.

**종료 기준:** 화면 체크 표시가 확인된 서버 상태와 일치하고 불명확한 결과는 불명확하다고 표시한다. 계정 A의 목록·토큰·늦은 쓰기 결과가 B 화면에 섞이지 않는다.

## 12. T8 — 통합·배포·운영 문서

**Files:** 생성 Windows/src/Orbit.Windows/SelfTests.cs, Windows/scripts/build.ps1, test.ps1, package.ps1, Windows/README.md, docs/superpowers/specs/2026-09-06-windows-acceptance-results.md. 수정 README.md의 Windows 상태/링크, App.xaml.cs의 --self-test 모드.

**Interfaces:** --self-test는 앱 소유 fixture만 검사하고 실제 공급자·계정·프로토콜 실행·tray·Google 연결 없이 종료 코드 0/비0과 정제한 테스트 결과를 반환한다. package.ps1은 앞선 검증 하나라도 실패하면 ZIP 생성을 중단한다.

- [ ] **1. 실패 시험:** self-test 출력에 합성 민감 문자열을 넣어 유출을 탐지하고 누락 resource/loader, 위조 설정, credential 파일 혼입, 명령 실패 시 포장 중단을 시험한다.
- [ ] **2. 스크립트 구현:** PowerShell에서 ErrorActionPreference=Stop만 믿지 않고 외부 명령마다 LASTEXITCODE를 검사한다. 다음 파이프라인을 구성하며 Release publish 폴더 전체를 ZIP에 넣는다.

~~~powershell
rtk proxy dotnet restore Windows/Orbit.Windows.sln --locked-mode
rtk proxy dotnet build Windows/Orbit.Windows.sln -c Release --no-restore
rtk proxy dotnet test Windows/Orbit.Windows.sln -c Release --no-restore
rtk proxy node --test tests/web.test.cjs tests/platform.test.cjs
rtk proxy dotnet publish Windows/src/Orbit.Windows/Orbit.Windows.csproj -c Release -r win-x64 --self-contained true --no-restore -o Windows/artifacts/publish
rtk proxy Windows/artifacts/publish/Orbit.Windows.exe --self-test
~~~

마지막 명령은 실행할 PowerShell 환경에서 해당 로컬 실행 파일 경로를 정상 해석하는지 검증한다. 출력 파일명은 csproj의 AssemblyName=Orbit.Windows로 고정한다. ZIP에 .NET 런타임·WebView2 필수 loader·공유 Resources·README·라이선스 고지를 포함하고 시험 fixture·사용자 설정·비밀·로그를 제외한다. 산출물 hash를 생성하되 서명을 대신한다고 설명하지 않는다.

- [ ] **3. 수용 시험:** 아래 전체 목록을 실제 Windows에서 확인하고 날짜·빌드·통과/실패·정제된 원인을 기록한다.

| ID | 필수 수용 시험 |
|---|---|
| A01 | Windows 11 x64, 일반 권한, .NET 미설치 계정, WebView2 있음/없음 |
| A02 | Moss/Pearl/Midnight × 100/125/150/200%, 혼합 DPI·작은 작업 영역 |
| A03 | 키보드·한글 IME·탭·Esc·Ctrl K·스크린리더 이름·포커스 복원 |
| A04 | 중복 실행, modal 취소, pin, 외부 포커스, 절전 복귀, Explorer 재시작, 종료 |
| A05 | Drive 스트리밍/미러링, vault, 중첩·한글 폴더, 실제 파일 열기 |
| A06 | Codex/Claude 도착 화면, 대안 설명, 미설치 앱, 터미널/복사/자동 전송 없음 |
| A07 | Google 로그인·취소·완료·다시 열기·계정 전환·해제, 쓰기 불확실성 |
| A08 | 오프라인·권한 거부·파손 설정/DB/JSONL·느린 루트에서도 다른 출처 사용 |
| A09 | 시작 전후 Startup/Run/예약 작업에 Orbit 항목 변화 없음 |
| A10 | 종료 후 교체 업데이트·데이터 유지·명시적 완전 제거·공급자 데이터 보존 |
| A11 | shared web tests, macOS build/self-test, 기존 macOS 테마·자동실행 기능 유지 |
| A12 | ZIP 재실행, 비밀/개인 로그 혼입 없음, 정제된 self-test 출력 |

- [ ] **4. 문서:** Windows/README.md에 최초 연결, 앱 링크의 한계, OAuth Testing 조건부 만료, WebView2 설치 안내, 서명 없음, 종료 후 교체 업데이트를 적는다. 앱 폴더 삭제와 설정/전용 profile/자격 증명 완전 제거를 구분한다. 제거는 Orbit 소유 대상만 사용자 확인 후 수행하며 공급자 폴더에는 접근하지 않는다.
- [ ] **5. 최종 검증:** 전체 테스트와 실제 수용 시험 결과를 모으고 미실행 항목을 성공으로 바꾸지 않는다. 실패가 있으면 해당 기능/출시만 미완료로 남긴다. 문서·Windows 변경·공유 UI 변경 범위를 검토한 뒤 ZIP과 검증 결과를 전달한다.

**종료 기준:** 설계서 §13과 A01~A12 통과. 수동 시험을 단위 테스트 통과로 대체하지 않는다.

## 13. 검토 의견 반영 추적

“반영”은 설계/계획에 대응책이 있다는 뜻이며 구현·실기기 검증 완료라는 뜻이 아니다.

| 검토 항목 | 재수립 결정 | 설계 절 | 작업·증거 |
|---|---|---:|---|
| 단일 인스턴스가 기존 창을 못 여는 문제 | 사용자/세션 mutex + show pipe | 6 | T0, T2, A04 |
| WebMessage Source를 origin과 혼동 | 정확한 index.html + 현재 문서 비교 | 5 | T2 BridgeTests |
| UI 전송 함수 외 플랫폼 변경 누락 | capabilities·설정·문구·shortcut·URL 표시형 | 4 | T3, A03/A11 |
| InPrivate를 무기록으로 오해 | 전용 프로필·비밀 미전달·한정 정리 | 5 | T2, A10/A12 |
| WSL/대소문자/경로 지원 과장 | native 기본, 별도 검증 없으면 미지원 | 8–9 | T0, PathPolicyTests |
| OAuth listener 권한/입력 경계 | 임시 loopback·상한·중복/재사용 거부 | 10 | T6 OAuthTests |
| Drive 미러링/다중 후보/placeholder | 저장→검증 후보→선택, 태그별 검증 | 8 | G0-F, T4, A05 |
| 경로 prefix·중복·열기 경쟁 | 최종 경로/볼륨/경계·클릭 재검증 | 8 | PathPolicyTests |
| 외부 앱 링크의 도착 화면 미확인 | dispatch와 도착 분리, 검증된 대안만 | 3, 9 | G0-D, A06 |
| Claude UUID/기록 연속성 오인 | 기록별 매핑; 기본 projectNew 설명 | 9 | ApplicationLinkTests |
| SQLite read-only와 WAL 보조 쓰기 | 합성 추적, immutable/단순 복사 금지 | 9 | G0-C, CodexProviderTests |
| Credential Manager 크기·중간 실패 | 항목 분리·active 마지막 교체 | 10 | G0-E, CredentialTests |
| 로그인/refresh 토큰 혼합 | 메서드 분리·새 계정 refresh 필수 | 10 | GoogleSessionTests |
| 공식 앱 완료 Tasks 누락 | showCompleted + showHidden, deleted 제외 | 11 | TasksTests, A07 |
| 루트/계정 바뀐 뒤 옛 캐시 잔류 | SourceKey·세대·ID 무효화 | 7 | ContractTests/RefreshTests |
| Claude 전체 읽기 한도 누락 | 200dir/20k/24files/9MiB/협력적4초 | 9 | ClaudeProviderTests |
| 느린 I/O 무한 worker 누적 | 출처별 1개 실행+1개 합쳐진 요청 | 7 | RefreshTests |
| PATCH 응답 유실·덮어쓰기 경합 | GET 재조회·unknown·계정/읽기 revision | 7, 11 | TaskMutationTests |
| 모달 선택창에서 패널 숨김 | 소유 대화상자 동안 숨김 억제 | 6 | A04 |
| OAuth Testing 재인증·운영 누락 | 조건부 만료 안내·invalid_grant 중단 | 10 | OAuthTests, README |
| 제거 시 비밀 잔류·포함 런타임 관리 | 명시적 데이터 제거·수동 재배포 책임 | 12 | A10/A12 |
| 단정적 일정 | G0 포함 12–16일의 잠정 공수 | 13 | T0 후 재산정 |

## 14. 이번 계획의 완료 확인

- [x] 승인 범위를 유지하고 추가 제외 사항을 기존 범위와 구분했다.
- [x] Windows 11 x64 및 Claude 새 프로젝트 작성 화면 대안을 명시했다.
- [x] Windows 미검증 가정을 선행 게이트와 실패 처리로 분리했다.
- [x] 작업별 파일·계약·실패 시험·구현 지침·종료 기준을 정했다.
- [x] 두 차례 검토 의견을 작업·시험에 연결했다.
- [ ] T0~T8 구현 및 Windows 실기기 시험 — 2026-09-07 T0 환경 점검 착수, Windows 실기기 검증·제품 코드 구현은 미실행.

**다음 실행의 첫 작업:** T0 환경 확인과 합성 호환성 시험. Windows 접근이 아직 없다면 실기기 게이트는 미실행으로 남기고, 접근 가능한 범위의 문서/오프라인 준비만 진행한다. 이미 확정된 x64·Windows 11·자동실행 제외·앱 연결을 다시 승인받는 절차는 반복하지 않는다.
