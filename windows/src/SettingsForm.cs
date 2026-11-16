using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace LangSwitch
{
    /// Окно настроек с вкладками: Основные / Исправления / О программе.
    internal sealed class SettingsForm : Form
    {
        bool _loading = true;

        public SettingsForm()
        {
            Text = "Настройки LangSwitch";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            Font = new Font("Segoe UI", 9.75f);
            ClientSize = new Size(500, 360);
            try { Icon = new Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico")); } catch { }

            var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(14, 5) };
            tabs.TabPages.Add(GeneralTab());
            tabs.TabPages.Add(FixesTab());
            tabs.TabPages.Add(AboutTab());
            Controls.Add(tabs);
            _loading = false;
        }

        // ---------------- элементы ----------------

        static FlowLayoutPanel Column()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false,
                Padding = new Padding(16, 14, 16, 10), AutoScroll = true,
            };
        }

        CheckBox Check(string text, string hint, bool value, Action<bool> set, FlowLayoutPanel parent)
        {
            var cb = new CheckBox { Text = text, Checked = value, AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
            cb.CheckedChanged += (s, e) => { if (_loading) return; set(cb.Checked); Settings.Save(); };
            parent.Controls.Add(cb);
            if (hint != null)
                parent.Controls.Add(new Label
                {
                    Text = hint, AutoSize = true, MaximumSize = new Size(440, 0), ForeColor = SystemColors.GrayText,
                    Margin = new Padding(18, 0, 0, 4),
                });
            return cb;
        }

        static Label Note(string text, FlowLayoutPanel parent, Color? color = null)
        {
            var l = new Label
            {
                Text = text, AutoSize = true, MaximumSize = new Size(450, 0), Margin = new Padding(0, 12, 0, 0),
                ForeColor = color ?? SystemColors.GrayText,
            };
            parent.Controls.Add(l);
            return l;
        }

        // ---------------- вкладки ----------------

        TabPage GeneralTab()
        {
            var page = new TabPage("Основные");
            var col = Column();

            Check("LangSwitch включён", null, Settings.Enabled, v => Settings.Enabled = v, col);
            var ds = Check("Двойной Shift: сменить язык / исправить слово / исправить выделение", null,
                Settings.DoubleShift, v => Settings.DoubleShift = v, col);

            var speedLabel = new Label { AutoSize = true, Margin = new Padding(18, 10, 0, 0) };
            var slider = new TrackBar
            {
                Minimum = 4, Maximum = 12, SmallChange = 1, LargeChange = 1, TickStyle = TickStyle.BottomRight,
                Value = Math.Max(4, Math.Min(12, (int)Math.Round(Settings.DoubleShiftInterval * 20))),
                Width = 300, Margin = new Padding(14, 0, 0, 0),
            };
            Action showSpeed = () => speedLabel.Text = $"Скорость двойного нажатия: {slider.Value * 50} мс  (влево — быстрее, вправо — медленнее)";
            showSpeed();
            slider.ValueChanged += (s, e) =>
            {
                showSpeed();
                if (_loading) return;
                Settings.DoubleShiftInterval = slider.Value / 20.0;
                Settings.Save();
            };
            ds.CheckedChanged += (s, e) => slider.Enabled = ds.Checked;
            slider.Enabled = ds.Checked;
            col.Controls.Add(speedLabel);
            col.Controls.Add(slider);

            bool autostart = false;
            try { autostart = Settings.LaunchAtLogin; } catch { }
            var cbAuto = new CheckBox { Text = "Запускать вместе с Windows", Checked = autostart, AutoSize = true, Margin = new Padding(0, 10, 0, 0) };
            cbAuto.CheckedChanged += (s, e) =>
            {
                if (_loading) return;
                try { Settings.LaunchAtLogin = cbAuto.Checked; }
                catch { MessageBox.Show("Не получилось изменить автозапуск.", "LangSwitch"); }
            };
            col.Controls.Add(cbAuto);

            if (InputSwitcher.HasBoth)
                Note("✓ Русская и английская раскладки найдены", col, Color.SeaGreen);
            else
                Note("⚠ Нужны две раскладки: русская и английская. Добавь их: Параметры → Время и язык → Язык и регион.", col, Color.DarkOrange);

            page.Controls.Add(col);
            return page;
        }

        TabPage FixesTab()
        {
            var page = new TabPage("Исправления");
            var col = Column();
            Check("Автопереключение раскладки",
                "«ghbdtn» + пробел → «привет». Ошибся — сразу двойной Shift, и всё вернётся.",
                Settings.AutoSwitch, v => Settings.AutoSwitch = v, col);
            Check("Исправлять регистр",
                "«ПРивет» → «Привет», «пРИВЕТ» → «Привет»",
                Settings.CaseFix, v => Settings.CaseFix = v, col);
            Check("Исправлять опечатки (бета)",
                "«превет» → «привет» по встроенному словарю. Может трогать сленг.",
                Settings.TypoFix, v => Settings.TypoFix = v, col);
            Note("Исправления срабатывают по пробелу. В полях паролей ничего не исправляется.", col);
            page.Controls.Add(col);
            return page;
        }

        TabPage AboutTab()
        {
            var page = new TabPage("О программе");
            var col = Column();
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            col.Controls.Add(new Label { Text = "RU⇄EN", AutoSize = true, Font = new Font("Segoe UI", 26f, FontStyle.Bold) });
            col.Controls.Add(new Label { Text = $"LangSwitch {v.Major}.{v.Minor}.{v.Build}", AutoSize = true, Font = new Font("Segoe UI", 13f) });
            Note("Бесплатный переключатель раскладки.\nНичего не отправляет в интернет и не сохраняет то, что ты печатаешь.", col);

            var link = new LinkLabel { Text = "Другие бесплатные программы — Telegram-канал «Без подписки»", AutoSize = true, Margin = new Padding(0, 14, 0, 0) };
            link.LinkClicked += (s, e) => Open("https://t.me/+CKC03YLzAaU3ZGI6");
            col.Controls.Add(link);
            var code = new LinkLabel { Text = "Исходный код на GitHub", AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
            code.LinkClicked += (s, e) => Open("https://github.com/PavelB-22/langswitch");
            col.Controls.Add(code);
            Note("Словари: FrequencyWords (Hermit Dave), CC BY-SA 4.0", col);
            page.Controls.Add(col);
            return page;
        }

        static void Open(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }
    }
}
