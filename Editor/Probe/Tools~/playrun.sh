#!/usr/bin/env bash
# Chạy một script C# trong Play qua Unity CLI rồi trả file kết quả. Mọi phép đo cần Play (trace, số đo, ảnh chụp) đi qua đây.
# Editor phải đang mở project. Chạy từ gốc project hoặc đặt PROJECT_PATH; UNITY_CLI ép đường dẫn unity.exe (mặc định tìm `unity` trên PATH).
# PLAY_FROM_BUILD_SCENE=1 vào Play từ scene đầu của Build Settings (PlayFrom.Arm), không cần mở scene khởi động trước.
# SIZES="1080x2340 1080x1920" chạy một lần mỗi cỡ Game view, file ra thêm hậu tố _<cỡ>, __SIZE__ trong script thành cỡ đó; xong trả cỡ cũ.
# DEVICES="Poco M7 Pro 5G;iOS Notch Device" chạy một lần mỗi máy trên Device Simulator (tên cách nhau bằng ;), hậu tố và __SIZE__
#   là tên máy đã thay khoảng trắng bằng _; xong trả view Play về như cũ. Có cả SIZES thì chạy SIZES trước.
# Dùng: bash playrun.sh <script .cs> <file ra> [marker] [token] [timeout giây]
#   marker  chuỗi trong script được thay bằng đường dẫn file ra kiểu Windows, đúng một lần (mặc định __OUT__)
#   token   chuỗi script trả về ở dòng cuối, để biết eval đã chạy tới cuối (mặc định playrun)
# KEEP_SAVE=1 giữ PlayerPrefs lượt chạy để lại; mặc định chụp trước Play và trả sau khi thoát Play (PrefsSnapshot).
# In đường dẫn và cỡ file, phần kết quả trước LOG (tối đa 4 KB, trace thì bỏ), LOG đã bỏ stack trace và gộp dòng lặp, rồi
# EXIT: lỗi console từ lúc script xong tới khi thoát Play hẳn (OnDestroy, dọn lúc thoát) — LOG không bắt được đoạn này.
set -u
[ $# -ge 2 ] || { echo "dùng: playrun.sh <script .cs> <file ra> [marker] [token] [timeout giây]"; exit 2; }
# unity.exe: UNITY_CLI, rồi `unity` trên PATH (bản MSIX ở WindowsApps), rồi đường cài cũ.
if [ -n "${UNITY_CLI:-}" ]; then U="$UNITY_CLI"
elif command -v unity >/dev/null 2>&1; then U="$(command -v unity)"
else U="/c/Users/$USERNAME/AppData/Local/Unity/bin/unity.exe"; fi
P="$(cygpath -w "${PROJECT_PATH:-$PWD}")"
SCRIPT="$1"; DEST="$2"; MARKER="${3:-__OUT__}"; TOKEN="${4:-playrun}"; TIMEOUT="${5:-240}"

# DEVICES: một lần Play mỗi máy trên Simulator, gọi lại chính file này với PLAYRUN_DEVICE; xong trả view Play về như cũ.
if [ -n "${DEVICES:-}" ] && [ -z "${SIZES:-}" ]; then
  dir="$(dirname "$DEST")"; base="$(basename "$DEST")"
  case "$base" in *.*) name="${base%.*}"; ext=".${base##*.}" ;; *) name="$base"; ext="" ;; esac
  status=0
  IFS=';' read -ra devices <<< "$DEVICES"
  for d in "${devices[@]}"; do
    d="$(echo "$d" | sed 's/^ *//;s/ *$//')"; [ -n "$d" ] || continue
    slug="${d// /_}"
    echo "== $d"
    DEVICES= PLAYRUN_DEVICE="$d" PLAYRUN_SLUG="$slug" bash "$0" "$SCRIPT" "$dir/${name}_$slug$ext" "$MARKER" "$TOKEN" "$TIMEOUT" || status=1
  done
  restore="$(mktemp -d)"
  echo 'return Horcrux.Editor.Probe.SimulatorDevice.Restore();' > "$restore/restore.cs"
  echo "play view: $("$U" cmd --project-path "$P" --result-only script eval_file --file "$(cygpath -w "$restore/restore.cs")" 2>&1 | head -c 200)"
  rm -rf "$restore"
  exit $status
fi

