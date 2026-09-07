# Windows 구현 검증 기록

검증일: 2026-09-07. 전체 판정: PARTIAL.
사용자 범위: “Windows 호환성 확인과 실행부 구현이며, 이후 UI 상태 처리와 실제 연동 검증”

## 환경
Windows 11 Education x64 10.0.26200, .NET SDK 10.0.400, WebView2 Runtime 152.0.4191 계열, SDK 패키지 1.0.4191.47, winsqlite3 3.51.1. .NET SDK는 개발 전용 로컬 폴더에 배치했고 PATH를 변경하지 않았습니다.

## 확인 결과
| 항목 | 상태 | 근거 및 범위 |
| --- | --- | --- |
| Windows Release 빌드 | PASS | 오류 0. WPF 매니페스트가 DPI 정책을 관리하므로 NotifyIcon용 WinForms의 WFO0003만 사유를 명시해 제외 |
| JavaScript 테스트 | PASS | 기존 12 + 신규 상태 처리 3 = 15개 |
| 네이티브 자동 검사 | PASS | 브리지 검증, PKCE/state, 자격증명 경계, 합성 Google 응답 및 오류 등 30개 |
| Credential Manager | PASS | 별도 시험 자격증명 2560바이트 저장·조회·삭제 |
| SQLite 읽기 | PASS | 합성 active WAL fixture를 별도 프로세스로 조회, 쓰기 I/O 0회/0바이트 및 DB/WAL/SHM 해시 불변 |
| 로컬 실제 자료 | PASS | 파일 카드 60개, AI 기록 48개; Drive/Obsidian/Claude/Codex 연결 상태 확인. 내용·경로를 로그에 출력하지 않음 |
| 실제 WebView2 | PASS | Windows 플랫폼, Ctrl K, 시작프로그램 옵션 숨김, AI 루트 설정, 실제 메시지 요청·응답 bridgeAck=true |
| UI 시각 점검 | PASS | 125% 배율의 moss/pearl/cobalt, 설정 및 AI 카드 합성 화면 5장 확인 |
| 중복 실행 | PASS | 첫 프로세스 showRequests=2, 두 프로세스 모두 exit 0 |
| Codex·Claude 앱 실행 | PASS | 설치 앱 활성화 및 프로세스 생존. 메시지 전송 없음 |
| 정확한 기존 AI 대화 재개 | 미검증 | 현재 appHome 방식으로 작업 직접 선택. 정확한 세션/프로젝트 새 작업 경로 미구현 |
| Google 실제 OAuth·Calendar·Tasks | 미검증 | 사용자 Desktop OAuth JSON 및 지정 시험 Tasks 항목 필요. 합성 HTTP 검증만 완료 |
| 실제 파일 클릭 후 대상 앱 확인 | 미검증 | 메타데이터 조회와 경로 정책 검사만 검증 |
| 다중 모니터·배율 행렬·절전 복귀 | 미검증 | 현재 기본 화면 배치와 125%만 확인 |
| macOS 회귀 실행 | 미검증 | Swift 소스 보존 및 JS 호환 테스트. macOS 런타임 없음 |

WebView 검증은 합성 데이터 모드에서 네이티브 메시지 전달을 확인합니다. 실제 Google 인증/서버 동작을 대체하지 않습니다. SQLite 검사는 active WAL 한 조건이며 모든 버전·상황의 무변경성을 증명하지 않습니다.

## 변경 및 남은 범위
Windows/에 C# 호스트, Google/로컬 제공자, Credential Manager, 읽기 전용 SQLite, 빌드·검증·패키징 스크립트를 추가했습니다. Resources/app.js, index.html, platform.css의 상태 표시를 보완하고 tests/windows-ui.test.cjs를 추가했습니다.

시작프로그램·AI CLI·클립보드 이어하기·외부 게시·커밋·푸시는 수행하지 않았습니다. 개발 SDK 첫 실행에서 생성된 localhost 개발 인증서는 해당 신규 지문 하나만 확인 후 제거했고, 후속 스크립트는 생성을 차단합니다.

중복 실행 검사 초회는 검사 로그 파일의 공유 잠금 때문에 실패했습니다. 표준출력 파이프로 검사 도구를 변경해 정상 동작을 재확인했습니다.

## 재현
Windows/scripts/build.ps1
Windows/scripts/test.ps1 -NativeUi -Credential
Windows/scripts/package.ps1

합성 화면은 artifacts/qa/, 배포 ZIP과 SHA256 파일은 artifacts/에 있습니다. 배포판은 실제 계정 연동 검증을 마친 정식 릴리스가 아닙니다.

## GitHub 반영 전 추가 확인

사용자가 Google 연결 완료를 보고했습니다. Calendar 목록 조회와 Tasks 쓰기의 독립적인 실연동 검증은 여전히 미검증입니다. 새 아이콘을 EXE·창·트레이에 적용하고 ICO 내부 9개 PNG 프레임을 검사했습니다. 소스 이동 후 SDK 10.0.400으로 복원·비증분 빌드 및 JS 15개·네이티브 30개·SQLite 검사를 통과했습니다. SDK 경로는 검사 시점에 따라 가용성이 달랐으므로 실행 환경에서 확인해야 합니다. macOS 네이티브 재빌드는 이번 Windows 환경에서 수행하지 않았습니다.
