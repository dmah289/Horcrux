---
name: ui-reskin
description: Dùng khi dựng hoặc reskin UI hay art world (SpriteRenderer trên vật thể) theo ảnh demo, hay thêm art: cách tổ chức prefab UI để đúng demo và dễ mở rộng, chữ art nhiều lớp (màu, viền, bóng đổ) đúng số theo demo, chia atlas và bundle cho art UI, cách làm việc với Editor qua công cụ (MCP, eval) khi dựng, soát, chụp, đo, và các bẫy của UGUI, prefab, sprite, TMP đã trả giá.
---

# ui-reskin — prefab UI theo demo

Skill công cụ dưới `Skills/horcrux_skill.md` (NT, ba tầng, ký hiệu định nghĩa ở đó). Tool: `docs/Editor/TmpToUniText.md` (chuyển chữ TMP sang UniText, giữ kiểu viền bóng) · `docs/Editor/UIReskin.md` (dựng,
soát, chụp, đo) · `docs/Editor/Probe.md` (cỡ Game view, vào Play, chờ scene, bấm) · `docs/Editor/SpriteAtlasBuilder.md` (soát, dựng
atlas) · `docs/ToolUsage/UIParticle.md`, `docs/ToolUsage/Spine.md` (hạt và Spine trên canvas, khi dự án dùng hai thư viện đó).

Mục dưới là **tiêu chí và lý do**, không phải trình tự. Bài nào có đường ngắn hơn mà vẫn đạt tiêu chí thì đi đường đó (NT6),
nói lý do và phép kiểm.

## Tổ chức prefab

**Skin là lớp hình đặt lên prefab đang có hành vi, không phải prefab viết lại.** Script, address, reference đã nối là hợp đồng
với code; dựng lại từ đầu là nối lại từng ô và mất override của dự án nguồn. Dạng thường gặp: variant của prefab gốc, cây
hình cũ **tắt** thay vì xoá — prefab gốc còn nhận bản cập nhật từ nơi khác thì merge không vỡ, và phát triển tiếp còn thấy chỗ
cũ. Tắt chỉ an toàn khi code không với tới object tắt: `GetComponentInChildren`, `GetComponentsInChildren(false)` bỏ qua object
tắt, reference kéo thả thì không. **Cây giữ, art cũ thì gỡ** (mục Atlas): art khác style không quay lại.

**Nguồn hình là màn tham chiếu (demo, prefab đã reskin) thì mọi thuộc tính hình lấy từ đó; prefab cũ chỉ góp hành vi.** Màu,
nhãn mà skin cũ gán theo ý nghĩa (đỏ cho thoát, vàng cho chơi lại) cũng là skin cũ: giữ lại là trộn hai skin. Vai mà tham chiếu
không có hình cho thì hỏi (mục cuối), không lấy từ skin cũ. *Đã sai một lần:* reskin theo popup tham chiếu nút xanh, agent giữ
nút đỏ, vàng của popup cũ dưới nhãn giả định; developer bắt đổi.

**Đổi art của một khối thì soát mọi nơi còn vẽ art cũ** (tìm theo GUID): bản sao hình của khối trong tool thiết kế, preview, prefab
song song không báo lỗi khi lệch skin, chỉ lộ khi người khác mở. Nêu từng nơi cho developer quyết, không tự đổi theo.

**Thanh tiến độ có trang trí trong lòng (vạch chia, vân) thì cắt, không co.** Co kéo méo trang trí và trượt vạch; cắt giữ art đứng
yên. Mốc dữ liệu đặt trùng mốc art (bước k/tổng nằm ở tâm vạch k) để mỗi lần dừng, đầu ruột chạm đúng một vạch. Cắt hay che một
renderer thì làm trong chính nó (shader, tham số theo renderer); mask dùng chung kéo theo mọi renderer khác cùng dải.

**Thứ lặp lại là một khối dùng chung; khác nhau ở dữ liệu là variant; khác hình dạng là khối riêng.** Tiêu chí: đổi kiểu (khung,
nút, font) ở một chỗ thì mọi chỗ theo, và thêm một mục mới là *thêm* một variant hay kéo một khối vào, không chép. Số tầng
variant theo số **lý do thay đổi** thật (kiểu chung → loại → gói), không theo cho đủ bộ (NT1). Sửa cho một nhóm variant thì
đặt ở **prefab gốc hẹp nhất phủ đúng nhóm đó**: chép sang từng variant là n bản buộc khớp; đặt ở gốc rộng hơn kéo theo nhóm
khác — cần thì override tại gốc tầng giữa (`PrefabAudit.ApplyOverrides`). *Đã sai một lần:* chép layout nút sang các variant anh em; developer bắt dời lên
gốc, và giá trị cũ nằm ở gốc chung với một loại thẻ khác.

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
chiều nào thì xem `async-presentation` › Trình diễn vật thể có chất liệu (khối Nền tảng về autosize). Chữ có phần chạy theo nhịp
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
đè), không chỉnh mò tới khi giống. *Đã sai một lần:* chữ dựng khớp khung và `Diff`, developer nghiệm thu thấy màu lệch,
viền dày mỏng khác demo, bóng đổ bị bỏ hẳn.

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
trước khi dựng (NT5), ghi câu trả lời vào tài liệu thiết kế của màn.

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

