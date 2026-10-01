# Goji VPN для компьютера

Десктоп-клиенты Goji VPN — Windows и macOS. Один дизайн «Стекло», одни возможности,
общая нумерация версий и общие релизы: в каждом релизе лежат файлы для обеих систем.

| Папка | Система | Стек |
|---|---|---|
| [`windows/`](windows) | Windows 10/11 | WPF / .NET 8, Xray-core + sing-box (Wintun) |
| [`macos/`](macos) | macOS 12+ (Apple Silicon и Intel) | Avalonia 11 / .NET 8, Xray-core + sing-box (TUN) |

Android-версия — [goji-vpn-android](https://github.com/goji-app/goji-vpn-android).

## Скачать

Все файлы — в [последнем релизе](https://github.com/goji-app/goji-vpn-desktop/releases/latest):

- **Windows** — `GodjiVpn-Setup-<версия>.exe`;
- **Mac с Apple Silicon (M1–M4)** — `GojiVPN-<версия>-macOS-arm64.dmg`;
- **Mac с Intel** — `GojiVPN-<версия>-macOS-x64.dmg`.

Файлы `-macOS-*.zip` нужны для автообновления внутри приложения.

## Сборка

- Windows — `windows/build-installer.ps1` (см. [windows/README.md](windows/README.md)).
- macOS — GitHub Actions `macOS build` (запуск вручную) или `python macos/build-macos.py`
  (см. [macos/README.md](macos/README.md)).
