<div align="center">

<img src="assets/icon.png" width="110" alt="CuePerch teleprompter for Windows logo" />

# CuePerch: free teleprompter for Windows

**A free, open-source teleprompter app for Windows 10 and 11 that stays hidden from screen sharing. An alternative to Moody Teleprompter.**

CuePerch sits at the top of your screen, right under the webcam, so you can read your notes and still look at the camera. On Windows 10 version 2004 or later, Zoom, Teams, Google Meet, OBS and screen recordings don't pick it up.

[![Download](https://img.shields.io/github/v/release/AnassSaleh/CuePerch?label=Download&style=for-the-badge&color=2563eb&cacheSeconds=3600)](https://github.com/AnassSaleh/CuePerch/releases/latest)
[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?style=for-the-badge&logo=windows&logoColor=white)](#install)
[![License: MIT](https://img.shields.io/badge/License-MIT-22c55e?style=for-the-badge)](LICENSE)

![C#](https://img.shields.io/badge/C%23-WPF-512BD4?logo=dotnet&logoColor=white)
![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.x-512BD4)
![Size](https://img.shields.io/badge/exe-~115%20KB-orange)
![No install](https://img.shields.io/badge/install-none%20needed-brightgreen)
![Downloads](https://img.shields.io/github/downloads/AnassSaleh/CuePerch/total?color=blue&cacheSeconds=3600)
![Stars](https://img.shields.io/github/stars/AnassSaleh/CuePerch?style=social&cacheSeconds=3600)

<p>
  <a href="https://github.com/AnassSaleh/CuePerch/releases/latest"><b>Download</b></a> ·
  <a href="#what-it-does">Features</a> ·
  <a href="#hotkeys">Hotkeys</a> ·
  <a href="#faq">FAQ</a> ·
  <a href="#build-from-source">Build</a> ·
  <a href="CONTRIBUTING.md">Contribute</a>
</p>

<img src="assets/demo-v3.gif" width="900" alt="CuePerch teleprompter demo: the panel slides down under the webcam, scrolls a script, stops when the speaker pauses, and stays invisible to the call while the screen is shared" />

<sub>The panel and script text are captured from the real app.</sub>

</div>

---

## What it does

CuePerch is a teleprompter for video calls, presentations and recording videos on a Windows PC.

- **Reads under your webcam.** The panel docks at the top center of your screen, so your eyes stay close to the camera and you keep eye contact while you read.
- **Hidden from screen sharing.** On Windows 10 version 2004 or later, people watching on Zoom, Microsoft Teams, Google Meet, Discord or OBS see neither the prompter nor the window with your script in it.
- **Voice-activated scrolling.** With voice activation on, the text moves only while you talk and waits when you stop.
- **Global hotkeys.** Change the speed, pause or jump around from whatever app you're in.
- **Tiny and private.** One `.exe` of about 115 KB. No installer, no account, it never goes online and it doesn't track you.

If Windows won't hide the panel on your PC, CuePerch shows a warning, and sharing a single app window instead of your whole screen keeps the prompter out of the share. Nothing on screen can hide from a phone camera pointed at it, or from a capture card recording your display output.

For voice activation, CuePerch only measures how loud your microphone is, on your PC. The audio is never recorded, saved or sent anywhere.

## Screenshot

<div align="center">
<img src="assets/screenshot-app.png" width="800" alt="CuePerch teleprompter main window with the script editor, scroll speed, text size, voice activation and screen-share settings" />

<sub>The main window, where you paste your script and set the speed, text size, font, look, voice activation and where the prompter sits.</sub>
</div>

## Install

Download **CuePerch.exe** from the [latest release](https://github.com/AnassSaleh/CuePerch/releases/latest) and double-click it. Nothing else needs installing.

> [!NOTE]
> The first time you run it, Windows SmartScreen may show "Windows protected your PC". Click **More info**, then **Run anyway**. You see this because the app isn't code-signed.

## Use

1. Paste or type your script in the main window, or load one with **Open file...**.
2. Press **Start**, and the text scrolls up in the panel at the top of your screen.
3. Drag the panel to move it and drag its edges to resize it. Double-click it to snap it back under the webcam.

Anything in `[square brackets]`, like `[pause]` or `[smile]`, shows up dimmed, so you can leave yourself cues.

## Hotkeys

These work from any app.

| Keys | Action |
|---|---|
| <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>P</kbd> | Play / pause |
| <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>↑</kbd> / <kbd>↓</kbd> | Faster / slower |
| <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>←</kbd> / <kbd>→</kbd> | Jump back / forward |
| <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>R</kbd> | Back to the start |
| <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>H</kbd> | Show / hide the prompter |
| <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>V</kbd> | Voice activation on / off |

Your script and settings save automatically to `%APPDATA%\CuePerch`.

## At a glance

| | CuePerch |
|---|---|
| Price | Free |
| Source code | Open, MIT license |
| Hidden from screen sharing | Yes, on Windows 10 version 2004 or later |
| Voice-activated scrolling | Yes, included |
| Works offline, no account | Yes |
| Size | One ~115 KB `.exe`, no installer |

## FAQ

**Is there a free teleprompter for Windows?**
Yes. CuePerch is free and open source under the MIT license, with no trial, watermark or account.

**Can people on Zoom or Teams see my teleprompter?**
No, on Windows 10 version 2004 or later. CuePerch asks Windows to leave both of its windows out of screen sharing and recordings, so Zoom, Teams, Google Meet, Discord and OBS don't capture them. If Windows refuses on your PC, the app tells you.

**How do I keep eye contact on a video call while reading notes?**
Put your notes right under the webcam. CuePerch docks there automatically, so your eyes stay near the lens while you read.

**Is CuePerch a Moody Teleprompter alternative for Windows?**
It covers the same idea: a notes panel under the camera that's hidden from screen sharing, with voice-activated scrolling. It's free and runs on Windows.

**Does it work for recording YouTube videos or presentations?**
Yes. It works with any app, including OBS, PowerPoint, Loom and the Windows Camera app.

**Was this called FloatPrompter?**
Yes. CuePerch was named FloatPrompter up to v1.0.1. Your saved script and settings carry over automatically.

## Build from source

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

It uses the C# compiler that comes with Windows, so you don't need an SDK. The exe ends up in `dist\CuePerch.exe`.

Tests live in `tests\`: `run-tests.ps1` drives the real app through UI Automation, and `run-voice-test.ps1` checks voice detection.

## Changelog

Changes for each version are in [CHANGELOG.md](CHANGELOG.md).

## Contributing

Bug reports, ideas and pull requests are all welcome. [Open an issue](https://github.com/AnassSaleh/CuePerch/issues/new/choose), or read [CONTRIBUTING.md](CONTRIBUTING.md) if you want to send code.

If CuePerch is useful to you, starring the repo helps other people find it.

## License

[MIT](LICENSE), so you can use it, change it and share it.

<sub>CuePerch is an independent project and is not affiliated with or endorsed by Moody or any other teleprompter vendor. Product names are used only for comparison.</sub>

<sub>Keywords: teleprompter for Windows, teleprompter app for Windows 10 and 11, free teleprompter, open source teleprompter, Moody Teleprompter alternative, Moody Teleprompter for Windows alternative, autocue for PC, video call teleprompter, Zoom teleprompter, Microsoft Teams teleprompter, Google Meet teleprompter, OBS teleprompter, invisible teleprompter, teleprompter hidden from screen share, webcam teleprompter, eye contact teleprompter, voice activated teleprompter, script reader for presentations.</sub>
