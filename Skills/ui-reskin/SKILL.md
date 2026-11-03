---
name: ui-reskin
description: Dùng khi dựng hoặc reskin UI theo ảnh demo: cách tổ chức prefab UI để đúng demo và dễ mở rộng, cách làm việc với Editor qua công cụ (MCP, eval) khi dựng, soát, chụp, đo, và các bẫy của UGUI, prefab, sprite, TMP đã trả giá.
---

# ui-reskin — prefab UI theo demo

Skill công cụ dưới `Skills/horcrux_skill.md` (NT, ba tầng, ký hiệu định nghĩa ở đó). Tool: `docs/Editor/UIReskin.md` (dựng,
soát, chụp, đo) · `docs/Editor/Probe.md` (cỡ Game view, vào Play, chờ scene, bấm).

Mục dưới là **tiêu chí và lý do**, không phải trình tự. Bài nào có đường ngắn hơn mà vẫn đạt tiêu chí thì đi đường đó (NT6),
nói lý do và phép kiểm.

## Tổ chức prefab

**Skin là lớp hình đặt lên prefab đang có hành vi, không phải prefab viết lại.** Script, address, reference đã nối là hợp đồng
với code; dựng lại từ đầu là nối lại từng ô và mất override của dự án nguồn. Dạng thường gặp: variant của prefab gốc, hình cũ
**tắt** thay vì xoá — prefab gốc còn nhận bản cập nhật từ nơi khác thì merge không vỡ. Tắt chỉ an toàn khi code không với tới
object tắt: `GetComponentInChildren`, `GetComponentsInChildren(false)` bỏ qua object tắt, reference kéo thả thì không.

**Thứ lặp lại là một khối dùng chung; khác nhau ở dữ liệu là variant; khác hình dạng là khối riêng.** Tiêu chí: đổi kiểu (khung,
nút, font) ở một chỗ thì mọi chỗ theo, và thêm một mục mới là *thêm* một variant hay kéo một khối vào, không chép. Số tầng
variant theo số **lý do thay đổi** thật (kiểu chung → loại → gói), không theo cho đủ bộ (NT1).

**Vị trí do layout tính, số tay chỉ cho thứ demo đặt tự do.** LayoutGroup, ContentSizeFitter xếp và co theo nội dung: thêm bớt
mục không phải đo lại. Override mà layout ghi đè mỗi lần tính là nhiễu — che thay đổi của prefab gốc và làm diff vô nghĩa;
soát và gỡ sau mỗi lần dựng (`PrefabAudit.PruneNoise`). Gỡ hàng loạt thì xem danh sách trước: cùng một property có thể là ý đồ.

**Demo là nguồn số đo.** Canvas có reference resolution bằng cỡ demo thì một pixel demo là một đơn vị UI: đo demo bằng pixel,
dựng đúng số đó. Rect của art gồm cả bóng và viền trong suốt, nên khung đo trên demo nhỏ hơn rect. Số hiển thị (giá, số lượng,
đồng hồ) đọc từ dữ liệu thật; demo là chữ giữ chỗ.

**Neo theo màn, không theo một máy.** Nền và dải màu phủ ra ngoài safe area (tai thỏ, thanh home); nội dung bấm được nằm trong.
Khối thiết kế theo một bề rộng thì giữ bề rộng đó: màn hẹp hơn thu nhỏ đều, màn rộng hơn (tablet) nằm giữa. Art vẽ sẵn chi tiết
(bóng sàn, khe) thì con đặt theo pixel của ảnh và phóng cùng ảnh. Fitter chạy trong Editor khai giá trị nó ghi là driven
(`DrivenRectTransformTracker`, `overrideSprite`) để đổi Game view không làm bẩn asset.

**9-slice là dữ liệu của art.** Đọc border hiện có trước khi ghi; art hai màu (thân và chân khác màu) chỉ đổi cạnh trên, dưới
để vạch chia không bị kéo giãn. Chân thẻ dày mỏng khác art thì chỉnh `pixelsPerUnitMultiplier` trên Image, không sửa border.

**Câu hỏi về hành vi thuộc developer.** Nút trên demo chưa có logic, nút cũ không còn chỗ trên demo, chữ demo khác chữ code: hỏi
trước khi dựng (NT5), ghi câu trả lời vào tài liệu thiết kế của màn.

## Làm việc qua Editor tool

**Gọi hàm Horcrux, không chép khung vào eval.** Mở, sửa, lưu, dỡ prefab, dựng node, chụp, đo đã có sẵn và đã compile thật: eval
chỉ còn phần riêng của bài, ít chỗ cho lỗi biên dịch C# 6 và API đoán tên. Khung nào eval tới lần thứ hai mà Horcrux chưa có
thì đề xuất công cụ theo `workflow` (đầu skill đó). *Đã sai một lần:* hàng trăm eval chép cùng khung dump, sửa, chụp, so; đoạn
code phải đào lại từ lịch sử chat nhiều lần.

