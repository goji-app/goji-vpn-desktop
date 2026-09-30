"""Сборка macOS-клиента Goji VPN на Windows.

Для каждой архитектуры (arm64 — Apple Silicon, x64 — Intel):
  dotnet publish (self-contained) -> Goji VPN.app -> ad-hoc подпись rcodesign -> zip с unix-правами.

Раскладка бандла по правилам Apple: в Contents/MacOS только нативный запускатель (apphost),
всё остальное — .NET-сборки, dylib, Runtime/xray, sing-box, geo*.dat — в Contents/Resources/app.
В apphost вшит относительный путь ../Resources/app/GojiVpn.dll (corehost поддерживает подпути).
Иначе .dll в Contents/MacOS не запечатываются подписью, и Gatekeeper называет приложение
"повреждённым" без кнопки "Всё равно открыть".

Результат: dist/GojiVPN-<версия>-macOS-<arch>.zip (имя ждёт UpdateService) + копия в ../builds/macos.
Запуск: python build-macos.py [arm64|x64 ...]
"""
import io
import os
import re
import shutil
import struct
import subprocess
import sys
import zipfile

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.abspath(__file__))
PROJ = os.path.join(ROOT, "GojiVpn.Mac", "GojiVpn.Mac.csproj")
RCODESIGN = os.path.join(ROOT, "tools", "rcodesign.exe")
ICON_SRC = os.path.join(ROOT, "..", "google-play", "assets", "ic_launcher_512.png")
OUT = os.path.join(ROOT, "out")
DIST = os.path.join(ROOT, "dist")
ARCHIVE = os.path.join(ROOT, "..", "builds", "macos")
APP_NAME = "Goji VPN"
EXE_NAME = "GojiVpn"
BUNDLE_ID = "xyz.gojihub.vpn.mac"
MACHO_MAGIC = {b"\xcf\xfa\xed\xfe", b"\xce\xfa\xed\xfe", b"\xca\xfe\xba\xbe", b"\xfe\xed\xfa\xcf"}


def version():
    with open(PROJ, encoding="utf-8") as f:
        return re.search(r"<Version>([^<]+)</Version>", f.read()).group(1)


def run(cmd):
    print(">", " ".join(cmd), flush=True)
    subprocess.run(cmd, check=True)


def make_icns(path):
    """Иконка в стиле macOS: скруглённый квадрат 824/1024 с тенью, внутри — иконка из Google Play."""
    src = Image.open(ICON_SRC).convert("RGBA")
    canvas = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
    box, off, radius = 824, 100, 185
    shadow = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle((off, off + 12, off + box, off + box + 12), radius, fill=(0, 0, 0, 90))
    canvas.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(14)))
    mask = Image.new("L", (box, box), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, box - 1, box - 1), radius, fill=255)
    tile = src.resize((box, box), Image.LANCZOS)
    tile.putalpha(mask)
    canvas.alpha_composite(tile, (off, off))

    # PNG-вложения icns: icp4 16, icp5 32, ic07 128, ic08 256, ic09 512, ic10 1024,
    # ic11 32 (16@2x), ic12 64 (32@2x), ic13 256 (128@2x), ic14 512 (256@2x).
    chunks = b""
    for tag, size in [("icp4", 16), ("icp5", 32), ("ic11", 32), ("ic12", 64), ("ic07", 128),
                      ("ic08", 256), ("ic13", 256), ("ic09", 512), ("ic14", 512), ("ic10", 1024)]:
        buf = io.BytesIO()
        canvas.resize((size, size), Image.LANCZOS).save(buf, "PNG")
        data = buf.getvalue()
        chunks += tag.encode("ascii") + struct.pack(">I", len(data) + 8) + data
    with open(path, "wb") as f:
        f.write(b"icns" + struct.pack(">I", len(chunks) + 8) + chunks)


def info_plist(ver):
    return f"""<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key><string>{APP_NAME}</string>
    <key>CFBundleDisplayName</key><string>{APP_NAME}</string>
    <key>CFBundleIdentifier</key><string>{BUNDLE_ID}</string>
    <key>CFBundleExecutable</key><string>{EXE_NAME}</string>
    <key>CFBundleIconFile</key><string>goji.icns</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
    <key>CFBundleVersion</key><string>{ver}</string>
    <key>CFBundleShortVersionString</key><string>{ver}</string>
    <key>LSMinimumSystemVersion</key><string>12.0</string>
    <key>LSApplicationCategoryType</key><string>public.app-category.utilities</string>
    <key>NSHighResolutionCapable</key><true/>
    <key>NSRequiresAquaSystemAppearance</key><false/>
    <key>NSHumanReadableCopyright</key><string>© Goji VPN</string>
</dict>
</plist>
"""


