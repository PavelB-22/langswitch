import AppKit
import Carbon

/// Исправляет текст: последнее слово или выделение.
enum TextFixer {

    // MARK: выделенный текст (через Универсальный доступ)

    static func selectedText() -> String? {
        let system = AXUIElementCreateSystemWide()
        AXUIElementSetMessagingTimeout(system, 0.25)
        var focused: CFTypeRef?
        guard AXUIElementCopyAttributeValue(system, kAXFocusedUIElementAttribute as CFString, &focused) == .success,
              let focusedRef = focused, CFGetTypeID(focusedRef) == AXUIElementGetTypeID() else { return nil }
        let element = focusedRef as! AXUIElement
        AXUIElementSetMessagingTimeout(element, 0.25)
        var value: CFTypeRef?
        guard AXUIElementCopyAttributeValue(element, kAXSelectedTextAttribute as CFString, &value) == .success,
              let text = value as? String, !text.isEmpty else { return nil }
        return text
    }

    /// Заменяет выделение исправленным текстом через буфер обмена, потом возвращает буфер как был.
    static func fixSelection(_ text: String) {
        let (converted, target) = LayoutConverter.convert(text)
        let pb = NSPasteboard.general
        let saved: [[(NSPasteboard.PasteboardType, Data)]] = (pb.pasteboardItems ?? []).map { item in
            item.types.compactMap { t in item.data(forType: t).map { (t, $0) } }
        }
        pb.clearContents()
        pb.setString(converted, forType: .string)
        InputSwitcher.select(target.lang)
        postKey(9, flags: .maskCommand)   // Cmd+V

        DispatchQueue.main.asyncAfter(deadline: .now() + 0.4) {
            pb.clearContents()
            let items: [NSPasteboardItem] = saved.map { pairs in
                let item = NSPasteboardItem()
                for (t, d) in pairs { item.setData(d, forType: t) }
                return item
            }
            if !items.isEmpty { pb.writeObjects(items) }
        }
    }

    // MARK: последнее слово

    /// Стирает слово и печатает его в другой раскладке. Возвращает новое слово.
    static func fixWord(_ word: String) -> String {
        let (converted, target) = LayoutConverter.convert(word)
        for _ in 0..<word.count { postKey(51) }   // Backspace
        InputSwitcher.select(target.lang)
        typeText(converted)
        return converted
    }

    /// Стереть deleteCount символов, включить раскладку lang (если задана) и напечатать text.
    static func replace(deleteCount: Int, with text: String, lang: String?) {
        for _ in 0..<deleteCount { postKey(51) }
        if let l = lang { InputSwitcher.select(l) }
        typeText(text)
    }

    // MARK: отправка нажатий

    private static let source = CGEventSource(stateID: .combinedSessionState)

    private static func mark(_ e: CGEvent) {
        e.setIntegerValueField(.eventSourceUserData, value: KeyMonitor.ownEventMark)
    }

    static func postKey(_ code: CGKeyCode, flags: CGEventFlags = []) {
        guard let down = CGEvent(keyboardEventSource: source, virtualKey: code, keyDown: true),
              let up = CGEvent(keyboardEventSource: source, virtualKey: code, keyDown: false) else { return }
        down.flags = flags; up.flags = flags
        mark(down); mark(up)
        down.post(tap: .cghidEventTap)
        up.post(tap: .cghidEventTap)
        usleep(2000)
    }

    static func typeText(_ text: String) {
        for ch in text {
            var units = Array(String(ch).utf16)
            guard let down = CGEvent(keyboardEventSource: source, virtualKey: 0, keyDown: true),
                  let up = CGEvent(keyboardEventSource: source, virtualKey: 0, keyDown: false) else { continue }
            down.flags = []; up.flags = []
            down.keyboardSetUnicodeString(stringLength: units.count, unicodeString: &units)
            up.keyboardSetUnicodeString(stringLength: units.count, unicodeString: &units)
            mark(down); mark(up)
            down.post(tap: .cghidEventTap)
            up.post(tap: .cghidEventTap)
            usleep(2000)
        }
    }
}
