import AppKit
import Carbon

/// Слушает клавиатуру во всех программах: ловит двойной Shift и помнит последнее набранное слово.
final class KeyMonitor {
    /// Метка, которой мы помечаем свои собственные нажатия, чтобы не реагировать на них.
    static let ownEventMark: Int64 = 0x4C53_5754   // "LSWT"

    var onDoubleShift: (() -> Void)?
    /// Нажали пробел после слова. Вернуть true — пробел «съедаем» (будет замена).
    var onWordEnd: ((String) -> Bool)?

    /// Счётчик настоящих нажатий и кликов — чтобы понять, что после автоисправления ничего не набирали.
    private(set) var inputCounter = 0

    /// Последнее слово, набранное сразу перед текущим моментом.
    var currentWord = ""
    /// Слово, после которого только что нажали пробел (и больше ничего не нажимали).
    var wordBeforeSpace: (word: String, counter: Int)?

    private var tap: CFMachPort?
    private var interval: TimeInterval { Settings.doubleShiftInterval }

    private var shiftIsDown = false
    private var shiftDownAt: TimeInterval = 0
    private var shiftSpoiled = false          // во время Shift нажали что-то ещё
    private var lastCleanShiftUp: TimeInterval = 0

    // MARK: запуск

    func start() -> Bool {
        if tap != nil { return true }
        let types: [CGEventType] = [.keyDown, .flagsChanged, .leftMouseDown, .rightMouseDown, .otherMouseDown]
        let mask = types.reduce(CGEventMask(0)) { $0 | (CGEventMask(1) << $1.rawValue) }

        let callback: CGEventTapCallBack = { _, type, event, refcon in
            if let refcon = refcon {
                let me = Unmanaged<KeyMonitor>.fromOpaque(refcon).takeUnretainedValue()
                if me.handle(type: type, event: event) { return nil }   // событие «съели»
            }
            return Unmanaged.passUnretained(event)   // остальное пропускаем дальше
        }

        guard let tap = CGEvent.tapCreate(tap: .cgSessionEventTap,
                                          place: .headInsertEventTap,
                                          options: .defaultTap,
                                          eventsOfInterest: mask,
                                          callback: callback,
                                          userInfo: Unmanaged.passUnretained(self).toOpaque()) else { return false }
        self.tap = tap
        let src = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, tap, 0)
        CFRunLoopAddSource(CFRunLoopGetMain(), src, .commonModes)
        CGEvent.tapEnable(tap: tap, enable: true)

        // При переключении на другую программу слово забываем
        NSWorkspace.shared.notificationCenter.addObserver(
            forName: NSWorkspace.didActivateApplicationNotification, object: nil, queue: .main) { [weak self] _ in
            self?.resetWord()
        }
        return true
    }

    func resetWord() { currentWord = "" }

    // MARK: обработка событий

    /// Возвращает true, если событие нужно «съесть».
    private func handle(type: CGEventType, event: CGEvent) -> Bool {
        // macOS иногда отключает перехватчик, если он «задумался» — включаем обратно
        if type == .tapDisabledByTimeout || type == .tapDisabledByUserInput {
            if let tap = tap { CGEvent.tapEnable(tap: tap, enable: true) }
            return false
        }
        if event.getIntegerValueField(.eventSourceUserData) == KeyMonitor.ownEventMark { return false }
        if !Settings.enabled { resetWord(); return false }

        let now = ProcessInfo.processInfo.systemUptime

        switch type {
        case .flagsChanged:
            handleFlags(event: event, now: now)
        case .keyDown:
            inputCounter += 1
            lastCleanShiftUp = 0
            if shiftIsDown { shiftSpoiled = true }
            return trackWord(event: event)
        case .leftMouseDown, .rightMouseDown, .otherMouseDown:
            inputCounter += 1
            lastCleanShiftUp = 0
            resetWord()
        default:
            break
        }
        return false
    }

    private func handleFlags(event: CGEvent, now: TimeInterval) {
        let code = event.getIntegerValueField(.keyboardEventKeycode)
        let isShiftKey = (code == 56 || code == 60)   // левый и правый Shift
        let flags = event.flags

        guard isShiftKey else {
            // нажали другой модификатор (Cmd, Option, Ctrl, Fn...) — это не наш жест
            lastCleanShiftUp = 0
            if shiftIsDown { shiftSpoiled = true }
            return
        }

        let otherMods: CGEventFlags = [.maskCommand, .maskAlternate, .maskControl]
        if flags.contains(.maskShift) && !shiftIsDown {
            // Shift нажат
            shiftIsDown = true
            shiftDownAt = now
            shiftSpoiled = !flags.intersection(otherMods).isEmpty
        } else if !flags.contains(.maskShift) && shiftIsDown {
            // Shift отпущен
            shiftIsDown = false
            let clean = !shiftSpoiled && (now - shiftDownAt) < interval
            if !clean { lastCleanShiftUp = 0; return }
            if lastCleanShiftUp > 0 && (now - lastCleanShiftUp) <= interval {
                lastCleanShiftUp = 0
                if Settings.doubleShift { DispatchQueue.main.async { self.onDoubleShift?() } }
            } else {
                lastCleanShiftUp = now
            }
        }
    }

    private func trackWord(event: CGEvent) -> Bool {
        let flags = event.flags
        if !flags.intersection([.maskCommand, .maskControl, .maskAlternate]).isEmpty { resetWord(); return false }

        let code = event.getIntegerValueField(.keyboardEventKeycode)
        switch code {
        case 51:                                   // Backspace
            if !currentWord.isEmpty { currentWord.removeLast() }
            return false
        case 49:                                   // пробел — проверяем слово
            let word = currentWord
            resetWord()
            if !word.isEmpty, !IsSecureEventInputEnabled(), let cb = onWordEnd, cb(word) { return true }
            wordBeforeSpace = word.isEmpty ? nil : (word, inputCounter)
            return false
        case 36, 76, 48, 53, 117, 115, 119, 116, 121, 123, 124, 125, 126:
            resetWord()                            // Enter, Tab, Esc, Delete, стрелки, Home/End/PgUp/PgDn
            return false
        default: break
        }

        var len = 0
        var buf = [UniChar](repeating: 0, count: 8)
        event.keyboardGetUnicodeString(maxStringLength: 8, actualStringLength: &len, unicodeString: &buf)
        let s = String(utf16CodeUnits: buf, count: len)
        if s.isEmpty || s.unicodeScalars.contains(where: { CharacterSet.whitespacesAndNewlines.contains($0) || CharacterSet.controlCharacters.contains($0) }) {
            resetWord()
            return false
        }
        currentWord += s
        if currentWord.count > 64 { currentWord = String(currentWord.suffix(64)) }
        return false
    }
}
