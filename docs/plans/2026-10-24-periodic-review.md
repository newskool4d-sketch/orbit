# Orbit 정기 개선 검토 의견 — 2026-10-24

검증 수행일: 2026-10-24. 의견 최종 정리일: 2026-10-26. 아래 시험 결과는 검증 수행일의 결과이며 최종 정리일에 다시 실행한 결과가 아님.

후속 상태: R01·R02는 사용자 승인에 따라 수정했고 새 네이티브 회귀 시험 13/13, 빌드·서명, 기존 자체 시험 48/48, JS 시험 35/35를 통과함. 상세 범위와 남은 경계는 [2026-10-26 수정 결과](2026-10-26-auth-bridge-fixes.md)를 참조함. 아래 본문은 수정 전 검토 기록임.

추가 후속 상태: R03·R04·R05도 구현했고 실제 WKWebView 시험 45/45, JS 시험 38/38, Swift 자체 시험 50/50과 빌드·서명을 통과함. [포커스·키보드 수정 기록](2026-10-26-focus-keyboard-fixes.md)을 참조함. Windows C# ID 재사용 변경의 전체 gate와 실제 입력기 전환 시험은 미검증임.

최신 후속 상태: R03~R05는 macOS 1.0.2 설치본의 실제 화면·두벌식 입력기 검증까지 통과함. R06~R09는 [정렬·경로·탐색·오류 안내 수정 기록](2026-10-26-local-source-fixes.md)으로 이어감. 아래 원문은 수정 전 검토 기록임.

검토 기준: `025890e5810b6a54e91886b815d8fe8e663b95af`. 요청 범위는 정밀 검토 후 개선의견 선제시임. 이번 산출물은 검토 의견이며 기능 수정·설치본 교체·커밋·배포는 포함하지 않음.

우선 권고는 **macOS 계정 토큰 처리 → 초기 브리지 응답 → 키보드·포커스 → Windows 목록·파일 탐색** 순서임. 아래 9건은 실행 재현 또는 코드 근거가 확인된 개선 대상임. Windows 네이티브 실행과 실제 Google 계정 검증은 별도 미검증임.

## 검증 범위

| 검사 | 결과 | 범위 |
| --- | --- | --- |
| JavaScript 기존 시험 재실행 | 35/35 통과 | 네 테스트 파일 전체. 실제 브라우저 배치 검증을 대체하지 않음 |
| Swift 기존 자체 시험 재실행 | 48/48 통과 | 기존 빌드 실행 파일의 합성 시험 |
| macOS 빌드·서명 | 앞선 작업에서 통과 | 이어받기에서는 빌드를 반복하지 않음. 기준 커밋과 소스 작업트리의 무변경을 확인함 |
| 계정 토큰 처리 추가 검증 | 결함 재현 | 실제 Models.swift·GoogleClient.swift를 컴파일하고 OAuth·키체인·HTTP를 메모리 대역으로 교체함 |
| 공용 UI 추가 검증 | 결함 재현 | 실제 Resources를 격리된 WKWebView에서 실행함. 데이터와 브리지 응답만 합성함 |
| Windows 정렬·오류 표시의 공용 UI 검증 | 결함 재현 | 실제 app.js를 기존 DOM 대역에서 실행함. C# 호스트 동작은 코드 대조 |
| Windows 전체 gate·실기기 | 미실행 | 현재 Mac에 Windows/WPF·.NET 실행 환경이 없음. 기존 Windows gate를 대체하거나 완화하지 않음 |
| 실제 Google 로그인·쓰기 | 미실행 | 합성 응답만 사용함 |

이전 다중 에이전트 작업은 7개 탐색 결과를 남겼으나 병합·검증·종합 단계가 사용량 제한으로 실패했음. 출력의 `confirmed=0 / rejected=180`은 결함 판정으로 사용할 수 없음. 이번에는 탐색 결과를 후보로만 참조하고 직접 검토·재현했음. 자체 검토이며 독립 검토 완료로 기록하지 않음.

## 우선 개선 대상

### R01 · P1 · macOS 재로그인에서 이전 계정의 갱신 토큰을 재사용할 수 있음

