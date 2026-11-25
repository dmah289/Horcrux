#!/usr/bin/env bash
# Một lệnh kiểm sau mỗi task: build các csproj → Unity recompile → test EditMode theo assembly. In gọn, mã thoát 1 khi một bước hỏng.
# Editor phải đang mở project cho hai bước Unity; --no-unity chỉ chạy bước build.
# Dùng: bash verify.sh [--project <path>] [--csproj a.csproj,b.csproj] [--tests Asm1,Asm2] [--no-unity] [--no-tests] [--timeout giây] [--help]
#   --csproj   csproj do Unity sinh ở gốc project (gitignore), build bằng dotnet hoặc MSBuild, chỉ in lỗi. Bỏ trống thì bỏ bước này.
#   MSBUILD ép đường dẫn MSBuild.exe. Không đặt thì dùng `dotnet` trên PATH, thiếu nữa thì tìm MSBuild của Rider, Visual Studio.
#   --tests    assembly test EditMode, mỗi assembly một lần `test run`. Bỏ trống thì bỏ bước này.
#   --timeout  giây chờ mỗi assembly test (mặc định 600).
#   --ready-wait  giây chờ Editor đang bận (import sau pull, merge) về ready trước bước Unity (mặc định 600). Không Editor nào mở project thì không chờ.
#   Test fail ghi vào Library/HorcruxVerify/<assembly>.fail; lượt sau đánh dấu fail mới, in fail đã hết so với lượt đó.
#   OK, hoặc chỉ còn fail có sẵn (0 fail mới), thì ghi Library/HorcruxVerify/last-ok (giờ epoch, kèm hậu tố khi bỏ bước
#   Unity) — mốc cho hook Stop của Agent. Chỉ còn fail có sẵn vẫn thoát mã 1 và in tên assembly còn fail.
#   UNITY_CLI ép đường dẫn unity.exe (mặc định tìm `unity` trên PATH). Chạy từ gốc project hoặc đặt --project.
set -u
PROJECT="$PWD"; CSPROJ=""; TESTS=""; UNITY=1; RUN_TESTS=1; TIMEOUT=600; READY_WAIT=600
while [ $# -gt 0 ]; do
  case "$1" in
    --project) PROJECT="$2"; shift 2 ;;
    --csproj) CSPROJ="$2"; shift 2 ;;
    --tests) TESTS="$2"; shift 2 ;;
    --no-unity) UNITY=0; shift ;;
    --no-tests) RUN_TESTS=0; shift ;;
    --timeout) TIMEOUT="$2"; shift 2 ;;
    --ready-wait) READY_WAIT="$2"; shift 2 ;;
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
  [ -n "$FAIL" ] && { echo "verify: FAIL$FAIL"; exit 1; }
  mkdir -p "$PROJECT/Library/HorcruxVerify" && echo "$(date +%s)${1:-}" > "$PROJECT/Library/HorcruxVerify/last-ok"
  [ -n "$KNOWN_FAIL" ] && { echo "verify: FAIL tests chỉ là fail có sẵn ($KNOWN_FAIL), 0 fail mới — đã ghi mốc last-ok"; exit 1; }
  echo "verify: OK${1:-}"; exit 0
}

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

# 1. Build csproj: chỉ dòng lỗi, bỏ dòng trùng (MSBuild in mỗi lỗi hai lần).
# Chọn builder một lần. Không tìm thấy là FAIL: chạy lệnh không tồn tại không in dòng "error CS" nào, bước này từng báo ok giả.
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
build_csproj() {
  if [ -n "$MSB" ]; then MSYS_NO_PATHCONV=1 "$MSB" "$(cygpath -w "$1")" -t:Build -p:Configuration=Debug -v:q -nologo -clp:ErrorsOnly 2>&1
  else dotnet build "$1" -nologo -v q -clp:ErrorsOnly 2>&1; fi
}
IFS=',' read -ra PROJS <<< "$CSPROJ"
if [ -n "$CSPROJ" ]; then
  MSB="${MSBUILD:-}"
  if [ -z "$MSB" ] && ! command -v dotnet >/dev/null 2>&1; then MSB="$(find_msbuild)"; fi
  if [ -n "$MSB" ] && [ ! -f "$MSB" ]; then echo "build: MSBuild không có ở $MSB"; FAIL="$FAIL build"; PROJS=()
  elif [ -z "$MSB" ] && ! command -v dotnet >/dev/null 2>&1; then
    echo "build: không có dotnet trên PATH, không tìm thấy MSBuild của Rider hay Visual Studio. Đặt MSBUILD=<đường dẫn MSBuild.exe>"
    FAIL="$FAIL build"; PROJS=()
  elif [ -n "$MSB" ]; then echo "build: dùng $MSB"; fi
