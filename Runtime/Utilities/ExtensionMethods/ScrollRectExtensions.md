# ScrollRectExtensions — cuộn `ScrollRect` dọc tới một item

Đặt **một điểm trên item** trùng **một điểm trên viewport**, bằng cách gán `verticalNormalizedPosition`.
Không giả định pivot hay anchor của item lẫn content, không giả định list mọc từ đỉnh hay từ đáy. Hàm không
biết "item" mang nghĩa gì; nó chỉ làm hình học. Nơi dùng đầu tiên: `CollectionMainScreen.ScrollToStep`
(`Assets/LiveOps/Collection/Collection_Plan.md` §4.8), nơi step hiện tại phải nằm sát mép mà list mọc ra khỏi.

Code trích từ Plan §4.8; file `ScrollRectExtensions.cs` do developer tạo cạnh tài liệu này. Đổi code thì đổi
tài liệu cùng một lần.

---

## §1. Bề mặt API

| Hàm | Làm gì | Khi nào dùng |
|---|---|---|
| `SnapVerticalToBottom(this ScrollRect, RectTransform item)` | mép **dưới** item chạm mép **dưới** viewport | list có phần tử đầu ở **đáy**, mọc lên trên |
| `SnapVerticalToTop(this ScrollRect, RectTransform item)` | mép **trên** item chạm mép **trên** viewport | list có phần tử đầu ở **đỉnh**, mọc xuống dưới |
| `SnapVertical(this ScrollRect, RectTransform item, float itemPivot, float viewportPivot)` | điểm `itemPivot` trên item trùng điểm `viewportPivot` trên viewport; hai pivot cùng thang `0` = mép dưới, `1` = mép trên | mọi ca khác: căn giữa `(0.5, 0.5)`, "đầu item ở 1/3 màn" `(1, 0.66)`… |
| `GetVerticalRangeIn(RectTransform item, RectTransform space, out yMin, out yMax)` — *private* | mép dưới và mép trên của item, đo trong hệ toạ độ của `space` | hàm phụ của `SnapVertical`; có người dùng thứ hai thì nâng lên `RectTransformExtensions` |

Hai wrapper chỉ để chỗ gọi đọc ra ý định mà không phải nhớ hai con số. Ba hàm public đều **đặt ngay**, không
tween; tween là một hàm **tính** trả `float` thêm vào sau, không sửa hàm nào (xem §7).

---

## §2. Bài toán bằng lời

Hình dung một **cuộn giấy dài** (content) trượt sau **một khung cửa sổ** cố định (viewport). Trên giấy in
một dãy ô (item). Người chơi chỉ nhìn thấy phần giấy nằm sau khung. "Cuộn tới ô X" là kéo giấy sao cho ô X
đứng ở chỗ ta muốn trong khung.

Có hai câu hỏi độc lập:

1. **Chỗ nào trên ô** cần đứng đúng vị trí: mép dưới, mép trên, hay giữa ô. Đó là `itemPivot`.
2. **Chỗ nào trong khung** ô phải đứng: sát mép dưới khung, sát mép trên, hay giữa khung. Đó là `viewportPivot`.

Với Collection: các step còn lại phải hiện **tiếp theo hướng list mọc**, nên step hiện tại đứng sát mép mà
list mọc *ra khỏi*. List mọc từ đáy lên thì step hiện tại nằm sát **đáy** khung, các step sau xếp phía trên
(`SnapVerticalToBottom`). List mọc từ đỉnh xuống thì ảnh gương (`SnapVerticalToTop`).

Có một ràng buộc vật lý: giấy **không kéo quá hai đầu**. Muốn ô nằm ở chỗ mà giấy không với tới được thì
giấy dừng ở đầu gần nhất, ô hiện ở đâu thì ở đó. Ràng buộc này chính là hành vi "các step cuối thì hiện đầu
list" mà không cần một luật riêng.

---

## §3. Hệ quy chiếu

`ScrollRect` không nhận "kéo giấy bao nhiêu pixel"; nó nhận **một số trong `[0, 1]`**:

| Ký hiệu | Là gì | Lấy từ đâu |
|---|---|---|
| `H` | chiều cao content | `content.rect.height` |
| `V` | chiều cao viewport | `viewport.rect.height` |
| `scrollable` | quãng giấy có thể trượt | `H − V` |
| `n` | `verticalNormalizedPosition` | `1` = mép trên content trùng mép trên viewport (đang ở **đỉnh**) · `0` = mép dưới trùng mép dưới (đang ở **đáy**) |
| `offsetFromTop` | mép trên viewport đang nằm **dưới** mép trên content bao xa | đại lượng trung gian, dễ nghĩ hơn `n` |

Quan hệ giữa hai cách nói:

