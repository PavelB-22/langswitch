# Tasks: LangSwitch

## Подготовка
- [x] T001 Структура папок, Info.plist (LSUIElement, bundle id) — Resources/Info.plist
- [x] T002 Скрипт сборки .app + локальная подпись — build.sh

## Основа
- [x] T003 Запуск приложения без Dock — Sources/main.swift
- [x] T004 Проверка и запрос разрешения «Универсальный доступ» — Sources/AppDelegate.swift
- [x] T005 Поиск RU/EN раскладок, переключение, текущий язык — Sources/InputSwitcher.swift

## US1 (P1, MVP) — двойной Shift
- [x] T006 [US1] CGEventTap + детектор двойного Shift (0,3 с, без других клавиш) — Sources/KeyMonitor.swift
- [x] T007 [US1] Пропуск в полях паролей — Sources/KeyMonitor.swift
- [ ] T008 [US1] Сборка и проверка в Заметках/Safari/Telegram

## US2 (P2) — исправить последнее слово
- [x] T009 [P] [US2] Таблица ЙЦУКЕН↔QWERTY + определение направления — Sources/LayoutConverter.swift
- [x] T010 [US2] Буфер последнего слова, сброс на пробел/Enter/клик/смену приложения — Sources/KeyMonitor.swift
- [x] T011 [US2] Стереть слово и напечатать исправленное — Sources/TextFixer.swift

## US3 (P3) — исправить выделение
- [x] T012 [US3] Чтение выделения через Accessibility — Sources/TextFixer.swift
- [x] T013 [US3] Замена через буфер обмена с восстановлением — Sources/TextFixer.swift

## US4 (P3) — иконка и меню
- [x] T014 [P] [US4] Иконка RU/EN, обновление при любой смене языка — Sources/AppDelegate.swift
- [x] T015 [US4] Меню: вкл/выкл, автозапуск, выход — Sources/AppDelegate.swift

## Полировка
- [ ] T016 Проверка всех критериев spec.md (Converge)
- [x] T017 Короткая инструкция по установке — README.md
