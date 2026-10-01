#!/bin/bash
# Пробный запуск на macOS (GitHub Actions): ставит приложение из .dmg как пользователь,
# запускает его, снимает экран и собирает журналы в smoke/. Туннель не поднимается —
# для него нужна подписка и пароль администратора.
set -u
cd "$(dirname "$0")/.."
mkdir -p smoke
status=0

shot() { sleep "$2"; screencapture -x "smoke/$1.png" || true; }
alive() { if pgrep -x GojiVpn >/dev/null; then echo "OK: $1 работает"; else echo "FAIL: $1 не запущен"; status=1; fi; }
stop_app() { pkill -x GojiVpn || true; sleep 2; }

dmg=$(ls dist/*-macOS-arm64.dmg)
echo "== Образ: $dmg"
hdiutil attach "$dmg" -mountpoint /Volumes/goji -noautoopen
ls -la /Volumes/goji
open /Volumes/goji
shot 0-dmg-window 4
osascript -e 'tell application "Finder" to close every window' || true
rm -rf "/Applications/Goji VPN.app"
ditto "/Volumes/goji/Goji VPN.app" "/Applications/Goji VPN.app"
hdiutil detach /Volumes/goji -force

echo "== Подпись"
codesign --verify --deep --strict --verbose=2 "/Applications/Goji VPN.app" || status=1
codesign -dv "/Applications/Goji VPN.app" 2>&1 | grep -E "Identifier|Signature|Format"
spctl --assess --type execute -vv "/Applications/Goji VPN.app" 2>&1 || true  # без Developer ID отказ ожидаем

echo "== Обычный запуск (экран входа, строка меню)"
open "/Applications/Goji VPN.app"
shot 1-arm64-login 25
alive "arm64"
stop_app

echo "== Превью оболочки с демо-данными"
for tab in 0 1 2 3; do
  open -n --env GODJI_UI_PREVIEW=1 --env GODJI_UI_PREVIEW_DEMO=1 --env GODJI_UI_PREVIEW_SCREEN=shell \
       --env GODJI_UI_PREVIEW_TAB=$tab --env GODJI_UI_PREVIEW_THEME=$([ $tab = 0 ] && echo dark || echo light) \
       "/Applications/Goji VPN.app"
  shot "2-arm64-tab$tab" 15
  alive "arm64 tab $tab"
  stop_app
done

echo "== Ядро запускается"
"/Applications/Goji VPN.app/Contents/Resources/app/Runtime/xray" version | head -1 || status=1
"/Applications/Goji VPN.app/Contents/Resources/app/Runtime/sing-box" version | head -1 || status=1

echo "== Intel-сборка через Rosetta"
if /usr/bin/arch -x86_64 /usr/bin/true 2>/dev/null; then
  x64="out/osx-x64/Goji VPN.app"
  GODJI_UI_PREVIEW=1 GODJI_UI_PREVIEW_DEMO=1 GODJI_UI_PREVIEW_SCREEN=shell /usr/bin/arch -x86_64 "$x64/Contents/MacOS/GojiVpn" > smoke/x64-stdout.log 2>&1 &
  shot 3-x64-rosetta 40
  alive "x64 (Rosetta)"
  "$x64/Contents/Resources/app/Runtime/xray" version | head -1 || status=1
  /usr/bin/arch -x86_64 "$x64/Contents/Resources/app/Runtime/sing-box" version | head -1 || status=1
  stop_app
else
  echo "Rosetta нет — Intel-сборку не проверяю"
fi

echo "== Ядро и TLS: проверка сертификатов системными корнями (патч crypto/x509 для старых macOS)"
for app in "/Applications/Goji VPN.app" "out/osx-x64/Goji VPN.app"; do
  rt="$app/Contents/Resources/app/Runtime"
  run=""; case "$app" in *osx-x64*) run="/usr/bin/arch -x86_64";; esac
  for f in xray sing-box; do printf '%s %s: ' "$app" "$f"; vtool -show-build "$rt/$f" | awk '/minos/{print "minos " $2; exit}'; done
  if $run "$rt/xray" tls ping www.apple.com 2>&1 | grep -qi "succeeded"; then echo "OK: xray TLS"; else echo "FAIL: xray TLS"; $run "$rt/xray" tls ping www.apple.com 2>&1 | tail -5; status=1; fi
  if $run "$rt/sing-box" tools fetch https://www.apple.com/ > /dev/null 2>smoke/sb-fetch.err; then echo "OK: sing-box TLS"; else echo "FAIL: sing-box TLS"; cat smoke/sb-fetch.err; status=1; fi
done

echo "== Журналы приложения"
find "$HOME/Library/Application Support" "$HOME/.local/share" -path "*GodjiVpn*" -name "*.log" 2>/dev/null | while read -r f; do
  echo "--- $f"; tail -n 60 "$f"; cp "$f" "smoke/$(basename "$f")"
done
exit $status
