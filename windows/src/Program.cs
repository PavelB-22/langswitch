using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace LangSwitch
{
    internal static class Program
    {
        static readonly uint ShowSettingsMsg = Native.RegisterWindowMessage("LangSwitch.ShowSettings");

        [STAThread]
        static void Main()
        {
            using (var mutex = new Mutex(true, @"Local\LangSwitch.SingleInstance", out bool first))
            {
                if (!first)
                {
                    // Уже запущен — просим первую копию открыть настройки
                    Native.PostMessage(Native.HWND_BROADCAST, (int)ShowSettingsMsg, IntPtr.Zero, IntPtr.Zero);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                bool firstRun = !File.Exists(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LangSwitch", "settings.ini"));
                Settings.Load();
                if (firstRun)
                {
                    try { Settings.LaunchAtLogin = true; } catch { }
                    Settings.Save();
                }

                try { WordFreq.LoadAll(); }
                catch { MessageBox.Show("Не удалось загрузить словари — автоисправление отключено.", "LangSwitch"); }

                var app = new TrayApp(firstRun);
                using (new MessageWindow(ShowSettingsMsg, app.ShowSettings))
                    Application.Run(app);
                GC.KeepAlive(mutex);
            }
        }

        /// Невидимое окно, которое слушает «открой настройки» от второго запуска.
        sealed class MessageWindow : NativeWindow, IDisposable
        {
            readonly uint _msg;
            readonly Action _onShow;

            public MessageWindow(uint msg, Action onShow)
            {
                _msg = msg;
                _onShow = onShow;
                CreateHandle(new CreateParams { Caption = "LangSwitch.MessageWindow" });
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == (int)_msg) _onShow();
                base.WndProc(ref m);
            }

            public void Dispose() => DestroyHandle();
        }
    }
}
