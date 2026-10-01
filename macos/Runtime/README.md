# Ядро для сборки macOS-клиента

Бинарники не хранятся в git. Перед сборкой положите их сюда:

| Файл | Источник |
|---|---|
| `arm64/xray`, `arm64/geoip.dat`, `arm64/geosite.dat` | [Xray-core v26.3.27](https://github.com/XTLS/Xray-core/releases/tag/v26.3.27) → `Xray-macos-arm64-v8a.zip` (SHA-256 `2e93a67e8aa1936ecefb307e120830fcbd4c643ab9b1c46a2d0838d5f8409eaf`) |
| `x64/xray`, `x64/geoip.dat`, `x64/geosite.dat` | там же → `Xray-macos-64.zip` (SHA-256 `f5b0471d3459eff1b82e48af0aeac186abcc3298210070afbbbd8437a4e8b203`) |
| `arm64/sing-box` | [sing-box v1.14.0](https://github.com/SagerNet/sing-box/releases/tag/v1.14.0) → `sing-box-1.14.0-darwin-arm64.tar.gz` |
| `x64/sing-box` | там же → `sing-box-1.14.0-darwin-amd64.tar.gz` |
| `../tools/rcodesign.exe` | [apple-codesign 0.29.0](https://github.com/indygreg/apple-platform-rs/releases/tag/apple-codesign%2F0.29.0) → `apple-codesign-0.29.0-x86_64-pc-windows-msvc.zip` (SHA-256 `54bb500e2da7a8de02fcae0f331d1cac6e6d7173b4281042ff9c528ba3159aaa`) |

Версии совпадают с Windows-клиентом.