$$ offsetFromTop = (1 - n) \cdot scrollable \qquad\Longleftrightarrow\qquad n = 1 - \frac{offsetFromTop}{scrollable} $$

Kiểm hai mốc: `n = 1` → `offsetFromTop = 0`, viewport dán ở mép trên content ✓. `n = 0` → `offsetFromTop = scrollable = H − V`,
viewport dán ở mép dưới ✓.

Toàn bộ `SnapVertical` chỉ là: **tính `offsetFromTop` mong muốn, rồi đổi sang `n`**.

```
                 mép trên content  ─────────────────────────── 0
                                            │
                                            │ offsetFromTop            ▲
                                            ▼                          │
                 ┌── mép trên viewport ────────────────┐               │
                 │                                     │               │
                 │   · điểm viewportPivot              │ ← cách mép trên viewport (1 − viewportPivot)·V
                 │                                     │               │ distFromContentTop
                 └── mép dưới viewport ────────────────┘               │
                                                                       │
                 ┌── mép trên item ─────────┐                          │
                 │   · điểm itemPivot       │ ◄─────────────────────────┘
                 └── mép dưới item ─────────┘
                                            │
                 mép dưới content  ─────────────────────────── H
```

Mọi khoảng cách trong hình đo **từ mép trên content xuống**, và đo trong **không gian local của content**.
Lý do ở §4.

---

## §4. Vì sao đo trong không gian content

Content là thứ **di chuyển** khi cuộn; item nằm **trong** content nên vị trí của item *so với content* không
đổi dù đang cuộn tới đâu. Đo ở đó thì gọi lúc nào cũng ra cùng một `offsetFromTop`, không phụ thuộc trạng
thái hiện tại. Ba cách đo đã cân:

| Cách đo | Đúng khi | Sai khi | |
|---|---|---|---|
| `item.anchoredPosition.y` cộng trừ `pivot × height` | content pivot ở đỉnh, item anchor ở mép trên content | đổi pivot của content, đổi anchor của item, item có scale | ✗ |
| `item.position` (world) trừ `content.position` | không có scale nào trên đường Canvas → content | Canvas scale theo màn (`CanvasScaler`), content có scale | ✗ |
| Góc của `item.rect` → world → local của content bằng `TransformPoint` / `InverseTransformPoint` | mọi tổ hợp anchor, pivot, scale | item **xoay** so với content — layout group không bao giờ làm việc đó | ✓ |

`anchoredPosition` là khoảng cách từ **anchor** của item tới **pivot** của item. Nó đổi nghĩa mỗi khi prefab
đổi anchor hoặc pivot, và công thức cũ chỉ đúng với đúng một tổ hợp. Cách thứ ba tốn hai phép biến đổi ma
trận cho một lần cuộn (nhịp *mỗi tương tác*), đổi lấy việc không có điều kiện nào phải nhớ khi dựng prefab.

---

## §5. Từng hàm

### 5.1 `GetVerticalRangeIn` — mép dưới và mép trên của item, trong hệ của content

```csharp
private static void GetVerticalRangeIn(RectTransform self, RectTransform space, out float yMin, out float yMax)
{
    Rect rect = self.rect;
    float a = space.InverseTransformPoint(self.TransformPoint(new Vector3(rect.xMin, rect.yMin))).y;
    float b = space.InverseTransformPoint(self.TransformPoint(new Vector3(rect.xMin, rect.yMax))).y;

    yMin = Mathf.Min(a, b);
    yMax = Mathf.Max(a, b);
}
```

| Bước | Vì sao |
|---|---|
| `self.rect` là hình chữ nhật của item **trong hệ của chính nó**, gốc tại pivot | `rect.yMin` là mép dưới, `rect.yMax` là mép trên, tính tương đối với pivot — nên pivot của item **tự triệt tiêu** ở bước sau |
| `TransformPoint` đưa góc ra world; `InverseTransformPoint` đưa về local của `space` | hai phép này nuốt hết anchor, pivot, scale của cả item lẫn content, kể cả `CanvasScaler` |
| Hai góc **cùng `xMin`**, chỉ khác `yMin`/`yMax` | chỉ cần trục dọc; lấy hai góc đủ, không cần bốn |
| `Min`/`Max` ở cuối | không giả định góc nào cao hơn sau biến đổi (scale âm, content lật). Giả định còn lại: item không xoay so với content |

### 5.2 `SnapVertical` — hàm làm việc thật

