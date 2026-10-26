// Prints the on-screen windows of one process and fails when it has none. Usage: swift macos-window.swift <pid>
import CoreGraphics
import Foundation

let pid = Int32(CommandLine.arguments[1])!
let windows = (CGWindowListCopyWindowInfo([.optionOnScreenOnly], kCGNullWindowID) as? [[String: Any]] ?? [])
    .filter { ($0[kCGWindowOwnerPID as String] as? Int32) == pid }
for window in windows {
    print(window[kCGWindowBounds as String] ?? "")
}
exit(windows.isEmpty ? 1 : 0)
