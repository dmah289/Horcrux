---
name: feel-tune
description: Dùng khi chỉnh, refactor hay dò lỗi feeling của game Unity — tween, trình diễn, hiệu ứng, nhịp, juice — hoặc đo GC, batches trong lúc trình diễn. Chọn công cụ đo dựng sẵn trong Horcrux theo câu hỏi, thay vì viết script chạy ngầm mới mỗi lần.
---

# feel-tune — chỉnh feeling với công cụ dựng sẵn

Đường dẫn tính từ gốc Horcrux (dự án hiện tại: `Assets/Horcrux/`). Chi tiết API ở `Editor/Probe/Probe.md` và
`Editor/FeelTrace/FeelTrace.md`; file này chỉ chọn công cụ và giữ ngân sách token.

## Luật

1. **Developer cảm được thì agent không vào Play để xem.** Bố cục, vị trí, nhịp, hình ảnh: đọc code và asset, sửa, rồi giao kịch bản
   chơi thử — vào đâu · làm gì (chuỗi ngắn nhất) · nhìn hiện tượng nào · khác trước ra sao · dấu hiệu hỏng.
2. **Play qua công cụ chỉ cho thứ developer không cảm được:** chứng minh refactor giữ nguyên chuyển động · con số (đỉnh, thời lượng,
   vọt lố, GC, batches) · trạng thái nội bộ màn hình không hiện · lỗi hình agent phải tự thấy trước khi giao.
3. **Không viết script khung mới.** Vào Play, thử lại eval, bắt log lỗi, ghi file, khoá fps, chụp, ghép ảnh đã có. Thiếu thì mở rộng
   công cụ trong Horcrux (code Editor agent viết thẳng) hoặc đường tắt Editor của dự án, rồi dùng.
4. **Không đọc dữ liệu thô vào ngữ cảnh.** Trace đọc qua `stat`, `diff`, `sig`; kết quả probe là bản tóm tắt dưới 4 KB; ảnh là một
   sheet đã cắt và thu nhỏ.
5. **Số tinh chỉnh trong asset là nháp của developer.** Đổi công thức thì giữ hành vi thuật toán, chứng minh bằng trace, rồi hỏi số cũ
   là nháp hay mốc — không tự ánh xạ.

## Chọn công cụ theo câu hỏi

| Câu hỏi | Công cụ | Vào Play |
|---|---|---|
| Vị trí, cỡ, sorting, bounds trong prefab; override của prefab lồng nhau | `PrefabDump.DumpAsset(path, filter)` qua `script eval` | không |
| Refactor có giữ nguyên chuyển động không | FeelTrace: quay mốc, sửa, quay lại, `feeltrace.py diff` | có |
| Một chuyển động cao bao nhiêu, chạy bao lâu, vọt lố bao nhiêu | FeelTrace quay một lần, `feeltrace.py stat <entity> --from --to` | có |
| GC, batches, renderer bật mà không vẽ trong lúc diễn | `feeltrace.sh --gc` (chèn `recorder.Metrics` vào bản copy), `stat <file> M0 --cols 0` | có |
| Một hàm có cấp phát dưới vài chục KB không | test PlayMode `Is.Not.AllocatingGCMemory` (khung `CharmJumpTests`), **không** đọc từ M0: GC/frame trong Editor có nền nhiễu ≈ 100 KB (Editor + recorder), đo 2026-10-31 | có |
| Trạng thái nội bộ sau một chuỗi thao tác | `playrun.sh` + `PlayProbe.Run` | có |
| Mất hình, lệch lớp, nháy ở một frame cụ thể | `FrameSheet` trong probe hay `Act` của kịch bản, một PNG | có |
| Cảm giác, nhịp, "đã tay" | developer chơi thử | — |

## Lệnh

```bash
# Không vào Play: dump một nhánh prefab
unity cmd --project-path <project> --result-only script eval --code 'return Horcrux.Editor.Probe.PrefabDump.DumpAsset("<prefab>", "<nhánh>");'

# Chạy một probe trong Play, in kết quả
bash "<horcrux>/Editor/Probe/Tools~/playrun.sh" <probe.cs> <file ra>

# Quay trace, so, tóm tắt
bash "<horcrux>/Editor/FeelTrace/Tools~/feeltrace.sh" [--gc] <kịch bản.cs> <runs> <tên>   # unity.exe: UNITY_CLI, rồi `unity` trên PATH
python -X utf8 -I "<horcrux>/Editor/FeelTrace/Tools~/feeltrace.py" pairs "<runs>/*.txt"   # quay tới khi có hai file cùng chữ ký
python -X utf8 -I "<horcrux>/Editor/FeelTrace/Tools~/feeltrace.py" diff <a> <b>
python -X utf8 -I "<horcrux>/Editor/FeelTrace/Tools~/feeltrace.py" stat <file> <entity> --cols 1.y,3 --from 0 --to 120
```

Probe tối thiểu (thân hàm, không `using`, type viết đủ namespace):

```csharp
var outPath = @"__OUT__";
Horcrux.Editor.Probe.PlayProbe.Run(outPath, async (sb, ct) =>
{
    var director = await <Game>.Editor.Probe.<Game>Probe.EnterLevelAsync(1, ct);
    // gõ, chờ, đo; ghi số đã tính vào sb, không ghi mỗi frame
});
return "playrun";
```

## Đường tắt của dự án

Mỗi dự án giữ **một class Editor** đưa game tới trạng thái cần đo: vào level, chờ rảnh, chọn entity, gọi hàm private qua
`PlayProbe.Call`. Tìm bằng tên `*Probe` trong thư mục Editor của dự án. Đường tắt cần lần thứ hai thì thêm vào class đó, không viết lại
trong script; kịch bản FeelTrace của dự án dùng chung nó.

## Quy trình

1. Đọc code tween, trình diễn đang chạm; lỗi bố cục thì `PrefabDump` trước.
2. Refactor phải giữ chuyển động: kịch bản đã phủ chưa, rồi quay mốc **trước** khi sửa (`FeelTrace.md`, mục quy trình).
3. Sửa — PrimeTween cho tween, UniTask cho async; tween nhân hay cộng trên giá trị nghỉ, không ghi tuyệt đối.
4. Kiểm bằng con số: `diff` 0 chênh, hoặc `stat` cho số cần báo.
5. Báo cáo: số đo kèm tên file; phần cảm giác giao kịch bản chơi thử.

## Ngân sách token

- Một lần đo là một lệnh Bash; không gọi MCP `start`, `stop`, `wait`, `game_view`, `screenshot` từng bước.
- `stat --cols` chỉ lấy cột đang hỏi; `--series` chỉ khi cần từng frame.
- Ảnh: `shrink` từ 2 trở lên, `crop` đúng vùng cần xem, nhiều khoảnh khắc ghép vào một sheet.
- Lần chạy chạy nền khi lâu hơn một phút; không đọc file output khi chưa xong.
