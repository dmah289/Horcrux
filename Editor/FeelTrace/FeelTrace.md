# FeelTrace — quay trạng thái mỗi frame để chứng minh refactor giữ nguyên chuyển động

Quay trạng thái nhìn thấy được của các entity ở cuối mỗi frame, với fps khoá, trong một kịch bản gõ cố định. Hai lần quay cùng kịch bản
và cùng bố cục ban đầu cho ra file khớp 0 chênh; refactor giữ chuyển động thì trace trước và sau cũng khớp 0 chênh.

**Chỉ chứng minh "không đổi", không thay chơi thử.** Cảm giác "đã tay" vẫn chỉ developer nghiệm thu được bằng tay.

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | agent, khi refactor tween, trình diễn mà phải giữ nguyên; developer đọc kết quả diff |
| Ranh giới | Horcrux giữ lõi quay, định dạng file, công cụ so, runner. Dự án giữ **một file kịch bản**: ghi entity nào, gõ gì ở frame nào, vào level thế nào |
| Chỗ đặt | C# ở `Editor/FeelTrace/` (assembly `com.horcrux.editor`). Python, shell ở `Editor/FeelTrace/Tools~/`: thư mục đuôi `~` Unity không import, không sinh `.meta`, không vào build |
| Chạy thế nào | Unity CLI vào Play, `script eval_file` chạy kịch bản; kịch bản gọi lõi đã compile sẵn. Đổi kịch bản không cần domain reload |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Ghi tự động mọi `Transform` trong scene | file phình hàng trăm lần, chênh ở object không liên quan che chênh thật; chọn thứ nhìn thấy được là quyết định của người refactor |
| Bước vào level dùng chung (chờ nút, seed, bấm) | mỗi game vào level một kiểu; chưa có người dùng thứ hai |
| Chạy từ menu Editor hay Test Runner | eval của Unity CLI đã chạy được trong Play, không domain reload khi đổi kịch bản |
| Định dạng nhị phân, nén | file 641 frame cỡ 4 MB, so bằng Python trong vài giây |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| Khoá entity mở bằng ` \|` | mốc tách không phụ thuộc game; nhận diện bằng chữ cái đầu buộc công cụ biết loại entity của từng game |
| Entity không có khoá ổn định ghi `kind?`, công cụ đánh số theo thứ tự trong frame | ID kiểu category đổi mỗi phiên; kịch bản lo thứ tự ghi ổn định |
| Chữ ký là dòng `SIG` riêng, kịch bản ghi | kịch bản biết thứ gì quyết bố cục; công cụ không suy được mà không biết game |
| Lỗi giữa chừng, kể cả thoát Play, vẫn ghi file và cờ `.done` | runner thấy lỗi trong `LOG` ngay, không chờ tới timeout |
| Runner dừng ngay khi kịch bản lỗi compile, thoát Play trước khi báo timeout | lỗi compile thử lại không bao giờ qua; Play bỏ treo làm lần quay sau hỏng |

## Luồng dữ liệu

```
kịch bản .cs của dự án (chứa __TRACE_OUT__ đúng một lần)
   │  feeltrace.sh: feeltrace.py render thay __TRACE_OUT__ bằng đường dẫn file ra (kiểu Windows) → run.cs trong thư mục tạm
   ▼
Unity CLI: playmode stop → start → script eval_file run.cs   (thử lại tới 10 lần tới khi eval trả "feeltrace")
   │
   ▼  thân eval (kịch bản)
Boot async: vào level (bấm menu, seed Random…) → dựng FeelTraceRecorder → recorder.Start(isReady, minIdleFrames, Record, Act)
   │
   ▼  FeelTraceRecorder.RunAsync, mỗi lượt ở PlayerLoopTiming.PostLateUpdate, Time.captureFramerate khoá
chờ isReady() đúng VÀ đủ minIdleFrames lượt
for frame in 0 .. frameCount-1:  "F<frame>" + Record(writer) + "\n"  →  Act(frame)  →  yield
   │  finally: captureFramerate = 0, gỡ log hook, ghi file, ghi cờ <file>.done
   ▼
feeltrace.sh: thấy .done → playmode stop → mv vào <runs>/<tên>.txt → in chữ ký
   │
   ▼
feeltrace.py sig   → chọn hai file cùng chữ ký
feeltrace.py diff  → "mismatches: N", mã thoát 0 khi N = 0
```

