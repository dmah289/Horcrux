# Plan — `TextFitFrame` lên Horcrux

Ngày 2026-10-31. Nguồn: bản tạm trong dự án `Assets/_CategoryJam/Scripts/Entities/Box/TextFitFrame.cs`, đã có test EditMode
(`Assets/_CategoryJam/Tests/EditMode/TextFitFrameTests.cs`, 5 case) và test dữ liệu thật
(`CategoryNameTagPrefabTests.cs`, ở lại dự án).

**Phân công:**
- Task 1 (Horcrux, code): developer gõ.
- Task 2 (tài liệu module), Task 3 (dự án đổi sang bản Horcrux, xoá bản tạm), Task 4 (test): agent làm.

Horcrux là repo git riêng: Task 1, 2, 4 commit **trong `Assets/Horcrux`**, Task 3 commit ở repo dự án kèm con trỏ submodule.

---

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | `CategoryNameTag` của Category Jam, gắn trên object `name_tab` của `Assets/_CategoryJam/Addressables/Box/CategoryBox.prefab`; `TextFitFrame` cũng nằm trên `name_tab` · dự án sau có nhãn chữ nền khung Sliced mà số dòng đổi theo nội dung |
| Mục tiêu | khung `SpriteRenderer` Sliced cao theo số dòng chữ TMP, **đáy khung đứng yên**; số dòng tối đa là chiều cao khung chữ đã dựng trong prefab, tràn thì autosize của TMP co chữ |
| Chỗ đặt | `Assets/Horcrux/Runtime/Utilities/UI/TextFitFrame/TextFitFrame.cs` + `TextFitFrame.md` cạnh nó (utility lẻ có tài liệu thì xuống thư mục con mang tên nó, §5). Namespace `Horcrux.Runtime.Utilities.UI`: cả 7 file hiện có trong `Utilities/UI/` dùng đúng namespace này |
| Assembly | `com.horcrux.runtime` đã tham chiếu `Unity.TextMeshPro` và `UnityEngine.UI`; không phải sửa asmdef. `Splitter` ở `Horcrux.Runtime.Utilities`, cha của namespace đích, nên `using Horcrux.Runtime.Utilities;` trong code mẫu thừa nhưng vô hại; developer bỏ được |
| Tên | giữ `TextFitFrame` · giữ GUID cũ (xem Task 3) |

**Đo được ở TMP, quyết định dựa trên đó**

| Hành vi TMP | Hệ quả cho code |
|---|---|
| Autosize chỉ co chữ khi **tràn chiều cao**, không co khi một từ rộng hơn khung chữ | chữ dài quá số dòng tối đa được co bởi autosize, nên `TextFitFrame` không đo chiều rộng |
| Một từ đơn rộng hơn khung chữ bị bẻ giữa từ xuống dòng 2 | không tên category thật nào chạm trường hợp này (test tên thật của dự án chứng minh); không viết code xử lý |
| Layout nghỉ chụp ở **lần `SetText` đầu**, không ở `Awake` | object chưa từng được bật vẫn dùng được; `ForceMeshUpdate(true)` đo được cả khi object đang tắt |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Tự co chữ hay đo chiều rộng | autosize của TMP đã đủ cho mọi tên thật; viết thêm là code canh thứ chưa ai gặp |
| Tween chiều cao khung | caller (`CategoryNameTag`) đặt tab theo `ExtraHeight` rồi tự trượt; component này chỉ đặt kích thước |
| Tính lại khi đổi font hoặc cỡ chữ chuẩn lúc chạy | khung và cỡ chữ là giá trị authoring, cố định trong phiên |
| Guard `frame` hay `text` null | thiếu ô Inspector lộ ngay ở `SetText` đầu tiên bằng `NullReferenceException` trỏ đúng dòng |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| `LogError` khi text và frame khác cha, khi frame Draw Mode Simple | hai lỗi setup này không có exception: khác cha thì `ExtraHeight` đo ở không gian sai và khung lệch im lặng; Simple thì gán `size` bị bỏ qua nên khung không cao lên |
| Đáy khung đứng yên: `localPosition.y = restY + ExtraHeight * pivotY` | khung cao thêm `ExtraHeight / scaleY` quanh pivot; dời vị trí đúng phần pivot nằm trên đáy thì đáy giữ nguyên với mọi pivot của sprite |
| `ExtraHeight` đo từ đáy dòng đầu tới đáy dòng cuối (`descender`), nhân `localScale.y` của chữ | một dòng thì bằng 0 và khung đúng kích thước prefab; mỗi dòng thêm bằng đúng khoảng cách dòng của font ở cỡ chữ autosize đang dùng |
| Chia `ExtraHeight` cho `frame.transform.localScale.y` khi gán `size` | `size` của Sliced tính theo đơn vị sprite, trước scale của transform |
| `ExtraHeight` là property công khai | caller ngoài (tab trượt) cần phần khung cao thêm để tính điểm ẩn |

