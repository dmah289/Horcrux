#!/usr/bin/env bash
# Quay một trace: vào Play, chạy kịch bản C# của dự án bằng eval_file, chờ cờ .done, thoát Play, in chữ ký.
# Editor phải đang mở project. Chạy từ thư mục gốc project, hoặc đặt PROJECT_PATH.
# Dùng: bash feeltrace.sh <kịch bản .cs> <thư mục runs> <tên run>
set -u
[ $# -eq 3 ] || { echo "dùng: feeltrace.sh <kịch bản .cs> <thư mục runs> <tên run>"; exit 2; }
U="${UNITY_CLI:-/c/Users/$USERNAME/AppData/Local/Unity/bin/unity.exe}"
TOOLS="$(cd "$(dirname "$0")" && pwd)"
P="$(cygpath -w "${PROJECT_PATH:-$PWD}")"
SCRIPT="$1"; RUNS="$2"; NAME="$3"
WORK="$(mktemp -d)"
OUT="$WORK/trace_out.txt"
mkdir -p "$RUNS"
python -X utf8 -I "$TOOLS/feeltrace.py" render "$SCRIPT" "$(cygpath -w "$OUT")" "$WORK/run.cs" || exit 1

ready() { "$U" status --project-path "$P" 2>&1 | grep -q "ready"; }
waitready() { for i in $(seq 1 60); do ready && return 0; sleep 2; done; echo "pipeline not ready"; return 1; }

waitready || exit 1
"$U" cmd --project-path "$P" editor.playmode stop >/dev/null 2>&1
sleep 2; waitready || exit 1
"$U" cmd --project-path "$P" editor.playmode start >/dev/null 2>&1
sleep 4; waitready || exit 1
started=0
for i in $(seq 1 10); do
  out=$("$U" cmd --project-path "$P" --result-only script eval_file --file "$(cygpath -w "$WORK/run.cs")" 2>&1)
  echo "$out" | grep -q '"feeltrace"' && { started=1; break; }
  # Kịch bản lỗi compile thì thử lại hay chờ cờ .done đều vô ích: dừng ngay, in đủ lỗi.
  echo "$out" | grep -q 'Compilation Failed' && { echo "$out"; break; }
  echo "retry eval: $(echo "$out" | head -c 200)"; sleep 3; waitready
done
if [ $started -eq 1 ]; then
  for i in $(seq 1 80); do [ -f "$OUT.done" ] && break; sleep 3; done
fi
"$U" cmd --project-path "$P" editor.playmode stop >/dev/null 2>&1
[ $started -eq 1 ] || { echo "kịch bản không chạy, xem $WORK/run.cs"; exit 1; }
[ -f "$OUT.done" ] || { echo "trace timeout, xem $WORK"; exit 1; }
mv "$OUT" "$RUNS/$NAME.txt"
rm -rf "$WORK"
python -X utf8 -I "$TOOLS/feeltrace.py" sig "$RUNS/$NAME.txt"
