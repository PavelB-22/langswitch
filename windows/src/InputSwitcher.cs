using System;

namespace LangSwitch
{
    /// Раскладки: какая сейчас в активном окне, переключение RU ↔ EN.
    internal static class InputSwitcher
    {
        const int LANG_RU = 0x19, LANG_EN = 0x09;

        static int PrimaryLang(IntPtr hkl) => (int)((long)hkl & 0x3FF);

        public static IntPtr CurrentLayout()
        {
            var hwnd = Native.GetForegroundWindow();
            uint tid = Native.GetWindowThreadProcessId(hwnd, out _);
            return Native.GetKeyboardLayout(tid);
        }

        /// "ru", "en" или другой код (по первичному языку раскладки).
        public static string CurrentLanguage() => LangOf(CurrentLayout());

        public static string LangOf(IntPtr hkl)
        {
            switch (PrimaryLang(hkl))
            {
                case LANG_RU: return "ru";
                case LANG_EN: return "en";
                case 0: return null;
                default:
                    try { return new System.Globalization.CultureInfo(PrimaryLang(hkl)).TwoLetterISOLanguageName; }
                    catch { return "??"; }
            }
        }

        static IntPtr Find(string lang)
        {
            int want = lang == "ru" ? LANG_RU : LANG_EN;
            int n = Native.GetKeyboardLayoutList(0, null);
            if (n <= 0) return IntPtr.Zero;
            var list = new IntPtr[n];
            Native.GetKeyboardLayoutList(n, list);
            foreach (var h in list) if (PrimaryLang(h) == want) return h;
            return IntPtr.Zero;
        }

        public static bool HasBoth => Find("ru") != IntPtr.Zero && Find("en") != IntPtr.Zero;

        /// Включить раскладку в активном окне.
        public static bool Select(string lang)
        {
            if (lang == null) return false;
            var hkl = Find(lang);
            if (hkl == IntPtr.Zero) return false;
            if (PrimaryLang(CurrentLayout()) == PrimaryLang(hkl)) return true;
            var hwnd = Native.GetForegroundWindow();
            return Native.PostMessage(hwnd, Native.WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, hkl);
        }

        /// RU → EN, всё остальное → RU.
        public static void Toggle()
        {
            if (CurrentLanguage() == "ru") Select("en"); else Select("ru");
        }

        public static string CurrentLabel()
        {
            var l = CurrentLanguage();
            return l == null ? "??" : l.ToUpperInvariant();
        }
    }
}
