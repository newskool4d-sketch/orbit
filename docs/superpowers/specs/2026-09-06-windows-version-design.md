# Orbit Windows 버전 설계서 — 재수립안

작성일: 2026-09-06 · 개정: 2

상태: 승인된 범위와 두 차례 검토 의견 반영. Windows 실기기 호환성은 아직 미검증.

실행 문서: [Windows 착수·실행 계획](../plans/2026-09-06-windows-version-plan.md)

## 1. 목표와 확정 범위

Windows 11 x64에서 작동하는 Orbit 트레이 앱을 만든다. 기존 macOS 기능 중 **로그인 시 자동실행만 제외**하고, Moss·Pearl·Midnight 외관과 한국어 사용 흐름을 유지한다. AI 작업은 터미널 대신 Codex·Claude 데스크톱 앱으로 연다.

| 구분 | Windows v1 기준 |
|---|---|
| 화면 | 오늘, 캘린더, 최근 파일, AI 작업, 설정 |
| 셸 | 단일 인스턴스, 트레이, 패널 열기·닫기, 고정, 종료 |
| 갱신 | 수동, 패널 표시 중 60초 주기, 최근 파일 120초 캐시 |
| Google | 앞으로 7일의 Calendar, Tasks 조회·완료·다시 열기 |
| 파일 | Drive·Obsidian 최근 파일, 한글 검색, 출처 필터, 연결 앱으로 열기 |
| AI | 로컬 Codex·Claude Code 기록, 보수적인 상태 표시, 데스크톱 앱 연결 |
| 설정 | 테마, 폴더 선택, Google 연결, 출처별 상태 |
| 기본 배포 | .NET 포함 self-contained win-x64 폴더 ZIP |

제외 사항:

- 로그인 자동실행 UI 및 Startup 폴더·Run 레지스트리·예약 작업 등록.
- AI 연결의 Terminal·PowerShell·CLI 실행, resume 명령 복사, 자동 메시지 전송.
- Store·MSIX·공개 코드 서명·자동 업데이트. 서명되지 않은 배포의 평판 경고 가능성은 문서에 알린다.
- Drive·Obsidian 문서 본문 수집 및 공급자 저장소 수정. 단, AI 목록에 필요한 로컬 메타데이터 레코드는 제한된 크기로 읽는다.
- Claude Desktop 일반 채팅 수집. 기존 macOS 기능은 Claude Code 로컬 기록이며, 모든 Desktop 기록 수집을 뜻하지 않는다.

Claude는 기존 세션의 정확한 열기가 검증되지 않으면 **같은 프로젝트의 새 Code 작성 화면**을 여는 승인된 대안을 쓴다. 이를 기존 대화 재개라고 표시하지 않는다. Windows ARM64·Windows 10·WSL 저장소는 필수 지원 범위가 아니다.

## 2. 구조와 의존성

C# 14 / .NET 10 LTS / WPF / Windows Forms NotifyIcon / Microsoft WebView2를 사용한다. 실행 환경은 Windows 11 x64·일반 사용자 권한이다. 시스템 winsqlite3.dll을 좁은 interop 계층으로 사용하며 별도 SQLite 런타임은 배포하지 않는다.

배포에 포함하는 .NET 런타임 및 Microsoft WebView2 SDK의 필수 구성 요소 외에 Electron·Tauri·Qt·제3자 실행 런타임·로컬 UI 웹 서버를 추가하지 않는다. WebView2 Evergreen Runtime은 **존재를 가정하지 않고 시작 시 확인**한다. 미설치 시 공식 설치 안내를 표시하며 자동 다운로드·관리자 권한 상승은 하지 않는다.

Sources/는 macOS 전용으로 유지한다. Windows 코드는 Windows/에 격리하고 Resources/만 공유한다. 기존 루트 AGENTS.md의 Swift·Keychain 지침을 없애지 않고, 구현 착수 시 Windows/AGENTS.md에 Windows 전용 승인 예외를 명시한다. 에이전트 위임 없이 인라인으로 진행한다.

