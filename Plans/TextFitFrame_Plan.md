# Plan — `TextFitFrame`: khung Sliced cao theo số dòng chữ, đáy khung đứng yên

Bản tạm: category-jam `Scripts/Entities/Box/TextFitFrame.cs`, test `Tests/EditMode/TextFitFrameTests.cs`; host `CategoryNameTag` trên `name_tab` của `Addressables/Box/CategoryBox.prefab`.
**Phụ thuộc:** không — gõ lúc nào cũng được.
Kiểm: `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,Horcrux.EditModeTests.csproj --tests Horcrux.EditModeTests`; chuyển dự án `--csproj CategoryJam.Gameplay.csproj,CategoryJam.EditModeTests.csproj,com.horcrux.runtime.csproj --tests CategoryJam.EditModeTests,Horcrux.EditModeTests`.

## Kịch bản

| Tình huống | Hệ làm gì | Host / người chơi thấy |
|---|---|---|
| tên một dòng | đo, `ExtraHeight` 0 | khung cỡ prefab |
| tên hai dòng (ngắt cứng hay wrap) | `ExtraHeight` = bước dòng; khung cao thêm, dời lên phần nằm dưới pivot | khung ôm hai dòng, đáy ở chỗ prefab đặt; host trượt theo `ExtraHeight` |
| tên quá trần dòng (cao hơn khung chữ) | autosize UniText co chữ cả hai chiều tới `MinFontSize` | chữ nhỏ lại trong khung chữ; khung cao tối đa `rect.height` − một dòng |
| chữ rỗng | hiệu âm kẹp 0 | khung nghỉ |
| hai dòng rồi một dòng | đo lại từ khung nghỉ đã chụp | khung về nghỉ |
| object đang tắt lúc `SetText` đầu | `MeasureText` chạy layout ngay lúc gọi | đo và đặt khung đúng |
| chữ hay khung có scale khác 1 | nhân scale chữ, chia scale khung | `ExtraHeight` theo không gian cha; đáy đứng yên |
| `text` và `frame` khác cha | `LogError` ở `SetText` đầu, vẫn chạy | Console đỏ; khung lệch |
| `frame` Draw Mode Simple | `LogError` ở `SetText` đầu | khung không cao lên |
| `text` hay `frame` trống | `NullReferenceException` ở `SetText` đầu | lỗi setup lộ ngay |

## Flow

```
                 SetText(value)                              Text = value · MeasureText(khung) · MeasureText("A")
  host ─────────────────────────────► TextFitFrame ─────────────────────────────────────────────► UniTextBase text
    ▲                                   │  Runtime/Utilities/UI/TextFitFrame.cs                    (autosize co trong khung chữ)
    │ ExtraHeight                       │ lần đầu: CaptureRestLayout (LogError khác cha, Simple)
    └───────────────────────────────────┤ size.y = nghỉ + ExtraHeight / scaleY
                                        │ localPosition.y = restY + ExtraHeight × pivotY
                                        ▼
                                SpriteRenderer frame (Sliced)
```

## Thuật toán

| Đại lượng | Công thức |
|---|---|
| `E` (`ExtraHeight`, không gian cha) | `max(0, MeasureText(khung).y − MeasureText("A").y) × text.localScale.y` |
| `size.y` khung | `restH + E / frame.localScale.y` |
| `localPosition.y` khung | `restY + E · p`, `p = sprite.pivot.y / sprite.rect.height` |
| bất biến đáy | `y − size.y · scaleY · p` = `restY − restH · scaleY · p`: không còn `E`, đúng với mọi pivot |

| Sự thật UniText (`MeasureText`, `UniTextBase_Layout.cs`) | Hệ quả |
|---|---|
| autosize co **cả hai chiều** khi tràn `maxWidth × maxHeight`, tới `MinFontSize` | trần số dòng là chiều cao khung chữ |
| thiếu `maxWidth` đo một dòng không wrap; thiếu `maxHeight` bỏ qua autosize | truyền cả hai từ `rect` khung chữ |
| chạy layout ngay lúc gọi, không cần object bật | đo được cùng frame đặt chữ |
| trả kích thước **outer** (gồm `Padding`) | hiệu hai phép đo triệt tiêu padding |
| chữ rỗng thấp hơn một dòng | hiệu âm, kẹp 0 |
| bước dòng theo Line Height Mode của **cả dự án** (`UniTextSettings`) | đổi mode là đổi bước dòng của mọi khung chữ |

---

## GĐ1 — xong thì host đặt được vị trí theo số dòng dù chưa có khung

