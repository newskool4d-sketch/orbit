# Windows 구현 검증 기록

## 2026-10-26 검증 (R03~R09·M1~M6 Windows 네이티브)

기준: 인계 문서 `docs/plans/2026-10-26-windows-handoff.md`, 최신 main `938cb2b` 위 후보 커밋. 과거(2026-09-17) 결과와 Mac 시험은 이번 근거로 쓰지 않음.

### 환경
Windows 11 Education x64 10.0.26300, .NET SDK 10.0.400(`%LOCALAPPDATA%/OrbitDevelopment`, PATH 변경 없음), PowerShell 7.6.9(.NET 10.0.14), Node.js 24.14.1, WebView2 Runtime 155.0.4283.62, SDK 패키지 1.0.4191.47, winsqlite3 3.53.4.

### 자동 gate (`build.ps1`, `test.ps1 -NativeUi -Credential`)
| 항목 | 상태 | 근거 |
| --- | --- | --- |
| Release 빌드 | PASS | 잠금 복원, 경고 0·오류 0 |
| 공용 JavaScript | PASS | 43/43 |
| C# D-day 소스 시험 | PASS | 36/36 (PowerShell .NET 10 분기) |
| 네이티브 자체 시험 | PASS | 113/113. R03 불투명 ID 유지 3종, R06~R08 신규 10종, M1~M6 신규 8종 포함 |
| Credential Manager | PASS | 경계·UTF-8·원자적 실패·네이티브 왕복(시험 자격증명 생성 후 삭제) |
| SQLite 읽기 전용 | PASS | 합성 active WAL fixture, 별도 프로세스 쓰기 0회/0바이트, DB/WAL/SHM 해시 불변 |
| WebView2 smoke | PASS | D-day UI(9조합), 배율 상당 40조합 넘침·겹침 0, 네이티브 왕복 bridgeAck, 단일 실행 표시 요청 1 |
| 재시작 복원 | PASS | 격리 저장소 2개 프로세스, D-day 1개·`xlarge` 복원, 2차 실행 저장 파일 해시 불변 |

### winsqlite3 3.53.4 허용
최초 gate는 `--sqlite-test`에서 `sqlite-version-unverified`로 중단됨(읽기 거부, 파일 불변·쓰기 0). 이 PC의 시스템 winsqlite3가 3.53.4이기 때문이며 코드 결함이 아님. 인계 문서의 조건(해시 불변·쓰기 I/O 0 별도 검증)에 따라 다음을 확인한 뒤 사용자 승인으로 허용 목록에 3.53.4를 추가함. 3.51.1 허용, WAL 있음·SHM 없음 거부, `mode=ro&readonly_shm=1`, 쓰기 문장 거부는 그대로임.

| 검사 | 결과 |
| --- | --- |
| 합성 WAL fixture `--sqlite-test` 반복 | 5/5 PASS, 해시 불변, 쓰기 0회/0바이트 |
| 실제 Codex DB 열 이름 조회 | 조회 성공. thread_history DB/WAL/SHM 해시 불변. state DB의 WAL/SHM은 실행 중인 Codex가 직접 갱신함을 대조 측정으로 확인해 해시 판정에서 제외 |
| 실제 state DB를 Orbit 리더로 열기 | Orbit 프로세스 쓰기 0회/0바이트 |

다른 SQLite 버전은 계속 읽지 않음. 이 검사는 위 조건에서의 결과이며 모든 상황의 무변경성을 증명하지 않음.

### 패키지·설치본 (후보 `b64a5a4`)
| 항목 | 상태 | 근거 |
| --- | --- | --- |
| `package.ps1` | PASS | 빌드·전체 gate 반복, publish 자체 시험 113/113·UI smoke 통과 |
| 패키지 | PASS | `Orbit.Windows-20261026-111810.zip` SHA-256 `50FBAFF1F25C8E310E42772372E54FF8FF13847FB7828E9B40043A1E7860F800`, BUILD-INFO baseCommit `b64a5a4`·workingTreeDirty=false, 파일 구성 직전 배포본과 동일(498개) |
| 설치 교체 | PASS | 실제 경로(바로가기 대상) `%LOCALAPPDATA%/Programs/Orbit`. 기존 폴더를 해시 동일 백업 후 ZIP 내용으로 교체, 설치본 498개 파일 해시가 ZIP과 일치 |
| 설치본 자동 시험 | PASS | 자체 시험 113/113, SQLite 읽기 전용, D-day UI·배율 40조합·WebView2 왕복 |
| 재시작 복원 | PASS | 앱 내 종료 후 재실행, 테마·D-day 목록 유지, 설치 경로 단일 프로세스, 사용자 설정·D-day 파일 해시 불변 |

