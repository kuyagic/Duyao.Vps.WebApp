#!/bin/bash
# a
# fulfill-asn-org.sh
# 1. 查询线上接口（默认 /vpn/query）拉取还没有 asn_org 的 vpn 记录
# 2. 对每条记录，调用 https://ip.net.coffee/api/ip/lookup/<ip> 拿 ASN 信息
# 3. 取返回里的 asOrganization 字段，调 PUT /vpn/setAsnOrg/{id} 回写
#    body: { "data": "<asOrganization>" }
#
# 用法:
#   ./fulfill-asn-org.sh
#   API_BASE=https://asstp.eooi.de/vpn ./fulfill-asn-org.sh
#   PAGE_SIZE=200 CONCURRENCY=10 ./fulfill-asn-org.sh
#   DRY_RUN=1 ./fulfill-asn-org.sh        # 只打印，不写回
#   ONLY_EMPTY=1 ./fulfill-asn-org.sh     # 只处理 asn_org 为空的记录（默认）
#   QUERY='{"page":1,"size":50}' ./fulfill-asn-org.sh  # 自定义查询条件
#
# Env:
#   API_BASE       - 自己的 VPN 接口 base (default https://asstp.eooi.de/vpn)
#   LOOKUP_BASE    - 第三方 ASN 查询 base (default https://ip.net.coffee/api/ip/lookup)
#   PAGE_SIZE      - 每页拉取条数 (default 100)
#   CONCURRENCY    - 并发请求数 (default 8)
#   RATE_LIMIT     - 单 worker 请求间隔秒数 (default 0.1)
#   DRY_RUN        - 1 表示不真正写回 (default 0)
#   ONLY_EMPTY     - 1 表示只处理 asn_org 为空 / 为 null 的 (default 1)
#   QUERY          - 自定义查询 JSON 字符串 (default 见脚本内)
#   LOG_FILE       - 日志路径 (default ~/.cache/fulfill-asn-org.log)

set -u

API_BASE="${API_BASE:-https://asstp.eooi.de/vpn}"
LOOKUP_BASE="${LOOKUP_BASE:-https://ip.net.coffee/api/ip/lookup}"
PAGE_SIZE="${PAGE_SIZE:-100}"
CONCURRENCY="${CONCURRENCY:-8}"
RATE_LIMIT="${RATE_LIMIT:-0.1}"
DRY_RUN="${DRY_RUN:-0}"
ONLY_EMPTY="${ONLY_EMPTY:-1}"

LOG_DIR="${HOME}/.cache"
LOG_FILE="${LOG_FILE:-${LOG_DIR}/fulfill-asn-org.log}"
mkdir -p "$LOG_DIR"
log_line() {
    local line="$(date '+%Y-%m-%dT%H:%M:%S%z') $1"
    echo "$line" | tee -a "$LOG_FILE"
}
: > "$LOG_FILE"

for cmd in python3; do
    if ! command -v "$cmd" >/dev/null 2>&1; then
        echo "[error] missing command: $cmd" >&2
        exit 1
    fi
done

log_line "===== fulfill-asn-org start API_BASE=$API_BASE LOOKUP_BASE=$LOOKUP_BASE PAGE_SIZE=$PAGE_SIZE CONCURRENCY=$CONCURRENCY DRY_RUN=$DRY_RUN ONLY_EMPTY=$ONLY_EMPTY ====="

# 默认查询条件：拉所有未失效的记录（is_valid=1），分页拉完
# 客户端再按 AsnOrg 是否为空做过滤（避免对每条记录发起请求前再查一次 DB）
DEFAULT_QUERY_BODY='{"page": 1, "size": '"$PAGE_SIZE"', "is_valid": 1}'
QUERY_BODY="${QUERY:-$DEFAULT_QUERY_BODY}"

# 一个 python 脚本，串起来：分页拉 / 并发查 / 回写
python3 - "$API_BASE" "$LOOKUP_BASE" "$PAGE_SIZE" "$CONCURRENCY" "$RATE_LIMIT" "$DRY_RUN" "$ONLY_EMPTY" "$QUERY_BODY" "$LOG_FILE" <<'PY'
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import concurrent.futures as cf

(
    api_base, lookup_base, page_size, concurrency, rate_limit,
    dry_run, only_empty, query_body, log_file,
) = sys.argv[1:10]

page_size = int(page_size)
concurrency = int(concurrency)
rate_limit = float(rate_limit)
dry_run = dry_run == "1"
only_empty = only_empty == "1"

HEADERS_JSON = {"Content-Type": "application/json"}
# 第三方接口 (ip.net.coffee) 对默认 "Python-urllib/3.x" UA 返回 403，
# 显式声明常见浏览器 UA 以通过其 WAF。
DEFAULT_UA = os.environ.get(
    "FULFILL_UA",
    "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
)


