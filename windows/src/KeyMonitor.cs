using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace LangSwitch
{
    /// Слушает клавиатуру и мышь во всех программах: ловит двойной Shift и помнит последнее слово.
    /// Работает в главном потоке и должна отвечать мгновенно, иначе Windows отключит перехватчик.
    internal sealed class KeyMonitor : IDisposable
    {
        /// Метка, которой мы помечаем свои нажатия, чтобы не реагировать на них.
        public static readonly UIntPtr OwnMark = new UIntPtr(0x4C535754);   // "LSWT"

        public event Action DoubleShift;
        /// Нажали пробел после слова. Вернуть true — пробел «съедаем» (будет замена).
        public Func<string, bool> WordEnd;

        public readonly object Lock = new object();
        /// Счётчик настоящих нажатий и кликов — чтобы понять, что после исправления ничего не набирали.
        public int InputCounter;
        public string CurrentWord = "";
        /// Слово, после которого только что нажали пробел.
        public string WordBeforeSpace;
        public int WordBeforeSpaceCounter = -1;

        readonly Native.HookProc _kbProc, _mouseProc;   // держим ссылки, чтобы сборщик мусора их не удалил
        IntPtr _kbHook, _mouseHook;

        readonly Stopwatch _clock = Stopwatch.StartNew();
        bool _shiftDown, _shiftSpoiled;
        double _shiftDownAt, _lastCleanShiftUp;
        IntPtr _lastWindow;
        readonly byte[] _keyState = new byte[256];
        readonly StringBuilder _buf = new StringBuilder(8);

        public KeyMonitor()
        {
            _kbProc = KeyboardProc;
            _mouseProc = MouseProc;
        }

        public bool Start()
        {
            var mod = Native.GetModuleHandle(null);
            _kbHook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _kbProc, mod, 0);
            _mouseHook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _mouseProc, mod, 0);
            return _kbHook != IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_kbHook != IntPtr.Zero) Native.UnhookWindowsHookEx(_kbHook);
            if (_mouseHook != IntPtr.Zero) Native.UnhookWindowsHookEx(_mouseHook);
            _kbHook = _mouseHook = IntPtr.Zero;
        }

        public void ResetWord() { lock (Lock) CurrentWord = ""; }

        double Now => _clock.Elapsed.TotalSeconds;
        static bool Down(int vk) => Native.GetAsyncKeyState(vk) < 0;
        static bool IsShift(uint vk) => vk == Native.VK_SHIFT || vk == Native.VK_LSHIFT || vk == Native.VK_RSHIFT;
        static bool IsOtherModifier(uint vk) =>
            vk == Native.VK_CONTROL || vk == Native.VK_LCONTROL || vk == Native.VK_RCONTROL ||
            vk == Native.VK_MENU || vk == Native.VK_LMENU || vk == Native.VK_RMENU ||
            vk == Native.VK_LWIN || vk == Native.VK_RWIN || vk == Native.VK_CAPITAL;

        // ---------------- клавиатура ----------------

        IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                bool eat = false;
                try { eat = HandleKey((int)wParam, Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam)); }
                catch { /* никогда не роняем ввод */ }
                if (eat) return (IntPtr)1;
            }
            return Native.CallNextHookEx(_kbHook, nCode, wParam, lParam);
        }

        bool HandleKey(int msg, Native.KBDLLHOOKSTRUCT k)
        {
            if (k.dwExtraInfo == OwnMark) return false;          // наше собственное нажатие
            if (!Settings.Enabled) { ResetWord(); return false; }

            bool keyDown = msg == Native.WM_KEYDOWN || msg == Native.WM_SYSKEYDOWN;
            bool keyUp = msg == Native.WM_KEYUP || msg == Native.WM_SYSKEYUP;
            uint vk = k.vkCode;
            double now = Now;

            // Переключились на другое окно — слово забываем
            var fg = Native.GetForegroundWindow();
            if (fg != _lastWindow) { _lastWindow = fg; ResetWord(); }

            if (IsShift(vk))
            {
                if (keyDown && !_shiftDown)
                {
                    _shiftDown = true;
                    _shiftDownAt = now;
                    _shiftSpoiled = Down(Native.VK_CONTROL) || Down(Native.VK_MENU) || Down(Native.VK_LWIN) || Down(Native.VK_RWIN);
                }
                else if (keyUp && _shiftDown)
                {
                    _shiftDown = false;
                    double interval = Settings.DoubleShiftInterval;
                    bool clean = !_shiftSpoiled && (now - _shiftDownAt) < interval;
                    if (!clean) { _lastCleanShiftUp = 0; return false; }
                    if (_lastCleanShiftUp > 0 && (now - _lastCleanShiftUp) <= interval)
                    {
                        _lastCleanShiftUp = 0;
                        if (Settings.DoubleShift) DoubleShift?.Invoke();
                    }
                    else _lastCleanShiftUp = now;
                }
                return false;
            }

            if (IsOtherModifier(vk))
            {
                if (keyDown) { _lastCleanShiftUp = 0; if (_shiftDown) _shiftSpoiled = true; }
                return false;
            }

            if (!keyDown) return false;

            lock (Lock) InputCounter++;
            _lastCleanShiftUp = 0;
            if (_shiftDown) _shiftSpoiled = true;
            return TrackWord(vk, k.scanCode, fg);
        }

        bool TrackWord(uint vk, uint scan, IntPtr fg)
        {
            bool ctrl = Down(Native.VK_CONTROL), alt = Down(Native.VK_MENU), win = Down(Native.VK_LWIN) || Down(Native.VK_RWIN);
            if (ctrl || alt || win) { ResetWord(); return false; }

            switch (vk)
            {
                case Native.VK_BACK:
                    lock (Lock) if (CurrentWord.Length > 0) CurrentWord = CurrentWord.Substring(0, CurrentWord.Length - 1);
                    return false;
                case Native.VK_SPACE:
                    {
                        string word;
                        int counter;
                        lock (Lock) { word = CurrentWord; CurrentWord = ""; counter = InputCounter; }
                        if (word.Length > 0 && WordEnd != null && WordEnd(word)) return true;
                        lock (Lock)
                        {
                            WordBeforeSpace = word.Length > 0 ? word : null;
                            WordBeforeSpaceCounter = counter;
                        }
                        return false;
                    }
                case Native.VK_RETURN: case Native.VK_TAB: case Native.VK_ESCAPE: case Native.VK_DELETE:
                case Native.VK_LEFT: case Native.VK_RIGHT: case Native.VK_UP: case Native.VK_DOWN:
                case Native.VK_HOME: case Native.VK_END: case Native.VK_PRIOR: case Native.VK_NEXT: case Native.VK_INSERT:
                    ResetWord();
                    return false;
            }

            // Какой символ даст эта клавиша в раскладке активного окна
            uint tid = Native.GetWindowThreadProcessId(fg, out _);
            var hkl = Native.GetKeyboardLayout(tid);
            Array.Clear(_keyState, 0, 256);
            if (Down(Native.VK_SHIFT)) _keyState[Native.VK_SHIFT] = 0x80;
            if ((Native.GetKeyState(Native.VK_CAPITAL) & 1) != 0) _keyState[Native.VK_CAPITAL] = 0x01;
            _buf.Clear();
            int n = Native.ToUnicodeEx(vk, scan, _keyState, _buf, 8, 0x4, hkl);   // 0x4 — не трогать состояние клавиатуры
            if (n <= 0) { ResetWord(); return false; }
            string s = _buf.ToString(0, Math.Min(n, _buf.Length));
            foreach (var c in s) if (char.IsWhiteSpace(c) || char.IsControl(c)) { ResetWord(); return false; }

            lock (Lock)
            {
                CurrentWord += s;
                if (CurrentWord.Length > 64) CurrentWord = CurrentWord.Substring(CurrentWord.Length - 64);
            }
            return false;
        }

        // ---------------- мышь ----------------

        IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int m = (int)wParam;
                if (m == Native.WM_LBUTTONDOWN || m == Native.WM_RBUTTONDOWN || m == Native.WM_MBUTTONDOWN || m == Native.WM_XBUTTONDOWN)
                {
                    lock (Lock) { InputCounter++; CurrentWord = ""; }
                    _lastCleanShiftUp = 0;
                    if (_shiftDown) _shiftSpoiled = true;   // Shift+клик — это выделение, а не жест
                }
            }
            return Native.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }
    }
}
