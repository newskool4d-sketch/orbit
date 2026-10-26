# Orbit 포커스·설정·검색 단축키 수정 — 2026-10-26

대상: 정기 검토 R03·R04·R05. 이전 R01·R02 변경과 설치 상태는 유지하고 이번 변경을 작업트리와 build/Orbit.app에 반영함.

## 변경 결과

### 화면 갱신 시 포커스 유지

- 공용 UI가 DOM을 재생성하기 전에 현재 항목의 ID·소속 영역·목록 위치·스크롤 위치를 기억하고, 렌더링 후 같은 항목으로 포커스를 복원함.
- 목록의 정렬이 바뀌어도 ID로 항목을 찾음. 항목이 삭제되면 인접 항목으로 이동하고, 파일 목록이 비면 검색 입력으로 이동함.
- 중요 날짜 시계 갱신과 할 일 목록 전환에도 같은 복원 절차를 적용함. 설정 안에서 컨트롤이 비활성화되면 설정 안의 사용 가능한 컨트롤로 이동함.
- 갱신·처리 대기 중인 Tasks 체크박스는 aria-disabled로 잠그되 포커스를 유지함. 클릭 취소와 change 이벤트의 상태 검사로 중복 쓰기를 차단함. pending·unknown 상태의 중복 조작 방지도 유지함.
- 새로고침 버튼도 포커스를 유지하면서 처리 중 중복 요청을 차단함.
- macOS 파일 항목은 source와 네이티브 경로가 같은 경우 이전 불투명 ID를 재사용함. 경로는 표시 데이터에 추가하지 않음.
- Windows 파일·Codex·Claude 항목도 이전 스냅샷의 네이티브 식별 정보를 이용해 불투명 ID를 재사용함. 일정은 캘린더·이벤트 식별자별로 기존 불투명 ID를 유지함. 내부 경로·세션 식별자는 이 처리 때문에 웹 계층에 추가로 노출되지 않음. 이 C# 변경은 아직 네이티브 실행 미검증임.

### 설정 패널 포커스 제한

- 설정에 dialog·aria-modal 의미를 부여하고 배경 header·탭·본문·footer를 inert 처리함.
- Tab·Shift+Tab이 설정 안에서 순환함. Escape로 닫고 열었던 버튼으로 복귀함.
- 설정에서 중요 날짜 관리창이 열렸다 닫혀도 설정 뒤의 배경을 다시 활성화하지 않음.
- 스냅샷 갱신으로 footer의 연결 버튼이 교체되어도 같은 연결 버튼으로 복귀함.

### 한글 입력 상태 검색 단축키

- Cmd/Ctrl과 물리 키 code=KeyK를 함께 판정함. 기존 문자 key=k 판정은 호환용으로 유지함.
- 한글 key=ㅏ 또는 조합 중 key=Process에서도 검색으로 이동함. 일반 K 입력과 Alt 조합은 검색 단축키로 처리하지 않음.
- 설정·중요 날짜 창이 열려 있으면 닫고 최근 파일 검색으로 이동하며 배경 inert 상태도 해제함.

## 검증 증거

| 검사 | 결과 | 범위 |
| --- | --- | --- |
| 수정 전 UI 대조 | 26 통과 / 19 실패 | HEAD의 app.js·index.html·readability.css를 임시 시험 번들에 넣고 동일한 최종 45개 검사를 실행함. 작업 소스는 되돌리지 않음 |
| 수정 후 macOS 네이티브 회귀 | 45/45 통과 | 실제 표시한 NSWindow·WKWebView, 생산 Bridge·GoogleClient와 공용 UI. 인증·저장소는 합성 대역 |
| 공용 JavaScript | 38/38 통과 | 기존 네 시험 파일. 설정 중첩·Tab 순환, 한글/조합 키, 강제 change 이벤트의 중복 쓰기 차단 포함 |
| macOS 빌드·서명 | 통과 | scripts/build.sh 종료 코드 0. 새 공용 Resources 포함 |
| 새 빌드 자체 시험 | 50/50 통과 | 기존 48개와 파일 ID 재사용·소스/경로 구분 2개 |
| 변경 검토·공백 검사 | 통과 | git diff와 git diff --check |
| Windows build.ps1·test.ps1 | 미실행 | 현재 Mac에 pwsh·dotnet과 Windows/WPF 실행 환경이 없음. 기존 전체 gate를 완화하거나 대체하지 않음 |

