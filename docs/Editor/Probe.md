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
| Ranh giới | Horcrux giữ runner, ghi kết quả, reflection, chờ scene, bấm nút, dump prefab, ghép ảnh, cỡ Game view, máy trên Device Simulator, chạy lần lượt nhiều cỡ hay nhiều máy, file máy thật (`Editor/Probe/Devices/`), đo khung object trên màn, vào Play từ scene đầu. Dự án giữ **đường tắt vào trạng thái cần đo** (bấm menu, chờ level, chọn entity), một class Editor của dự án |
| Chỗ đặt | C# ở `Editor/Probe/` (assembly `com.horcrux.editor`). Shell ở `Editor/Probe/Tools~/`: Unity không import, không vào build |
| Quan hệ với FeelTrace | `feeltrace.sh` chạy qua `playrun.sh`; `FeelTraceRecorder` dùng chung cờ `.done` |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Chạy từ menu Editor | eval của Unity CLI chạy được trong Play, đổi script không cần domain reload |
| Vẽ nhãn lên ảnh ghép | thứ tự ảnh cố định (trên trái, theo hàng) là đủ đọc; vẽ chữ cần font và shader |
| `PlayProbe` bọc `UniTask.WaitUntil`, `DelayFrame` | UniTask đã có, gọi thẳng |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| `playrun.sh` in phần kết quả tối đa 4 KB, không in thân trace | kết quả đo nên là bản tóm tắt; trace đọc bằng `feeltrace.py stat`, không đổ vào ngữ cảnh |
| `playrun.sh` in `LOG` đã bỏ stack trace và gộp dòng lặp; file vẫn giữ đủ | lỗi có sẵn của game (thiếu remote config…) lặp hàng trăm dòng stack che mất kết quả; cần stack thì mở file |
| `PlayProbe.Field`, `Call` dò lên từng base | `GetField(NonPublic)` không thấy private khai trên base, script tự viết reflection hay quên |
| `PrefabDump` tính bounds từ `sprite.bounds`, không từ `Renderer.bounds` | prefab asset chưa instantiate thì renderer không có bounds thế giới |
| `GameViewSize` đi qua reflection | Unity CLI và API công khai không có lệnh đổi cỡ Game view; tên type, method nội bộ đã kiểm trên Unity 6, đổi version thì kiểm lại |
| `PlayFrom` nhớ scene khởi động cũ bằng `SessionState` | vào Play có domain reload: biến static mất, `SessionState` còn tới khi đóng Editor |
| `PlayProbe.Click` gửi thẳng tới object, không raycast | bấm theo tên object không phụ thuộc toạ độ màn và thứ che phía trên; kiểm nút có bị che thì nhìn ảnh chụp |
| `ScreenBox` gốc trên trái, trục y đi xuống | trùng hệ toạ độ ảnh chụp và ảnh demo: số đo so thẳng với `DemoCompare` (`docs/Editor/UIReskin.md`). Tự đổi bằng `WorldToScreenPoint` hay quên lật trục y |
| `ScreenBox` của object có RectTransform là rect của chính nó, không gộp con | chữ world có RectTransform mà không có Renderer; khung là ô chữ, không phải nét chữ. Cần cả cụm thì đo object cha không có RectTransform |
| `SIZES` đặt cỡ trước khi vào Play, mỗi cỡ một lần Play | game đọc cỡ màn lúc khởi động; đổi giữa Play thì phần tính theo màn lúc boot vẫn theo cỡ cũ |
| Đo cả Game view lẫn Device Simulator | Game view có `Screen.safeArea` bằng cả màn: mọi thứ neo theo vùng an toàn (tai thỏ, thanh home) đứng đúng ở Game view mà lệch trên máy. Simulator giả cỡ màn, vùng an toàn, cutout của máy |
| `SimulatorDevice` đổi view Play chính bằng `PlayModeView.SwapMainWindow` qua reflection | không có API công khai chọn máy; swap trong cùng ô dock, Simulator nhớ máy đã chọn qua domain reload. Tên type, method nội bộ kiểm trên Unity 6 |
| `DEVICES` dừng lần chạy khi không chọn được máy | chạy tiếp là đo máy đang chọn từ trước mà file ra mang tên máy mới |

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
| `PlayProbe.WaitForSceneAsync(string sceneName, CancellationToken ct)` | trả `Scene` khi scene đúng tên đã nạp xong. Gọi service của game trước khi scene chứa nó nạp là chạy trước boot |
| `PlayProbe.Click(GameObject target)` | pointerDown, pointerUp, pointerClick vào handler gần nhất từ `target` lên cha; cần EventSystem |
| `GameViewSize.Set(int width, int height)` · `Restore()` · `Current()` | chọn cỡ cố định (chưa có thì thêm vào nhóm của platform đang chọn, lưu trong preference của máy), trả `index: nhãn`; `Restore` về cỡ trước lần `Set` đầu. Trong và ngoài Play; layout UI cập nhật vài frame sau |
| `SimulatorDevice.Set(string name)` · `Restore()` · `Current()` · `List()` | đưa Device Simulator lên làm view Play chính và chọn máy theo tên (không phân biệt hoa thường, tên trần khớp máy mẫu Unity có đuôi `" (WxH)"`), trả `index: tên rộng×cao safe x,y,w,h`; không thấy tên thì ném `ArgumentException` kèm danh sách. `Restore` về view và máy trước lần `Set` đầu; `List` mọi máy Simulator thấy. `GameViewSize` tự đưa Game view về trước khi đổi cỡ |
| `PlayFrom.FirstBuildScene()` · `Arm()` | đặt scene đầu Build Settings làm scene khởi động (`FirstBuildScene` vào Play luôn); về Edit mode thì trả scene khởi động cũ. Trong Play bật `Application.runInBackground` |
| `PlayProbe.Field<T>(object target, string fieldName)` · `Call(object target, string methodName, params object[] args)` | instance, public hay không, dò từ type thật lên base. Không có thì ném `MissingFieldException` / `MissingMethodException`; nhiều overload cùng tên thì `AmbiguousMatchException` |
| `PrefabDump.Dump(GameObject root, string pathFilter = null)` | một dòng mỗi object (xem "Đọc PrefabDump"); `pathFilter` giữ dòng có đường dẫn chứa chuỗi đó. Soát override, reference gãy của prefab UI ở `docs/Editor/UIReskin.md` |
| `PrefabDump.DumpAsset(string assetPath, string pathFilter = null)` | prefab nạp qua `AssetDatabase`, không instantiate, không cần Play |
| `ScreenBox.Of(GameObject target)` | khung trên Game view theo pixel, gốc trên trái: `Left` `Top` `Right` `Bottom` `Width` `Height`, `ToString()` ra `y top-bottom x left-right`. RectTransform: rect của chính nó, canvas Overlay đọc thẳng, canvas Camera và World qua `worldCamera` (thiếu thì `Camera.main`), không canvas qua `Camera.main`. Còn lại: gộp bounds mọi Renderer con đang bật, chiếu tám góc qua `Camera.main`. Không có cả hai thì ném `InvalidOperationException`. Gọi trong Play, sau khi layout chạy |
| `ScreenBox.Of(Renderer renderer)` | khung của riêng renderer đó qua `Camera.main`, không gộp con: cho sprite nền có con nhô ra ngoài. Bounds gồm viền trong suốt của art: so với pixel demo thì lấy cột từ khung này rồi đo màu bằng `DemoCompare.Runs` trên cả demo lẫn ảnh chụp |
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