# SIZES: một lần Play mỗi cỡ, gọi lại chính file này với PLAYRUN_SIZE; xong trả Game view về cỡ trước lần đặt đầu.
if [ -n "${SIZES:-}" ]; then
  dir="$(dirname "$DEST")"; base="$(basename "$DEST")"
  case "$base" in *.*) name="${base%.*}"; ext=".${base##*.}" ;; *) name="$base"; ext="" ;; esac
  status=0
  for s in $SIZES; do
    echo "== $s"
    SIZES= DEVICES= PLAYRUN_SIZE="$s" bash "$0" "$SCRIPT" "$dir/${name}_$s$ext" "$MARKER" "$TOKEN" "$TIMEOUT" || status=1
  done
  restore="$(mktemp -d)"
  echo 'return Horcrux.Editor.Probe.GameViewSize.Restore();' > "$restore/restore.cs"
  echo "game view: $("$U" cmd --project-path "$P" --result-only script eval_file --file "$(cygpath -w "$restore/restore.cs")" 2>&1 | head -c 200)"
  rm -rf "$restore"
  if [ -n "${DEVICES:-}" ]; then SIZES= bash "$0" "$@" || status=1; fi
  exit $status
fi
SIZE="${PLAYRUN_SIZE:-}"
DEVICE="${PLAYRUN_DEVICE:-}"
if [ -n "$SIZE" ]; then
  [[ "$SIZE" =~ ^([0-9]+)x([0-9]+)$ ]] || { echo "cỡ $SIZE phải dạng <rộng>x<cao>"; exit 2; }
  SIZE_W="${BASH_REMATCH[1]}"; SIZE_H="${BASH_REMATCH[2]}"
fi

WORK="$(mktemp -d)"
OUT="$WORK/out.txt"
python -X utf8 -I -c '
import io, sys
script, out, dest, marker, size = sys.argv[1:6]
src = io.open(script, encoding="utf8").read()
if src.count(marker) != 1:
    sys.exit("%s phải chứa %s đúng một lần" % (script, marker))
io.open(dest, "w", encoding="utf8").write(src.replace(marker, out).replace("__SIZE__", size))
' "$SCRIPT" "$(cygpath -w "$OUT")" "$WORK/run.cs" "$MARKER" "${SIZE:-${PLAYRUN_SLUG:-}}" || exit 1

cli() { "$U" cmd --project-path "$P" "$@" >/dev/null 2>&1; }
ready() { "$U" status --project-path "$P" 2>&1 | grep -q "ready"; }
waitready() { for i in $(seq 1 60); do ready && return 0; sleep 2; done; echo "pipeline not ready"; return 1; }

waitready || exit 1
# Editor mất focus thì frame đứng: hai lệnh này giữ nó chạy, chỉ ghi đè trong Editor, không vào build.
# Trả lại ở mọi đường ra: override Run In Background ghi vào ProjectSettings/Packages/com.unity.pipeline.
# Unity ghi lại file config đó với xuống dòng LF dù giá trị đã về như cũ: chép lại bản trước khi chạy, chỉ khi khác mỗi xuống dòng.
PIPE_CONFIG="${PROJECT_PATH:-$PWD}/ProjectSettings/Packages/com.unity.pipeline/EditorPipelineConfig.json"
PIPE_SNAPSHOT="$(mktemp)"
[ -f "$PIPE_CONFIG" ] && cp "$PIPE_CONFIG" "$PIPE_SNAPSHOT"
restore_pipe_config() {
  if [ -s "$PIPE_SNAPSHOT" ] && [ -f "$PIPE_CONFIG" ]; then
    if cmp -s <(tr -d '\r' < "$PIPE_SNAPSHOT") <(tr -d '\r' < "$PIPE_CONFIG"); then cp "$PIPE_SNAPSHOT" "$PIPE_CONFIG"
    else echo "EditorPipelineConfig.json đổi giá trị sau lần chạy, giữ bản Unity ghi"; fi
  fi
  rm -f "$PIPE_SNAPSHOT"
}
cli editor.playmode set_run_in_background --enable true
cli editor set_auto_tick --enable true
trap 'cli editor.playmode set_run_in_background --enable false; cli editor set_auto_tick --enable false; restore_pipe_config' EXIT
cli editor.playmode stop
sleep 2; waitready || exit 1
# Đặt cỡ trước Play: game đọc cỡ màn lúc khởi động, đổi giữa Play thì layout tính theo màn cũ.
if [ -n "$SIZE" ]; then
  echo "return Horcrux.Editor.Probe.GameViewSize.Set($SIZE_W, $SIZE_H);" > "$WORK/size.cs"
  echo "game view: $("$U" cmd --project-path "$P" --result-only script eval_file --file "$(cygpath -w "$WORK/size.cs")" 2>&1 | head -c 200)"