## Định dạng file

```
NOTES <ghi chú tự do, một dòng>
SIG <chữ ký, một dòng>
LOG
<lỗi console, lỗi Act, kiểm phụ của kịch bản; mỗi dòng kết thúc bằng \n>
TRACE
F0 |T0 0.98086,1.66047,0.00000 1.00000 1 InShelf |B? … |C0 …
F1 …
```

| Phần | Luật |
|---|---|
| Dòng frame | `F<n>`, n tính từ lúc level rảnh |
| Entity | ` \|<kind><index>` khi có khoá ổn định · ` \|<kind>?` khi không; `feeltrace.py` đổi thành `<kind>#<thứ tự trong frame>` |
| Token | mỗi giá trị sau một dấu cách. `float` ghi `F5` culture bất biến · `Vector3` là một token `x,y,z` · `bool` ghi `1`/`0` · `int` nguyên · chuỗi so khớp tuyệt đối, không chứa dấu cách hay `\|` |
| So | `float` lệch quá dung sai (`--tol`, mặc định `2e-4`) là chênh · token khác loại hay khác số token là chênh · entity chỉ có ở một bên là chênh |

`LOG` không vào `TRACE` nên không làm lệch diff: kịch bản dùng nó cho kiểm phụ (ví dụ đếm lượt renderer bật mà không vẽ gì).

## API

### C# — namespace `Horcrux.Editor.FeelTrace`

| Member | Hợp đồng |
|---|---|
| `FeelTraceRecorder(string outPath, int frameCount, int captureFramerate = 60)` | `outPath` đường dẫn tuyệt đối file ra |
| `StringBuilder Notes` | một dòng, chỉ để người đọc |
| `StringBuilder Signature` | một dòng: thứ quyết bố cục ban đầu. Ghi được tới lúc quay xong (thường ở frame 0 và ở các `Act` chọn entity) |
| `StringBuilder Log` | kịch bản thêm dòng kết thúc bằng `'\n'` |
| `void Start(Func<bool> isReady, int minIdleFrames, Action<FeelTraceWriter> record, Action<int> act)` | xoá cờ `.done` cũ rồi chạy nền. `act` ném thì ghi `act frame <n>: …` vào `LOG` và quay tiếp; lỗi khác (kể cả `record` ném) hay thoát Play thì dừng, ghi `recorder: …`, vẫn ghi file |
| `static string DoneFlagPath(string outPath)` | `outPath + ".done"` |
| `FeelTraceWriter.Entity(string kind, int index)` · `EntityByOrder(string kind)` | mở entity; trả lại writer để nối chuỗi |
| `FeelTraceWriter.Value(float)` · `Value(Vector3)` · `Value(int)` · `Value(bool)` · `Value(string)` | thêm một token |
| `FeelTraceInput.Down` · `Up` · `Click` · `Tap(GameObject)` | pointer giả trái qua `ExecuteEvents` của `EventSystem.current`; `Tap` = Down, Up, Click cùng frame |

### Script ngoài Unity — `Editor/FeelTrace/Tools~/`

| Lệnh | Làm gì |
|---|---|
| `bash feeltrace.sh <kịch bản .cs> <thư mục runs> <tên run>` | quay một trace vào `<runs>/<tên>.txt`, in chữ ký. Chạy từ gốc project hoặc đặt `PROJECT_PATH`; `UNITY_CLI` đổi đường dẫn `unity.exe` (mặc định `C:/Users/<user>/AppData/Local/Unity/bin/unity.exe`). Mã thoát khác 0 khi kịch bản lỗi compile, không chạy, hay timeout (4 phút) |
| `python -X utf8 -I feeltrace.py render <script.cs> <file ra kiểu Windows> <.cs đích>` | runner gọi; dừng nếu `__TRACE_OUT__` không xuất hiện đúng một lần |
| `python -X utf8 -I feeltrace.py sig <glob>…` | in `đường dẫn => chữ ký`, chỉ đọc phần đầu file |
| `python -X utf8 -I feeltrace.py diff <a> <b> [--tol 2e-4] [--limit 40]` | in hai chữ ký, tối đa `limit` chỗ chênh, rồi `mismatches: N`; mã thoát 0 khi N = 0 |

## Hợp đồng kịch bản

Kịch bản là **thân của một hàm** do `eval_file` biên dịch: không có `using`, mọi type viết đủ namespace; local function và
`async UniTaskVoid` dùng được.