- 근거: `Sources/GoogleClient.swift:48`, `:62`, `:70`, `:74`. 새 인증과 기존 인증의 갱신이 같은 saveTokens()를 사용하고, 응답에 refresh_token이 없으면 기존 tokens.refresh로 대체함.
- 조건·영향: 계정 A가 연결된 상태에서 계정 B로 재로그인했는데 응답에 갱신 토큰이 없으면 B의 access token과 A의 refresh token이 함께 저장됨. 이후 갱신에서 A의 데이터로 돌아가며, 사용자에게 계정 전환 실패를 알리지 못함. Tasks 쓰기 대상 계정에도 영향을 줄 수 있음.
- 검증: 실제 GoogleClient 소스에 합성 응답을 주입함. 새 인증 응답에서 갱신 토큰을 생략하고 만료를 앞당겼을 때 `acceptedFreshWithoutRefresh=true`, `reusedPriorAccountRefresh=true`, `eventualRequestUsesPriorAccount=true`를 확인함. 실제 Google 서버가 이 응답을 얼마나 자주 내는지는 검증하지 않음.
- 개선: 새 인증 저장과 기존 인증 갱신 저장을 구분함. 새 인증에는 새 refresh token을 요구하고 불완전한 응답은 기존 상태를 보존한 채 실패 처리함. Windows `Google.cs:92`의 fresh 구분이 참고 가능한 기존 구현임.
- 통과 기준: 새 인증에서 갱신 토큰이 누락되면 연결 성공으로 보고하지 않고 계정의 토큰을 혼합하지 않음. 정상적인 기존 인증 갱신에서는 새 refresh token이 없어도 기존 토큰을 유지함.

### R02 · P2 · macOS 첫 ready 응답 유실로 뒤늦게 오류가 표시될 수 있음

- 근거: `Resources/app.js:11`, `:145`, `Sources/Bridge.swift:41`, `:62`, `:210`. 스크립트가 로드되면 ready 요청을 보내지만 native 준비 플래그는 didFinish에서 켜짐. 그 전에 처리된 응답은 emit()에서 버림.
- 검증: 실제 Resources와 동일한 준비 플래그·Task 응답 순서를 사용하는 격리 WKWebView에서 `initialReadyBeforeFinish=true`, `droppedReplies=1`, `pendingReady=1`을 확인함. 생산 Bridge 전체를 실행한 시험은 아님. 남은 요청이 180초 뒤 오류 토스트를 발생시키는 것은 send()의 타임아웃 코드로 확인함.
- 개선: 검증된 ready 메시지를 준비 완료의 기준으로 삼거나, 초기 응답을 준비 완료 시점까지 보관하여 전송함.
- 통과 기준: 최초 로드와 WebContent 프로세스 재로드에서 ready 요청이 정확히 한 번 정리되고 대기 요청이 남지 않음. 실제 앱에서 180초 경과 후 잘못된 오류가 표시되지 않음.

### R03 · P2 · 스냅샷 갱신마다 카드와 체크박스 포커스가 소실됨

- 근거: `Resources/app.js:41`, `:44`, `:119`, `:121`, `:131`, `:133`, `:134`, `:139`. 여러 섹션을 매번 innerHTML로 교체함. D-day 관리 목록에만 변경 없는 마크업을 보존하는 처리가 있음.
- 검증: 실제 WKWebView에서 최근 파일 카드에 포커스를 놓고 동일 스냅샷을 다시 전달함. `focusedBefore=true`, `focusedAfter=false`, `removed=true`를 확인함.
- 영향·개선: 자동 갱신·할 일 체크 응답에서 키보드 사용자의 현재 위치가 사라짐. 변경 없는 목록의 DOM 교체를 생략하고 변경된 목록은 항목 식별자로 포커스를 복원함. Windows처럼 갱신마다 새 ID를 만드는 소스는 메모리 안에서 불투명 ID를 재사용하도록 함께 조정함. 실제 경로를 웹 계층 식별자로 노출하지 않음.
- 통과 기준: 동일 스냅샷, 시간 표시 변경, 체크 성공·실패에서 포커스를 유지함. 항목 삭제 시 인접 항목 등 예측 가능한 위치로 이동함.

### R04 · P2 · 설정 패널 뒤의 컨트롤로 포커스가 이동함

- 근거: `Resources/app.js:137`, `:142`. 설정은 hidden과 첫 포커스만 변경하며 배경 inert·Tab 순환을 적용하지 않음. D-day 관리창에는 이미 유사 처리가 있음.
- 검증: 실제 WKWebView에서 설정을 연 뒤 배경 새로고침 버튼의 focus()가 성공하는 `backgroundCanFocus=true`를 확인함. 물리 키보드 Tab 이동을 시험한 것은 아님.
- 개선·통과 기준: 설정이 열린 동안 배경 포커스를 차단하고 Tab·Shift+Tab 순환을 구현함. 닫으면 진입 버튼으로 복귀함. 설정에서 D-day 관리창으로 전환하는 상태도 확인함.

### R05 · P2 · 한글 입력 상태의 검색 단축키가 처리되지 않을 수 있음