**Gọi hàm Horcrux, không chép khung vào eval.** Mở, sửa, lưu, dỡ prefab, dựng node, chụp, đo đã có sẵn và đã compile thật: eval
chỉ còn phần riêng của bài, ít chỗ cho lỗi biên dịch C# 6 và API đoán tên. Khung nào eval tới lần thứ hai mà Horcrux chưa có
thì đề xuất công cụ theo `workflow` (đầu skill đó). *Đã sai một lần:* hàng trăm eval chép cùng khung dump, sửa, chụp, so; đoạn
code phải đào lại từ lịch sử chat nhiều lần · viết lại bộ đọc PNG và dò dải màu bằng Python ngoài Editor trong khi
`DemoCompare.Runs` đã có — demo và ảnh chụp đo bằng cùng một hàm, cùng cột, cùng tolerance mới so được.

**Nhìn trước khi sửa.** Dump cây và field serialize (`PrefabDump`, `PrefabAudit.ListOverrides`), contact sheet art
(`DemoCompare.ContactSheet`), báo cáo sprite (`SpriteSlicing.Report`) trước khi chạm: tên file art và số trong YAML có thể đánh
lừa, giá trị đọc qua engine thì không. Sau khi thay khối (`PrefabEdit.ReplaceWithPrefab`) hay ghép prefab, soát wiring bằng
`PrefabDump.Refs`: ô trỏ sai chỗ không lỗi nào báo tới lúc bấm.

**Edit mode cho dựng và soát; Play cho thứ cần dữ liệu thật.** Bố cục của khối xem được bằng chụp canvas ngoài Play
(`UICapture.Canvas`); một look ở vài giá trị thông số thì chụp copy, mỗi ô một giá trị (`UICapture.Variants`; vật world thì `UICapture.World`, đúng px mỗi unit của demo). Màn cần service (chữ do script đặt, popup mở qua navigator) thì vào Play. Chuỗi Play lặp lại nhiều lần (chụp
cùng bộ màn ở nhiều độ phân giải) gom thành một script chạy một lần (`playrun.sh`, nhiều cỡ qua `SIZES`); đi từng bước qua MCP chỉ khi còn đang dò
mình cần nhìn gì. Hình đúng ở Prefab Mode mà sai ở Play: liệt kê điều kiện vẽ khác nhau giữa hai nơi (chế độ render canvas,
camera, mask, quality), lật từng cái trên bản chạy, không lưu, tới khi gọi tên được nguyên nhân rồi mới sửa asset.
Mọi trạng thái Editor đã đổi (cỡ Game view, scene khởi động, Play) trả lại trước khi báo xong.

