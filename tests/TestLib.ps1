# Helpers for driving FloatPrompter in automated tests (UI Automation + synthetic input + screen capture).
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class TNative {
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, int data, UIntPtr extra);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string c, string t);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool GetWindowDisplayAffinity(IntPtr h, out uint a);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

$script:Exe = Join-Path $PSScriptRoot '..\dist\FloatPrompter.exe'

function Start-Prompter([string]$DataDir, [hashtable]$Settings) {
  New-Item -ItemType Directory -Force $DataDir | Out-Null
  if ($Settings) { ($Settings | ConvertTo-Json) | Set-Content -Encoding UTF8 (Join-Path $DataDir 'settings.json') }
  $psi = New-Object System.Diagnostics.ProcessStartInfo $script:Exe
  $psi.UseShellExecute = $false
  $psi.EnvironmentVariables['FLOATPROMPTER_DATA_DIR'] = $DataDir
  if ($env:FP_FAKE_MIC) { $psi.EnvironmentVariables['FLOATPROMPTER_FAKE_MIC'] = $env:FP_FAKE_MIC }
  $p = [System.Diagnostics.Process]::Start($psi)
  for ($i = 0; $i -lt 50; $i++) { Start-Sleep -Milliseconds 200; if ((Get-Hwnd 'FloatPrompter Prompter') -ne [IntPtr]::Zero -and (Get-Hwnd 'FloatPrompter') -ne [IntPtr]::Zero) { break } }
  Start-Sleep -Milliseconds 800
  return $p
}

function Stop-Prompter($p) {
  $h = Get-Hwnd 'FloatPrompter'
  if ($h -ne [IntPtr]::Zero) { (Get-El $h).GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close() }
  if ($p) { if (-not $p.WaitForExit(5000)) { $p.Kill() } }
}

function Get-Hwnd([string]$title) { [TNative]::FindWindow([NullString]::Value, $title) }
function Get-El([IntPtr]$h) { [System.Windows.Automation.AutomationElement]::FromHandle($h) }
function Get-Rect([IntPtr]$h) { $r = New-Object TNative+RECT; [void][TNative]::GetWindowRect($h, [ref]$r); $r }

function Get-State {
  $h = Get-Hwnd 'FloatPrompter Prompter'
  $t = (Get-El $h).Current.HelpText
  $o = @{}
  foreach ($kv in $t.Split(';')) { $a = $kv.Split('='); $o[$a[0]] = [double]::Parse($a[1], [Globalization.CultureInfo]::InvariantCulture) }
  return $o
}

function Find-ByName([string]$name, $patternAvailableProperty) {
  $A = [System.Windows.Automation.AutomationElement]
  $root = Get-El (Get-Hwnd 'FloatPrompter')
  $c = New-Object System.Windows.Automation.PropertyCondition($A::NameProperty, $name)
  if ($patternAvailableProperty) {
    $c2 = New-Object System.Windows.Automation.PropertyCondition($patternAvailableProperty, $true)
    $c = New-Object System.Windows.Automation.AndCondition($c, $c2)
  }
  $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
}
function Find-ById([string]$id) {
  $root = Get-El (Get-Hwnd 'FloatPrompter')
  $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
  $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
}
$script:AE = [System.Windows.Automation.AutomationElement]
function Invoke-Named([string]$name) { (Find-ByName $name $script:AE::IsInvokePatternAvailableProperty).GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
function Toggle-Named([string]$name) { (Find-ByName $name $script:AE::IsTogglePatternAvailableProperty).GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle() }
function Get-ToggleState([string]$name) { (Find-ByName $name $script:AE::IsTogglePatternAvailableProperty).GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState }
function Set-RangeValue([string]$name, [double]$v) { (Find-ByName $name $script:AE::IsRangeValuePatternAvailableProperty).GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).SetValue($v) }
function Get-RangeValue([string]$name) { (Find-ByName $name $script:AE::IsRangeValuePatternAvailableProperty).GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).Current.Value }
function Select-Named([string]$name) { (Find-ByName $name $script:AE::IsSelectionItemPatternAvailableProperty).GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() }
function Get-Selected([string]$name) { (Find-ByName $name $script:AE::IsSelectionItemPatternAvailableProperty).GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected }
function Get-ScriptBox { Find-ByName 'Script' $script:AE::IsValuePatternAvailableProperty }
function Get-StatusText { (Find-ById 'StatusText').Current.Name }

