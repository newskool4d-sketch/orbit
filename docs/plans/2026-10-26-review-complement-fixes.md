# 보완 검토 M1~M6 수정 — 2026-10-26

범위: 독립 교차 검증 보완 의견의 medium 6건(M1~M6). 기준 커밋 `ab8a7be`, 변경은 작업트리에만 있음(커밋·설치본 교체·배포 없음).

## 변경

| ID | 플랫폼 | 내용 |
| --- | --- | --- |
| M1 | Windows | 최근 파일 확장자를 macOS·Windows 설계서 §8과 같게 맞춤. `.html .gdoc .gsheet .gslides` 추가, `.png .jpg .jpeg` 제외 |
| M2 | Windows | Tasks를 macOS와 같은 순서(미완료 우선, 기한 오름차순, 기한 없음은 뒤)로 정렬 |
| M3 | macOS·Windows | 토큰 갱신 400 응답의 `error`가 `invalid_grant`일 때만 재연결 안내를 표시하고, 재연결·연결 해제·OAuth 설정 교체 전까지 갱신 요청을 보내지 않음. 응답 본문은 표시하지 않음. 다른 토큰 오류는 기존처럼 재시도. Windows는 이 상태의 할 일 변경을 '반영 여부 확인 중'으로 잠그지 않음 |
| M4 | macOS | Tasks를 `showHidden=true`로 조회하고 hidden 완료 항목을 유지함(Windows 설계 §11과 일치). deleted 항목 제외는 유지 |
| M5 | macOS·Windows | Vault가 Drive 폴더 안에 있으면 Drive 스캔에서 Vault 하위 트리를 건너뜀. macOS의 기존 경로 중복 제거는 안전장치로 유지 |
| M6 | macOS·Windows | Claude 프로젝트 폴더를 수정 시각 최신순으로 정렬한 뒤 200개 상한을 적용하고, 초과 시 '일부 범위' 상태를 표시함(공용 UI의 기존 부분 검색 안내 사용) |
| 추가 | macOS | 파일 스캔에서 심볼릭 링크 파일에 `skipDescendants()`를 호출해 다음 형제 폴더 전체가 누락되던 기존 결함을 수정. 탐색기는 링크를 따라가지 않음을 확인함 |

공용 Resources(app.js 등)는 변경하지 않음.

## 검증

| 검사 | 결과 | 범위 |
| --- | --- | --- |
| macOS 빌드·서명 | 통과 | `bash scripts/build.sh` |
| Swift 자체 시험 | 60/60 통과 | 기존 54개 + hidden 완료 항목, 링크가 형제 폴더를 가리지 않음, Vault 제외, Vault 한 번만 표시, 프로젝트 상한 정렬, Claude 부분 범위 표시 |
| 공용 JavaScript | 43/43 통과 | `node --test tests/*.cjs` |
| macOS 인증·브리지 회귀 | 61/61 통과 | 기존 56개 + invalid_grant 재연결 안내·재시도 중단·계정 유지·재연결 복구·다른 오류 재시도 |
| Windows 빌드·자체 시험 | **미실행** | 이 Mac에 dotnet·pwsh 없음. SelfTests.cs에 M1·M2·M3·M5·M6 검사를 추가했으나 컴파일·실행하지 않음 |

Windows 검증은 [Windows 작업 인계](2026-10-26-windows-handoff.md)의 순서(build.ps1 → test.ps1 -NativeUi)를 따르며, 이 변경의 추가 검사 이름은 `file-extensions-match-macos-and-design`, `drive-scan-skips-vault-inside-drive`, `vault-files-listed-once-as-obsidian`, `claude-project-cap-keeps-newest-folder-and-reports-partial`, `tasks-open-first-then-due-date`, `invalid-grant-asks-to-reconnect`, `invalid-grant-stops-repeated-refresh`, `other-token-errors-keep-retrying`임.

## 남은 경계

- 실제 Google 계정의 refresh token 만료·hidden 할 일은 합성 응답으로만 검증함.
- macOS 설치본(`/Applications/Orbit.app`)은 교체하지 않음.
- Vault 제외는 경로 문자열 비교(macOS: symlink 해석·NFC, Windows: 전체 경로·대소문자 무시)이며 서로 다른 별칭 경로는 다루지 않음. macOS는 폴더 이름이 같은 경우에만 symlink를 해석하므로, 이름이 다른 링크로 연결된 Vault는 건너뛰지 않고 기존 경로 중복 제거가 중복 표시를 막음.
- R01·R02 독립 검토의 low 2건(새 계정 연결 시 이전 일정 캐시 유지, 갱신 중 신규 연결의 토큰 혼합 가능성)은 이번 범위에 포함하지 않음.