| 위치 | 책임 |
|---|---|
| Resources/, tests/ | 공유 화면·플랫폼 분기·공유 회귀 시험 |
| Sources/Bridge.swift | macOS 기능 계약을 명시하는 호환성 확장 |
| Windows/global.json, Directory.Build.props, Orbit.Windows.sln | 검증된 SDK·빌드 설정 |
| Windows/src/Orbit.Windows/App.xaml, App.xaml.cs | 수명주기와 구성 요소 조립 |
| Windows/src/Orbit.Windows/Shell/ | 트레이·창·DPI·단일 인스턴스 |
| Windows/src/Orbit.Windows/Bridge/, Models/ | 메시지 검증·내부/표시 데이터 분리 |
| Windows/src/Orbit.Windows/Providers/ | 파일·Codex·Claude·갱신 조정 |
| Windows/src/Orbit.Windows/Google/, Security/ | OAuth·Calendar·Tasks·Credential Manager |
| Windows/src/Orbit.Windows/Platform/ | 설정·파일·앱 열기 |
| Windows/tests/Orbit.Windows.Tests/ | Microsoft 테스트 도구·합성 fixture |
| Windows/spikes/, scripts/, README.md | 호환성 확인·빌드·배포·사용 안내 |

## 3. 선행 검증과 중단 기준

기능 범위는 확정되어 있으나 외부 앱의 비공개 저장 형식·링크는 확정 API가 아니다. 실제 Windows에서 아래 결과를 기록하기 전에는 해당 연동을 완료로 판정하지 않는다.

| 게이트 | 확인 내용 | 통과 또는 처리 |
|---|---|---|
| G0-A | OS·아키텍처·SDK·WebView2·Codex·Claude 버전 | 버전 기록, SDK·패키지 잠금. 개인 경로·작업명은 기록하지 않음 |
| G0-B | 가상 호스트, 메인 문서 메시지, 트레이·모달·두 번째 실행 | 정상 메시지 수신, 위조 메시지 거부, 기존 창 활성화 |
| G0-C | Windows Codex 경로·스키마·SQLite WAL 읽기 | 호환 스키마에서 읽기 성공, Orbit의 공급자 파일 쓰기 없음 |
| G0-D | Codex 정확한 작업 / 앱 홈, Claude 새 Code 작성 화면 | 경로별 실제 도착 화면 확인. 앱 실행 요청 성공만으로 통과 금지 |
| G0-E | Credential Manager 한도·중간 실패·복구 | 합성 비밀로 저장/조회/전환/삭제, 이전 정상 세대 유지 |
| G0-F | Drive 스트리밍·미러링, 한글·공백 경로 | 메타데이터 읽기·클릭 열기, 루트 이탈 방지 |

Claude의 로컬 UUID와 Desktop 세션 식별자는 같다고 가정하지 않는다. 앱 버전 하나에서 성공한 사례를 모든 기록에 적용하지 않는다. Codex 정확한 작업 연결이 안 되면 **검증된** 앱 홈 경로만 허용하며 제한을 표시한다. 앱 홈 경로도 확인되지 않으면 임의 URI를 만들지 않고 해당 연결을 미지원으로 남겨 해결 후 출시한다.

핵심 게이트 실패를 테스트 생략·임의 스키마 추정·터미널 실행·쓰기 허용으로 우회하지 않는다. 안전한 원인 확인과 다른 작업은 진행하되, 해결이 범위 변경을 요구하면 그 변경만 사용자에게 요청한다.

## 4. 공유 UI와 계약

단순 전송 함수만 바꾸는 작업이 아니다. Resources/app.js, index.html, style.css, google-setup.html의 플랫폼 문구·기능 노출·접근성도 함께 다룬다. 기존 레이아웃과 테마 값을 유지하고 Midnight의 내부 키 **cobalt**를 바꾸지 않는다. Windows 글꼴에는 Segoe UI·Malgun Gothic 등 시스템 대체 글꼴을 사용한다.

기존 window.orbit.receive(message)와 kind: snapshot/reply를 유지한다. 요청은 기존의 평면 { action, request, ...args } 형태이며 임의로 중첩 payload로 바꾸지 않는다. macOS는 WebKit, Windows는 chrome.webview로 전송한다. Windows 수신 이벤트는 한 번만 등록한다.