네이티브 회귀 시험은 기존 OAuth·초기 브리지 검사 13개도 계속 실행함. 나머지는 실제 DOM의 포커스, 항목 삭제·재정렬, 할 일 잠금, 설정·중요 날짜 중첩, 키 이벤트를 검사함. 표시되지 않은 미리보기 카드를 잘못 선택하지 않도록 시험 선택자를 실제 보이는 목록으로 한정했고, 시험 창의 document.hidden=false도 별도로 확인함.

macOS·Windows 플랫폼 값에서 각각 metaKey·ctrlKey와 key=k/ㅏ/Process 조합을 WKWebView에서 검사했음. 이는 Windows WebView2 자체 실행이나 실제 OS 입력기 전환·물리 키보드 시험을 대신하지 않음. 설치된 개인 데이터 화면에서 새 UI를 조작한 검증도 아님.

실제 Google 쓰기·AI 작업 재개·사용자 자료 변경은 수행하지 않았음. 시험 출력에는 합성 사례의 결과만 기록함.

## 변경 파일

- Resources/app.js, index.html, readability.css: 포커스 복원, 모달 상태, 단축키, 잠금 상태.
- Sources/Models.swift, Bridge.swift, SelfTests.swift: macOS 파일 ID 재사용과 시험.
- Windows/src/Orbit.Windows/LocalProviders.cs, CodexProvider.cs, Program.cs, Google.cs, SelfTests.cs: Windows ID 재사용과 네이티브 회귀 사례.
- tests/ui-focus-fixture.js, MacOSAuthBridgeTests.swift, dday-integration.test.cjs, windows-ui.test.cjs: 실제 DOM·호스트 및 공용 논리 회귀 검사.
- scripts/test-macos-auth-bridge.sh, README.md: UI fixture 연결과 검증 절차 안내.

## 후속 경계

- macOS와 공용 UI의 이번 수정·자동 검증은 PASS임. 전체 플랫폼 판정은 Windows 네이티브 검증이 남아 PARTIAL임.
- Windows 환경에서 `Windows/scripts/build.ps1` 후 `Windows/scripts/test.ps1 -NativeUi`를 수행해야 함. 추가한 C# 파일·AI·일정 ID 재사용 검사도 그때 실행됨.
- 초기 검증 시점에는 build/Orbit.app만 변경했음. 사용자의 후속 요청에 따른 최종 설치·배포 검증은 아래에 기록함.
- 정기 검토의 R06~R09, 실제 입력기 전환, 보조공학 검증은 별도 후속 범위임.

## 최종 통합 빌드·설치 검증

- 원격 main의 Google 독립 동기화·날짜 검증·Swift 콜백 보완·DMG 배포 절차를 통합함. Windows 충돌은 독립 동기화 구조를 보존하면서 일정 ID 재사용을 적용하고 합성 fixture를 통합함.
- 기준 소스 커밋: `d59dfd821e1f67fa665b3de9a5211be909487941`. 앱 버전 1.0.2, 빌드 3.
- 깨끗한 소스에서 scripts/package-macos.sh 실행: macOS 자체 시험 54/54, 웹 시험 38/38, arm64·최소 OS·서명·DMG 무결성·읽기 전용 마운트본 자체 시험 54/54 통과.
- 통합 후 scripts/test-macos-auth-bridge.sh의 실제 WKWebView 회귀 시험 45/45 통과.
- 배포 DMG 내부 앱을 /Applications/Orbit.app에 설치함. 기존 앱을 정상 종료·백업하고 새 앱을 재실행함. 백업은 `~/Applications/.Orbit-backups/Orbit-before-20261026-focus-build3.app`에 보존함.
- DMG·빌드·설치본의 번들 파일 12개가 SHA-256 기준으로 일치함. 설치본 자체 시험 54/54 통과. 단일 설치본 프로세스의 finishedLaunching=true·terminated=false 확인.
- 실행 파일 SHA-256: `1b02fe53188c5edc712ca7bdf798407e1d9d888445de17488ac4fce05ea0074d`.
- DMG SHA-256: `9173ad311371b845b4c3e1be3d5110a53df7aada96e995b6dbd5537033028797`.
- 실제 개인 데이터 화면 검증 도구는 두 차례 timeoutReached를 반환함. dots 연결 확인을 요청했으며 실제 설치 화면에서의 포커스 유지·설정 Tab 순환·OS 한글 입력 상태 단축키 검증은 아직 미완료임. 합성 WKWebView 검증을 실제 OS 입력기 검증으로 보고하지 않음.
- Windows 전체 gate는 실행하지 않았으며 Windows 배포 파일은 갱신하지 않음. macOS 배포는 기존과 같이 ad-hoc 서명이며 Apple 공증 없음.
