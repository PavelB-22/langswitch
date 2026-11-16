using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Forms;

namespace LangSwitch
{
    /// Исправляет текст: стирает и печатает, читает выделение, работает с буфером обмена.
    /// Всё здесь вызывается из рабочего потока (не из перехватчика клавиш).
    internal static class TextFixer
    {
        // ---------------- отправка нажатий ----------------

        static Native.INPUT Key(ushort vk, bool up) => new Native.INPUT
        {
            type = Native.INPUT_KEYBOARD,
            u = new Native.InputUnion { ki = new Native.KEYBDINPUT { wVk = vk, dwFlags = up ? Native.KEYEVENTF_KEYUP : 0, dwExtraInfo = KeyMonitor.OwnMark } }
        };

        static Native.INPUT Unicode(char c, bool up) => new Native.INPUT
        {
            type = Native.INPUT_KEYBOARD,
            u = new Native.InputUnion { ki = new Native.KEYBDINPUT { wScan = c, dwFlags = Native.KEYEVENTF_UNICODE | (up ? Native.KEYEVENTF_KEYUP : 0), dwExtraInfo = KeyMonitor.OwnMark } }
        };

        static void Send(params Native.INPUT[] inputs)
        {
            Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Native.INPUT)));
        }

        public static void Backspace(int count)
        {
            for (int i = 0; i < count; i++) { Send(Key(Native.VK_BACK, false), Key(Native.VK_BACK, true)); Thread.Sleep(2); }
        }

        public static void Type(string text)
        {
            foreach (var c in text) { Send(Unicode(c, false), Unicode(c, true)); Thread.Sleep(2); }
        }

        /// Стереть deleteCount символов, включить раскладку lang (если задана) и напечатать text.
        public static void Replace(int deleteCount, string text, string lang)
        {
            Backspace(deleteCount);
            if (lang != null) InputSwitcher.Select(lang);
            Type(text);
        }

        /// Стирает слово и печатает его в другой раскладке. Возвращает новое слово.
        public static string FixWord(string word)
        {
            var (conv, lang) = LayoutConverter.Convert(word);
            Replace(word.Length, conv, lang);
            return conv;
        }

        // ---------------- UI Automation ----------------

        /// Выполнить с ограничением по времени: некоторые программы отвечают долго — тогда просто сдаёмся.
        static T WithTimeout<T>(Func<T> f, int ms, T fallback)
        {
            try
            {
                var t = Task.Run(f);
                return t.Wait(ms) ? t.Result : fallback;
            }
            catch { return fallback; }
        }

        /// Курсор сейчас в поле пароля?
        public static bool InPasswordField() => WithTimeout(() =>
        {
            var el = AutomationElement.FocusedElement;
            return el != null && el.Current.IsPassword;
        }, 250, false);

        /// Выделенный текст в активном поле (null — нет выделения или программа его не отдаёт).
        public static string SelectedText() => WithTimeout(() =>
        {
            var el = AutomationElement.FocusedElement;
            if (el == null) return null;
            if (!el.TryGetCurrentPattern(TextPattern.Pattern, out var p)) return null;
            var ranges = ((TextPattern)p).GetSelection();
            if (ranges == null || ranges.Length == 0) return null;
            var parts = new List<string>();
            foreach (var r in ranges) parts.Add(r.GetText(10000));
            var text = string.Join("", parts);
            return string.IsNullOrEmpty(text) ? null : text;
        }, 400, null);

        // ---------------- выделение через буфер обмена ----------------

        /// Заменяет выделение исправленным текстом через буфер обмена, потом возвращает буфер как был.
        public static void FixSelection(string text)
        {
            var (conv, lang) = LayoutConverter.Convert(text);
            var saved = SaveClipboard();
            if (!Retry(() => Clipboard.SetText(conv))) return;
            InputSwitcher.Select(lang);
            Send(Key(Native.VK_CONTROL, false), Key(Native.VK_V, false), Key(Native.VK_V, true), Key(Native.VK_CONTROL, true));
            Thread.Sleep(400);
            Retry(() => { if (saved != null) Clipboard.SetDataObject(saved, true); else Clipboard.Clear(); });
        }

        static bool Retry(Action a)
        {
            for (int i = 0; i < 5; i++)
            {
                try { a(); return true; }
                catch (ExternalException) { Thread.Sleep(50); }
                catch { return false; }
            }
            return false;
        }

        static DataObject SaveClipboard()
        {
            try
            {
                var src = Clipboard.GetDataObject();
                if (src == null) return null;
                var copy = new DataObject();
                bool any = false;
                foreach (var f in src.GetFormats(false))
                {
                    try
                    {
                        var d = src.GetData(f, false);
                        if (d != null) { copy.SetData(f, false, d); any = true; }
                    }
                    catch { /* некоторые форматы не копируются — пропускаем */ }
                }
                return any ? copy : null;
            }
            catch { return null; }
        }
    }
}
