# Probe — chạy script đo trong Play và đọc kết quả gọn

Bộ công cụ cho agent đo thứ developer **không cảm được** bằng chơi thử: trạng thái nội bộ, số đo hiệu năng, bố cục prefab, ảnh chụp
để tự kiểm lỗi hình. Một lệnh shell chạy script trong Play và in kết quả; phần khung lặp lại (vào Play, thử lại eval, bắt log lỗi, ghi
file, cờ xong) viết một lần ở đây.

**Không thay chơi thử.** Cảm giác, nhịp, độ "đã tay" developer nghiệm thu bằng tay. Thứ developer chơi là thấy (vị trí, bố cục) thì
kiểm tĩnh trước: `PrefabDump` không cần vào Play.

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | agent, khi chỉnh feeling, dò lỗi trình diễn, đo hiệu năng; quy trình chọn công cụ ở `Skills/feel-tune/SKILL.md` |
| Ranh giới | Horcrux giữ runner, ghi kết quả, reflection, dump prefab, ghép ảnh. Dự án giữ **đường tắt vào trạng thái cần đo** (bấm menu, chờ level, chọn entity), một class Editor của dự án |
| Chỗ đặt | C# ở `Editor/Probe/` (assembly `com.horcrux.editor`). Shell ở `Editor/Probe/Tools~/`: Unity không import, không vào build |
| Quan hệ với FeelTrace | `feeltrace.sh` chạy qua `playrun.sh`; `FeelTraceRecorder` dùng chung cờ `.done` |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Chạy từ menu Editor | eval của Unity CLI chạy được trong Play, đổi script không cần domain reload |
| Tự chỉnh cỡ Game view | Unity CLI không có lệnh; cỡ Game view giữ nguyên giữa các lần Play nên là bước "Trước khi chạy" |
| Vẽ nhãn lên ảnh ghép | thứ tự ảnh cố định (trên trái, theo hàng) là đủ đọc; vẽ chữ cần font và shader |
| `PlayProbe` bọc `UniTask.WaitUntil`, `DelayFrame` | UniTask đã có, gọi thẳng |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| `playrun.sh` in phần kết quả tối đa 4 KB, không in thân trace | kết quả đo nên là bản tóm tắt; trace đọc bằng `feeltrace.py stat`, không đổ vào ngữ cảnh |
| `playrun.sh` in `LOG` đã bỏ stack trace và gộp dòng lặp; file vẫn giữ đủ | lỗi có sẵn của game (thiếu remote config…) lặp hàng trăm dòng stack che mất kết quả; cần stack thì mở file |
| `PlayProbe.Field`, `Call` dò lên từng base | `GetField(NonPublic)` không thấy private khai trên base, script tự viết reflection hay quên |
| `PrefabDump` tính bounds từ `sprite.bounds`, không từ `Renderer.bounds` | prefab asset chưa instantiate thì renderer không có bounds thế giới |

## Luồng dữ liệu

```
script .cs của agent (chứa __OUT__ đúng một lần, dòng cuối return "playrun";)
   │  playrun.sh: thay __OUT__ bằng đường dẫn file tạm kiểu Windows → run.cs
   ▼
Unity CLI: set_run_in_background, set_auto_tick → playmode stop → start → script eval_file run.cs (thử lại tới 10 lần)
   │
   ▼  thân eval
PlayProbe.Run(outPath, async (sb, ct) => { vào trạng thái; đo; sb.Append(kết quả) })
   │  finally: ghi "<kết quả>\nLOG\n<lỗi console, exception>" và cờ <file>.done
   ▼
playrun.sh: thấy .done → playmode stop → chuyển file về <file ra> → in đường dẫn, kết quả (≤ 4 KB), LOG gọn
```

## API

### C# — namespace `Horcrux.Editor.Probe`