스냅샷에 protocolVersion: 2, platform: windows|macos, capabilities를 추가한다. capabilities는 launchAtLogin, chooseAgentRoots, agentDesktopOpen 불리언이다. Windows 값은 각각 false, true, true다. 구버전 macOS 스냅샷에 필드가 없으면 현재 WebKit 동작으로 처리한다. 기능 권한은 사용자 에이전트 문자열이 아니라 네이티브 선언을 따른다.

- Windows에는 자동실행 컨트롤을 렌더링하지 않으며 네이티브도 launchAtLogin 액션을 거부한다. macOS 기능은 유지한다.
- Windows는 Ctrl K, macOS는 ⌘ K를 표시한다. 탭·Esc·검색·설정 포커스를 회귀 테스트한다.
- Windows AI 카드에 openMode: exact|projectNew|appHome|unavailable과 설명을 표시한다. 정확한 재개로 오인시키지 않는다.
- statuses의 기존 state 값 connected/disconnected/error를 유지하고 limited, stale 불리언을 추가한다. 메시지의 특정 한국어 단어에만 의존하지 않는다.
- Windows settings.drive/vault/codex/claude에는 말단 폴더 표시명만 전달한다. 탐지 방식은 rootOrigins의 auto/selected/none 값으로 별도 전달한다. 표시명·제목도 개인정보일 수 있으므로 로그로 출력하지 않는다.
- files, agents, events, tasks의 화면 필드는 유지한다. Windows id는 출처 세대에 묶인 임시 ID다. 실제 경로·cwd·세션 ID·Google list/task ID·원본 링크는 네이티브 맵에 보관한다. 이벤트 URL은 화면에 필요한 hasMeeting만 새 필드로 전달하고, 렌더러는 기존 macOS meetingURL에도 호환되게 한다.
- Tasks는 선택적 mutationState: idle|pending|unknown을 추가한다. 응답 유실 상태를 JS 타임아웃만으로 완료·실패로 확정하지 않는다.

## 5. WebView2 보안 경계

패키지의 Resources/만 https://orbit.invalid 에 매핑하고 교차 출처 접근은 Deny로 설정한다. 허용 메인 문서는 **https://orbit.invalid/index.html** 하나다. 메시지의 Source는 단순 origin이 아니라 전체 문서 URI이므로 호스트 루트 /와 비교하지 않는다.

처리 직전에 정규화한 발신 문서 URI와 현재 메인 문서 URI를 각각 허용 문서와 비교한다. query·fragment·userinfo·다른 포트·다른 경로는 거부한다. 프레임 생성/탐색, 외부 탐색, 새 창, 다운로드를 차단한다. 프로필·로딩 오류 상태에서는 네이티브 동작을 실행하지 않는다.

허용 액션은 ready, refresh, theme, pin, close, quit, openFile, openAgent, openCalendar, openTasks, openEvent, completeTask, chooseDrive, chooseVault, chooseCodex, chooseClaude, importGoogle, connectGoogle, cancelGoogle, disconnectGoogle, googleHelp다.

메시지는 UTF-8 64 KiB 이하, request는 길이 1–99의 문자열로 제한한다. 액션별 허용 필드·형식·길이를 검증한다. openAgent만 선택적 mode=default|fallback을 받으며 생략 시 default다. fallback도 현재 카드에 대해 네이티브가 검증한 대안만 실행한다. WebView가 넘긴 경로·URL·SQL·실행 파일을 직접 사용하지 않고 현재 ID 맵으로 조회한다. 처리 중인 중복 request는 거부한다. 반환은 JSON serializer와 PostWebMessageAsJson을 사용하며 JavaScript 문자열 연결을 금지한다.

기존 CSP를 약화하지 않는다. connect-src 'none', default-src 'none', self script/style, base-uri 'none', form-action 'none'을 유지한다. 호스트 객체를 노출하지 않는다. 배포 빌드에서는 DevTools·일반 컨텍스트 메뉴·자동 완성·비밀번호 저장·불필요한 브라우저 단축키·권한 요청을 비활성화한다. 앱 자체 단축키는 유지한다.

전용 %LOCALAPPDATA%\Orbit\WebView2 프로필과 InPrivate 모드를 사용한다. **InPrivate를 디스크 무기록 보장으로 설명하지 않는다.** 표시 데이터가 메모리·프로필·OS 오류 덤프에 남을 가능성을 인정하고 비밀은 애초에 WebView에 보내지 않는다. 프로필 정리는 WebView 프로세스 종료 후 Orbit 소유 경로만 대상으로 하며, 실패하면 다음 실행에 안전하게 재시도한다. 사용자/다른 앱 프로필은 건드리지 않는다.

