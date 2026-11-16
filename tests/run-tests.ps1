# End-to-end tests for CuePerch. Run: powershell -ExecutionPolicy Bypass -File tests\run-tests.ps1
# Drives the real app with UI Automation, synthetic keyboard/mouse input and GDI screen capture.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'TestLib.ps1')
Get-Process CuePerch -ErrorAction SilentlyContinue | Stop-Process -Force

$data = Join-Path $env:TEMP 'fp-e2e'
Remove-Item -Recurse -Force $data -ErrorAction SilentlyContinue
$script = (1..40 | ForEach-Object { "Line number $_ of the test script, with enough words to wrap around. [cue $_]" }) -join "`r`n"
$p = Start-Prompter $data @{ Script = $script; HideFromCapture = $true }
$hp = Get-Hwnd 'CuePerch Prompter'
$he = Get-Hwnd 'CuePerch'
$scr = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds

Write-Host "`n== Window placement =="
$r = Get-Rect $hp
Check ($r.Top -eq $scr.Top) "prompter is at the top edge (top=$($r.Top))"
Check ([math]::Abs((($r.Left + $r.Right) / 2) - ($scr.Left + $scr.Width / 2)) -le 1) "prompter is horizontally centered (left=$($r.Left) right=$($r.Right))"
$ex = [TNative]::GetWindowLong($hp, -20)
Check (($ex -band 0x8) -ne 0) "prompter is always on top (WS_EX_TOPMOST)"
Check (($ex -band 0x08000000) -ne 0) "prompter never steals focus (WS_EX_NOACTIVATE)"
Check (($ex -band 0x80) -ne 0) "prompter is hidden from Alt+Tab (WS_EX_TOOLWINDOW)"

Write-Host "`n== Hidden from screen capture =="
$a = 0; [void][TNative]::GetWindowDisplayAffinity($hp, [ref]$a)
Check ($a -eq 0x11) "prompter has WDA_EXCLUDEFROMCAPTURE (affinity=$a)"
$a = 0; [void][TNative]::GetWindowDisplayAffinity($he, [ref]$a)
Check ($a -eq 0x11) "editor has WDA_EXCLUDEFROMCAPTURE (affinity=$a)"
# Compare a capture of the prompter area with the prompter shown vs hidden: excluded => identical.
$mid = (Get-Rect $hp)
function Diff-Captures($rect) {
  $f1 = "$env:TEMP\fp-cap-shown.png"; $f2 = "$env:TEMP\fp-cap-hidden.png"
  Save-Screen $f1 $rect
  Send-Hotkey 0x48; Start-Sleep -Milliseconds 400      # Ctrl+Alt+H hide
  Save-Screen $f2 $rect
  Send-Hotkey 0x48; Start-Sleep -Milliseconds 400      # show again
  $b1 = [System.Drawing.Bitmap]::FromFile($f1); $b2 = [System.Drawing.Bitmap]::FromFile($f2)
  $d = 0; $n = 0
  for ($y = 0; $y -lt $b1.Height; $y += 3) { for ($x = 0; $x -lt $b1.Width; $x += 3) { $c1 = $b1.GetPixel($x,$y); $c2 = $b2.GetPixel($x,$y); $d += [math]::Abs($c1.R-$c2.R) + [math]::Abs($c1.G-$c2.G) + [math]::Abs($c1.B-$c2.B); $n++ } }
  $b1.Dispose(); $b2.Dispose()
  return $d / $n
}
$diffHidden = Diff-Captures $mid
Check ($diffHidden -lt 40) "screen capture does not contain the prompter (pixel diff shown vs hidden = $([math]::Round($diffHidden,2)))"
Toggle-Named 'Hide from screen sharing'; Start-Sleep -Milliseconds 400
$a = 0; [void][TNative]::GetWindowDisplayAffinity($hp, [ref]$a)
Check ($a -eq 0) "turning the option off makes it capturable (affinity=$a)"
$diffShown = Diff-Captures $mid
Check ($diffShown -gt 100) "with the option off the prompter appears in captures (pixel diff = $([math]::Round($diffShown,2)))"
Toggle-Named 'Hide from screen sharing'; Start-Sleep -Milliseconds 300
$a = 0; [void][TNative]::GetWindowDisplayAffinity($hp, [ref]$a)
Check ($a -eq 0x11) "turning it back on hides it again (affinity=$a)"

