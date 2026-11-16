using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Threading;
using System.Windows.Forms;

namespace LangSwitch
{
    /// Значок в трее, меню и главная логика (аналог AppDelegate.swift на Mac).
    internal sealed class TrayApp : ApplicationContext
    {
        readonly NotifyIcon _tray;
        readonly KeyMonitor _monitor = new KeyMonitor();
        readonly System.Windows.Forms.Timer _timer;
        readonly ToolStripMenuItem _enabledItem;
        string _shownLabel;
        bool _shownEnabled;

        // Рабочий поток: печать, буфер обмена, UI Automation. Перехватчик клавиш его не ждёт.
        readonly BlockingCollection<Action> _jobs = new BlockingCollection<Action>();

        /// Последнее автоисправление — чтобы откатить его двойным Shift.
        sealed class LastAuto { public string Original, Fixed, Lang; public int Counter; }
        LastAuto _lastAuto;

        public TrayApp(bool firstRun)
        {
            var worker = new Thread(() => { foreach (var job in _jobs.GetConsumingEnumerable()) { try { job(); } catch { } } })
            { IsBackground = true, Name = "LangSwitch worker" };
            worker.SetApartmentState(ApartmentState.STA);   // нужно для буфера обмена
            worker.Start();

            var menu = new ContextMenuStrip();
            _enabledItem = new ToolStripMenuItem("Включено", null, (s, e) => { Settings.Enabled = !Settings.Enabled; Settings.Save(); });
            menu.Items.Add(_enabledItem);
            menu.Items.Add(new ToolStripMenuItem("Настройки…", null, (s, e) => ShowSettings()));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Выход", null, (s, e) => ExitThread()));
            menu.Opening += (s, e) => _enabledItem.Checked = Settings.Enabled;

            _tray = new NotifyIcon { ContextMenuStrip = menu, Visible = true, Text = "LangSwitch" };
            _tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowSettings(); };
            UpdateIcon(true);

            // Значок обновляем, когда меняется раскладка в активном окне (любым способом)
            _timer = new System.Windows.Forms.Timer { Interval = 250 };
            _timer.Tick += (s, e) => UpdateIcon(false);
            _timer.Start();
            Settings.Changed += () => UpdateIcon(true);

            _monitor.DoubleShift += () => _jobs.Add(HandleDoubleShift);
            _monitor.WordEnd = HandleWordEnd;
            if (!_monitor.Start())
                MessageBox.Show("Не получилось включить перехват клавиатуры. Перезапусти LangSwitch.", "LangSwitch");

            if (firstRun)
                _tray.ShowBalloonTip(5000, "LangSwitch работает",
                    "Двойной Shift — сменить язык или исправить слово. «ghbdtn» + пробел само станет «привет».", ToolTipIcon.Info);
        }

        // ---------------- автоисправление по пробелу ----------------

        /// Вызывается прямо в перехватчике в момент нажатия пробела. true — пробел съедаем и делаем замену.
        bool HandleWordEnd(string word)
        {
            var fix = Corrector.Analyze(word);
            if (fix == null) return false;
            int counter;
            lock (_monitor.Lock) counter = _monitor.InputCounter;
            var auto = new LastAuto { Original = word, Fixed = fix.Text, Lang = InputSwitcher.CurrentLanguage(), Counter = counter };
            _jobs.Add(() =>
            {
                if (TextFixer.InPasswordField()) { _lastAuto = null; TextFixer.Type(" "); return; }
                _lastAuto = auto;
                TextFixer.Replace(word.Length, fix.Text + " ", fix.Lang);
            });
            return true;
        }

        // ---------------- двойной Shift ----------------

        void HandleDoubleShift()
        {
            Thread.Sleep(30);   // чтобы Shift точно был отпущен перед нашими нажатиями
            if (TextFixer.InPasswordField()) { _monitor.ResetWord(); InputSwitcher.Toggle(); return; }

            string word, prev;
            int counter, prevCounter;
            lock (_monitor.Lock)
            {
                word = _monitor.CurrentWord;
                counter = _monitor.InputCounter;
                prev = _monitor.WordBeforeSpace;
                prevCounter = _monitor.WordBeforeSpaceCounter;
            }
            var last = _lastAuto;

            if (word.Length > 0)
            {
                // Только что набранное слово — перекодировать
                _lastAuto = null;
                var conv = TextFixer.FixWord(word);
                lock (_monitor.Lock) _monitor.CurrentWord = conv;
            }
            else if (last != null && last.Counter == counter)
            {
                // Откат автоисправления: вернуть как было и больше это слово не трогать
                _lastAuto = null;
                Corrector.Ignored.Add(last.Original);
                TextFixer.Replace(last.Fixed.Length + 1, last.Original + " ", last.Lang);
            }
            else if (prev != null && prevCounter == counter && Corrector.LooksWrong(prev))
            {
                // Слово + пробел не исправилось само — исправляем по двойному Shift
                var (conv, lang) = LayoutConverter.Convert(prev);
                lock (_monitor.Lock) _monitor.WordBeforeSpace = conv;
                TextFixer.Replace(prev.Length + 1, conv + " ", lang);
            }
            else
            {
                _lastAuto = null;
                var sel = TextFixer.SelectedText();
                if (sel != null) { _monitor.ResetWord(); TextFixer.FixSelection(sel); }
                else InputSwitcher.Toggle();
            }
        }

        // ---------------- значок ----------------

        void UpdateIcon(bool force)
        {
            var label = InputSwitcher.CurrentLabel();
            if (label == "??" && _shownLabel != null) label = _shownLabel;   // окно без раскладки (рабочий стол и т.п.)
            if (!force && label == _shownLabel && Settings.Enabled == _shownEnabled) return;
            _shownLabel = label;
            _shownEnabled = Settings.Enabled;
            var old = _tray.Icon;
            _tray.Icon = MakeIcon(label, Settings.Enabled);
            _tray.Text = "LangSwitch — " + label + (Settings.Enabled ? "" : " (выключен)");
            if (old != null) { Native.DestroyIcon(old.Handle); old.Dispose(); }
        }

        static Icon MakeIcon(string label, bool enabled)
        {
            int size = SystemInformation.SmallIconSize.Width;
            if (size < 16) size = 16;
            using (var bmp = new Bitmap(size, size))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    g.Clear(Color.Transparent);
                    var bg = !enabled ? Color.FromArgb(120, 120, 120)
                           : label == "RU" ? Color.FromArgb(255, 79, 139)
                           : Color.FromArgb(123, 92, 255);
                    using (var path = RoundRect(new RectangleF(0, 0, size - 1, size - 1), size * 0.22f))
                    using (var b = new SolidBrush(bg)) g.FillPath(b, path);
                    using (var font = new Font("Segoe UI", size * 0.42f, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString(label.Length > 2 ? label.Substring(0, 2) : label, font, Brushes.White, new RectangleF(0, 0, size, size + 1), sf);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        static GraphicsPath RoundRect(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // ---------------- окно настроек ----------------

        SettingsForm _settings;

        public void ShowSettings()
        {
            if (_settings == null || _settings.IsDisposed) _settings = new SettingsForm();
            _settings.Show();
            if (_settings.WindowState == FormWindowState.Minimized) _settings.WindowState = FormWindowState.Normal;
            _settings.Activate();
        }

        protected override void ExitThreadCore()
        {
            _timer.Stop();
            _monitor.Dispose();
            _jobs.CompleteAdding();
            _tray.Visible = false;
            _tray.Dispose();
            base.ExitThreadCore();
        }
    }
}
