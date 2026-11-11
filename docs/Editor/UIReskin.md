# UIReskin — dựng, soát, chụp và đo prefab UI theo demo

Code: `Editor/UIReskin/`

Bộ hàm tĩnh cho agent khi dựng UI theo ảnh demo: sửa prefab và tạo variant, dựng node UGUI, soát override và reference,
đặt 9-slice, tạo sprite asset TMP, chụp canvas, đo ảnh. Mỗi hàm gọi được bằng **một dòng** từ eval của Editor (MCP
`execute_code`, Unity CLI `script eval`): khung lặp lại (nạp, lưu, dỡ, trả trạng thái) viết một lần ở đây, đã compile thật,
nên eval chỉ còn phần riêng của bài.

Cách dùng chúng trong một task reskin ở `Skills/ui-reskin/SKILL.md`. Đặt cỡ Game view và vào Play từ scene đầu ở
`docs/Editor/Probe.md` (`GameViewSize`, `PlayFrom`).

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | agent, qua eval; developer review code |
| Ranh giới | Horcrux giữ thao tác chung của UGUI, prefab, ảnh. Dự án giữ đường tới màn cần chụp (mở popup, đổi tab) |
| Chỗ đặt | `Editor/UIReskin/`, assembly `com.horcrux.editor`, namespace `Horcrux.Editor.UIReskin` |
| Phụ thuộc | UniText (`LightSide.UniText`) cho chữ; TMP (`Unity.TextMeshPro`) chỉ cho `TmpSpriteAssetBuilder`, dự án còn dùng TMP mới cần. `SpriteSlicing.SetBorder` cần package `com.unity.2d.sprite`: thiếu package thì hàm không compile (define `HORCRUX_SPRITE_EDITOR`), phần còn lại vẫn chạy |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Menu, cửa sổ Editor | người gọi là agent qua eval; developer làm tay đã có Prefab Mode |
| Vẽ chữ lên ảnh so sánh | cần font và shader; vạch lưới theo y tuyệt đối đủ để đọc số hàng |
| Chụp prefab chưa đặt vào scene | chưa có người dùng: popup cần dữ liệu của service mới hiện đúng, nên chụp trong Play |
| Tự gỡ override, tự sửa border khi soát | ghi đè thứ đã author là khó đảo ngược: hàm soát chỉ in, hàm gỡ mặc định chạy thử |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| Ảnh đo theo gốc **trên trái** | số đo khớp phần mềm ảnh và ảnh demo; `Texture2D` gốc dưới trái được lật một lần lúc nạp |
| `PrefabEdit.CreateVariant` lưu variant trống rồi sửa qua `Edit` | sửa trên instance trong scene mà không ghi lại modification thì mất khi lưu; sửa trong nội dung prefab thì thay đổi tự thành override |
| Override nhiễu xác định theo **component trên instance** (cha có LayoutGroup, chính nó có ContentSizeFitter), không theo tên property | cùng `m_AnchoredPosition` là ý đồ ở object tự do và là rác ở con của LayoutGroup |
| `UICapture.Canvas` tắt bật lại behaviour `[ExecuteAlways]` của dự án | fitter đọc bề rộng cha ở `Update`; chụp đồng bộ không có frame nào chạy giữa đổi cỡ và vẽ |
| `UICapture.Variants` chụp trên **copy** trong preview scene, không trên object gốc | nguồn hay là con của prefab asset (không nằm trong scene nào), và mọi giá trị `pose` đặt mất theo copy: không phải trả lại, không làm bẩn prefab đang mở Prefab Mode |
| `TmpSpriteAssetBuilder` ghi `m_Version` | asset version rỗng bị TMP nâng cấp lúc nạp và xoá bảng glyph |

## API

### `PrefabEdit`

