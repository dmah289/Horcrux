---
name: ui-reskin
description: Dùng khi dựng hoặc reskin UI hay art world (SpriteRenderer trên vật thể) theo ảnh demo, hay thêm art: cách tổ chức prefab UI để đúng demo và dễ mở rộng, chữ art nhiều lớp (màu, viền, bóng đổ) đúng số theo demo, chia atlas và bundle cho art UI, cách làm việc với Editor qua công cụ (MCP, eval) khi dựng, soát, chụp, đo, và hành vi của UGUI, prefab, sprite, TMP cần biết.
---

# ui-reskin — prefab UI theo demo

Skill dưới `Agent/horcrux.md` (NT, tầng, ký hiệu định nghĩa ở đó). Tool: `docs/Editor/TmpToUniText.md` (chuyển chữ TMP sang UniText, giữ kiểu viền bóng) · `docs/Editor/UIReskin.md` (dựng,
soát, chụp, đo) · `docs/Editor/Probe.md` (cỡ Game view, vào Play, chờ scene, bấm) · `docs/Editor/SpriteAtlasBuilder.md` (soát, dựng
atlas) · `docs/ToolUsage/UIParticle.md`, `docs/ToolUsage/Spine.md` (hạt và Spine trên canvas, khi dự án dùng hai thư viện đó).

## Tổ chức prefab

**Skin là lớp hình đặt lên prefab đang có hành vi, không phải prefab viết lại.** Script, address, reference đã nối là hợp đồng
với code; dựng lại từ đầu là nối lại từng ô và mất override của dự án nguồn. Dạng thường gặp: variant của prefab gốc, cây
hình cũ **tắt** thay vì xoá — prefab gốc còn nhận bản cập nhật từ nơi khác thì merge không vỡ, và phát triển tiếp còn thấy chỗ
cũ. Tắt chỉ an toàn khi code không với tới object tắt: `GetComponentInChildren`, `GetComponentsInChildren(false)` bỏ qua object
tắt, reference kéo thả thì không. **Cây giữ, art cũ thì gỡ** (mục Atlas): art khác style không quay lại.

**Nguồn hình là màn tham chiếu (demo, prefab đã reskin) thì mọi thuộc tính hình lấy từ đó; prefab cũ chỉ góp hành vi.** Màu,
nhãn mà skin cũ gán theo ý nghĩa (đỏ cho thoát, vàng cho chơi lại) cũng là skin cũ: giữ lại là trộn hai skin. Vai mà tham chiếu
không có hình cho thì hỏi (mục cuối), không lấy từ skin cũ.

**Đổi art của một khối thì soát mọi nơi còn vẽ art cũ** (tìm theo GUID): bản sao hình của khối trong tool thiết kế, preview, prefab
song song không báo lỗi khi lệch skin, chỉ lộ khi người khác mở. Nêu từng nơi cho developer quyết, không tự đổi theo.

**Thanh tiến độ có trang trí trong lòng (vạch chia, vân) thì cắt, không co.** Co kéo méo trang trí và trượt vạch; cắt giữ art đứng
yên. Mốc dữ liệu đặt trùng mốc art (bước k/tổng nằm ở tâm vạch k) để mỗi lần dừng, đầu ruột chạm đúng một vạch. Cắt hay che một
renderer thì làm trong chính nó (shader, tham số theo renderer); mask dùng chung kéo theo mọi renderer khác cùng dải.

**Thứ lặp lại là một khối dùng chung; khác nhau ở dữ liệu là variant; khác hình dạng là khối riêng.** Tiêu chí: đổi kiểu (khung,
nút, font) ở một chỗ thì mọi chỗ theo, và thêm một mục mới là *thêm* một variant hay kéo một khối vào, không chép. Số tầng
variant theo số **lý do thay đổi** thật (kiểu chung → loại → gói), không theo cho đủ bộ (NT2). Sửa cho một nhóm variant thì
đặt ở **prefab gốc hẹp nhất phủ đúng nhóm đó**: chép sang từng variant là n bản buộc khớp; đặt ở gốc rộng hơn kéo theo nhóm
khác — cần thì override tại gốc tầng giữa (`PrefabAudit.ApplyOverrides`).

