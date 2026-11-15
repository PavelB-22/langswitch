#!/bin/bash
# Кладёт в буфер секреты подписи (те же, что у Shepot) — для репозитория langswitch.
# Запуск: bash secrets.sh
KEYS=~/Projects/Shepot/.secret
URL="https://github.com/PavelB-22/langswitch/settings/secrets/actions"
step() {
  printf "%s" "$(cat "$KEYS/$2")" | pbcopy
  echo
  echo "──── Секрет: $1 ────"
  echo "✅ Значение в буфере. На GitHub: «New repository secret» → Name: $1 → в поле Secret ⌘V → «Add secret»."
  read -p "Готово? Enter... "
}
open "$URL"
step MACOS_CERT_P12      shepot-cert.p12.b64
step MACOS_CERT_PASSWORD shepot-cert-password.txt
printf "" | pbcopy
echo "🎉 Секреты добавлены. Теперь:  bash publish.sh"