| Member | Hợp đồng |
|---|---|
| `Edit(string prefabPath, Action<GameObject> edit)` | nạp nội dung prefab vào scene cô lập, gọi `edit(root)`, lưu, dỡ. `edit` ném thì không lưu, exception đi tiếp. Trả đường dẫn |
| `CreateVariant(string basePath, string variantPath, Action<GameObject> edit = null)` | variant mới (tạo thư mục nếu thiếu), rồi `Edit` nếu có `edit`. Đã có file thì ném |
| `Child(GameObject root, string path)` | `Transform.Find` kể cả object tắt; không có thì ném kèm tên các con của cha gần nhất |
| `AssetByName<T>(string name, string folder = null)` | asset duy nhất đúng tên (kể cả sub-asset như sprite, material); không có hay trùng thì ném kèm đường dẫn |

### `UIBuild` — gọi trong `edit`

| Member | Hợp đồng |
|---|---|
| `Node(Transform parent, string name, Vector2 size = default, Vector2 anchoredPosition = default)` | `RectTransform` neo giữa, pivot giữa, layer UI |
| `Image(parent, name, Sprite sprite, size = default, anchoredPosition = default)` | Sliced khi sprite có border, không thì Simple; `size` zero lấy cỡ gốc; `raycastTarget` tắt |
| `Text(parent, name, string text, float fontSize, UniTextFontStack fontStack, StylePreset look = null, size, anchoredPosition)` | `UniText` một dòng, căn giữa, không xuống dòng, `raycastTarget` tắt; `look` là preset kiểu chữ (viền, bóng) gắn vào Style Presets |
| `Stretch(RectTransform rect)` | anchor 0–1, offset 0 |

### `PrefabAudit`

| Member | Hợp đồng |
|---|---|
| `Report(string folder)` | mỗi prefab: `variant of`, số override từng instance ngoài cùng (`noise n`), `missing script`, `missing ref`, `listener` (UnityEvent lưu sẵn: object, method; target `null` là gãy) |
| `ListOverrides(string prefabPath, string filter = null)` | một dòng mỗi override (định dạng dưới), `+` object, component thêm vào, `-` component gỡ. Override bắt buộc (vị trí, tên root instance) không in |
| `RevertOverrides(string prefabPath, Func<string, bool> select, bool apply = false)` | gỡ override có dòng được `select` chọn; `apply` false chỉ in. Override bắt buộc không bao giờ gỡ |
| `PruneNoise(string prefabPath, bool apply = false)` | `RevertOverrides` với mọi dòng `~` |

Dòng override: `~ variant | boosters/booster_1:RectTransform.m_AnchoredPosition.x = 12`

| Phần | Nghĩa |
|---|---|
| `~` | nhiễu: anchor, vị trí của con LayoutGroup · cỡ của con Grid hay của trục `childControl` · cỡ theo trục ContentSizeFitter không Unconstrained · `m_TextStyleHashCode` |
| trước `|` | instance ngoài cùng (root của variant in tên root) |
| sau `|` | object trên instance `:` component `.` property `=` giá trị (reference in tên object) |

### `HiddenArt` — art trên skin cũ đã tắt

Skin cũ tắt để giữ cây và merge với prefab gốc, nhưng sprite nó giữ vẫn nạp theo prefab, kéo cả trang atlas cũ. Reskin không
dùng lại art kiểu cũ: gỡ reference art, giữ object.

| Member | Hợp đồng |
|---|---|
| `List(string prefabPath)` | mỗi gốc tắt một dòng `gốc \| N ref \| texture, ...`: object tắt ngoài cùng (`""` khi chính root tắt), hay `object:Type` cho component tắt trên object đang bật |
| `Clear(string prefabPath, string[] roots, bool apply = false)` | gỡ mọi Sprite, Texture2D (kể cả phần tử mảng, như sprite của UV module ParticleSystem) mà component dưới các gốc giữ; object, component giữ nguyên. Gốc đang bật thì ném. Trên variant, gỡ thành override của variant. `apply` false chỉ in `object:Type.property = texture` |

