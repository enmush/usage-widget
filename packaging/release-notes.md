Claude, Codex, Antigravity(Gemini) 구독 계정의 사용량과 Gemini API 호출 수를 한눈에 보여주는 데스크톱 위젯입니다. Windows와 macOS에서 같은 화면으로 실행됩니다.

**macOS**

- Apple Silicon(`macos-arm64`)과 Intel(`macos-x64`)용 `Usage Widget.app`을 제공합니다.
- Claude Code가 키체인에 저장한 로그인을 계정(Login folder)별로 읽고, 만료되면 갱신해서 같은 키체인 항목에 저장합니다. 저장할 수 없는 경우에는 갱신하지 않습니다.
- Finder에서 열어도 agy와 gcloud를 찾습니다. 셸 설정의 PATH와 `GEMINI_API_KEY`도 읽습니다.
- `⌃⌥⇧U`로 위젯을 띄우거나 숨기고, Dock 아이콘으로 다시 엽니다. 우클릭 메뉴의 **Start at login**으로 로그인할 때 자동 실행합니다.
- 설정 파일은 `~/Library/Application Support/UsageWidget/settings.json`입니다.

**다운로드**

- `UsageWidget-0.4.0-macos-arm64.zip`: Apple Silicon Mac (macOS 15 이상)
- `UsageWidget-0.4.0-macos-x64.zip`: Intel Mac (macOS 15 이상)
- `UsageWidget-0.4.0-win-x64-standalone.zip`: Windows, 그대로 실행됩니다.
- `UsageWidget-0.4.0-win-x64.zip`: Windows, [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)이 필요합니다.
- `SHA256SUMS.txt`: 체크섬입니다.

**처음 열 때:** 서명되지 않은 앱입니다. Mac에서는 **시스템 설정 → 개인정보 보호 및 보안 → 그래도 열기**를 누릅니다. Claude 로그인을 읽을 때 키체인 접근 창이 뜨면 **항상 허용**을 누릅니다. Windows에서 SmartScreen 경고가 뜨면 **추가 정보 → 실행**을 누릅니다. 자세한 내용은 [README](https://github.com/enmush/usage-widget#readme)를 보세요.

**검증:** 단위 테스트 155개가 Windows와 macOS에서 통과했습니다. GitHub Actions에서 Windows 두 패키지와 Mac 두 패키지(x64는 Rosetta)를 실제로 실행해 창이 뜨고, Mac에서 `⌃⌥⇧U`로 숨기고 다시 띄우고, 정상 종료되는 것을 확인했습니다. Mac 키체인 읽기·쓰기는 macOS 러너의 실제 키체인으로 확인했습니다.
