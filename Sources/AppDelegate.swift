import AppKit
import Carbon
import ServiceManagement

final class AppDelegate: NSObject, NSApplicationDelegate, NSMenuDelegate {
    private var statusItem: NSStatusItem!
    private let monitor = KeyMonitor()
    private var permissionTimer: Timer?

    private var enabledItem: NSMenuItem!
    private var permissionItem: NSMenuItem!

    /// Последнее автоисправление — чтобы откатить его двойным Shift.
    private struct LastAuto { let original: String; let fixed: String; let lang: String?; let counter: Int }
    private var lastAuto: LastAuto?

    func applicationDidFinishLaunching(_ notification: Notification) {
        Settings.registerDefaults()
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        buildMenu()
        updateTitle()

        // Обновляем значок при любой смене раскладки и при изменении настроек
        DistributedNotificationCenter.default().addObserver(
            forName: NSNotification.Name(kTISNotifySelectedKeyboardInputSourceChanged as String),
            object: nil, queue: .main) { [weak self] _ in self?.updateTitle() }
        NotificationCenter.default.addObserver(
            forName: UserDefaults.didChangeNotification, object: nil, queue: .main) { [weak self] _ in self?.updateTitle() }

        monitor.onDoubleShift = { [weak self] in self?.handleDoubleShift() }
        monitor.onWordEnd = { [weak self] word in self?.handleWordEnd(word) ?? false }
        startWhenTrusted()
    }

    /// Повторный запуск (двойной клик по LangSwitch в «Программах» или через Spotlight) — открываем настройки.
    /// Пригодится, если значок в строке меню спрятался за «чёлкой» экрана.
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        SettingsWindowController.shared.show()
        return true
    }

    // MARK: разрешение «Универсальный доступ»

    private func isTrusted(prompt: Bool) -> Bool {
        let key = kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String
        return AXIsProcessTrustedWithOptions([key: prompt] as CFDictionary)
    }

    private func startWhenTrusted() {
        if isTrusted(prompt: true) && monitor.start() {
            permissionItem.isHidden = true
            updateTitle()
            return
        }
        permissionItem.isHidden = false
        updateTitle()
        permissionTimer?.invalidate()
        permissionTimer = Timer.scheduledTimer(withTimeInterval: 2, repeats: true) { [weak self] t in
            guard let self = self else { return }
            if self.isTrusted(prompt: false) && self.monitor.start() {
                t.invalidate()
                self.permissionItem.isHidden = true
                self.updateTitle()
            }
        }
    }

    // MARK: автоисправление по пробелу

    /// Вызывается прямо в момент нажатия пробела. true — пробел съедаем и делаем замену.
    private func handleWordEnd(_ word: String) -> Bool {
        guard let fix = Corrector.analyze(word) else { return false }
        let langBefore = InputSwitcher.currentLanguage()
        lastAuto = LastAuto(original: word, fixed: fix.text, lang: langBefore, counter: monitor.inputCounter)
        DispatchQueue.main.async {
            TextFixer.replace(deleteCount: word.count, with: fix.text + " ", lang: fix.lang)
        }
        return true
    }

    // MARK: двойной Shift

    private func handleDoubleShift() {
        // В поле пароля ничего не исправляем — только меняем язык
        if IsSecureEventInputEnabled() {
            monitor.resetWord()
            InputSwitcher.toggle()
            return
        }
        // Небольшая пауза, чтобы Shift точно был отпущен перед нашими нажатиями
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.03) { [self] in
            if let selection = TextFixer.selectedText() {
                monitor.resetWord()
                lastAuto = nil
                TextFixer.fixSelection(selection)
            } else if !monitor.currentWord.isEmpty {
                lastAuto = nil
                monitor.currentWord = TextFixer.fixWord(monitor.currentWord)
            } else if let last = lastAuto, last.counter == monitor.inputCounter {
                // Откат автоисправления: вернуть как было и больше это слово не трогать
                lastAuto = nil
                Corrector.ignored.insert(last.original)
                TextFixer.replace(deleteCount: last.fixed.count + 1, with: last.original + " ", lang: last.lang)
            } else if let prev = monitor.wordBeforeSpace, prev.counter == monitor.inputCounter,
                      Corrector.looksWrong(prev.word) {
                // Слово + пробел не исправилось само — исправляем по двойному Shift
                let (converted, target) = LayoutConverter.convert(prev.word)
                monitor.wordBeforeSpace = (converted, prev.counter)
                TextFixer.replace(deleteCount: prev.word.count + 1, with: converted + " ", lang: target.lang)
            } else {
                lastAuto = nil
                InputSwitcher.toggle()
            }
        }
    }

    // MARK: меню

    private func buildMenu() {
        let menu = NSMenu()
        menu.delegate = self
        permissionItem = NSMenuItem(title: "⚠︎ Дать доступ в «Универсальный доступ»…", action: #selector(openPermissions), keyEquivalent: "")
        permissionItem.target = self
        menu.addItem(permissionItem)

        enabledItem = NSMenuItem(title: "Включено", action: #selector(toggleEnabled), keyEquivalent: "")
        enabledItem.target = self
        menu.addItem(enabledItem)

        let settings = NSMenuItem(title: "Настройки…", action: #selector(openSettings), keyEquivalent: ",")
        settings.target = self
        menu.addItem(settings)

        menu.addItem(.separator())
        menu.addItem(NSMenuItem(title: "Выход", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q"))
        statusItem.menu = menu
    }

    func menuWillOpen(_ menu: NSMenu) {
        enabledItem.state = Settings.enabled ? .on : .off
    }

    private func updateTitle() {
        guard let button = statusItem.button else { return }
        if !permissionItem.isHidden { button.title = "⚠︎"; return }
        button.title = InputSwitcher.currentLabel()
        button.alphaValue = Settings.enabled ? 1.0 : 0.4
    }

    @objc private func openSettings() {
        SettingsWindowController.shared.show()
    }

    @objc private func openPermissions() {
        _ = isTrusted(prompt: true)
        if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility") {
            NSWorkspace.shared.open(url)
        }
    }

    @objc private func toggleEnabled() {
        Settings.enabled.toggle()
        updateTitle()
    }
}