Gốc tắt có thể là skin cũ, cũng có thể là trạng thái code bật lên (ô thưởng, nút chỉ hiện ở một màn): đọc `List` rồi chọn gốc
theo tay. Prefab gốc của variant còn giữ art; build nướng variant thành prefab đầy đủ nên art đã gỡ không theo vào bundle, và
`AtlasAudit.Usage` không đếm art đi qua prefab gốc.

### `SpriteSlicing`

| Member | Hợp đồng |
|---|---|
| `Report(string folder)` | mỗi texture Sprite: cỡ nguồn, `max`, `SHRUNK` khi nguồn vượt Max Size (rect và border đã co, số đo từ art gốc lệch), mode, PPU; từng mảnh với rect, border `(trái,dưới,phải,trên)` |
| `LargestSlice(string texturePath)` | mảnh lớn nhất. `LoadAssetAtPath<Sprite>` trả mảnh đầu, có khi là mảnh vụn |
| `SetBorder(string texturePath, Vector4 border, string spriteName = null)` | Single hay Multiple; trả `tên: cũ -> mới` hoặc `(giữ)`. `spriteName` null chỉ khi có đúng một mảnh |

### `ComponentTransplant`

| Member | Hợp đồng |
|---|---|
| `CopyLogic(GameObject source, GameObject destination, Func<Component, bool> select = null)` | chép component (mặc định mọi `MonoBehaviour` không phải `UIBehaviour`), giữ giá trị; reference trỏ vào `source` nối lại tới bản chép, tới `destination`, hay tới component cùng kiểu trên `destination`. Trả dòng `copy`, `remap`, `unmapped`. Không tắt, không xoá `source` |

### `TmpSpriteAssetBuilder`

| Member | Hợp đồng |
|---|---|
| `Create(string texturePath, string assetPath)` | mỗi mảnh một glyph theo thứ tự (`<sprite=i>`); face info: point size bằng mảnh cao nhất, ascent 80%, descent −20%; glyph cách hai bên 3.5% chiều cao, mảnh thấp hơn căn giữa dọc; material `TextMeshPro/Sprite`; thiếu shader (dự án không có TMP Essential Resources) thì ném. Đã có file thì ném. Chữ UniText dùng catalog `UniTextSprites` + `SpriteModifier`, không dùng tool này |

### `UICapture`

| Member | Hợp đồng |
|---|---|
| `GameAsync(string pngPath, CancellationToken ct = default)` | Play: chờ cuối frame, ghi PNG đúng cỡ Game view, gồm canvas Overlay |
| `Game(string pngPath)` | như trên, không chờ: file có sau một frame (eval tiếp theo đọc được) |
| `Canvas(Canvas canvas, int width, int height, string pngPath)` | ngoài Play: canvas tạm sang Screen Space–Camera với camera tạm vẽ vào RenderTexture width×height, CanvasScaler tính theo cỡ đó; trả canvas như cũ ở mọi đường ra. Vì vẽ ở chế độ Camera, lỗi chỉ có ở Overlay (material hạt không dùng shader UI) không lộ: kiểm bằng `Game` trong Play. `UIParticle` ngoài Play chỉ bake một lần mỗi frame: dựng object và chụp ở hai eval |
| `Variants(GameObject source, int width, int height, Color background, int count, Action<GameObject, int> pose, string pngPath, int columns = 1)` | ngoài Play, không chạm scene đang mở: copy `source` (object trong prefab asset hay scene) lên canvas tạm, không CanvasScaler (1 đơn vị = 1 px), giữa khung; mỗi lượt `i` gọi `pose(copy, i)` rồi chụp; ghép lưới như `FrameSheet.Compose`, ảnh 0 trên trái. Dùng để so một look ở vài giá trị thông số (lớp vẽ, độ toả, offset dải sáng, scale) mà không vào Play. Copy tách khỏi nguồn: modifier trong Styles riêng của chữ UniText đổi trên copy không lọt về asset; thứ copy chỉ tham chiếu (preset, material, ScriptableObject) thì `pose` sửa là sửa asset dùng chung |