- 근거: `Resources/app.js:142`. 검색 단축키를 event.key.toLowerCase()==='k'로만 검사함.
- 검증: 실제 WKWebView에 `code='KeyK'`, `key='ㅏ'`, `metaKey=true`인 합성 이벤트를 전달했을 때 `layoutShortcutHandled=false`를 확인함. 실제 입력기에서 운영체제가 전달하는 key 값과 Cmd/Ctrl 조합의 동작은 별도 실기기 확인이 필요함.
- 개선·통과 기준: 문자와 키 위치를 구분하여 code==='KeyK'를 포함한 판정을 적용함. 실제 macOS 두벌식·영문과 Windows 한글·영문에서 Cmd+K/Ctrl+K의 검색 이동과 D-day·설정 닫기를 확인함.

### R06 · P2 · Windows 최근 파일·AI 전체 목록이 최신순으로 통합되지 않음

- 근거: `Windows/src/Orbit.Windows/LocalProviders.cs:24`는 Drive → Obsidian, Claude → Codex 순으로 목록을 붙인 뒤 바로 반환함. `Resources/app.js:133`, `:134`도 정렬 없이 표시함. 반면 macOS는 소스별 결과를 통합 정렬함.
- 검증: 실제 UI 코드에 오래된 Drive 파일 뒤 최신 Obsidian 파일, 오래된 Claude 작업 뒤 최신 Codex 작업을 전달하면 오래된 항목이 먼저 표시됨을 확인함. C# 네이티브 실행은 미검증임.
- 개선·통과 기준: Windows 최종 스냅샷에서 파일은 modified, AI 작업은 updated 내림차순으로 통합 정렬함. 소스별 최대 표시 수와 오늘 화면의 서비스별 최신 항목 선택을 유지함.

### R07 · P2 · Windows에서 드라이브 루트를 선택하면 파일 포함 검사가 실패함

