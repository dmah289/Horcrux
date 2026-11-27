---
name: feel-tune
description: Dùng khi chỉnh, refactor hay dò lỗi feeling của game Unity — tween, trình diễn, hiệu ứng, nhịp, juice — hoặc đo GC, batches trong lúc trình diễn, hoặc dò một luồng runtime trong Play (treo, không chuyển màn, quảng cáo, popup kẹt) bằng trạng thái nội bộ, hoặc đo FPS, giật, CPU và chơi qua kịch bản trên máy Android thật đang cắm cáp. Chọn công cụ đo dựng sẵn trong Horcrux theo câu hỏi, thay vì viết script chạy ngầm mới mỗi lần.
---

# feel-tune — chỉnh feeling với công cụ dựng sẵn

Skill dưới `Agent/horcrux.md` (NT, tầng, ký hiệu định nghĩa ở đó).
`<horcrux>` là gốc Horcrux trong dự án. API chi tiết: `docs/Editor/Probe.md`, `docs/Editor/FeelTrace.md`.
Khi nào được vào Play và khi nào giao developer chơi thử: `workflow` › Nghiệm thu. Số tinh chỉnh là nháp: NT11.

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
| developer giao kiểm kịch bản hành vi (thiếu điều kiện thì rẽ nhánh nào, đủ thì ghi đúng số) | một probe chạy hết các ca: dựng dữ liệu, `Click`, đọc số trước–sau; runner trả PlayerPrefs, save ngoài PlayerPrefs thì probe tự trả (`docs/Editor/Probe.md` › Bẫy); mỗi ngữ cảnh cửa tự chọn (menu, màn chơi) là một ca | có |
| luồng dừng giữa chừng, Editor còn giữ nguyên (không chuyển màn, popup kẹt) | **không dừng Play**: `console_digest.py` tới dòng cuối trước khi im, rồi `PlayProbe.Field` đọc trạng thái hai bên chỗ đứt (hệ ngoài đã trả chưa, bên chờ còn sống không) | đang Play |
| sửa xong, tái hiện luồng (thắng → quảng cáo → chuyển màn) | `playrun.sh`: `WaitForSceneAsync` trước mọi service, `Call` handler nút hay `Click`, đóng quảng cáo giả (`docs/Editor/Probe.md` › Bẫy), ghi số trước–sau | có |
| mất hình, lệch lớp, nháy ở một frame | `FrameSheet` trong probe hay `Act` của kịch bản, một PNG | có |
| một look ở vài giá trị thông số (lớp vẽ có bị che, độ toả, offset, scale đỉnh) | `UICapture.Variants` qua `script eval` (`docs/Editor/UIReskin.md`) | không |
| tiếng "không phát", to nhỏ lệch giữa các tiếng | nguồn đang phát chưa (`PlayProbe.Field`), rồi nút `Audit Loudness` của catalog audio (`docs/Runtime/AudioSystem/AudioSystem.md`): file nhỏ hơn mặt bằng hàng chục dB nghe như không phát — sửa file, không sửa code | không (audit) |
| FPS, giật, nóng máy chỉ thấy trên máy thật; build đang cài có bản sửa chưa | DeviceProbe (`docs/Editor/DeviceProbe.md`): `play --fps-out` kịch bản có mốc, `summary` theo trạng thái màn, tách CPU/GPU bằng `threads`, `res-ab` | trên máy |
| chơi lại đúng chuỗi thao tác developer báo lỗi trên máy | `record` lúc developer chơi, `play` để tái hiện và đo | trên máy |
| event analytics, doanh thu quảng cáo build thật có gửi đi không, mang tham số gì | DeviceProbe `events` (`docs/Editor/DeviceProbe.md` › Kiểm event analytics) | trên máy |
| cảm giác, nhịp, "đã tay" | developer chơi thử | — |

## Lệnh

```bash
# Không vào Play: dump một nhánh prefab
unity cmd --project-path <project> --result-only script eval --code 'return Horcrux.Editor.Probe.PrefabDump.DumpAsset("<prefab>", "<nhánh>");'

# Luồng dừng giữa chừng trong phiên developer để nguyên: tóm console, đọc dòng cuối trước khi im
PROJECT_PATH=<project> python -X utf8 -I "<horcrux>/Editor/Probe/Tools~/console_digest.py" --drop "^Tween" [--level error] [--since <cursor>]

# Chạy một probe trong Play, in kết quả
bash "<horcrux>/Editor/Probe/Tools~/playrun.sh" <probe.cs> <file ra>

# Quay trace, so, tóm tắt
bash "<horcrux>/Editor/FeelTrace/Tools~/feeltrace.sh" [--gc] <kịch bản.cs> <runs> <tên>   # unity.exe: UNITY_CLI, rồi `unity` trên PATH
python -X utf8 -I "<horcrux>/Editor/FeelTrace/Tools~/feeltrace.py" pairs "<runs>/*.txt"   # quay tới khi có hai file cùng chữ ký
python -X utf8 -I "<horcrux>/Editor/FeelTrace/Tools~/feeltrace.py" diff <a> <b>
python -X utf8 -I "<horcrux>/Editor/FeelTrace/Tools~/feeltrace.py" stat <file> <entity> --cols 1.y,3 --from 0 --to 120

# Máy Android thật: đọc máy, chụp nhỏ, chạy cả kịch bản một lệnh kèm FPS theo mốc
python -X utf8 -I "<horcrux>/Editor/DeviceProbe/Tools~/device.py" info
python -X utf8 -I "<horcrux>/Editor/DeviceProbe/Tools~/device.py" shot a.jpg --scale 0.35
python -X utf8 -I "<horcrux>/Editor/DeviceProbe/Tools~/device.py" play <kịch bản.txt> --fps-out fps.log
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
- Trên máy thật: cả đoạn chơi viết thành một file kịch bản chạy bằng `play`, không đi từng bước chạm–chụp–đọc; ảnh
  `--scale 0.35` chỉ ở điểm kiểm.
