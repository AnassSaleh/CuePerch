# Contributing to FloatPrompter

Thanks for helping out. Bug reports, ideas and pull requests are all welcome.

## Reporting a bug or asking for a feature

Open an [issue](https://github.com/AnassSaleh/FloatPrompter/issues/new/choose) and pick a template. For bugs, include your Windows version and the steps that cause the problem.

## Building

FloatPrompter builds with the C# compiler that ships with Windows, so you don't need Visual Studio or the .NET SDK:

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

The exe lands in `dist\FloatPrompter.exe`.

## Code notes

- The compiler is the .NET Framework 4.x `csc.exe`, which only supports **C# 5**. Don't use string interpolation (`$"..."`), `?.`, `nameof` or expression-bodied members (`=>`).
- The XAML files in `src\` are embedded as resources and loaded at runtime with `XamlReader`.
- Keep the app a single dependency-free exe.

## Testing

```powershell
powershell -ExecutionPolicy Bypass -File tests\run-tests.ps1
powershell -ExecutionPolicy Bypass -File tests\run-voice-test.ps1
```

`run-tests.ps1` drives the real app through UI Automation and moves the mouse, so don't touch the PC while it runs (about 5 minutes). Close any running FloatPrompter first.

## Pull requests

1. Fork the repo and create a branch.
2. Make your change, build, and run the tests.
3. Add a line under **Unreleased** in `CHANGELOG.md`.
4. Open a pull request describing what changed and why.