Write-Host "`n== Play / pause and smooth scrolling =="
$s0 = Get-State
Check ($s0.playing -eq 0 -and $s0.offset -eq 0) "starts paused at the top"
Invoke-Named 'Start'; Start-Sleep -Milliseconds 600
$s1 = Get-State; Start-Sleep -Seconds 3; $s2 = Get-State
Check ($s1.playing -eq 1) "Start button starts scrolling"
$rate = ($s2.offset - $s1.offset) / 3.0
$expected = 6 * 0.06 * 28 * 1.35
Check ([math]::Abs($rate - $expected) / $expected -lt 0.15) "scroll rate matches speed 6 ($([math]::Round($rate,1)) px/s, expected $([math]::Round($expected,1)))"
Check ((Get-StatusText) -like 'Playing*') "status shows Playing ($(Get-StatusText))"
Invoke-Named 'Pause'; Start-Sleep -Milliseconds 300
$a1 = (Get-State).offset; Start-Sleep -Seconds 1; $a2 = (Get-State).offset
Check ((Get-State).playing -eq 0 -and $a1 -eq $a2) "Pause button stops scrolling"

Write-Host "`n== Hotkeys =="
Send-Hotkey 0x50   # Ctrl+Alt+P
Check ((Get-State).playing -eq 1) "Ctrl+Alt+P plays"
Send-Hotkey 0x50
Check ((Get-State).playing -eq 0) "Ctrl+Alt+P pauses"
Send-Hotkey 0x26   # Up
Check ((Get-State).speed -eq 7) "Ctrl+Alt+Up speeds up (speed=$((Get-State).speed))"
Check ((Get-RangeValue 'Scroll speed') -eq 7) "speed slider follows the hotkey"
Send-Hotkey 0x28; Send-Hotkey 0x28
Check ((Get-State).speed -eq 5) "Ctrl+Alt+Down slows down (speed=$((Get-State).speed))"
$o = (Get-State).offset
Send-Hotkey 0x27   # Right
$o2 = (Get-State).offset
Check ([math]::Abs(($o2 - $o) - 2 * 28 * 1.35) -lt 0.5) "Ctrl+Alt+Right jumps forward 2 lines ($([math]::Round($o2-$o,1)) px)"
Send-Hotkey 0x25   # Left
Check ([math]::Abs((Get-State).offset - $o) -lt 0.5) "Ctrl+Alt+Left jumps back"
Send-Hotkey 0x50; Start-Sleep -Milliseconds 500
Send-Hotkey 0x52   # R
$s = Get-State
Check ($s.offset -eq 0 -and $s.playing -eq 0) "Ctrl+Alt+R goes back to the start and pauses"
Send-Hotkey 0x48   # H
Check (-not [TNative]::IsWindowVisible($hp)) "Ctrl+Alt+H hides the prompter"
Check ((Get-ToggleState 'Show prompter') -eq 'Off') "Show prompter switch follows"
Send-Hotkey 0x48
Check ([TNative]::IsWindowVisible($hp)) "Ctrl+Alt+H shows it again"
Send-Hotkey 0x56   # V
Check ((Get-State).voice -eq 1) "Ctrl+Alt+V turns voice activation on"
Check ((Get-ToggleState 'Voice activation') -eq 'On') "voice switch follows"
Send-Hotkey 0x56
Check ((Get-State).voice -eq 0) "Ctrl+Alt+V turns it off"