| Member | Hợp đồng |
|---|---|
| `PlayProbe.Run(string outPath, Func<StringBuilder, CancellationToken, UniTask> body)` | chạy nền trong đời phiên Play (`Application.exitCancellationToken`). Body ném hay thoát Play vẫn ghi file và cờ `.done`; exception vào `LOG` dạng `probe: …` |
| `PlayProbe.DoneFlagPath(string outPath)` | `outPath + ".done"`; FeelTrace dùng chung |
| `PlayProbe.WaitForActiveAsync(string objectName, CancellationToken ct)` | trả GameObject active đầu tiên mang đúng tên; kiểm mỗi frame ở `Update` |
| `PlayProbe.Field<T>(object target, string fieldName)` · `Call(object target, string methodName, params object[] args)` | instance, public hay không, dò từ type thật lên base. Không có thì ném `MissingFieldException` / `MissingMethodException`; nhiều overload cùng tên thì `AmbiguousMatchException` |
| `PrefabDump.Dump(GameObject root, string pathFilter = null)` | một dòng mỗi object (xem "Đọc PrefabDump"); `pathFilter` giữ dòng có đường dẫn chứa chuỗi đó |
| `PrefabDump.DumpAsset(string assetPath, string pathFilter = null)` | prefab nạp qua `AssetDatabase`, không instantiate, không cần Play |
| `new FrameSheet(RectInt crop, int shrink = 2)` | `crop` theo pixel Game view, gốc dưới trái, `width` 0 là cả màn; `shrink` thu nhỏ nguyên lần |
| `FrameSheet.CaptureAsync(CancellationToken ct)` | chờ cuối frame hiện tại rồi chụp Game view (gồm UI overlay). Gọi trong `Act` của FeelTrace là chụp đúng frame đó |
| `FrameSheet.Save(string pngPath, int columns)` · `Count` · `Dispose()` | ghép lưới: ảnh 0 trên trái, đi theo hàng, khe 4 px xám; `Dispose` huỷ texture đã chụp |
| `FrameSheet.Shrink(...)` · `Compose(...)` | static, dùng khi ghép từ texture có sẵn; texture trả về do người gọi huỷ |

### Shell — `Editor/Probe/Tools~/playrun.sh`

```bash
bash "Assets/Horcrux/Editor/Probe/Tools~/playrun.sh" <script .cs> <file ra> [marker] [token] [timeout giây]
```

| Tham số | Mặc định | Nghĩa |
|---|---|---|
| `marker` | `__OUT__` | chuỗi được thay bằng đường dẫn file ra kiểu Windows; phải xuất hiện đúng một lần, kể cả trong comment |
| `token` | `playrun` | script trả chuỗi này ở dòng cuối; runner dựa vào nó để biết eval đã chạy tới cuối |
| `timeout` | `240` | giây chờ cờ `.done`; hết giờ thì thoát Play, báo `timeout`, giữ thư mục tạm |

Chạy từ gốc project hoặc đặt `PROJECT_PATH`; `UNITY_CLI` đổi đường dẫn `unity.exe`. Mã thoát khác 0 khi script lỗi compile (in đủ
lỗi, dừng ngay), không chạy, hay timeout.

## Hợp đồng script

Script là **thân một hàm** do `eval_file` biên dịch: không có `using`, mọi type viết đủ namespace.

```csharp
var outPath = @"__OUT__";
Horcrux.Editor.Probe.PlayProbe.Run(outPath, async (sb, ct) =>
{
    var director = await MyGame.Editor.Probe.MyGameProbe.EnterLevelAsync(1, ct);   // đường tắt của dự án
    await Cysharp.Threading.Tasks.UniTask.DelayFrame(90, cancellationToken: ct);
    var box = UnityEngine.Object.FindFirstObjectByType<MyGame.Box>();
    sb.Append("lidY=").Append(Horcrux.Editor.Probe.PlayProbe.Field<UnityEngine.Transform>(box, "lid").localPosition.y);
});
return "playrun";
```

