#!/usr/bin/env bash
# Một lệnh kiểm sau mỗi task: Unity recompile → test EditMode theo assembly. In gọn, mã thoát 1 khi một bước hỏng.
# Editor mở thì recompile là phép kiểm biên dịch, bỏ build csproj (trùng việc, tốn 20–40 s); recompile LỖI thì build để in
# đủ lỗi mọi assembly. Editor không mở, hoặc --no-unity, thì build csproj thay.
# Dùng: bash verify.sh [--project <path>] [--csproj a.csproj,b.csproj] [--tests Asm1,Asm2] [--no-unity] [--no-tests] [--timeout giây] [--help]
#   --csproj   csproj do Unity sinh ở gốc project (gitignore), build bằng dotnet hoặc MSBuild, chỉ in lỗi — chỉ khi không có
#              Editor hoặc khi recompile LỖI. Bỏ trống thì không build. --build ép build cả khi Editor mở.
#   MSBUILD ép đường dẫn MSBuild.exe. Không đặt thì dùng `dotnet` trên PATH, thiếu nữa thì tìm MSBuild của Rider, Visual Studio.
#   --tests    assembly test EditMode. Bỏ trống thì bỏ bước này. Mặc định chỉ chạy lớp test bị chạm bởi file đổi từ lượt
#              test trước (affected_tests.py; không lớp nào thì bỏ bước test), kèm assembly test bị chạm mà danh sách thiếu.
#   --all-tests  chạy cả các assembly trong --tests, không chọn — trước merge, release, hoặc khi nghi bộ chọn sót.
#   --timeout  giây chờ mỗi assembly test (mặc định 600). Lượt đầu chỉ chờ --first-timeout (mặc định 150): lượt treo
#              (Editor không xử lý lệnh) thì huỷ rồi chạy lại một lần với --timeout, thay vì chờ đủ 600 s.
#   --ready-wait  giây chờ Editor đang bận (import sau pull, merge) về ready trước bước Unity (mặc định 600). Không Editor nào mở project thì không chờ.
#   Test fail ghi vào Library/HorcruxVerify/<assembly>.fail; lượt sau đánh dấu fail mới, in fail đã hết so với lượt đó.
#   OK, hoặc chỉ còn fail có sẵn (0 fail mới), thì ghi Library/HorcruxVerify/last-ok (giờ epoch, kèm hậu tố khi bỏ bước
#   Unity) — mốc cho hook Stop của Agent. Chỉ còn fail có sẵn vẫn thoát mã 1 và in tên assembly còn fail. Mọi lượt
#   chạy tới cuối (kể cả FAIL) ghi last-run: agent đã có kết quả để báo, hook không ép chạy lại.
#   UNITY_CLI ép đường dẫn unity.exe (mặc định tìm `unity` trên PATH). Chạy từ gốc project hoặc đặt --project.
#   --print-builder  in builder sẽ dùng (`dotnet` hoặc đường dẫn Windows của MSBuild.exe) rồi thoát; không có builder thì mã 1.
set -u
PROJECT="$PWD"; CSPROJ=""; TESTS=""; UNITY=1; RUN_TESTS=1; ALL_TESTS=0; TIMEOUT=600; FIRST_TIMEOUT=150; READY_WAIT=600; PRINT_BUILDER=0; FORCE_BUILD=0
HERE="$(cd "$(dirname "$0")" && pwd)"
while [ $# -gt 0 ]; do
  case "$1" in
    --project) PROJECT="$2"; shift 2 ;;
    --csproj) CSPROJ="$2"; shift 2 ;;
    --tests) TESTS="$2"; shift 2 ;;
    --all-tests) ALL_TESTS=1; shift ;;
    --no-unity) UNITY=0; shift ;;
    --no-tests) RUN_TESTS=0; shift ;;
    --timeout) TIMEOUT="$2"; shift 2 ;;
    --first-timeout) FIRST_TIMEOUT="$2"; shift 2 ;;
    --build) FORCE_BUILD=1; shift ;;
    --ready-wait) READY_WAIT="$2"; shift 2 ;;
    --print-builder) PRINT_BUILDER=1; shift ;;
    --help|-h) sed -n '2,/^set -u/p' "$0" | sed '$d;s/^# \{0,1\}//'; exit 0 ;;
    *) echo "verify.sh: tham số lạ $1"; exit 2 ;;
  esac
