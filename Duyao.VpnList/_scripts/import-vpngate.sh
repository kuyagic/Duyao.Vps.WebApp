#!/bin/bash
# import-vpngate.sh
# Parse vpngate-style CSV and POST to /vpn/batch in chunks.
# Reads from a file argument or stdin (whichever is available).
#
# Format (CSV, up to 5 columns):
#   ip:port, uptime, ddns-host, country, ext-host
# Mapping:
#   host_name = ddns-host (col 3)
#   ext_1     = uptime    (col 2)
#   ext_2     = ext-host  (col 5, if present)
#   country   = col 4
#
# Usage:
#   ./import-vpngate.sh                          # read vpngate.csv in repo (default)
#   ./import-vpngate.sh /path/to/file.csv
#   cat file.csv | ./import-vpngate.sh
#   curl ... | ./import-vpngate.sh
#
# Env:
#   API    - base URL (default https://asstp.eooi.de/vpn)
#   BATCH  - chunk size (default 200)
set -e

API="${API:-https://sstp-https.1api.pp.ua/vpn}"
BATCH="${BATCH:-100}"
DEFAULT_DATA="vpngate.csv"

WORK=$(mktemp -d)
trap "rm -rf $WORK" EXIT

# Decide source: file arg > stdin (with content) > default file
if [[ -n "${1:-}" && -f "$1" ]]; then
    SRC="$1"
elif [[ ! -t 0 ]]; then
    # stdin is piped/redirected — but might still be empty.
    # Read all of stdin; if nothing arrived, fall back to default file.
    cat > "$WORK/stdin.txt"
    if [[ -s "$WORK/stdin.txt" ]]; then
        SRC="$WORK/stdin.txt"
    elif [[ -f "$DEFAULT_DATA" ]]; then
        SRC="$DEFAULT_DATA"
    else
        echo "[error] stdin is empty and no input file given. Usage: $0 [file.csv]" >&2
        exit 1
    fi
elif [[ -f "$DEFAULT_DATA" ]]; then
    SRC="$DEFAULT_DATA"
else
    echo "[error] no input file and stdin is empty. Usage: $0 [file.csv]" >&2
    exit 1
fi

# Parse CSV -> JSON array (file path, one item per line)
python3 - "$SRC" "$WORK/items.json" <<'PY'
import json, sys
src, out = sys.argv[1], sys.argv[2]
items = []
with open(src, "r", encoding="utf-8") as f:
    for raw in f:
        line = raw.strip()
        if not line:
            continue
        parts = [p.strip() for p in line.split(",")]
        while parts and parts[-1] == "":
            parts.pop()
        if len(parts) < 4:
            continue
        addr, uptime, host, country = parts[0], parts[1], parts[2], parts[3]
        ext = parts[4] if len(parts) >= 5 else ""
        if ":" not in addr:
            continue
        ip, port = addr.split(":", 1)
        item = {"ip": ip, "port": port, "country": country, "is_valid": 1}
        if host:
            item["host_name"] = host
        if uptime:
            item["ext_1"] = uptime
        if ext:
            item["ext_2"] = ext
        items.append(item)
with open(out, "w", encoding="utf-8") as o:
    json.dump(items, o, ensure_ascii=False)
print(f"parsed {len(items)} items from {src}")
PY

# Split into chunks
python3 - "$WORK/items.json" "$WORK/chunks" "$BATCH" <<'PY'
import json, os, sys
items = json.load(open(sys.argv[1]))
os.makedirs(sys.argv[2], exist_ok=True)
n = int(sys.argv[3])
for i in range(0, len(items), n):
    with open(f"{sys.argv[2]}/chunk_{i//n:04d}.json", "w") as o:
        json.dump(items[i:i+n], o, ensure_ascii=False)
chunks = (len(items) + n - 1) // n
print(f"split {len(items)} into {chunks} chunks")
PY

total_ins=0
total_upd=0
total_skp=0
for f in $(ls $WORK/chunks/chunk_*.json | sort); do
    resp=$(curl -4s -m 60 -X POST "$API/batch" \
        -H "Content-Type: application/json" \
        --data-binary @$f)
    echo "[$(basename $f)] $resp"
    ins=$(echo "$resp" | python3 -c "import json,sys;print(json.load(sys.stdin).get('inserted',0))")
    upd=$(echo "$resp" | python3 -c "import json,sys;print(json.load(sys.stdin).get('updated',0))")
    skp=$(echo "$resp" | python3 -c "import json,sys;print(json.load(sys.stdin).get('skipped',0))")
    total_ins=$((total_ins + ins))
    total_upd=$((total_upd + upd))
    total_skp=$((total_skp + skp))
done
echo "============================="
echo "TOTAL inserted=$total_ins updated=$total_upd skipped=$total_skp"
