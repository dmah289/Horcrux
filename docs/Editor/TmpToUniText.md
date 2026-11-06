Code: `Editor/TmpToUniText/`

# TmpToUniText — chuyển chữ TextMesh Pro sang UniText

Tool Migration của UniText (`Tools → LightSide → UniText → Migration`) thay component, nối reference, viết lại script, nhưng
**bỏ material** của TMP: preset viền, bóng mất theo. `TmpLookMigration` giữ phần đó: chụp bảng *chữ nào dùng material nào* trước khi
migrate, đổi mỗi material thành một `StylePreset`, gán lại preset sau khi migrate. Tài liệu này cũng ghi trình tự chuyển cả dự án và
các bẫy đã trả giá.

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | agent qua eval, một lần cho mỗi dự án chuyển hệ chữ |
| Ranh giới | tool Migration của UniText lo component, reference, script; `TmpLookMigration` chỉ lo kiểu chữ (material → StylePreset) |
| Chỗ đặt | `Editor/TmpToUniText/`, assembly `com.horcrux.editor` (tham chiếu `LightSide.UniText`, `Unity.TextMeshPro`) |
| Bảng chụp | file JSON ngoài project (scratchpad): chỉ sống từ lúc chụp tới lúc gán |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Gộp material gần trùng thành một preset | tên preset giữ tên material designer đã quen; gộp là quyết định của developer |
| Chuyển Bevel, Glow, FaceDilate không có outline | không có modifier tương đương một–một; tool ghi vào cột ghi chú để làm tay |
| Gradient đỉnh chữ (vertex gradient) | cần swatch gradient trong provider; tool in dòng `gradient by hand` kèm hai màu |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| Underlay không lệch (offset 0) thành `StrokeModifier`, không phải `ShadowModifier` | TMP hay giả viền bằng underlay dilate; vẽ như stroke ngoài mới cùng dạng |
| `Width` = 2 × bề dày cần ra ngoài mép | `StrokeModifier` lấy `width × 0.5` làm nửa dải: align +1 chỉ vẽ ra ngoài nửa `Width`. Đo pixel trước–sau khớp |
| Đơn vị em | TMP đo lớp sơn theo khoảng SDF, tỉ lệ với cỡ chữ: một preset dùng được mọi cỡ, như material |
| `CreatePresets` dựng lại preset đã có | giữ GUID, chữ đã gán vẫn trỏ đúng; chỉnh công thức rồi chạy lại là đủ |
| Đường dẫn bản ghi là `tên#thứ tự anh em` từ root | tên trùng trong cùng cha là chuyện thường ở prefab UI; GameObject không đổi khi tool thay component |

## Quy đổi material TMP → lớp sơn

Một đơn vị shader SDF = `_GradientScale` px atlas, nhân scale ratio của lớp; chia `pointSize` của atlas ra em.

| TMP | UniText |
|---|---|
| `_OutlineWidth` W, `_FaceDilate` F (ratio A) | `StrokeModifier` align +1, bề dày ngoài mép `(F·A·GS + W·A·GS/2) / pointSize` em |
| Underlay offset 0, dilate D, softness S (ratio C) | `StrokeModifier` align +1, bề dày `D·C·GS / pointSize`, softness `S·C·GS / pointSize` |
| Underlay có offset (X, Y) | `ShadowModifier` offset `(X, Y)·C·GS / pointSize`, spread theo D, blur theo S |
| `_FaceColor` khác trắng, `BEVEL_ON`, `GLOW_ON` | ghi chú, làm tay |

## Luồng

```
Snapshot(asset paths, json) ── chữ TMP có material/gradient author tại asset đó (kể cả override trên prefab lồng)
   ▼
tool Migration của UniText ── font → component (prefab lá trước, scene sau) → script
   ▼
CreatePresets(json, folder) ── mỗi material (trừ material mặc định của font) → "Text <tên>.asset"
   ▼
Apply(json, folder) ── gán preset vào ô Style Presets của UniText cùng GameObject; in gradient cần làm tay, bản ghi thiếu
```

## API — `Horcrux.Editor.TmpToUniText.TmpLookMigration`

| Member | Hợp đồng |
|---|---|
| `Snapshot(IEnumerable<string> assetPaths, string jsonPath)` | prefab mở nội dung, scene mở cộng thêm rồi đóng; ghi JSON; trả số bản ghi mỗi asset |
| `CreatePresets(string jsonPath, string folder)` | tạo hoặc dựng lại preset; trả dòng `create`/`rebuild` kèm ghi chú phần không mang sang |
| `Apply(string jsonPath, string folder)` | material mặc định thì để trống Style Presets; trả `applied n / m`, dòng `missing`, `gradient by hand` |

