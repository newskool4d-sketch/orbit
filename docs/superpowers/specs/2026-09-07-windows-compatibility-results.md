# Orbit Windows 착수 점검 결과

확인일: 2026-09-07

상태: T0 착수 및 T3 공용 UI 계층 일부 선행 구현. 현재 호스트 기준 점검과 macOS 회귀시험 완료. Windows 실기기 게이트는 미실행.

계획: [Windows 착수·실행 계획](../plans/2026-09-06-windows-version-plan.md)

## 현재 호스트에서 확인한 사실

| 항목 | 관찰 결과 |
|---|---|
| 운영체제 / 프로세스 아키텍처 | Darwin / arm64 |
| Node | v24.14.1 |
| Swift | 명령 사용 가능, 기존 앱 빌드 성공 |
| .NET SDK | PATH 및 확인한 일반 설치 위치에서 실행 파일을 찾지 못함 |
| PowerShell | pwsh 명령을 찾지 못함 |
| 가상화 앱 | 확인한 기본 Applications 위치에 Parallels Desktop, UTM, VMware Fusion 없음 |
| Windows 실행 환경 | 현재 제공된 도구·호스트 정보에서 사용할 Windows 환경이 확인되지 않음 |
| Git | 현재 폴더의 `codex/windows-port` 전용 브랜치. 비공개 GitHub 동명 브랜치에 문서와 공용 UI 변경 동기화 |
| Windows PC | 사용자가 개인 Windows 노트북을 보유함. 현재 Codex 호스트 목록에는 아직 연결되지 않음 |

위 결과는 확인한 경로·명령에 한정한다. 다른 위치의 설치나 사용자가 별도로 보유한 PC가 없다는 의미는 아니다. 임의의 원격 호스트에 접속하거나 SDK·가상 머신을 시스템에 설치하지 않았다.

## 기준 시험

2026-09-07 현재 체크아웃에서 실행했다. Windows 구현의 시험 결과가 아니다.

| 명령 | 결과 |
|---|---|
| rtk proxy node --test tests/web.test.cjs tests/platform.test.cjs | 12 passed, 0 failed |
| rtk proxy bash scripts/build.sh | 종료 코드 0, macOS 앱 빌드/서명 검증 성공 |
| rtk proxy build/Orbit.app/Contents/MacOS/Orbit --self-test | 32 passed, 0 failed |

공용 웹 전송 어댑터와 플랫폼별 표시 분기를 제품 소스에 추가했다. 공급자 저장소, Google 계정·자격 증명은 변경하지 않았다. 기존 빌드 스크립트로 무시 대상 build/ 산출물을 다시 만들었다.

## T3 선행 구현 결과

- WebView2와 WKWebView 메시지를 하나의 `orbitPlatform` 어댑터로 분리했다.
- Windows에서는 `Ctrl K`, Segoe UI, 로그인 시 자동실행 설정 숨김, Codex·Claude 앱 열기 안내를 사용하도록 분기했다.
- macOS의 `⌘ K`, 로그인 시 자동실행, 기존 Claude Terminal 동작은 호환 경로로 유지했다.
- 브라우저 미리보기와 두 네이티브 브리지의 전송·수신 계약을 Node 합성 시험으로 검증했다.

이 결과는 Windows 네이티브 호스트, 실제 앱 열기, 공급자 저장소 접근을 검증한 것이 아니다.

## Windows 호환성 게이트

| 게이트 | 상태 | 아직 필요한 증거 |
|---|---|---|
| G0-A | not-run | Windows 11 x64, .NET 10 SDK, WebView2 및 대상 앱 버전 |
| G0-B | not-run | WPF/WebView2 문서·메시지, 트레이, 모달, 두 번째 실행 |
| G0-C | not-run | Windows Codex 스키마 및 합성 WAL 읽기 무변경 시험 |
| G0-D | not-run | Codex/Claude의 실제 도착 화면과 지원 대안 |
| G0-E | not-run | Windows Credential Manager blob·부분 실패·복구 시험 |
| G0-F | not-run | Windows Drive 스트리밍/미러링, 경로·cloud tag 시험 |

macOS arm64에서 위 게이트를 통과했다고 판정하거나, 검증하지 않은 SDK/앱 버전을 고정하지 않았다. .NET SDK를 macOS에 추가하는 것만으로 WPF·Credential Manager·Windows 앱 연결의 실기기 검증이 대체되지는 않는다.

## 다음 진행 조건

1. 개인 Windows 11 x64 노트북의 Codex 앱에서 비공개 저장소의 `codex/windows-port` 브랜치를 프로젝트로 연다. 터미널 실행은 필수 조건으로 두지 않는다.
2. Windows Codex 호스트가 확인되면 T0의 합성 시험부터 실행한다. 비밀번호·토큰을 문서나 대화에 남기지 않는다.
3. 실제 공급자와 Google 쓰기 시험은 각각 읽기 전용 경계·사용자 지정 테스트 항목을 지킨다.
4. T3 선행 구현은 Windows 실기기에서 WebView2 호스트와 연결한 뒤 완료 판정한다. T0/T2 완료로 간주하지 않는다.

Windows용 제품 코드·설치 패키지는 아직 생성하지 않았다. 배포 일정은 Windows 선행 검증 결과를 얻은 뒤 재산정한다.
