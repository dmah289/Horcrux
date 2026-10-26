#!/usr/bin/env bash
# Chạy một script C# trong Play qua Unity CLI rồi trả file kết quả. Mọi phép đo cần Play (trace, số đo, ảnh chụp) đi qua đây.
# Editor phải đang mở project. Chạy từ gốc project hoặc đặt PROJECT_PATH; UNITY_CLI đổi đường dẫn unity.exe.
# Dùng: bash playrun.sh <script .cs> <file ra> [marker] [token] [timeout giây]
#   marker  chuỗi trong script được thay bằng đường dẫn file ra kiểu Windows, đúng một lần (mặc định __OUT__)
#   token   chuỗi script trả về ở dòng cuối, để biết eval đã chạy tới cuối (mặc định playrun)
# In đường dẫn và cỡ file, phần kết quả trước LOG (tối đa 4 KB, trace thì bỏ), rồi LOG đã bỏ stack trace và gộp dòng lặp.
set -u
[ $# -ge 2 ] || { echo "dùng: playrun.sh <script .cs> <file ra> [marker] [token] [timeout giây]"; exit 2; }
U="${UNITY_CLI:-/c/Users/$USERNAME/AppData/Local/Unity/bin/unity.exe}"
P="$(cygpath -w "${PROJECT_PATH:-$PWD}")"
SCRIPT="$1"; DEST="$2"; MARKER="${3:-__OUT__}"; TOKEN="${4:-playrun}"; TIMEOUT="${5:-240}"
WORK="$(mktemp -d)"
OUT="$WORK/out.txt"
python -X utf8 -I -c '
import io, sys
script, out, dest, marker = sys.argv[1:5]
src = io.open(script, encoding="utf8").read()
if src.count(marker) != 1:
    sys.exit("%s phải chứa %s đúng một lần" % (script, marker))
io.open(dest, "w", encoding="utf8").write(src.replace(marker, out))
' "$SCRIPT" "$(cygpath -w "$OUT")" "$WORK/run.cs" "$MARKER" || exit 1

cli() { "$U" cmd --project-path "$P" "$@" >/dev/null 2>&1; }
ready() { "$U" status --project-path "$P" 2>&1 | grep -q "ready"; }
waitready() { for i in $(seq 1 60); do ready && return 0; sleep 2; done; echo "pipeline not ready"; return 1; }

waitready || exit 1
# Editor mất focus thì frame đứng: hai lệnh này giữ nó chạy, chỉ ghi đè trong Editor, không vào build.
# Trả lại ở mọi đường ra: override Run In Background ghi vào ProjectSettings/Packages/com.unity.pipeline.
cli editor.playmode set_run_in_background --enable true
cli editor set_auto_tick --enable true
trap 'cli editor.playmode set_run_in_background --enable false; cli editor set_auto_tick --enable false' EXIT
cli editor.playmode stop
sleep 2; waitready || exit 1
cli editor.playmode start
sleep 4; waitready || exit 1
started=0
for i in $(seq 1 10); do
  out=$("$U" cmd --project-path "$P" --result-only script eval_file --file "$(cygpath -w "$WORK/run.cs")" 2>&1)
  echo "$out" | grep -q "\"$TOKEN\"" && { started=1; break; }
  # Lỗi compile thử lại không bao giờ qua: dừng ngay, in đủ lỗi.
  echo "$out" | grep -q 'Compilation Failed' && { echo "$out"; break; }
  echo "retry eval: $(echo "$out" | head -c 200)"; sleep 3; waitready
done
if [ $started -eq 1 ]; then
  for i in $(seq 1 $((TIMEOUT / 3))); do [ -f "$OUT.done" ] && break; sleep 3; done
fi
cli editor.playmode stop
[ $started -eq 1 ] || { echo "script không chạy, xem $WORK/run.cs"; exit 1; }
[ -f "$OUT.done" ] || { echo "timeout sau $TIMEOUT giây, xem $WORK"; exit 1; }
mkdir -p "$(dirname "$DEST")"
mv "$OUT" "$DEST"
rm -rf "$WORK"
echo "$DEST ($(wc -c < "$DEST") byte)"
# Trace thì không in thân (đọc bằng feeltrace.py); probe thì in phần kết quả trước LOG, tối đa 4 KB.
grep -q '^TRACE$' "$DEST" || sed '/^LOG$/,$d' "$DEST" | head -c 4096
# LOG bỏ dòng stack trace, gộp dòng lặp: lỗi của game không che kết quả.
echo "LOG:"
sed -n '/^LOG$/,/^TRACE$/p' "$DEST" | sed '1d;/^TRACE$/d;/^$/d' | grep -v -E '\(at |^[[:space:]]+at |:[^ ]+ \(.*\)$' | sort | uniq -c | sort -rn | head -20