---

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| Bản tạm trong dự án | toàn bộ | — (namespace đổi, GUID giữ) |
| Horcrux `Utilities/` | chưa có helper fit chữ vào khung | — |
| Dự án: `_Kelsey/Modules/TextMeshProExtension` | — | chỉ uốn chữ theo đường cong, không đo số dòng hay đổi cỡ khung |
| TMP `Auto Size` | co chữ khi tràn chiều cao | không đổi kích thước khung quanh chữ; `TextFitFrame` dùng nó cho phần co chữ và thêm phần khung |
| Unity `ContentSizeFitter` + `Image` Sliced (UI) | — | dành cho `RectTransform` trên Canvas; bài toán là `SpriteRenderer` trong world, không có Canvas |

---

## Task 1 — Horcrux (developer gõ)

**Files**
- Create: `Assets/Horcrux/Runtime/Utilities/UI/TextFitFrame/TextFitFrame.cs`

**Interfaces**

| Chữ ký | Vai trò |
|---|---|
| `void SetText(string value)` | ghi chữ, đo, đặt `frame.size` và vị trí khung. Lần gọi đầu chụp layout nghỉ |
| `float ExtraHeight { get; }` | phần khung cao thêm ở lần fit gần nhất, theo không gian cha của khung |
| `[SerializeField] TMP_Text text` · `[SerializeField] SpriteRenderer frame` | hai tham chiếu authoring; tên field giữ nguyên vì prefab serialize theo tên |

**Code hoàn chỉnh** — bản tạm nguyên văn, chỉ đổi namespace:

```csharp
using Horcrux.Runtime.Utilities;
using TMPro;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.UI
{
    /// <summary>
    /// Khung Sliced cao theo số dòng của chữ, đáy khung đứng yên. Số dòng tối đa là chiều cao khung chữ trong prefab;
    /// tràn thì autosize của TMP co chữ.
    /// </summary>
    public class TextFitFrame : MonoBehaviour
    {
        #region Properties

        [Splitter("References")]
        [Tooltip("Text to wrap. Must share one parent with the frame.")]
        [SerializeField] private TMP_Text text;
        [Tooltip("Sliced frame. Moves up by part of the extra height, so nothing else may tween its position.")]
        [SerializeField] private SpriteRenderer frame;

        private float _restFrameHeight;
        private float _restFrameLocalY;
        private float _framePivotY;
        private bool _hasRestLayout;

        /// <summary>Phần khung cao thêm ở lần fit gần nhất, theo đơn vị không gian cha của khung.</summary>
        public float ExtraHeight { get; private set; }

        #endregion

        #region API

        public void SetText(string value)
        {
            if (!_hasRestLayout) CaptureRestLayout();

            text.text = value;
            // true: đo được cả khi object đang tắt.
            text.ForceMeshUpdate(true);

            ExtraHeight = MeasureExtraTextHeight() * text.transform.localScale.y;
            frame.size = new Vector2(frame.size.x, _restFrameHeight + ExtraHeight / frame.transform.localScale.y);

            var framePos = frame.transform.localPosition;
            framePos.y = _restFrameLocalY + ExtraHeight * _framePivotY;
            frame.transform.localPosition = framePos;
        }

        #endregion

        #region Class Methods

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

        /// <summary>Khoảng từ đáy dòng đầu tới đáy dòng cuối, theo không gian của chữ; một dòng thì bằng 0.</summary>
        private float MeasureExtraTextHeight()
        {
            var info = text.textInfo;
            if (info.lineCount <= 1) return 0f;
            return info.lineInfo[0].descender - info.lineInfo[info.lineCount - 1].descender;
        }

        #endregion
    }
}
```

