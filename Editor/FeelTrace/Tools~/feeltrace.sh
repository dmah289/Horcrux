#!/usr/bin/env bash
# Quay một trace bằng playrun.sh của Horcrux Probe, rồi in chữ ký.
# Editor phải đang mở project. Chạy từ thư mục gốc project, hoặc đặt PROJECT_PATH.
# Dùng: bash feeltrace.sh <kịch bản .cs> <thư mục runs> <tên run>
set -u
[ $# -eq 3 ] || { echo "dùng: feeltrace.sh <kịch bản .cs> <thư mục runs> <tên run>"; exit 2; }
TOOLS="$(cd "$(dirname "$0")" && pwd)"
SCRIPT="$1"; RUNS="$2"; NAME="$3"
bash "$TOOLS/../../Probe/Tools~/playrun.sh" "$SCRIPT" "$RUNS/$NAME.txt" __TRACE_OUT__ feeltrace 240 || exit 1
python -X utf8 -I "$TOOLS/feeltrace.py" sig "$RUNS/$NAME.txt"
