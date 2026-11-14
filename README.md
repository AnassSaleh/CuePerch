# FloatPrompter

A floating teleprompter for Windows 10/11. It sits at the top-center of your screen, right under your webcam, and it's hidden from screen sharing and recordings.

## Install

Download **FloatPrompter.exe** from [Releases](../../releases/latest) and double-click it. There's nothing to install.

> The first time, Windows SmartScreen may say "Windows protected your PC". Click **More info**, then **Run anyway**. The app isn't code-signed, so this is normal.

## Use

1. Paste or type your script in the main window.
2. Press **Start**. The text scrolls up in the panel at the top of your screen.
3. Drag the panel to move it, drag its edges to resize it, and double-click it to snap it back to the top.

Turn on **Voice activation** if you want the text to move only while you're talking.

## Hotkeys (work in any app)

| Keys | Action |
|---|---|
| Ctrl + Alt + P | Play / pause |
| Ctrl + Alt + Up / Down | Faster / slower |
| Ctrl + Alt + Left / Right | Jump back / forward |
| Ctrl + Alt + R | Back to the start |
| Ctrl + Alt + H | Show / hide the prompter |
| Ctrl + Alt + V | Voice activation on / off |

Your script and settings are saved automatically in `%APPDATA%\FloatPrompter`.

## Build from source

```
powershell -ExecutionPolicy Bypass -File build.ps1
```

This uses the C# compiler that ships with Windows, so no SDK is needed. The output goes to `dist\FloatPrompter.exe`. Tests are in `tests\` (`run-tests.ps1` for the app, `run-voice-test.ps1` for voice detection).