1. Chứa chuỗi `__TRACE_OUT__` **đúng một lần** — kể cả trong comment — ở dòng `var outPath = @"__TRACE_OUT__";`.
2. Tự vào trạng thái cần quay (bấm menu, seed, chờ level nạp) trong một hàm async, rồi gọi `recorder.Start(...)`.
3. `Record` ghi entity theo **thứ tự ổn định** giữa các lần chạy (duyệt danh sách cố định, hoặc sort theo `InstanceID`).
4. `Act` dùng `FeelTraceInput` cho input — đi đúng đường input thật — và ghi lựa chọn quyết bố cục vào `Signature`.
5. Dòng cuối `return "feeltrace";` — runner dựa vào chuỗi này để biết eval đã chạy tới cuối.

Kịch bản mẫu rút gọn (bản đầy đủ: `docs/tools/feel_trace/trace.cs` của category-jam):

```csharp
var outPath = @"__TRACE_OUT__";
var recorder = new Horcrux.Editor.FeelTrace.FeelTraceRecorder(outPath, 641);
MyGame.Director director = null;
List<MyGame.Tile> tiles = null;

void Record(Horcrux.Editor.FeelTrace.FeelTraceWriter w)
{
    for (int i = 0; i < tiles.Count; i++)
        w.Entity("T", i).Value(tiles[i].transform.position).Value(tiles[i].transform.localScale).Value(tiles[i].IsAnimating);
}

void Act(int frame)
{
    if (frame == 0)
    {
        recorder.Signature.Append("A=").Append(0);
        Horcrux.Editor.FeelTrace.FeelTraceInput.Tap(tiles[0].gameObject);
    }
}

async Cysharp.Threading.Tasks.UniTaskVoid Boot()
{
    Time.captureFramerate = 60;
    UnityEngine.Random.InitState(20261026);
    // bấm vào level theo kiểu của game, rồi chờ:
    while (director == null || !director.LevelLoaded)
    {
        director = UnityEngine.Object.FindFirstObjectByType<MyGame.Director>();
        await Cysharp.Threading.Tasks.UniTask.Yield();
    }
    tiles = director.Tiles.ToList();
    recorder.Start(() => !director.IsAnimating, 90, Record, Act);
}

Boot().Forget();
return "feeltrace";
```

## Trước khi chạy

| # | Việc | Thiếu thì |
|---|---|---|
| 1 | Unity Editor mở project; Unity CLI và package `com.unity.pipeline` đã cài; `unity status --project-path <project>` báo `ready` | runner báo `pipeline not ready` |
| 2 | Không có lỗi compile trong project | Editor vào Safe Mode, pipeline không nạp |
| 3 | Scene khởi động của game đang mở (scene kịch bản chờ nút vào level) | kịch bản chờ mãi, runner báo timeout |
| 4 | Game view giữ **một** độ phân giải cho mọi lần quay sẽ so với nhau | layout co giãn theo màn thì vị trí lệch ở mọi frame |
| 5 | Bật Run In Background (MCP `set_run_in_background`, chỉ ghi đè trong Editor, không vào build) và auto-tick khi Editor không focus (MCP `set_auto_tick`) | frame đứng khi Editor mất focus, runner báo timeout |
| 6 | Không sửa save giữa hai lần quay | level hay bố cục khác, chữ ký không bao giờ trùng |

## Quy trình cho agent

1. **Kịch bản phủ chuyển động sắp sửa chưa?** Chưa thì thêm `case` vào `Act` và entity, giá trị vào `Record` **trước** khi quay mốc —
   thêm sau là mốc cũ không còn so được.
2. **Quay mốc trên code chưa sửa**: `feeltrace.sh … before_1`, `before_2`… tới khi có **hai file cùng chữ ký**; `diff` hai file đó
   phải ra `mismatches: 0` — chứng minh kịch bản tất định trên máy này trước khi tin bất cứ phép so nào.
3. **Sửa code.** Commit hay không tuỳ task; không đổi save, scene, Game view.
4. **Quay sau sửa**: `after_1`, `after_2`… tới khi chữ ký trùng một file mốc.
5. `diff before_k after_m` cùng chữ ký: `mismatches: 0` là giữ nguyên chuyển động. Chênh thì đọc frame và token đầu tiên lệch —
   lệch thật thì sửa lại code, không nới `--tol`.