Write-Host "`n== Settings controls =="
Set-RangeValue 'Scroll speed' 12; Start-Sleep -Milliseconds 200
Check ((Get-State).speed -eq 12) "speed slider sets speed"
$max1 = (Get-State).max
Set-RangeValue 'Text size' 40; Start-Sleep -Milliseconds 500
$max2 = (Get-State).max
Check ($max2 -gt $max1 * 1.3) "bigger text size makes the script taller ($max1 -> $max2)"
Set-RangeValue 'Width' 800; Start-Sleep -Milliseconds 600
$r = Get-Rect $hp
Check (($r.Right - $r.Left) -eq 800) "width slider resizes the prompter (width=$($r.Right-$r.Left))"
Check ([math]::Abs((($r.Left + $r.Right) / 2) - ($scr.Left + $scr.Width / 2)) -le 1) "and it stays centered"
Set-RangeValue 'Height' 260; Start-Sleep -Milliseconds 600
$r = Get-Rect $hp
Check (($r.Bottom - $r.Top) -eq 260) "height slider resizes the prompter (height=$($r.Bottom-$r.Top))"
Set-RangeValue 'Text size' 28; Start-Sleep -Milliseconds 300

Write-Host "`n== Mouse: wheel, drag, snap, resize =="
$r = Get-Rect $hp
$cx = [int](($r.Left + $r.Right) / 2); $cy = [int](($r.Top + $r.Bottom) / 2) - 30
$o = (Get-State).offset
Mouse-Wheel $cx $cy -120
Check ((Get-State).offset -gt $o + 30) "mouse wheel scrolls the script ($o -> $((Get-State).offset))"
Mouse-Drag $cx $cy ($cx - 500) ($cy + 300)
$r2 = Get-Rect $hp
Check ([math]::Abs(($r2.Left - $r.Left) + 500) -le 3 -and [math]::Abs(($r2.Top - $r.Top) - 300) -le 3) "drag moves the prompter (now at $($r2.Left),$($r2.Top))"
$foreground = [TNative]::GetForegroundWindow()
Check ($foreground -ne $hp) "dragging did not take focus away from the active app"
Mouse-Click ([int](($r2.Left + $r2.Right) / 2)) ([int]($r2.Top + 30)) 2
$r3 = Get-Rect $hp
Check ($r3.Top -eq 0 -and [math]::Abs((($r3.Left + $r3.Right) / 2) - ($scr.Width / 2)) -le 1) "double-click snaps back to top-center ($($r3.Left),$($r3.Top))"
# drag a bit away then back close to the top center -> snaps
$cx = [int](($r3.Left + $r3.Right) / 2); $cy = [int]($r3.Top + 40)
Mouse-Drag $cx $cy ($cx + 200) ($cy + 200)
$r4 = Get-Rect $hp
Mouse-Drag ($cx + 200) ($cy + 200) ($cx + 15) ($cy + 10)
$r5 = Get-Rect $hp
Check ($r5.Top -eq 0 -and [math]::Abs((($r5.Left + $r5.Right) / 2) - ($scr.Width / 2)) -le 1) "dropping near the top-center snaps into place"
# resize from the right edge
$w0 = $r5.Right - $r5.Left
Mouse-Drag ($r5.Right - 3) ([int](($r5.Top + $r5.Bottom) / 2)) ($r5.Right + 97) ([int](($r5.Top + $r5.Bottom) / 2))
$r6 = Get-Rect $hp
$w1 = $r6.Right - $r6.Left
Check ($w1 -ge $w0 + 90) "dragging the right edge resizes ($w0 -> $w1)"
Check ([TNative]::GetForegroundWindow() -ne $hp) "resizing did not take focus away from the active app"
Check ([math]::Abs((($r6.Left + $r6.Right) / 2) - ($scr.Width / 2)) -le 1) "and it re-centers at the top"
Check ([math]::Abs((Get-RangeValue 'Width') - $w1) -le 10) "width slider follows the resize ($(Get-RangeValue 'Width'))"
# bottom edge
$h0 = $r6.Bottom - $r6.Top
Mouse-Drag ([int](($r6.Left + $r6.Right) / 2) - 150) ($r6.Bottom - 3) ([int](($r6.Left + $r6.Right) / 2) - 150) ($r6.Bottom + 57)
$r7 = Get-Rect $hp
Check ((($r7.Bottom - $r7.Top) - $h0) -ge 50) "dragging the bottom edge resizes ($h0 -> $($r7.Bottom - $r7.Top))"
# move to custom spot for the persistence test
$cx = [int](($r7.Left + $r7.Right) / 2); $cy = [int]($r7.Top + 40)
Mouse-Drag $cx $cy ($cx - 300) ($cy + 400)
$custom = Get-Rect $hp

