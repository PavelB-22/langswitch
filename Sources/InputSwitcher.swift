import Carbon

/// Работа с раскладками: какая сейчас включена, переключение RU ↔ EN.
enum InputSwitcher {
    static func keyboardSources() -> [TISInputSource] {
        let filter = [
            kTISPropertyInputSourceCategory as String: kTISCategoryKeyboardInputSource as String,
            kTISPropertyInputSourceIsSelectCapable as String: true
        ] as CFDictionary
        guard let list = TISCreateInputSourceList(filter, false)?.takeRetainedValue() as? [TISInputSource] else { return [] }
        return list
    }

    static func language(of source: TISInputSource) -> String? {
        guard let ptr = TISGetInputSourceProperty(source, kTISPropertyInputSourceLanguages) else { return nil }
        let langs = Unmanaged<CFArray>.fromOpaque(ptr).takeUnretainedValue() as? [String]
        return langs?.first
    }

    static func currentLanguage() -> String? {
        let cur = TISCopyCurrentKeyboardInputSource().takeRetainedValue()
        return language(of: cur)
    }

    static func source(for lang: String) -> TISInputSource? {
        keyboardSources().first { language(of: $0) == lang }
    }

    @discardableResult
    static func select(_ lang: String) -> Bool {
        guard let s = source(for: lang) else { return false }
        return TISSelectInputSource(s) == noErr
    }

    /// RU → EN, всё остальное → RU. Другие раскладки не трогаем.
    static func toggle() {
        if currentLanguage() == "ru" { select("en") } else { select("ru") }
    }

    /// Короткая подпись для строки меню.
    static func currentLabel() -> String {
        switch currentLanguage() {
        case "ru": return "RU"
        case "en": return "EN"
        case let l?: return l.uppercased()
        default: return "??"
        }
    }
}
