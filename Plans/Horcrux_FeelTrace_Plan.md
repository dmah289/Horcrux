# Plan — `FeelTrace`: quay trạng thái mỗi frame để chứng minh refactor giữ nguyên chuyển động

Ngày 2026-10-26. Nguồn: bản tạm trong dự án category-jam, `docs/tools/feel_trace/` (`trace.cs`, `mkboot.py`, `runtrace.sh`,
`sig.py`, `tracetool.py`, `README.md`), dùng cho đợt 5 refactor feeling: 0 chênh trên 641 frame ở ba level.

MY_SKILL Sổ tay đã ghi luật "refactor giữ chuyển động thì quay trace mỗi frame, chứng minh tất định trước rồi mới so". Luật ở
Horcrux mà công cụ ở một dự án thì dự án sau phải viết lại từ đầu. Plan này đưa phần không phụ thuộc game lên Horcrux; dự án chỉ còn
giữ một file kịch bản: ghi entity nào, gõ gì ở frame nào.

**Phân công:**
- Task 1 (Horcrux, code C#, Python, shell): developer gõ.
- Task 2 (test EditMode), Task 3 (tài liệu), Task 4 (dự án chuyển sang bản Horcrux, xoá bản tạm): agent làm.

Horcrux là submodule: Task 1–3 commit **trong `Assets/Horcrux`**, Task 4 commit ở repo dự án kèm con trỏ submodule.

Plan độc lập với `Horcrux_HopCurve_DipFlash_Plan.md`. Làm plan này trước thì Task 4 của plan kia quay trace bằng bản Horcrux.

---

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | dự án này: mọi refactor tween, trình diễn · dự án sau: cùng việc, thay kịch bản |
| Mục tiêu | một lõi quay, một định dạng file, một công cụ so cho mọi dự án; kịch bản là thứ duy nhất dự án viết |
| Chỗ đặt | C# ở `Editor/FeelTrace/` (assembly `com.horcrux.editor`), script chạy ngoài Unity ở `Editor/FeelTrace/Tools~/`: thư mục đuôi `~` Unity bỏ qua khi import, không sinh `.meta`, không lẫn vào build |
| Chạy thế nào | không đổi so với bản tạm: Unity CLI vào Play, `script eval_file` chạy kịch bản của dự án, kịch bản gọi lõi Horcrux đã compile sẵn |
| Tên | đề xuất, developer đổi tuỳ ý: `FeelTraceRecorder`, `FeelTraceWriter`, `FeelTraceInput`, `feeltrace.py`, `feeltrace.sh` |

**Phần nào lên, phần nào ở lại**

| Phần của bản tạm | Đi đâu | Vì |
|---|---|---|
| khoá fps, vòng `PostLateUpdate`, chờ rảnh, bắt log lỗi, ghi file và cờ `.done` | Horcrux `FeelTraceRecorder` | không gọi tên type nào của game |
| `F()`, `V3()`, nối khoá entity | Horcrux `FeelTraceWriter` | định dạng file phải một nguồn với công cụ so |
| `Down`, `Up`, `Click` qua `ExecuteEvents` | Horcrux `FeelTraceInput` | mọi game bấm bằng EventSystem đều gõ như nhau |
| `tracetool.py diff`, `sig.py`, phần thay đường dẫn của `mkboot.py` | Horcrux `feeltrace.py` | chuẩn hoá khoá entity và so theo dung sai là của định dạng, không của game |
| `runtrace.sh` | Horcrux `feeltrace.sh` | chỉ thêm tham số đường dẫn kịch bản và thư mục `runs` |
| ghi tile, box, ô cache bằng reflection; kịch bản gõ frame 0 → 470; bấm `btnPlay` của menu và seed `Random` | dự án, một file `trace.cs` | gọi tên type, field private và luồng menu của game |
| `tracetool.py summary` | bỏ | đọc theo vị trí token của tile category-jam; chưa ai dùng ngoài lúc dò |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Ghi tự động mọi `Transform` trong scene | file phình hàng trăm lần, chênh ở object không liên quan che chênh thật; kịch bản chọn thứ nhìn thấy được là quyết định của người refactor |
| Bước bấm menu và seed dùng chung | mỗi game vào level một kiểu; chưa có người dùng thứ hai |
| Chạy từ menu Editor hay test runner | eval của Unity CLI đã chạy được trong Play và không cần domain reload khi đổi kịch bản |
| Định dạng nhị phân, nén | file 641 frame cỡ vài MB, so bằng Python trong vài giây |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| Đổi định dạng file: khoá entity mở bằng ` \|` thay vì nhận diện bằng chữ cái đầu `T`, `B`, `C` | regex của bản tạm chỉ biết ba loại entity của category-jam; ` \|` là mốc tách không phụ thuộc game. Mốc trace cũ không so được với trace mới, Task 4 chứng minh tương đương bằng đổi định dạng file cũ |
| Entity không có khoá ổn định ghi `kind?`, công cụ đánh số theo thứ tự trong frame | ID box category-jam là category đổi mỗi phiên; dự án nào cũng có ID ngẫu nhiên kiểu này. Thứ tự ghi trong frame phải ổn định, kịch bản lo bằng sort |
| Chữ ký là dòng riêng `SIG` do kịch bản ghi, tách khỏi `NOTES` | bản tạm suy chữ ký từ ghi chú và thứ tự box ở frame đầu, tức công cụ biết về box. Kịch bản biết thứ gì quyết bố cục nên kịch bản ghi |
| Lỗi giữa chừng vẫn ghi file và cờ `.done` | bản tạm chết giữa chừng thì runner chờ tới timeout rồi báo "timeout", không thấy lỗi |
| Runner thoát Play trước khi báo timeout | bản tạm để Editor kẹt trong Play khi timeout |
| Kịch bản trả chuỗi `"feeltrace"` | runner nhận ra eval đã chạy tới cuối; eval hay rớt ngay sau khi vào Play nên phải thử lại |

---

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| dự án `docs/tools/feel_trace/` | toàn bộ lõi, định dạng số `F5` culture bất biến, dung sai `2e-4`, 641 frame, chờ rảnh ít nhất 90 frame | `summary` (bảng trên) |
| Horcrux `Editor/com.horcrux.editor.asmdef` | chỗ đặt; hiện chỉ tham chiếu `com.horcrux.runtime` | cần thêm `UniTask` và `UnityEngine.UI` (Task 1.5) |
| Horcrux `Tests/PlayMode/AllocationMeter.cs` | — | đo GC, khác việc: trace đo giá trị nhìn thấy |
| MY_SKILL Sổ tay "refactor phải giữ nguyên chuyển động" | luật và cái bẫy seed `Random` | — |

---

## Task 1 — Horcrux (developer gõ)

Thứ tự: 1.6 trước 1.1–1.3 vì cần tham chiếu assembly. Xong mỗi bước compile sạch.

### 1.1 `Editor/FeelTrace/FeelTraceWriter.cs` — file mới

```csharp
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Horcrux.Editor.FeelTrace
{
    /// <summary>
    /// Ghi trạng thái một frame thành token: " |khoá" mở một entity, mỗi giá trị là một token sau một dấu cách.
    /// Số ghi F5 culture bất biến; vector ghi "x,y,z" thành một token.
    /// </summary>
    public sealed class FeelTraceWriter
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private readonly StringBuilder _sb;

        public FeelTraceWriter(StringBuilder sb) => _sb = sb;

        /// <summary>Entity có khoá ổn định giữa các lần chạy, ví dụ chỉ số trong một danh sách cố định.</summary>
        public FeelTraceWriter Entity(string kind, int index)
        {
            _sb.Append(" |").Append(kind).Append(index);
            return this;
        }

        /// <summary>Entity không có khoá ổn định, ví dụ ID ngẫu nhiên mỗi phiên: feeltrace.py đánh số theo thứ tự ghi trong frame.</summary>
        public FeelTraceWriter EntityByOrder(string kind)
        {
            _sb.Append(" |").Append(kind).Append('?');
            return this;
        }

        public FeelTraceWriter Value(float value)
        {
            _sb.Append(' ');
            AppendFloat(value);
            return this;
        }

        public FeelTraceWriter Value(Vector3 value)
        {
            _sb.Append(' ');
            AppendFloat(value.x);
            _sb.Append(',');
            AppendFloat(value.y);
            _sb.Append(',');
            AppendFloat(value.z);
            return this;
        }

        public FeelTraceWriter Value(int value)
        {
            _sb.Append(' ').Append(value);
            return this;
        }

        public FeelTraceWriter Value(bool value) => Value(value ? 1 : 0);

        /// <summary>Token chữ, so khớp tuyệt đối. Không chứa dấu cách hay '|'.</summary>
        public FeelTraceWriter Value(string value)
        {
            _sb.Append(' ').Append(value);
            return this;
        }

        private void AppendFloat(float value) => _sb.Append(value.ToString("F5", Invariant));
    }
}
```

### 1.2 `Editor/FeelTrace/FeelTraceRecorder.cs` — file mới

```csharp
using System;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Horcrux.Editor.FeelTrace
{
    /// <summary>
    /// Quay trạng thái mỗi frame ở PostLateUpdate với fps khoá, để hai lần chạy cùng kịch bản so được từng frame.
    /// Kịch bản cấp record (ghi gì) và act (gõ gì ở frame nào); recorder lo khoá fps, bắt log lỗi, ghi file và cờ .done.
    /// </summary>
    public sealed class FeelTraceRecorder
    {
        private readonly string _outPath;
        private readonly int _frameCount;
        private readonly int _captureFramerate;
        private readonly StringBuilder _trace = new(1 << 22);
        private readonly StringBuilder _log = new();
        private readonly FeelTraceWriter _writer;

        /// <summary>Ghi chú tự do trên một dòng: chỉ để người đọc.</summary>
        public StringBuilder Notes { get; } = new();

        /// <summary>Khoá so sánh trên một dòng: thứ quyết bố cục ban đầu. Chỉ so hai file cùng chữ ký.</summary>
        public StringBuilder Signature { get; } = new();

        public FeelTraceRecorder(string outPath, int frameCount, int captureFramerate = 60)
        {
            _outPath = outPath;
            _frameCount = frameCount;
            _captureFramerate = captureFramerate;
            _writer = new FeelTraceWriter(_trace);
        }

        public static string DoneFlagPath(string outPath) => outPath + ".done";

        /// <summary>
        /// Chờ isReady đúng và ít nhất minIdleFrames frame, rồi mỗi frame: record ghi trạng thái, act gõ kịch bản với chỉ số frame.
        /// Lỗi của act ghi vào LOG và chạy tiếp; lỗi khác dừng quay nhưng vẫn ghi file và cờ .done.
        /// </summary>
        public void Start(Func<bool> isReady, int minIdleFrames, Action<FeelTraceWriter> record, Action<int> act)
        {
            File.Delete(DoneFlagPath(_outPath));
            RunAsync(isReady, minIdleFrames, record, act).Forget();
        }

        private async UniTaskVoid RunAsync(Func<bool> isReady, int minIdleFrames, Action<FeelTraceWriter> record, Action<int> act)
        {
            Application.logMessageReceived += OnLog;
            Time.captureFramerate = _captureFramerate;
            try
            {
                var waited = 0;
                while (!isReady() || waited < minIdleFrames)
                {
                    waited++;
                    await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
                }

                for (var frame = 0; frame < _frameCount; frame++)
                {
                    _trace.Append('F').Append(frame);
                    record(_writer);
                    _trace.Append('\n');
                    try
                    {
                        act(frame);
                    }
                    catch (Exception e)
                    {
                        _log.Append("act frame ").Append(frame).Append(": ").Append(e).Append('\n');
                    }
                    await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
                }
            }
            catch (Exception e)
            {
                _log.Append("recorder: ").Append(e).Append('\n');
            }
            finally
            {
                Time.captureFramerate = 0;
                Application.logMessageReceived -= OnLog;
                File.WriteAllText(_outPath, "NOTES " + Notes + "\nSIG " + Signature + "\nLOG\n" + _log + "\nTRACE\n" + _trace);
                File.WriteAllText(DoneFlagPath(_outPath), "1");
            }
        }

        private void OnLog(string message, string stackTrace, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert)
                _log.Append('[').Append(type).Append("] ").Append(message).Append('\n').Append(stackTrace).Append('\n');
        }
    }
}
```

### 1.3 `Editor/FeelTrace/FeelTraceInput.cs` — file mới

```csharp
using UnityEngine;
using UnityEngine.EventSystems;

namespace Horcrux.Editor.FeelTrace
{
    /// <summary>Gõ pointer giả lên một GameObject qua EventSystem: đi đúng đường input thật, lặp lại y hệt giữa các lần quay.</summary>
    public static class FeelTraceInput
    {
        public static void Down(GameObject target) => ExecuteEvents.Execute(target, NewPointer(), ExecuteEvents.pointerDownHandler);

        public static void Up(GameObject target) => ExecuteEvents.Execute(target, NewPointer(), ExecuteEvents.pointerUpHandler);

        public static void Click(GameObject target) => ExecuteEvents.Execute(target, NewPointer(), ExecuteEvents.pointerClickHandler);

        /// <summary>Down, Up, Click trong cùng frame.</summary>
        public static void Tap(GameObject target)
        {
            Down(target);
            Up(target);
            Click(target);
        }

        private static PointerEventData NewPointer() => new(EventSystem.current) { button = PointerEventData.InputButton.Left };
    }
}
```

### 1.4 `Editor/FeelTrace/Tools~/feeltrace.py` — file mới

```python
"""So trace của Horcrux FeelTraceRecorder. Xem Editor/FeelTrace/FeelTrace.md.

  feeltrace.py render <script.cs> <đường dẫn file ra, kiểu Windows> <file .cs đích>
  feeltrace.py sig <glob>...
  feeltrace.py diff <a.txt> <b.txt> [--tol 2e-4] [--limit 40]
"""
import argparse
import glob
import io
import sys

MARKER = '__TRACE_OUT__'


def render(script, out_path, dest):
    src = io.open(script, encoding='utf8').read()
    if src.count(MARKER) != 1:
        sys.exit('%s phải chứa %s đúng một lần' % (script, MARKER))
    io.open(dest, 'w', encoding='utf8').write(src.replace(MARKER, out_path))


def header(text, name):
    for line in text.split('\nLOG\n', 1)[0].split('\n'):
        if line.startswith(name + ' '):
            return line[len(name) + 1:]
    return ''


def load(path):
    text = io.open(path, encoding='utf8').read()
    head, trace = text.split('\nTRACE\n', 1)
    frames = {}
    for line in trace.strip().split('\n'):
        parts = line.split(' |')
        order = {}
        entities = {}
        for part in parts[1:]:
            key, _, rest = part.partition(' ')
            if key.endswith('?'):
                kind = key[:-1]
                index = order.get(kind, 0)
                order[kind] = index + 1
                key = '%s#%d' % (kind, index)
            entities[key] = rest.split()
        frames[int(parts[0][1:])] = entities
    return head, frames


def numbers(tokens):
    out = []
    for token in tokens:
        for piece in token.split(','):
            try:
                out.append(float(piece))
            except ValueError:
                out.append(piece)
    return out


def differs(x, y, tol):
    if isinstance(x, float) and isinstance(y, float):
        return abs(x - y) > tol
    return x != y


def diff(a, b, tol, limit):
    head_a, frames_a = load(a)
    head_b, frames_b = load(b)
    print('A', header(head_a, 'SIG'))
    print('B', header(head_b, 'SIG'))
    count = 0
    for frame in sorted(set(frames_a) | set(frames_b)):
        ea, eb = frames_a.get(frame, {}), frames_b.get(frame, {})
        for key in sorted(set(ea) | set(eb)):
            if key not in ea or key not in eb:
                count += 1
                if count <= limit:
                    print('F%d %s chỉ có ở %s' % (frame, key, 'A' if key in ea else 'B'))
                continue
            va, vb = numbers(ea[key]), numbers(eb[key])
            bad = [i for i, (x, y) in enumerate(zip(va, vb)) if differs(x, y, tol)]
            if len(va) != len(vb) or bad:
                count += 1
                if count <= limit:
                    print('F%d %s token %s\n  A %s\n  B %s' % (frame, key, bad[:6], ' '.join(ea[key]), ' '.join(eb[key])))
    print('mismatches:', count)
    return count


def main():
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest='command', required=True)
    p = sub.add_parser('render')
    p.add_argument('script')
    p.add_argument('out_path')
    p.add_argument('dest')
    p = sub.add_parser('sig')
    p.add_argument('patterns', nargs='+')
    p = sub.add_parser('diff')
    p.add_argument('a')
    p.add_argument('b')
    p.add_argument('--tol', type=float, default=2e-4)
    p.add_argument('--limit', type=int, default=40)
    args = parser.parse_args()
    if args.command == 'render':
        render(args.script, args.out_path, args.dest)
    elif args.command == 'sig':
        for pattern in args.patterns:
            for path in sorted(glob.glob(pattern)):
                print(path, '=>', header(io.open(path, encoding='utf8').read(), 'SIG'))
    else:
        sys.exit(1 if diff(args.a, args.b, args.tol, args.limit) else 0)


if __name__ == '__main__':
    main()
```

### 1.5 `Editor/FeelTrace/Tools~/feeltrace.sh` — file mới

```bash
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
for i in $(seq 1 10); do
  out=$("$U" cmd --project-path "$P" --result-only script eval_file --file "$(cygpath -w "$WORK/run.cs")" 2>&1)
  echo "$out" | grep -q '"feeltrace"' && break
  echo "retry eval: $(echo "$out" | head -c 200)"; sleep 3; waitready
done
for i in $(seq 1 80); do [ -f "$OUT.done" ] && break; sleep 3; done
"$U" cmd --project-path "$P" editor.playmode stop >/dev/null 2>&1
[ -f "$OUT.done" ] || { echo "trace timeout"; exit 1; }
mv "$OUT" "$RUNS/$NAME.txt"
rm -rf "$WORK"
python -X utf8 -I "$TOOLS/feeltrace.py" sig "$RUNS/$NAME.txt"
```

### 1.6 `Editor/com.horcrux.editor.asmdef` — thêm hai tham chiếu

```json
    "references": [
        "com.horcrux.runtime",
        "UniTask",
        "UnityEngine.UI"
    ],
```

**Editor setup:** tạo ba file C# trong Project window của Unity (để có `.meta`). Hai file trong `Tools~` tạo bằng trình soạn thảo
ngoài: Unity không thấy thư mục đó.

---

## Task 2 — Test EditMode (agent)

`Tests/EditMode/FeelTraceWriterTests.cs`; asmdef test EditMode thêm tham chiếu `com.horcrux.editor` nếu chưa có.

| Gọi | Kỳ vọng chuỗi |
|---|---|
| `Entity("T", 3).Value(1.5f)` | ` \|T3 1.50000` |
| `EntityByOrder("B").Value(new Vector3(1, -2, 0.000004f))` | ` \|B? 1.00000,-2.00000,0.00000` |
| `Value(true).Value(false).Value(7)` | ` 1 0 7` |
| `Value(0.5f)` khi `CultureInfo.CurrentCulture` là `de-DE` | ` 0.50000`, không phải `0,50000` |

`feeltrace.py`: Task 4 kiểm bằng chạy thật (render, sig, diff hai lần quay cùng chữ ký, diff file cũ đổi định dạng).

## Task 3 — Tài liệu (agent)

`Editor/FeelTrace/FeelTrace.md`:
- Khi nào dùng: refactor tween, trình diễn mà phải giữ nguyên; không thay chơi thử, chỉ chứng minh "không đổi".
- Hợp đồng kịch bản: chứa `__TRACE_OUT__` đúng một lần, cuối file `return "feeltrace";`, tự vào level rồi gọi `Start`.
- Định dạng file: `NOTES`, `SIG`, `LOG`, `TRACE`; dòng `F<n>`, entity ` |kind<index>` hoặc ` |kind?`, token.
- Trước khi chạy: Unity CLI `ready`, scene khởi động, Game view đúng cỡ, Run In Background và auto-tick khi Editor không focus.
- Bẫy tất định: seed `Random` ngay trước khi vào level chưa đủ vì thời gian nạp đổi số lần gọi; chữ ký ghi thứ quyết bố cục, chạy lại
  tới khi trùng; thứ tự ghi entity `?` phải ổn định; save không đổi giữa hai lần quay.
- Quy trình: quay mốc hai lần cùng chữ ký, 0 chênh; sửa; quay lại; 0 chênh.
- Một kịch bản mẫu rút gọn từ category-jam.

MY_SKILL Sổ tay "refactor phải giữ nguyên chuyển động": thêm một câu trỏ `Editor/FeelTrace/FeelTrace.md`.

## Task 4 — Dự án dùng bản Horcrux, xoá bản tạm (agent)

| File dự án | Đổi |
|---|---|
| `docs/tools/feel_trace/trace.cs` | thành kịch bản duy nhất: phần bấm `btnPlay` và seed của `mkboot.py` lên đầu file trong một hàm async; `F`, `V3`, `sb.Append(" T")` → `FeelTraceWriter`; box ghi `EntityByOrder("B")`; chỉ số tile được chọn và thứ tự ID box ở frame 0 ghi vào `Signature`; `Down`, `Up`, `Click` → `FeelTraceInput`; vòng chờ và vòng frame → `FeelTraceRecorder.Start(…, 90, Record, Act)` với 641 frame; cuối file `return "feeltrace";` |
| `docs/tools/feel_trace/mkboot.py`, `runtrace.sh`, `sig.py`, `tracetool.py` | xoá |
| `docs/tools/feel_trace/README.md` | còn bảng kịch bản và lệnh `bash Assets/Horcrux/Editor/FeelTrace/Tools~/feeltrace.sh docs/tools/feel_trace/trace.cs docs/tools/feel_trace/runs <tên>`; phần chung trỏ `FeelTrace.md` |

**Nghiệm thu:**
- Quay hai lần cùng chữ ký bằng bản mới: `feeltrace.py diff` báo 0 chênh, mã thoát 0.
- Tương đương bản tạm: một file `runs/` cũ cùng chữ ký, đổi định dạng bằng script nháp ở scratchpad (` T<i>` → ` |T<i>`,
  ` B<id>` → ` |B?`, ` C<i>` → ` |C<i>`, tách dòng `NOTES` thành `NOTES` và `SIG`), diff với file mới: 0 chênh.
- Kịch bản cố ý ném exception ở một frame: file vẫn ghi, `LOG` có dòng `act frame`, runner không timeout.
- grep `mkboot`, `runtrace.sh`, `tracetool.py` trong `docs/` chỉ còn ở lịch sử plan.

---

## Mở rộng sau

- Bước vào level dùng chung (chờ nút theo tên, seed, bấm, chờ điều kiện) khi dự án thứ hai viết kịch bản và vào level giống thế.
- `feeltrace.py summary` tổng quát: in khi một entity đổi token ở vị trí cho trước, nếu người dò cần lại.
