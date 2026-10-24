# Build the widget with WPF on .NET 10

The widget is a single small WPF window on .NET 10 (LTS), not a Tauri/web app like Win-CodexBar. It only needs one borderless, always-on-top, draggable window and a tray icon, which WPF does natively with a small memory footprint and one self-contained executable, while Tauri adds a Rust plus Node build chain and a WebView runtime for a few rows of text. The installed .NET 7 SDK is out of support, so .NET 10 is installed for this project.

**Amended in v0.2.0:** the window is no longer always on top and the tray icon is gone. It now behaves like Sticky Notes: a normal borderless window with a taskbar button and its own minimize button, because the user found a floating window and a tray-only hide control awkward. WPF still covers this natively (the borderless window only needs the native minimize style bit so the taskbar button can minimize it), so the choice of WPF stands.
