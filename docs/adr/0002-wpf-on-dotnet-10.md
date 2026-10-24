# Build the widget with WPF on .NET 10

The widget is a single small WPF window on .NET 10 (LTS), not a Tauri/web app like Win-CodexBar. It only needs one borderless, always-on-top, draggable window and a tray icon, which WPF does natively with a small memory footprint and one self-contained executable, while Tauri adds a Rust plus Node build chain and a WebView runtime for a few rows of text. The installed .NET 7 SDK is out of support, so .NET 10 is installed for this project.
