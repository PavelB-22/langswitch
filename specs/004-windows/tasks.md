# Tasks 004: LangSwitch для Windows

## Подготовка
- [x] T001 Проект net48, один exe — windows/LangSwitch.csproj
- [x] T002 [P] Словари RU/EN из FrequencyWords, сжатые — windows/res/words-*.txt.gz
- [x] T003 [P] Иконка — windows/res/app.ico

## Основа
- [x] T004 Вызовы WinAPI — windows/src/Native.cs
- [x] T005 [P] Настройки и автозапуск — windows/src/Settings.cs
- [x] T006 [P] Раскладки RU/EN — windows/src/InputSwitcher.cs
- [x] T007 [P] Перекодировка — windows/src/LayoutConverter.cs
- [x] T008 Перехват клавиш, двойной Shift, слово — windows/src/KeyMonitor.cs

## US1–US3 (P1)
- [x] T009 [US1] Двойной Shift → смена языка — windows/src/TrayApp.cs
- [x] T010 [US2] Стереть и напечатать слово — windows/src/TextFixer.cs
- [x] T011 [US3] Словари + решение «исправлять/нет» — windows/src/Dictionary.cs, Corrector.cs
- [x] T012 [US3] Перехват пробела, замена, откат двойным Shift — TrayApp.cs
- [x] T013 [US3] Проверка логики на списке слов (тест) — windows/test/

## US4–US7 (P2–P3)
- [x] T014 [US4] Выделение через UI Automation + буфер — TextFixer.cs
- [x] T015 [US5] Значок RU/EN в трее, меню — TrayApp.cs
- [x] T016 [US5] Окно настроек с вкладками — windows/src/SettingsForm.cs
- [x] T017 [US6] Регистр — Corrector.cs
- [x] T018 [US7] Опечатки по словарю — Corrector.cs

## US8: установка
- [x] T019 [US8] Сборка Windows в GitHub Actions — ci/release.yml
- [x] T020 [US8] Установка одной строкой — install.ps1
- [x] T021 [US8] README + каталог (вкладка Windows) — README.md, Catalog/programs.json

## Полировка
- [x] T022 Проверка сборки и логики здесь (компиляция, тест слов)
- [ ] T023 Павел проверяет на своей Windows по критериям spec.md

## Converge (проверка по spec.md)
| Требование | Статус |
|---|---|
| US1 двойной Shift → язык | ✅ в коде (KeyMonitor + TrayApp), ждёт проверки на Windows |
| US2 исправить слово | ✅ в коде |
| US3 автопереключение + откат | ✅ логика проверена тестом: 37/37, 0 ложных из 30 000 частых RU, 7 из 30 000 EN (все — набор не в той раскладке) |
| US4 выделение | ✅ UI Automation + буфер; где программа не отдаёт выделение — просто смена языка |
| US5 трей + настройки | ✅ в коде |
| US6 регистр, US7 опечатки | ✅ проверено тестом |
| US8 установка одной строкой | ✅ install.ps1 + сборка в release.yml |
| Компиляция | ✅ собирается (проверочная сборка mono) |
| Память < 40 МБ, 10/10 в программах | ⏳ T023 — проверка Павлом на Windows |