def patch_apphost(src, dst):
    """Переносит apphost в Contents/MacOS, заменив имя сборки на путь до Resources/app."""
    with open(src, "rb") as f:
        data = bytearray(f.read())
    old = f"{EXE_NAME}.dll".encode() + bytes(1)
    new = f"../Resources/app/{EXE_NAME}.dll".encode()
    at = data.find(old)
    if at < 0 or data.find(old, at + 1) >= 0 or any(data[at + len(old):at + len(new) + 1]):
        sys.exit("Не нашёл однозначное место для пути сборки в apphost")
    data[at:at + len(new)] = new
    data[at + len(new)] = 0
    with open(dst, "wb") as f:
        f.write(data)
    os.remove(src)


def is_macho(path):
    with open(path, "rb") as f:
        return f.read(4) in MACHO_MAGIC


def zip_app(app_dir, zip_path):
    """zip с unix-правами: Windows их не хранит, а без 0755 macOS не запустит бинарники."""
    base = os.path.dirname(app_dir)
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for dirpath, dirnames, filenames in os.walk(app_dir):
            dirnames.sort()
            rel_dir = os.path.relpath(dirpath, base).replace(os.sep, "/")
            zi = zipfile.ZipInfo(rel_dir + "/")
            zi.external_attr = (0o40755 << 16) | 0x10
            zi.create_system = 3
            z.writestr(zi, b"")
            for name in sorted(filenames):
                full = os.path.join(dirpath, name)
                zi = zipfile.ZipInfo.from_file(full, rel_dir + "/" + name)
                zi.create_system = 3
                zi.external_attr = ((0o100755 if is_macho(full) else 0o100644) << 16)
                zi.compress_type = zipfile.ZIP_DEFLATED
                with open(full, "rb") as f:
                    z.writestr(zi, f.read(), compresslevel=9)


def build(arch, ver):
    rid = f"osx-{arch}"
    pub = os.path.join(OUT, rid, "publish")
    shutil.rmtree(os.path.join(OUT, rid), ignore_errors=True)
    run(["dotnet", "publish", PROJ, "-c", "Release", "-r", rid, "--self-contained", "true",
         "-p:PublishSingleFile=false", "-p:PublishTrimmed=false", "-p:DebugType=None", "-o", pub])

    app = os.path.join(OUT, rid, f"{APP_NAME}.app")
    macos = os.path.join(app, "Contents", "MacOS")
    res = os.path.join(app, "Contents", "Resources")
    app_dir = os.path.join(res, "app")
    shutil.copytree(pub, app_dir)
    os.makedirs(macos)
    patch_apphost(os.path.join(app_dir, EXE_NAME), os.path.join(macos, EXE_NAME))
    runtime_src = os.path.join(ROOT, "Runtime", arch)
    runtime_dst = os.path.join(app_dir, "Runtime")
    os.makedirs(runtime_dst)
    for name in ("xray", "sing-box", "geoip.dat", "geosite.dat"):
        src = os.path.join(runtime_src, name)
        if not os.path.exists(src):
            sys.exit(f"Нет {src} — см. Runtime/README.md")
        shutil.copy2(src, runtime_dst)
    make_icns(os.path.join(res, "goji.icns"))
    with open(os.path.join(app, "Contents", "Info.plist"), "w", encoding="utf-8", newline="\n") as f:
        f.write(info_plist(ver))

    # Ad-hoc подпись: без неё Apple Silicon не запускает Mach-O вовсе (Developer ID нет).
    run([RCODESIGN, "sign", app])

    os.makedirs(DIST, exist_ok=True)
    zip_path = os.path.join(DIST, f"GojiVPN-{ver}-macOS-{arch}.zip")
    if os.path.exists(zip_path):
        os.remove(zip_path)
    zip_app(app, zip_path)
    os.makedirs(ARCHIVE, exist_ok=True)
    shutil.copy2(zip_path, ARCHIVE)
    print(f"OK {zip_path} ({os.path.getsize(zip_path):,} байт)", flush=True)


if __name__ == "__main__":
    v = version()
    for a in (sys.argv[1:] or ["arm64", "x64"]):
        build(a, v)