## 6. 트레이·창·수명주기

현재 사용자 및 로그인 세션에 한정한 mutex와 현재 사용자 전용 named pipe를 사용한다. 두 번째 실행은 고정된 show 명령만 제한 시간 내 전달하고 종료한다. pipe로 경로·명령·작업 내용을 받지 않는다. 초기화 중 재실행도 한 트레이·한 공급자 집합만 생성한다.

패널 기본 크기는 560 × 700 DIP이며 사용 가능한 작업 영역이 작으면 축소하고 본문을 스크롤한다. 트레이 위치 또는 클릭 모니터의 작업 영역 안에 배치한다. 100/125/150/200% DPI, 혼합 DPI 다중 모니터, 음수 좌표, 작업 표시줄 자동 숨김을 확인한다.

고정 해제 시 외부 포커스 이동으로 숨기고 고정 시 유지한다. 단, 앱이 소유한 폴더/파일 선택 대화상자에서는 일시적으로 숨김을 억제하고 닫힌 뒤 포커스를 복원한다. OAuth 브라우저 이동은 패널이 숨겨져도 계속되며, 패널을 다시 열었을 때 결과가 반영된다. Esc는 설정을 먼저 닫고 다음 입력에서 패널을 숨긴다.

패널 닫기는 프로세스 종료가 아니다. 트레이 Open/Exit 및 설정의 종료를 지원한다. 종료 시 timer, pipe, OAuth listener, WebView, tray, mutex와 작업 수신을 정리한다. Explorer 재시작 후 트레이 복원, 절전 복귀 후 한 번의 갱신을 검증한다.

## 7. 갱신·캐시·출처 세대

캐시 키는 **provider + 검증된 루트 식별자 + identityGeneration**이다. Google의 identityGeneration은 연결 계정/클라이언트가 바뀔 때 변경되며, 같은 계정의 access token 갱신만으로 바뀌지 않는다. 이전 성공 데이터는 같은 캐시 키에서만 stale 상태로 유지한다.

폴더 변경·계정 전환·연결 해제는 해당 세대를 증가시키고 해당 목록·ID 맵·캐시를 즉시 무효화한다. 이전 세대에서 늦게 완료된 읽기 결과나 UI 동작을 채택하지 않는다. 다른 출처의 데이터는 지우지 않는다.

출처별 한 작업만 실행한다. 새 요청은 합쳐서 한 번만 대기시키며 UI 스레드에서 파일 I/O·SQLite·HTTP를 하지 않는다. 로컬 출처 하나가 느려도 다른 출처와 Google 결과는 개별 게시한다. 일반 갱신은 폴더 캐시 120초를 사용하고 강제 갱신/루트 변경은 우회한다.

파일/Claude 검색의 4초는 **협력적 작업 예산**이다. OS 동기 I/O가 항상 4초 안에 취소된다고 보장하지 않는다. 예산 만료 시 UI에 limited/지연 상태를 게시하고 추가 읽기를 중단한다. 실제 I/O가 남아 있는 동안 같은 출처의 새 worker를 만들지 않아 대기 작업 누적을 막는다. 오래된 완료 결과는 버린다.

Google 읽기와 쓰기는 조정한다. 이미 시작된 읽기는 작업 변경 직후 결과를 덮어쓰지 못한다. 쓰기는 캡처한 계정 세대와 토큰을 사용하고 미전송 요청은 전환 시 취소한다. 이미 서버에 도달한 쓰기는 연결 해제로 취소/롤백됐다고 주장하지 않는다.

## 8. Drive·Obsidian·파일 열기

탐지는 저장된 유효 루트 → 제한된 알려진 후보 → 폴더 선택 순서다. Drive 스트리밍은 가상 드라이브일 수 있고 미러링은 사용자가 지정한 폴더일 수 있다. My Drive/내 드라이브 이름 하나만으로 임의 볼륨을 채택하지 않는다. 복수 후보는 사용자 선택으로 확정한다. Obsidian은 %APPDATA%\obsidian\obsidian.json의 검증된 열린/최근 vault 후보를 사용한다.