**Vị trí do layout tính, số tay chỉ cho thứ demo đặt tự do.** LayoutGroup, ContentSizeFitter xếp và co theo nội dung: thêm bớt
mục không phải đo lại. Override mà layout ghi đè mỗi lần tính là nhiễu — che thay đổi của prefab gốc và làm diff vô nghĩa;
soát và gỡ sau mỗi lần dựng (`PrefabAudit.PruneNoise`). Gỡ hàng loạt thì xem danh sách trước: cùng một property có thể là ý đồ.

**Demo là nguồn số đo.** Canvas có reference resolution bằng cỡ demo thì một pixel demo là một đơn vị UI: đo demo bằng pixel,
dựng đúng số đó. Rect của art gồm cả bóng và viền trong suốt, nên khung đo trên demo nhỏ hơn rect. Số hiển thị (giá, số lượng,
đồng hồ) đọc từ dữ liệu thật; demo là chữ giữ chỗ.

**Chữ đổi theo dữ liệu thì co trong vùng art cho phép, không đặt cỡ cố định.** Giá, số lượng, chữ dịch dài ngắn theo dữ liệu
và ngôn ngữ; cỡ cố định vừa chữ demo sẽ tràn khi giá nhiều chữ số hơn hay sang ngôn ngữ dài hơn. Mỗi lần thay art, xét từng
chữ: chữ nào có thể đổi thì bật autosize, cỡ tối đa là cỡ demo. Khung co là **vùng chữ được phép nằm**: lòng nút trừ viền,
gờ sáng, bóng, chỗ của icon. Đó không phải rect của Image, vì rect gồm cả phần trong suốt và trang trí, chữ co trong đó vẫn
đè lên viền. Đo vùng đó trên art (`DemoCompare.InnerRegion`), đặt rect của chữ đúng vùng, nghiệm thu bằng chuỗi dài nhất có
thật; soát cả thư mục prefab sau mỗi lần thay art bằng `PrefabAudit.TextFit`. Khung mọc cao theo số dòng thì bề ngang chữ vẫn
theo lòng. Mỗi loại chữ co theo
chiều nào thì xem `async-presentation` › tra cứu › Renderer và chữ. Chữ có phần chạy theo nhịp
(chấm loading, số đếm) neo về phía phần đứng yên: căn giữa thì phần đứng yên trượt mỗi nhịp. Khung chữ trên demo là trạng thái dài
nhất.

**Chữ art là một chồng lớp; dựng đủ mọi lớp demo có, mỗi lớp đúng số.** Thường gồm ruột (màu phẳng hay gradient), một hay
vài vòng viền, bóng đổ lệch xuống hoặc chéo, đôi khi gờ sáng, bóng trong. Bóng đổ cho chữ khối nổi: thiếu nó chữ dẹt dù màu
và khung đều đúng. Trước khi chọn kiểu chữ, tách chữ demo thành lớp bằng số: cắt một đường qua nét dày từ nền vào tới ruột,
kéo tiếp sang phía bóng; mỗi dải màu là một lớp, cho màu và bề dày px (`DemoCompare.Layers`). Màu lấy ở lõi dải, không ở pixel mép (khử răng cưa
trộn màu lớp kề); ruột gradient thì đo cả hai đầu. Bề dày đo bằng px ở cỡ demo rồi mới đổi ra đơn vị của kiểu chữ: đơn vị
tương đối theo cỡ chữ thì đổi cỡ là đổi bề dày. Kiểu chữ có sẵn chỉ dùng khi khớp số từng lớp; giống bằng mắt chưa đủ, và
sửa kiểu dùng chung là đổi mọi chữ khác đang dùng nó. **Nghiệm thu theo lớp:** cùng đường cắt trên demo và ảnh chụp ra cùng
số dải, màu và bề dày từng dải khớp (`DemoCompare.LayerDiff`). Khung bao đo bằng màu ruột không thấy viền và bóng; lớp mỏng chiếm ít pixel nên chìm
trong `Diff` trung bình. Màu đặt đúng số mà ảnh chụp ra khác thì tìm khâu đổi màu (không gian màu, alpha cha, tint, lớp
đè), không chỉnh mò tới khi giống.

