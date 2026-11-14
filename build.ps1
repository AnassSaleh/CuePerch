# Builds dist\FloatPrompter.exe using the C# compiler that ships with Windows (.NET Framework 4.x).
# No SDK or downloads needed:  powershell -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$fw   = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path "$fw\csc.exe")) { $fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319' }
$wpf  = Join-Path $fw 'WPF'
$out  = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $out | Out-Null

$sources = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }
$cscArgs = @(
  '/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/utf8output',
  "/out:$out\FloatPrompter.exe",
  "/win32icon:$root\assets\icon.ico",
  "/win32manifest:$root\src\app.manifest",
  "/r:$wpf\PresentationCore.dll", "/r:$wpf\PresentationFramework.dll", "/r:$wpf\WindowsBase.dll",
  "/r:$fw\System.Xaml.dll", '/r:System.dll', '/r:System.Core.dll', '/r:System.Xml.dll',
  '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll', '/r:System.Runtime.Serialization.dll',
  "/resource:$root\src\ControlWindow.xaml,FloatPrompter.ControlWindow.xaml",
  "/resource:$root\src\Prompter.xaml,FloatPrompter.Prompter.xaml"
) + $sources

& "$fw\csc.exe" @cscArgs
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
Get-Item "$out\FloatPrompter.exe" | Select-Object FullName, Length
