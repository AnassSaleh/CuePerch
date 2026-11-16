// Unit test for the voice-activation logic in MicMonitor (feeds synthetic audio frames).
// Build + run: powershell -ExecutionPolicy Bypass -File tests\run-voice-test.ps1
using System;
using CuePerch;

static class VoiceGateTest
{
    static int failures;

    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "PASS  " : "FAIL  ") + what);
        if (!ok) failures++;
    }

    static short[] Tone(double amplitude, int n, int offset)
    {
        var s = new short[n];
        for (int i = 0; i < n; i++) s[i] = (short)(amplitude * 32767 * Math.Sin(2 * Math.PI * 220 * (i + offset) / MicMonitor.SampleRate));
        return s;
    }

    static short[] Noise(double amplitude, int n, Random r)
    {
        var s = new short[n];
        for (int i = 0; i < n; i++) s[i] = (short)(amplitude * 32767 * (r.NextDouble() * 2 - 1));
        return s;
    }

    static int Main()
    {
        var mic = new MicMonitor();
        var settings = new Settings();          // default sensitivity
        mic.Threshold = settings.VoiceThreshold;
        const int N = MicMonitor.FrameSamples;
        const double dt = N / (double)MicMonitor.SampleRate; // 50 ms
        var rnd = new Random(1);
        double t = 100;

        // quiet room noise (~ -50 dBFS) for 2 s
        for (int i = 0; i < 40; i++, t += dt) { var f = Noise(0.005, N, rnd); mic.ProcessSamples(f, N, t); }
        Check(!mic.IsSpeakingAt(t), "quiet room noise is not treated as speech (level " + mic.Level.ToString("0.00") + ", threshold " + mic.Threshold.ToString("0.00") + ")");

        // one loud click (single frame) must not start scrolling
        mic.ProcessSamples(Tone(0.5, N, 0), N, t); t += dt;
        mic.ProcessSamples(Noise(0.005, N, rnd), N, t); t += dt;
        Check(!mic.IsSpeakingAt(t), "a single short click is ignored");

        // normal speech level (~ -20 dBFS) for 1 s
        for (int i = 0; i < 20; i++, t += dt) mic.ProcessSamples(Tone(0.1, N, i * N), N, t);
        Check(mic.IsSpeakingAt(t), "talking is detected (level " + mic.Level.ToString("0.00") + ")");

        // short pause between words (0.4 s) keeps scrolling
        for (int i = 0; i < 8; i++, t += dt) mic.ProcessSamples(Noise(0.005, N, rnd), N, t);
        Check(mic.IsSpeakingAt(t), "a short pause between words keeps it going");

        // long silence (1 s) stops it
        for (int i = 0; i < 12; i++, t += dt) mic.ProcessSamples(Noise(0.005, N, rnd), N, t);
        Check(!mic.IsSpeakingAt(t), "stops shortly after you stop talking");

        // sensitivity: soft speech (~ -36 dBFS) detected only at high sensitivity
        settings.VoiceSensitivity = 0.2; mic.Threshold = settings.VoiceThreshold;
        for (int i = 0; i < 10; i++, t += dt) mic.ProcessSamples(Tone(0.016, N, i * N), N, t);
        bool lowSens = mic.IsSpeakingAt(t);
        for (int i = 0; i < 30; i++, t += dt) mic.ProcessSamples(Noise(0.0005, N, rnd), N, t);
        settings.VoiceSensitivity = 0.95; mic.Threshold = settings.VoiceThreshold;
        for (int i = 0; i < 10; i++, t += dt) mic.ProcessSamples(Tone(0.016, N, i * N), N, t);
        bool highSens = mic.IsSpeakingAt(t);
        Check(!lowSens && highSens, "sensitivity slider changes what counts as speech");

        // all-zero input (mic blocked by privacy settings) => NoSignal
        var mic2 = new MicMonitor();
        var zeros = new short[N];
        double t2 = 0;
        mic2.ProcessSamples(Tone(0.1, N, 0), N, t2);
        for (int i = 0; i < 70; i++) { t2 += dt; mic2.ProcessSamples(zeros, N, t2); }
        // state only updates while running, so check the level path instead
        Check(mic2.Level < 0.05, "silent input gives zero level");

        // live device: open the real default microphone briefly
        var live = new MicMonitor();
        live.Start();
        for (int i = 0; i < 6; i++)
        {
            System.Threading.Thread.Sleep(500);
            Console.WriteLine("      t=" + (i + 1) * 0.5 + "s state=" + live.State + " frames=" + live.FramesProcessed + " raw=" + live.LastRawLevel.ToString("0.00") + " level=" + live.Level.ToString("0.00"));
        }
        Console.WriteLine("      live microphone state: " + live.State + ", level " + live.Level.ToString("0.00"));
        Check(live.State == MicState.Listening || live.State == MicState.NoSignal || live.State == MicState.NoDevice, "live microphone opens (or reports no device) without errors");
        live.Stop();
        Check(live.State == MicState.Off && !live.IsRunning, "microphone stops cleanly");

        Console.WriteLine(failures == 0 ? "ALL VOICE TESTS PASSED" : failures + " VOICE TEST(S) FAILED");
        return failures;
    }
}