**Neo theo màn, không theo một máy.** Nền và dải màu phủ ra ngoài safe area (tai thỏ, thanh home); nội dung bấm được nằm trong.
Khối thiết kế theo một bề rộng thì giữ bề rộng đó: màn hẹp hơn thu nhỏ đều, màn rộng hơn (tablet) nằm giữa. Art vẽ sẵn chi tiết
(bóng sàn, khe) thì con đặt theo pixel của ảnh và phóng cùng ảnh. Fitter chạy trong Editor khai giá trị nó ghi là driven
(`DrivenRectTransformTracker`, `overrideSprite`) để đổi Game view không làm bẩn asset.

**Khung nổi (bubble, tooltip) xếp ràng buộc cứng trước, thẩm mỹ sau.** Nằm trọn trong vùng an toàn và chỉ đúng vật được nói
tới là cứng; phía ưu tiên, mũi tên giữa hộp là đẹp. Thiếu chỗ thì phần đẹp nhường: đổi sang phía còn chỗ, hộp trượt vào trong
màn, mũi tên rời giữa hộp nhưng vẫn chỉ đúng vật.

**9-slice là dữ liệu của art.** Đọc border hiện có trước khi ghi; art hai màu (thân và chân khác màu) chỉ đổi cạnh trên, dưới
để vạch chia không bị kéo giãn. Chân thẻ dày mỏng khác art thì chỉnh `pixelsPerUnitMultiplier` trên Image, không sửa border.

**Câu hỏi về hành vi thuộc developer.** Nút trên demo chưa có logic, nút cũ không còn chỗ trên demo, chữ demo khác chữ code: hỏi
trước khi dựng (NT1), ghi câu trả lời vào tài liệu thiết kế của màn.

## Atlas

Màn dựng xong mà art còn lẻ vẫn vẽ đúng, nên thiếu atlas không lộ qua ảnh chụp; soát bằng `AtlasAudit` (`docs/Editor/SpriteAtlasBuilder.md`)
mỗi khi thêm art UI, dựng bằng `SpriteAtlasBuilder`.

**Chia atlas theo thứ hiện cùng lúc, không theo tên thư mục art.** Mỗi lần đổi texture giữa hai Graphic kề nhau là một batch, mỗi
atlas nạp là cả trang vào bộ nhớ: một màn vẽ từ ít atlas nhất, và không nạp trang mà phần lớn nó không dùng. Art của một màn vào
atlas của màn; art nhiều màn cùng dùng vào atlas chung hoặc atlas của bên luôn có mặt (khung popup mở trên mọi màn). Thư mục art là
packable của atlas để art mới thả vào tự vào atlas; thứ không nên pack thì dời sang thư mục khác (GUID giữ, reference không vỡ; đường dẫn viết bằng chữ trong code, bảng layout thì
vỡ — grep đường dẫn cũ sau mỗi lần dời).

**Thứ không nên pack: to hơn trang, hoặc chỉ một trong vài bản hiện.** Nền toàn màn vượt trang thì để lẻ; nền theo loại máy
(mobile, tablet) chỉ một bản hiện, pack chung là nạp cả hai. Sprite to nhưng vừa trang (`BIG`) thì so hai phương án bằng số — trang
nhỏ hơn cộng texture lẻ, với trang lớn chứa nó: texture lẻ cỡ lẻ trên Android rơi về format nặng hơn nên thường thua. Art của
scene nằm sẵn trong build (boot, loading) không vào atlas đi Addressables: scene kéo cả trang vào build thêm một bản và giữ trang
trong bộ nhớ suốt lúc scene sống — để lẻ, hoặc atlas riêng không đi Addressables. Soát bằng Addressables Analyze, rule
`Check Scene to Addressable Duplicate Dependencies`.

