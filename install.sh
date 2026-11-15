#!/bin/bash
# Установка / обновление LangSwitch одной командой:
#   curl -fsSL https://raw.githubusercontent.com/PavelB-22/langswitch/main/install.sh | bash
set -e
URL="https://github.com/PavelB-22/langswitch/releases/latest/download/LangSwitch.zip"

MAJOR=$(sw_vers -productVersion | cut -d. -f1)
if [ "$MAJOR" -lt 13 ]; then
  echo "❌ Нужна macOS 13 Ventura или новее."; exit 1
fi

TMP=$(mktemp -d)
echo "▶ Скачиваю LangSwitch..."
curl -fL --progress-bar -o "$TMP/LangSwitch.zip" "$URL"
ditto -x -k "$TMP/LangSwitch.zip" "$TMP/"

echo "▶ Устанавливаю в «Программы»..."
pkill -x LangSwitch 2>/dev/null || true
sleep 1
rm -rf /Applications/LangSwitch.app
cp -R "$TMP/LangSwitch.app" /Applications/
xattr -dr com.apple.quarantine /Applications/LangSwitch.app 2>/dev/null || true
rm -rf "$TMP"

open /Applications/LangSwitch.app
echo "✅ Готово! LangSwitch запущен."
echo "   Если появится запрос «Универсальный доступ» — включи там LangSwitch."
echo "   Если стоит Caramba или Punto Switcher — выключи их, чтобы не конфликтовали."