루트별 기준: 깊이 7, 방문 20,000개, 협력적 4초, 최근 30개. 확장자는 md pdf hwpx hwp docx xlsx pptx txt csv html gdoc gsheet gslides로 한정한다. 숨김 항목 및 node_modules/build/dist/vendor를 제외하고 이름 검색은 NFC 정규화한다. 한도 도달은 limited로 표시한다.

문자열 prefix만으로 루트 포함을 판단하지 않는다. 정규화·경로 구성 요소 경계·파일 핸들의 최종 경로·볼륨 식별자를 확인한다. junction/symlink/mount-point 및 미지원 reparse tag는 거부한다. Drive cloud placeholder는 모든 reparse point로 묶어 막지 않고 G0-F에서 확인한 안전한 태그/메타데이터 접근만 허용한다. 문서 본문이나 hydration을 위한 강제 읽기는 하지 않는다.

일반 Windows 경로는 해당 파일시스템의 대소문자 규칙을 따른다. case-sensitive 디렉터리/WSL 경로에 일괄 소문자화를 적용하지 않는다. 네트워크/UNC 경로는 지연·권한 오류를 출처별로 처리한다. WSL은 경로 변환·대소문자·WAL·앱의 폴더 인식까지 별도로 검증된 경우에만 지원한다.

겹치는 Drive/vault 루트는 검증된 파일 동일성으로 중복을 제거하고 Obsidian 표시를 우선한다. 클릭 시 현재 세대 ID를 조회한 뒤 다시 경로·일반 파일·루트 포함 여부를 검증한다. Obsidian md는 안전하게 인코딩한 obsidian://open을 사용하고 앱이 없으면 일반 연결 프로그램으로 열 수 있다. 그 밖에는 파일 연결 프로그램을 사용한다. 허용 확장자 밖의 실행 파일·스크립트는 열지 않는다. 검증과 Shell 실행 사이의 경쟁 조건이 완전히 제거된다고 주장하지 않는다.

## 9. Codex·Claude 읽기와 앱 연결

Codex 기본 후보는 %USERPROFILE%\.codex, Claude는 %USERPROFILE%\.claude다. 실제 Windows 설치와 다른지 먼저 확인하며 설정에서 덮어쓸 수 있다. Orbit는 wsl.exe, PATH 기반 실행 파일 탐색, 레지스트리 프로토콜 등록/수정을 하지 않는다.

Codex는 확인한 스키마만 지원한다. 필요한 컬럼·테이블을 검사하고 알 수 없는 스키마는 오류로 분리한다. 읽기 전용·full mutex·1초 busy timeout·prepared statement·bound parameter만 사용한다. archived·비사용자/하위 에이전트를 제외하고 이름 우선, 최대 24개, 최신 turn의 보수적 상태 판정을 유지한다.

SQLITE_OPEN_READONLY만으로 공급자 디렉터리 무변경을 보장하지 않는다. WAL DB에서 -wal/-shm 접근 조건과 SQLite 보조 파일 생성 여부를 합성 fixture로 확인한다. 살아 있는 DB에 immutable=1을 적용하거나 .sqlite 파일만 복사해 WAL을 무시하지 않는다. 수정 없이 접근할 조건이 충족되지 않으면 제한 상태로 처리한다. 실제 공급자 앱의 자체 쓰기와 Orbit의 쓰기를 구분해 관찰한다.

Claude는 최대 200개 프로젝트 디렉터리, 전체 방문 항목 20,000개, 수정 시각 기준 상위 24개 UUID JSONL, 파일당 처음 128 KiB+끝 256 KiB, 전체 협력적 4초로 제한한다. 잘린 경계 줄·잘못된 JSON·sidechain을 제외한다. 파싱 byte 상한은 최대 9 MiB이며 작은 파일은 중복해서 읽지 않는다. 제목은 160자로 제한하고 custom title을 우선한다. 세션 본문을 화면/진단에 전달하지 않는다.