```csharp
public static void SnapVertical(this ScrollRect self, RectTransform item, float itemPivot, float viewportPivot)
{
    RectTransform content = self.content;
    RectTransform viewport = self.viewport != null ? self.viewport : (RectTransform)self.transform;

    LayoutRebuilder.ForceRebuildLayoutImmediate(content);

    float scrollable = content.rect.height - viewport.rect.height;
    if (scrollable <= 0f)
    {
        self.verticalNormalizedPosition = 1f;
        return;
    }

    GetVerticalRangeIn(item, content, out float yMin, out float yMax);
    float itemPoint = Mathf.Lerp(yMin, yMax, itemPivot);
    float distFromContentTop = content.rect.yMax - itemPoint;
    float offsetFromTop = distFromContentTop - (1f - viewportPivot) * viewport.rect.height;

    self.verticalNormalizedPosition = 1f - Mathf.Clamp01(offsetFromTop / scrollable);
}
```

Suy ra từng bước, không nhảy:

| # | Dòng | Vì sao |
|---|---|---|
| ① | `viewport = self.viewport ?? self.transform` | ô Viewport của `ScrollRect` trong Inspector **được phép trống**; khi đó `ScrollRect` dùng chính `RectTransform` của nó làm khung. Hàm làm y như vậy |
| ② | `ForceRebuildLayoutImmediate(content)` | `VerticalLayoutGroup` và `ContentSizeFitter` chỉ xếp con ở **cuối frame**, trong lượt cập nhật Canvas. Vừa `SetActive` hay `SetSiblingIndex` mà đọc rect ngay là đọc số của frame trước. Dòng này ép xếp ngay. Đặt **trong** hàm để caller không phải nhớ một thủ tục trước khi gọi |
| ③ | `scrollable <= 0` → `n = 1`, thoát | content ngắn hơn viewport thì không có gì để cuộn. Thoát trước bước ⑦ để không chia cho 0: `NaN` gán vào `normalizedPosition` đẩy content bay khỏi màn |
| ④ | `itemPoint = Lerp(yMin, yMax, itemPivot)` | điểm cần căn trên item, toạ độ content. `0` → mép dưới, `1` → mép trên, `0.5` → giữa |
| ⑤ | `distFromContentTop = content.rect.yMax − itemPoint` | `content.rect.yMax` là **mép trên content trong local của chính nó** (pivot ở đỉnh thì bằng `0`, pivot giữa thì bằng `H/2`). Hiệu này là khoảng cách từ mép trên content xuống điểm cần căn, không âm với item nằm trong content |
| ⑥ | `offsetFromTop = distFromContentTop − (1 − viewportPivot)·V` | điểm `viewportPivot` nằm dưới mép trên viewport một đoạn `(1 − viewportPivot)·V`; mép trên viewport lại nằm dưới mép trên content một đoạn `offsetFromTop`. Vậy điểm đó cách mép trên content là `offsetFromTop + (1 − viewportPivot)·V`. Muốn nó trùng `itemPoint` thì đặt bằng `distFromContentTop`; chuyển vế là ra dòng này |
| ⑦ | `n = 1 − Clamp01(offsetFromTop / scrollable)` | đổi sang thang của `ScrollRect` theo §3. `Clamp01` là ràng buộc "giấy không kéo quá hai đầu" ở §2: `offsetFromTop < 0` là muốn kéo content **xuống dưới** mép trên, không được, dừng ở đỉnh; `> scrollable` là muốn kéo quá đáy, dừng ở đáy |

Viết gọn thành một công thức:

$$ n = 1 - \operatorname{clamp}_{[0,1]}\!\left(\frac{(y_{top}^{content} - \operatorname{lerp}(y_{min}, y_{max}, p_i)) - (1 - p_v)\,V}{H - V}\right) $$

| Ký hiệu | Nghĩa |
|---|---|
| $y_{top}^{content}$ | `content.rect.yMax` |
| $y_{min}, y_{max}$ | mép dưới, mép trên của item trong local của content (§5.1) |
| $p_i, p_v$ | `itemPivot`, `viewportPivot` |
| $V, H$ | chiều cao viewport, content |

### 5.3 Hai wrapper

```csharp
public static void SnapVerticalToBottom(this ScrollRect self, RectTransform item) => self.SnapVertical(item, 0f, 0f);
public static void SnapVerticalToTop(this ScrollRect self, RectTransform item)    => self.SnapVertical(item, 1f, 1f);
```

`(0, 0)`: mép dưới item chạm mép dưới khung. `(1, 1)`: mép trên item chạm mép trên khung. Tên nói hướng list
thay cho hai con số.

---

## §6. Kiểm bằng tay

Dữ liệu: viewport `V = 500`, 20 item cao 100, content `H = 2000`, content pivot ở đỉnh, **step 1 ở đáy**.
Item thứ `k` tính từ trên (bắt đầu 0) chiếm `[100k, 100k + 100]` đo từ mép trên content và là **step `20 − k`**.
`scrollable = 1500`.

