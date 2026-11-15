#!/bin/bash
# Выпустить новую версию LangSwitch:  bash publish.sh
# Сам поднимает номер версии, отправляет на GitHub, GitHub собирает и публикует.
set -e
cd "$(dirname "$0")"

mkdir -p .github/workflows
cp ci/release.yml .github/workflows/release.yml

PLIST=Resources/Info.plist
CUR=$(grep -A0 'CFBundleShortVersionString' "$PLIST" | sed -E 's/.*<string>([0-9.]+)<\/string>.*/\1/')
[ -d .git ] || git init -b main
if git rev-parse "v$CUR" >/dev/null 2>&1; then
  NEXT=$(echo "$CUR" | awk -F. '{print $1"."$2"."$3+1}')
  echo "▶ Версия $CUR уже выпущена — поднимаю до $NEXT"
  perl -pi -e "s#<string>$CUR</string>#<string>$NEXT</string># if /CFBundleShortVersionString/" "$PLIST"
  BUILD=$(grep 'CFBundleVersion' "$PLIST" | sed -E 's/.*<string>([0-9]+)<\/string>.*/\1/')
  perl -pi -e "s#<string>$BUILD</string>#<string>$((BUILD+1))</string># if /CFBundleVersion/" "$PLIST"
  CUR=$NEXT
fi

echo
echo "Что нового в версии $CUR? Напиши через точку с запятой:"
read -p "> " NOTES
[ -z "$NOTES" ] && NOTES="Улучшения и исправления"
NOTES_LIST=$(echo "$NOTES" | tr ';' '\n' | sed -E 's/^ *//; s/ *$//; /^$/d; s/^/• /')

git add -A
git commit -m "LangSwitch $CUR" || echo "(нет новых изменений)"
git remote | grep -q origin || git remote add origin https://github.com/PavelB-22/langswitch.git

echo "▶ Отправляю на GitHub..."
git push -u origin main
git tag -a "v$CUR" -m "$NOTES_LIST"
git push origin "v$CUR"
echo "✅ Версия $CUR отправлена. Через ~5 минут она появится тут:"
echo "   https://github.com/PavelB-22/langswitch/releases"
