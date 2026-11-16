using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;

namespace LangSwitch
{
    /// Частотный словарь: «как часто встречается слово». Хранится компактно:
    /// все слова отсортированы и склеены в одну строку, поиск — делением пополам.
    internal sealed class WordFreq
    {
        readonly string _all;
        readonly int[] _start;
        readonly int[] _count;

        public static WordFreq Ru, En;

        /// Загружает оба словаря из ресурсов программы (вызывать один раз при старте).
        public static void LoadAll()
        {
            var asm = Assembly.GetExecutingAssembly();
            Ru = FromStream(asm.GetManifestResourceStream("words-ru.txt.gz"));
            En = FromStream(asm.GetManifestResourceStream("words-en.txt.gz"));
        }

        public static WordFreq FromStream(Stream gz)
        {
            string text;
            using (var z = new GZipStream(gz, CompressionMode.Decompress))
            using (var r = new StreamReader(z, Encoding.UTF8))
                text = r.ReadToEnd();
            return new WordFreq(text);
        }

        /// Строки «слово\tчастота», отсортированные по кодам символов.
        WordFreq(string text)
        {
            int lines = 1;
            foreach (var c in text) if (c == '\n') lines++;
            _start = new int[lines + 1];
            _count = new int[lines];
            var sb = new StringBuilder(text.Length / 2);
            int n = 0, i = 0;
            while (i < text.Length)
            {
                int tab = text.IndexOf('\t', i);
                int nl = text.IndexOf('\n', i);
                if (nl < 0) nl = text.Length;
                if (tab < 0 || tab > nl) { i = nl + 1; continue; }
                _start[n] = sb.Length;
                sb.Append(text, i, tab - i);
                int.TryParse(text.Substring(tab + 1, nl - tab - 1), out _count[n]);
                Total += _count[n];
                n++;
                i = nl + 1;
            }
            _start[n] = sb.Length;
            Size = n;
            _all = sb.ToString();
        }

        public int Size { get; }

        /// Сумма всех частот — чтобы сравнивать русские и английские слова «на миллион».
        public long Total { get; private set; }

        /// Сколько раз слово встречается на миллион слов текста.
        public double PerMillion(string w) => Total == 0 ? 0 : Freq(w) * 1e6 / Total;

        int Compare(int idx, string w)
        {
            int s = _start[idx], len = _start[idx + 1] - s;
            int m = Math.Min(len, w.Length);
            for (int k = 0; k < m; k++)
            {
                int d = _all[s + k] - w[k];
                if (d != 0) return d;
            }
            return len - w.Length;
        }

        /// Частота слова (0 — слова нет). Слово должно быть в нижнем регистре.
        public int Freq(string w)
        {
            int lo = 0, hi = Size - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int c = Compare(mid, w);
                if (c == 0) return _count[mid];
                if (c < 0) lo = mid + 1; else hi = mid - 1;
            }
            return 0;
        }
    }
}
