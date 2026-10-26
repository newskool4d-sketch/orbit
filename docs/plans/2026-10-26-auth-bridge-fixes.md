# Orbit 계정 토큰·초기 응답 수정 결과 — 2026-10-26

사용자가 승인한 R01·R02 수정과 검증을 완료함. 기준 커밋은 `025890e5810b6a54e91886b815d8fe8e663b95af`이며 변경은 작업트리에 있음.

## 변경 내용

- `Sources/GoogleClient.swift`: 신규 인증과 기존 토큰 갱신을 fresh 인자로 구분함. 신규 인증에서 refresh_token이 누락되거나 비어 있으면 실패 처리하고 기존 메모리·저장 계정을 보존함. 기존 인증 갱신 응답에서 refresh_token이 생략된 경우에만 기존 토큰을 재사용함. 키체인 쓰기 성공 후 메모리 상태를 바꾸는 기존 순서를 유지함.
- `Sources/Bridge.swift`: 번들 메인 프레임 검증을 통과한 ready 요청에서 준비 상태를 먼저 설정함. didFinish 이전에 도착한 초기 요청도 스냅샷과 응답을 받을 수 있음. 기존 프레임·문서 경로·액션 검증은 유지함.
- `tests/MacOSAuthBridgeTests.swift`, `scripts/test-macos-auth-bridge.sh`: 실제 GoogleClient·Bridge·Models·LocalProviders와 공용 Resources를 함께 컴파일·실행하는 macOS 회귀 시험을 추가함. 외부 인증·키체인·D-day 저장소·앱 셸만 메모리 대역으로 대체함. 시험 앱과 컴파일 캐시는 고유 임시 디렉터리에 만들고 종료 시 정리함.
- `README.md`: 새 검증 명령과 사용 범위를 안내함. 공용 JS 검증 명령도 전체 시험 파일을 실행하도록 맞춤.

## 검증 결과

| 항목 | 결과 | 근거 |
| --- | --- | --- |
| 수정 전 회귀 재현 | 7 통과 / 6 실패 | 갱신 토큰 누락 시 신규 인증 수락·기존 저장 상태 변경·활성 계정 변경을 확인함. 초기 ready 실패와 이에 의존하는 후속 요청·재로드 확인도 실패함 |
| 수정 후 새 회귀 시험 | 13/13 통과 | 실제 수정 소스와 실제 WKWebView 사용 |
| macOS 앱 빌드·서명 | 통과 | bash scripts/build.sh 종료 코드 0. 스크립트 내 ad-hoc 서명·strict 검증 통과 |
| 새 빌드 자체 시험 | 48/48 통과 | build/Orbit.app/Contents/MacOS/Orbit --self-test |
| 공용 JavaScript | 35/35 통과 | 기존 네 개 cjs 시험 파일 전체 실행 |
| 변경 내용·공백 검사 | 통과 | git diff 검토 및 git diff --check |

새 회귀 시험의 확인 사항:

1. 새 인증 응답에 갱신 토큰이 없으면 거부함.
2. 위 실패에서 기존 저장 계정을 보존함.
3. 위 실패 후 API 요청도 기존 활성 계정을 사용함.
4. 빈 갱신 토큰도 거부함.
5. 빈 토큰으로 실패했을 때 기존 저장 계정을 보존함.
6. 빈 토큰으로 실패한 후 기존 활성 계정을 보존함.
7. 유효한 신규 계정으로 연결 후 그 계정의 갱신 토큰을 사용함.
8. API 요청이 신규 계정의 갱신된 access token을 사용함.
9. 정상 갱신 응답에 refresh_token이 생략되어도 해당 계정의 토큰을 유지함.
10. 자격증명 저장 실패 시 기존 저장·활성 계정을 보존함.
11. 최초 ready 응답이 한 번 도착하고 대기 요청이 남지 않음.
12. navigation 완료 후 추가 ready 요청도 한 번 정리됨.
13. WebContent 종료 처리에 연결된 reload 경로에서 새 ready 응답이 한 번 도착하고 대기 요청이 남지 않음.

## 재실행 명령

```sh
bash scripts/build.sh
build/Orbit.app/Contents/MacOS/Orbit --self-test
node --test tests/*.cjs
bash scripts/test-macos-auth-bridge.sh
```

빌드의 첫 샌드박스 실행은 컴파일러 캐시 쓰기 권한으로 실패했으며 승인된 재실행으로 통과했음. 네이티브 회귀 시험도 WebKit 시스템 서비스 접근이 가능한 승인된 실행으로 수행했음. 시스템 설정·전역 도구 설정은 변경하지 않았음.

## 범위와 잔여 경계

