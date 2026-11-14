using System;
using System.Windows.Threading;

namespace FloatPrompter
{
    /// <summary>Shared state between the editor window, the prompter and the hotkeys.</summary>
    public class AppModel
    {
        public readonly Settings S;
        public readonly MicMonitor Mic = new MicMonitor();

        public bool IsPlaying { get; private set; }
        public bool PrompterVisible { get; private set; }
        /// <summary>0..1 how far through the script the prompter is (written by the prompter).</summary>
        public double Progress { get; set; }
        /// <summary>Display affinity Windows reports for the prompter after we asked it to hide (0x11 hidden, 0x1 black box, 0 visible).</summary>
        public uint PrompterAffinity { get; set; }

        /// <summary>Raised with a key: Script, Speed, Text, Theme, Size, Position, Voice, Capture, Hover, IsPlaying, PrompterVisible.</summary>
        public event Action<string> Changed;
        public event Action RestartRequested;
        public event Action<double> NudgeRequested;   // in lines (+ forward / - back)
        public event Action EditorRequested;

        private readonly DispatcherTimer saveTimer;

        public AppModel(Settings s)
        {
            S = s;
            PrompterVisible = true;
            saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            saveTimer.Tick += delegate { saveTimer.Stop(); S.Save(); };
            Mic.Threshold = S.VoiceThreshold;
            if (S.VoiceActivation) Mic.Start();
        }

        private void Raise(string what)
        {
            var h = Changed;
            if (h != null) h(what);
        }

        /// <summary>Call after changing a setting in <see cref="S"/>; notifies listeners and saves soon.</summary>
        public void Notify(string what)
        {
            if (what == "Voice")
            {
                Mic.Threshold = S.VoiceThreshold;
                if (S.VoiceActivation && !Mic.IsRunning) Mic.Start();
                else if (!S.VoiceActivation && Mic.IsRunning) Mic.Stop();
            }
            Raise(what);
            saveTimer.Stop();
            saveTimer.Start();
        }

        public void SetPlaying(bool playing)
        {
            if (IsPlaying == playing) return;
            IsPlaying = playing;
            Raise("IsPlaying");
        }

        public void TogglePlay() { SetPlaying(!IsPlaying); }

        public void ChangeSpeed(double delta)
        {
            double v = Math.Max(Settings.MinSpeed, Math.Min(Settings.MaxSpeed, Math.Round(S.Speed + delta)));
            if (v == S.Speed) return;
            S.Speed = v;
            Notify("Speed");
        }

        public void Restart()
        {
            SetPlaying(false);
            var h = RestartRequested;
            if (h != null) h();
        }

        public void Nudge(double lines)
        {
            var h = NudgeRequested;
            if (h != null) h(lines);
        }

        public void SetPrompterVisible(bool visible)
        {
            if (PrompterVisible == visible) return;
            PrompterVisible = visible;
            Raise("PrompterVisible");
        }

        public void ToggleVoice()
        {
            S.VoiceActivation = !S.VoiceActivation;
            Notify("Voice");
        }

        public void RequestEditor()
        {
            var h = EditorRequested;
            if (h != null) h();
        }

        public void SaveNow()
        {
            saveTimer.Stop();
            S.Save();
        }
    }
}