| 출처 | 조건 | 동작·표시 |
|---|---|---|
| Codex | 검증된 로컬 작업 매핑+등록된 프로토콜 | 검증된 codex://threads/{uuid} · “Codex 작업 열기” |
| Codex | 정확한 열기 미지원, 앱 홈 경로 검증됨 | 검증된 홈 URI · “Codex 앱 열기 · 작업 직접 선택” |
| Claude | 해당 기록의 Desktop 매핑과 경로가 검증됨 | 검증된 정확한 경로 · “Claude 작업 열기” |
| Claude | 정확한 매핑 없음, 프로젝트 폴더 사용 가능 | claude://code/new?folder={encodedAbsoluteFolder} · “같은 프로젝트에서 새 작업” |
| 공통 | 앱/지원 경로/폴더 없음 | unavailable · 설치·업데이트 또는 폴더 확인 안내 |

claude://code/{uuid}는 검증 후보일 뿐 기본 지원 경로가 아니다. Claude Desktop과 CLI 기록은 별개이며 로컬 UUID가 있다는 이유로 exact로 분류하지 않는다. 앱 등록 조회·Shell 요청 성공은 실제 화면 도착 확인과 다르다. 자동 확인 API가 없으면 연결 요청을 보냈다고만 알린다. 실패한 exact 요청 이후 다른 작업을 몰래 열지 않고 명시적인 대안 버튼을 제공한다. URI에는 네이티브의 검증된 값만 인코딩하고 프롬프트나 자동 전송 옵션을 넣지 않는다.

## 10. Google 자격 증명과 OAuth

OAuth 입력은 64 KiB 미만의 Desktop installed JSON이다. client ID 형식과 필수 필드를 검증하고 입력 JSON의 임의 endpoint를 호출하지 않는다. HTTP endpoint는 네이티브 고정 허용 목록으로 관리한다.

Credential Manager generic credential의 blob 한도는 항목당 **2,560 bytes**다. access token·refresh token·client ID·client secret을 하나의 JSON blob에 넣지 않는다. 각 값을 UTF-8 바이트로 검증해 분리 저장하고 초과 값을 잘라 저장하거나 평문 파일로 우회하지 않는다.

저장 target은 Orbit.Windows/v1/ 아래 client 세대와 token 세대별 항목, 그리고 작은 active 메타데이터 항목으로 나눈다. 사용자명/계정 이메일을 target에 넣지 않는다. 현재 사용자·현재 PC 범위로 저장한다.

1. 새 세대 값을 모두 저장하고 읽기 검증한다.
2. client/token 세대·만료 시각·identityGeneration만 포함하는 active 포인터를 마지막 단일 쓰기로 교체한다.
3. 성공 후 이전 세대를 정리한다. 중간 실패/종료 시 기존 active를 유지하고 다음 실행에서 Orbit 소유 고아 항목만 정리한다.
4. 조회/쓰기 실패는 연결 오류로 처리하고 비밀이나 raw 예외를 출력하지 않는다.

**새 로그인과 기존 토큰 갱신은 별개 경로다.** 새 로그인은 새 refresh token이 없으면 전환을 완료하지 않는다. 기존 계정의 refresh token을 가져와 섞지 않는다. 같은 연결의 refresh 응답에서만 기존 refresh token 보존을 허용한다. 유효 scope·완전한 저장을 확인한 뒤 새 연결을 채택한다. 로그인 취소/실패는 기존 정상 연결을 유지하되 계정이 바뀌지 않았음을 알린다.

연결 전환 중 작업 쓰기를 막는다. 성공한 전환에서는 Google 목록·ID·캐시를 지운 뒤 새 계정으로 다시 조회한다. 클라이언트 설정 가져오기는 후보 검증/저장 후 활성화하며 설정이 달라지면 기존 연결을 명시적으로 해제하고 재로그인을 요구한다.

OAuth는 Authorization Code + PKCE S256, 난수 state/verifier, 기본 브라우저를 사용한다. TcpListener(IPAddress.Loopback, 0) 방식의 임시 IPv4 loopback listener로 /oauth/callback만 받는다. GET·state/code 단일 값·정확한 Host/포트·경로·16 KiB 요청 상한·180초 전체 제한을 검증한다. 중복 파라미터·재사용·잘못된 state는 거부하며 유효 callback 또는 취소/만료 시 listener를 닫는다. 관리자 권한·예약 URL ACL은 요구하지 않는다.

