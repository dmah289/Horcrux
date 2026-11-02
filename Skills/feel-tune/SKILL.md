---
name: feel-tune
description: Dùng khi chỉnh, refactor hay dò lỗi feeling của game Unity — tween, trình diễn, hiệu ứng, nhịp, juice — hoặc đo GC, batches trong lúc trình diễn. Chọn công cụ đo dựng sẵn trong Horcrux theo câu hỏi, thay vì viết script chạy ngầm mới mỗi lần.
---

# feel-tune — chỉnh feeling với công cụ dựng sẵn

Skill công cụ. `<horcrux>` là gốc Horcrux trong dự án. API chi tiết: `docs/Editor/Probe.md`, `docs/Editor/FeelTrace.md`.
Khi nào được vào Play và khi nào giao developer chơi thử: `workflow` › Nghiệm thu. Số tinh chỉnh là nháp: NT9.

## Luật

1. **Không viết script khung mới** (vào Play, thử lại eval, bắt log, ghi file, khoá fps, chụp, ghép ảnh). Thiếu thì mở
   rộng công cụ trong Horcrux hoặc class đường tắt của dự án, rồi dùng.
2. **Không đọc dữ liệu thô vào ngữ cảnh.** Trace đọc qua `stat`, `diff`, `sig`; kết quả probe tóm tắt dưới 4 KB; ảnh là
   một sheet đã cắt và thu nhỏ.
3. **Refactor phải giữ chuyển động thì chứng minh bằng trace**, không bằng mắt: quay mỗi frame ở cuối frame với frame rate
   khoá (`Time.captureFramerate`), kịch bản gõ qua đúng đường input thật, ghi mọi thuộc tính nhìn thấy của mọi entity.
   Chứng minh trace **tất định** trước — hai lần quay mốc 0 chênh, so theo chữ ký trạng thái đầu (thời gian nạp đổi số lần
   gọi `Random`) — rồi mới so trước–sau. Trace chứng minh "không đổi", không thay chơi thử.

## Chọn công cụ theo câu hỏi

| Câu hỏi | Công cụ | Vào Play |
|---|---|---|
| vị trí, cỡ, sorting, bounds trong prefab; override của prefab lồng nhau | `PrefabDump.DumpAsset(path, filter)` qua `script eval` | không |
| refactor có giữ nguyên chuyển động | FeelTrace: quay mốc, sửa, quay lại, `feeltrace.py diff` | có |
| chuyển động cao bao nhiêu, bao lâu, vọt lố bao nhiêu | FeelTrace quay một lần, `stat <entity> --from --to` | có |
| GC, batches, renderer bật mà không vẽ lúc diễn | `feeltrace.sh --gc`, `stat <file> M0 --cols 0` | có |
| một hàm có cấp phát dưới vài chục KB | test PlayMode `Is.Not.AllocatingGCMemory`, **không** đọc từ M0 — GC mỗi frame trong Editor có nền nhiễu ≈ 100 KB | có |
| trạng thái nội bộ sau một chuỗi thao tác | `playrun.sh` + `PlayProbe.Run` | có |
| mất hình, lệch lớp, nháy ở một frame | `FrameSheet` trong probe hay `Act` của kịch bản, một PNG | có |
| cảm giác, nhịp, "đã tay" | developer chơi thử | — |

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

**Đường tắt của dự án:** mỗi dự án giữ **một class Editor** `*Probe` đưa game tới trạng thái cần đo (vào level, chờ rảnh,
chọn entity, gọi hàm private qua `PlayProbe.Call`). Đường tắt cần lần thứ hai thì thêm vào class đó; kịch bản FeelTrace
của dự án dùng chung nó, dự án chỉ viết kịch bản.

## Quy trình

1. Đọc code tween, trình diễn đang chạm; lỗi bố cục thì `PrefabDump` trước.
2. Refactor phải giữ chuyển động: kịch bản đã phủ chưa, rồi quay mốc **trước** khi sửa.
3. Sửa theo `async-presentation` (PrimeTween, UniTask; tween nhân hay cộng trên giá trị nghỉ).
4. Kiểm bằng con số: `diff` 0 chênh, hoặc `stat` cho số cần báo.
5. Báo cáo: số đo kèm tên file; phần cảm giác giao kịch bản chơi thử.

## Ngân sách token

- Một lần đo là một lệnh Bash; không gọi MCP `start`, `stop`, `wait`, `game_view`, `screenshot` từng bước.
- `stat --cols` chỉ lấy cột đang hỏi; `--series` chỉ khi cần từng frame.
- Ảnh: `shrink` từ 2 trở lên, `crop` đúng vùng, nhiều khoảnh khắc ghép một sheet.
- Lần chạy lâu hơn một phút thì chạy nền; không đọc output khi chưa xong.
