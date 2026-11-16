$ErrorActionPreference = 'Stop'
$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$out = Join-Path $env:TEMP 'fp-voicetest'
New-Item -ItemType Directory -Force $out | Out-Null
Copy-Item (Join-Path $PSScriptRoot '..\dist\CuePerch.exe') $out -Force
& "$fw\csc.exe" /nologo /target:exe "/out:$out\VoiceGateTest.exe" "/r:$out\CuePerch.exe" "/r:$fw\WPF\WindowsBase.dll" "/r:$fw\WPF\PresentationCore.dll" "/r:$fw\WPF\PresentationFramework.dll" (Join-Path $PSScriptRoot 'VoiceGateTest.cs')
if ($LASTEXITCODE -ne 0) { throw 'build failed' }
$env:CUEPERCH_DATA_DIR = $out
& "$out\VoiceGateTest.exe"
exit $LASTEXITCODE
