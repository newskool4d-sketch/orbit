# Windows 작업 인계 — 2026-10-26

목적: Mac에서 수정한 R03~R09를 Windows에서 빌드·검증하고, 통과한 Windows 배포본으로 설치·실제 화면 확인·릴리스 갱신을 이어감.

## 0. Windows 새 대화에 붙여넣을 시작 프롬프트

Windows의 새 대화에서 Orbit 저장소를 작업 폴더로 선택하고 아래 블록 전체를 붙여넣음. 이 문서 하나에 변경 내역·검증 기준·실행 순서·시작 프롬프트가 함께 있음. 인계 문서 최초 커밋은 `e9fd7c5`이며 프롬프트를 포함한 최신 main을 사용함.

```text
Orbit Windows 작업을 이어서 진행해줘. 계획 제시에 그치지 말고 인계 문서를 기준으로 검증·필요한 수정·패키징·설치본 교체·실제 화면 확인·GitHub 커밋·푸시·릴리스 갱신까지 수행해줘.

저장소: https://github.com/newskool4d-sketch/orbit
브랜치: main
인계 문서 최초 커밋: e9fd7c5
인계 문서: docs/plans/2026-10-26-windows-handoff.md

먼저 현재 작업트리와 적용되는 AGENTS.md, Windows/AGENTS.md를 읽어줘. 로컬 변경은 보존하고, 깨끗한 상태에서 git pull --ff-only origin main으로 최신 내용을 받아줘. 인계 커밋으로 되돌리지 말고 최신 main에서 작업해줘.

인계 문서에 따라 다음 순서로 진행해줘.

1. Windows·.NET SDK 10.0.400·PowerShell·Node.js·WebView2 환경 확인.
2. Windows/scripts/build.ps1과 Windows/scripts/test.ps1 -NativeUi 실행.
3. R03~R09의 포커스·설정·실제 한글/영문 Ctrl+K·통합 최신순 정렬·드라이브 루트 경계·제외 폴더·부분 탐색 실패·소스 오류 안내를 검증하고, 발견한 문제를 최소 범위로 수정한 뒤 관련 전체 gate 재실행.
4. 검증된 소스 기준으로 Windows/scripts/package.ps1 실행. ZIP·SHA-256·BUILD-INFO의 소스 커밋과 작업트리 상태 확인.
5. 실제 설치 경로를 확인하고 앱을 정상 종료한 뒤 기존 프로그램 폴더를 백업. 새 배포 파일 전체로 교체·재실행하고 설치본 해시·자체 시험·실제 화면·재시작 복원을 확인. 실패하면 백업으로 복원.
6. 비식별 검증 기록을 갱신하고 소스·시험·문서를 커밋·푸시. 모든 필수 검증을 통과하면 새 Windows 시험판 릴리스에 ZIP·체크섬·검증 기록을 업로드하고, 공개 자산을 다시 다운로드해 해시 확인.

이번 작업 범위의 수정·시험·패키징·설치본 백업과 교체·재실행·커밋·푸시·Windows 시험판 릴리스 게시를 승인한다. 통상적인 단계마다 재확인하지 말고 진행하되, 권한이나 사용자 판단이 실제로 필요한 경우에만 이유와 함께 질문해줘.

주의사항:
- Mac의 시험 통과와 과거 Windows 검증 기록을 이번 Windows 네이티브 검증으로 간주하지 말 것.
- winsqlite3 3.51.1 제한, WAL/SHM 보호, 기존 전체 gate를 완화하지 말 것.
- 사용자 설정·D-day·Credential Manager를 초기화하거나 덮어쓰지 말 것.
- provider 저장소는 읽기만 하고, 개인 파일명·작업 제목·경로·토큰을 로그나 GitHub에 공개하지 말 것.
- 실제 Google Tasks 쓰기는 별도로 지정한 시험 항목 없이는 수행하지 말 것.
- AI CLI 실행·메시지 전송·시작프로그램 등록·에이전트 위임은 하지 말 것.
- 기존 macOS 릴리스와 기존 Windows 자산을 덮어쓰지 말 것.
- 미실행·실패한 검증은 명확히 구분하고 완료로 보고하지 말 것.

최종적으로 변경 내용, 검증 결과, 설치본 확인, 커밋·릴리스 링크, 남은 검증과 최종 판정을 알려줘.
```

