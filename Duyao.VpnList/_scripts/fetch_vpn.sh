#!/bin/bash
# fetch_vpn.sh
# 1. curl CF Access 保护的源端点拿 JSON
# 2. 抽取 ip/port/host_name/country 等字段映射成 /vpn/batch 的入参
# 3. 分批（默认 100/批）POST 到 /vpn/batch
# 4. 把每批接口返回值记录到 ~/.cache/$(basename $0).log
#
# 用法:
#   ./fetch_vpn.sh
#   API_BASE=https://asstp.eooi.de/vpn BATCH=50 ./fetch_vpn.sh
#   SOURCE_URL=https://... ./fetch_vpn.sh

set -u

API_BASE="${API_BASE:-https://google.com}"
SOURCE_URL="${SOURCE_URL:-https://broad-band-0e8b.kuyagic.workers.dev/duyaoo}"
BATCH="${BATCH:-100}"
DRY_RUN="${DRY_RUN:-0}"

CF_ID="${CF_AID:-7ae23f247c53451aadc7115cc99896d8.access}"
CF_SECRET="${CF_ASEC:-29458b810b225dc803ce9d1c3f17568200265cfd00bd87ce82a4aca4e79180a8}"

# 绕开本机 DNS（与 import_vpn.sh 保持一致）
USE_RESOLVE="${USE_RESOLVE:-1}"
RESOLVE_FLAG=""

for cmd in curl jq; do
    if ! command -v "$cmd" >/dev/null 2>&1; then
        echo "[error] missing command: $cmd" >&2
        exit 1
    fi
done

# 日志
LOG_DIR="${HOME}/.cache"
LOG_FILE="${LOG_DIR}/$(basename "$0").log"
mkdir -p "$LOG_DIR"
log_line() {
    local line="$(date '+%Y-%m-%dT%H:%M:%S%z') $1"
    echo "$line" | tee -a "$LOG_FILE"
}
> "$LOG_FILE"
log_line "===== fetch_vpn start API_BASE=$API_BASE SOURCE_URL=$SOURCE_URL BATCH=$BATCH DRY_RUN=$DRY_RUN ====="
[[ -n "$RESOLVE_FLAG" ]] && log_line "[info] using: $RESOLVE_FLAG"

# 1. 拉源数据
echo "[info] fetching $SOURCE_URL"
raw=$(curl -s -L -m 60 \
    -H "CF-Access-Client-Id: $CF_ID" \
    -H "CF-Access-Client-Secret: $CF_SECRET" \
    "$SOURCE_URL")

if [[ -z "$raw" ]]; then
    log_line "[error] empty response from source"
    exit 1
fi

ok=$(echo "$raw" | jq -r '.ok // empty' 2>/dev/null)
src=$(echo "$raw" | jq -r '.src // empty' 2>/dev/null)
total=$(echo "$raw" | jq -r '.total // 0' 2>/dev/null)
log_line "[info] source ok=$ok src=$src total=$total"

# 2. 把 data[] 转换成本地批量接口期望的字段
mapped=$(echo "$raw" | jq -c '[
    .data[]?
    | select(.ip != null and .tcp_port != null)
    | {
        ip: (.ip | tostring),
        port: (.tcp_port | tostring),
        host_name: (.ddnsname // ""),
        country:  (.country // ""),
        ext_1:    (.uptime // ""),
        is_valid: 1
    }
]')
count=$(echo "$mapped" | jq 'length')
log_line "[info] mapped records: $count"

if [[ "$count" -eq 0 ]]; then
    log_line "[warn] no records to push"
    exit 0
fi

# 3. 分批 POST
total_inserted=0
total_updated=0
total_skipped=0
for ((i=0; i<count; i+=BATCH)); do
    end=$((i + BATCH))
    [[ $end -gt $count ]] && end=$count
    chunk=$(echo "$mapped" | jq -c ".[$i:$end]")
    idx=$(( i / BATCH + 1 ))
    log_line "----- batch #$idx range=[$i,$end) -----"

    if [[ "$DRY_RUN" == "1" ]]; then
        log_line "[DRY_RUN] would POST $((end-i)) items"
        continue
    fi

    resp=$(curl -s -m 60 $RESOLVE_FLAG \
        -X POST "$API_BASE/batch" \
        -H "Content-Type: application/json" \
        --data-binary "$chunk")
    log_line "resp: $resp"

    ins=$(echo "$resp" | jq -r '.inserted // 0')
    upd=$(echo "$resp" | jq -r '.updated // 0')
    skp=$(echo "$resp" | jq -r '.skipped // 0')
    total_inserted=$((total_inserted + ins))
    total_updated=$((total_updated + upd))
    total_skipped=$((total_skipped + skp))
done

log_line "===== DONE inserted=$total_inserted updated=$total_updated skipped=$total_skipped total=$count ====="
echo "log: $LOG_FILE"