scope는 Calendar 읽기 전용과 Tasks 읽기/쓰기다. OAuth 동의 화면이 외부 Testing 상태인 경우 이 scope 조합의 refresh token이 7일 후 만료될 수 있음을 도움말에 조건부로 설명한다. invalid_grant는 무한 재시도하지 않고 재연결을 안내한다. Orbit가 Google Cloud 콘솔의 Testing 상태를 자동으로 안다고 가정하지 않는다.

## 11. Calendar·Tasks·쓰기 불확실성

네이티브 HTTP만 사용하고 고정 OAuth/Google API HTTPS 호스트·경로를 허용한다. API 응답 URL로 redirect하여 Authorization을 보내지 않는다. 요청당 20초, 페이지당 최대 100개, 최대 100페이지, 반복 page token 검출을 적용한다. 전체 한도 도달은 조용한 성공이 아니라 limited 상태다. 자동 재시도는 멱등 읽기에만 제한적으로 적용한다.

Calendar는 사용자 시간대 기준 앞으로 7일, 여러 캘린더, 종일/다일/시간대 경계, 취소·본인 거절 제외를 유지한다. Tasks 기한은 날짜만의 의미로 처리한다. 달력/Tasks 홈 및 클릭한 일정·회의 링크는 네이티브가 저장한 HTTPS URL만 기본 브라우저로 연다. userinfo·위험 scheme을 거부하고 WebView에서 임의 URL을 받지 않는다.

Tasks 목록은 showCompleted=true, **showHidden=true**, 삭제 항목 제외를 사용한다. Google 공식 화면에서 완료한 hidden 작업도 “전체 보기”에 포함하여 다시 열 수 있어야 한다. hidden을 일괄 제외하지 않는다.

Tasks 변경은 항목별 pending 처리와 Google 읽기/쓰기 조정 하에 실행한다. 완료는 status=completed, 다시 열기는 status=needsAction 및 completed=null을 전송한다. 성공 응답 또는 이후 GET으로 서버 상태가 확인됐을 때만 화면 상태를 확정한다.

PATCH 이후 타임아웃·연결 단절은 서버 반영 여부가 불명확하다. 자동으로 같은 PATCH를 반복하지 않고 해당 작업 GET으로 재조회한다. 성공하면 서버 상태로 맞추고, 조회도 실패하면 mutationState=unknown으로 남겨 재시도 클릭을 막고 “반영 여부 확인 중”과 상태 새로고침을 제공한다. UI request 타임아웃이 지나도 네이티브 상태가 판정의 기준이다. 다른 출처 기능은 계속 사용 가능해야 한다.

연결 해제는 새 작업 전송을 중지하고 active에서 token 연결을 제거한 후 해당 토큰 세대를 삭제한다. Google 데이터·ID를 지우고 imported client 설정은 재연결을 위해 남긴다. 완전 제거는 별도 명시적 데이터 제거 절차로 설명한다. 앱 폴더만 삭제해도 Credential Manager 데이터가 사라진다고 안내하지 않는다.

## 12. 설정·오류·배포

%LOCALAPPDATA%\Orbit\settings.json에는 테마와 선택한 루트 등 비밀 아닌 설정만 저장한다. 같은 디렉터리 임시 파일·원자적 교체를 사용한다. 형식 오류/미지원 테마는 기본값으로 복구하되 원본을 무조건 지우지 않는다. 전체 경로는 네이티브 설정에만 존재한다. pin은 현재 실행 동안의 상태다.

진단 출력은 테스트 이름·개수·출처·상태·정제한 오류 코드로 제한한다. 작업 제목·파일명·개인 절대 경로·토큰·인증 JSON·이벤트/작업 식별 URL·본문·raw 예외를 기록하지 않는다. 개인정보가 포함된 화면 캡처도 자동으로 보고서에 첨부하지 않는다. 자동 테스트는 합성 데이터와 가짜 HTTP/credential 저장소를 사용한다.

배포는 restore 잠금 → build → native/shared tests → self-test → self-contained publish → publish 결과 self-test → ZIP → 깨끗한 Windows 계정 검증 순서다. .NET 런타임·WebView2Loader 등 실행 필수 산출물을 빠뜨리지 않는다. 패키지에는 비밀·로컬 설정·진짜 공급자 데이터·개인 로그를 넣지 않는다.