- 로컬 빌드·합성 시험은 Apple Silicon Mac에서 수행함. 후속 설치본의 실제 화면 점검은 사용자가 dots에 Mac을 연결한 뒤 수행함.
- 인증 흐름의 Google 응답과 자격증명 저장은 합성 시험임. 실제 Google 로그인·계정 전환·Tasks 쓰기는 실행하지 않았음.
- 실제 번들 페이지의 초기 메시지는 생산 Bridge가 처리함. WebContent 복구는 해당 delegate를 호출하여 reload 경로를 검증했으며 시스템 프로세스를 강제로 종료하지 않았음.
- ready 대기 요청이 즉시 제거되는 것을 검사했으며 180초 실시간 대기 시험은 실행하지 않았음.
- 초기 구현 검증 단계의 산출물은 build/Orbit.app임. 이후 사용자가 설치본 교체·재실행을 승인하여 아래 후속 작업을 수행함. Git 커밋·외부 게시를 수행하지 않았음.
- Windows 소스와 공용 Resources는 변경하지 않았음. 전체 제품의 기존 Windows·실제 계정 연동 검증 경계는 유지함.

이번 승인 범위인 R01·R02의 로컬 수정·합성 인증·실제 WKWebView 검증 판정은 PASS임. 나머지 R03~R09는 원 검토 보고서의 후속 개선 대상으로 남음.

## 후속 설치 교체·재실행

사용자의 추가 요청에 따라 실행 중이던 `/Applications/Orbit.app`을 교체함. 사전 조사에서 `~/Applications/Orbit.app`도 존재했으나 실행 프로세스의 실제 번들 경로가 `/Applications/Orbit.app`이므로 그 경로를 대상으로 작업함.

- 기존 설치본 백업: `/Users/hongjuhyeong/Applications/.Orbit-backups/Orbit-before-20261026-9e1d78dee4.app`.
- 기존 설치본 실행 파일 SHA-256: `95a88bb1a410feab8d882c0e88a6ddf4c413710b1befc6c2c612b38fe4769cbd`.
- 교체 후 실행 파일 SHA-256: `8e44ce6c80baec50fec1c4562328dd4e1d2aa27b8b633b6dc51aa3b53aef1005`.
- 기존 Orbit 1개를 정상 종료한 뒤 스테이징한 검증 빌드로 교체하고 `/Applications/Orbit.app`을 재실행함.
- 실행 파일·Info.plist·Resources·서명 자료를 포함한 번들 파일 11개가 검증 빌드와 SHA-256 기준으로 일치함.
- 설치된 앱의 deep·strict 서명 검증 통과, 설치된 실행 파일 자체 시험 48/48 통과.
- 실행 상태 검사에서 올바른 설치 경로의 단일 프로세스, `finishedLaunching=true`, `terminated=false`를 확인함.
- 실제 화면 도구의 초기 timeoutReached 이후 사용자에게 dots의 Mac 연결 상태 확인을 요청했고, 사용자의 연결 완료 응답 뒤 실제 설치본에 접근함.

연결 후 실제 설치본 UI 점검 결과:

| 항목 | 결과 |
| --- | --- |
| 실제 번들 페이지 경로 | file:///Applications/Orbit.app/Contents/Resources/index.html 확인 |
| 오늘 화면 | 일정·할 일·중요 날짜·최근 파일·AI 영역 표시 확인 |
| 캘린더 | 탭 선택과 앞으로 7일 목록 표시 확인 |
| 최근 파일 | 탭 선택·검색 입력·최근 수정 파일 안내 표시 확인 |
| AI 작업 | 탭 선택과 전체·Codex·Claude Code 필터 표시 확인 |
| AI 소스 필터 | Codex 선택 시 Codex 카드 24개·Claude 0개, Claude 선택 시 반대 24개·0개 확인. 전체 필터로 복원 |
| 설정 | 열기·닫기, 테마 선택 컨트롤과 소스 연결 상태 표시 확인. 설정값은 변경하지 않음 |
| 기존 Google 연결 | 설정과 연결 상태에서 연결됨 표시 확인. 재로그인·쓰기 조작은 하지 않음 |
| Cmd+K | 최근 파일 탭으로 이동하고 검색 입력에 포커스가 놓이는 것 확인 |
| 초기 오류 표시 | 확인한 화면들에서 응답 지연 오류 토스트가 표시되지 않음 |
| 최종 상태 | 오늘 화면으로 복원 |

실제 화면 점검은 설치된 앱의 네이티브 접근성 상태와 UI 조작으로 수행함. 개인 일정·파일명·작업 제목은 검증 기록에 복제하지 않음. 초기 오류는 관찰한 시점의 미표시를 확인한 것이며 180초 고정 대기 시험으로 확대해 해석하지 않음.

이번 설치 교체·재실행·기본 UI 확인 요청의 판정은 PASS임. 앱 설정·D-day·Google 자격증명을 직접 변경하지 않았고 백업은 보존함. 원 검토의 R03~R09 및 실제 신규 Google 계정 전환·쓰기 검증은 별도 범위로 남음.