```
  host ──SetText+──► TextFitFrame+ ──Text = · MeasureText(khung) · MeasureText("A")──► UniTextBase
    ▲                    │
    └──── ExtraHeight+ ──┘
```

`Runtime/Utilities/UI/TextFitFrame.cs` — file mới:

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

            // MeasureText trả theo không gian của chữ; host đọc theo không gian cha.
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
            // Mốc không truyền khung: một dòng ở cỡ chuẩn; truyền khung thì mốc trôi theo cỡ đang co.
            var oneLineHeight = text.MeasureText(OneLine).y;
            // Hiệu hai phép đo, không đếm dòng: chữ đã co thì khung cao đúng phần vượt thật.
            return Mathf.Max(0f, textHeight - oneLineHeight);
        }

        #endregion
    }
}
```

`Tests/EditMode/TextFitFrameTests.cs` (agent), không asset dự án:

| Fixture | Dựng |
|---|---|
| chữ | `UniTextWorld` trên `RectTransform` con của cha; `Font` trống (UniText lấy font hệ thống), Word Wrap, Center, Bottom, Auto Size, Min nhỏ, Max = cỡ chuẩn; cao = `MeasureText{text = "A\nA", maxWidth}` lúc Auto Size tắt + chút dư |
| nối | `TextFitFrame` trên cha, `text` qua `SerializedObject`; `TearDown` `DestroyImmediate` cha |
| bước dòng kỳ vọng | từ metric font chính (`text.FontProvider.PrimaryFont` sau lần đo đầu), không từ `ExtraHeight`: `FaceInfo.lineHeight / UnitsPerEm × cỡ chuẩn` khi `DefaultLineHeightMode` `Primary` · `LineHeightScale × cỡ chuẩn` khi `Scaled` |

| Case | Input | Kỳ vọng |
|---|---|---|
| rỗng | `SetText("")` | `ExtraHeight` 0 |
| một dòng | `SetText("Food")` | `ExtraHeight` 0; chữ ở cỡ chuẩn |
| hai dòng ngắt cứng | `SetText("Food\nFood")` | `ExtraHeight` = bước dòng (> 0 — đỏ khi fixture không layout được); cỡ chữ vẫn cỡ chuẩn (`MeasureText{autoSize = false}.y` bằng `MeasureText{khung}.y`) |
| hai dòng do wrap | chữ không có `\n`, rộng hơn `rect.width` | `ExtraHeight` = bước dòng — đỏ nếu không truyền `maxWidth` |
| ba dòng tràn | `SetText("Food\nFood\nFood")` | đối chứng `MeasureText{autoSize = false}.y` > `rect.height`; `MeasureText{khung}.y` ≤ `rect.height`; `ExtraHeight` ≤ `rect.height` − một dòng cỡ chuẩn |
| hai dòng rồi một dòng | `SetText` hai dòng rồi `"Food"` | `ExtraHeight` về 0 |
| object tắt trước lần đầu | `SetActive(false)` cha rồi `SetText` hai dòng | `ExtraHeight` = bước dòng |
| chữ scale khác 1 | `text.localScale.y` = 2, hai dòng | `ExtraHeight` = 2 × bước dòng — đỏ nếu bỏ nhân scale |
| gọi lặp | `SetText` hai dòng hai lần | `ExtraHeight` không đổi giữa hai lần |

---

## GĐ2 — xong thì tab tên hai dòng có khung ôm đủ hai dòng, đáy khung ở chỗ prefab đặt

```
  host ──SetText──► TextFitFrame ──Text = · MeasureText ×2──► UniTextBase
    ▲                    │ lần đầu: CaptureRestLayout+ (LogError khác cha, Simple)
    │ ExtraHeight        │ size.y+ = nghỉ + ExtraHeight / scaleY
    └────────────────────┤ localPosition.y+ = restY + ExtraHeight × pivotY
                         ▼
                 SpriteRenderer frame+ (Sliced)
```

`TextFitFrame.cs` › `Properties` › thêm sau `text`, trước `OneLine`:

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

`TextFitFrame.cs` › `API` › `SetText` — cũ:

```csharp
        public void SetText(string value)
        {
            text.Text = value;

            // MeasureText trả theo không gian của chữ; host đọc theo không gian cha.
            ExtraHeight = MeasureExtraTextHeight() * text.transform.localScale.y;
        }