fi
if [ -n "$DEVICE" ]; then
  echo "return Horcrux.Editor.Probe.SimulatorDevice.Set(\"$DEVICE\");" > "$WORK/device.cs"
  # chọn máy hỏng mà vẫn chạy thì đo nhầm máy đang chọn: dừng ở đây, in danh sách máy
  picked="$("$U" cmd --project-path "$P" --result-only script eval_file --file "$(cygpath -w "$WORK/device.cs")" 2>&1)"
  echo "$picked" | grep -q '"success": true' || { echo "không chọn được máy $DEVICE:"; echo "$picked" | grep -o 'Devices:[^"]*' | head -c 1500; echo; rm -rf "$WORK"; exit 1; }
  echo "device: $(echo "$picked" | grep -o '"result": "[^"]*"' | head -c 300)"
fi
if [ "${PLAY_FROM_BUILD_SCENE:-0}" = "1" ]; then
  echo 'return Horcrux.Editor.Probe.PlayFrom.Arm();' > "$WORK/arm.cs"
  echo "start scene: $("$U" cmd --project-path "$P" --result-only script eval_file --file "$(cygpath -w "$WORK/arm.cs")" 2>&1 | head -c 200)"
fi
# Lượt chạy đi trên save thật của developer: chụp PlayerPrefs trước Play, trả sau khi thoát Play (game flush lúc thoát).
SNAP=""
if [ "${KEEP_SAVE:-0}" != "1" ]; then
  echo "return Horcrux.Editor.Probe.PrefsSnapshot.Take(@\"$(cygpath -w "$WORK/prefs.json")\");" > "$WORK/take.cs"
  took="$("$U" cmd --project-path "$P" --result-only script eval_file --file "$(cygpath -w "$WORK/take.cs")" 2>&1)"
  if echo "$took" | grep -q 'prefs: chụp'; then SNAP="$WORK/prefs.json"; echo "$(echo "$took" | grep -o 'prefs: [^"]*')"
  else echo "không chụp được PlayerPrefs, lượt này giữ save nó để lại: $(echo "$took" | head -c 300)"; fi
fi
# Trả hỏng (Editor bận lệnh khác, mất kết nối) thì bản chụp chép cạnh file ra: thư mục tạm bị dọn ở đường thành công.
restore_save() {
  [ -n "$SNAP" ] || return 0
  echo "return Horcrux.Editor.Probe.PrefsSnapshot.Restore(@\"$(cygpath -w "$SNAP")\");" > "$WORK/restore.cs"
  local restored
  restored="$("$U" cmd --project-path "$P" --result-only script eval_file --file "$(cygpath -w "$WORK/restore.cs")" 2>&1 | grep -o 'prefs: [^"]*')"
  if [ -n "$restored" ]; then echo "$restored"
  else
    mkdir -p "$(dirname "$DEST")"; cp "$SNAP" "$DEST.prefs.json"
    echo "KHÔNG trả được PlayerPrefs — save đang là bản lượt chạy để lại. Khi Editor rảnh, ngoài Play:"
    echo "  unity cmd --project-path \"$P\" script eval --code 'return Horcrux.Editor.Probe.PrefsSnapshot.Restore(@\"$(cygpath -w "$DEST.prefs.json")\");'"
  fi
  SNAP=""
}
# Lỗi console sau khi script xong (OnDestroy, dọn lúc thoát Play): LOG của PlayProbe đã đóng, đọc qua console_digest.py.
CURSOR=""
print_exit_log() {
  [ -n "$CURSOR" ] || return 0
  local digest
  digest="$(UNITY_CLI="$U" PROJECT_PATH="$P" python -X utf8 -I "$(dirname "$0")/console_digest.py" --since "$CURSOR" --level error --frames 1 --max 20 2>&1)"
  echo "$digest" | head -1 | grep -q '^entries 0 ' || { echo "EXIT:"; echo "$digest"; }
}
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
  CURSOR="$("$U" cmd --project-path "$P" --result-only console status 2>/dev/null | grep -o '"cursor": [0-9]*' | grep -o '[0-9]*$')"
fi
cli editor.playmode stop
sleep 2; waitready
restore_save
[ $started -eq 1 ] || { echo "script không chạy, xem $WORK/run.cs"; exit 1; }
[ -f "$OUT.done" ] || { echo "timeout sau $TIMEOUT giây, xem $WORK"; print_exit_log; exit 1; }
mkdir -p "$(dirname "$DEST")"
mv "$OUT" "$DEST"
rm -rf "$WORK"
echo "$DEST ($(wc -c < "$DEST") byte)"
# Trace thì không in thân (đọc bằng feeltrace.py); probe thì in phần kết quả trước LOG, tối đa 4 KB.
grep -q '^TRACE$' "$DEST" || sed '/^LOG$/,$d' "$DEST" | head -c 4096
# LOG bỏ dòng stack trace, gộp dòng lặp: lỗi của game không che kết quả.
echo "LOG:"
sed -n '/^LOG$/,/^TRACE$/p' "$DEST" | sed '1d;/^TRACE$/d;/^$/d' | grep -v -E '\(at |^[[:space:]]+at |:[^ ]+ \(.*\)$' | sort | uniq -c | sort -rn | head -20
print_exit_log