# Ctrl+Alt+<vk>
function Send-Hotkey([byte]$vk) {
  $KEYUP = 2; $EXT = 1
  $ext = 0; if ($vk -ge 0x21 -and $vk -le 0x28) { $ext = $EXT }
  [TNative]::keybd_event(0x11, 0, 0, [UIntPtr]::Zero)
  [TNative]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)
  [TNative]::keybd_event($vk, 0, $ext, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 40
  [TNative]::keybd_event($vk, 0, $ext -bor $KEYUP, [UIntPtr]::Zero)
  [TNative]::keybd_event(0x12, 0, $KEYUP, [UIntPtr]::Zero)
  [TNative]::keybd_event(0x11, 0, $KEYUP, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 250
}

function Save-Screen([string]$path, $rect) {
  if (-not $rect) { $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds; $x=$b.X; $y=$b.Y; $w=$b.Width; $h=$b.Height }
  else { $x=$rect.Left; $y=$rect.Top; $w=$rect.Right-$rect.Left; $h=$rect.Bottom-$rect.Top }
  $bmp = New-Object System.Drawing.Bitmap $w, $h
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($x, $y, 0, 0, (New-Object System.Drawing.Size $w, $h))
  $g.Dispose(); $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
}

# Average brightness (0-255) of a screen region, via GDI capture
function Get-RegionBrightness($rect) {
  $w=$rect.Right-$rect.Left; $h=$rect.Bottom-$rect.Top
  $bmp = New-Object System.Drawing.Bitmap $w, $h
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size $w, $h)); $g.Dispose()
  $sum = 0; $n = 0
  for ($yy = 0; $yy -lt $h; $yy += 4) { for ($xx = 0; $xx -lt $w; $xx += 4) { $c = $bmp.GetPixel($xx, $yy); $sum += ($c.R + $c.G + $c.B) / 3; $n++ } }
  $bmp.Dispose(); return $sum / $n
}

function Mouse-Drag($x1, $y1, $x2, $y2) {
  [void][TNative]::SetCursorPos($x1, $y1); Start-Sleep -Milliseconds 150
  [TNative]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 150
  $steps = 15
  for ($i = 1; $i -le $steps; $i++) { [void][TNative]::SetCursorPos([int]($x1 + ($x2-$x1)*$i/$steps), [int]($y1 + ($y2-$y1)*$i/$steps)); Start-Sleep -Milliseconds 25 }
  Start-Sleep -Milliseconds 150
  [TNative]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 400
}

function Mouse-Click($x, $y, [int]$count = 1) {
  [void][TNative]::SetCursorPos($x, $y); Start-Sleep -Milliseconds 100
  for ($i = 0; $i -lt $count; $i++) { [TNative]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); [TNative]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 60 }
  Start-Sleep -Milliseconds 300
}

function Mouse-Wheel($x, $y, [int]$delta) {
  [void][TNative]::SetCursorPos($x, $y); Start-Sleep -Milliseconds 100
  [TNative]::mouse_event(0x800, 0, 0, $delta, [UIntPtr]::Zero); Start-Sleep -Milliseconds 300
}

$script:Failures = 0
function Check([bool]$cond, [string]$what) {
  if ($cond) { Write-Host "PASS  $what" } else { Write-Host "FAIL  $what" -ForegroundColor Red; $script:Failures++ }
}