Kết quả viết thành **bản tóm tắt** (vài dòng số đã tính), không đổ log mỗi frame: đo theo frame thì dùng FeelTrace rồi
`feeltrace.py stat`.

## Đọc PrefabDump

```
sprite_offset/box_anim/lid OFF p=0,0.762,0 s=1,1,1 r=0 | sr target_1_0 Default#30 a=1 root c=0,0.762 size=2.26,2.05
```

| Phần | Nghĩa |
|---|---|
| đường dẫn | từ root, root in tên root |
| `on` / `OFF` | `activeSelf` |
| `p` `s` `r` | `localPosition`, `localScale`, `localEulerAngles.z` |
| `rt a sd anc pv` | RectTransform: `anchoredPosition`, `sizeDelta`, `anchorMin-anchorMax`, `pivot` |
| `sr` | SpriteRenderer: `OFF` khi tắt, tên sprite, `sortingLayer#order`, alpha, `drawMode size` khi không Simple, `root c size` là tâm và cỡ khung bao trong hệ toạ độ root |
| `sg` | SortingGroup `layer#order` |
| cuối dòng | component còn lại; `Missing` là script mất |

## Trước khi chạy

| # | Việc | Thiếu thì |
|---|---|---|
| 1 | Unity Editor mở project; Unity CLI và package `com.unity.pipeline` đã cài; `unity status --project-path <project>` báo `ready` | runner báo `pipeline not ready` |
| 2 | Không có lỗi compile trong project | Editor vào Safe Mode, pipeline không nạp |
| 3 | Scene khởi động của game đang mở, nếu script vào level qua menu | script chờ mãi, runner báo timeout |
| 4 | Game view giữ một độ phân giải cho mọi lần đo so với nhau, và cho mọi `crop` của `FrameSheet` | toạ độ, vùng cắt lệch |

Run In Background và auto-tick runner tự bật, và trả lại khi thoát ở mọi đường ra (override ghi vào `ProjectSettings/Packages/com.unity.pipeline`).

## Bẫy

- **Eval hay rớt ngay sau khi vào Play** ("domain reloading" hay chưa có kết quả): runner thử lại tới 10 lần. Script có tác dụng phụ trước
  `PlayProbe.Run` (ghi file, đổi state) thì phải chịu được chạy hai lần.
- **Lệnh Unity CLI đổi cài đặt Editor ghi vào file của project**: override Run In Background nằm trong
  `ProjectSettings/Packages/com.unity.pipeline/EditorPipelineConfig.json`. *Đã sai một lần:* runner bật mà không trả, file hiện trong
  `git status` của dự án sau mỗi lần đo. Runner trả lại bằng `trap … EXIT`; thêm lệnh cài đặt mới thì trả lại cùng chỗ.
- **`FindObjectsByType` mỗi frame cấp phát**: trong script đo GC, gom danh sách một lần lúc vào level.
- **`Time.captureFramerate` còn khoá sau khi script tự đặt**: `PlayProbe.Run` không trả về 0; script đặt thì tự trả, hoặc dùng
  `FeelTraceRecorder` (tự trả ở mọi đường ra).

## Kiểm thử

`Tests/EditMode/ProbeTests.cs`: `Field`, `Call` với private khai trên base, tên không có thì ném; `Shrink` lấy trung bình trong vùng
cắt; `Compose` đặt ảnh 0 trên trái theo hàng; `Dump` tính bounds trong hệ toạ độ root và đánh dấu object tắt.

Runner kiểm bằng chạy thật: trace quay qua `playrun.sh` khớp 0 chênh với trace cùng chữ ký quay bằng runner trước nó; một probe vào
level, tap, chụp bốn ảnh ghép một sheet, đọc field private, in kết quả ba dòng.

## Mở rộng sau

- Cỡ Game view do runner đặt, nếu Unity CLI có lệnh đó.
- Ghép ảnh kèm nhãn frame, nếu đọc lưới theo thứ tự không đủ.