- 근거: `Windows/src/Orbit.Windows/Policies.cs:69`, `:71`. Path.TrimEndingDirectorySeparator()는 루트 구분자를 보존하므로 G:\에 구분자를 다시 붙이면 역슬래시가 두 개인 접두사로 비교하게 됨. 정상 하위 경로는 이를 통과하지 못함.
- 근거 수준: 코드와 [Microsoft 공식 메서드 설명](https://learn.microsoft.com/en-us/dotnet/api/system.io.path.trimendingdirectoryseparator?view=net-10.0) 대조로 확인함. Windows 실행 재현은 미검증임.
- 영향: 드라이브 루트를 수동 선택한 경우 파일 0건과 제한 검색 상태가 표시되거나 파일 열기가 거부됨. 자동 탐지된 일반 하위 폴더에는 이 조건이 적용되지 않음.
- 개선·통과 기준: 루트 여부를 반영하여 경계 접두사를 만듦. 드라이브 루트와 일반 폴더 하위 경로는 통과하고 같은 접두사인 형제 폴더·상위 경로 이탈·금지 reparse 경로는 거부함.

### R08 · P2 · Windows 파일 탐색의 폴더 제외·부분 실패 처리가 부족함

- 근거: `Windows/src/Orbit.Windows/LocalProviders.cs:42`, `:47`, `:60`. 숨김·점 폴더와 node_modules/build/dist/vendor를 제외하지 않음. 하위 폴더 열거 예외는 소스 전체 try/catch로 올라가며 수집 후보를 파일 목록에 반영하기 전에 탐색이 중단될 수 있음.
- 조건·영향: .trash의 md나 빌드 결과의 txt/pdf가 최근 파일에 포함될 수 있고, 접근 불가 하위 폴더 한 곳 때문에 다른 정상 폴더에서 모은 후보도 화면에 반영되지 않을 수 있음. 코드 확인이며 실제 Windows ACL·파일 트리 재현은 미검증임.
- 개선·통과 기준: 표시에서 제외할 폴더 정책을 명시하고 macOS와 의도적으로 맞춤. 폴더별 열거 실패는 해당 분기만 건너뛰고 정상 결과와 부분 검색 표시를 유지함. 깊이·개수·시간 상한과 링크 검사도 유지함.

### R09 · P2 · 로컬 소스 오류를 탭 본문에서 기록 없음으로 표시함

- 근거: `Resources/app.js:133`, `:134`. 로컬 파일·AI 본문이 오류 상태를 고려하지 않고 빈 목록 메시지를 만듦. 자세한 오류는 설정에만 있음.
- 검증: 실제 app.js의 기존 DOM 대역에 Claude 오류 상태와 빈 목록을 전달하면 오류 대신 '해당 서비스의 로컬 작업 기록이 없습니다'를 출력함을 확인함.
- 영향·개선: 사용자가 연결·형식·권한 오류를 정상적인 빈 결과로 오인할 수 있음. 초기 읽기 중, 정상 빈 결과, 연결 필요, 읽기 실패를 구분하고 현재 필터에 해당하는 오류와 복구 동작을 본문에 표시함.
- 통과 기준: 동일한 빈 목록이라도 소스 상태에 따라 안내와 복구 동작이 달라짐. 원문 예외·개인 경로를 오류 문구에 그대로 노출하지 않음.

## 권장 실행 묶음

| 순서 | 범위 | 종료 기준 |
| --- | --- | --- |
| 1 | R01 계정 토큰, R02 초기 응답 | 합성 계정 전환·갱신과 초기 로드·재로드의 불변조건 충족 |
| 2 | R03~R05 포커스·설정·검색 단축키 | 실제 WKWebView 반복 갱신·키보드 조작 확인. Windows WebView2도 별도 확인 |
| 3 | R06~R09 정렬·경로·탐색·오류 안내 | 혼합 소스·드라이브 루트·접근 거부·상태별 빈 결과 fixture와 Windows 전체 gate |
| 4 | 검증 절차·유지보수 정리 | 아래 보강 의견을 필요한 범위에서 적용 |

추가 보강 의견:

- 신규 인증과 토큰 갱신을 구분하는 회귀 시험, 초기 브리지 응답 시험, DOM 교체·포커스 시험을 추가함. 기존 48/35 통과만으로 이 경로들이 검증되었다고 볼 수 없음.
- Windows Google 오류는 인증 만료·권한·네트워크·설정 오류별 복구 방법을 안내함. 현재 Google.cs:142와 Program.cs:150에서 구체적인 안내가 일반 문구로 합쳐짐. 원문 예외를 그대로 전달하는 방식은 피함.
- UI 함수가 한 줄이어야 추출되는 테스트 구조를 완화한 뒤 서식을 정리함. 동작 변경과 대규모 서식 변경은 다른 diff로 검토할 수 있게 함.
- README의 시험 수·검증 범위와 문서 색인을 갱신함. 필요하면 양 플랫폼 기존 검증 명령을 모으는 로컬 진입점을 먼저 만들고, CI는 환경·권한 범위를 정해 별도로 설계함.

기존 D-day 손상 파일 보존·쓰기 실패 시 상태 보존, 브리지 호출 제한, 외부 문자열 이스케이프는 유지할 가치가 있는 구현임. 해당 기존 시험은 통과했으며 위 결함 수정에서도 회귀를 확인해야 함.

고의로 둔 Windows SQLite 버전 제한은 이번 결함 수에 포함하지 않았음. 3.51.1 이외 버전을 허용하려면 읽기 중 쓰기 I/O와 DB/WAL/SHM 불변성을 다시 검증해야 하므로 버전 문자열 검사만 제거하는 수정은 권고하지 않음. Google 외부 쓰기 실검증도 지정된 시험 항목을 사용하는 별도 단계로 유지함.

## 추가 재현 자료

당일 임시 검증 소스:

- `/private/tmp/orbit-review-20261024-oauth.swift`: OAuth·키체인·HTTP 대역. 실제 소스 두 파일과 함께 컴파일하며 실제 자격증명·외부 네트워크를 사용하지 않음.
- `/private/tmp/orbit-review-20261024-probe.swift`: 격리 WKWebView에서 실제 Resources를 로드하고 합성 스냅샷·이벤트를 전달함. 초기 Bridge 순서는 최소 재현으로 구성함.

```text
oauth-probe acceptedFreshWithoutRefresh=true reusedPriorAccountRefresh=true eventualRequestUsesPriorAccount=true
native-probe initialReadyBeforeFinish=true droppedReplies=1
pendingReady=1 focusedBefore=true focusedAfter=false removed=true
backgroundCanFocus=true layoutShortcutHandled=false
```

WKWebView 검증기는 처음 샌드박스에서 시스템 서비스 접근 제한으로 실행되지 않았으며 동일한 합성 검증기를 승인된 재실행으로 수행했음. 임시 파일은 영구 보관되는 시험 자산이 아니므로 후속 수정 단계에서는 필요한 사례를 정식 회귀 시험으로 옮겨야 함.

검토 의견 정리는 완료했으며 제품 전체 플랫폼 검증 상태는 PARTIAL임. 후속 구현에서는 substantive Swift 변경 후 빌드·자체 시험, 공용 UI 변경 후 JS 시험과 실제 호스트 검증, Windows 변경 후 기존 build.ps1·test.ps1 전체 gate를 수행해야 함.