### 실제 화면 (설치본, 실제 사용자 데이터 읽기 전용)
| 항목 | 상태 | 근거 |
| --- | --- | --- |
| R03 갱신 포커스 | PASS(부분) | 파일 카드에 포커스를 둔 채 전체 갱신 완료 후 같은 카드에 포커스 유지. 재정렬·삭제 상황은 자체 시험·JS fixture로만 확인 |
| R04 설정 포커스 | PASS | Shift+Tab 첫 요소→마지막, Tab 마지막→닫기 버튼 순환, Escape 후 설정 버튼 복귀. D-day 시트에서도 Shift+Tab이 시트 안에 머묾 |
| R05 입력기 | PASS | 실제 Microsoft 한국어 IME 한글 모드(표시기 '가', 검색창 'ㅏ' 입력 확인)에서 필터 칩·설정 열림 상태 Ctrl+K → 설정 닫힘·검색 포커스. 한글 조합 중(D-day 제목 'ㄱ')·영문 모드의 D-day 시트 Ctrl+K → 시트 닫힘·빈 검색창 포커스, 남은 자모 없음·D-day 미추가 |
| R06 최신순 | PASS(부분) | 전체·소스별 목록이 수정 시각 최신순, 폴더별 최대 30개 표시. 혼합 소스 역전 상황은 자체 시험으로 확인 |
| R07·R08 경계·제외 | 미검증(실제 트리) | 합성 fixture 자체 시험만 통과. 실제 ACL·junction/symlink·클라우드 reparse 장치 시험과 합성 트리 화면 시험은 실행하지 않음(일반 실행은 사용자 설정 경로를 사용하므로 경로를 바꾸지 않음) |
| R09 상태 안내 | PASS | Drive·Obsidian '일부 범위만 검색' 안내와 정상 카드 공존, Google Drive 필터에서 Obsidian 안내 미표시, Calendar 연결 필요 안내, 부분 검색 결과 없음은 '현재 읽은 파일 중'으로 표현, 원문 예외·경로 비노출 |
| R09 복구 | PASS | 다시 읽기 중 두 버튼 비활성(중복 클릭 차단), 완료 후 재활성·카드·필터 유지 |
| Codex 읽기 | PASS | 연결 상태 '연결됨·읽기 전용 로컬 기록', AI 작업 48개 표시 |

관찰: Drive(Google Drive 스트리밍)는 시간 제한 때문에 매 스캔 '일부 범위'로 표시되며 수집 건수가 실행마다 달라짐(재시작 전 60개, 후 41개). Google 일정·할 일은 실제 계정의 토큰이 만료되어 재연결 안내(M3)가 표시됨. 재연결·실제 Google 쓰기는 수행하지 않음.

### 판정
자동 gate·설치본·핵심 화면(R04·R05·R09) PASS. R03·R06은 화면 부분 확인, R07·R08 실제 장치 경계·정확한 AI 대화 재개·실제 Google 연동·다중 모니터·macOS 회귀는 미검증. 전체 판정: PARTIAL(시험판).

## 2026-09-17 검증 (과거 소스)

검증일: 2026-09-17. 전체 판정: PARTIAL.
사용자 범위: “Windows 호환성 확인과 실행부 구현이며, 이후 UI 상태 처리와 실제 연동 검증”

### 환경
Windows 11 Education x64 10.0.26200, .NET SDK 10.0.400, WebView2 Runtime 152.0.4191 계열, SDK 패키지 1.0.4191.47, winsqlite3 3.51.1. .NET SDK는 개발 전용 로컬 폴더에 배치했고 PATH를 변경하지 않았습니다.

