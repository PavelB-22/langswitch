#!/bin/bash
# Сборка LangSwitch.app
#   bash build.sh          — только собрать
#   bash build.sh install  — собрать, положить в «Программы» и запустить
#   bash build.sh share    — собрать архив для друга: LangSwitch.zip на Рабочем столе
set -e
cd "$(dirname "$0")"
APP="build/LangSwitch.app"
rm -rf build
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
FRAMEWORKS="-framework AppKit -framework Carbon -framework ServiceManagement -framework SwiftUI"

echo "▶ Компилирую (Apple Silicon + Intel)..."
swiftc -O -target arm64-apple-macos13  Sources/*.swift -o build/LangSwitch-arm64  $FRAMEWORKS
swiftc -O -target x86_64-apple-macos13 Sources/*.swift -o build/LangSwitch-x86_64 $FRAMEWORKS
lipo -create build/LangSwitch-arm64 build/LangSwitch-x86_64 -output "$APP/Contents/MacOS/LangSwitch"
rm build/LangSwitch-arm64 build/LangSwitch-x86_64

cp Resources/Info.plist "$APP/Contents/Info.plist"
cp Resources/AppIcon.icns "$APP/Contents/Resources/AppIcon.icns"
codesign --force --deep -s "${SIGN_IDENTITY:--}" "$APP"
echo "✅ Готово: $APP"

if [ "$1" == "install" ]; then
  pkill -x LangSwitch 2>/dev/null || true
  rm -rf /Applications/LangSwitch.app
  cp -R "$APP" /Applications/
  touch /Applications/LangSwitch.app   # чтобы Finder обновил иконку
  open /Applications/LangSwitch.app
  echo "✅ Установлено в /Applications и запущено"
fi

if [ "$1" == "share" ]; then
  SHARE="build/share/LangSwitch"
  mkdir -p "$SHARE"
  cp -R "$APP" "$SHARE/"
  cp "Resources/Как установить.txt" "$SHARE/"
  rm -f ~/Desktop/LangSwitch.zip
  ditto -c -k --keepParent "$SHARE" ~/Desktop/LangSwitch.zip
  echo "✅ Архив для друга: ~/Desktop/LangSwitch.zip"
fi
