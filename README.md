# Orbit

일정·할 일·최근 파일·AI 작업을 확인하는 개인용 데스크톱 앱입니다. 이 저장소는 **macOS 원본과 Windows 포트**를 함께 관리합니다.

| 플랫폼 | 실행부 | 안내 |
| --- | --- | --- |
| macOS | Swift / AppKit / WebKit, 메뉴 막대 앱 | 아래 macOS 안내 |
| Windows | .NET 10 / WPF / WebView2, 트레이 앱 | [Windows 실행·빌드 안내](Windows/README.md) · [검증 범위](Windows/VERIFICATION.md) |

공용 UI는 Resources/, macOS 실행부는 Sources/, Windows 실행부는 Windows/src/Orbit.Windows/에 있습니다. 두 플랫폼의 인증 저장소와 AI 작업 열기 동작은 다릅니다. Windows 포트는 시험판이며 정확한 기존 AI 대화 재개 등 제한은 Windows 안내를 확인하세요.

Windows 빌드: PowerShell에서 ./Windows/scripts/build.ps1, 검증: ./Windows/scripts/test.ps1 -NativeUi. .NET SDK 10.0.400과 WebView2 Runtime이 필요합니다. 실행 패키지는 ./Windows/scripts/package.ps1로 생성합니다. ZIP·실행 바이너리·OAuth JSON·로컬 설정은 소스 커밋에 포함하지 않습니다.

## macOS 안내

macOS 메뉴 막대에서 일정·할 일·최근 파일·Codex·Claude Code 작업을 확인하는 개인용 앱입니다. 승인된 Moss/Pearl/Midnight 디자인을 실제 AppKit 팝오버로 구현했습니다.

## 실행

빌드된 `build/Orbit.app`을 사용자 응용 프로그램 폴더(`~/Applications`)에 복사해 열고, 메뉴 막대의 Orbit 궤도 아이콘을 누릅니다. 아이콘 우클릭으로 앱을 종료할 수 있습니다. 설정의 ‘로그인할 때 Orbit 열기’는 기본적으로 꺼져 있습니다.

## 사용

- **오늘:** 다음 일정, 처리할 할 일, 최근 파일, Codex·Claude Code 각 최신 작업.
- **캘린더:** 선택된 Google 캘린더의 앞으로 7일 일정을 날짜별 목록으로 표시하고 각 일정을 바로 엽니다.
- **최근 파일:** Drive·Obsidian 필터, 한글 검색, 파일 바로 열기. 앱 안에서 `⌘K`로 검색합니다.
- **AI 작업:** 전체·Codex·Claude Code 필터. Codex는 해당 작업을 엽니다. Claude Code는 검증된 `claude --resume` 명령을 복사하고 Terminal을 엽니다. 사용자가 붙여넣고 Enter를 눌러 이어갑니다.
- **설정:** 테마, 고정, 파일 폴더, Google 로그인, 소스별 연결/오류 상태.

## 연결 범위

Google Drive와 Obsidian은 자동 탐지한 로컬 동기화 폴더에서 파일 이름·수정 시각만 읽습니다. 파일 내용은 읽거나 외부에 전송하지 않습니다. Drive/Obsidian 파일은 변경하지 않습니다.

파일은 소스별 최대 30개, 깊이 7단계, 20,000개 항목 또는 4초 탐색 범위 내 최근 수정순입니다. 큰 폴더는 일부 범위만 검색할 수 있으며 화면과 연결 상태에 표시됩니다. 필요한 경우 설정에서 현재 작업 폴더로 범위를 좁히세요. ‘최근 파일’은 최근 열람순이 아니라 수정순입니다.

Codex는 로컬 `state_5.sqlite`, `thread_history_1.sqlite`를 읽기 전용으로 조회합니다. 사용자 작업만 표시하고 내부 guardian/subagent·보관 작업은 제외합니다. Claude Code는 `.claude/projects`의 상위 세션 JSONL을 처음 128 KiB/마지막 256 KiB까지만 읽습니다. 응답 완료가 확인되면 완료로, 불확실하면 최근 기록으로 표시합니다. Claude Desktop 일반 채팅과 클라우드 세션은 포함하지 않습니다. 각 서비스는 최신 24개까지 표시합니다.

