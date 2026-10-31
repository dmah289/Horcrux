#!/usr/bin/env bash
# Quay một trace bằng playrun.sh của Horcrux Probe, rồi in chữ ký.
# Editor phải đang mở project. Chạy từ thư mục gốc project, hoặc đặt PROJECT_PATH.
# Dùng: bash feeltrace.sh [--gc] <kịch bản .cs> <thư mục runs> <tên run>
#   --gc  chèn `recorder.Metrics = new FeelTraceMetrics()` vào bản copy của kịch bản (kịch bản phải tạo `var recorder = new …FeelTraceRecorder(`):
#         entity M0 ghi GC byte/frame, batches, set pass. Không dùng cho lần quay để diff: GC đổi giữa các lần chạy.
set -u
GC=0
[ "${1:-}" = "--gc" ] && { GC=1; shift; }
[ $# -eq 3 ] || { echo "dùng: feeltrace.sh [--gc] <kịch bản .cs> <thư mục runs> <tên run>"; exit 2; }
TOOLS="$(cd "$(dirname "$0")" && pwd)"
SCRIPT="$1"; RUNS="$2"; NAME="$3"
if [ $GC -eq 1 ]; then
  GCSCRIPT="$(mktemp --suffix=.cs)"
  python -X utf8 -I -c '
import io, sys
src, dst = sys.argv[1:3]
text = io.open(src, encoding="utf8").read()
marker = "FeelTraceRecorder("
nl = chr(10)
line_end = text.find(nl, text.find(marker))
if marker not in text or line_end < 0:
    sys.exit("%s không có dòng tạo FeelTraceRecorder" % src)
inject = nl + "recorder.Metrics = new Horcrux.Editor.FeelTrace.FeelTraceMetrics();"
io.open(dst, "w", encoding="utf8").write(text[:line_end] + inject + text[line_end:])
' "$SCRIPT" "$GCSCRIPT" || exit 1
  SCRIPT="$GCSCRIPT"
  trap 'rm -f "$GCSCRIPT"' EXIT
fi
bash "$TOOLS/../../Probe/Tools~/playrun.sh" "$SCRIPT" "$RUNS/$NAME.txt" __TRACE_OUT__ feeltrace 240 || exit 1
python -X utf8 -I "$TOOLS/feeltrace.py" sig "$RUNS/$NAME.txt"
