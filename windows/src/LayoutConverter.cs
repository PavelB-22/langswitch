using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LangSwitch
{
    /// Перекодировка текста между раскладками QWERTY и ЙЦУКЕН (та же логика, что на Mac).
    internal static class LayoutConverter
    {
        const string En = "qwertyuiop[]asdfghjkl;'zxcvbnm,.`QWERTYUIOP{}ASDFGHJKL:\"ZXCVBNM<>~";
        const string Ru = "йцукенгшщзхъфывапролджэячсмитьбюёЙЦУКЕНГШЩЗХЪФЫВАПРОЛДЖЭЯЧСМИТЬБЮЁ";

        static readonly Dictionary<char, char> EnToRu = new Dictionary<char, char>();
        static readonly Dictionary<char, char> RuToEn = new Dictionary<char, char>();

        static LayoutConverter()
        {
            for (int i = 0; i < En.Length; i++) { EnToRu[En[i]] = Ru[i]; RuToEn[Ru[i]] = En[i]; }
        }

        public static bool IsLatin(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        public static bool IsCyrillic(char c) => c >= 'Ѐ' && c <= 'ӿ';

        public static bool IsMixed(string s) => s.Any(IsLatin) && s.Any(IsCyrillic);

        /// Вернёт (перекодированный текст, язык цели "ru"/"en").
        public static (string text, string lang) Convert(string text)
        {
            int latin = text.Count(IsLatin), cyr = text.Count(IsCyrillic);
            var sb = new StringBuilder(text.Length);
            if (latin > 0 && cyr > 0)
            {
                // Вперемешку: переводим только «чужие» буквы в язык большинства.
                if (cyr >= latin)
                {
                    const string extra = "[];'`{}:\"~";
                    foreach (var c in text)
                        sb.Append((IsLatin(c) || extra.IndexOf(c) >= 0) && EnToRu.TryGetValue(c, out var r) ? r : c);
                    return (sb.ToString(), "ru");
                }
                foreach (var c in text)
                    sb.Append(IsCyrillic(c) && RuToEn.TryGetValue(c, out var e) ? e : c);
                return (sb.ToString(), "en");
            }
            bool toRu = latin >= cyr;
            var map = toRu ? EnToRu : RuToEn;
            foreach (var c in text) sb.Append(map.TryGetValue(c, out var m) ? m : c);
            return (sb.ToString(), toRu ? "ru" : "en");
        }
    }
}
