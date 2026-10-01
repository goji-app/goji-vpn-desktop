#!/bin/sh
# Скачивает ядро (Xray-core, sing-box, geoip/geosite) в Runtime/arm64 и Runtime/x64
# и сверяет каждый файл с Runtime/SHA256SUMS. Источники и версии — в Runtime/README.md.
set -eu
cd "$(dirname "$0")"
XRAY=v26.3.27
SINGBOX=1.14.0
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT

fetch_xray() { # $1 — архив релиза, $2 — папка архитектуры
  curl -fsSL -o "$tmp/$1" "https://github.com/XTLS/Xray-core/releases/download/$XRAY/$1"
  mkdir -p "$tmp/$2-xray" "$2"
  unzip -q -o "$tmp/$1" -d "$tmp/$2-xray"
  cp "$tmp/$2-xray/xray" "$tmp/$2-xray/geoip.dat" "$tmp/$2-xray/geosite.dat" "$2/"
}

fetch_singbox() { # $1 — darwin-arm64|darwin-amd64, $2 — папка архитектуры
  name="sing-box-$SINGBOX-$1"
  curl -fsSL -o "$tmp/$name.tar.gz" "https://github.com/SagerNet/sing-box/releases/download/v$SINGBOX/$name.tar.gz"
  tar -xzf "$tmp/$name.tar.gz" -C "$tmp"
  cp "$tmp/$name/sing-box" "$2/"
}

fetch_xray Xray-macos-arm64-v8a.zip arm64
fetch_xray Xray-macos-64.zip x64
fetch_singbox darwin-arm64 arm64
fetch_singbox darwin-amd64 x64
chmod 755 arm64/xray arm64/sing-box x64/xray x64/sing-box
shasum -a 256 -c SHA256SUMS