**Atlas mà nhiều bundle cùng dùng là entry Addressables tường minh**, không thì mỗi bundle chép một bản (`DUP`). Group theo vòng
đời: thứ mọi màn dùng nạp lúc boot, thứ của một màn đi với màn. Khai ở bảng layout của dự án (`AddressablesLayout`), không thêm tay.
Cài đặt importer theo spec atlas sẵn có của dự án, không đặt mới cho từng atlas.

**Reference serialize là dependency dù không dùng.** Ô của chế độ đang tắt (mesh trên renderer hạt vẽ Billboard, material của
lớp tắt) vẫn kéo asset và mọi thứ asset đó trỏ vào bundle; thêm VFX hay prefab của bên thứ ba thì đọc `GetDependencies` của
prefab nhận, gỡ ô thừa.

**Object tắt vẫn nạp art của nó.** Skin cũ tắt để giữ cây (mục trên) mà còn giữ sprite thì kéo theo cả trang atlas cũ — `Usage`
báo `off`. Reskin đổi style nên art cũ không dùng lại: gỡ reference art trên cây skin cũ, giữ object (`HiddenArt`,
`docs/Editor/UIReskin.md`). Trên variant, gỡ là override của variant: prefab gốc không đổi, merge vẫn sạch. Gốc tắt không phải
lúc nào cũng là skin cũ — ô thưởng, nút chỉ hiện ở một màn, trạng thái code bật lên thuộc skin mới, giữ art: chọn gốc từ `List`
theo tay, rồi vào Play mở từng màn đã gỡ, xem hình và console.

**Nghiệm thu bằng số trước–sau** trên cùng màn: batch trong Play (`UnityEditor.UnityStats.batches`), số trang và fill
(`AtlasAudit.Atlases`), không còn `DUP`, texture lẻ ngoài thứ cố ý để lẻ, hay atlas cũ chỉ object tắt giữ; ảnh chụp trước–sau
sai khác ~0.

## Làm việc qua Editor tool

Luật chung ở `editor-first` › Agent thao tác Editor qua công cụ (asset đã lưu, gọi helper, nhìn trước khi sửa, trả trạng
thái Editor). Riêng UI:

- **Nhìn trước khi sửa** bằng dump cây và override (`PrefabDump`, `PrefabAudit.ListOverrides`), contact sheet art
  (`DemoCompare.ContactSheet`), báo cáo sprite (`SpriteSlicing.Report`). Sau khi thay khối (`PrefabEdit.ReplaceWithPrefab`)
  hay ghép prefab, soát wiring bằng `PrefabDump.Refs`: ô trỏ sai chỗ không lỗi nào báo tới lúc bấm.
- **Edit mode cho dựng và soát; Play cho thứ cần dữ liệu thật.** Bố cục khối chụp canvas ngoài Play (`UICapture.Canvas`); một
  look ở vài giá trị thông số thì chụp copy, mỗi ô một giá trị (`UICapture.Variants`; vật world `UICapture.World`, đúng px
  mỗi unit của demo). Màn cần service (chữ do script đặt, popup mở qua navigator) thì vào Play; chụp cùng bộ màn ở nhiều cỡ
  gom một lần `playrun.sh` với `SIZES`.
- **Hình đúng ở Prefab Mode mà sai ở Play:** liệt kê điều kiện vẽ khác nhau giữa hai nơi (chế độ render canvas, camera, mask,
  quality), lật từng cái trên bản chạy, không lưu, tới khi gọi tên được nguyên nhân rồi mới sửa asset.
- **So với demo** ở Game view đúng cỡ demo và trên máy giả lập theo `demo-compare`. Hình đúng hay chưa do developer chốt;
  agent báo số lệch còn lại.

## Tra cứu — `ui-reskin/reference.md`

| Chạm | Mục |
|---|---|
| dựng, lưu, apply prefab; sprite, border, atlas, bundle; hạt và shader trên canvas; chữ TMP, UniText; SerializedProperty; ảnh chụp lệch, diff có dòng lạ | Hành vi UGUI, prefab, sprite cần biết |
