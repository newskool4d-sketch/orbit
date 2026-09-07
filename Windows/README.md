# Orbit Windows 시험판

Windows 11 x64에서 원본 Orbit UI를 실행하는 WPF/WebView2 호스트입니다. 2026-09-07 현재 이 PC에서 빌드 및 기본 연동을 검증했습니다. 전체 판정은 PARTIAL이며 세부 근거는 [VERIFICATION.md](VERIFICATION.md)를 확인하세요.

## 실행
1. artifacts의 Orbit.Windows-날짜.zip을 원하는 폴더에 모두 압축 해제합니다.
2. 폴더 안의 Orbit.Windows.exe를 실행합니다. DLL과 Resources 폴더를 함께 유지하세요.
3. 알림 영역의 Orbit 아이콘으로 창을 열고 닫습니다. 오른쪽 클릭 → 종료로 끝냅니다.
4. 설정에서 Drive, Obsidian, Codex, Claude 폴더를 확인합니다. 자동 검색이 모호하면 직접 선택합니다.

.NET 런타임을 포함하므로 별도 .NET 설치는 필요하지 않습니다. Microsoft Edge WebView2 Runtime은 필요하며 자동 설치하지 않습니다. 관리자 권한·시작프로그램 등록은 사용하지 않습니다. 코드 서명 및 설치 프로그램은 포함하지 않은 로컬 시험판입니다.

## 구현 범위
- WPF 창, 알림 영역, 단일 인스턴스, 고정/숨김, WebView2 UI, 세 테마, Ctrl K.
- Drive/Obsidian 파일 메타데이터, Claude Code 로컬 기록, Codex SQLite 최근 작업의 제한된 읽기.
- Codex/Claude 설치 앱 열기. 기존 대화의 정확한 재개 및 같은 프로젝트 새 작업 생성은 구현하지 않았으며 카드에 작업 직접 선택을 안내합니다.
- Google OAuth PKCE, Calendar 조회, Tasks 조회·완료·다시 열기 코드. 인증 정보는 Windows Credential Manager에 보관합니다.
- Tasks 처리 중·결과 불명 상태의 중복 조작 차단과 오류 표시.

Google 실제 계정 연동은 아직 미검증입니다. 클라이언트 파일 가져오기만으로 실제 API 검증이 완료되지는 않습니다.

## Google 설정
Google Cloud 프로젝트에서 Calendar API와 Tasks API 및 OAuth 동의 화면을 준비하고 Desktop 유형 OAuth 클라이언트 JSON을 발급받습니다. 설정에서 JSON을 가져오고 연결을 누른 뒤 시스템 브라우저에서 동의합니다. Calendar 읽기 및 Tasks 권한을 요청합니다. JSON 내용이나 토큰을 채팅에 붙이지 마세요.

실제 Tasks 완료·다시 열기 검증은 사용자가 지정한 시험 항목으로만 수행합니다. 연결 해제는 Orbit의 저장 자격증명을 제거하며 Google 계정 전체 권한 철회는 Google 계정 설정에서 별도로 관리합니다.

## 저장 위치와 제한
- 설정: %LOCALAPPDATA%/Orbit/settings.json
- WebView2 프로필: %LOCALAPPDATA%/Orbit/WebView2 (InPrivate 사용)
- 자격증명: 현재 Windows 사용자의 Orbit.Windows/v1/ 접두사 항목
- 개발 SDK·캐시: %LOCALAPPDATA%/OrbitDevelopment (배포판 실행에 불필요)

현재 Codex 읽기는 이 PC에서 검증한 winsqlite3 3.51.1에 한정됩니다. 다른 버전은 읽기를 중단하고 오류 상태를 표시합니다. WAL이 있고 SHM이 없는 경우도 읽지 않습니다. 스캔은 깊이·항목 수·시간을 제한하므로 모든 기록을 보여 주지는 않습니다. 다중 모니터 배치, 배율별 동작, 절전 복귀는 추가 검증이 필요합니다.

앱을 종료한 뒤 배포 폴더를 삭제하면 실행 파일을 제거할 수 있습니다. 설정·자격증명은 자동 삭제하지 않습니다. 원본 macOS Sources와 build.sh는 유지했습니다. 공용 UI 변경의 macOS 실제 실행은 미검증입니다.

## 개발
Windows/scripts/build.ps1: 잠금 파일 기반 복원 및 Release 빌드.
Windows/scripts/test.ps1 -NativeUi -Credential: JS, 네이티브, 합성 WAL, WebView2 및 별도 시험 자격증명 검증.
Windows/scripts/package.ps1: self-contained ZIP 생성 및 배포 실행 파일 검사.

SDK 10.0.400과 WebView2 SDK 1.0.4191.47을 고정했습니다. 개발 스크립트는 SDK 최초 실행 인증서 생성을 비활성화합니다. 업데이트와 배포는 별도 작업입니다.