Write-Host "`n== Pause on hover =="
[void][TNative]::SetCursorPos(5, $scr.Height - 200)
Toggle-Named 'Pause on hover'
Send-Hotkey 0x50; Start-Sleep -Milliseconds 800
$a1 = (Get-State).offset
[void][TNative]::SetCursorPos([int](($custom.Left + $custom.Right) / 2), [int]($custom.Top + 30)); Start-Sleep -Milliseconds 900
$a2 = (Get-State).offset; Start-Sleep -Milliseconds 1000; $a3 = (Get-State).offset
Check ($a2 -gt $a1 -and [math]::Abs($a3 - $a2) -lt 1.0) "scrolling stops while the mouse is over the prompter ($a2 -> $a3)"
[void][TNative]::SetCursorPos(5, $scr.Height - 200); Start-Sleep -Milliseconds 1200
Check ((Get-State).offset -gt $a3 + 5) "and resumes when the mouse leaves"
Send-Hotkey 0x50
Toggle-Named 'Pause on hover'

Write-Host "`n== Script editing =="
$box = Get-ScriptBox
$box.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue("Short script.")
Start-Sleep -Milliseconds 500
Check ((Get-State).max -lt 5) "typing a new script updates the prompter (max=$((Get-State).max))"
Set-RangeValue 'Scroll speed' 20
$box.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue(((1..6 | % { "Line $_" }) -join "`r`n"))
Start-Sleep -Milliseconds 500
Send-Hotkey 0x52; Send-Hotkey 0x50
Start-Sleep -Seconds 6
$s = Get-State
Check ($s.playing -eq 0 -and $s.offset -eq $s.max) "stops by itself at the end of the script"
Check ((Get-StatusText) -like 'Finished*') "status shows Finished ($(Get-StatusText))"
Set-Clipboard -Value "Pasted from the clipboard. [smile]"
Invoke-Named 'Paste'; Start-Sleep -Milliseconds 400
Check ($box.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value -eq "Pasted from the clipboard. [smile]") "Paste button replaces the script with the clipboard"
Invoke-Named 'Clear'; Start-Sleep -Milliseconds 400
Check ($box.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value -eq "") "Clear button empties the script"
$box.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($script)
Set-RangeValue 'Scroll speed' 9
Start-Sleep -Milliseconds 300

Write-Host "`n== Voice activation (live microphone) =="
Toggle-Named 'Voice activation'; Start-Sleep -Seconds 2
$vs = (Find-ById 'VoiceStatus').Current.Name
Write-Host "      mic status: $vs"
Check ($vs -ne '' -and $vs -notlike 'Starting*') "microphone opened and reports a status"
Send-Hotkey 0x52; Send-Hotkey 0x50
Set-RangeValue 'Mic sensitivity' 100; Start-Sleep -Seconds 2
$s = Get-State
Write-Host "      real mic at max sensitivity: speaking=$($s.speaking) level=$($s.level) offset=$($s.offset)"
Send-Hotkey 0x50
Set-RangeValue 'Mic sensitivity' 62
Toggle-Named 'Voice activation'

