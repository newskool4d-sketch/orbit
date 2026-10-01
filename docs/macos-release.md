# macOS DMG 시험판 릴리스

Apple Silicon Mac과 macOS 14.0 이상에서 `bash scripts/package-macos.sh`를 실행합니다. 기존 `scripts/build.sh`로 빌드하고 Mac 자체 시험·공용 웹 시험, DMG 무결성, 읽기 전용 마운트본의 서명·버전·arm64·최소 OS·자체 시험을 확인합니다. 소스 작업 트리가 깨끗해야 합니다.

산출물은 `build/releases/Orbit.macOS-<UTC시각>.dmg`, `.dmg.sha256`, `.verification.json`입니다. 설정·D-day·인증 자료·개인 로그를 포함하지 않습니다. Info.plist의 앱 버전과 빌드 번호, 실제 소스 커밋은 앱 안 BUILD-INFO.json과 검증 JSON으로 확인합니다.

GitHub Actions의 **macOS DMG preview**는 수동 실행만 지원합니다. 공개 저장소의 표준 `macos-14` ARM64 러너를 사용하며 유료 larger runner, 캐시, Actions artifact 저장소, 외부 서명 인증서나 사용자 비밀을 사용하지 않습니다. 기존 저장소의 릴리스 업로드에 필요한 `contents: write`만 해당 job에 지정합니다.

사용하지 않은 `macos-YYYY.MM.DD` 태그를 입력해 실행합니다. 시험과 패키지 검증이 모두 통과하면 GitHub 초안 prerelease에 DMG·체크섬·검증 JSON을 업로드합니다. 초안 자산의 해시·버전·소스 커밋을 확인한 뒤 공개하고 다시 다운로드해 SHA-256을 검증합니다. 기존 태그나 릴리스 자산은 덮어쓰지 않습니다.

앱은 기존 방식의 ad-hoc 서명이며 Apple 공증이 없습니다. 패키징은 GUI 화면 또는 실제 Google 계정 시험을 수행하지 않으며 사용자 Mac에 설치하지 않습니다. CLI 자체 시험의 실제 macOS 실행과 GUI 검증 여부를 구분해 보고합니다.
