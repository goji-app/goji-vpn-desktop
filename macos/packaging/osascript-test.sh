#!/bin/bash
# Запуск root-обёртки ровно так, как это делает приложение: osascript "do shell script …
# with administrator privileges" + nohup … & (VpnEngine.StartPrivilegedTunnelAsync). В CI нет
# окна пароля, поэтому одноразовой машине задаётся пароль и он передаётся в AppleScript.
# Проверяет, что обёртка переживает завершение osascript и туннель поднимается.
set -u
cd "$(dirname "$0")/.."
arch=${1:-arm64}
rt="$PWD/Runtime/$arch"
work=$(mktemp -d)
mkdir -p smoke "$work/state"
status=0
sblog="$work/sing-box.log"; stop="$work/state/tun.stop"; pid="$work/state/tun.pid"
PASS="GojiCi-$RANDOM$RANDOM"
sudo dscl . -passwd "/Users/$USER" "$PASS"

sed -n '/const string script = """/,/""";/p' GojiVpn.Mac/Services/VpnEngine.cs | sed '1d;$d' | sed 's/^            //' > "$work/wrapper.sh"
cat > "$work/xray.json" <<'EOF'
{"log":{"loglevel":"warning"},
 "inbounds":[{"listen":"127.0.0.1","port":10808,"protocol":"socks","settings":{"udp":true}}],
 "outbounds":[{"protocol":"freedom"}]}
EOF
cat > "$work/sing-box.json" <<'EOF'
{"log":{"level":"info"},
 "dns":{"servers":[{"type":"udp","tag":"dns-remote","server":"1.1.1.1","detour":"socks-out"}],"final":"dns-remote"},
 "inbounds":[{"type":"tun","tag":"tun-in","address":["172.19.0.1/30"],"mtu":1280,"auto_route":true,"strict_route":true,"stack":"system"}],
 "outbounds":[{"type":"socks","tag":"socks-out","server":"127.0.0.1","server_port":10808,"version":"5"},{"type":"direct","tag":"direct"}],
 "route":{"auto_detect_interface":true,
  "rules":[{"action":"sniff"},{"protocol":"dns","action":"hijack-dns"},
           {"process_name":["xray","GojiVpn"],"outbound":"direct"},
           {"ip_cidr":["169.254.0.0/16"],"action":"reject"}],
  "final":"socks-out"}}
EOF

# Строка для osascript — те же ShQuote/AppleScriptEscape, что в VpnEngine.cs.
python3 - "$work" "$rt/sing-box" "$sblog" "$$" "$stop" "$pid" "$PASS" > "$work/apple.txt" <<'PY'
import sys
work, sb, log, app_pid, stop, pid, password = sys.argv[1:]
q = lambda s: "'" + s.replace("'", "'\\''") + "'"
esc = lambda s: s.replace("\\", "\\\\").replace('"', '\\"')
script = open(f"{work}/wrapper.sh").read().replace("\r", "")
shell = ("nohup /bin/sh -c " + q(script) + " goji-tun " + q(sb) + " " + q(f"{work}/sing-box.json") + " " + q(log) +
         " " + app_pid + " " + q(stop) + " " + q(pid) + " 172.19.0.2 >/dev/null 2>&1 &")
print(f'do shell script "{esc(shell)}" with administrator privileges user name "{__import__("os").environ["USER"]}" password "{password}"')
PY

dotnet build -v q -nologo packaging/tun-probe -o "$work/probe" > /dev/null || { echo "FAIL: сборка tun-probe"; exit 1; }
"$rt/xray" run -c "$work/xray.json" > "$work/xray.log" 2>&1 &
XRAY=$!
sleep 2

echo "== [$arch] osascript … with administrator privileges"
osascript -e "$(cat "$work/apple.txt")"; echo "osascript exit=$?"
sleep 1
[ -f "$pid" ] && echo "pid-файл: $(cat "$pid")" || echo "pid-файла нет (пока)"
if dotnet "$work/probe/tun-probe.dll" 40; then echo "OK: туннель поднят через osascript"; else status=1; fi
ps -axo pid,user,command | grep -E "goji-tun|sing-box run" | grep -v grep || echo "FAIL: процессов обёртки/sing-box нет"
code=$(curl -s -o /dev/null -w "%{http_code}" -m 15 https://www.apple.com/ || true)
echo "curl: HTTP $code"

touch "$stop"
for i in $(seq 1 30); do sudo pgrep -f "goji-tun" >/dev/null || break; sleep 0.5; done
sudo pgrep -f "goji-tun" >/dev/null && { echo "FAIL: обёртка не завершилась"; status=1; } || echo "OK: обёртка завершилась"
[ -f "$pid" ] && echo "NOTE: pid-файл остался" || echo "OK: pid-файл удалён"
kill $XRAY 2>/dev/null
cp "$sblog" "smoke/osascript-$arch-sing-box.log" 2>/dev/null
echo "--- журнал sing-box (начало)"; head -n 8 "$sblog" 2>/dev/null || echo "(журнала нет)"
exit $status
