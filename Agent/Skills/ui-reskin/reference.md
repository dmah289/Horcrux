# ui-reskin — tra cứu

Nền tảng và Sổ tay của `ui-reskin`. Đọc mục được bảng Tra cứu trong `SKILL.md` gọi.

## Hành vi UGUI, prefab, sprite cần biết

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
| Console báo material "doesn't have a texture property '_MainTex'" từ `Canvas.SendWillRenderCanvases` | Graphic gán texture của nó vào `_MainTex` mỗi lần canvas dựng lại, kể cả khi shader không lấy mẫu texture · shader UI viết tay khai `[PerRendererData] _MainTex`; nằm dưới Mask, RectMask2D thì cần thêm khối stencil, `_ColorMask` và `_ClipRect` như `UI/Default`, thiếu thì không bị cắt |
| Lớp phủ màu đặt cùng sprite với art ra bản tối của art, không ra màu phủ | shader UI mặc định nhân màu Image với texture · phủ bằng bản trắng của art giữ nguyên alpha (`SpriteSilhouette.Create`, art đổi thì gọi lại), hoặc shader thay màu giữ alpha — material khác thì tách batch; màu + % hoạ sĩ fit lại theo `Memory/demo-compare.md` |
| Glow màu trên nền màu đậm ra trắng, hồng | blend cộng cộng cả màu nền · glow cần giữ màu dùng alpha blend; cộng chỉ cho ánh sáng trên nền tối |
| Số đo pixel lệch đều một tỉ lệ | texture nguồn vượt Max Size nên bị thu lúc import · `SpriteSlicing.Report` báo `SHRUNK`, nâng Max Size |
| Chữ TMP có `<sprite=0>` không hiện gì, không lỗi | sprite asset có face info bằng 0 · `TmpSpriteAssetBuilder.Create` |
| Không chữ UniText nào được vẽ, ảnh chụp có số batches thấp bất thường | plugin GPU của UniText tách khi package cập nhật lúc Editor chạy · khởi động lại Editor trước mọi phép chụp, đo |
| Ảnh chụp ngay sau đổi cỡ Game view vẫn bố cục cũ | layout và fitter cập nhật vài frame sau · chờ vài frame, hoặc chụp canvas ngoài Play |
| Diff prefab sau khi lưu qua `PrefabEdit` có dòng mình không sửa | Unity ghi lại field mới có mặc định, cờ nội bộ (`m_WasSpriteAssigned`) · đọc diff trước khi commit để biết, không gỡ tay |
| Đổi `SpriteRenderer` từ Sliced, Tiled sang Simple qua script, sprite phóng méo gấp nhiều lần | Editor quy `size` cũ vào `localScale` khi đổi `drawMode` · đặt scale sau khi đổi mode, đọc `bounds` trước khi lưu |
| Ghi giá trị vào SerializedProperty ném lỗi kiểu, hoặc lưu xong giá trị vẫn như cũ | property là Float mà ghi `intValue`; Vector2 mà ghi `vector3Value` thì bỏ qua im lặng, lần lưu prefab vẫn kéo theo nhiễu serialize · đọc `propertyType` trước, đọc lại giá trị sau khi lưu |
