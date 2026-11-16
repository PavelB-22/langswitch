using System.Collections.Generic;
using System.Linq;

namespace LangSwitch
{
    /// Решает, нужно ли исправлять набранное слово, и во что.
    internal static class Corrector
    {
        public sealed class Fix
        {
            public string Text;   // на что заменить слово
            public string Lang;   // какую раскладку включить после (null — не менять)
        }

        /// Слова, которые пользователь откатил двойным Shift — до перезапуска их не трогаем.
        public static readonly HashSet<string> Ignored = new HashSet<string>();

        /// Слово считается «настоящим», если встречается хотя бы столько раз.
        const int WordMin = 20;
        /// Во сколько раз (в пересчёте «на миллион слов») слово в другой раскладке
        /// должно встречаться чаще набранного, чтобы мы исправили.
        const double Ratio = 100;

        /// Сленг и сокращения, которые никогда не переключаем.
        static readonly HashSet<string> Keep = new HashSet<string>
        {
            "ща", "щас", "ок", "оке", "лол", "кек", "хз", "пж", "спс", "пасиб", "ин", "фе", "ру", "мб", "кст", "чс", "лс",
            "vs", "dj", "ye", "tt", "nt", "lol", "omg", "btw", "wtf", "tbh", "imo", "pls", "plz", "thx", "ty", "np", "gg", "ok",
        };

        static WordFreq Dict(string lang) => lang == "ru" ? WordFreq.Ru : WordFreq.En;

        public static int Freq(string word, string lang)
        {
            var d = Dict(lang);
            return d == null ? 0 : d.Freq(word.ToLowerInvariant());
        }

        public static bool IsWord(string word, string lang) => Freq(word, lang) >= WordMin;

        static string Letters(string s) => new string(s.Where(char.IsLetter).ToArray());
        static bool AllLatin(string s) => s.Length > 0 && s.All(LayoutConverter.IsLatin);
        static bool AllCyr(string s) => s.Length > 0 && s.All(LayoutConverter.IsCyrillic);

        /// Похоже ли слово на набранное не в той раскладке (для двойного Shift после пробела).
        public static bool LooksWrong(string word)
        {
            var core = Letters(word);
            if (core.Length == 0) return false;
            if (LayoutConverter.IsMixed(core)) return true;
            if (AllLatin(core)) return !IsWord(core, "en");
            if (AllCyr(core)) return !IsWord(core, "ru");
            return false;
        }

        /// Главная функция: вызывается, когда после слова нажали пробел. null — ничего не делать.
        public static Fix Analyze(string word)
        {
            if (word.Length < 2 || Ignored.Contains(word) || Keep.Contains(word.ToLowerInvariant())) return null;
            // ссылки, почта, пути, числа — не трогаем
            if (word.Any(c => "@/:\\_#0123456789".IndexOf(c) >= 0)) return null;

            string result = word, lang = null;

            // 1. Раскладка
            if (Settings.AutoSwitch)
            {
                var core = Letters(word);
                if (core.Length >= 2)
                {
                    if (LayoutConverter.IsMixed(core))
                    {
                        // «привеn» → «привет»
                        var (conv, target) = LayoutConverter.Convert(word);
                        if (IsWord(Letters(conv), target)) { result = conv; lang = target; }
                    }
                    else if (AllLatin(core) || AllCyr(core))
                    {
                        string src = AllLatin(core) ? "en" : "ru";
                        var (conv, target) = LayoutConverter.Convert(word);
                        var convCore = Letters(conv);
                        // Буквы не должны превращаться в знаки («ещё» → «to`» — нет)
                        if (convCore.Length >= 2 && convCore.Length >= core.Length && ShouldSwitch(core, src, convCore, target))
                        {
                            result = conv; lang = target;
                        }
                    }
                }
            }

            // 2. Регистр
            if (Settings.CaseFix) { var f = FixCase(result); if (f != null) result = f; }

            // 3. Опечатки
            if (Settings.TypoFix && lang == null) { var f = FixTypo(result); if (f != null) result = f; }

            return result == word ? null : new Fix { Text = result, Lang = lang };
        }

        /// Исправляем, если в другой раскладке получается настоящее слово,
        /// и оно встречается намного чаще, чем то, что набрано.
        public static bool ShouldSwitch(string core, string src, string convCore, string target)
        {
            if (Freq(convCore, target) < WordMin) return false;
            double conv = Dict(target).PerMillion(convCore.ToLowerInvariant());
            double orig = Dict(src).PerMillion(core.ToLowerInvariant());
            return conv >= orig * Ratio;
        }

        /// «ПРивет» → «Привет», «пРИВЕТ» → «Привет». Аббревиатуры (всё заглавное) не трогаем.
        public static string FixCase(string s)
        {
            var letters = s.Where(char.IsLetter).ToArray();
            if (letters.Length < 3) return null;
            var up = letters.Select(char.IsUpper).ToArray();
            if (up[0] && up[1] && up.Skip(2).All(u => !u))
                return s.Substring(0, 1) + s.Substring(1).ToLower();
            if (!up[0] && up.Skip(1).All(u => u))
                return s.Substring(0, 1).ToUpper() + s.Substring(1).ToLower();
            return null;
        }

        const string RuAlphabet = "абвгдеёжзийклмнопрстуфхцчшщъыьэюя";
        const string EnAlphabet = "abcdefghijklmnopqrstuvwxyz";

        /// Опечатка: ищем самое частое слово на расстоянии одной ошибки (только слова от 4 букв).
        public static string FixTypo(string s)
        {
            var core = Letters(s);
            if (core.Length < 4 || core != s) return null;
            string lang = AllCyr(core) ? "ru" : AllLatin(core) ? "en" : null;
            if (lang == null) return null;
            var low = core.ToLowerInvariant();
            int origF = Freq(low, lang);
            if (origF >= 200) return null;   // слово известно — не трогаем
            var abc = lang == "ru" ? RuAlphabet : EnAlphabet;

            string best = null; int bestF = 0;
            System.Action<string> Try = cand =>
            {
                int f = Freq(cand, lang);
                if (f > bestF) { bestF = f; best = cand; }
            };
            for (int i = 0; i <= low.Length; i++)
            {
                if (i < low.Length) Try(low.Remove(i, 1));                                   // лишняя буква
                if (i < low.Length - 1)                                                       // переставлены
                    Try(low.Substring(0, i) + low[i + 1] + low[i] + low.Substring(i + 2));
                foreach (var c in abc)
                {
                    if (i < low.Length && c != low[i]) Try(low.Substring(0, i) + c + low.Substring(i + 1)); // не та
                    Try(low.Insert(i, c.ToString()));                                          // пропущена
                }
            }
            if (best == null || bestF < 1000 || bestF < origF * 1000L) return null;
            // сохранить заглавную первую букву
            if (char.IsUpper(core[0])) best = char.ToUpper(best[0]) + best.Substring(1);
            return best;
        }
    }
}