패널이 보이는 동안 60초마다 갱신하며 파일 목록은 120초 동안 재사용합니다. 패널을 열거나 새로고침해도 갱신됩니다. 앱을 닫으면 백그라운드 상시 스캔을 하지 않습니다.

## Google 최초 연결

Google Cloud의 **Desktop app OAuth 클라이언트 JSON**과 사용자 로그인이 필요합니다. 기존 Codex/Claude 커넥터 인증을 가져오지 않습니다.

1. Google Cloud 프로젝트에서 Calendar API와 Tasks API를 활성화합니다.
2. OAuth 동의 화면을 설정하고 테스트 사용자에 본인 계정을 추가합니다.
3. 데스크톱 앱 OAuth 클라이언트 JSON을 다운로드합니다.
4. Orbit 설정 → OAuth JSON 가져오기 → Google 연결.
5. 기본 브라우저에서 요청 권한을 확인하고 직접 동의합니다.

앱 설정의 ‘처음 연결하는 방법’에 전체 안내가 있습니다. [Google 공식 문서](https://developers.google.com/identity/protocols/oauth2/native-app)에 따라 PKCE(S256), 무작위 state, loopback 전용 임시 콜백을 사용합니다. Calendar는 읽기 전용, Tasks는 완료/다시 열기를 위한 읽기·쓰기 권한입니다. 토큰과 클라이언트 설정은 macOS 키체인에 저장합니다.

선택된 캘린더의 7일 일정과 Tasks 목록을 페이지별로 가져옵니다. 오늘 할 일에는 미완료·기한 없음·오늘/기한 지난 항목을 표시하며 ‘전체 보기’에서 미래·완료 항목도 볼 수 있습니다. 체크박스는 Google 저장 성공 후 상태를 확정합니다. Google 오류 시 마지막 성공 데이터와 오류를 표시합니다. 연결 해제는 이 맥의 Orbit 토큰을 제거하며, Google 계정의 앱 접근 철회는 Google 연결 관리에서 할 수 있습니다.

## 빌드 및 검증

Apple Silicon Mac, macOS 14 이상, Xcode Command Line Tools/Swift 필요. 서드파티 라이브러리와 상시 웹 서버는 사용하지 않습니다.

```sh
bash scripts/build.sh
build/Orbit.app/Contents/MacOS/Orbit --self-test
build/Orbit.app/Contents/MacOS/Orbit --smoke-test
node --test tests/web.test.cjs
```

테스트는 가상 데이터로 OAuth 응답 검증, PKCE, 셸 인자, Google 응답 처리, 할 일 PATCH, 한글 파일 검색, 읽기 전용 SQLite, 내부 작업 필터를 검사합니다. smoke-test는 개인 파일명이나 작업 제목을 출력하지 않고 건수와 연결 상태만 출력합니다.

앱은 개인용 ad-hoc 서명입니다. App Store 배포·공증 대상이 아니며 macOS 키체인 접근 확인이 나타날 수 있습니다. Codex/Claude의 로컬 기록 형식이나 URL 처리 방식이 바뀌면 해당 연결 코드 조정이 필요합니다.

## 검증 경계

2026-09-06 기준 설치 앱의 패널 표시, 캘린더 탭과 7일 일정 목록, 최근 파일 한글 검색, Claude Code 필터, 설정 열기/닫기, Moss·Pearl·Midnight 전환, 고정 on/off, 수동 새로고침을 확인했습니다. Swift 32개 + 웹 5개 테스트 및 빌드·서명 검증이 통과했습니다. 로컬 smoke-test 결과는 파일 60개, Codex 24개, Claude Code 13개이며 네 소스 모두 연결됨입니다.

Google 계정의 OAuth 동의와 Calendar·Tasks 읽기는 실제 계정으로 확인했습니다. Tasks 완료·다시 열기 쓰기는 아직 실제 항목으로 검증하지 않았습니다. Codex·Claude 작업 클릭 후 Orbit 패널이 닫히는 것은 확인했지만 대상 앱(Codex/Terminal)의 UI 접근은 자동화 도구 안전 정책에 의해 차단되어 정확한 작업 열기/이어하기 끝단은 미검증입니다. 실제 파일 열기 및 로그인 시 자동 시작도 별도 사용자 확인이 필요합니다. 테스트 후 테마는 Moss, 패널 고정과 로그인 시 시작은 꺼진 상태입니다.