README는 압축 해제, WebView2 누락, Google 설정, 앱 링크 제약, 종료 후 폴더 교체 업데이트, 데이터 보존/완전 제거를 다룬다. 완전 제거 대상은 Orbit 설정·전용 프로필·Orbit.Windows/v1/ 자격 증명으로 한정하고 명시적 사용자 선택을 요구한다. 공급자 폴더는 삭제하지 않는다. 배포 담당자가 .NET 포함 패키지의 보안 업데이트를 확인하고 재배포한다. 자동 업데이트가 없다는 이유로 런타임 관리 책임을 생략하지 않는다.

## 13. 완료 기준

모든 G0 핵심 게이트, 오프라인 자동 테스트, Windows 실사용 수용 시험을 통과한 때만 Windows v1 완료로 판정한다. macOS에서 문서 작성이나 정적 검사만으로 Windows 성공을 선언하지 않는다.

- 3개 테마, 4개 DPI, 키보드 탐색, 한국어 표시·검색, 폴더 대화상자 복귀, 고정/숨김, 재실행·종료를 확인한다.
- 합성 DB/JSONL·경로·WAL·캐시 세대·브리지·토큰 부분 실패·Google 재연결·PATCH 불확실성 시험을 통과한다.
- 실제 앱 도착 화면, 파일 열기, 승인된 테스트 계정의 Calendar/Tasks 완료·공식 화면 완료 항목 다시 열기·계정 전환을 확인한다. 실제 계정 쓰기는 사용자가 지정한 테스트 항목에만 수행한다.
- 별도 .NET 미설치 계정에서 ZIP을 실행하고 자동실행 등록이 없음을 전후 비교한다.
- 공유 리소스나 Swift를 변경한 경우 macOS build, Swift self-test, shared web tests를 통과한다.

예상 순공수는 **12–16 개발일**이며 확정 납기가 아니다. Windows 11 x64 테스트 환경과 앱·테스트 계정이 준비된 뒤 선행 검증 2일을 포함한 초기 추정이다. 환경 준비·외부 앱 문제·재인증 대기·휴일은 제외하며 G0 결과 후 재산정한다. 상세 작업과 검토 의견 추적은 실행 계획에 둔다.

## 14. 확인 근거

아래는 API 제약과 검증 항목의 근거이며 Orbit의 Windows 실기기 시험 결과를 대신하지 않는다.

- [.NET 지원 정책](https://learn.microsoft.com/en-us/dotnet/core/releases-and-support)
- [WebView2 WPF 시작](https://learn.microsoft.com/en-us/microsoft-edge/webview2/get-started/wpf), [로컬 콘텐츠](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/working-with-local-content), [보안 지침](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security)
- [WebMessage Source는 문서 URI](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2webmessagereceivedeventargs.source), [InPrivate 설정](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.wpf.corewebview2creationproperties.isinprivatemodeenabled), [컨트롤러 옵션·프로필](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2environment.createcorewebview2controlleroptions)
- [Windows Credential 구조·blob 한도](https://learn.microsoft.com/en-us/windows/win32/api/wincred/ns-wincred-credentialw), [Windows 시스템 SQLite](https://learn.microsoft.com/ko-kr/uwp/win32-and-com/win32-extension-apis)
- [SQLite WAL 읽기 전용 조건](https://sqlite.org/wal.html#read_only_databases), [SQLite URI와 immutable](https://sqlite.org/uri.html)
- [Claude Desktop 링크](https://support.claude.com/en/articles/14729294-open-claude-desktop-with-a-link), [CLI와 Desktop 기록 차이](https://code.claude.com/docs/en/desktop#coming-from-the-cli)
- [Codex Windows 안내](https://help.openai.com/en/articles/11369540) — 특정 deep link 보장 근거는 아님
- [Google 네이티브 앱 OAuth](https://developers.google.com/identity/protocols/oauth2/native-app), [토큰 크기·만료 조건](https://developers.google.com/identity/protocols/oauth2)
- [Tasks 목록과 showHidden](https://developers.google.com/workspace/tasks/reference/rest/v1/tasks/list), [Tasks GET](https://developers.google.com/workspace/tasks/reference/rest/v1/tasks/get)
- [Drive 스트리밍·미러링](https://support.google.com/drive/answer/13401938)