### `DemoCompare` — toạ độ gốc trên trái, `RectInt(x, y, width, height)` theo hệ đó

| Member | Hợp đồng |
|---|---|
| `SideBySide(string demoPath, string capturePath, string outPath, int y0 = 0, int y1 = -1)` | demo trái, ảnh chụp đưa về cỡ demo bên phải, khe 20 px hồng; dải hàng `y0..y1`; vạch vàng mỗi 100 px theo y tuyệt đối, dày 3 px ở mỗi 500 |
| `BBox(string imagePath, RectInt region, Func<Color32, bool> match)` | `x0,y0 x1,y1 (w x h)`, góc tính vào; không có là `none` |
| `Near(Color32 color, int tolerance)` | điều kiện cho `BBox`: lệch từng kênh RGB ≤ tolerance |
| `Runs(string imagePath, bool vertical, int index, int from, int to, int tolerance = 30, int minLength = 3)` | dải màu liền dọc cột (hay hàng) `index`: `a-b r,g,b`; lệch tổng ba kênh so với pixel đầu dải quá tolerance là dải mới |
| `TemplateMatch(string artPath, string imagePath, RectInt region, float minScale, float maxScale, float step = 0.02f)` | chỗ và tỉ lệ đặt art (PNG có alpha) khớp nhất: `x,y w x h scale s rmse e`, x, y góc trên trái, `s` = w / bề rộng art; so pixel art alpha ≥ 128. Dò thô ở ảnh thu nhỏ (tới 8 lần, art thu còn ≥ 8 px) với bước `step`, rồi dò tinh quanh điểm thô, mỗi bề rộng nguyên trong ±step một lần. Vùng 700×600 với art ~300 px mất ~3 s. rmse cao mà vị trí đúng thường là chữ, số vẽ đè trên art trong demo |
| `ContactSheet(string folder, string outPath, int cell = 256, int columns = 6)` | ghép mọi PNG theo tên thành lưới (ô giữ tỉ lệ, nền xám); trả `i: tên WxH` theo thứ tự ô |
| `Diff(string demoPath, string capturePath, params RectInt[] exclude)` | ảnh chụp đưa về cỡ demo; mỗi pixel ngoài `exclude` lấy kênh RGB lệch nhất (0–255): `mean m p95 p max x at x,y (n px)`; không còn pixel là `none`. Số lệch còn lại để báo sau khi dựng |
| `FitRadialGlow(string demoPath, params RectInt[] exclude)` | dò thông số shader `Horcrux/ui_radial_glow` (`Shader/ui_radial_glow.shader`) từ demo mà rect nền phủ kín: trả `RadialGlowFit`. Fit trong không gian màu project (Linear thì linear, đúng chỗ shader nội suy): lưới thô trên ~1500 mẫu, dò mẫu trên ~40 000; màu tâm, mép giải đóng bằng hồi quy theo trọng số. Demo 363×783 mất ~13 s. Ngoài `exclude` còn dưới 16 pixel thì ném |
| `RadialGlowFit` | `Center` (uv gốc dưới trái), `Radius` (× chiều cao rect), `Falloff`, `CenterColor`, `EdgeColor` (màu Inspector), `FittedInLinear`, `ModelDiff` (bản render mô phỏng so với demo, định dạng `Diff`). `ApplyTo(Material)` ghi đúng các ô và lưu asset; material không dùng `ui_radial_glow` thì ném. `ToString()` in một dòng theo tên ô |