Write-Host "`n== Voice activation (simulated voice) =="
Stop-Prompter $p
$fake = Join-Path $data 'fake-mic.txt'
'0' | Set-Content $fake
$env:FP_FAKE_MIC = $fake
$p = Start-Prompter $data $null
$hp = Get-Hwnd 'CuePerch Prompter'
Toggle-Named 'Voice activation'; Start-Sleep -Milliseconds 800
Send-Hotkey 0x52; Send-Hotkey 0x50; Start-Sleep -Seconds 1.5
Check ((Get-State).offset -lt 0.5) "silence: text holds still"
Check ((Get-StatusText) -like 'Waiting for your voice*') "status says it's waiting for your voice"
'0.1' | Set-Content $fake; Start-Sleep -Seconds 2
$s1 = Get-State
Check ($s1.speaking -eq 1 -and $s1.offset -gt 5) "talking: text scrolls (offset=$($s1.offset))"
Check ((Get-StatusText) -like 'Playing*') "status shows Playing while talking"
'0' | Set-Content $fake; Start-Sleep -Seconds 1.5
$s2 = Get-State; Start-Sleep -Seconds 1; $s3 = Get-State
Check ($s2.speaking -eq 0 -and [math]::Abs($s3.offset - $s2.offset) -lt 0.5) "stopped talking: text stops ($($s2.offset) -> $($s3.offset))"
'0.1' | Set-Content $fake; Start-Sleep -Seconds 1.5
Check ((Get-State).offset -gt $s3.offset + 3) "talking again: text resumes"
'0' | Set-Content $fake
Remove-Item Env:FP_FAKE_MIC
Send-Hotkey 0x50
Set-RangeValue 'Mic sensitivity' 62
Toggle-Named 'Voice activation'

Write-Host "`n== Single instance =="
$psi = New-Object System.Diagnostics.ProcessStartInfo $script:Exe
$psi.UseShellExecute = $false; $psi.EnvironmentVariables['CUEPERCH_DATA_DIR'] = $data
$p2 = [System.Diagnostics.Process]::Start($psi)
Check ($p2.WaitForExit(5000)) "a second copy exits and focuses the running one"
Check ((Get-Process CuePerch).Count -eq 1) "only one CuePerch is running"

Write-Host "`n== Settings are remembered =="
Set-RangeValue 'Scroll speed' 11
Set-RangeValue 'Text size' 33
Select-Named 'Light'
Start-Sleep -Milliseconds 300
Stop-Prompter $p
Check (Test-Path (Join-Path $data 'settings.json')) "settings.json was written"
$p = Start-Prompter $data $null
$hp = Get-Hwnd 'CuePerch Prompter'
$r = Get-Rect $hp
Check ((Get-State).speed -eq 11) "speed remembered"
Check ((Get-RangeValue 'Text size') -eq 33) "text size remembered"
Check (Get-Selected 'Light') "light theme remembered"
Check ($r.Left -eq $custom.Left -and $r.Top -eq $custom.Top -and $r.Right -eq $custom.Right) "position and size remembered ($($r.Left),$($r.Top) $($r.Right-$r.Left)x$($r.Bottom-$r.Top))"
Invoke-Named 'Snap to top'; Start-Sleep -Milliseconds 400
$r = Get-Rect $hp
Check ($r.Top -eq 0 -and [math]::Abs((($r.Left + $r.Right) / 2) - ($scr.Width / 2)) -le 1) "Snap to top button re-centers it"
Stop-Prompter $p

$log = Join-Path $data 'log.txt'
if (Test-Path $log) { Write-Host "`nApp log:"; Get-Content $log }
Write-Host ""
if ($script:Failures -eq 0) { Write-Host "ALL TESTS PASSED" -ForegroundColor Green } else { Write-Host "$script:Failures TEST(S) FAILED" -ForegroundColor Red }
exit $script:Failures
