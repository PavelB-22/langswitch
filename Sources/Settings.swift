import Foundation

/// Все настройки программы. Хранятся в UserDefaults, меняются из окна настроек.
enum Settings {
    enum Key {
        static let enabled = "enabled"
        static let doubleShift = "doubleShiftEnabled"
        static let doubleShiftInterval = "doubleShiftInterval"
        static let autoSwitch = "autoSwitchEnabled"
        static let caseFix = "caseFixEnabled"
        static let typoFix = "typoFixEnabled"
    }

    static func registerDefaults() {
        UserDefaults.standard.register(defaults: [
            Key.enabled: true,
            Key.doubleShift: true,
            Key.doubleShiftInterval: 0.3,
            Key.autoSwitch: true,
            Key.caseFix: true,
            Key.typoFix: false,
        ])
    }

    private static var d: UserDefaults { .standard }

    static var enabled: Bool {
        get { d.bool(forKey: Key.enabled) }
        set { d.set(newValue, forKey: Key.enabled) }
    }
    static var doubleShift: Bool { d.bool(forKey: Key.doubleShift) }
    static var doubleShiftInterval: Double {
        let v = d.double(forKey: Key.doubleShiftInterval)
        return v > 0 ? v : 0.3
    }
    static var autoSwitch: Bool { d.bool(forKey: Key.autoSwitch) }
    static var caseFix: Bool { d.bool(forKey: Key.caseFix) }
    static var typoFix: Bool { d.bool(forKey: Key.typoFix) }
}
