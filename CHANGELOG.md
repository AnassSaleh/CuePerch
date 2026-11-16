# Changelog

All notable changes to CuePerch (called FloatPrompter before v1.1.0) are listed here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [1.1.0] - 2026-11-17

### Changed
- Renamed from FloatPrompter to CuePerch. The exe is now `CuePerch.exe` and the repository is `AnassSaleh/CuePerch` (old links redirect).
- Script and settings now live in `%APPDATA%\CuePerch`. On first run, CuePerch copies them from `%APPDATA%\FloatPrompter` so nothing is lost.

### Removed
- The GitHub Pages website. The README on GitHub is now the project's home page.

## [1.0.1] - 2026-11-14

### Fixed
- The prompter now re-applies "hide from screen sharing" once it is on screen, not only before its first frame. On some Windows 10 PCs the early setting didn't stick and screen shares could show the prompter.

### Added
- A warning under "Hide from screen sharing" when Windows doesn't actually hide the prompter on your PC, with the Windows build and what to do instead.
- The log (`%APPDATA%\FloatPrompter\log.txt`) records what Windows reported for screen-capture hiding, to help with bug reports.

### Added
- README with demo GIF, screenshots and hotkey reference.
- MIT license, contributing guide, changelog and issue templates.

## [1.0.0] - 2026-11-14

### Added
- Floating prompter panel that docks at the top-center of the screen, right under the webcam.
- Hidden from screen sharing and recordings (Zoom, Teams, Meet, Discord, OBS) on Windows 10 2004 and later.
- Voice activation: the script scrolls only while you're talking.
- Global hotkeys for play/pause, speed, jump, restart, show/hide and voice.
- `[bracketed]` cues shown dimmed.
- Adjustable speed, text size, font, alignment, dark/light look and multi-monitor placement.
- Script and settings saved automatically to `%APPDATA%\FloatPrompter`.

[Unreleased]: https://github.com/AnassSaleh/CuePerch/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/AnassSaleh/CuePerch/compare/v1.0.1...v1.1.0
[1.0.1]: https://github.com/AnassSaleh/CuePerch/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/AnassSaleh/CuePerch/releases/tag/v1.0.0
