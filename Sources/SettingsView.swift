import SwiftUI
import AppKit
import ServiceManagement

/// Окно настроек с вкладками.
struct SettingsView: View {
    var body: some View {
        TabView {
            GeneralTab().tabItem { Label("Основные", systemImage: "gearshape") }
            FixesTab().tabItem { Label("Исправления", systemImage: "wand.and.stars") }
            AboutTab().tabItem { Label("О программе", systemImage: "info.circle") }
        }
        .frame(width: 460, height: 330)
        .padding()
    }
}

private struct GeneralTab: View {
    @AppStorage(Settings.Key.enabled) private var enabled = true
    @AppStorage(Settings.Key.doubleShift) private var doubleShift = true
    @AppStorage(Settings.Key.doubleShiftInterval) private var interval = 0.3
    @State private var launchAtLogin = SMAppService.mainApp.status == .enabled
    @State private var trusted = AXIsProcessTrusted()
    @State private var loginError: String?

    var body: some View {
        Form {
            Toggle("LangSwitch включён", isOn: $enabled)

            Section {
                Toggle("Двойной Shift: сменить язык / исправить слово / исправить выделение", isOn: $doubleShift)
                VStack(alignment: .leading, spacing: 4) {
                    Text("Скорость двойного нажатия: \(Int(interval * 1000)) мс")
                    Slider(value: $interval, in: 0.2...0.6, step: 0.05) {
                        EmptyView()
                    } minimumValueLabel: { Text("быстро").font(.caption) }
                      maximumValueLabel: { Text("медленно").font(.caption) }
                }
                .disabled(!doubleShift)
            }

            Section {
                Toggle("Запускать при входе в систему", isOn: Binding(
                    get: { launchAtLogin },
                    set: { newValue in
                        do {
                            if newValue { try SMAppService.mainApp.register() }
                            else { try SMAppService.mainApp.unregister() }
                            loginError = nil
                        } catch {
                            loginError = "Не вышло. Положи LangSwitch в «Программы»."
                        }
                        launchAtLogin = SMAppService.mainApp.status == .enabled
                    }))
                if let e = loginError { Text(e).font(.caption).foregroundColor(.red) }

                HStack {
                    Image(systemName: trusted ? "checkmark.circle.fill" : "exclamationmark.triangle.fill")
                        .foregroundColor(trusted ? .green : .orange)
                    Text(trusted ? "Доступ «Универсальный доступ» выдан" : "Нет доступа «Универсальный доступ»")
                    Spacer()
                    if !trusted {
                        Button("Открыть настройки") {
                            if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility") {
                                NSWorkspace.shared.open(url)
                            }
                        }
                    }
                }
            }
        }
        .formStyle(.grouped)
        .onAppear { trusted = AXIsProcessTrusted() }
    }
}

private struct FixesTab: View {
    @AppStorage(Settings.Key.autoSwitch) private var autoSwitch = true
    @AppStorage(Settings.Key.caseFix) private var caseFix = true
    @AppStorage(Settings.Key.typoFix) private var typoFix = false

    var body: some View {
        Form {
            Section {
                Toggle(isOn: $autoSwitch) {
                    VStack(alignment: .leading) {
                        Text("Автопереключение раскладки")
                        Text("«ghbdtn» + пробел → «привет». Ошибся — сразу двойной Shift, и всё вернётся.")
                            .font(.caption).foregroundColor(.secondary)
                    }
                }
                Toggle(isOn: $caseFix) {
                    VStack(alignment: .leading) {
                        Text("Исправлять регистр")
                        Text("«ПРивет» → «Привет», «пРИВЕТ» → «Привет»")
                            .font(.caption).foregroundColor(.secondary)
                    }
                }
                Toggle(isOn: $typoFix) {
                    VStack(alignment: .leading) {
                        Text("Исправлять опечатки (бета)")
                        Text("«превет» → «привет» по словарю macOS. Может трогать сленг.")
                            .font(.caption).foregroundColor(.secondary)
                    }
                }
            }
            Section {
                Text("Исправления срабатывают по пробелу. В полях паролей ничего не исправляется.")
                    .font(.caption).foregroundColor(.secondary)
            }
        }
        .formStyle(.grouped)
    }
}

private struct AboutTab: View {
    var body: some View {
        VStack(spacing: 10) {
            Text("RU⇄EN").font(.system(size: 34, weight: .bold, design: .rounded))
            Text("LangSwitch 2.0").font(.title2)
            Text("Бесплатный переключатель раскладки.\nНичего не отправляет в интернет и не сохраняет то, что ты печатаешь.")
                .multilineTextAlignment(.center)
                .foregroundColor(.secondary)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

/// Показывает окно настроек (одно на всё приложение).
final class SettingsWindowController {
    static let shared = SettingsWindowController()
    private var window: NSWindow?

    func show() {
        if window == nil {
            let w = NSWindow(contentViewController: NSHostingController(rootView: SettingsView()))
            w.title = "Настройки LangSwitch"
            w.styleMask = [.titled, .closable, .miniaturizable]
            w.isReleasedWhenClosed = false
            w.center()
            window = w
        }
        NSApp.activate(ignoringOtherApps: true)
        window?.makeKeyAndOrderFront(nil)
    }
}