**Nhìn trước khi sửa.** Dump cây và field serialize (`PrefabDump`, `PrefabAudit.ListOverrides`), contact sheet art
(`DemoCompare.ContactSheet`), báo cáo sprite (`SpriteSlicing.Report`) trước khi chạm: tên file art và số trong YAML có thể đánh
lừa, giá trị đọc qua engine thì không.

**Edit mode cho dựng và soát; Play cho thứ cần dữ liệu thật.** Bố cục của khối xem được bằng chụp canvas ngoài Play
(`UICapture.Canvas`). Màn cần service (chữ do script đặt, popup mở qua navigator) thì vào Play. Chuỗi Play lặp lại nhiều lần (chụp
cùng bộ màn ở nhiều độ phân giải) gom thành một script chạy một lần (`playrun.sh`); đi từng bước qua MCP chỉ khi còn đang dò
mình cần nhìn gì. Mọi trạng thái Editor đã đổi (cỡ Game view, scene khởi động, Play) trả lại trước khi báo xong.

**Kết quả là asset đã lưu** (`editor-first` › Editor-first). Nghiệm thu bằng hình **sau khi lưu và nạp lại**: lỗi pivot, override
mất chỉ lộ sau khi nạp. So cạnh demo ở cùng tỉ lệ (`DemoCompare.SideBySide`), đo lệch bằng số (`BBox`, `Runs`, `TemplateMatch`),
rồi xem thêm một máy dài và một tablet. Hình đúng hay chưa do developer chốt (`workflow` › Nghiệm thu); agent báo số lệch còn lại.

**Commit chọn file.** Vào Play làm đổi file không thuộc task (config Addressables, scene khởi động, xuống dòng): stage theo danh
sách file của task, không `add` cả thư mục.

## Bẫy

> **Nền tảng** — eval của MCP biên dịch C# 6 (CodeDom): không hàm cục bộ, không `out var`, không pattern matching; `Object` mơ hồ
> giữa `System` và `UnityEngine`; type `internal` không gọi được; mọi type viết đủ namespace.

> **Nền tảng** — Editor mất focus thì frame Play đứng: bật `Application.runInBackground` trong Play (`PlayFrom` tự bật).
> Scene đang mở có thay đổi chưa lưu thì vào Play hay chạy test bật hộp thoại hỏi lưu: Editor đứng, CLI chỉ thấy timeout.

| Triệu chứng | Nguyên nhân · cách tránh |
|---|---|
| Thay đổi trên instance trong scene mất sau khi lưu thành prefab | modification không được ghi lại · sửa trong nội dung prefab (`PrefabEdit.Edit`) hoặc gọi `RecordPrefabInstancePropertyModifications` |
| Sửa prefab gốc mà màn không đổi | override lồng ở variant hay instance che giá trị gốc · `ListOverrides` trên chuỗi variant, gỡ đúng dòng |
| Border đặt xong vẫn như cũ | sprite Multiple bỏ qua `spriteBorder` của importer · `SpriteSlicing.SetBorder` (đi qua sprite data provider) |
| Sprite lấy ra là mảnh vụn | `LoadAssetAtPath<Sprite>` trả mảnh đầu · `SpriteSlicing.LargestSlice` |
| Số đo pixel lệch đều một tỉ lệ | texture nguồn vượt Max Size nên bị thu lúc import · `SpriteSlicing.Report` báo `SHRUNK`, nâng Max Size |
| Chữ TMP có `<sprite=0>` không hiện gì, không lỗi | sprite asset có face info bằng 0 · `TmpSpriteAssetBuilder.Create` |
| Gọi service trong Play làm game kẹt ở màn chờ | gọi trước khi scene chứa service nạp · `PlayProbe.WaitForSceneAsync` |
| Ảnh chụp ngay sau đổi cỡ Game view vẫn bố cục cũ | layout và fitter cập nhật vài frame sau · chờ vài frame, hoặc chụp canvas ngoài Play |
| Ghi giá trị vào SerializedProperty ném lỗi kiểu | property là Float mà ghi `intValue` · đọc `propertyType` trước |
| `manage_camera` không ghi ảnh ra ngoài project, ảnh xuất hiện muộn | lệnh ghi bất đồng bộ vào `Assets/` · `UICapture` ghi đường dẫn bất kỳ |
| Eval thất bại không dữ liệu ngay sau khi vào Play | Editor đang domain reload · thử lại |
