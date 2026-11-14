using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace FloatPrompter
{
    /// <summary>Main window: script editor and settings.</summary>
    public class ControlWindow : Window
    {
        private readonly AppModel model;
        private Settings S { get { return model.S; } }
        private readonly FrameworkElement ui;
        private bool syncing;
        private readonly DispatcherTimer uiTimer;

        private readonly TextBox scriptBox;
        private readonly TextBlock wordInfo, statusText, speedVal, sizeVal, sensVal, widthVal, heightVal, opacityVal, voiceStatus, hotkeyWarn;
        private readonly Slider speedSlider, sizeSlider, sensSlider, widthSlider, heightSlider, opacitySlider;
        private readonly ComboBox fontBox, displayBox;
        private readonly RadioButton alignCenter, alignLeft, themeDark, themeLight;
        private readonly CheckBox voiceSwitch, showSwitch, hideSwitch, hoverSwitch;
        private readonly Button playBtn;
        private readonly FrameworkElement voicePanel, meterTrack, meterFill, meterMark;
        private readonly Grid hotkeyGrid;
        private readonly TextBlock playGlyph, playLabel;

        private T Find<T>(string name) where T : class
        {
            var o = ui.FindName(name) as T;
            if (o == null) throw new InvalidOperationException("UI element not found: " + name);
            return o;
        }

        public ControlWindow(AppModel m)
        {
            model = m;
            Title = App.EditorTitle;
            Width = 980; Height = 700;
            MinWidth = 760; MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6));

            ui = (FrameworkElement)App.LoadXaml("ControlWindow.xaml");
            Content = ui;

            scriptBox = Find<TextBox>("ScriptBox");
            wordInfo = Find<TextBlock>("WordInfo");
            statusText = Find<TextBlock>("StatusText");
            speedVal = Find<TextBlock>("SpeedVal");
            sizeVal = Find<TextBlock>("SizeVal");
            sensVal = Find<TextBlock>("SensVal");
            widthVal = Find<TextBlock>("WidthVal");
            heightVal = Find<TextBlock>("HeightVal");
            opacityVal = Find<TextBlock>("OpacityVal");
            voiceStatus = Find<TextBlock>("VoiceStatus");
            hotkeyWarn = Find<TextBlock>("HotkeyWarn");
            speedSlider = Find<Slider>("SpeedSlider");
            sizeSlider = Find<Slider>("SizeSlider");
            sensSlider = Find<Slider>("SensSlider");
            widthSlider = Find<Slider>("WidthSlider");
            heightSlider = Find<Slider>("HeightSlider");
            opacitySlider = Find<Slider>("OpacitySlider");
            fontBox = Find<ComboBox>("FontBox");
            displayBox = Find<ComboBox>("DisplayBox");
            alignCenter = Find<RadioButton>("AlignCenter");
            alignLeft = Find<RadioButton>("AlignLeft");
            themeDark = Find<RadioButton>("ThemeDark");
            themeLight = Find<RadioButton>("ThemeLight");
            voiceSwitch = Find<CheckBox>("VoiceSwitch");
            showSwitch = Find<CheckBox>("ShowSwitch");
            hideSwitch = Find<CheckBox>("HideSwitch");
            hoverSwitch = Find<CheckBox>("HoverSwitch");
            playBtn = Find<Button>("PlayBtn");
            voicePanel = Find<FrameworkElement>("VoicePanel");
            meterTrack = Find<FrameworkElement>("MeterTrack");
            meterFill = Find<FrameworkElement>("MeterFill");
            meterMark = Find<FrameworkElement>("MeterMark");
            hotkeyGrid = Find<Grid>("HotkeyGrid");

            // Play button content: icon + label
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            playGlyph = new TextBlock { FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            playLabel = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(playGlyph); sp.Children.Add(playLabel);
            playBtn.Content = sp;

            // Fonts
            var fonts = Fonts.SystemFontFamilies.OrderBy(f => f.Source, StringComparer.OrdinalIgnoreCase).ToList();
            fontBox.ItemsSource = fonts;

            LoadFromSettings();
            RefreshDisplays();
            WireEvents();

            model.Changed += OnModelChanged;

            uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
            uiTimer.Tick += delegate { UpdateLive(); };
            uiTimer.Start();

            SourceInitialized += delegate
            {
                Native.ApplyCaptureExclusion(new WindowInteropHelper(this).Handle, S.HideFromCapture);
            };
            Closed += delegate { App.Quit(); };
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += delegate { Dispatcher.BeginInvoke(new Action(RefreshDisplays)); };
        }

        private void LoadFromSettings()
        {
            syncing = true;
            scriptBox.Text = S.Script;
            speedSlider.Value = S.Speed;
            sizeSlider.Value = S.FontSize;
            sensSlider.Value = Math.Round(S.VoiceSensitivity * 100);
            widthSlider.Value = S.Width;
            heightSlider.Value = S.Height;
            opacitySlider.Value = Math.Round(S.BackgroundOpacity * 100);
            alignLeft.IsChecked = S.AlignLeft; alignCenter.IsChecked = !S.AlignLeft;
            themeLight.IsChecked = S.LightTheme; themeDark.IsChecked = !S.LightTheme;
            voiceSwitch.IsChecked = S.VoiceActivation;
            showSwitch.IsChecked = model.PrompterVisible;
            hideSwitch.IsChecked = S.HideFromCapture;
            hoverSwitch.IsChecked = S.PauseOnHover;
            SelectFont();
            syncing = false;
            UpdateLabels();
            UpdatePlayButton();
            UpdateWordInfo();
            voicePanel.Visibility = S.VoiceActivation ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SelectFont()
        {
            var fonts = fontBox.ItemsSource as List<FontFamily>;
            if (fonts == null) return;
            var match = fonts.FirstOrDefault(f => string.Equals(f.Source, S.FontFamily, StringComparison.OrdinalIgnoreCase));
            fontBox.SelectedItem = match;
        }

        private void WireEvents()
        {
            scriptBox.TextChanged += delegate
            {
                if (syncing) return;
                S.Script = scriptBox.Text;
                model.Notify("Script");
                UpdateWordInfo();
            };
            Find<Button>("PasteBtn").Click += delegate
            {
                if (!Clipboard.ContainsText()) { scriptBox.Focus(); return; }
                scriptBox.Focus();
                scriptBox.SelectAll();
                scriptBox.Paste();          // goes through undo, so Ctrl+Z brings the old script back
                scriptBox.CaretIndex = 0;
                scriptBox.ScrollToHome();
                model.Restart();
            };
            Find<Button>("OpenBtn").Click += delegate { OpenFile(); };
            Find<Button>("ClearBtn").Click += delegate
            {
                scriptBox.Focus();
                scriptBox.SelectAll();
                scriptBox.SelectedText = "";
                model.Restart();
            };
            playBtn.Click += delegate { model.TogglePlay(); };
            Find<Button>("RestartBtn").Click += delegate { model.Restart(); };
            Find<Button>("CenterBtn").Click += delegate
            {
                var item = displayBox.SelectedItem as DisplayItem;
                if (item != null) S.Monitor = item.DeviceName;
                S.CustomPosition = false;
                model.Notify("Position");
            };

            speedSlider.ValueChanged += delegate { UpdateLabels(); if (syncing) return; S.Speed = speedSlider.Value; model.Notify("Speed"); };
            sizeSlider.ValueChanged += delegate { UpdateLabels(); if (syncing) return; S.FontSize = sizeSlider.Value; model.Notify("Text"); };
            sensSlider.ValueChanged += delegate { UpdateLabels(); if (syncing) return; S.VoiceSensitivity = sensSlider.Value / 100.0; model.Notify("Voice"); };
            widthSlider.ValueChanged += delegate { UpdateLabels(); if (syncing) return; S.Width = widthSlider.Value; model.Notify("Size"); };
            heightSlider.ValueChanged += delegate { UpdateLabels(); if (syncing) return; S.Height = heightSlider.Value; model.Notify("Size"); };
            opacitySlider.ValueChanged += delegate { UpdateLabels(); if (syncing) return; S.BackgroundOpacity = opacitySlider.Value / 100.0; model.Notify("Theme"); };

            fontBox.SelectionChanged += delegate
            {
                if (syncing) return;
                var f = fontBox.SelectedItem as FontFamily;
                if (f == null) return;
                S.FontFamily = f.Source;
                model.Notify("Text");
            };
            RoutedEventHandler align = delegate { if (syncing) return; S.AlignLeft = alignLeft.IsChecked == true; model.Notify("Text"); };
            alignLeft.Checked += align; alignCenter.Checked += align;
            RoutedEventHandler theme = delegate { if (syncing) return; S.LightTheme = themeLight.IsChecked == true; model.Notify("Theme"); };
            themeLight.Checked += theme; themeDark.Checked += theme;

            RoutedEventHandler voice = delegate
            {
                if (syncing) return;
                S.VoiceActivation = voiceSwitch.IsChecked == true;
                model.Notify("Voice");
            };
            voiceSwitch.Checked += voice; voiceSwitch.Unchecked += voice;

            RoutedEventHandler show = delegate { if (syncing) return; model.SetPrompterVisible(showSwitch.IsChecked == true); };
            showSwitch.Checked += show; showSwitch.Unchecked += show;

            RoutedEventHandler hide = delegate
            {
                if (syncing) return;
                S.HideFromCapture = hideSwitch.IsChecked == true;
                model.Notify("Capture");
            };
            hideSwitch.Checked += hide; hideSwitch.Unchecked += hide;

            RoutedEventHandler hover = delegate { if (syncing) return; S.PauseOnHover = hoverSwitch.IsChecked == true; model.Notify("Hover"); };
            hoverSwitch.Checked += hover; hoverSwitch.Unchecked += hover;

            displayBox.SelectionChanged += delegate
            {
                if (syncing) return;
                var item = displayBox.SelectedItem as DisplayItem;
                if (item == null) return;
                S.Monitor = item.DeviceName;
                S.CustomPosition = false;
                model.Notify("Position");
            };
        }

        private void OnModelChanged(string what)
        {
            syncing = true;
            try
            {
                switch (what)
                {
                    case "Speed": speedSlider.Value = S.Speed; break;
                    case "Size": widthSlider.Value = S.Width; heightSlider.Value = S.Height; break;
                    case "Position": SelectDisplay(); break;
                    case "Voice":
                        voiceSwitch.IsChecked = S.VoiceActivation;
                        voicePanel.Visibility = S.VoiceActivation ? Visibility.Visible : Visibility.Collapsed;
                        break;
                    case "Capture":
                        hideSwitch.IsChecked = S.HideFromCapture;
                        Native.ApplyCaptureExclusion(new WindowInteropHelper(this).Handle, S.HideFromCapture);
                        break;
                    case "PrompterVisible": showSwitch.IsChecked = model.PrompterVisible; break;
                    case "IsPlaying": UpdatePlayButton(); break;
                }
            }
            finally { syncing = false; }
            UpdateLabels();
        }

        private void UpdateLabels()
        {
            speedVal.Text = speedSlider.Value.ToString("0", CultureInfo.InvariantCulture);
            sizeVal.Text = sizeSlider.Value.ToString("0", CultureInfo.InvariantCulture) + " pt";
            sensVal.Text = sensSlider.Value.ToString("0", CultureInfo.InvariantCulture) + "%";
            widthVal.Text = widthSlider.Value.ToString("0", CultureInfo.InvariantCulture) + " px";
            heightVal.Text = heightSlider.Value.ToString("0", CultureInfo.InvariantCulture) + " px";
            opacityVal.Text = opacitySlider.Value.ToString("0", CultureInfo.InvariantCulture) + "%";
        }

        private void UpdatePlayButton()
        {
            playGlyph.Text = model.IsPlaying ? "" : "";
            playLabel.Text = model.IsPlaying ? "Pause" : "Start";
            System.Windows.Automation.AutomationProperties.SetName(playBtn, model.IsPlaying ? "Pause" : "Start");
        }

        private static readonly Regex WordRx = new Regex(@"[^\s\[\]]+", RegexOptions.Compiled);
        private static readonly Regex CueRx = new Regex(@"\[[^\]\r\n]{1,80}\]", RegexOptions.Compiled);

        private void UpdateWordInfo()
        {
            string text = CueRx.Replace(S.Script ?? "", " ");
            int words = WordRx.Matches(text).Count;
            double minutes = words / 140.0;
            int secs = (int)Math.Round(minutes * 60);
            wordInfo.Text = words == 0 ? "" : string.Format(CultureInfo.InvariantCulture, "{0} words · about {1}:{2:00} to read", words, secs / 60, secs % 60);
        }

        private void UpdateLive()
        {
            // Status line
            string st;
            int pct = (int)Math.Round(model.Progress * 100);
            if (model.IsPlaying)
            {
                st = S.VoiceActivation && !model.Mic.IsSpeaking ? "Waiting for your voice" : "Playing";
            }
            else st = pct >= 100 ? "Finished" : "Paused";
            if (!model.PrompterVisible) st += " (prompter hidden)";
            st += string.Format(CultureInfo.InvariantCulture, " · {0}%", pct);
            if (statusText.Text != st) statusText.Text = st;

            if (!S.VoiceActivation) return;
            double w = meterTrack.ActualWidth;
            double level = model.Mic.Level;
            double thr = S.VoiceThreshold;
            meterFill.Width = Math.Max(0, w * level);
            meterMark.Margin = new Thickness(Math.Max(0, w * thr - 1), 0, 0, 0);
            ((System.Windows.Controls.Border)meterFill).Background = new SolidColorBrush(level >= thr ? Color.FromRgb(0x22, 0xC5, 0x5E) : Color.FromRgb(0x9C, 0xA3, 0xAF));
            string vs;
            switch (model.Mic.State)
            {
                case MicState.NoDevice: vs = "No microphone found. Plug one in or pick a default mic in Windows sound settings."; break;
                case MicState.NoSignal: vs = "The microphone is silent. Check it isn't muted, and that Windows Settings › Privacy › Microphone allows desktop apps."; break;
                case MicState.Starting: vs = "Starting microphone…"; break;
                default: vs = model.Mic.IsSpeaking ? "Hearing you" : "Listening…"; break;
            }
            if (voiceStatus.Text != vs) voiceStatus.Text = vs;
        }

        private void OpenFile()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Text files (*.txt;*.md;*.rtf)|*.txt;*.md;*.rtf|All files (*.*)|*.*",
                Title = "Open a script"
            };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                string text = File.ReadAllText(dlg.FileName);
                if (dlg.FileName.EndsWith(".rtf", StringComparison.OrdinalIgnoreCase))
                {
                    var box = new RichTextBox();
                    var range = new System.Windows.Documents.TextRange(box.Document.ContentStart, box.Document.ContentEnd);
                    using (var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text))) range.Load(ms, DataFormats.Rtf);
                    text = range.Text;
                }
                scriptBox.Focus();
                scriptBox.SelectAll();
                scriptBox.SelectedText = text;
                scriptBox.CaretIndex = 0;
                scriptBox.ScrollToHome();
                model.Restart();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Couldn't open that file:\n" + ex.Message, App.EditorTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ---------- displays ----------

        private class DisplayItem
        {
            public string DeviceName;
            public string Label;
            public override string ToString() { return Label; }
        }

        private void RefreshDisplays()
        {
            syncing = true;
            try
            {
                var items = new List<DisplayItem>();
                var screens = Forms.Screen.AllScreens;
                for (int i = 0; i < screens.Length; i++)
                {
                    var s = screens[i];
                    items.Add(new DisplayItem
                    {
                        DeviceName = s.DeviceName,
                        Label = string.Format(CultureInfo.InvariantCulture, "Display {0}  ({1}×{2}){3}", i + 1, s.Bounds.Width, s.Bounds.Height, s.Primary ? "  · main" : "")
                    });
                }
                displayBox.ItemsSource = items;
                SelectDisplay();
            }
            finally { syncing = false; }
        }

        private void SelectDisplay()
        {
            var items = displayBox.ItemsSource as List<DisplayItem>;
            if (items == null || items.Count == 0) return;
            var match = items.FirstOrDefault(i => i.DeviceName == S.Monitor);
            if (match == null)
            {
                string primary = Forms.Screen.PrimaryScreen.DeviceName;
                match = items.FirstOrDefault(i => i.DeviceName == primary) ?? items[0];
            }
            displayBox.SelectedItem = match;
        }

        public void SetHotkeys(List<Hotkeys.Entry> entries)
        {
            hotkeyGrid.Children.Clear();
            hotkeyGrid.RowDefinitions.Clear();
            var failed = new List<string>();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                hotkeyGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var kbd = new Border { Style = (Style)ui.FindResource("Kbd") };
                kbd.Child = new TextBlock { Text = e.Keys, FontSize = 12, Foreground = new SolidColorBrush(e.Registered ? Color.FromRgb(0x1F, 0x29, 0x37) : Color.FromRgb(0xB9, 0x1C, 0x1C)) };
                Grid.SetRow(kbd, i);
                var act = new TextBlock { Text = e.Action, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(0x37, 0x41, 0x51)) };
                Grid.SetRow(act, i); Grid.SetColumn(act, 2);
                hotkeyGrid.Children.Add(kbd);
                hotkeyGrid.Children.Add(act);
                if (!e.Registered) failed.Add(e.Keys);
            }
            if (failed.Count > 0)
            {
                hotkeyWarn.Text = "Already used by another app: " + string.Join(", ", failed.ToArray()) + ". Use the buttons on the prompter instead.";
                hotkeyWarn.Visibility = Visibility.Visible;
            }
        }
    }
}