Chạy từ gốc project hoặc đặt `PROJECT_PATH`; `UNITY_CLI` đổi đường dẫn `unity.exe`; `PLAY_FROM_BUILD_SCENE=1` vào Play từ scene đầu Build
Settings qua `PlayFrom.Arm`. Mã thoát khác 0 khi script lỗi compile (in đủ
lỗi, dừng ngay), không chạy, hay timeout.

`SIZES="1080x2340 1080x1920"` chạy cùng script một lần mỗi cỡ Game view, đặt cỡ trước khi vào Play. File ra thêm hậu tố cỡ trước đuôi
(`out.txt` → `out_1080x2340.txt`), mọi `__SIZE__` trong script thành cỡ đó (đặt tên ảnh chụp), xong trả Game view về cỡ trước lần đặt
đầu. Một cỡ hỏng thì vẫn chạy cỡ sau, mã thoát 1.

```bash
SIZES="1080x2340 1080x2220 1080x1920" PLAY_FROM_BUILD_SCENE=1 bash "Assets/Horcrux/Editor/Probe/Tools~/playrun.sh" probe.cs out.txt
```

`DEVICES="Poco M7 Pro 5G;iOS Notch Device"` chạy một lần mỗi máy trên Device Simulator, tên cách nhau bằng `;`. File ra và `__SIZE__`
mang tên máy đã thay khoảng trắng bằng `_` (`out_Poco_M7_Pro_5G.txt`); xong trả view Play về như trước. Chọn máy hỏng thì in danh sách
máy và bỏ máy đó. Có cả `SIZES` thì chạy các cỡ Game view trước, rồi các máy.

