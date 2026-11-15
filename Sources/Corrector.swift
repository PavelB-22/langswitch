import AppKit

/// Решает, нужно ли исправлять набранное слово, и во что.
enum Corrector {
    struct Fix {
        let text: String      // на что заменить слово
        let lang: String?     // какую раскладку включить после (nil — не менять)
    }

    /// Слова, которые пользователь откатил двойным Shift — до перезапуска их не трогаем.
    static var ignored = Set<String>()

    private static let checker = NSSpellChecker.shared
    private static func dictLang(_ prefix: String) -> String? {
        let all = checker.availableLanguages
        if prefix == "en" {
            for pref in ["en_US", "en_GB", "en"] where all.contains(pref) { return pref }
        }
        if all.contains(prefix) { return prefix }
        return all.first { $0.hasPrefix(prefix) }
    }
    private static let ruLang = dictLang("ru")
    private static let enLang = dictLang("en")

    /// Есть ли слово в словаре этого языка.
    static func isWord(_ word: String, lang: String) -> Bool {
        guard let dl = (lang == "ru" ? ruLang : enLang) else { return false }
        let r = checker.checkSpelling(of: word, startingAt: 0, language: dl, wrap: false,
                                      inSpellDocumentWithTag: 0, wordCount: nil)
        return r.location == NSNotFound
    }

    private static func letters(_ s: String) -> String {
        String(s.unicodeScalars.filter { CharacterSet.letters.contains($0) }.map(Character.init))
    }
    private static func isLatin(_ c: Character) -> Bool {
        c.unicodeScalars.allSatisfy { ("a"..."z").contains($0) || ("A"..."Z").contains($0) }
    }
    private static func isCyrillic(_ c: Character) -> Bool {
        c.unicodeScalars.allSatisfy { (0x0400...0x04FF).contains(Int($0.value)) }
    }

    /// Похоже ли слово на набранное не в той раскладке (для двойного Shift после пробела).
    /// Правильное слово («привет») — нет: тогда двойной Shift просто сменит язык.
    static func looksWrong(_ word: String) -> Bool {
        let core = letters(word)
        guard !core.isEmpty else { return false }
        if LayoutConverter.isMixed(core) { return true }
        if core.allSatisfy(isLatin) { return !isWord(core, lang: "en") }
        if core.allSatisfy(isCyrillic) { return !isWord(core, lang: "ru") }
        return false
    }

    /// Главная функция: вызывается, когда после слова нажали пробел.
    static func analyze(_ raw: String) -> Fix? {
        let word = raw
        if word.count < 2 || ignored.contains(word) { return nil }
        // ссылки, почта, пути, числа — не трогаем
        if word.contains(where: { "@/:\\_#0123456789".contains($0) }) { return nil }

        var result = word
        var lang: String? = nil

        // 1. Раскладка
        if Settings.autoSwitch {
            let core = letters(word)
            if core.count >= 2 {
                let allLatin = core.allSatisfy(isLatin)
                let allCyr = core.allSatisfy(isCyrillic)
                // для латиницы учитываем и символы-буквы ; ' [ ] , . ` (это ж э х ъ б ю ё)
                if LayoutConverter.isMixed(core) {
                    // «привеn» + пробел → «привет»
                    let (converted, target) = LayoutConverter.convert(word)
                    if isWord(letters(converted), lang: target.lang) {
                        result = converted
                        lang = target.lang
                    }
                } else if allLatin || allCyr {
                    let src = allLatin ? "en" : "ru"
                    let (converted, target) = LayoutConverter.convert(word)
                    let convCore = letters(converted)
                    if convCore.count >= 2,
                       !isWord(core, lang: src),
                       isWord(convCore, lang: target.lang) {
                        result = converted
                        lang = target.lang
                    }
                }
            }
        }

        // 2. Регистр
        if Settings.caseFix, let fixed = fixCase(result) { result = fixed }

        // 3. Опечатки
        if Settings.typoFix, let fixed = fixTypo(result) { result = fixed }

        return result == word ? nil : Fix(text: result, lang: lang)
    }

    /// «ПРивет» → «Привет», «пРИВЕТ» → «Привет». Аббревиатуры (всё заглавное) не трогаем.
    static func fixCase(_ s: String) -> String? {
        let chars = Array(s)
        let lettersOnly = chars.filter { $0.isLetter }
        guard lettersOnly.count >= 3 else { return nil }
        let up = lettersOnly.map { $0.isUppercase }
        // две заглавные, дальше все строчные
        if up[0] && up[1] && up[2...].allSatisfy({ !$0 }) {
            return String(chars.prefix(1)) + String(chars.dropFirst()).lowercased()
        }
        // первая строчная, остальные заглавные — Caps Lock
        if !up[0] && up[1...].allSatisfy({ $0 }) {
            return String(chars.prefix(1)).uppercased() + String(chars.dropFirst()).lowercased()
        }
        return nil
    }

    /// Исправление опечатки по словарю macOS (только слова от 4 букв).
    static func fixTypo(_ s: String) -> String? {
        let core = letters(s)
        guard core.count >= 4, core == s else { return nil }   // без знаков препинания внутри
        let lang = core.allSatisfy(isCyrillic) ? "ru" : (core.allSatisfy(isLatin) ? "en" : nil)
        guard let l = lang, let dl = (l == "ru" ? ruLang : enLang), !isWord(core, lang: l) else { return nil }
        let range = NSRange(location: 0, length: (core as NSString).length)
        guard let c = checker.correction(forWordRange: range, in: core, language: dl, inSpellDocumentWithTag: 0),
              c.lowercased() != core.lowercased() else { return nil }
        return c
    }
}
