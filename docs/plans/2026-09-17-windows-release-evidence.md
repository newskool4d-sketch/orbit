# Orbit Windows 로컬 배포 증거

검증일: 2026-09-17
프로젝트: `orbit-codex-windows-port`
후보: ZIP 내부 `BUILD-INFO.json`으로 기준 커밋·작업트리 상태·소스 지문 식별
분류: self-contained Windows ZIP
주장: 로컬 시험판 설치·실행·재배포 가능
판정: **PASS**

공개 서명 릴리스 판정은 **PARTIAL**이다. 실행 파일은 코드 서명되지 않았고 공개 업로드도 수행하지 않았다.

## 증거표

| 계층 | 상태 | 증거 | 남은 위험 |
| --- | --- | --- | --- |
| 후보 식별 | PASS | ZIP 내부 `BUILD-INFO.json`: 기준 커밋, 작업트리 상태, 소스 매니페스트 SHA-256 | 없음 |
| 작업트리 | PASS | D-day·글자 크기·배율·Resume·패키징 변경을 현재 후보 범위로 확인 | macOS 실행 미검증 |
| 시험 | PASS | JavaScript 35/35, C# 저장소·정책 36/36, 네이티브 64/64, SQLite 읽기 전용, WebView2, 재시작 복원 | 실제 Google 미검증 |
| 빌드 | PASS | `Windows/scripts/package.ps1`: Release 빌드 경고 0·오류 0 | 없음 |
| 설치본 | PASS | `%LOCALAPPDATA%/Programs/Orbit` 교체, 후보 DLL 해시 일치, 시작 메뉴 바로가기·사용자 설정 불변 | 이전 설치본 롤백 폴더 유지 |
| 배율 화면 | PASS(장치 범위) | 실제 125·150·175%에서 각 25개 캡처와 화면 확인. 100·125·150·200% 상당 × 보통·아주 크게 × 5개 화면 40조합 통과 | 장치에서 100·200% 실제 적용 불가, 다중 모니터 미검증 |
| 실제 키보드 | PASS | 사용자가 Tab·Shift+Tab·Esc·Ctrl K와 D-day 조작의 정상 작동을 실기기에서 확인 | 없음 |
| 물리 절전 복귀 | PASS | Modern Standby Kernel-Power 506→507, 동일 PID·시작 시각, 표시 창, 사용자 데이터 해시 불변 | 장시간 절전은 별도 |
| 시각 완성도 | PASS | Moss·Pearl·Midnight 글래스 표면과 Segoe UI Variable 글꼴 적용, 설치본 세 테마 캡처 확인 | 실제 사용자 선호 평가는 별도 |
| 패키지 | PASS | `Orbit.Windows-<빌드시각>.zip` self-contained win-x64 | 코드 서명 없음 |
| 패키지 해시 | PASS | 생성된 ZIP의 SHA-256과 나란히 생성한 `.sha256` 일치 | 없음 |
| 클린 압축 해제 | PASS | checkout 밖 새 임시 폴더에 압축 해제, 필수 문서·리소스 확인 | 새 Windows 사용자 계정 시험은 미실행 |
| 압축 해제본 실행 | PASS | 자체 시험 및 실제 WebView2 UI smoke, 캡처 25개 | 없음 |
| 설치본·ZIP 동등성 | PASS | `Orbit.Windows.dll` SHA-256 일치 | 없음 |
| 개인정보 경계 | PASS | ZIP에 `settings.json`, `ddays.json`, DB, SQLite, 로그 0개 | 바이너리 정적 분석은 별도 |
| 서명 | PASS(상태 확인) | Authenticode `NotSigned`, 시험판 README에 명시 | 외부 배포 시 평판 경고 가능 |
| 공개 배포 | NOT_RUN | 업로드·GitHub Release·외부 게시 미실행 | 사용자가 내려받는 공개 자산 없음 |

## 산출물

- `Windows/artifacts/Orbit.Windows-<빌드시각>.zip`
- `Windows/artifacts/Orbit.Windows-<빌드시각>.zip.sha256`

설치본 교체 전 폴더와 글래스 스타일 교체 전 CSS는 `%LOCALAPPDATA%` 아래 롤백 경로에 보존했다. 이전 ZIP은 삭제하지 않았고 공개 업로드는 수행하지 않았다.