```bash
SIZES="1080x2340" DEVICES="Poco M7 Pro 5G" bash "Assets/Horcrux/Editor/Probe/Tools~/playrun.sh" probe.cs out.txt
```

## Thêm máy thật cho Simulator

Một file `.device` (JSON, định dạng Device Simulator của Unity) ở `Editor/Probe/Devices/`; Simulator nạp mọi file `.device` trong
`Assets`. Cần đúng: `friendlyName`, `screens[0]` `width` `height` `dpi`, `orientations` (1 là dọc) với `safeArea` và `cutouts` theo pixel
gốc **dưới** trái, `systemInfo.operatingSystem` (Android hay iOS). Vùng an toàn lấy từ thông số hãng, hoặc đo từ ảnh chụp máy: chạy
probe ở Game view đúng cỡ màn máy, so dải màu cùng cột với ảnh chụp (`DemoCompare.Runs`); mọi khối neo mép trên lệch cùng một số pixel
là inset trên. Kiểm file: chạy probe trên máy mới, ảnh chụp Simulator phải trùng ảnh máy theo từng dải màu.

| Máy | Màn | Vùng an toàn dọc | Nguồn |
|---|---|---|---|
| `Poco M7 Pro 5G` | 1080×2400, 395 dpi | trên 110 px, dưới 0 | đo từ ảnh chụp máy; cutout chỉ ước lượng vị trí lỗ camera |

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
header/txt_title on p=20,-105,0 s=1,1,1 r=0 | rt a=20,8 sd=660,130 anc=0.5,0.5-0.5,0.5 pv=0.5,0.5 | ut "Title" 92 auto Text Outline-Blue
```

| Phần | Nghĩa |
|---|---|
| đường dẫn | từ root, root in tên root |
| `on` / `OFF` | `activeSelf` |
| `p` `s` `r` | `localPosition`, `localScale`, `localEulerAngles.z` |
| `rt a sd anc pv` | RectTransform: `anchoredPosition`, `sizeDelta`, `anchorMin-anchorMax`, `pivot` |
| `sr` | SpriteRenderer: `OFF` khi tắt, tên sprite, `sortingLayer#order`, alpha, `drawMode size` khi không Simple, `root c size` là tâm và cỡ khung bao trong hệ toạ độ root |
| `sg` | SortingGroup `layer#order` |
| `img` | Image: `OFF` khi tắt, sprite, kiểu, `ppum` khi khác 1, `c` khi khác trắng, `ray` khi nhận raycast |
| `ut` | chữ UniText: nội dung (24 ký tự đầu), cỡ, `auto` khi auto size, tên StylePreset nối bằng `+` (`-` khi không có), `c` khi khác trắng |
| `hlg` `vlg` `grid` | LayoutGroup: spacing, `ctrl` trục điều khiển cỡ con (grid: cell, constraint), `pad` trái, phải, trên, dưới, căn |
| `csf` `le` | ContentSizeFitter ngang, dọc · LayoutElement `ignore`, `pref`, `min` |
| `prefab` | root của prefab instance: prefab nguồn trực tiếp; `ov` số override trừ override bắt buộc, chỉ ở instance ngoài cùng |
| cuối dòng | component còn lại; `Missing` là script mất |

## Trước khi chạy