def log(msg):
    line = f"{time.strftime('%Y-%m-%dT%H:%M:%S%z')} {msg}"
    print(line, flush=True)
    with open(log_file, "a", encoding="utf-8") as f:
        f.write(line + "\n")


def http_json(method, url, body=None, timeout=30):
    data = None
    headers = {"Accept": "application/json", "User-Agent": DEFAULT_UA}
    if body is not None:
        if isinstance(body, (dict, list)):
            data = json.dumps(body).encode("utf-8")
            headers.update(HEADERS_JSON)
        elif isinstance(body, str):
            data = body.encode("utf-8")
            headers.update(HEADERS_JSON)
    req = urllib.request.Request(url, data=data, method=method, headers=headers)
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        raw = resp.read().decode("utf-8", errors="replace")
        return json.loads(raw) if raw else {}


def fetch_page(page):
    body = json.loads(query_body)
    body["page"] = page
    body["size"] = page_size
    url = api_base.rstrip("/") + "/query"
    try:
        resp = http_json("POST", url, body=body)
    except Exception as e:
        log(f"[error] query page={page} failed: {e}")
        return [], 0
    data = resp.get("data") or []
    total = int(resp.get("total") or 0)
    return data, total


def lookup_asn(ip):
    url = lookup_base.rstrip("/") + "/" + urllib.parse.quote(ip)
    try:
        resp = http_json("GET", url)
    except urllib.error.HTTPError as e:
        log(f"[warn] lookup {ip} HTTPError: {e.code}")
        return None
    except Exception as e:
        log(f"[warn] lookup {ip} error: {e}")
        return None
    as_org = resp.get("asOrganization")
    if not as_org:
        as_org = (
            resp.get("as_organization")
            or resp.get("asn_org")
            or (resp.get("asn") or {}).get("organization")
        )
    if not isinstance(as_org, str):
        return None
    as_org = as_org.strip()
    if not as_org:
        return None
    return as_org


def write_back(row_id, as_org):
    if dry_run:
        log(f"[DRY_RUN] would PUT /setAsnOrg/{row_id} body={{'data': {as_org!r}}}")
        return True
    url = api_base.rstrip("/") + f"/setAsnOrg/{row_id}"
    # 服务端约定 body: { "data": "<asn_org>" }
    try:
        resp = http_json("PUT", url, body={"data": as_org})
    except Exception as e:
        log(f"[error] write_back id={row_id} failed: {e}")
        return False
    ok = bool(resp.get("ok"))
    if not ok:
        log(f"[warn] write_back id={row_id} resp={resp}")
    return ok


def should_process(row):
    if not only_empty:
        return True
    v = row.get("asn_org")
    return v is None or v == ""


def process_row(row):
    rid = row.get("id")
    ip = row.get("ip")
    if rid is None or not ip:
        return ("skip", rid, ip, None, "missing id/ip")
    if not should_process(row):
        return ("skip", rid, ip, None, "asn_org already set")
    if rate_limit > 0:
        time.sleep(rate_limit)
    as_org = lookup_asn(ip)
    if as_org is None:
        return ("miss", rid, ip, None, "no asOrganization in lookup response")
    ok = write_back(rid, as_org)
    return ("ok" if ok else "fail", rid, ip, as_org, "")


# 1) 分页拉取
all_rows = []
page = 1
total = 0
while True:
    data, total = fetch_page(page)
    if not data:
        break
    all_rows.extend(data)
    log(f"[info] page={page} fetched={len(data)} total={total} collected={len(all_rows)}")
    if len(all_rows) >= total or len(data) < page_size:
        break
    page += 1
    if page > 10000:
        log("[warn] hit page guard, stop paging")
        break

log(f"[info] total rows fetched: {len(all_rows)} (server total={total})")

# 2) 过滤
targets = [r for r in all_rows if should_process(r)]
log(f"[info] to process: {len(targets)} (only_empty={only_empty})")

if not targets:
    log("[done] nothing to do")
    sys.exit(0)

# 3) 并发处理
ok = miss = fail = skip = 0
with cf.ThreadPoolExecutor(max_workers=concurrency) as pool:
    futures = [pool.submit(process_row, r) for r in targets]
    for fut in cf.as_completed(futures):
        try:
            status, rid, ip, as_org, reason = fut.result()
        except Exception as e:
            log(f"[error] worker exception: {e}")
            fail += 1
            continue
        if status == "ok":
            ok += 1
            log(f"[ok] id={rid} ip={ip} asn_org={as_org!r}")
        elif status == "miss":
            miss += 1
            log(f"[miss] id={rid} ip={ip} reason={reason}")
        elif status == "fail":
            fail += 1
            log(f"[fail] id={rid} ip={ip} asn_org={as_org!r} reason={reason}")
        else:
            skip += 1
            log(f"[skip] id={rid} ip={ip} reason={reason}")

log(f"===== DONE ok={ok} miss={miss} fail={fail} skip={skip} total_targets={len(targets)} =====")
PY