**Kết quả là asset đã lưu** (`editor-first` › Editor-first). Nghiệm thu bằng hình **sau khi lưu và nạp lại**: lỗi pivot, override
mất chỉ lộ sau khi nạp. So với demo ở Game view đúng cỡ demo và trên máy giả lập theo `demo-compare`. Hình đúng hay chưa do developer chốt (`workflow` › Nghiệm thu); agent báo số lệch còn lại.

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
| Prefab mở riêng thành khung 0×0; diff có anchor, size, vị trí root về 0 | Apply từ instance là con LayoutGroup ghi cả rect layout đang điều khiển · apply theo từng property, không Apply All; trả rect root về bản commit |
| Border đặt xong vẫn như cũ | sprite Multiple bỏ qua `spriteBorder` của importer · `SpriteSlicing.SetBorder` (đi qua sprite data provider) |
| Đường dẫn của sprite đã pack là file `.spriteatlasv2` | `sprite.texture` trả trang atlas · lấy nguồn bằng `AssetDatabase.GetAssetPath(sprite)` |
| Art đã vào atlas mà bundle vẫn chứa texture nguồn | RawImage, material, sprite asset TMP giữ thẳng texture, không qua atlas · chấp nhận khi nhỏ, hoặc tách art đó ra |
| Sprite lấy ra là mảnh vụn | `LoadAssetAtPath<Sprite>` trả mảnh đầu · `SpriteSlicing.LargestSlice`; art một mảnh mà importer Multiple tự cắt thì `SpriteSlicing.MakeSingle` — đổi mode tay để lại bảng mảnh cũ trong `.meta` |
| Hạt trên canvas vẽ thành khối màu đặc ở Play, Prefab Mode và ảnh chụp canvas vẫn đúng | material hạt dùng shader không dành cho UI, cần dữ liệu camera mà canvas Overlay không có · hạt bake vào canvas (UIParticle) dùng shader UI, kiểm trong Play ở đúng chế độ render của game |
| Hạt UI phải phủ một khối rộng mà vẫn bung từ một điểm; đổi Shape sang Box thì hạt như đứng yên | Shape tính theo đơn vị thế giới, nhân scale bake (UIParticle) và scale các node — đổi ra px theo khung cần phủ trước khi đặt · Box phát dọc trục Z của shape, nhìn từ camera là bay vào màn: Spherical Direction để hạt toả trong mặt phẳng màn |
| Lớp phủ màu đặt cùng sprite với art ra bản tối của art, không ra màu phủ | shader UI mặc định nhân màu Image với texture · phủ bằng bản trắng của art giữ nguyên alpha (`SpriteSilhouette.Create`, art đổi thì gọi lại), hoặc shader thay màu giữ alpha — material khác thì tách batch; màu + % hoạ sĩ fit lại theo `demo-compare` › Bẫy |
| Console báo material "doesn't have a texture property '_MainTex'" từ `Canvas.SendWillRenderCanvases` | Graphic gán texture của nó vào `_MainTex` mỗi lần canvas dựng lại, kể cả khi shader không lấy mẫu texture · shader UI viết tay khai `[PerRendererData] _MainTex`; nằm dưới Mask, RectMask2D thì cần thêm khối stencil, `_ColorMask` và `_ClipRect` như `UI/Default`, thiếu thì không bị cắt |
| Glow màu trên nền màu đậm ra trắng, hồng | blend cộng cộng cả màu nền · glow cần giữ màu dùng alpha blend; cộng chỉ cho ánh sáng trên nền tối |
| Số đo pixel lệch đều một tỉ lệ | texture nguồn vượt Max Size nên bị thu lúc import · `SpriteSlicing.Report` báo `SHRUNK`, nâng Max Size |
| Chữ TMP có `<sprite=0>` không hiện gì, không lỗi | sprite asset có face info bằng 0 · `TmpSpriteAssetBuilder.Create` |
| Không chữ UniText nào được vẽ, ảnh chụp có số batches thấp bất thường | plugin GPU của UniText tách khi package cập nhật lúc Editor chạy · khởi động lại Editor trước mọi phép chụp, đo |
| Đổi màu viền chữ phải sửa từng prefab | viền, bóng đặt thẳng trên component · kiểu chữ là `StylePreset` dùng chung gắn vào Style Presets, như material preset của TMP |
| Gọi service trong Play làm game kẹt ở màn chờ | gọi trước khi scene chứa service nạp · `PlayProbe.WaitForSceneAsync` |
| Ảnh chụp ngay sau đổi cỡ Game view vẫn bố cục cũ | layout và fitter cập nhật vài frame sau · chờ vài frame, hoặc chụp canvas ngoài Play |
| Diff prefab sau khi lưu qua `PrefabEdit` có dòng mình không sửa | Unity ghi lại field mới có mặc định, cờ nội bộ (`m_WasSpriteAssigned`) · đọc diff trước khi commit để biết, không gỡ tay |
| Đổi `SpriteRenderer` từ Sliced, Tiled sang Simple qua script, sprite phóng méo gấp nhiều lần | Editor quy `size` cũ vào `localScale` khi đổi `drawMode` · đặt scale sau khi đổi mode, đọc `bounds` trước khi lưu |
| Ghi giá trị vào SerializedProperty ném lỗi kiểu, hoặc lưu xong giá trị vẫn như cũ | property là Float mà ghi `intValue`; Vector2 mà ghi `vector3Value` thì bỏ qua im lặng, lần lưu prefab vẫn kéo theo nhiễu serialize · đọc `propertyType` trước, đọc lại giá trị sau khi lưu |
| `manage_camera` không ghi ảnh ra ngoài project, ảnh xuất hiện muộn | lệnh ghi bất đồng bộ vào `Assets/` · `UICapture` ghi đường dẫn bất kỳ |
| Eval thất bại không dữ liệu ngay sau khi vào Play | Editor đang domain reload · thử lại |