| # | Việc | Thiếu thì |
|---|---|---|
| 1 | Unity Editor mở project; Unity CLI và package `com.unity.pipeline` đã cài; `unity status --project-path <project>` báo `ready` | runner báo `pipeline not ready` |
| 2 | Không có lỗi compile trong project | Editor vào Safe Mode, pipeline không nạp |
| 3 | Scene khởi động của game đang mở, hoặc chạy với `PLAY_FROM_BUILD_SCENE=1`, nếu script vào level qua menu | script chờ mãi, runner báo timeout |
| 4 | Game view giữ một độ phân giải cho mọi lần đo so với nhau, và cho mọi `crop` của `FrameSheet`: chạy với `SIZES` hay `DEVICES`, hoặc script gọi `GameViewSize.Set`, `SimulatorDevice.Set` trước khi đo | toạ độ, vùng cắt lệch |

Run In Background và auto-tick runner tự bật, và trả lại khi thoát ở mọi đường ra (override ghi vào `ProjectSettings/Packages/com.unity.pipeline`).

## Bẫy

- **Eval hay rớt ngay sau khi vào Play** ("domain reloading" hay chưa có kết quả): runner thử lại tới 10 lần. Script có tác dụng phụ trước
  `PlayProbe.Run` (ghi file, đổi state) thì phải chịu được chạy hai lần.
- **Lệnh Unity CLI đổi cài đặt Editor ghi vào file của project**: override Run In Background nằm trong
  `ProjectSettings/Packages/com.unity.pipeline/EditorPipelineConfig.json`. *Đã sai một lần:* runner bật mà không trả, file hiện trong
  `git status` của dự án sau mỗi lần đo. Runner trả lại bằng `trap … EXIT`; thêm lệnh cài đặt mới thì trả lại cùng chỗ. Giá trị đã về
  như cũ mà file vẫn hiện trong `git status`: Unity ghi lại nó với xuống dòng LF. Runner chép lại bản trước khi chạy khi hai bản chỉ khác
  xuống dòng; khác giá trị thì giữ bản Unity ghi và in một dòng báo.
- **Scene đang mở có thay đổi chưa lưu**: Editor hỏi lưu khi vào Play hay khi chạy test và đứng chờ hộp thoại; CLI chỉ thấy timeout,
  `unity status` báo `unreachable`. Lưu hoặc bỏ thay đổi trước khi chạy.
- **Đúng ở Game view, lệch trên máy thật**: Game view không có tai thỏ, thanh home. Khối neo theo vùng an toàn chỉ lộ lệch trên Simulator
  với máy có inset; kiểm cả hai trước khi build.
- **`FindObjectsByType` mỗi frame cấp phát**: trong script đo GC, gom danh sách một lần lúc vào level.
- **`Time.captureFramerate` còn khoá sau khi script tự đặt**: `PlayProbe.Run` không trả về 0; script đặt thì tự trả, hoặc dùng
  `FeelTraceRecorder` (tự trả ở mọi đường ra).

## Kiểm thử

`Tests/EditMode/ProbeTests.cs`: `Field`, `Call` với private khai trên base, tên không có thì ném; `Shrink` lấy trung bình trong vùng
cắt; `Compose` đặt ảnh 0 trên trái theo hàng; `Dump` tính bounds trong hệ toạ độ root và đánh dấu object tắt.

Runner kiểm bằng chạy thật: trace quay qua `playrun.sh` khớp 0 chênh với trace cùng chữ ký quay bằng runner trước nó; một probe vào
level, tap, chụp bốn ảnh ghép một sheet, đọc field private, in kết quả ba dòng. `ScreenBox` và `SIZES` kiểm cùng một lần chạy hai cỡ:
khung sprite và ô chữ world khớp số tự tính bằng `WorldToScreenPoint`, rect UI Overlay co theo cỡ màn, Game view về cỡ cũ, `git status`
của dự án sạch. `SimulatorDevice` và `DEVICES` kiểm bằng một máy đo từ ảnh chụp: ảnh Simulator trùng ảnh máy theo từng dải màu ở
mọi khối neo mép trên; tên máy sai thì runner dừng và in danh sách; tên trần chọn đúng máy mẫu; view Play về Game view sau lần chạy.

## Mở rộng sau

- Ghép ảnh kèm nhãn frame, nếu đọc lưới theo thứ tự không đủ.
