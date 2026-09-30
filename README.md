# Goji VPN для macOS

Клиент Goji VPN для Mac: Avalonia 11 / .NET 8, Xray-core + sing-box (TUN).
Интерфейс и возможности такие же, как у Windows- и Android-версий: дизайн «Стекло», глобус,
серверы с пингом и избранным, подписка, поддержка прямо в приложении, «Сайты мимо VPN»,
проверка утечек, значок в строке меню.

## Установка

1. Скачайте архив из [последнего релиза](https://github.com/goji-app/goji-vpn-macos/releases/latest):
   - `…-macOS-arm64.zip` — Mac на Apple Silicon (M1–M4);
   - `…-macOS-x64.zip` — Mac на Intel.
2. Распакуйте и перенесите **Goji VPN** в «Программы».
3. Приложение не заверено Apple, поэтому при первом запуске выполните в Терминале:
   ```
   xattr -dr com.apple.quarantine "/Applications/Goji VPN.app"
   ```
   или откройте его, затем «Системные настройки → Конфиденциальность и безопасность → Всё равно открыть».
4. При подключении macOS попросит пароль администратора — он нужен, чтобы создать
   VPN-интерфейс для всего трафика Mac.

## Сборка (на Windows или macOS)

Нужны .NET 8 SDK, Python 3 с Pillow и `tools/rcodesign` (ad-hoc подпись без Mac).
Ядра под обе архитектуры кладутся в `Runtime/arm64` и `Runtime/x64` — см. `Runtime/README.md`.

```
python build-macos.py            # обе архитектуры
python build-macos.py arm64      # только Apple Silicon
```

Результат — `dist/GojiVPN-<версия>-macOS-<arch>.zip`.
