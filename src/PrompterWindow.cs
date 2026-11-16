using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace CuePerch
{
    /// <summary>The floating, always-on-top prompter panel.</summary>
    public class PrompterWindow : Window
    {
        private readonly AppModel model;
        private Settings S { get { return model.S; } }

        private readonly Border root, progressTrack, progressFill, controls;
        private readonly Grid viewport;
        private readonly Canvas textHost;
        private readonly TextBlock scriptText, placeholder, speedLabel;
        private readonly Ellipse micDot;
        private readonly Button btnPlay;
        private readonly TranslateTransform shift = new TranslateTransform();

        private IntPtr hwnd;
        private double offset;          // how far the script has scrolled (DIPs)
        private double maxOffset;
        private double padTop = 20;
        private double gain;            // 0..1 eased multiplier (voice / hover start-stop smoothing)
        private double lastFrame = -1;
        private bool rendering;
        private bool hovering;
        private Native.RECT sizeMoveStart;
        private readonly DispatcherTimer textTimer, uiTimer;

        private static readonly Regex Cue = new Regex(@"\[[^\]\r\n]{1,80}\]", RegexOptions.Compiled);
        private const double PadX = 24;
        private const double LineFactor = 1.35;
        private const double LinesPerSecondPerSpeed = 0.06;

        public PrompterWindow(AppModel m)
        {
            model = m;
            Title = "CuePerch Prompter";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = -30000; Top = -30000;
            Width = S.Width; Height = S.Height;
            MinWidth = Settings.MinWidth; MinHeight = Settings.MinHeight;

            root = (Border)App.LoadXaml("Prompter.xaml");
            Content = root;
            viewport = (Grid)root.FindName("Viewport");
            textHost = (Canvas)root.FindName("TextHost");
            scriptText = (TextBlock)root.FindName("ScriptText");
            placeholder = (TextBlock)root.FindName("Placeholder");
            progressTrack = (Border)root.FindName("ProgressTrack");
            progressFill = (Border)root.FindName("ProgressFill");
            controls = (Border)root.FindName("Controls");
            micDot = (Ellipse)root.FindName("MicDot");
            speedLabel = (TextBlock)root.FindName("SpeedLabel");
            btnPlay = (Button)root.FindName("BtnPlay");

            scriptText.RenderTransform = shift;
            Canvas.SetLeft(scriptText, PadX);

            ((Button)root.FindName("BtnRestart")).Click += delegate { model.Restart(); };
            ((Button)root.FindName("BtnSlower")).Click += delegate { model.ChangeSpeed(-1); };
            ((Button)root.FindName("BtnFaster")).Click += delegate { model.ChangeSpeed(+1); };
            ((Button)root.FindName("BtnEdit")).Click += delegate { model.RequestEditor(); };
            btnPlay.Click += delegate { model.TogglePlay(); };

            root.MouseLeftButtonDown += OnMouseDown;
            root.MouseMove += OnDragMove;
            root.MouseLeftButtonUp += delegate { EndDrag(); };
            root.LostMouseCapture += delegate { EndDrag(); };
            root.MouseWheel += OnWheel;
            root.MouseEnter += delegate { hovering = true; ShowControls(true); };
            root.MouseLeave += delegate { hovering = false; ShowControls(false); };
            viewport.SizeChanged += delegate { Relayout(); };
            scriptText.SizeChanged += delegate { UpdateMaxOffset(); ApplyOffset(); };

            textTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            textTimer.Tick += delegate { textTimer.Stop(); RebuildText(); };

            uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            uiTimer.Tick += delegate { UpdateStatus(); };
            uiTimer.Start();

            SourceInitialized += OnSourceInitialized;
            Loaded += delegate { Place(); PlaceAgainSoon(); };
            ContentRendered += delegate { ApplyCapture(); };
            IsVisibleChanged += delegate { if (IsVisible) Dispatcher.BeginInvoke(new Action(ApplyCapture), DispatcherPriority.Background); };

            model.Changed += OnModelChanged;
            model.RestartRequested += delegate { offset = 0; gain = 0; ApplyOffset(); };
            model.NudgeRequested += delegate (double lines) { ScrollBy(lines * LineHeight); };

            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += delegate
            {
                Dispatcher.BeginInvoke(new Action(delegate { Place(); PlaceAgainSoon(); }));
            };

            ApplyTextStyle();
            ApplyTheme();
            RebuildText();
            UpdateSpeedLabel();
            UpdatePlayButton();
        }

        private double LineHeight { get { return S.FontSize * LineFactor; } }

        // ---------- window plumbing ----------

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            hwnd = new WindowInteropHelper(this).Handle;
            int ex = Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);
            Native.SetWindowLong(hwnd, Native.GWL_EXSTYLE, ex | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE);
            HwndSource.FromHwnd(hwnd).AddHook(WndProc);
            ApplyCapture();
            Place();
        }

        /// <summary>
        /// Ask Windows to keep the prompter out of screen captures and tell the editor what Windows actually did.
        /// Called again once the window is on screen: a layered (transparent) window can lose or reject the
        /// setting if it is only applied before its first frame.
        /// </summary>
        private void ApplyCapture()
        {
            if (hwnd == IntPtr.Zero) return;
            uint a = Native.ApplyCaptureExclusion(hwnd, S.HideFromCapture, "prompter");
            if (a != model.PrompterAffinity)
            {
                model.PrompterAffinity = a;
                model.Notify("CaptureStatus");
            }
        }

        private IntPtr WndProc(IntPtr h, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case Native.WM_MOUSEACTIVATE:
                    handled = true;   // never steal focus from the app you're recording/presenting in
                    return new IntPtr(Native.MA_NOACTIVATE);
            }
            return IntPtr.Zero;
        }

        private static Forms.Screen FindScreen(string deviceName)
        {
            foreach (var s in Forms.Screen.AllScreens)
                if (s.DeviceName == deviceName) return s;
            return Forms.Screen.PrimaryScreen;
        }

        /// <summary>Moves the window to its saved spot: top-center of the chosen display, or a custom position.</summary>
        public void Place()
        {
            if (hwnd == IntPtr.Zero) return;
            var scr = FindScreen(S.Monitor);
            var b = scr.Bounds;
            double scale = Native.DpiScaleAt(b.Left + b.Width / 2, b.Top + b.Height / 2);
            int w = (int)Math.Round(S.Width * scale), h = (int)Math.Round(S.Height * scale);
            w = Math.Min(w, b.Width); h = Math.Min(h, b.Height);
            int x, y;
            if (S.CustomPosition)
            {
                x = Math.Max(b.Left, Math.Min(b.Right - w, b.Left + S.CustomX));
                y = Math.Max(b.Top, Math.Min(b.Bottom - h, b.Top + S.CustomY));
            }
            else
            {
                x = b.Left + (b.Width - w) / 2;
                y = b.Top;
            }
            Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, x, y, w, h, Native.SWP_NOACTIVATE);
            root.CornerRadius = y <= b.Top ? new CornerRadius(0, 0, 18, 18) : new CornerRadius(18);
        }

        /// <summary>Second pass after Windows applies a DPI change when crossing monitors.</summary>
        private void PlaceAgainSoon()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(Place));
        }

        private void OnMoveOrResizeFinished()
        {
            if (hwnd == IntPtr.Zero) return;
            Native.RECT r;
            Native.GetWindowRect(hwnd, out r);
            var scr = Forms.Screen.FromHandle(hwnd);
            var b = scr.Bounds;
            double scale = Native.DpiScaleAt(r.Left + r.Width / 2, r.Top + r.Height / 2);
            bool resized = r.Width != sizeMoveStart.Width || r.Height != sizeMoveStart.Height;
            bool moved = r.Left != sizeMoveStart.Left || r.Top != sizeMoveStart.Top;
            if (!resized && !moved) return;

            S.Monitor = scr.DeviceName;
            if (resized)
            {
                S.Width = Math.Round(Math.Max(Settings.MinWidth, r.Width / scale));
                S.Height = Math.Round(Math.Max(Settings.MinHeight, r.Height / scale));
            }
            if (!resized || S.CustomPosition)
            {
                int cx = (r.Left + r.Right) / 2, bcx = b.Left + b.Width / 2;
                bool snapTop = Math.Abs(r.Top - b.Top) <= 24 * scale && Math.Abs(cx - bcx) <= 60 * scale;
                if (snapTop) S.CustomPosition = false;
                else
                {
                    S.CustomPosition = true;
                    S.CustomX = r.Left - b.Left;
                    S.CustomY = r.Top - b.Top;
                }
            }
            if (resized) model.Notify("Size");
            model.Notify("Position");
        }

        // Edge flags for resizing
        private const int EdgeLeft = 1, EdgeRight = 2, EdgeBottom = 4;
        private const double EdgeSize = 8;
        private bool dragging;
        private int resizeEdge;
        private Native.POINT dragStartCursor;

        private int EdgeAt(Point p)
        {
            int e = 0;
            if (p.X < EdgeSize) e |= EdgeLeft;
            else if (p.X > root.ActualWidth - EdgeSize) e |= EdgeRight;
            if (p.Y > root.ActualHeight - EdgeSize) e |= EdgeBottom;
            return e;
        }

        private static Cursor CursorFor(int edge)
        {
            switch (edge)
            {
                case EdgeLeft: case EdgeRight: return Cursors.SizeWE;
                case EdgeBottom: return Cursors.SizeNS;
                case EdgeLeft | EdgeBottom: return Cursors.SizeNESW;
                case EdgeRight | EdgeBottom: return Cursors.SizeNWSE;
                default: return null;
            }
        }

        // Moving and resizing are done by hand (not DragMove / system sizing) so the
        // prompter never activates and never steals keyboard focus from Zoom, OBS, etc.
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (hwnd == IntPtr.Zero) return;
            int edge = EdgeAt(e.GetPosition(root));
            if (e.ClickCount == 2 && edge == 0)
            {
                SnapToTop();
                e.Handled = true;
                return;
            }
            Native.GetCursorPos(out dragStartCursor);
            Native.GetWindowRect(hwnd, out sizeMoveStart);
            resizeEdge = edge;
            dragging = root.CaptureMouse();
            e.Handled = true;
        }

        private void OnDragMove(object sender, MouseEventArgs e)
        {
            if (!dragging)
            {
                root.Cursor = CursorFor(EdgeAt(e.GetPosition(root)));
                return;
            }
            Native.POINT pt;
            Native.GetCursorPos(out pt);
            int dx = pt.X - dragStartCursor.X, dy = pt.Y - dragStartCursor.Y;
            var r0 = sizeMoveStart;
            if (resizeEdge == 0)
            {
                Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, r0.Left + dx, r0.Top + dy, 0, 0, Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
                return;
            }
            double scale = Native.DpiScaleAt(r0.Left + r0.Width / 2, r0.Top + r0.Height / 2);
            int minW = (int)(Settings.MinWidth * scale), minH = (int)(Settings.MinHeight * scale);
            int left = r0.Left, right = r0.Right, bottom = r0.Bottom;
            bool symmetric = !S.CustomPosition;   // top-centered: grow both sides so it stays centered
            if ((resizeEdge & EdgeRight) != 0)
            {
                int w = Math.Max(minW, r0.Width + (symmetric ? 2 * dx : dx));
                if (symmetric) { left = r0.Left + (r0.Width - w) / 2; right = left + w; } else right = left + w;
            }
            if ((resizeEdge & EdgeLeft) != 0)
            {
                int w = Math.Max(minW, r0.Width - (symmetric ? 2 * dx : dx));
                if (symmetric) { left = r0.Left + (r0.Width - w) / 2; right = left + w; } else left = right - w;
            }
            if ((resizeEdge & EdgeBottom) != 0) bottom = Math.Max(r0.Top + minH, r0.Bottom + dy);
            Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, left, r0.Top, right - left, bottom - r0.Top, Native.SWP_NOACTIVATE);
        }

        private void EndDrag()
        {
            if (!dragging) return;
            dragging = false;
            if (root.IsMouseCaptured) root.ReleaseMouseCapture();
            OnMoveOrResizeFinished();
        }

        public void SnapToTop()
        {
            if (hwnd != IntPtr.Zero) S.Monitor = Forms.Screen.FromHandle(hwnd).DeviceName;
            S.CustomPosition = false;
            model.Notify("Position");
        }

        private void OnWheel(object sender, MouseWheelEventArgs e)
        {
            ScrollBy(-e.Delta / 120.0 * LineHeight);
            e.Handled = true;
        }

        private void ShowControls(bool show)
        {
            controls.IsHitTestVisible = show;
            var anim = new DoubleAnimation(show ? 1 : 0, TimeSpan.FromMilliseconds(show ? 120 : 220));
            controls.BeginAnimation(OpacityProperty, anim);
        }

        // ---------- model ----------

        private void OnModelChanged(string what)
        {
            switch (what)
            {
                case "Script": textTimer.Stop(); textTimer.Start(); break;
                case "Text": ApplyTextStyle(); break;
                case "Theme": ApplyTheme(); break;
                case "Speed": UpdateSpeedLabel(); break;
                case "Size":
                    Width = S.Width; Height = S.Height;
                    Place(); PlaceAgainSoon();
                    break;
                case "Position": Place(); PlaceAgainSoon(); break;
                case "Capture": ApplyCapture(); break;
                case "PrompterVisible":
                    if (model.PrompterVisible) { Show(); Place(); } else Hide();
                    UpdateRendering();
                    break;
                case "IsPlaying":
                    if (model.IsPlaying && offset >= maxOffset - 0.5) { offset = 0; ApplyOffset(); }
                    if (model.IsPlaying) gain = S.VoiceActivation ? 0 : 0.35;
                    UpdatePlayButton();
                    UpdateRendering();
                    break;
            }
        }

        private void UpdatePlayButton()
        {
            btnPlay.Content = model.IsPlaying ? "" : "";
        }

        private void UpdateSpeedLabel()
        {
            speedLabel.Text = "Speed " + S.Speed.ToString("0", CultureInfo.InvariantCulture);
        }

        private void ApplyTextStyle()
        {
            scriptText.FontSize = S.FontSize;
            try { scriptText.FontFamily = new FontFamily(S.FontFamily); } catch (Exception) { scriptText.FontFamily = new FontFamily("Segoe UI"); }
            scriptText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            scriptText.LineHeight = LineHeight;
            scriptText.TextAlignment = S.AlignLeft ? TextAlignment.Left : TextAlignment.Center;
            Relayout();
        }

        private Brush cueBrush;

        private void ApplyTheme()
        {
            byte a = (byte)Math.Round(255 * S.BackgroundOpacity);
            if (S.LightTheme)
            {
                root.Background = new SolidColorBrush(Color.FromArgb(a, 255, 255, 255));
                scriptText.Foreground = new SolidColorBrush(Color.FromRgb(0x11, 0x18, 0x27));
                cueBrush = new SolidColorBrush(Color.FromArgb(0x99, 0x11, 0x18, 0x27));
                placeholder.Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0, 0, 0));
                progressTrack.Background = new SolidColorBrush(Color.FromArgb(0x22, 0, 0, 0));
                progressFill.Background = new SolidColorBrush(Color.FromArgb(0x88, 0, 0, 0));
            }
            else
            {
                root.Background = new SolidColorBrush(Color.FromArgb(a, 0, 0, 0));
                scriptText.Foreground = Brushes.White;
                cueBrush = new SolidColorBrush(Color.FromArgb(0x8C, 0xFF, 0xFF, 0xFF));
                placeholder.Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF));
                progressTrack.Background = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));
                progressFill.Background = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF));
            }
            RebuildText();
        }

        private void RebuildText()
        {
            string s = S.Script ?? "";
            scriptText.Inlines.Clear();
            int last = 0;
            foreach (Match mt in Cue.Matches(s))
            {
                if (mt.Index > last) scriptText.Inlines.Add(new Run(s.Substring(last, mt.Index - last)));
                scriptText.Inlines.Add(new Run(mt.Value) { FontStyle = FontStyles.Italic, Foreground = cueBrush });
                last = mt.Index + mt.Length;
            }
            if (last < s.Length) scriptText.Inlines.Add(new Run(s.Substring(last)));
            placeholder.Visibility = string.IsNullOrWhiteSpace(s) ? Visibility.Visible : Visibility.Collapsed;
            UpdateMaxOffset();
            ApplyOffset();
        }

        // ---------- scrolling ----------

        private void Relayout()
        {
            double w = viewport.ActualWidth, h = viewport.ActualHeight;
            if (w <= 0 || h <= 0) return;
            scriptText.Width = Math.Max(40, w - 2 * PadX);
            double fadeTop = Math.Max(8, Math.Min(34, h * 0.14));
            double fadeBottom = Math.Max(14, Math.Min(64, h * 0.26));
            padTop = fadeTop + 4;
            // Absolute mapping: the fade must follow the visible area, not the (much taller) text inside it.
            var mask = new LinearGradientBrush { MappingMode = BrushMappingMode.Absolute, StartPoint = new Point(0, 0), EndPoint = new Point(0, h) };
            mask.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0));
            mask.GradientStops.Add(new GradientStop(Color.FromArgb(255, 0, 0, 0), fadeTop / h));
            mask.GradientStops.Add(new GradientStop(Color.FromArgb(255, 0, 0, 0), Math.Max(fadeTop / h, 1 - fadeBottom / h)));
            mask.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 1));
            textHost.OpacityMask = mask;
            UpdateMaxOffset();
            ApplyOffset();
        }

        private void UpdateMaxOffset()
        {
            maxOffset = Math.Max(0, scriptText.ActualHeight - LineHeight);
            if (offset > maxOffset) offset = maxOffset;
        }

        private void ScrollBy(double dips)
        {
            offset = Math.Max(0, Math.Min(maxOffset, offset + dips));
            ApplyOffset();
        }

        private void ApplyOffset()
        {
            shift.Y = padTop - offset;
            double p = maxOffset > 0 ? offset / maxOffset : 0;
            model.Progress = p;
            double th = progressTrack.ActualHeight;
            progressFill.Height = Math.Max(0, th * p);
        }

        private void UpdateRendering()
        {
            bool need = model.IsPlaying && model.PrompterVisible;
            if (need && !rendering)
            {
                lastFrame = -1;
                CompositionTarget.Rendering += OnFrame;
                rendering = true;
            }
            else if (!need && rendering)
            {
                CompositionTarget.Rendering -= OnFrame;
                rendering = false;
            }
        }

        private void OnFrame(object sender, EventArgs e)
        {
            var re = e as RenderingEventArgs;
            double t = re != null ? re.RenderingTime.TotalSeconds : model.Mic.Now;
            if (t == lastFrame) return;          // Rendering can fire twice per frame
            double dt = lastFrame < 0 ? 0 : Math.Min(0.1, t - lastFrame);
            lastFrame = t;

            double target = 1;
            if (S.VoiceActivation && !model.Mic.IsSpeaking) target = 0;
            if (S.PauseOnHover && hovering) target = 0;
            gain += (target - gain) * (1 - Math.Exp(-dt / 0.18));

            double pxPerSec = S.Speed * LinesPerSecondPerSpeed * LineHeight;
            offset += pxPerSec * gain * dt;
            if (offset >= maxOffset)
            {
                offset = maxOffset;
                ApplyOffset();
                model.SetPlaying(false);   // reached the end
                return;
            }
            ApplyOffset();
        }

        private void UpdateStatus()
        {
            if (S.VoiceActivation)
            {
                micDot.Visibility = Visibility.Visible;
                var st = model.Mic.State;
                Color c;
                if (st == MicState.NoDevice || st == MicState.NoSignal) c = Color.FromRgb(0xEF, 0x44, 0x44);
                else if (model.Mic.IsSpeaking) c = Color.FromRgb(0x22, 0xC5, 0x5E);
                else c = Color.FromArgb(0xB0, 0x9C, 0xA3, 0xAF);
                micDot.Fill = new SolidColorBrush(c);
                micDot.ToolTip = st == MicState.NoDevice ? "No microphone found"
                    : st == MicState.NoSignal ? "Microphone is silent (muted or blocked in Windows privacy settings)"
                    : "Voice activation is on";
            }
            else micDot.Visibility = Visibility.Collapsed;

            // Machine-readable state (used by the automated tests via UI Automation).
            AutomationProperties.SetHelpText(this, string.Format(CultureInfo.InvariantCulture,
                "offset={0:0.0};max={1:0.0};playing={2};gain={3:0.00};speed={4};voice={5};speaking={6};level={7:0.00}",
                offset, maxOffset, model.IsPlaying ? 1 : 0, gain, S.Speed, S.VoiceActivation ? 1 : 0, model.Mic.IsSpeaking ? 1 : 0, model.Mic.Level));
        }
    }
}
