// Проверка логики исправлений на списке слов (без Windows).
// Запуск (нужен mono): bash windows/test/run.sh
using System;
using System.IO;
using LangSwitch;

static class WordTest
{
    static int fails = 0;

    static void Expect(string input, string expected)
    {
        var fix = Corrector.Analyze(input);
        var got = fix == null ? input : fix.Text;
        bool ok = got == expected;
        if (!ok) fails++;
        Console.WriteLine($"{(ok ? "OK  " : "FAIL")} {input,-14} → {got,-14} (ждали {expected})");
    }

    static int Main(string[] args)
    {
        var res = args.Length > 0 ? args[0] : "res";
        WordFreq.Ru = WordFreq.FromStream(File.OpenRead(Path.Combine(res, "words-ru.txt.gz")));
        WordFreq.En = WordFreq.FromStream(File.OpenRead(Path.Combine(res, "words-en.txt.gz")));
        Console.WriteLine($"Словари: ru {WordFreq.Ru.Size}, en {WordFreq.En.Size}");

        Console.WriteLine("— раскладка —");
        Expect("ghbdtn", "привет");
        Expect("vfvf", "мама");
        Expect("rfr", "как");
        Expect("ltkf", "дела");
        Expect("lf", "да");
        Expect("yt", "не");
        Expect("xnj", "что");
        Expect("Ghbdtn", "Привет");
        Expect("cjj,otybt", "сообщение");
        Expect("руддщ", "hello");
        Expect("цщкдв", "world");
        Expect("привеn", "привет");
        Expect("hellщ", "hello");
        Console.WriteLine("— не трогать —");
        Expect("hello", "hello");
        Expect("привет", "привет");
        Expect("GPT", "GPT");
        Expect("iPhone", "iPhone");
        Expect("ok", "ok");
        Expect("hi", "hi");
        Expect("is", "is");
        Expect("qwe", "qwe");
        Expect("и", "и");
        Expect("test@mail.ru", "test@mail.ru");
        Expect("v2", "v2");
        Expect("NASA", "NASA");
        Expect("США", "США");
        Expect("kubernetes", "kubernetes");
        Expect("docker", "docker");
        Console.WriteLine("— регистр —");
        Expect("ПРивет", "Привет");
        Expect("пРИВЕТ", "Привет");
        Console.WriteLine("— опечатки (включаем) —");
        Settings.TypoFix = true;
        Expect("превет", "привет");
        Expect("сообщеие", "сообщение");
        Expect("машына", "машина");
        Expect("pleese", "please");
        Expect("ща", "ща");
        Expect("привет", "привет");

        Settings.TypoFix = false;
        // Ложные срабатывания: самые частые настоящие слова не должны меняться
        foreach (var lang in new[] { "ru", "en" })
        {
            int changed = 0, total = 0;
            using (var z = new System.IO.Compression.GZipStream(File.OpenRead(Path.Combine(res, $"words-{lang}.txt.gz")), System.IO.Compression.CompressionMode.Decompress))
            using (var r = new StreamReader(z))
            {
                var all = new System.Collections.Generic.List<(string w, int c)>();
                string line;
                while ((line = r.ReadLine()) != null) { var p = line.Split('\t'); all.Add((p[0], int.Parse(p[1]))); }
                all.Sort((a, b) => b.c.CompareTo(a.c));
                foreach (var item in all.GetRange(0, 30000))
                {
                    total++;
                    var w = item.w; var f = Corrector.Analyze(w);
                    if (f != null) { changed++; if (changed <= 15) Console.WriteLine($"  {lang}: {w} → {f.Text}"); }
                }
            }
            Console.WriteLine($"Ложные срабатывания {lang}: {changed} из {total} самых частых слов");
        }

        Console.WriteLine(fails == 0 ? "\nВсё сошлось ✅" : $"\nОшибок: {fails} ❌");
        return fails == 0 ? 0 : 1;
    }
}