`exclude` của `Diff`, `FitRadialGlow` gồm mọi thứ không phải nền: logo, chữ, cả **khung viền ảnh demo** — demo xuất từ tool thiết kế
có thể kèm viền xám dày khác nhau mỗi cạnh; đo bằng `Runs` ở mép trước khi tin số `max`. `GlowShape.Weight` là bản C# của frag
trong shader: sửa shader thì sửa theo, test `FitRadialGlow_RecoversRenderedParameters` dùng oracle viết riêng để bắt lệch.

## Ví dụ eval

Eval của MCP biên dịch C# 6: viết đủ namespace, `UnityEngine.Object` thay `Object`, không hàm cục bộ, không `out var`.

```csharp
var art = "Assets/MyGame/Arts/UI/Popup/";
Horcrux.Editor.UIReskin.PrefabEdit.CreateVariant("Assets/MyGame/Popups/Parts/popup_btn.prefab",
    "Assets/MyGame/Popups/Parts/popup_btn_red.prefab", root =>
    {
        root.GetComponent<UnityEngine.UI.Image>().sprite = Horcrux.Editor.UIReskin.SpriteSlicing.LargestSlice(art + "btn_red.png");
    });
return Horcrux.Editor.UIReskin.PrefabAudit.Report("Assets/MyGame/Popups");
```

## Kiểm thử

`Tests/EditMode/UIReskinTests.cs`: `BBox`, `Runs` theo gốc trên trái; `TemplateMatch` tìm đúng chỗ art đặt sẵn; `SideBySide` ghép
đúng cỡ; `Diff` chỉ đếm pixel ngoài `exclude`; `FitRadialGlow` lấy lại tâm, bán kính, falloff, hai màu (±1 trên 255) của vầng sáng
render bằng oracle riêng, có khối logo che giữa; `PruneNoise` gỡ vị trí do LayoutGroup ghi và giữ cỡ cố ý, chạy thử không đổi file; `SetBorder` trả giá trị cũ và ghi;
sprite asset TMP mang face info, metric, version; `CopyLogic` nối reference sang object mới. Asset tạm ở
`Assets/HorcruxUIReskinTestTemp`, xoá sau mỗi test. `Tests/EditMode/HiddenArtTests.cs`: `List` gom theo object tắt ngoài cùng;
`Clear` chạy thử không đổi, gỡ thật chỉ thành override của variant, từ chối gốc đang bật; art đã gỡ trên variant không còn trong
`AtlasAudit.Usage`.

Kiểm bằng chạy thật trên một dự án: `Report` 16 prefab popup; `ListOverrides` khớp override thấy trong Inspector; `Canvas` chụp
1080×2340 và 1536×2048; `TemplateMatch` tìm art tim trong demo khớp khung bao màu đỏ đo bằng `BBox`. `FitRadialGlow` trên demo
màn loading 363×783 (loại logo, chữ, khung viền): bản mô phỏng lệch mean 0.46, p95 1; material ghi số đó trên canvas Overlay, chụp
trong Play ở 1080×2340, `Diff` với demo mean 0.51, p95 1 — max 14 ở bóng logo sát vùng loại trừ, có ở cả hai. `HiddenArt.Clear` gỡ 280
reference art trên skin cũ của PopupShop, Pause, Setting, RefillLive: các popup thôi nạp hai atlas cũ (một trang 1024×2048 của
shop) và một texture lẻ; chụp trong Play không đổi hình, không lỗi mới.

## Mở rộng sau

- `FitRadialGlow` nhận vùng của rect nền trên demo, khi có nền không phủ kín demo (panel, popup). Bản hiện tại coi cả ảnh là rect.
- Fit cho shader gradient khác (`vertical_screen_gradient`) theo cùng khuôn `GlowShape` + hồi quy màu, khi có màn dựng bằng nó.

- Chụp prefab chưa đặt vào scene (canvas tạm trong preview scene), khi có bài cần xem bố cục khối mà không vào Play.
- `PruneNoise` nhận thêm driver của dự án (fitter tự viết khai `DrivenRectTransformTracker`), khi override do chúng ghi xuất hiện.
