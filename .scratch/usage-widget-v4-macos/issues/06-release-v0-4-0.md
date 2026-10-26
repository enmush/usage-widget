# 06: Release v0.4.0 for Windows and macOS from a tag

**What to build:** Pushing a `v0.4.0` tag builds, tests, smoke-starts and packages all four zips with one checksum file and attaches them to a draft GitHub release with Korean notes. The README covers installing on macOS. The local Windows script still builds the Windows zips.

**Blocked by:** 03, 04, 05

**Status:** ready-for-agent

- [ ] Version 0.4.0 in the app and both packages
- [ ] Tag workflow produces the four zips and `SHA256SUMS.txt` on a draft release
- [ ] README explains the Mac download, the first open of an unsigned app and the Keychain prompt
- [ ] Korean release notes describe the current app only
- [ ] The local Windows build script still produces the two Windows zips