## Trước khi chạy

| # | Việc | Thiếu thì |
|---|---|---|
| 1 | Asset Serialization = Force Text; commit sạch; nhánh riêng | tool Migration quét ra 0 hoặc không có đường lùi |
| 2 | Danh sách asset cho `Snapshot`: mọi prefab, scene chứa script GUID của TMP **hoặc** có `propertyPath: m_sharedMaterial` / `m_fontColorGradient` (override trên prefab lồng) | override material trên instance mất mà không báo |
| 3 | `Snapshot` **trước** khi tool Migration thay component | material đã mất, không chụp lại được |
| 4 | Tool Migration: tạo font + stack, preset markup toàn dự án (tag `<b>`, `<color>`, `<size>`, `<br>`… mà runtime ghi) | tag hiện thành chữ thô |
| 5 | Chạy **script stage trước** `Apply` | lưu prefab khi field còn kiểu TMP thì Unity xoá field trỏ chữ |
| 6 | Gắn lại component bị gỡ vì `RequireComponent(TMP)` (ghi ở `ProjectSettings/UniText/MigrationLosses.json`) sau khi script đổi kiểu | timer, typewriter, nhãn số mất hành vi, không lỗi compile |
| 7 | Khởi động lại Editor sau khi cài, cập nhật package UniText | plugin GPU tách: không chữ nào được vẽ |
| 8 | Có `UniTextWorld` thì bật Project Settings → LightSide → Core → Rendering → **Include World Shaders** | build vẫn xanh nếu chữ world chỉ nằm trong prefab Addressables (bộ kiểm của LightSide chỉ quét scene); trên máy chữ world không hiện, log `Shader 'LightSide/World' is unavailable`. Editor vẫn vẽ nên không lộ |

## Bẫy

| Triệu chứng | Nguyên nhân · cách tránh |
|---|---|
| Không chữ UniText nào hiện, Console đỏ `[GpuUpload] … detached` | package cập nhật lúc Editor chạy · khởi động lại Editor |
| Gọi handler của cửa sổ Migration qua eval, quét đứng ở 0% | `EditorApplication.delayCall` đặt từ eval không chạy · gọi tiếp `ProcessChunk` của analyzer cho tới hết |
| Eval treo tới timeout khi viết lại script hay migrate | hộp thoại *Scene(s) Have Been Modified* · nạp lại scene từ đĩa trước khi gọi |
| Chữ 3D to gấp 10 | TMP 3D nhân cỡ chữ 0.1, `UniTextWorld` 1:1; tool Migration không đổi · chia 10 `fontSize`, min, max |
| Khoảng dòng rộng hơn TMP | Line Height Mode mặc định `Scaled` 1.4 em · `Primary`: bước dòng theo metric font, như TMP |
| `MeasureText` ra một dòng cho chữ wrap | thiếu `maxWidth` là đo không wrap; thiếu `maxHeight` là bỏ qua autosize · truyền cả hai bằng khung chữ |
| `MeasureText().x` lệch cả dòng ở chữ world | bề ngang làm tròn lên số nguyên · so bằng chiều cao |
| `GetRangeBounds` mọi dòng cùng `yMin` | ngoài vòng render vị trí glyph chưa dựng · đo bằng `MeasureText` |
| Autosize co chữ thay vì ngắt giữa từ | autosize UniText co cả theo bề ngang (TMP chỉ co theo chiều cao) · kiểm cỡ tối thiểu thay vì kiểm ngắt từ |
| `SetText("{0}", n)` không còn | UniText không có overload format · `SetNumber(int)` (`UniTextExtensionMethods`) |
| Atlas glyph 8 MB | mặc định page 2048², 16 bit · font Latin một bộ: 1024², 8 bit (1 MB), so pixel trước–sau |
| Skin cũ đã tắt vẫn kéo texture sprite TMP vào bundle | catalog `UniTextSprites` tool sinh trỏ texture TMP · gỡ style sprite trên cây tắt |

## Kiểm thử

Chạy thật trên một dự án 201 chữ, 17 material: 209 bản ghi, `Apply` 209 / 209; ảnh chụp trước–sau các màn trùng ở viền (dải màu
viền đo cùng cột pixel), chỉ còn gradient một chữ làm tay.

## Mở rộng sau

- Tự dựng swatch gradient từ `colorGradient` của TMP, khi gặp dự án có nhiều chữ gradient.
- Gom bước tìm danh sách asset (Trước khi chạy #2) vào tool, khi chuyển dự án thứ hai.
