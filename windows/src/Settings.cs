using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Win32;

namespace LangSwitch
{
    /// Все настройки программы. Хранятся в %APPDATA%\LangSwitch\settings.ini.
    internal static class Settings
    {
        public static bool Enabled = true;
        public static bool DoubleShift = true;
        public static double DoubleShiftInterval = 0.3;
        public static bool AutoSwitch = true;
        public static bool CaseFix = true;
        public static bool TypoFix = false;

        public static event Action Changed;

        static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LangSwitch");
        static string FilePath => Path.Combine(Dir, "settings.ini");

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                var d = new Dictionary<string, string>();
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    int i = line.IndexOf('=');
                    if (i > 0) d[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                }
                Enabled = B(d, "enabled", Enabled);
                DoubleShift = B(d, "doubleShift", DoubleShift);
                AutoSwitch = B(d, "autoSwitch", AutoSwitch);
                CaseFix = B(d, "caseFix", CaseFix);
                TypoFix = B(d, "typoFix", TypoFix);
                if (d.TryGetValue("doubleShiftInterval", out var s) &&
                    double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v >= 0.15 && v <= 1)
                    DoubleShiftInterval = v;
            }
            catch { /* битый файл — остаются значения по умолчанию */ }
        }

        static bool B(Dictionary<string, string> d, string k, bool def) =>
            d.TryGetValue(k, out var s) ? s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase) : def;

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllLines(FilePath, new[]
                {
                    "enabled=" + (Enabled ? 1 : 0),
                    "doubleShift=" + (DoubleShift ? 1 : 0),
                    "doubleShiftInterval=" + DoubleShiftInterval.ToString("0.00", CultureInfo.InvariantCulture),
                    "autoSwitch=" + (AutoSwitch ? 1 : 0),
                    "caseFix=" + (CaseFix ? 1 : 0),
                    "typoFix=" + (TypoFix ? 1 : 0),
                });
            }
            catch { }
            Changed?.Invoke();
        }

        // ---- автозапуск ----
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool LaunchAtLogin
        {
            get
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k?.GetValue("LangSwitch") != null;
            }
            set
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (value) k.SetValue("LangSwitch", "\"" + System.Windows.Forms.Application.ExecutablePath + "\"");
                    else k.DeleteValue("LangSwitch", false);
                }
            }
        }
    }
}