done
if [ -n "${UNITY_CLI:-}" ]; then U="$UNITY_CLI"
elif command -v unity >/dev/null 2>&1; then U="$(command -v unity)"
else U="/c/Users/$USERNAME/AppData/Local/Unity/bin/unity.exe"; fi
W="$(cygpath -w "$PROJECT")"
FAIL=""; KNOWN_FAIL=""

# Một trường của object JSON đầu tiên trên stdin. CLI in thêm một dòng hint sau JSON nên không json.load thẳng.
jfield() { python -X utf8 -I -c '
import json, sys
raw = sys.stdin.read()
try:
    obj, _ = json.JSONDecoder().raw_decode(raw[raw.index("{"):])
except (ValueError, json.JSONDecodeError):
    print(""); sys.exit(0)
cur = obj
for key in sys.argv[1].split("."):
    cur = cur.get(key) if isinstance(cur, dict) else None
    if cur is None: break
print("" if cur is None else (json.dumps(cur, ensure_ascii=False) if isinstance(cur, (dict, list)) else cur))
' "$1"; }

# Ghi mốc last-ok khi task không làm hỏng gì: hook Stop (Agent/Tools~/agent_hooks.py) so mtime file .cs agent đã sửa với
# mốc này. Fail có sẵn không chặn mốc, không thì assembly còn fail từ trước làm hook chặn mọi lượt.
finish() {
  mkdir -p "$PROJECT/Library/HorcruxVerify" && date +%s > "$PROJECT/Library/HorcruxVerify/last-run"
  [ -n "$FAIL" ] && { echo "verify: FAIL$FAIL"; exit 1; }
  echo "$(date +%s)${1:-}" > "$PROJECT/Library/HorcruxVerify/last-ok"
  [ -n "$KNOWN_FAIL" ] && { echo "verify: FAIL tests chỉ là fail có sẵn ($KNOWN_FAIL), 0 fail mới — đã ghi mốc last-ok ($SECONDS s)"; exit 1; }
  echo "verify: OK${1:-} ($SECONDS s)"; exit 0
}

# Builder: MSBUILD › dotnet › MSBuild mới nhất của Rider hay Visual Studio. Đặt MSB (rỗng là dotnet); không có thì in lý do, trả 1.
# Không tìm thấy là FAIL: chạy lệnh không tồn tại không in dòng "error CS" nào, bước build từng báo ok giả.
# Git Bash không liệt kê ổ đĩa dưới `/`, glob `/?/` không khớp: duyệt từng chữ ổ.
find_msbuild() {
  local d c
  for d in c d e f g h; do
    for c in "/$d/Program Files/JetBrains/"*/tools/MSBuild/Current/Bin/MSBuild.exe \
             "/$d/Program Files/Microsoft Visual Studio/"*/*/MSBuild/Current/Bin/MSBuild.exe; do
      [ -f "$c" ] && echo "$c"
    done
  done | sort | tail -1
}
pick_builder() {
  MSB="${MSBUILD:-}"
  if [ -z "$MSB" ] && ! command -v dotnet >/dev/null 2>&1; then MSB="$(find_msbuild)"; fi
  if [ -n "$MSB" ] && [ ! -f "$MSB" ]; then echo "build: MSBuild không có ở $MSB"; return 1; fi
  if [ -z "$MSB" ] && ! command -v dotnet >/dev/null 2>&1; then
    echo "build: không có dotnet trên PATH, không tìm thấy MSBuild của Rider hay Visual Studio. Đặt MSBUILD=<đường dẫn MSBuild.exe>"
    return 1
  fi
}
# plan_check.py hỏi builder ở đây để luật chọn builder chỉ có một bản.
if [ $PRINT_BUILDER -eq 1 ]; then
  pick_builder || exit 1
  if [ -n "$MSB" ]; then cygpath -w "$MSB"; else echo dotnet; fi
  exit 0
fi

# Trạng thái Editor đang mở project (cột state của `unity status`: ready hay trạng thái bận); rỗng khi không Editor nào mở.
editor_state() {
  local out line
  out="$("$U" status --project-path "$W" 2>&1 | tr -d '\r')"
  line="$(echo "$out" | grep -iF -- "$W" | head -1)"
  if [ -n "$line" ]; then echo "$line" | awk '{print $2}'
  elif echo "$out" | grep -q "ready"; then echo ready; fi
}
# Sau pull, merge kéo nhiều file, Editor import vài phút và báo bận; gọi lệnh lúc đó thì timeout. Chờ thay vì báo FAIL unity.
# $1: giây vẫn chờ khi status không thấy Editor — ngay sau khi kết nối rớt giữa lượt, status trả trống vài giây dù Editor còn sống.
wait_ready() {
  local s waited=0 absent_grace="${1:-0}"
  s="$(editor_state)"
  [ "$s" = "ready" ] && return 0
  [ -z "$s" ] && [ "$absent_grace" -le 0 ] && return 1
  echo "unity: Editor đang ${s:-không trả lời}, chờ ready (tối đa $READY_WAIT s)"
  while [ $waited -lt "$READY_WAIT" ]; do
    sleep 5; waited=$((waited + 5)); s="$(editor_state)"
    [ "$s" = "ready" ] && { echo "unity: ready sau $waited s"; return 0; }
    [ -z "$s" ] && [ $waited -ge "$absent_grace" ] && return 1
  done
  return 1
}

# 0. Editor mở thì Refresh trước build: file mới từ pull, merge chỉ vào csproj khi Editor nạp chúng — build csproj cũ báo CS0246 giả.
# Editor không focus cũng chưa thấy file sửa từ ngoài: recompile trả up_to_date trên bản cũ.
EDITOR_READY=0
if [ $UNITY -eq 1 ] && wait_ready; then
  EDITOR_READY=1
  "$U" cmd --project-path "$W" --result-only --timeout 300 script eval --code "UnityEditor.AssetDatabase.Refresh(); return 0;" >/dev/null 2>&1
fi

# 1. Build csproj khi không có Editor (hoặc --build): chỉ dòng lỗi, bỏ dòng trùng (MSBuild in mỗi lỗi hai lần).
build_csproj() {
  if [ -n "$MSB" ]; then MSYS_NO_PATHCONV=1 "$MSB" "$(cygpath -w "$1")" -t:Build -p:Configuration=Debug -v:q -nologo -clp:ErrorsOnly 2>&1
  else dotnet build "$1" -nologo -v q -clp:ErrorsOnly 2>&1; fi
}
# Editor mở thì bỏ ở đường xanh: recompile đủ chứng minh biên dịch; recompile LỖI thì build lại để in đủ lỗi mọi assembly.
run_builds() {
  IFS=',' read -ra PROJS <<< "$CSPROJ"
  [ ${#PROJS[@]} -gt 0 ] || return 0
  if ! pick_builder; then FAIL="$FAIL build"; return 0; fi
  [ -n "$MSB" ] && echo "build: dùng $MSB"
  for proj in "${PROJS[@]}"; do
    [ -z "$proj" ] && continue
    path="$PROJECT/$proj"; [ -f "$proj" ] && path="$proj"
    if [ ! -f "$path" ]; then echo "build $proj: không có file (Unity chưa sinh csproj?)"; FAIL="$FAIL build"; continue; fi
    step=$SECONDS; out="$(build_csproj "$path")"; rc=$?
    errs="$(echo "$out" | tr -d '\r' | grep -E "error (CS|MSB)[0-9]+" | sed 's/ *\[.*csproj\] *$//' | sort -u)"
    if [ -n "$errs" ]; then echo "build $proj: LỖI"; echo "$errs" | head -40; FAIL="$FAIL build"
    elif [ $rc -ne 0 ]; then echo "build $proj: LỖI (mã thoát $rc, không có dòng error CS/MSB)"; echo "$out" | tr -d '\r' | head -20; FAIL="$FAIL build"
    else echo "build $proj: ok ($((SECONDS - step)) s)"; fi
  done
}
if [ -n "$CSPROJ" ] && [ $EDITOR_READY -eq 1 ] && [ $FORCE_BUILD -eq 0 ]; then
  echo "build: bỏ, Editor mở — recompile là phép kiểm biên dịch"
else
  run_builds
fi

[ $UNITY -eq 1 ] || finish " (chỉ build csproj)"

# 2. Unity recompile: hỏi trạng thái tới khi completed hoặc up_to_date; lỗi biên dịch nằm trong errors.
if [ $EDITOR_READY -eq 0 ]; then
  s="$(editor_state)"
  if [ -z "$s" ]; then echo "unity: Editor chưa mở project"; else echo "unity: Editor vẫn $s sau $READY_WAIT s (hộp thoại đang mở? import nặng thì tăng --ready-wait)"; fi
  FAIL="$FAIL unity"; finish
fi
step=$SECONDS
"$U" cmd --project-path "$W" --result-only script recompile >/dev/null 2>&1
status=""; raw=""
for i in $(seq 1 60); do
  raw="$("$U" cmd --project-path "$W" --result-only script recompile_status 2>&1)"
  status="$(echo "$raw" | jfield status)"
  case "$status" in completed|up_to_date) break ;; esac
  sleep 3
done
if [ "$status" != "completed" ] && [ "$status" != "up_to_date" ]; then echo "recompile: không xong sau 180 s (status=$status)"; FAIL="$FAIL recompile"
elif [ "$(echo "$raw" | jfield compilationFailed)" = "True" ] || [ "$(echo "$raw" | jfield failed)" = "True" ]; then
  echo "recompile: LỖI"
  [ -n "$CSPROJ" ] && [ $EDITOR_READY -eq 1 ] && [ $FORCE_BUILD -eq 0 ] && run_builds
  echo "$raw" | jfield errors | python -X utf8 -I -c '
import json, sys
for e in json.loads(sys.stdin.read() or "[]")[:40]:
    print("  " + (e if isinstance(e, str) else json.dumps(e, ensure_ascii=False)))'
  FAIL="$FAIL recompile"
else echo "recompile: $status ($((SECONDS - step)) s)"; fi

{ [ $RUN_TESTS -eq 1 ] && [ -z "$FAIL" ]; } || finish

# Recompile xong còn domain reload: Pipeline ngắt vài giây, test run gửi lúc đó trả EDITOR_NOT_RUNNING. Đợi ready lại.
for i in $(seq 1 30); do "$U" status --project-path "$W" 2>&1 | grep -q "ready" && break; sleep 2; done

# 3. Test EditMode: chỉ test bị chạm bởi file đổi từ lượt test trước (affected_tests.py), --all-tests thì cả assembly.
# Một lần `test run` theo bộ lọc; lệnh trả in_progress thì hỏi test status tới khi xong. Kết quả vào RAW, lỗi đã in thì trả 1.
run_filter() {
  local label="$1" ftype="$2" filter="$3" started=$SECONDS first=$FIRST_TIMEOUT
  [ "$first" -gt "$TIMEOUT" ] && first=$TIMEOUT
  # scene đang mở có thay đổi chưa lưu thì Test Runner mở hộp thoại hỏi lưu: lệnh đứng tới timeout, Editor không trả lời CLI
  RAW="$("$U" cmd --project-path "$W" --result-only test run --mode EditMode --filter_type "$ftype" --filter "$filter" --timeout "$first" 2>&1)"
  # Hết hạn thì CLI huỷ lượt (không trả in_progress). Lượt treo từng chờ đủ 600 s rồi chạy lại xong trong vài chục giây
  # (đo 2026-11): huỷ sớm, chạy lại một lần với hạn đầy đủ.
  if [ "$first" -lt "$TIMEOUT" ] && echo "$RAW" | grep -q "timed out"; then
    echo "tests $label: lượt đầu quá $first s, chạy lại với hạn $TIMEOUT s"
    wait_ready 60 >/dev/null; started=$SECONDS
    RAW="$("$U" cmd --project-path "$W" --result-only test run --mode EditMode --filter_type "$ftype" --filter "$filter" --timeout "$TIMEOUT" 2>&1)"
  fi
  # Kết nối rớt giữa lượt ("socket hang up"): test có thể vẫn chạy trong Editor nên hỏi test status; Editor mất hẳn thì nói thẳng.
  # Editor bận (domain reload vì file đổi giữa lượt, session khác sửa code) thì chờ ready; lượt bị reload cắt thì gửi lại một lần.
  if [ "$(echo "$RAW" | jfield success)" = "False" ]; then
    if ! wait_ready 60; then
      echo "tests $label: Editor ngắt giữa lượt test ($(echo "$RAW" | jfield errors | head -c 160)) và không về ready. Mở lại Editor rồi chạy lại"
      FAIL="$FAIL unity"; return 1
    fi
    RAW="$("$U" cmd --project-path "$W" --result-only test status 2>&1)"
    if [ "$(echo "$RAW" | jfield status)" != "in_progress" ] && [ -z "$(echo "$RAW" | jfield Summary.Total)" ]; then
      echo "tests $label: lượt test bị cắt giữa chừng, chạy lại"
      started=$SECONDS
      RAW="$("$U" cmd --project-path "$W" --result-only test run --mode EditMode --filter_type "$ftype" --filter "$filter" --timeout "$TIMEOUT" 2>&1)"
    fi
  fi
  while [ "$(echo "$RAW" | jfield status)" = "in_progress" ] && [ $((SECONDS - started)) -lt "$TIMEOUT" ]; do
    sleep 5; RAW="$("$U" cmd --project-path "$W" --result-only test status 2>&1)"
  done
  if [ "$(echo "$RAW" | jfield status)" = "in_progress" ]; then
    echo "tests $label: chưa xong sau $TIMEOUT s. Editor có hộp thoại đang mở (scene chưa lưu thì Test Runner hỏi lưu và đứng chờ)? Không có thì tăng --timeout"; FAIL="$FAIL tests"; return 1
  fi
  if [ -z "$(echo "$RAW" | jfield Summary.Total)" ]; then echo "tests $label: không đọc được kết quả"; echo "$RAW" | head -c 600; echo; FAIL="$FAIL tests"; return 1; fi
}

# Lựa chọn: dòng "all<TAB>asm" hoặc "class<TAB>asm<TAB>lớp"; "note" in ra cho người đọc.
if [ $ALL_TESTS -eq 1 ]; then
  SELECTION="$(IFS=','; for a in $TESTS; do [ -n "$a" ] && printf 'all\t%s\n' "$a"; done)"
else
  SELECTION="$(python -X utf8 -I "$HERE/affected_tests.py" --project "$PROJECT" --tests "$TESTS")" || { echo "tests: affected_tests.py hỏng, chạy lại với --all-tests"; FAIL="$FAIL tests"; finish; }
  echo "$SELECTION" | awk -F'\t' '$1 == "note" { print "tests: " $2 }'
fi
ASMS="$(echo "$SELECTION" | awk -F'\t' '$1 == "all" || $1 == "class" { print $2 }' | awk '!seen[$0]++')"
[ -z "$ASMS" ] && echo "tests: không test nào bị chạm, bỏ bước test"
while IFS= read -r asm; do
  [ -z "$asm" ] && continue
  step=$SECONDS
  if echo "$SELECTION" | awk -F'\t' -v a="$asm" '$1 == "all" && $2 == a { f = 1 } END { exit !f }'; then
    FILTERS="$(printf 'assembly\t%s' "$asm")"; SCOPE=""
  else
    SCOPE="$(echo "$SELECTION" | awk -F'\t' -v a="$asm" '$1 == "class" && $2 == a { print $3 }')"
    FILTERS="$(echo "$SCOPE" | awk '{ print "testName\t" $0 }')"
  fi
  # Mỗi bộ lọc một lượt, gộp Results vào một file để so tên fail một lần cho cả assembly.
  MERGED="$PROJECT/Library/HorcruxVerify/$asm.run.json"; mkdir -p "$PROJECT/Library/HorcruxVerify"; : > "$MERGED"
  ok=1
  while IFS=$'\t' read -r ftype filter; do
    run_filter "$asm" "$ftype" "$filter" || { ok=0; break; }
    echo "$RAW" >> "$MERGED"
  done <<< "$FILTERS"
  [ $ok -eq 1 ] || continue
  # Fail có sẵn từ trước task không phân biệt được với fail task gây ra nếu chỉ đọc số: so tên với lượt trước, ghi lượt này.
  # Chạy theo lớp thì chỉ so và ghi đè tên fail trong các lớp vừa chạy; fail của lớp không chạy giữ nguyên.
  python -X utf8 -I -c '
import json, os, subprocess, sys, time
path, project, merged, label, scope = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4], [s for s in sys.argv[5].splitlines() if s]
raw = open(merged, encoding="utf8").read()
results, pos, dec = [], 0, json.JSONDecoder()
while True:
    i = raw.find("{", pos)
    if i < 0: break
    obj, pos = dec.raw_decode(raw[i:]); pos += i
    results += obj.get("Results", [])
in_scope = (lambda n: any(n.startswith(s + ".") or n.startswith(s + "(") for s in scope)) if scope else (lambda n: True)
# Bộ lọc testName là chuỗi con: lớp FooTests kéo theo FooTestsExtra. Bỏ test ngoài lớp đã chọn, bỏ trùng giữa các lượt.
results = list({r["FullName"]: r for r in results if in_scope(r["FullName"])}.values())
fails = [r for r in results if r.get("Status") not in ("Passed", "Skipped")]
passed = sum(1 for r in results if r.get("Status") == "Passed")
print("tests %s: %d/%d pass, %d fail%s (%s s)" % (label, passed, len(results), len(fails),
      " — %d lớp bị chạm" % len(scope) if scope else "", sys.argv[6]))
# Dòng "+tên" là fail mới chưa pass lại: giữ nhãn mới qua các lượt, không thì chạy lại lần hai là fail mới thành "có sẵn".
prev, prev_new, stamp, kept = None, set(), "", []
if os.path.exists(path):
    lines = open(path, encoding="utf8").read().splitlines()
    stamp, names = (lines[0], lines[1:]) if lines else ("", [])
    prev = {n.lstrip("+") for n in names if in_scope(n.lstrip("+"))}
    prev_new = {n[1:] for n in names if n.startswith("+")}
    kept = [n for n in names if not in_scope(n.lstrip("+"))]
# Tên mọi test từng chạy (pass hay fail). Test fail mà chưa từng chạy thì mốc không biết nó có sẵn hay do task:
# test đã có ở HEAD là "chưa có mốc" (dữ liệu commit trước đó làm đỏ thì không chặn hook mãi), test task vừa viết là mới.
seen_path = path[:-len(".fail")] + ".seen"
seen = set(open(seen_path, encoding="utf8").read().splitlines()) if os.path.exists(seen_path) else set()
def at_head(name):
    method = name.split("(")[0].rsplit(".", 1)[-1]
    return subprocess.run(["git", "-C", project, "grep", "--recurse-submodules", "-q", "-w", "-e", method, "HEAD", "--", "*.cs"],
                          capture_output=True).returncode == 0
prev = prev or set()
now = {r["FullName"] for r in fails}
first = {n for n in now - prev - prev_new if n not in seen and at_head(n)}
new = (now - prev - first) | (now & prev_new)
for r in fails:
    msg = (r.get("Message") or "").strip().replace("\n", " | ")
    mark = " (mới)" if r["FullName"] in new else (" (chưa có mốc)" if r["FullName"] in first else "")
    print("  %s: %s%s %s" % (r["FullName"], r["Status"], mark, msg[:300]))
for name in sorted(prev - now):
    print("  hết fail: %s" % name)
print("  so với lượt %s: %d fail mới, %d chưa có mốc, %d hết fail" % (stamp or "chưa có", len(new), len(first), len(prev - now)))
if first:
    print("  chưa có mốc: test có sẵn ở HEAD, lần đầu chạy trên máy này đã fail; không chặn. Tìm commit đặt dữ liệu (git log) rồi báo developer")
with open(seen_path, "w", encoding="utf8") as f:
    f.writelines(n + "\n" for n in sorted(seen | {r["FullName"] for r in results}))
head = subprocess.run(["git", "-C", project, "rev-parse", "--short", "HEAD"], capture_output=True, text=True).stdout.strip()
with open(path, "w", encoding="utf8") as f:
    f.write("%s @%s\n" % (time.strftime("%Y-%m-%d %H:%M"), head or "?"))
    f.writelines(n + "\n" for n in kept)
    f.writelines(("+" if r["FullName"] in new else "") + r["FullName"] + "\n" for r in fails)
# Mã 1: có fail mới. Mã 2: chỉ còn fail có sẵn hoặc chưa có mốc.
sys.exit(1 if new else (2 if fails else 0))' \
    "$PROJECT/Library/HorcruxVerify/$asm.fail" "$PROJECT" "$MERGED" "$asm" "$SCOPE" "$((SECONDS - step))"
  rc=$?
  rm -f "$MERGED"
  if [ $rc -eq 1 ]; then FAIL="$FAIL tests"; elif [ $rc -eq 2 ]; then KNOWN_FAIL="${KNOWN_FAIL:+$KNOWN_FAIL, }$asm"; fi
done <<< "$ASMS"

# Mốc chọn test dời khi lượt test không có fail mới: lượt sau chỉ xét file đổi từ đây.
[ -z "$FAIL" ] && python -X utf8 -I "$HERE/affected_tests.py" --project "$PROJECT" --record
finish
