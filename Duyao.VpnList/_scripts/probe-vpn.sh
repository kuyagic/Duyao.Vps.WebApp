#!/bin/bash
# probe-vpn.sh
# 通过 /vpn/query 拉取所有 vpn 记录，nc 测每个 ip:port 是否可达
# 不可达的调用 PUT /vpn/setvalid/{id}/0 标记为失效
# 可达的调用 PUT /vpn/setvalid/{id}/1 标记为有效
#
# 依赖: curl, jq, nc
#
# 用法:
#   ./probe-vpn.sh                          # 默认 https://asstp.eooi.de/vpn
#   API_BASE=https://x.example.com/vpn ./probe-vpn.sh
#   PAGE_SIZE=200 NC_TIMEOUT=3 ./probe-vpn.sh
#   DRY_RUN=1 ./probe-vpn.sh                # 只探测不写库
#   NO_COLOR=1 ./probe-vpn.sh               # 关闭颜色输出
#   IS_VALID=0 ./probe-vpn.sh               # 只探测失效 (is_valid=0)
#   IS_VALID=1 ./probe-vpn.sh               # 只探测有效 (is_valid=1)
#   IS_VALID=-1 ./probe-vpn.sh              # 不过滤 (is_valid=-1)

set -u

API_BASE="${API_BASE:-http://127.0.0.1:5001/vpn}"
PAGE_SIZE="${PAGE_SIZE:-200}"
NC_TIMEOUT="${NC_TIMEOUT:-2}"
DRY_RUN="${DRY_RUN:-0}"
IS_VALID="${IS_VALID:-0}"

# 颜色（NO_COLOR=1 时关闭）
if [[ -t 1 && -z "${NO_COLOR:-}" ]]; then
    C_OK="\033[32m"     # 绿
    C_FAIL="\033[31m"   # 红
    C_RST="\033[0m"
else
    C_OK=""; C_FAIL=""; C_RST=""
fi

for cmd in curl jq nc; do
    if ! command -v "$cmd" >/dev/null 2>&1; then
        echo "[error] missing command: $cmd" >&2
        exit 1
    fi
done

echo "[info] API_BASE=$API_BASE"
echo "[info] PAGE_SIZE=$PAGE_SIZE NC_TIMEOUT=$NC_TIMEOUT DRY_RUN=$DRY_RUN IS_VALID=$IS_VALID"

# 1. 取总数
query_one() {
    local body="$1"
    curl -s -m 15 \
        -X POST "$API_BASE/query" \
        -H "Content-Type: application/json" \
        -d "$body"
}

total=$(query_one '{"page":1,"size":1,"is_valid":'"$IS_VALID"'}' | jq -r '.total // 0')
if ! [[ "$total" =~ ^[0-9]+$ ]] || [[ "$total" -eq 0 ]]; then
    echo "[info] no records, total=$total"
    exit 0
fi
echo "[info] total records: $total"

ok_count=0
fail_count=0
total_pages=$(( (total + PAGE_SIZE - 1) / PAGE_SIZE ))

for ((page=1; page<=total_pages; page++)); do
    body=$(printf '{"page":%d,"size":%d,"is_valid":%s}' "$page" "$PAGE_SIZE" "$IS_VALID")
    resp=$(query_one "$body")
    # 解析 id/ip/port/country
    mapfile -t rows < <(echo "$resp" | jq -r '.data[]? | "\(.id)\t\(.ip)\t\(.port)\t\(.country // "")"')

    for row in "${rows[@]}"; do
        [[ -z "$row" ]] && continue
        IFS=$'\t' read -r id ip port country <<< "$row"
        [[ -z "$ip" || -z "$port" ]] && continue
        [[ -z "$country" ]] && country="-"

        if nc -z -w "$NC_TIMEOUT" "$ip" "$port" > /dev/null 2>&1; then
            ok_count=$((ok_count + 1))
            if [[ "$DRY_RUN" == "1" ]]; then
                printf '%b✅ [OK  ]%b id=%-6s %s:%s  %s -> setvalid 1 (DRY_RUN)\n' \
                    "$C_OK" "$C_RST" "$id" "$ip" "$port" "$country"
            else
                setresp=$(curl -s -m 10 \
                    -X PUT "$API_BASE/setvalid/$id/1")
                printf '%b✅ [OK  ]%b id=%-6s %s:%s  %-20s -> setvalid 1  %s\n' \
                    "$C_OK" "$C_RST" "$id" "$ip" "$port" "$country" "$setresp"
            fi
        else
            fail_count=$((fail_count + 1))
            if [[ "$DRY_RUN" == "1" ]]; then
                printf '%b❌ [FAIL]%b id=%-6s %s:%s  %-20s -> setvalid 0 (DRY_RUN)\n' \
                    "$C_FAIL" "$C_RST" "$id" "$ip" "$port" "$country"
            else
                setresp=$(curl -s -m 10 \
                    -X PUT "$API_BASE/setvalid/$id/0")
                printf '%b❌ [FAIL]%b id=%-6s %s:%s  %-20s -> setvalid 0  %s\n' \
                    "$C_FAIL" "$C_RST" "$id" "$ip" "$port" "$country" "$setresp"
            fi
        fi
    done
done

# 汇总带颜色
if [[ "$fail_count" -eq 0 ]]; then
    printf '%b✅ DONE ok=%d fail=%d total=%d%b\n' "$C_OK" "$ok_count" "$fail_count" "$total" "$C_RST"
else
    printf '%b❌ DONE ok=%d fail=%d total=%d%b\n' "$C_FAIL" "$ok_count" "$fail_count" "$total" "$C_RST"
fi