| Gọi | `distFromContentTop` | `offsetFromTop` | `n` | Thấy gì trong khung |
|---|---|---|---|---|
| `SnapVerticalToBottom(step 7)` — `k = 13`, mép dưới 1400 | 1400 | 1400 − 500 = **900** | 1 − 900/1500 = **0.4** | phủ 900..1400: step 11 · 10 · 9 · 8 · **7 ở đáy** |
| `SnapVerticalToBottom(step 16)` — `k = 4`, mép dưới 500 | 500 | 0 | 1 | đỉnh list: step 20..16, **16 ở đáy** — biên đúng của vùng clamp |
| `SnapVerticalToBottom(step 19)` — `k = 1`, mép dưới 200 | 200 | −300 → **clamp 0** | 1 | đỉnh list: step 20..16 |
| `SnapVerticalToBottom(step 20)` — item cuối | 100 | −400 → clamp 0 | 1 | đỉnh list — đây là `ScrollToLastStep` của Collection |
| `SnapVertical(step 7, 0.5, 0.5)` | 1350 | 1350 − 250 = 1100 | 0.267 | step 7 giữa khung |
| `SnapVerticalToTop(step 1)` — `k = 19`, mép trên 1900 | 1900 | 1900 − 0 = 1900 → **clamp 1500** | 0 | đáy list: step 5..1 |

Hàng 2–4 là lý do Collection bỏ được luật riêng "3 step cuối thì lên đỉnh": mọi step có mép dưới nằm trong
`V` đơn vị đầu của content đều bị clamp về đỉnh. Với khung 5 item đó là step 16 tới 20. Hai cách chỉ khác nhau
khi khung hiện **ít hơn 3 item**.

Mốc biên của chính hàm:

| Mốc | Kỳ vọng | Kết quả | |
|---|---|---|---|
| `H ≤ V` | không cuộn, không `NaN` | thoát ở ③ với `n = 1` | ✓ |
| `viewport` trống trong Inspector | vẫn chạy | ① dùng `self.transform` | ✓ |
| item ở đỉnh content, `SnapVerticalToBottom` | dừng ở đỉnh | `offsetFromTop < 0` → clamp 0 → `n = 1` | ✓ |
| item ở đáy content, `SnapVerticalToTop` | dừng ở đáy | `offsetFromTop > scrollable` → clamp 1 → `n = 0` | ✓ |
| content pivot đổi từ `(0.5, 1)` sang `(0.5, 0.5)` | cùng kết quả | `content.rect.yMax` từ `0` thành `H/2`, `yMin/yMax` của item cùng dịch `H/2` → hiệu ở ⑤ không đổi | ✓ |
| item pivot đổi từ `1` sang `0.5` | cùng kết quả | `rect.yMin/yMax` của item dịch theo pivot, `TransformPoint` bù lại → cùng world → cùng local | ✓ |
| gọi khi đang cuộn ở giữa | cùng kết quả như gọi ở đỉnh | mọi số đo trong local của content, không đọc `anchoredPosition` của content | ✓ |

Đối chiếu với công thức cũ của Plan §4.8 (`itemBottom = −anchoredPosition.y + pivot.y × height`): content pivot ở
đỉnh nên `content.rect.yMax = 0`; item pivot 1 với `anchoredPosition.y = −1300` cho `yMax = −1300`, `yMin = −1400`;
`itemPivot = 0` lấy `−1400`; `distFromContentTop = 0 − (−1400) = 1400`, bằng `1300 + 100` của bản cũ. Cùng số,
bớt một điều kiện.

---

## §7. Bẫy, giới hạn, đường mở

| | Nội dung |
|---|---|
| **Item xoay** | `GetVerticalRangeIn` chỉ lấy hai góc; item xoay so với content thì `yMin/yMax` không còn là bao ngoài. Layout group không xoay con, nên chưa phải ca thật |
| **Rebuild mỗi lần gọi** | `ForceRebuildLayoutImmediate` chạy toàn bộ layout của content. Ở nhịp *mỗi tương tác* (một cú mở màn, một cú tap) là rẻ; đặt vào `Update` thì không |
| **Chỉ trục dọc** | `ScrollRect` ngang chưa có người dùng. Thêm thì là `SnapHorizontal` với `xMin/xMax` và `horizontalNormalizedPosition`, không sửa hàm dọc |
| **Tween tới vị trí** | tách bước ④–⑦ thành hàm `float GetVerticalNormalizedPositionFor(item, itemPivot, viewportPivot)` trả `n`, caller tween `verticalNormalizedPosition` từ hiện tại tới `n`. `SnapVertical` gọi hàm đó rồi gán. Là **thêm**, không phải sửa |
| **`n = 1` khi không cuộn được** | là lựa chọn, không phải kết quả suy ra: content ngắn hơn khung thì `n` không có nghĩa; chọn `1` để nội dung dính mép trên như mọi list ngắn trong uGUI |