Mốc compile sạch: dự án lúc này có hai `TextFitFrame` ở hai namespace khác nhau, không đụng nhau vì `CategoryNameTag` vẫn dùng bản dự án. Bản Horcrux chưa có người dùng cho tới Task 3.

---

## Task 2 — Tài liệu module (agent)

**Files**
- Create: `Assets/Horcrux/Runtime/Utilities/UI/TextFitFrame/TextFitFrame.md`

Nội dung: luồng `SetText` → đo → đặt khung (ASCII), hai bảng "Đo được ở TMP" và quyết định ở trên, và mục **Trước khi chạy** (Editor setup):

| # | Thao tác | Thiếu thì hỏng ở đâu |
|---|---|---|
| 1 | `text` và `frame` là **hai con cùng một cha** | `LogError` ở `SetText` đầu; `ExtraHeight` đo ở không gian sai, khung lệch |
| 2 | `frame` (SpriteRenderer): Draw Mode **Sliced** (hoặc Tiled), sprite có border | `LogError`; khung không đổi chiều cao |
| 3 | Chiều cao `Rect Transform` của `text` = **chiều cao đúng số dòng tối đa** (vd 2 dòng) | chiều cao khung chữ chính là trần số dòng; sai thì autosize co chữ sớm hoặc muộn hơn dự kiến |
| 4 | `text`: Auto Size bật, **Font Size Max = cỡ chuẩn**, Wrapping Normal | Max lớn hơn cỡ chuẩn thì chữ 1 dòng phình to; tắt Auto Size thì chữ dài tràn khung chữ |
| 5 | `text`: Vertical Alignment **Bottom**, Margin 0 | đáy dòng cuối lệch khỏi đáy khung chữ nên khung không ôm chữ |
| 6 | Kéo `text` và `frame` vào hai ô của `TextFitFrame` | `NullReferenceException` ở `SetText` đầu |
| 7 | Không ai khác tween vị trí `frame` | `TextFitFrame` ghi `localPosition.y` của frame mỗi lần `SetText`, tween khác bị ghi đè |

---

## Task 3 — Dự án chuyển sang bản Horcrux (agent)

Developer gõ file mới nên Unity gán GUID mới; prefab trỏ script bằng GUID cũ sẽ báo "Missing script". Để khỏi nối lại bằng tay, bước này đặt GUID cũ cho `.meta` của bản Horcrux và xoá bản tạm trong một lần.

**Files**
- Modify: `Assets/Horcrux/Runtime/Utilities/UI/TextFitFrame/TextFitFrame.cs.meta` (chỉ dòng `guid:`)
- Delete: `Assets/_CategoryJam/Scripts/Entities/Box/TextFitFrame.cs` và `TextFitFrame.cs.meta`
- Modify: `Assets/_CategoryJam/Scripts/Entities/Box/CategoryNameTag.cs` (thêm `using Horcrux.Runtime.Utilities.UI;`)
- Modify: `Assets/_CategoryJam/Tests/EditMode/CategoryNameTagPrefabTests.cs` (thêm cùng `using`)
- Delete: `Assets/_CategoryJam/Tests/EditMode/TextFitFrameTests.cs` (+ `.meta`), sau khi Task 4 xong

**Thứ tự**
1. Đóng Unity Editor (sửa `.meta` khi Editor mở thì Unity ghi đè).
2. Đọc `guid:` trong `Assets/_CategoryJam/Scripts/Entities/Box/TextFitFrame.cs.meta` (hiện là `8809b76d80bd8f74f8c8ded05e441248`; đọc lại từ file, không chép từ Plan).
3. Ghi guid đó vào `guid:` của `.cs.meta` bản Horcrux; xoá bản tạm `.cs` và `.meta`.
4. Thêm `using` ở `CategoryNameTag.cs` và `CategoryNameTagPrefabTests.cs`.
5. Mở Editor, đợi compile.

