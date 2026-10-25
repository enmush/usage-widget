# Build the widget with WPF on .NET 10

The widget is a single small WPF window on .NET 10 (LTS), not a Tauri or web app. It behaves like Sticky Notes: a borderless, draggable window that is not always on top, with a taskbar button and its own minimize button. WPF does this natively with a small memory footprint and one self-contained executable (the borderless window only needs the native minimize style bit so the taskbar button can minimize it), while Tauri adds a Rust plus Node build chain and a WebView runtime for a few rows of text.
