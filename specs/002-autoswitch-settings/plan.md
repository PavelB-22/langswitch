# Plan 002
- **Settings.swift** — все настройки в UserDefaults с значениями по умолчанию.
- **Corrector.swift** — решает, что делать со словом: раскладка (NSSpellChecker ru/en), регистр, опечатки.
- **KeyMonitor** — по пробелу спрашивает Corrector; если есть исправление — «съедает» пробел и отдаёт замену. Считает реальные нажатия (для отката).
- **TextFixer.replace(...)** — стереть N символов, сменить язык, напечатать текст.
- **SettingsView.swift** — SwiftUI TabView в обычном окне (NSHostingController).
- **AppDelegate** — пункт «Настройки…», хранит последнее автоисправление для отката.
- build.sh: + `-framework SwiftUI`.

Проверка по constitution: зависимостей нет (словарь системный), на диск пишем только настройки — ок.