## 1. 시작 기준과 현재 상태

- 저장소: `newskool4d-sketch/orbit`, 브랜치 `main`.
- 구현 커밋: `22a606e`(계정·초기 응답·포커스), `d59dfd8`(기존 원격 Google 동기화 변경 통합), `54c2459`(R06~R09).
- 인계 작성 시 원격 main: `70f927f7607c5fc250265a19a02809d3d741c984`. 이 인계 문서는 그 뒤에 추가됨. 위 커밋으로 되돌리지 말고 최신 main을 사용함.
- Mac: 1.0.3(빌드 4) 설치·재실행 완료. 자체 시험 54/54, 공용 JS 43/43, 실제 표시한 격리 WKWebView 시험 56/56 통과. 실제 설치 화면에서 부분 검색 안내·필터·복구 동작 확인.
- macOS 릴리스: [1.0.3 시험판](https://github.com/newskool4d-sketch/orbit/releases/tag/macos-2026.10.26-2). Windows용 패키지가 아니므로 Windows에 설치하지 않음.
- **현재 Windows 소스는 아직 네이티브 빌드·전체 gate를 실행하지 않았음.** Windows 배포 자산도 교체하지 않았음. 전체 플랫폼 판정은 PARTIAL임.
- `Windows/VERIFICATION.md`의 2026-09-17 PASS와 기존 Windows 릴리스는 과거 소스의 결과이며 이번 변경의 검증 근거가 아님. Mac의 WKWebView도 Windows WebView2 시험을 대신하지 않음.

## 2. 수정 내용과 확인할 파일

| 항목 | 변경 | 핵심 파일 |
| --- | --- | --- |
| R03 갱신 포커스 | 공용 UI가 항목·영역·위치를 기억해 DOM 갱신 후 복원함. Windows 파일·AI·일정의 불투명 ID를 재사용함 | `Resources/app.js`, `Windows/src/Orbit.Windows/LocalProviders.cs`, `CodexProvider.cs`, `Google.cs`, `Program.cs` |
| R04 설정 포커스 | 배경 inert, Tab·Shift+Tab 순환, Escape 후 진입 버튼 복귀, 중첩 D-day 상태 처리 | `Resources/app.js`, `Resources/index.html` |
| R05 한글 검색 | 문자 key와 물리 code=KeyK를 함께 판정함. Ctrl+K에서 설정·D-day를 닫고 파일 검색으로 이동함 | `Resources/app.js` |
| R06 통합 최신순 | LocalSnapshot 최종 Files를 modified, Agents를 updated 내림차순으로 정렬함 | `Windows/src/Orbit.Windows/LocalProviders.cs` |
| R07 드라이브 루트 | 끝 구분자가 있는 루트에 구분자를 중복 추가하지 않음. 루트 자신·형제 접두사·경로 이탈은 계속 거부함 | `Windows/src/Orbit.Windows/Policies.cs` |
| R08 부분 탐색 | 숨김·점 항목과 node_modules/build/dist/vendor 제외. 하위 폴더 실패 시 정상 결과 보존·부분 검색, 루트 열거 실패 시 오류와 수집 결과 표시 | `Windows/src/Orbit.Windows/LocalProviders.cs` |
| R09 소스 안내 | 초기 읽기·정상 빈 결과·연결 필요·오류·부분 검색을 구분함. 현재 필터에만 안내하고 사용 가능한 카드 유지. 다시 읽기·연결 설정 제공 | `Resources/app.js`, `Resources/readability.css` |

주요 회귀 fixture는 `Windows/src/Orbit.Windows/SelfTests.cs`, `tests/dday-integration.test.cjs`, `tests/ui-focus-fixture.js`에 있음. R01 신규 Google 토큰 혼합 방지와 R02 초기 ready 응답은 macOS 수정이며, Windows의 기존 fresh 토큰 구분과 Google 독립 동기화 보완도 보존해야 함.

## 3. 실행 원칙과 환경

1. 저장소 `AGENTS.md`, `Windows/AGENTS.md`와 해당 Windows 환경의 상위 지침을 읽음. Mac 전용 절대 경로의 지침이 접근 불가하면 그 범위를 기록하고 읽은 것으로 처리하지 않음. 위임 에이전트는 사용하지 않음.
2. Windows 11 x64, Git, Node.js, PowerShell, **.NET SDK 10.0.400**, Microsoft Edge WebView2 Runtime이 필요함. SDK는 `Windows/global.json`에 고정되어 있음. PowerShell에서 저장소 루트를 작업 디렉터리로 사용함.
3. `common.ps1`은 `%LOCALAPPDATA%/OrbitDevelopment/dotnet-10.0.400/dotnet.exe`를 먼저 찾고 없으면 PATH의 dotnet을 사용함. WebView2 SDK는 잠금 파일의 1.0.4191.47을 사용함. 검증 편의를 위해 SDK·의존성 잠금이나 시스템 정책을 변경하지 않음.
4. 사용자 provider 저장소는 읽기만 함. 실제 파일명·작업 제목·개인 경로·토큰을 로그·Git·릴리스에 넣지 않음. Google 실제 Tasks 쓰기는 사용자가 지정한 시험 항목이 별도로 있어야 함.
5. `ReadSqlite.cs`의 **winsqlite3 3.51.1 제한을 제거하지 않음**. WAL이 있고 SHM이 없는 경우의 거부도 유지함. 다른 버전을 지원하려면 DB/WAL/SHM 해시 불변과 쓰기 I/O 0 검증을 별도로 수행해야 함.

## 4. 실행 순서

### A. 작업트리와 실행 환경 확인

PowerShell에서 저장소 루트로 이동한 뒤 실행함. 로컬 변경이 있으면 보존·검토하고 중단하며 reset·강제 checkout으로 지우지 않음.

```powershell
$ErrorActionPreference = 'Stop'
git status --short --branch
# 로컬 변경이 없음을 확인한 뒤 진행
git pull --ff-only origin main
if ($LASTEXITCODE -ne 0) { throw 'Git 동기화 실패' }
git merge-base --is-ancestor 70f927f HEAD
if ($LASTEXITCODE -ne 0) { throw '인계 기준 변경이 포함되지 않음' }
git log -1 --oneline
node --version
$PSVersionTable.PSVersion
[Environment]::Version
& {
    . .\Windows\scripts\common.ps1
    & $OrbitDotnet --version
    if ($LASTEXITCODE -ne 0) { throw 'SDK 확인 실패' }
}
```

SDK 출력은 10.0.400이어야 함. 더 높은 SDK만 있는 경우 잠금을 풀어 통과시키지 말고 필요한 환경을 준비함. PowerShell이 .NET 10 기반이면 전체 test.ps1이 C# D-day 소스 시험도 실행함. 해당 분기가 생략된 환경은 검증 기록에 명시함.

### B. 현재 소스 빌드와 전체 자동 gate

각 단계가 통과한 뒤 다음 단계로 진행함.

```powershell
.\Windows\scripts\build.ps1
.\Windows\scripts\test.ps1 -NativeUi
```

`build.ps1`은 locked restore와 Release 빌드, `test.ps1 -NativeUi`는 공용 JS·네이티브 자체 시험·SQLite 별도 프로세스 시험·WebView2 smoke·프로세스 재시작 복원을 수행함. JS 기대 수는 현재 43개이며 네이티브 총수는 이번 Windows 실행 결과를 기록함. 과거의 64개를 현재 기대 수로 고정하지 않음.

Credential Manager 회귀까지 확인할 때는 다음 명령을 사용함. `compat-<임의 ID>` 시험 자격증명을 만들고 finally에서 삭제하는 별도 시험이며 실제 계정 인증 시험은 아님.

```powershell
.\Windows\scripts\test.ps1 -NativeUi -Credential
```

`-SourceOnly`는 진단용 일부 시험으로 전체 gate를 대체하지 않음. 오래된 EXE 또는 배포 Resources 불일치가 검출되면 반드시 다시 빌드함. 네이티브 명령 실패·버전 거부를 무시해 PASS로 만들지 않음.

### C. 실패 시 수정과 재검증

- 컴파일 실패가 있으면 우선 LocalProviders의 타임스탬프 정렬·열거 delegate, PathPolicy 루트 정규화, SelfTests fixture 통합 부분을 실제 오류 위치와 대조함. 사전에 실패했다고 단정하지 않음.
- 변경 후 B 전체 gate를 다시 수행함. 공용 Resources를 수정하면 JS와 Windows UI를 재검증하고 macOS 회귀가 필요한 범위를 기록함. Mac 시험을 실행하지 못했다면 PASS로 갱신하지 않음.
- SQLite 버전 거부·Runtime 부재·UI 세션 부재는 코드 결함과 구분함. 실행하지 못한 검사는 미검증으로 남김.

### D. 실제 Windows 화면·파일 트리 검증

아래 표를 완료하고 나서 배포 후보를 만듦. 개인 데이터 대신 합성 파일·세션 트리를 우선 사용하며, 접근 거부 시험은 별도 fixture 디렉터리에서만 수행함.

| 항목 | 통과 기준 |
| --- | --- |
| R03 ID·포커스 | 동일 스냅샷·시간 표시 변경·목록 재정렬에서 같은 파일/AI/일정의 포커스 유지. 삭제 시 인접 항목 또는 검색으로 예측 가능하게 이동 |
| R04 설정 | 배경 컨트롤에 Tab이 빠져나가지 않음. 양방향 경계 순환, Escape 후 진입 버튼 복귀, D-day 중첩 후에도 배경 차단 |
| R05 입력기 | Windows 실제 한글·영문에서 Ctrl+K 검색 이동. 설정·D-day에서 실행해 닫기/검색 포커스와 조합 중 동작 확인 |
| R06 최신순 | 오래된 Drive/Claude 뒤 최신 Obsidian/Codex가 수집돼도 전체 목록은 최신순. 각 소스 필터와 오늘의 서비스별 최신 항목, 30파일/24작업 상한 유지 |
| R07 경계 | 드라이브 루트와 일반 폴더의 하위 fixture 파일 허용. 끝 구분자 유무와 대소문자 변형 확인. 루트 자신·부모 이탈·비슷한 이름의 형제·금지 링크는 거부 |
| R08 제외 | 숨김·점·node_modules/build/dist/vendor 하위 파일이 카드에 포함되지 않음. 의도적 제외만으로 부분 실패가 되지 않음 |
| R08 부분 실패 | 하위 접근 거부에서 정상 형제 결과 보존+부분 검색. 루트 지연 열거 실패 시 수집 결과+오류. 시간·깊이·개수 제한 및 reparse/cloud 정책 유지 |
| R09 상태 안내 | 초기 읽기/정상 빈 결과/연결 필요/오류/부분 검색이 구분됨. 다른 필터의 오류가 섞이지 않고 정상 카드 유지. 원문 예외·개인 경로 비노출 |
| R09 복구 | 다시 읽기 중 중복 클릭 차단, 갱신 완료와 카드·필터 유지. 연결 설정 열기/Escape 복귀. 부분 검색의 결과 없음은 읽은 범위로 표현 |
| 기존 기능 | Moss/Pearl/Midnight, 글자 크기, D-day 재시작 복원, 트레이·단일 실행·창 표시와 범위 내 배율에서 회귀 없음 |

추가한 자동 시험 이름: `mixed-source-files-latest-first`, `mixed-provider-agents-latest-first`, `drive-root-descendant-allowed`, `trailing-separator-descendant-allowed`, `path-self-parent-and-prefix-rejected`, `excluded-folders-and-hidden-entries-omitted`, `intentional-exclusions-are-not-partial-failures`, `denied-child-preserves-sibling-results-with-partial-status`, `lazy-root-failure-keeps-collected-file-and-reports-error`, `per-source-thirty-file-cap-preserved`.

기존 `file-refresh-preserves-opaque-id`, `agent-refresh-preserves-source-specific-id`, `event-refresh-preserves-opaque-id`도 통과해야 함. 접근 거부·지연 예외 fixture 통과만으로 실제 ACL·junction/symlink·클라우드 reparse 장치까지 검증했다고 보고하지 않음. 필요한 권한이나 장치가 없으면 별도 미검증 항목으로 남김.

### E. 패키징·설치 교체·재실행

1. 결과를 기록하고 필요한 소스 수정을 커밋해 후보 기준 커밋을 확정함. 빌드 산출물·개인 자료는 커밋하지 않음.
2. 다음 스크립트로 새 Windows ZIP을 만듦. 이 스크립트는 빌드·전체 UI gate를 반복하고 self-contained publish 결과의 자체 시험·UI smoke도 실행함.

```powershell
.\Windows\scripts\package.ps1
```

3. `Windows/artifacts/Orbit.Windows-<시각>.zip`과 `.zip.sha256`, 압축 안 `BUILD-INFO.json`을 확인함. 소스 커밋이 후보와 일치하고 workingTreeDirty=false인지 확인함. 예상하지 않은 파일이 없는지도 확인함.
4. 현재 설치 경로는 실제 실행 파일·바로가기 대상으로 확인함. 과거의 `%LOCALAPPDATA%/Programs/Orbit` 기록을 현재 경로로 단정하지 않음.
5. 트레이에서 정상 종료한 뒤 기존 **프로그램 폴더**를 백업하고 새 ZIP의 EXE·DLL·Resources·라이선스 등 전체 배포 파일로 교체함. `%LOCALAPPDATA%/Orbit`의 설정·D-day·WebView2 데이터와 Credential Manager는 덮어쓰거나 공개하지 않음.
6. 설치본의 파일 해시를 후보와 대조하고 설치 경로의 EXE로 자체 시험·UI smoke를 실행함. 이어 실제 패널을 열어 D 항목과 재시작 복원을 확인함. 올바른 설치 경로의 단일 프로세스인지 확인함.
7. 교체 실패나 회귀 시 새 앱을 정상 종료하고 백업 프로그램 폴더로 되돌려 재실행함. 사용자 데이터는 시험 편의를 위해 초기화하지 않음.

### F. 기록·푸시·릴리스

- 새 날짜·실제 OS/SDK/WebView2/SQLite 버전·후보 커밋·시험 결과·미검증 사유·설치 해시·복원 결과를 기록함. `Windows/VERIFICATION.md`의 과거 결과는 역사로 남기고 현재 결과를 별도 절로 추가함.
- 소스/시험/비식별 검증 기록을 커밋·푸시한 뒤 원격 main 커밋을 확인함. 기존 macOS 릴리스와 Windows 릴리스 자산을 덮어쓰지 않음.
- 모든 필수 Windows gate·설치본 검증이 통과하면 사용하지 않은 Windows 시험판 태그로 ZIP·체크섬·비식별 검증 기록을 업로드함. 공개 후 다시 다운로드해 SHA-256과 압축 안 BUILD-INFO의 소스 일치를 확인함.
- gate 실패나 필수 화면 미검증이면 배포 완료로 판정하지 말고 원인·다음 명령을 남김. 실제 Google 연동·정확한 AI 세션 재개·다중 모니터 등 별도 경계는 그대로 명시함.

## 5. 다음 작업자의 결과 회신 양식

```text
기준 커밋 / 실제 실행한 설치본:
Windows / SDK / PowerShell .NET / WebView2 / winsqlite3 버전:
빌드:
JS / 네이티브 / SQLite / UI smoke / 재시작 복원:
Credential 시험(실행/미실행):
R03~R09 실제 화면·입력기·파일 트리 결과:
실제 ACL / 링크·클라우드 장치 검사(실행/미실행):
패키지 SHA-256 / BUILD-INFO 일치 / 설치본 일치:
커밋·푸시 / 릴리스 URL / 재다운로드 해시:
남은 항목과 최종 판정(PASS/PARTIAL/BLOCKED/FAIL):
```

개인 파일명·작업 제목·계정·원문 예외 대신 합성 시험 ID와 성공/실패·건수만 회신함.

참고: [원 검토](2026-10-24-periodic-review.md), [포커스·키보드 수정](2026-10-26-focus-keyboard-fixes.md), [로컬 소스 수정·Mac 설치 검증](2026-10-26-local-source-fixes.md), [Windows 개발 안내](../../Windows/README.md).