### 확인 결과
| 항목 | 상태 | 근거 및 범위 |
| --- | --- | --- |
| Windows Release 빌드 | PASS | 오류 0. WPF 매니페스트가 DPI 정책을 관리하므로 NotifyIcon용 WinForms의 WFO0003만 사유를 명시해 제외 |
| JavaScript 테스트 | PASS | 공용 UI·플랫폼·D-day·글자 크기 35/35 |
| 네이티브 자동 검사 | PASS | 브리지, PKCE/state, 자격증명 경계, Google 합성 응답, D-day 저장소 등 64/64 |
| Credential Manager | PASS | 별도 시험 자격증명 2560바이트 저장·조회·삭제 |
| SQLite 읽기 | PASS | 합성 active WAL fixture를 별도 프로세스로 조회, 쓰기 I/O 0회/0바이트 및 DB/WAL/SHM 해시 불변 |
| 로컬 실제 자료 | PASS | 파일 카드 60개, AI 기록 48개; Drive/Obsidian/Claude/Codex 연결 상태 확인. 내용·경로를 로그에 출력하지 않음 |
| 실제 WebView2 | PASS | Windows 플랫폼, Ctrl K, 시작프로그램 옵션 숨김, AI 루트 설정, 실제 메시지 요청·응답 bridgeAck=true |
| UI 시각 점검 | PASS | moss/pearl/cobalt와 글자 크기 3단계, D-day 관리 화면 캡처 확인 |
| 배율 상당 렌더링 | PASS | 현재 1536×816 작업 영역에서 100·125·150·200%의 논리 영역을 재현하고 보통·아주 크게 × 5개 화면 40조합의 넘침·겹침 검사와 캡처 확인 |
| 실제 OS 배율 | PASS(장치 범위) | 앱 창 DPI 120·144·168에서 125·150·175%를 확인하고 각 배율에서 25개 화면 캡처와 UI 검사를 통과. 이 장치는 100·200%를 적용하지 못해 배율 상당 검사로 보완 |
| 재시작 복원 | PASS | 격리 저장소에서 앱 프로세스를 두 번 실행해 D-day 1개와 `xlarge` 복원, 2차 실행의 저장 파일 해시 불변 확인 |
| 키보드·Resume | PASS | 실제 WebView2 합성 입력과 사용자 실기기 확인에서 Tab/Shift+Tab·Esc·Ctrl K가 정상 작동. Modern Standby 506→507 물리 절전·복귀 후 동일 PID·시작 시각, 표시 창, 사용자 데이터 해시 불변 확인 |
| 설치본 교체 | PASS | `%LOCALAPPDATA%/Programs/Orbit`에서 후보 DLL 해시 일치, 자체 시험·UI smoke, 바로가기와 사용자 설정 불변 확인 |
| 중복 실행 | PASS | 첫 프로세스 showRequests=2, 두 프로세스 모두 exit 0 |
| Codex·Claude 앱 실행 | PASS | 설치 앱 활성화 및 프로세스 생존. 메시지 전송 없음 |
| 정확한 기존 AI 대화 재개 | 미검증 | 현재 appHome 방식으로 작업 직접 선택. 정확한 세션/프로젝트 새 작업 경로 미구현 |
| Google 실제 OAuth·Calendar·Tasks | 미검증 | 사용자 Desktop OAuth JSON 및 지정 시험 Tasks 항목 필요. 합성 HTTP 검증만 완료 |
| 실제 파일 클릭 후 대상 앱 확인 | 미검증 | 메타데이터 조회와 경로 정책 검사만 검증 |
| 다중 모니터 | 미검증 | 단일 내장 디스플레이에서만 실제 배율을 전환함 |
| macOS 회귀 실행 | 미검증 | Swift 소스 보존 및 JS 호환 테스트. macOS 런타임 없음 |

WebView 검증은 합성 데이터 모드에서 네이티브 메시지 전달을 확인합니다. 실제 Google 인증/서버 동작을 대체하지 않습니다. SQLite 검사는 active WAL 한 조건이며 모든 버전·상황의 무변경성을 증명하지 않습니다.

### 변경 및 남은 범위
Windows/에 C# 호스트, Google/로컬 제공자, Credential Manager, 읽기 전용 SQLite, D-day 저장소와 빌드·검증·패키징 스크립트를 추가했습니다. 공용 UI에 세 단계 글자 크기와 D-day 관리 화면을 추가했습니다.

시작프로그램·AI CLI·클립보드 이어하기·외부 게시는 수행하지 않았습니다. 개발 SDK 첫 실행에서 생성된 localhost 개발 인증서는 해당 신규 지문 하나만 확인 후 제거했고, 후속 스크립트는 생성을 차단합니다.

중복 실행 검사 초회는 검사 로그 파일의 공유 잠금 때문에 실패했습니다. 표준출력 파이프로 검사 도구를 변경해 정상 동작을 재확인했습니다.

### 재현
Windows/scripts/build.ps1
Windows/scripts/test.ps1 -NativeUi -Credential
Windows/scripts/package.ps1

합성 화면은 artifacts/qa/, 배포 ZIP과 SHA256 파일은 artifacts/에 있습니다. 배포판은 코드 서명과 실제 Google 계정 연동 검증을 마친 정식 릴리스가 아닙니다.

### GitHub 반영 전 추가 확인

사용자가 Google 연결 완료를 보고했습니다. Calendar 목록 조회와 Tasks 쓰기의 독립적인 실연동 검증은 여전히 미검증입니다. 새 아이콘을 EXE·창·트레이에 적용하고 ICO 내부 9개 PNG 프레임을 검사했습니다. SDK 10.0.400으로 복원·빌드해 JavaScript 35개·네이티브 64개·SQLite 읽기 전용 검사를 통과했습니다. SDK 경로는 검사 시점에 따라 가용성이 달랐으므로 실행 환경에서 확인해야 합니다. macOS 네이티브 재빌드는 이번 Windows 환경에서 수행하지 않았습니다.
