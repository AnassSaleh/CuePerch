using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace FloatPrompter
{
    [DataContract]
    public class Settings
    {
        [DataMember] public string Script;
        [DataMember] public double Speed;
        [DataMember] public double FontSize;
        [DataMember] public string FontFamily;
        [DataMember] public double Width;
        [DataMember] public double Height;
        [DataMember] public double BackgroundOpacity;
        [DataMember] public bool LightTheme;
        [DataMember] public bool AlignLeft;
        [DataMember] public string Monitor;
        [DataMember] public bool CustomPosition;
        [DataMember] public int CustomX;
        [DataMember] public int CustomY;
        [DataMember] public bool VoiceActivation;
        [DataMember] public double VoiceSensitivity;
        [DataMember] public bool HideFromCapture;
        [DataMember] public bool PauseOnHover;

        public const double MinSpeed = 1, MaxSpeed = 20;
        public const double MinFont = 14, MaxFont = 96;
        public const double MinWidth = 220, MaxWidth = 2400;
        public const double MinHeight = 80, MaxHeight = 1200;

        public static readonly string DefaultScript =
            "Welcome to FloatPrompter!\r\n" +
            "Paste or type your script in the main window, then press Start. [pause]\r\n\r\n" +
            "The text scrolls slowly up here, right under your webcam, so you can keep eye contact while you read.\r\n\r\n" +
            "Make it faster with Ctrl+Alt+Up and slower with Ctrl+Alt+Down. " +
            "Pause and resume any time with Ctrl+Alt+P.\r\n\r\n" +
            "Drag this panel to move it, drag its edges to resize it, and double-click it to snap it back to the top. " +
            "Use the mouse wheel to scroll by hand.\r\n\r\n" +
            "Turn on voice activation and the text only moves while you are talking. [smile]\r\n\r\n" +
            "Words in square brackets, like [breathe], are notes for you. They appear dimmed.\r\n\r\n" +
            "This panel is hidden from screen sharing and recordings, so only you can see it.\r\n\r\n" +
            "Good luck!";

        public Settings() { SetDefaults(); }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext c) { SetDefaults(); }

        private void SetDefaults()
        {
            Script = DefaultScript;
            Speed = 6;
            FontSize = 28;
            FontFamily = "Segoe UI";
            Width = 560;
            Height = 190;
            BackgroundOpacity = 1.0;
            LightTheme = false;
            AlignLeft = false;
            Monitor = null;
            CustomPosition = false;
            CustomX = 0;
            CustomY = 0;
            VoiceActivation = false;
            VoiceSensitivity = 0.62;
            HideFromCapture = true;
            PauseOnHover = false;
        }

        private static double Clamp(double v, double lo, double hi, double fallback)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return fallback;
            return Math.Max(lo, Math.Min(hi, v));
        }

        public void Normalize()
        {
            if (Script == null) Script = "";
            Speed = Clamp(Speed, MinSpeed, MaxSpeed, 6);
            FontSize = Clamp(FontSize, MinFont, MaxFont, 28);
            if (string.IsNullOrWhiteSpace(FontFamily)) FontFamily = "Segoe UI";
            Width = Clamp(Width, MinWidth, MaxWidth, 560);
            Height = Clamp(Height, MinHeight, MaxHeight, 190);
            BackgroundOpacity = Clamp(BackgroundOpacity, 0.3, 1.0, 1.0);
            VoiceSensitivity = Clamp(VoiceSensitivity, 0.0, 1.0, 0.62);
        }

        /// <summary>Voice level (0..1) above which we treat the input as speech.</summary>
        public double VoiceThreshold { get { return 0.95 - VoiceSensitivity * 0.85; } }

        public static string Dir
        {
            get
            {
                string over = Environment.GetEnvironmentVariable("FLOATPROMPTER_DATA_DIR");
                if (!string.IsNullOrEmpty(over)) return over;
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FloatPrompter");
            }
        }

        public static string FilePath { get { return Path.Combine(Dir, "settings.json"); } }

        public static Settings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    byte[] bytes = File.ReadAllBytes(FilePath);
                    int start = (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) ? 3 : 0; // tolerate a UTF-8 BOM
                    using (var ms = new MemoryStream(bytes, start, bytes.Length - start))
                    {
                        var ser = new DataContractJsonSerializer(typeof(Settings));
                        var s = ser.ReadObject(ms) as Settings;
                        if (s != null) { s.Normalize(); return s; }
                    }
                }
            }
            catch (Exception ex) { App.Log("Settings load failed: " + ex.Message); }
            var d = new Settings();
            d.Normalize();
            return d;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                string tmp = FilePath + ".tmp";
                using (var fs = File.Create(tmp))
                {
                    new DataContractJsonSerializer(typeof(Settings)).WriteObject(fs, this);
                }
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
                else File.Move(tmp, FilePath);
            }
            catch (Exception ex) { App.Log("Settings save failed: " + ex.Message); }
        }
    }
}