fi
for proj in "${PROJS[@]}"; do
  [ -z "$proj" ] && continue
  path="$PROJECT/$proj"; [ -f "$proj" ] && path="$proj"
  if [ ! -f "$path" ]; then echo "build $proj: không có file (Unity chưa sinh csproj?)"; FAIL="$FAIL build"; continue; fi
  out="$(build_csproj "$path")"; rc=$?
  errs="$(echo "$out" | tr -d '\r' | grep -E "error (CS|MSB)[0-9]+" | sed 's/ *\[.*csproj\] *$//' | sort -u)"
  if [ -n "$errs" ]; then echo "build $proj: LỖI"; echo "$errs" | head -40; FAIL="$FAIL build"
  elif [ $rc -ne 0 ]; then echo "build $proj: LỖI (mã thoát $rc, không có dòng error CS/MSB)"; echo "$out" | tr -d '\r' | head -20; FAIL="$FAIL build"
  else echo "build $proj: ok"; fi
done

[ $UNITY -eq 1 ] || finish " (chỉ build csproj)"

# 2. Unity recompile: hỏi trạng thái tới khi completed hoặc up_to_date; lỗi biên dịch nằm trong errors.
if [ $EDITOR_READY -eq 0 ]; then
  s="$(editor_state)"
  if [ -z "$s" ]; then echo "unity: Editor chưa mở project"; else echo "unity: Editor vẫn $s sau $READY_WAIT s (hộp thoại đang mở? import nặng thì tăng --ready-wait)"; fi
  FAIL="$FAIL unity"; finish
fi
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
  echo "$raw" | jfield errors | python -X utf8 -I -c '
import json, sys
for e in json.loads(sys.stdin.read() or "[]")[:40]:
    print("  " + (e if isinstance(e, str) else json.dumps(e, ensure_ascii=False)))'
  FAIL="$FAIL recompile"
else echo "recompile: $status"; fi

{ [ $RUN_TESTS -eq 1 ] && [ -z "$FAIL" ]; } || finish

# Recompile xong còn domain reload: Pipeline ngắt vài giây, test run gửi lúc đó trả EDITOR_NOT_RUNNING. Đợi ready lại.
for i in $(seq 1 30); do "$U" status --project-path "$W" 2>&1 | grep -q "ready" && break; sleep 2; done