```

Mới:

```csharp
        public void SetText(string value)
        {
            // Chụp ở SetText đầu, không Awake: Awake không chạy trên object tắt hay prefab LoadPrefabContents.
            if (!_hasRestLayout) CaptureRestLayout();

            text.Text = value;

            // MeasureText trả theo không gian của chữ; host đọc theo không gian cha.
            ExtraHeight = MeasureExtraTextHeight() * text.transform.localScale.y;
            // size của Sliced tính theo đơn vị sprite, trước scale của transform.
            frame.size = new Vector2(frame.size.x, _restFrameHeight + ExtraHeight / frame.transform.localScale.y);

            var framePos = frame.transform.localPosition;
            // Dời lên đúng phần cao thêm nằm dưới pivot: đáy đứng yên với mọi pivot.
            framePos.y = _restFrameLocalY + ExtraHeight * _framePivotY;
            frame.transform.localPosition = framePos;
        }
```

`TextFitFrame.cs` › `Class Methods` › thêm trước `MeasureExtraTextHeight`:

```csharp
        private void CaptureRestLayout()
        {
            // LogError, không ném: hai lỗi này hỏng im lặng; vẫn chạy để thấy khung sai ngay trong Editor.
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

Editor setup — host mới (prefab category-jam đã nối sẵn):

| # | Thao tác | Thiếu thì hỏng ở đâu |
|---|---|---|
| 1 | `text` và `frame` là **hai con cùng một cha**; `TextFitFrame` gắn trên cha đó hoặc nơi host với tới | `LogError` ở `SetText` đầu; `ExtraHeight` đo ở không gian sai, khung lệch |
| 2 | `frame` (`SpriteRenderer`): Draw Mode **Sliced** (hoặc Tiled), sprite có border | `LogError`; khung không đổi chiều cao |
| 3 | Chiều cao `Rect Transform` của `text` = **chiều cao đúng số dòng tối đa** ở cỡ chuẩn (vd 2 dòng); bề ngang = **lòng khung trừ viền**, đo trên ảnh art của `frame` bằng `Horcrux.Editor.UIReskin.DemoCompare.InnerRegion(đường dẫn ảnh)` (trả vùng lòng theo pixel; chia PPU của sprite ra unit) | chiều cao khung chữ chính là trần số dòng; sai thì autosize co chữ sớm hoặc muộn hơn dự kiến · bề ngang bằng cả khung thì tên dài xuống dòng muộn, co muộn, chữ đè lên viền |
| 4 | `text`: Auto Size bật, **Max = cỡ chuẩn** (= Font Size), Min đủ nhỏ, Word Wrap bật | Max lớn hơn cỡ chuẩn thì chữ một dòng phình to; tắt Auto Size thì chữ dài tràn khung chữ; tắt Word Wrap thì không bao giờ xuống dòng |
| 5 | `text`: Vertical Alignment **Bottom** | dòng cuối không nằm sát đáy khung chữ nên khung không ôm chữ; padding tuỳ ý |
| 6 | Kéo `text` và `frame` vào hai ô của `TextFitFrame` | `NullReferenceException` ở `SetText` đầu |
| 7 | Không ai khác tween vị trí `frame` | `TextFitFrame` ghi `localPosition.y` của khung mỗi lần `SetText`, tween khác bị ghi đè |
| 8 | Line Height Mode (Project Settings của UniText) chốt trước khi dựng khung chữ ở bước 3 | đổi mode là đổi bước dòng: khung chữ hai dòng thành hơn hoặc kém hai dòng |

`Tests/EditMode/TextFitFrameTests.cs` (agent) — fixture thêm khung `SpriteRenderer` con của cha: sprite `Sprite.Create` trên `Texture2D` nhỏ, `border` ≠ 0, pivot tham số (đáy, giữa), `drawMode = Sliced`, `size = (x, nghỉ)`, `frame` qua `SerializedObject`; `TearDown` huỷ texture, sprite. Đáy = `localPosition.y − size.y × localScale.y × pivotY`, chụp trước `SetText` đầu.

| Case | Input | Kỳ vọng |
|---|---|---|
| rỗng, một dòng | `SetText("")`, `SetText("Food")` | `size.y` = nghỉ; đáy = đáy nghỉ |
| hai dòng | `SetText("Food\nFood")` | `size.y` = nghỉ + bước dòng; đáy = đáy nghỉ |
| pivot giữa | sprite pivot y 0.5, hai dòng | `localPosition.y` = nghỉ + bước dòng / 2; đáy = đáy nghỉ — ca bắt buộc: pivot đáy làm số hạng `ExtraHeight × pivotY` bằng 0 |
| pivot đáy | sprite pivot y 0, hai dòng | `localPosition.y` = nghỉ; đáy = đáy nghỉ |
| khung scale khác 1 | `frame.localScale.y` = 2, hai dòng | `size.y` = nghỉ + bước dòng / 2; đáy = đáy nghỉ — đỏ nếu bỏ chia scale |
| ba dòng tràn | `SetText("Food\nFood\nFood")` | `size.y` ≤ nghỉ + (`rect.height` − một dòng cỡ chuẩn); đáy = đáy nghỉ |
| hai dòng rồi một dòng | | `size.y` = nghỉ; đáy = đáy nghỉ |
| nghỉ chụp một lần | `SetText` hai dòng hai lần liên tiếp | `size.y` không cộng dồn (vẫn nghỉ + bước dòng) |
| object tắt trước lần đầu | `SetActive(false)` cha rồi hai dòng | khung vẫn cao đúng, đáy đứng yên |
| khác cha | `text` dời sang cha khác | `LogAssert.Expect(LogType.Error, …)` khớp thông điệp; không exception |
| Draw Mode Simple | `frame.drawMode = Simple` | `LogAssert.Expect` thông điệp Draw Mode |

---

## Chuyển dự án (agent)

| Chỗ | Đổi |
|---|---|
| `docs/Runtime/TextFitFrame/TextFitFrame.md`, `.html` | tạo, `Code: Runtime/Utilities/UI/`: Flow · Thuật toán · bảng API (`SetText`, `ExtraHeight`, hai field) · Không làm · host gọi `SetText` rồi đọc `ExtraHeight × localScale.y` của chính nó để đặt vị trí · "Trước khi chạy" = Editor setup GĐ2 · lưu ý khác cha, Draw Mode, Line Height Mode |
| `Runtime/Utilities/UI/TextFitFrame.cs.meta` | Editor đóng; `guid:` ← GUID đọc từ `.meta` bản tạm (hiện `8809b76d80bd8f74f8c8ded05e441248`, đọc lại từ file), cùng lần xoá bản tạm để `CategoryBox.prefab` giữ component và hai ô đã nối |
| `Entities/Box/TextFitFrame.cs`, `Tests/EditMode/TextFitFrameTests.cs` của dự án (+ `.meta`) | xoá — bảng case đã ở `TextFitFrameTests.cs` của Horcrux |
| `CategoryNameTag.cs`, `Tests/EditMode/CategoryNameTagPrefabTests.cs` | thêm `using Horcrux.Runtime.Utilities.UI;`; không đổi dòng nào khác |
| GUID không giữ được (Editor ghi đè) | `CategoryBox.prefab` › `name_tab`: thêm lại `TextFitFrame`, kéo `name_top_text` vào `text`, `name_tab_frame` vào `frame`, lưu prefab |
| grep | `TextFitFrame` trong `.cs` của `Assets/_CategoryJam` chỉ còn hai file trên; `8809b76d` chỉ ở `.meta` Horcrux và `CategoryBox.prefab`; Console không "Missing script"; `CategoryNameTagPrefabTests` xanh |
| Plan này | xoá cùng `.meta` |
| chơi thử | level có box tên category hai dòng và box tên một dòng → tab tên trượt ra khi tile đầu đáp, trượt vào khi nắp rơi: khung hai dòng cao đúng hai dòng, đáy cùng chỗ tab một dòng, lúc ẩn đỉnh tab hai dòng không ló trên thân box; hỏng: khung không cao lên (Missing script, Draw Mode) · chữ hai dòng tràn khung · tab hai dòng ló khi ẩn |

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | đếm dòng qua `GetRangeBounds` | ngoài vòng render mọi dòng cùng `yMin` |
| bỏ | bắt pivot sprite ở đáy | sprite khung của dự án pivot giữa; đổi pivot là đổi art |
| bỏ | tự co chữ hay đo bề ngang | autosize của UniText đủ cho mọi tên thật |
| bỏ | tween chiều cao khung | host tự trượt theo `ExtraHeight` |
| bỏ | tính lại khi đổi font, cỡ chữ lúc chạy | giá trị authoring |
| bỏ | guard `text`, `frame` null | `NullReferenceException` ở `SetText` đầu đã trỏ đúng dòng |
| bỏ | `ContentSizeFitter` + `Image` Sliced | chỉ cho Canvas, khung ở đây là sprite world |
| bỏ | chuyển file bằng Project window để giữ GUID | Horcrux và dự án dính một commit đôi; developer không còn gì để gõ |