**Nghiệm thu**
- `grep -rn "TextFitFrame" Assets --include=*.cs`: chỉ còn `Assets/Horcrux/` và hai file dùng ở trên; không file `.cs` nào của dự án còn định nghĩa `TextFitFrame`, không còn tham chiếu `CategoryJam.Gameplay.Entities.TextFitFrame`.
- Compile sạch, console không có "Missing script".
- `CategoryBox.prefab`: `name_tab` vẫn có component `TextFitFrame` với hai ô `text`, `frame` đã nối; `CategoryNameTagPrefabTests` xanh.
- GUID cũ chỉ còn trong `.meta` của Horcrux và trong prefab (`grep -rl 8809b76d Assets`).

Nếu GUID không giữ được (Editor đã mở và ghi đè): mở `CategoryBox.prefab`, thêm lại `TextFitFrame` trên `name_tab`, kéo hai object con của `name_tab` vào ô `text` (object chữ) và `frame` (object khung Sliced), lưu prefab.

---

## Task 4 — Test chuyển sang `Horcrux.EditModeTests` (agent)

Agent viết sau khi developer gõ xong Task 1 và đọc code thật (chữ ký trong Plan có thể lệch). Plan không có code test.

**Files**
- Create: `Assets/Horcrux/Tests/EditMode/TextFitFrameTests.cs`

**Fixture không phụ thuộc asset dự án** (test cũ nạp font và sprite từ `Assets/_CategoryJam/...`, phải bỏ):
- Sprite khung: `Sprite.Create` từ `Texture2D` nhỏ, có `border` khác 0 để Sliced có hiệu lực.
- Font: font asset mặc định của TMP (`TMP_Settings.defaultFontAsset`).
- Dựng bằng code: cha, frame (Sliced), text (`TextMeshPro`, Wrapping Normal, Bottom, margin 0, Auto Size bật, Max = cỡ chuẩn, chiều cao khung chữ = hai dòng cộng một chút dư), gắn `TextFitFrame`, nối field bằng `SerializedObject`. `TearDown` huỷ object, texture và sprite.

**Bảng case** (chuyển từ 5 case của `TextFitFrameTests`; số cụ thể do fixture quyết)

| Case | Input | Kỳ vọng |
|---|---|---|
| `EmptyText_KeepsRestFrame` | `SetText("")` | `ExtraHeight` = 0 · `frame.size.y` = chiều cao nghỉ · đáy khung bằng đáy nghỉ |
| `OneLine_KeepsRestFrame` | `SetText("Food")` | như trên |
| `TwoLines_GrowsByOneLineStep_KeepsBottom` | `SetText("Food\nFood")` | `lineCount` = 2 · `ExtraHeight` = khoảng cách `descender` giữa hai dòng · `frame.size.y` = nghỉ + khoảng đó · đáy khung không đổi · cỡ chữ vẫn bằng cỡ chuẩn |
| `OverTwoLines_ShrinksFontToFitTwoLineBox` | `SetText("Food\nFood\nFood")` | cỡ chữ nhỏ hơn cỡ chuẩn · chiều cao `textBounds` không vượt chiều cao khung chữ |
| `TwoLinesThenOneLine_ReturnsToRest` | `SetText` hai dòng rồi một dòng | trở về đúng trạng thái nghỉ (kích thước và đáy) |

Bản cũ chưa có case cho các biên sau, agent thêm vào bảng này khi viết:

| Case thêm | Kỳ vọng |
|---|---|
| Sprite pivot ở giữa (không ở đáy) | đáy khung vẫn đứng yên khi hai dòng |
| `frame.transform.localScale.y` ≠ 1 | `size.y` = nghỉ + `ExtraHeight / scaleY`; đáy khung đứng yên |
| `SetActive(false)` trước lần `SetText` đầu | vẫn đo và đặt khung đúng |

`CategoryNameTagPrefabTests` (tên thật, prefab thật) **ở lại dự án**.

---

## Danh sách cheat

| Cheat | Cách lộ |
|---|---|
| Fixture dùng sprite không border nên Sliced không có hiệu lực mà test vẫn xanh | `LogError` Draw Mode làm test fail (NUnit fail log lỗi chưa khai báo); fixture phải có `border` |
| Hằng kỳ vọng chép từ kết quả code | kỳ vọng tính từ `textInfo.lineInfo` độc lập, không lấy từ `ExtraHeight` |