# 3. Test EditMode theo assembly: in Summary; lệnh trả in_progress thì hỏi test status tới khi xong.
IFS=',' read -ra ASMS <<< "$TESTS"
for asm in "${ASMS[@]}"; do
  [ -z "$asm" ] && continue
  # scene đang mở có thay đổi chưa lưu thì Test Runner mở hộp thoại hỏi lưu: lệnh đứng tới timeout, Editor không trả lời CLI
  started=$SECONDS
  raw="$("$U" cmd --project-path "$W" --result-only test run --mode EditMode --filter_type assembly --filter "$asm" --timeout "$TIMEOUT" 2>&1)"
  # Kết nối rớt giữa lượt ("socket hang up"): test có thể vẫn chạy trong Editor nên hỏi test status; Editor mất hẳn thì nói thẳng.
  # Editor bận (domain reload vì file đổi giữa lượt, session khác sửa code) thì chờ ready; lượt bị reload cắt thì gửi lại một lần.
  if [ "$(echo "$raw" | jfield success)" = "False" ]; then
    if ! wait_ready 60; then
      echo "tests $asm: Editor ngắt giữa lượt test ($(echo "$raw" | jfield errors | head -c 160)) và không về ready. Mở lại Editor rồi chạy lại"
      FAIL="$FAIL unity"; continue
    fi
    raw="$("$U" cmd --project-path "$W" --result-only test status 2>&1)"
    if [ "$(echo "$raw" | jfield status)" != "in_progress" ] && [ -z "$(echo "$raw" | jfield Summary.Total)" ]; then
      echo "tests $asm: lượt test bị cắt giữa chừng, chạy lại"
      started=$SECONDS
      raw="$("$U" cmd --project-path "$W" --result-only test run --mode EditMode --filter_type assembly --filter "$asm" --timeout "$TIMEOUT" 2>&1)"
    fi
  fi
  while [ "$(echo "$raw" | jfield status)" = "in_progress" ] && [ $((SECONDS - started)) -lt "$TIMEOUT" ]; do
    sleep 5; raw="$("$U" cmd --project-path "$W" --result-only test status 2>&1)"
  done
  if [ "$(echo "$raw" | jfield status)" = "in_progress" ]; then
    echo "tests $asm: chưa xong sau $TIMEOUT s. Editor có hộp thoại đang mở (scene chưa lưu thì Test Runner hỏi lưu và đứng chờ)? Không có thì tăng --timeout"; FAIL="$FAIL tests"; continue
  fi
  total="$(echo "$raw" | jfield Summary.Total)"; passed="$(echo "$raw" | jfield Summary.Passed)"; failed="$(echo "$raw" | jfield Summary.Failed)"
  if [ -z "$total" ]; then echo "tests $asm: không đọc được kết quả"; echo "$raw" | head -c 600; echo; FAIL="$FAIL tests"; continue; fi
  echo "tests $asm: $passed/$total pass, $failed fail"
  # Fail có sẵn từ trước task không phân biệt được với fail task gây ra nếu chỉ đọc số: so tên với lượt trước, ghi lượt này.
  mkdir -p "$PROJECT/Library/HorcruxVerify"
  echo "$raw" | python -X utf8 -I -c '
import json, os, subprocess, sys, time
path, project = sys.argv[1], sys.argv[2]
raw = sys.stdin.read(); obj, _ = json.JSONDecoder().raw_decode(raw[raw.index("{"):])
fails = [r for r in obj.get("Results", []) if r.get("Status") not in ("Passed", "Skipped")]
# Dòng "+tên" là fail mới chưa pass lại: giữ nhãn mới qua các lượt, không thì chạy lại lần hai là fail mới thành "có sẵn".
prev, prev_new, stamp = None, set(), ""
if os.path.exists(path):
    lines = open(path, encoding="utf8").read().splitlines()
    stamp, names = (lines[0], lines[1:]) if lines else ("", [])
    prev = {n.lstrip("+") for n in names}
    prev_new = {n[1:] for n in names if n.startswith("+")}
now = {r["FullName"] for r in fails}
new = set() if prev is None else (now - prev) | (now & prev_new)
for r in fails:
    msg = (r.get("Message") or "").strip().replace("\n", " | ")
    mark = " (mới)" if r["FullName"] in new else ""
    print("  %s: %s%s %s" % (r["FullName"], r["Status"], mark, msg[:300]))
if prev is not None:
    for name in sorted(prev - now):
        print("  hết fail: %s" % name)
    print("  so với lượt %s: %d fail mới, %d hết fail" % (stamp, len(new), len(prev - now)))
head = subprocess.run(["git", "-C", project, "rev-parse", "--short", "HEAD"], capture_output=True, text=True).stdout.strip()
with open(path, "w", encoding="utf8") as f:
    f.write("%s @%s\n" % (time.strftime("%Y-%m-%d %H:%M"), head or "?"))
    f.writelines(("+" if r["FullName"] in new else "") + r["FullName"] + "\n" for r in fails)
# Mã 1: có fail mới, hoặc chưa có lượt trước để biết fail nào có sẵn.
sys.exit(1 if new or (prev is None and fails) else 0)' "$PROJECT/Library/HorcruxVerify/$asm.fail" "$PROJECT"
  has_new=$?
  if [ "$failed" != "0" ]; then
    if [ $has_new -eq 0 ]; then KNOWN_FAIL="${KNOWN_FAIL:+$KNOWN_FAIL, }$asm"; else FAIL="$FAIL tests"; fi
  fi
done

finish
