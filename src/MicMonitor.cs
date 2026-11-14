using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace FloatPrompter
{
    public enum MicState { Off = 0, Starting = 1, Listening = 2, NoDevice = 3, NoSignal = 4 }

    /// <summary>
    /// Listens to the default microphone (winmm waveIn, no dependencies) and reports
    /// a 0..1 loudness level plus whether the user is currently talking.
    /// </summary>
    public class MicMonitor
    {
        public const int SampleRate = 16000;
        public const int FrameSamples = 800;          // 50 ms frames
        public const double HoldSeconds = 0.7;        // keep "talking" this long after the voice stops
        public const double NoSignalSeconds = 2.5;    // all-zero input this long => mic blocked/muted

        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly object sync = new object();
        private sealed class RunToken { public volatile bool Cancel; }
        private RunToken token;
        private volatile bool running;
        private volatile int state;
        private double level;
        private double lastVoice = -100;
        private double lastNonZero;
        private int framesAbove;

        private double threshold = 0.42;
        public double Threshold
        {
            get { lock (sync) return threshold; }
            set { lock (sync) threshold = value; }
        }

        public double Now { get { return clock.Elapsed.TotalSeconds; } }
        public MicState State { get { return (MicState)state; } }
        public double Level { get { lock (sync) return level; } }
        public bool IsSpeaking { get { return IsSpeakingAt(Now); } }
        public bool IsRunning { get { return running; } }
        /// <summary>Number of audio frames analysed so far (diagnostics).</summary>
        public long FramesProcessed { get { return Interlocked.Read(ref frames); } }
        private long frames;
        /// <summary>Raw level of the most recent frame (diagnostics).</summary>
        public double LastRawLevel { get { lock (sync) return lastRaw; } }
        private double lastRaw;

        public bool IsSpeakingAt(double now)
        {
            lock (sync) return now - lastVoice < HoldSeconds;
        }

        public void Start()
        {
            if (running) return;
            running = true;
            state = (int)MicState.Starting;
            var t = new RunToken();
            token = t;
            new Thread(() => Run(t)) { IsBackground = true, Name = "FloatPrompter mic" }.Start();
        }

        public void Stop()
        {
            // Non-blocking: the capture thread notices the cancel flag and closes the device itself.
            running = false;
            var t = token;
            token = null;
            if (t != null) t.Cancel = true;
            state = (int)MicState.Off;
            lock (sync) { level = 0; lastVoice = -100; framesAbove = 0; }
        }

        /// <summary>Feeds one frame of 16-bit mono samples. Public so it can be unit tested.</summary>
        public void ProcessSamples(short[] samples, int count, double now)
        {
            if (count <= 0) return;
            double sum = 0;
            bool nonZero = false;
            for (int i = 0; i < count; i++)
            {
                int s = samples[i];
                if (s != 0) nonZero = true;
                double v = s / 32768.0;
                sum += v * v;
            }
            double rms = Math.Sqrt(sum / count);
            double db = 20.0 * Math.Log10(Math.Max(rms, 1e-7));
            double lv = Math.Max(0.0, Math.Min(1.0, (db + 60.0) / 60.0));

            Interlocked.Increment(ref frames);
            lock (sync)
            {
                lastRaw = lv;
                // fast attack, gentle release (for a calm meter)
                level = lv > level ? lv : level * 0.7 + lv * 0.3;

                if (lv >= threshold)
                {
                    framesAbove++;
                    // need 2 consecutive loud frames to start (ignores clicks), any loud frame keeps it going
                    if (framesAbove >= 2 || now - lastVoice < HoldSeconds) lastVoice = now;
                }
                else framesAbove = 0;

                if (nonZero) lastNonZero = now;
            }
            if (state == (int)MicState.Listening || state == (int)MicState.NoSignal || state == (int)MicState.Starting)
                state = (int)(now - lastNonZero > NoSignalSeconds ? MicState.NoSignal : MicState.Listening);
        }

        private void Run(RunToken t)
        {
            while (!t.Cancel)
            {
                try { Capture(t); }
                catch (Exception ex) { App.Log("Mic error: " + ex.Message); }
                if (t.Cancel) break;
                // device missing or stalled: retry shortly (lets you plug a mic in later)
                for (int i = 0; i < 20 && !t.Cancel; i++) Thread.Sleep(100);
            }
        }

        private void SetState(RunToken t, MicState s) { if (!t.Cancel) state = (int)s; }

        /// <summary>Test hook: FLOATPROMPTER_FAKE_MIC=path to a file holding an amplitude (0..1) to simulate a voice.</summary>
        private void FakeCapture(RunToken t, string path)
        {
            SetState(t, MicState.Listening);
            var frame = new short[FrameSamples];
            var rnd = new Random();
            int phase = 0;
            while (!t.Cancel)
            {
                double amp = 0;
                try { amp = double.Parse(System.IO.File.ReadAllText(path).Trim(), System.Globalization.CultureInfo.InvariantCulture); } catch (Exception) { }
                for (int i = 0; i < FrameSamples; i++, phase++)
                    frame[i] = (short)(amp * 32767 * Math.Sin(2 * Math.PI * 220 * phase / SampleRate) + rnd.Next(-8, 9));
                ProcessSamples(frame, FrameSamples, Now);
                Thread.Sleep(50);
            }
        }

        private void Capture(RunToken t)
        {
            string fake = Environment.GetEnvironmentVariable("FLOATPROMPTER_FAKE_MIC");
            if (!string.IsNullOrEmpty(fake)) { FakeCapture(t, fake); return; }
            const int bufBytes = FrameSamples * 2;
            const int bufCount = 4;
            IntPtr h = IntPtr.Zero;
            var evt = new AutoResetEvent(false);
            var hdrs = new IntPtr[bufCount];
            var bufs = new IntPtr[bufCount];
            int hdrSize = Marshal.SizeOf(typeof(WAVEHDR));
            int flagsOff = Marshal.OffsetOf(typeof(WAVEHDR), "dwFlags").ToInt32();
            int recOff = Marshal.OffsetOf(typeof(WAVEHDR), "dwBytesRecorded").ToInt32();
            try
            {
                var fmt = new WAVEFORMATEX
                {
                    wFormatTag = 1,
                    nChannels = 1,
                    nSamplesPerSec = SampleRate,
                    nAvgBytesPerSec = SampleRate * 2,
                    nBlockAlign = 2,
                    wBitsPerSample = 16,
                    cbSize = 0
                };
                int r = waveInOpen(out h, WAVE_MAPPER, ref fmt, evt.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, CALLBACK_EVENT);
                if (r != 0)
                {
                    h = IntPtr.Zero;
                    SetState(t, MicState.NoDevice);
                    return;
                }
                for (int i = 0; i < bufCount; i++)
                {
                    bufs[i] = Marshal.AllocHGlobal(bufBytes);
                    hdrs[i] = Marshal.AllocHGlobal(hdrSize);
                    var wh = new WAVEHDR { lpData = bufs[i], dwBufferLength = bufBytes };
                    Marshal.StructureToPtr(wh, hdrs[i], false);
                    waveInPrepareHeader(h, hdrs[i], hdrSize);
                    waveInAddBuffer(h, hdrs[i], hdrSize);
                }
                lock (sync) lastNonZero = Now;
                SetState(t, MicState.Listening);
                if (waveInStart(h) != 0) { SetState(t, MicState.NoDevice); return; }

                var samples = new short[FrameSamples];
                double lastData = Now;
                while (!t.Cancel)
                {
                    evt.WaitOne(250);
                    bool any = false;
                    for (int i = 0; i < bufCount; i++)
                    {
                        int flags = Marshal.ReadInt32(hdrs[i], flagsOff);
                        if ((flags & WHDR_DONE) == 0) continue;
                        any = true;
                        int n = Math.Min(FrameSamples, Marshal.ReadInt32(hdrs[i], recOff) / 2);
                        if (n > 0)
                        {
                            Marshal.Copy(bufs[i], samples, 0, n);
                            if (!t.Cancel) ProcessSamples(samples, n, Now);
                        }
                        Marshal.WriteInt32(hdrs[i], flagsOff, flags & ~WHDR_DONE);
                        if (!t.Cancel) waveInAddBuffer(h, hdrs[i], hdrSize);
                    }
                    if (any) lastData = Now;
                    else if (Now - lastData > 3.0) { SetState(t, MicState.NoDevice); return; } // stalled / unplugged
                }
            }
            finally
            {
                if (h != IntPtr.Zero)
                {
                    waveInReset(h);
                    for (int i = 0; i < bufCount; i++)
                        if (hdrs[i] != IntPtr.Zero) waveInUnprepareHeader(h, hdrs[i], hdrSize);
                    waveInClose(h);
                }
                for (int i = 0; i < bufCount; i++)
                {
                    if (bufs[i] != IntPtr.Zero) Marshal.FreeHGlobal(bufs[i]);
                    if (hdrs[i] != IntPtr.Zero) Marshal.FreeHGlobal(hdrs[i]);
                }
                evt.Close();
                if (t.Cancel) lock (sync) level = 0;
            }
        }

        // ---- winmm interop ----
        private const int WAVE_MAPPER = -1;
        private const int CALLBACK_EVENT = 0x00050000;
        private const int WHDR_DONE = 0x1;

        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct WAVEFORMATEX
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public int nSamplesPerSec;
            public int nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEHDR
        {
            public IntPtr lpData;
            public int dwBufferLength;
            public int dwBytesRecorded;
            public IntPtr dwUser;
            public int dwFlags;
            public int dwLoops;
            public IntPtr lpNext;
            public IntPtr reserved;
        }

        [DllImport("winmm.dll")] private static extern int waveInOpen(out IntPtr h, int deviceId, ref WAVEFORMATEX fmt, IntPtr callback, IntPtr instance, int flags);
        [DllImport("winmm.dll")] private static extern int waveInPrepareHeader(IntPtr h, IntPtr hdr, int size);
        [DllImport("winmm.dll")] private static extern int waveInUnprepareHeader(IntPtr h, IntPtr hdr, int size);
        [DllImport("winmm.dll")] private static extern int waveInAddBuffer(IntPtr h, IntPtr hdr, int size);
        [DllImport("winmm.dll")] private static extern int waveInStart(IntPtr h);
        [DllImport("winmm.dll")] private static extern int waveInReset(IntPtr h);
        [DllImport("winmm.dll")] private static extern int waveInClose(IntPtr h);
    }
}
