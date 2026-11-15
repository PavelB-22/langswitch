import Foundation

/// Перекодировка текста между раскладками QWERTY и ЙЦУКЕН.
enum LayoutConverter {
    private static let en = "qwertyuiop[]asdfghjkl;'zxcvbnm,.`QWERTYUIOP{}ASDFGHJKL:\"ZXCVBNM<>~"
    private static let ru = "йцукенгшщзхъфывапролджэячсмитьбюёЙЦУКЕНГШЩЗХЪФЫВАПРОЛДЖЭЯЧСМИТЬБЮЁ"

    private static let enToRu: [Character: Character] = Dictionary(uniqueKeysWithValues: zip(en, ru))
    private static let ruToEn: [Character: Character] = Dictionary(uniqueKeysWithValues: zip(ru, en))

    enum Target { case russian, english
        var lang: String { self == .russian ? "ru" : "en" }
    }

    /// Куда переводить: если латинских букв больше — в русский, иначе в английский.
    static func target(for text: String) -> Target {
        var latin = 0, cyr = 0
        for u in text.unicodeScalars {
            if ("a"..."z").contains(u) || ("A"..."Z").contains(u) { latin += 1 }
            else if (0x0400...0x04FF).contains(Int(u.value)) { cyr += 1 }
        }
        return latin >= cyr ? .russian : .english
    }

    static func isLatinLetter(_ c: Character) -> Bool {
        c.unicodeScalars.allSatisfy { ("a"..."z").contains($0) || ("A"..."Z").contains($0) }
    }
    static func isCyrillicLetter(_ c: Character) -> Bool {
        c.unicodeScalars.allSatisfy { (0x0400...0x04FF).contains(Int($0.value)) }
    }

    /// Слово набрано вперемешку («привеn»)?
    static func isMixed(_ text: String) -> Bool {
        text.contains(where: isLatinLetter) && text.contains(where: isCyrillicLetter)
    }

    static func convert(_ text: String) -> (String, Target) {
        let latin = text.filter(isLatinLetter).count
        let cyr = text.filter(isCyrillicLetter).count
        if latin > 0 && cyr > 0 {
            // Вперемешку: переводим только «чужие» буквы в язык большинства.
            // «привеn» → «привет», «hellщ» → «hello»
            if cyr >= latin {
                let extra: Set<Character> = ["[", "]", ";", "'", "`", "{", "}", ":", "\"", "~"]
                return (String(text.map { (isLatinLetter($0) || extra.contains($0)) ? (enToRu[$0] ?? $0) : $0 }), .russian)
            } else {
                return (String(text.map { isCyrillicLetter($0) ? (ruToEn[$0] ?? $0) : $0 }), .english)
            }
        }
        let t = target(for: text)
        let map = t == .russian ? enToRu : ruToEn
        return (String(text.map { map[$0] ?? $0 }), t)
    }
}
