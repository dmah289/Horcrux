# Plan — `TextFitFrame` lên Horcrux

Ngày 2026-11-06. Nguồn: category-jam `Assets/_CategoryJam/Scripts/Entities/Box/TextFitFrame.cs` (bản 2026-11-05, chữ UniText),
host thật `CategoryNameTag` trên `name_tab` của `Assets/_CategoryJam/Addressables/Box/CategoryBox.prefab`. Test EditMode
`Assets/_CategoryJam/Tests/EditMode/TextFitFrameTests.cs` (5 case) chuyển sang Horcrux; `CategoryNameTagPrefabTests.cs` ở lại dự án.

**Phân công:** GĐ1–2 developer gõ, agent viết test theo bảng case rồi compile · GĐ3 (tài liệu), GĐ4 (dự án chuyển sang bản
Horcrux, xoá bản tạm, chuyển test) agent làm. **Chạy được, thấy được** ở GĐ1–2 = compile sạch + bảng case xanh trong
`Tests/EditMode/TextFitFrameTests.cs` (assembly `Horcrux.EditModeTests`); thấy trong game ở GĐ4. GĐ1–3 commit trong
`Assets/Horcrux`, GĐ4 commit ở dự án kèm con trỏ submodule. Không phụ thuộc Plan nào, không có `§0`.

## Ngữ cảnh đã chốt

- **Mục tiêu:** nhãn chữ trên khung `SpriteRenderer` Sliced trong world — khung cao theo số dòng, **đáy khung đứng yên**; trần
  số dòng là chiều cao khung chữ trong prefab, tràn thì autosize của UniText co chữ. Host đọc phần cao thêm để tự đặt vị trí.
- **Chỗ đặt:** `Runtime/Utilities/UI/TextFitFrame.cs`, namespace `Horcrux.Runtime.Utilities.UI`; tài liệu
  `docs/Runtime/TextFitFrame/TextFitFrame.md`. `com.horcrux.runtime` và `Horcrux.EditModeTests` đã tham chiếu `LightSide.UniText`
  (không sửa asmdef); `SplitterAttribute` ở `Horcrux.Runtime.Utilities`, namespace cha, không cần `using`.
- **Tên:** giữ `TextFitFrame`; field `text`, `frame` giữ tên (prefab serialize theo tên), GUID script giữ (GĐ4); tên class đổi
  tuỳ ý trước khi gõ. Kiểu chữ `UniTextBase` — nhận cả `UniText` (Canvas) lẫn `UniTextWorld` (host thật).
- **Đo được ở UniText** (`MeasureText`, `UniTextBase_Layout.cs`): autosize co **cả hai chiều** khi tràn `maxWidth × maxHeight`,
  tới `MinFontSize` · thiếu `maxWidth` đo một dòng không wrap, thiếu `maxHeight` bỏ qua autosize — truyền cả hai từ `rect` khung
  chữ · chạy layout ngay lúc gọi, không cần object bật · trả kích thước **outer** (gồm `Padding`) — hiệu hai phép đo triệt tiêu
  padding · chữ rỗng thấp hơn một dòng — hiệu âm kẹp 0 · bước dòng theo Line Height Mode của **cả dự án** (`UniTextSettings`),
  không theo component.
- **Không làm:** tự co chữ hay đo bề ngang (autosize đủ cho mọi tên thật) · tween chiều cao khung (host tự trượt theo
  `ExtraHeight`) · tính lại khi đổi font, cỡ chữ lúc chạy (giá trị authoring) · guard `text`, `frame` null (`NullReferenceException`
  ở `SetText` đầu đã trỏ đúng dòng) · đếm dòng bằng `GetRangeBounds` (lý do ở GĐ1).
