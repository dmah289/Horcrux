#!/usr/bin/env bash
# Một lệnh kiểm sau mỗi task: dotnet build các csproj → Unity recompile → test EditMode theo assembly. In gọn, mã thoát 1 khi một bước hỏng.
# Editor phải đang mở project cho hai bước Unity; --no-unity chỉ chạy dotnet build.
# Dùng: bash verify.sh [--project <path>] [--csproj a.csproj,b.csproj] [--tests Asm1,Asm2] [--no-unity] [--no-tests] [--timeout giây] [--help]
#   --csproj   csproj do Unity sinh ở gốc project (gitignore), build bằng dotnet, chỉ in lỗi. Bỏ trống thì bỏ bước này.
#   --tests    assembly test EditMode, mỗi assembly một lần `test run`. Bỏ trống thì bỏ bước này.
#   --timeout  giây chờ mỗi assembly test (mặc định 600).
#   UNITY_CLI ép đường dẫn unity.exe (mặc định tìm `unity` trên PATH). Chạy từ gốc project hoặc đặt --project.
set -u
PROJECT="$PWD"; CSPROJ=""; TESTS=""; UNITY=1; RUN_TESTS=1; TIMEOUT=600
while [ $# -gt 0 ]; do
  case "$1" in
    --project) PROJECT="$2"; shift 2 ;;
    --csproj) CSPROJ="$2"; shift 2 ;;
    --tests) TESTS="$2"; shift 2 ;;
    --no-unity) UNITY=0; shift ;;
    --no-tests) RUN_TESTS=0; shift ;;
    --timeout) TIMEOUT="$2"; shift 2 ;;
    --help|-h) sed -n '2,/^set -u/p' "$0" | sed '$d;s/^# \{0,1\}//'; exit 0 ;;
    *) echo "verify.sh: tham số lạ $1"; exit 2 ;;
  esac
done
if [ -n "${UNITY_CLI:-}" ]; then U="$UNITY_CLI"
elif command -v unity >/dev/null 2>&1; then U="$(command -v unity)"
else U="/c/Users/$USERNAME/AppData/Local/Unity/bin/unity.exe"; fi
W="$(cygpath -w "$PROJECT")"
FAIL=""

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

finish() { [ -z "$FAIL" ] && { echo "verify: OK${1:-}"; exit 0; }; echo "verify: FAIL$FAIL"; exit 1; }

# 1. dotnet build: chỉ dòng lỗi, bỏ dòng trùng (MSBuild in mỗi lỗi hai lần).
IFS=',' read -ra PROJS <<< "$CSPROJ"
for proj in "${PROJS[@]}"; do
  [ -z "$proj" ] && continue
  path="$PROJECT/$proj"; [ -f "$proj" ] && path="$proj"
  if [ ! -f "$path" ]; then echo "build $proj: không có file (Unity chưa sinh csproj?)"; FAIL="$FAIL build"; continue; fi
  out="$(dotnet build "$path" -nologo -v q -clp:ErrorsOnly 2>&1)"
  errs="$(echo "$out" | grep -E "error (CS|MSB)[0-9]+" | sed 's/ \[.*csproj\]$//' | sort -u)"
  if [ -n "$errs" ]; then echo "build $proj: LỖI"; echo "$errs" | head -40; FAIL="$FAIL build"
  else echo "build $proj: ok"; fi
done

[ $UNITY -eq 1 ] || finish " (chỉ dotnet build)"

# 2. Unity recompile: hỏi trạng thái tới khi completed hoặc up_to_date; lỗi biên dịch nằm trong errors.
"$U" status --project-path "$W" 2>&1 | grep -q "ready" || { echo "unity: Editor chưa mở project (unity status không ready)"; FAIL="$FAIL unity"; finish; }
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

# 3. Test EditMode theo assembly: in Summary; lệnh trả in_progress thì hỏi test status tới khi xong.
IFS=',' read -ra ASMS <<< "$TESTS"
for asm in "${ASMS[@]}"; do
  [ -z "$asm" ] && continue
  # scene đang mở có thay đổi chưa lưu thì Test Runner mở hộp thoại hỏi lưu: lệnh đứng tới timeout, Editor không trả lời CLI
  raw="$("$U" cmd --project-path "$W" --result-only test run --mode EditMode --filter_type assembly --filter "$asm" --timeout "$TIMEOUT" 2>&1)"
  for i in $(seq 1 $((TIMEOUT / 5))); do
    [ "$(echo "$raw" | jfield status)" = "in_progress" ] || break
    sleep 5; raw="$("$U" cmd --project-path "$W" --result-only test status 2>&1)"
  done
  if [ "$(echo "$raw" | jfield status)" = "in_progress" ]; then
    echo "tests $asm: chưa xong sau $TIMEOUT s. Editor có hộp thoại đang mở (scene chưa lưu thì Test Runner hỏi lưu và đứng chờ)? Không có thì tăng --timeout"; FAIL="$FAIL tests"; continue
  fi
  total="$(echo "$raw" | jfield Summary.Total)"; passed="$(echo "$raw" | jfield Summary.Passed)"; failed="$(echo "$raw" | jfield Summary.Failed)"
  if [ -z "$total" ]; then echo "tests $asm: không đọc được kết quả"; echo "$raw" | head -c 600; echo; FAIL="$FAIL tests"; continue; fi
  echo "tests $asm: $passed/$total pass, $failed fail"
  if [ "$failed" != "0" ]; then
    echo "$raw" | python -X utf8 -I -c '
import json, sys
raw = sys.stdin.read(); obj, _ = json.JSONDecoder().raw_decode(raw[raw.index("{"):])
for r in obj.get("Results", []):
    if r.get("Status") not in ("Passed", "Skipped"):
        msg = (r.get("Message") or "").strip().replace("\n", " | ")
        print("  %s: %s %s" % (r["FullName"], r["Status"], msg[:300]))'
    FAIL="$FAIL tests"
  fi
done

finish
