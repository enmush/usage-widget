# Build the widget with Avalonia on .NET 10

The widget is one small Avalonia window on .NET 10 (LTS) for both Windows and macOS, with one UI code base. It behaves like Sticky Notes: a borderless, draggable window that is not always on top, with its own minimize button and a taskbar button (Windows) or Dock icon (macOS) that brings it back. Avalonia draws the same window on both platforms and exposes the native hooks the widget needs (window styles and messages on Windows), while each platform's own UI toolkit would mean two UIs to keep in step, and Tauri or Electron would add a web runtime for a few rows of text.

## Consequences

- Platform differences (global shortcut, start at login, minimizing) live behind one small platform interface with a Windows and a macOS implementation.
- A borderless window on macOS cannot be minimized to the Dock, so minimizing hides it and clicking the Dock icon shows it again.