- **Đã khảo sát:** `TextFitFrame.cs` dự án (cả cơ chế; bỏ namespace dự án, `using Horcrux.Runtime.Utilities;` thừa) ·
  `CategoryNameTag.cs` (host gọi `fit.SetText(name)` rồi `hiddenLocalPos + Vector3.down * (fit.ExtraHeight * transform.localScale.y)`)
  · `TextFitFrameTests.cs` dự án (bảng case; fixture nạp asset dự án nên không lấy) · `CategoryNameTagPrefabTests.cs` (đo "như lúc
  render"; ở lại dự án) · `CategoryBox.prefab` › `name_tab` (setup thật cho Editor setup GĐ3, số nháp) · UniText
  `UniTextFontProvider` (`Font` trống thì `SystemFont.Default` — fixture không cần asset font) · Horcrux `Utilities/UI/` (tham
  chiếu `UniTextBase` như `UniTextTypewriter`; chưa có cái nào fit khung) · `ContentSizeFitter` + `Image` Sliced (không hợp:
  Canvas, không phải world).

## Luồng mô tả — một tên đi qua khung

```
host ghi tên ──► SetText: chữ vào khung chữ, autosize co khi quá trần dòng ──► ExtraHeight = phần chữ cao hơn một dòng   GĐ1
                                                       ▼
            lần đầu: chụp khung nghỉ (cao, y, pivot), kiểm setup ──► khung cao thêm ExtraHeight, đáy đứng yên              GĐ2
                                                       ▼
            host đọc ExtraHeight để đặt vị trí · tài liệu (GĐ3) · dự án dùng bản Horcrux, test sang Horcrux (GĐ4)
```

## Bản đồ hệ cuối

```
                 SetText(value)                              Text = value · MeasureText(khung) · MeasureText("A")
  host ─────────────────────────────► TextFitFrame ─────────────────────────────────────────────► UniTextBase text
    ▲                                   │                                                          (autosize co trong khung chữ)
    │ ExtraHeight                       │ size.y = nghỉ + ExtraHeight / scaleY
    └───────────────────────────────────┤ localPosition.y = restY + ExtraHeight × pivotY
                                        ▼
                                SpriteRenderer frame (Sliced)
```

---

## Giai đoạn 1 — Chữ vào khung chữ, đo phần cao hơn một dòng

**Mục tiêu:** host `SetText(value)`, chữ hiện trong khung chữ (autosize của UniText co khi quá trần dòng), `ExtraHeight` cho biết
chữ cao hơn một dòng chuẩn bao nhiêu theo không gian cha. Xong giai đoạn này host đặt được vị trí theo số dòng dù chưa có khung.

**Vì sao bây giờ:** `ExtraHeight` là con số duy nhất mọi thứ sau cần — khung (GĐ2) và host đều đọc nó; đo đúng trước, vẽ sau.

### `Runtime/Utilities/UI/TextFitFrame.cs` — file mới

```csharp
using LightSide;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.UI
{
    /// <summary>
    /// Khung Sliced cao theo số dòng của chữ, đáy khung đứng yên. Số dòng tối đa là chiều cao khung chữ trong prefab;
    /// tràn thì autosize của chữ co lại.
    /// </summary>
    public class TextFitFrame : MonoBehaviour
    {
        #region Properties

        [Splitter("References")]
        [Tooltip("Text to wrap. Must share one parent with the frame.")]
        [SerializeField] private UniTextBase text;

        private static readonly TextMeasureOptions OneLine = new() { text = "A" };

        /// <summary>Phần khung cao thêm ở lần fit gần nhất, theo đơn vị không gian cha của khung.</summary>
        public float ExtraHeight { get; private set; }

        #endregion

        #region API

        public void SetText(string value)
        {
            text.Text = value;

            ExtraHeight = MeasureExtraTextHeight() * text.transform.localScale.y;
        }

        #endregion

        #region Class Methods

        /// <summary>
        /// Chữ hiện tại cao hơn một dòng bao nhiêu, theo không gian của chữ; một dòng thì bằng 0. MeasureText chạy layout
        /// ngay lúc gọi nên đo được trong cùng frame đặt chữ, kể cả khi object đang tắt.
        /// </summary>
        private float MeasureExtraTextHeight()
        {
            // Khung chữ truyền cả hai chiều: thiếu maxWidth MeasureText đo một dòng không wrap, thiếu maxHeight thì bỏ qua
            // autosize co chữ tràn; đủ cả hai thì đo khớp lúc render.
            var rect = ((RectTransform)text.transform).rect;
            var box = new TextMeasureOptions { maxWidth = rect.width, maxHeight = rect.height };
            var textHeight = text.MeasureText(box).y;
            var oneLineHeight = text.MeasureText(OneLine).y;
            return Mathf.Max(0f, textHeight - oneLineHeight);
        }

        #endregion
    }
}
```

**Khác với bản dự án:** namespace `Horcrux.Runtime.Utilities.UI` thay `CategoryJam.Gameplay.Entities`; bỏ
`using Horcrux.Runtime.Utilities;` (`Splitter` ở namespace cha). Thân member khớp nguyên văn.

**Quyết định** — `ExtraHeight` là hiệu hai phép đo, không đếm dòng: chữ bị autosize co thì khung cao thêm đúng phần chữ thật sự
vượt một dòng chuẩn, không phải "số dòng × bước dòng" ở cỡ đã co. Phương án bỏ: đếm dòng qua `GetRangeBounds` — ngoài vòng render
mọi dòng cùng `yMin`. — `OneLine` đo không truyền khung: một dòng `"A"` ở cỡ chuẩn (autosize không bị ràng buộc dừng ở
`MaxFontSize`) là mốc "khung prefab vừa một dòng"; truyền khung thì mốc trôi theo cỡ chữ đang co. Struct `static readonly` vì
không đổi giữa các lần gọi. — Nhân `localScale.y` của chữ: `MeasureText` trả theo không gian của chữ, host đọc theo không gian
cha. — `ExtraHeight` công khai: host cần phần cao thêm để tính điểm ẩn.

| Consumes | Produces |
|---|---|
| `LightSide.UniTextBase.Text`, `MeasureText(TextMeasureOptions)`; `RectTransform.rect` của chữ | `TextFitFrame.SetText`, `ExtraHeight` |

**Sơ đồ tương tác sau GĐ1** (`+` là phần mới)

```
  host ──SetText+──► TextFitFrame+ ──Text = · MeasureText(khung) · MeasureText("A")──► UniTextBase
    ▲                    │
    └──── ExtraHeight+ ──┘
```

**Editor setup:** không — fixture test dựng bằng code; prefab thật đã nối sẵn (GĐ4).

**Kiểm:** compile sạch. Dự án lúc này có hai `TextFitFrame` ở hai namespace, không đụng nhau: `CategoryNameTag` chưa `using`
namespace Horcrux.

Fixture `Tests/EditMode/TextFitFrameTests.cs` (agent, không asset dự án): cha `GameObject`; chữ `UniTextWorld` trên
`RectTransform` con của cha, `Font` để trống (UniText lấy font hệ thống làm font chính), `WordWrap` bật, Center, Bottom,
`MinFontSize` nhỏ, `MaxFontSize` = cỡ chuẩn, `AutoSize` bật; chiều cao rect = `MeasureText{text = "A\nA", maxWidth}` đo lúc
`AutoSize` tắt ở cỡ chuẩn cộng một chút dư; `TextFitFrame` gắn trên cha, nối `text` qua `SerializedObject`; `TearDown`
`DestroyImmediate` cha. Bước dòng kỳ vọng tính **độc lập** từ metric font chính (`text.FontProvider.PrimaryFont` sau lần đo đầu:
`FaceInfo.lineHeight / UnitsPerEm × cỡ chuẩn` khi `UniTextSettings.DefaultLineHeightMode` là `Primary`; `LineHeightScale × cỡ
chuẩn` khi `Scaled`), không lấy từ `ExtraHeight`.

| Case | Input | Kỳ vọng |
|---|---|---|
| rỗng | `SetText("")` | `ExtraHeight` 0 |
| một dòng | `SetText("Food")` | `ExtraHeight` 0; chữ ở cỡ chuẩn |
| hai dòng ngắt cứng | `SetText("Food\nFood")` | `ExtraHeight` = bước dòng; cỡ chữ vẫn cỡ chuẩn (`MeasureText{autoSize = false}.y` bằng `MeasureText{khung}.y`) |
| hai dòng do wrap | chữ không có `\n`, rộng hơn `rect.width` | `ExtraHeight` = bước dòng (chứng minh `maxWidth` được truyền) |
| ba dòng tràn | `SetText("Food\nFood\nFood")` | đối chứng `MeasureText{autoSize = false}.y` > `rect.height`; `MeasureText{khung}.y` ≤ `rect.height`; `ExtraHeight` ≤ `rect.height` − một dòng cỡ chuẩn |
| hai dòng rồi một dòng | `SetText` hai dòng rồi `"Food"` | `ExtraHeight` về 0 |
| object tắt trước lần đầu | `SetActive(false)` cha rồi `SetText` hai dòng | `ExtraHeight` = bước dòng |
| chữ scale khác 1 | `text.localScale.y` = 2, hai dòng | `ExtraHeight` = 2 × bước dòng |
| gọi lặp | `SetText` hai dòng hai lần | `ExtraHeight` không đổi giữa hai lần |

**Cheat của giai đoạn:** fixture không layout được (font không nạp, `MeasureText` trả chỉ padding) thì mọi ca 0 xanh giả — ca hai
dòng khẳng định `ExtraHeight` > 0 và ca ba dòng có đối chứng "không co thì tràn" chặn. Kỳ vọng bước dòng chép từ `ExtraHeight`
là test tự chứng minh — tính từ metric font như trên.

**Mốc commit:** `feat(text): TextFitFrame đo phần chữ cao hơn một dòng`.

---

## Giai đoạn 2 — Khung cao theo chữ, đáy đứng yên

**Mục tiêu:** lần `SetText` đầu chụp khung nghỉ (chiều cao, `localPosition.y`, pivot sprite) và kiểm hai lỗi setup không có
exception; mỗi `SetText` đặt `frame.size.y` = nghỉ + `ExtraHeight` và dời khung đúng phần pivot nằm trên đáy. Xong giai đoạn này
tab tên hai dòng có khung ôm đủ hai dòng, đáy khung vẫn ở chỗ prefab đặt.

**Vì sao bây giờ:** khung cộng `ExtraHeight` của GĐ1; chụp nghỉ phải xảy ra trước lần đổi `size` đầu nên sinh cùng giai đoạn.

### `TextFitFrame.cs` › `Properties` › thêm sau `text`, trước `OneLine`

```csharp
        [SerializeField] private UniTextBase text;
        [Tooltip("Sliced frame. Moves up by part of the extra height, so nothing else may tween its position.")]
        [SerializeField] private SpriteRenderer frame;

        private float _restFrameHeight;
        private float _restFrameLocalY;
        private float _framePivotY;
        private bool _hasRestLayout;
        private static readonly TextMeasureOptions OneLine = new() { text = "A" };
```

### `TextFitFrame.cs` › `API` › `SetText` — đoạn cũ → đoạn mới

Cũ:

```csharp
        public void SetText(string value)
        {
            text.Text = value;

            ExtraHeight = MeasureExtraTextHeight() * text.transform.localScale.y;
        }
```

Mới — chụp nghỉ trước lần đổi `size` đầu; khung cao rồi dời:

```csharp
        public void SetText(string value)
        {
            if (!_hasRestLayout) CaptureRestLayout();

            text.Text = value;

            ExtraHeight = MeasureExtraTextHeight() * text.transform.localScale.y;
            frame.size = new Vector2(frame.size.x, _restFrameHeight + ExtraHeight / frame.transform.localScale.y);

            var framePos = frame.transform.localPosition;
            framePos.y = _restFrameLocalY + ExtraHeight * _framePivotY;
            frame.transform.localPosition = framePos;
        }
```

### `TextFitFrame.cs` › `Class Methods` › thêm trước `MeasureExtraTextHeight`

```csharp
        private void CaptureRestLayout()
        {
            if (text.transform.parent != frame.transform.parent)
                Debug.LogError("[TextFitFrame]: text and frame must share one parent; extra height is measured in that space.", this);

            if (frame.drawMode == SpriteDrawMode.Simple)
                Debug.LogError("[TextFitFrame]: frame must use Draw Mode Sliced or Tiled; size changes are ignored otherwise.", this);

            _restFrameHeight = frame.size.y;
            _restFrameLocalY = frame.transform.localPosition.y;
            _framePivotY = frame.sprite.pivot.y / frame.sprite.rect.height;
            _hasRestLayout = true;
        }
```

**Khác với bản dự án:** không — file sau giai đoạn này khớp nguyên văn bản dự án trừ namespace và dòng `using` đã bỏ ở GĐ1.

**Quyết định** — Đáy đứng yên: khung cao thêm `ExtraHeight / scaleY` đơn vị sprite quanh pivot, tức `ExtraHeight` đơn vị cha;
phần nằm dưới pivot là `ExtraHeight × pivotY`, dời khung lên đúng bấy nhiêu thì đáy giữ nguyên với mọi pivot. Kiểm: đáy =
`y − size.y × scaleY × pivotY`; thay `y' = restY + E·p`, `size' = restH + E/scaleY` → `restY − restH·scaleY·p`, không còn `E`.
Phương án bỏ: bắt pivot sprite ở đáy — sprite khung của dự án pivot giữa, đổi pivot là đổi art. — Chia `ExtraHeight` cho
`localScale.y` của khung: `size` của Sliced tính theo đơn vị sprite, trước scale của transform. — `LogError` thay vì exception
cho hai lỗi setup: khác cha thì `ExtraHeight` đo ở không gian sai, khung lệch im lặng; Simple thì gán `size` bị bỏ qua, khung
không cao lên; cả hai không ném gì nên phải nói ra, nhưng không chặn phần còn lại chạy để developer thấy khung sai ngay trong
Editor. — Chụp nghỉ ở `SetText` đầu, không ở `Awake`: `Awake` không chạy trên object chưa bật hay prefab mở bằng
`LoadPrefabContents` (test dữ liệu thật); chụp một lần vì sau lần đổi `size` đầu khung không còn là khung nghỉ.

| Consumes | Produces |
|---|---|
| GĐ1; `SpriteRenderer.size`, `drawMode`, `sprite.pivot`, `sprite.rect`; `Transform.localPosition`, `localScale` | `frame` (field authoring); `SetText` đặt khung |

**Sơ đồ tương tác sau GĐ2**

```
  host ──SetText──► TextFitFrame ──Text = · MeasureText ×2──► UniTextBase
    ▲                    │ lần đầu: CaptureRestLayout+ (LogError khác cha, Simple)
    │ ExtraHeight        │ size.y+ = nghỉ + ExtraHeight / scaleY
    └────────────────────┤ localPosition.y+ = restY + ExtraHeight × pivotY
                         ▼
                 SpriteRenderer frame+ (Sliced)
```

**Editor setup:** không — fixture test dựng bằng code; bảng "Trước khi chạy" cho prefab thật ở GĐ3.

**Kiểm:** compile sạch. Fixture thêm khung: `SpriteRenderer` con của cha, sprite từ `Sprite.Create` trên `Texture2D` nhỏ với
`border` khác 0 và pivot tham số hoá (đáy, giữa), `drawMode = Sliced`, `size = (x, nghỉ)`; nối `frame` qua `SerializedObject`;
`TearDown` huỷ thêm texture và sprite. Đáy khung = `localPosition.y − size.y × localScale.y × pivotY`, chụp trước `SetText` đầu.

| Case | Input | Kỳ vọng |
|---|---|---|
| rỗng, một dòng | `SetText("")`, `SetText("Food")` | `size.y` = nghỉ; đáy = đáy nghỉ |
| hai dòng | `SetText("Food\nFood")` | `size.y` = nghỉ + bước dòng; đáy = đáy nghỉ |
| pivot giữa | sprite pivot y 0.5, hai dòng | `localPosition.y` = nghỉ + bước dòng / 2; đáy = đáy nghỉ |
| pivot đáy | sprite pivot y 0, hai dòng | `localPosition.y` = nghỉ; đáy = đáy nghỉ |
| khung scale khác 1 | `frame.localScale.y` = 2, hai dòng | `size.y` = nghỉ + bước dòng / 2; đáy = đáy nghỉ |
| ba dòng tràn | `SetText("Food\nFood\nFood")` | `size.y` ≤ nghỉ + (`rect.height` − một dòng cỡ chuẩn); đáy = đáy nghỉ |
| hai dòng rồi một dòng | | `size.y` = nghỉ; đáy = đáy nghỉ |
| nghỉ chụp một lần | `SetText` hai dòng hai lần liên tiếp | `size.y` không cộng dồn (vẫn nghỉ + bước dòng) |
| object tắt trước lần đầu | `SetActive(false)` cha rồi hai dòng | khung vẫn cao đúng, đáy đứng yên |
| khác cha | `text` dời sang cha khác | `LogAssert.Expect(LogType.Error, …)` khớp thông điệp; không exception |
| Draw Mode Simple | `frame.drawMode = Simple` | `LogAssert.Expect` thông điệp Draw Mode |

**Cheat của giai đoạn:** fixture chỉ dùng pivot đáy thì số hạng `ExtraHeight × pivotY` bằng 0, công thức dời khung sai vẫn xanh —
ca pivot giữa bắt buộc. Fixture scale 1 cả hai transform giấu hai phép nhân chia scale — hai ca scale khác 1 (chữ ở GĐ1, khung ở
đây) bắt buộc.

**Mốc commit:** `feat(text): TextFitFrame đặt khung cao theo chữ, đáy đứng yên`.

---

## Giai đoạn 3 — Tài liệu `docs/Runtime/TextFitFrame/TextFitFrame.md` và `TextFitFrame.html` (agent)

Mở đầu bằng `Code: Runtime/Utilities/UI/`. Mục: luồng `SetText` → đo → đặt khung (ASCII ở trên) · bảng API (`SetText`,
`ExtraHeight`, hai field) · "Đo được ở UniText" và "Design decisions" gom các **Quyết định** của GĐ1–2 · "Dùng trong dự án": host
gọi `SetText` rồi đọc `ExtraHeight × localScale.y` của chính nó để đặt vị trí, tên trung tính · **Trước khi chạy** (bảng dưới).
`.html` chọn từ `.md`: kịch bản (một dòng, hai dòng, tràn), cách dùng ("Trước khi chạy" nguyên vẹn, `SetText` rồi đọc
`ExtraHeight`), flow, lưu ý (khác cha, Draw Mode, Line Height Mode).

| # | Thao tác | Thiếu thì hỏng ở đâu |
|---|---|---|
| 1 | `text` và `frame` là **hai con cùng một cha**; `TextFitFrame` gắn trên cha đó hoặc nơi host với tới | `LogError` ở `SetText` đầu; `ExtraHeight` đo ở không gian sai, khung lệch |
| 2 | `frame` (`SpriteRenderer`): Draw Mode **Sliced** (hoặc Tiled), sprite có border | `LogError`; khung không đổi chiều cao |
| 3 | Chiều cao `Rect Transform` của `text` = **chiều cao đúng số dòng tối đa** ở cỡ chuẩn (vd 2 dòng) | chiều cao khung chữ chính là trần số dòng; sai thì autosize co chữ sớm hoặc muộn hơn dự kiến |
| 4 | `text`: Auto Size bật, **Max = cỡ chuẩn** (= Font Size), Min đủ nhỏ, Word Wrap bật | Max lớn hơn cỡ chuẩn thì chữ một dòng phình to; tắt Auto Size thì chữ dài tràn khung chữ; tắt Word Wrap thì không bao giờ xuống dòng |
| 5 | `text`: Vertical Alignment **Bottom** | dòng cuối không nằm sát đáy khung chữ nên khung không ôm chữ; padding tuỳ ý |
| 6 | Kéo `text` và `frame` vào hai ô của `TextFitFrame` | `NullReferenceException` ở `SetText` đầu |
| 7 | Không ai khác tween vị trí `frame` | `TextFitFrame` ghi `localPosition.y` của khung mỗi lần `SetText`, tween khác bị ghi đè |
| 8 | Line Height Mode (Project Settings của UniText) chốt trước khi dựng khung chữ ở bước 3 | đổi mode là đổi bước dòng: khung chữ hai dòng thành hơn hoặc kém hai dòng |

**Mốc commit:** `docs(text): TextFitFrame.md`.

---

## Giai đoạn 4 — Dự án dùng bản Horcrux, xoá bản tạm, chuyển test (agent)

**Mục tiêu:** `CategoryNameTag` và `CategoryNameTagPrefabTests` dùng `Horcrux.Runtime.Utilities.UI.TextFitFrame`; prefab
`CategoryBox` giữ nguyên component và hai ô đã nối nhờ giữ GUID; bản tạm và test dự án của nó xoá. `CategoryNameTag.cs` không đổi
dòng nào ngoài `using`.

Developer gõ file mới nên Unity gán GUID mới; prefab trỏ script bằng GUID cũ sẽ báo "Missing script". Bước này đặt GUID cũ cho
`.meta` của bản Horcrux và xoá bản tạm trong một lần.

| File | Việc |
|---|---|
| `Assets/Horcrux/Runtime/Utilities/UI/TextFitFrame.cs.meta` | chỉ dòng `guid:` ← GUID đọc từ `.meta` bản tạm |
| `Assets/_CategoryJam/Scripts/Entities/Box/TextFitFrame.cs` + `.meta` | xoá |
| `Assets/_CategoryJam/Tests/EditMode/TextFitFrameTests.cs` + `.meta` | xoá — bảng case đã nằm ở `Tests/EditMode/TextFitFrameTests.cs` của Horcrux từ GĐ1–2 |
| `Assets/_CategoryJam/Scripts/Entities/Box/CategoryNameTag.cs` | thêm `using Horcrux.Runtime.Utilities.UI;` |
| `Assets/_CategoryJam/Tests/EditMode/CategoryNameTagPrefabTests.cs` | thêm cùng `using` |
| `CategoryJam.Gameplay.asmdef`, `CategoryJam.EditModeTests.asmdef` | không đổi: đã tham chiếu `com.horcrux.runtime` |

**Thứ tự**

1. Đóng Unity Editor (sửa `.meta` khi Editor mở thì Unity ghi đè).
2. Đọc `guid:` trong `Assets/_CategoryJam/Scripts/Entities/Box/TextFitFrame.cs.meta` (hiện là `8809b76d80bd8f74f8c8ded05e441248`;
   đọc lại từ file, không chép từ Plan).
3. Ghi GUID đó vào `guid:` của `.cs.meta` bản Horcrux; xoá bản tạm `.cs` + `.meta` và `TextFitFrameTests.cs` + `.meta` của dự án.
4. Thêm `using` ở hai file dự án.
5. Mở Editor, đợi compile.

Phương án bỏ: chuyển file bằng Project window của Unity để giữ GUID rồi sửa tại chỗ — GUID giữ được nhưng giai đoạn Horcrux và
giai đoạn dự án dính vào một commit đôi, và developer không còn gì để gõ ở GĐ1–2.

**Editor setup:** không, khi GUID giữ được. Nếu không giữ được (Editor đã mở và ghi đè): mở `CategoryBox.prefab`, trên `name_tab`
thêm lại `TextFitFrame`, kéo `name_top_text` vào ô `text` và `name_tab_frame` vào ô `frame`, lưu prefab.

**Kiểm:** `grep -rn "TextFitFrame" Assets/_CategoryJam --include=*.cs` chỉ còn `CategoryNameTag.cs` và
`CategoryNameTagPrefabTests.cs`; `grep -rl 8809b76d Assets` chỉ còn `.meta` của Horcrux và `CategoryBox.prefab` · compile sạch,
Console không "Missing script" · `CategoryBox.prefab` › `name_tab` vẫn có `TextFitFrame` với hai ô đã nối · `CategoryNameTagPrefabTests`
xanh · `Horcrux.EditModeTests` › `TextFitFrameTests` xanh · **cần chơi thử** (developer): vào một level có box tên category hai dòng
và box tên một dòng; nhìn tab tên trượt ra khi tile đầu đáp và trượt vào khi nắp rơi: khung tên hai dòng cao đúng hai dòng, đáy
khung ở cùng chỗ với tab một dòng, lúc ẩn đỉnh tab hai dòng không ló trên thân box; dấu hiệu hỏng: khung không cao lên (Missing
script hay Draw Mode), chữ hai dòng tràn ra ngoài khung, tab hai dòng ló khi ẩn.

**Mốc commit:** repo dự án `refactor(text): TextFitFrame dùng bản Horcrux` kèm con trỏ submodule.

---

## File → giai đoạn

| File | GĐ1 | GĐ2 | GĐ3 | GĐ4 |
|---|---|---|---|---|
| `Runtime/Utilities/UI/TextFitFrame.cs` | tạo: `text`, `OneLine`, `ExtraHeight`, `SetText`, `MeasureExtraTextHeight` | `frame`, bốn field nghỉ, `CaptureRestLayout`, `SetText` đặt khung | | `.meta` nhận GUID cũ |
| `Tests/EditMode/TextFitFrameTests.cs` (agent) | tạo | thêm fixture khung, case | | |
| `docs/Runtime/TextFitFrame/TextFitFrame.md`, `.html` (agent) | | | tạo | |
| dự án: `CategoryNameTag.cs`, `CategoryNameTagPrefabTests.cs`; xoá `Entities/Box/TextFitFrame.cs`, `Tests/EditMode/TextFitFrameTests.cs` | | | | đổi |