6. Báo cáo: tên file, chữ ký, số frame, số chênh. Phần cảm giác vẫn giao kịch bản chơi thử cho developer.

Một lần quay khoảng 30 giây; `runs/` của dự án để ngoài git.

## Bẫy tất định

- **Seed `Random` ngay trước lúc vào level chưa đủ**: thời gian nạp đổi số lần gọi `Random`, hai lần chạy ra hai bố cục. Vì vậy so
  theo **chữ ký**, chạy lại tới khi trùng (thường 1–3 lần). *Đã sai một lần:* tin seed là đủ, so hai trace khác bố cục ban đầu.
- **Chữ ký phải ghi đủ thứ quyết bố cục**, gồm cả **thứ tự** — cùng tập box nhưng đảo thứ tự ô là bố cục khác (đã đo: 753 chênh).
- **Thứ tự ghi entity `?` phải ổn định**: duyệt theo thứ tự có bảo đảm (`FindObjectsSortMode.InstanceID`, danh sách cố định).
- **Dữ liệu ngẫu nhiên sinh giữa kịch bản** (entity mới vào sau một sự kiện) làm hai lần cùng chữ ký lệch nhau từ đó trở đi; đọc
  frame đầu lệch để phân biệt với chênh thật, hoặc ghi lựa chọn đó vào chữ ký.
- **Save, Game view, scene khởi động** không đổi giữa các lần quay (mục "Trước khi chạy").

## An toàn và lỗi

| Ca | Kết quả |
|---|---|
| `Act` ném | `LOG` có `act frame <n>: <exception>`, quay tiếp đủ frame |
| `Record` hay `isReady` ném | dừng, `LOG` có `recorder: …`, file chỉ có tới frame đó |
| Thoát Play giữa chừng | token `Application.exitCancellationToken` huỷ vòng chờ, `LOG` có `recorder: OperationCanceledException`, file vẫn ghi |
| Lỗi console (`Error`, `Exception`, `Assert`) trong lúc quay | vào `LOG` kèm stack trace |
| Kịch bản lỗi compile | runner in lỗi đầy đủ, thoát Play, mã thoát 1 sau vài giây |
| Không có cờ `.done` sau 4 phút | runner thoát Play, báo `trace timeout`, giữ thư mục tạm để xem |

`Time.captureFramerate` về 0 ở mọi đường ra.

## Hiệu năng

Mỗi frame ghi vài trăm số, 641 frame ra ~4 MB; trong Editor vẫn giữ đường ghi không rác:

| Chỗ | Cách | Đo |
|---|---|---|
| `FeelTraceWriter` số `float`, `int` | `TryFormat` vào `stackalloc` rồi `Append(ReadOnlySpan<char>)`; `StringBuilder.Append(int)` của Mono cấp phát một chuỗi | test `Value_DoesNotAllocate` 0 B, đối chứng `Meter_SeesAllocation_WhenFloatGoesThroughToString` thấy cấp phát |
| Ghi file | `StreamWriter.Write(StringBuilder)` từng phần, không nối chuỗi | bỏ hai lần chép trace nhiều MB |
| `feeltrace.py diff` | so chuỗi token thô trước, chỉ tách số khi khác | phần lớn entity mỗi frame trùng từng ký tự |
| `feeltrace.py sig` | đọc từng dòng tới `LOG` | không nạp cả trace |

Kịch bản dự án nên giữ cùng luật ở `Record`: reflection và tra component một lần lúc boot (hoặc cache theo instance), không mỗi frame.

## Kiểm thử

- `Tests/EditMode/FeelTraceWriterTests.cs`: định dạng token, culture `de-DE`, biên `float.MaxValue`/`NaN`/`int.MinValue`, 0 cấp phát
  kèm đối chứng.
- Runner và `feeltrace.py`: kiểm bằng chạy thật — hai lần quay cùng chữ ký 0 chênh; `Act` ném vẫn đủ frame; thoát Play giữa chừng vẫn
  có file; kịch bản lỗi compile dừng ngay.

## Mở rộng sau

- Bước vào level dùng chung (chờ nút theo tên, seed, bấm, chờ điều kiện) khi dự án thứ hai viết kịch bản và vào level giống thế.
- `feeltrace.py summary`: in khi một entity đổi token ở vị trí cho trước, nếu người dò cần.
