# CanvasSpotlight — tối màn, nâng một canvas lên trên, tay chỉ

Một canvas phủ tối toàn màn ở sorting cao (`highlightSortingOrder`, mặc định 3103). **Target là một `Canvas` lồng**:
`Focus` bật raycaster của nó và nâng `sortingOrder` lên trên lớp tối, nên target vừa sáng vừa nhận tap, mọi thứ khác bị lớp tối nuốt.
Tay chỉ nhấp về phía target bằng `CharmPointAndBob`. `Release` trả target về ba giá trị đã nhớ (`enabled` của
GraphicRaycaster, `overrideSorting`, `sortingOrder`) và tắt cả object. `Focus` nhận được **hai** canvas cùng lúc (xem §2b).

Hệ độc lập với dự án (chỉ Unity UI, UniTask, InitArgs). Service **không biết click**: tap đi thẳng vào nút thật dưới
target, caller tự chờ tap rồi `Release`. Consumer: `CollectionHomeFlow.RunTutorialAsync` (một canvas, có tay) và `PlayReceiveCeremonyAsync` (hai canvas, không tay, tối dần).

Code: `ICanvasSpotlight.cs` (cạnh file này) · `Implementations/Foundations/CanvasSpotlight.cs` ·
`Utilities/Common/Direction.cs` · `TransformExtensions.CharmTween.cs` (`CharmPointAndBob`).

---

## §1. `HighlightConfig`

| Field | Nghĩa |
|---|---|
| `HandDirection` | hướng **ngón trỏ**, không phải chỗ tay đứng: `TopCenter` = trỏ lên, tay đứng dưới target; `TopLeft` = trỏ chéo lên trái, tay đứng góc dưới phải |
| `TargetPadding` | đầu ngón dừng cách pivot của target bấy nhiêu |
| `HandTargetOffset` | tay lùi ra thêm bấy nhiêu rồi trở lại — biên độ nhấp |
| `ShowHand` | `false` (`HighlightConfig.NoHand`): chỉ tối màn và nâng target |

Đơn vị là world position; cả hai canvas đều Overlay nên world = pixel màn hình, `CanvasScaler` của bên nào cũng chỉ đổi
cỡ sprite tay. Tay neo vào `transform.position` của target **lúc `Focus`**, không bám target đang chuyển động.

Góc xoay của tay là `Atan2` thuần của vector hướng, nên sprite trong `hand` phải **vẽ trỏ sang phải** (+x) ở góc 0, hoặc
con `hand` xoay bù sẵn trong prefab.

---

## §2. Luật của caller

```csharp
private static readonly HighlightConfig WidgetHandConfig = new(Direction.TopCenter, 12f, 80f);

try
{
    highlight.Focus(widget.HighlightCanvas, WidgetHandConfig);
    await widget.WidgetClickBtn.OnClickAsync(ct);
    highlight.Focus(mainScreen.InfoBtnCanvas, InfoHandConfig);   // Focus lần hai tự Release lần một
    await mainScreen.InfoBtn.OnClickAsync(ct);
}
finally
{
    highlight.Release();   // idle là no-op, nên finally không cần biết đã Focus chưa
}
```

| Luật | Vì |
|---|---|
| `Focus` **rồi mới** chờ tap | listener của chính nút đăng ký từ `Awake` nên chạy trước listener của `OnClickAsync`; flow chỉ chờ, không tự mở gì |
| `Release()` trong `finally` | huỷ giữa tutorial mà thiếu nó thì target kẹt sorting cao trên mọi popup và lớp tối phủ cả game, nuốt mọi tap |
| Config là `static readonly` của caller | mỗi target một cỡ, số của từng lần gọi; `readonly struct` truyền `in` nên `Focus` không cấp phát |
| Consumer nhận service qua Init (InitArgs), không qua `.Service` tĩnh | phụ thuộc nằm ở chữ ký |

---

## §2b. Hai canvas và tối dần

```csharp
highlight.Focus(widget.HighlightCanvas, stage.SelfCanvas, HighlightConfig.NoHand);   // cả hai sáng
await highlight.FadeDimAsync(0f, 0.5f, ct);                                           // tối tắt dần, vẫn nuốt tap
// ... diễn tiếp trên stage ...
highlight.Release();                                                                  // trả cả hai
```

| Luật | Vì |
|---|---|
| `Focus(target, extra, config)`: tay neo vào **target đầu**; `extra` chỉ được nâng, không có tay riêng | một tay, một đích; nội dung lễ (`extra`) là canvas con của target nên sáng cùng |
| `FadeDimAsync(toAlpha, duration, ct)` đổi alpha lớp tối, **tap vẫn bị nuốt tới `Release`** | lễ cần màn tối mờ dần mà người chơi chưa chạm được UI dưới; `Release` mới trả tương tác |
| `FadeDimAsync` chạy bằng thời gian unscaled, huỷ thì đặt alpha đích ngay | nhất quán với `CharmTween`; không để lớp tối kẹt nửa chừng |
| `Focus` lần sau luôn đặt lại alpha về `dimAlpha` | fade về 0 ở lượt trước không làm lượt sau không tối |
| `Release` trả **mọi** canvas đã nhớ (tối đa hai) | caller chỉ gọi một `Release` trong `finally` |

Nội dung lễ nằm trong canvas của consumer (đưa vào qua tham số `extra`); service **không** mọc cờ chế độ theo kịch bản. Kịch bản mới là consumer mới, không sửa service.

---

## §3. Trước khi chạy

### 3.1. Service (đã dựng trong `collection_setup.prefab`, instance trong `Services.unity`)

| # | Bước | Thiếu thì hỏng ở đâu |
|---|---|---|
| 1 | Root `canvas_spotlight`: `Canvas` **Screen Space - Overlay** · `GraphicRaycaster` · `CanvasSpotlight`. Root **active** trong prefab; `Awake` tự tắt | root tắt sẵn thì `Awake` chạy lần đầu ngay trong `Focus` và tắt lại object: không tối, không tay, không log |
| 2 | Con `dim`: `Image` stretch toàn màn, đen alpha ~0.78, **Raycast Target bật** | tắt thì tap ngoài target lọt xuống UI dưới |
| 3 | Con `hand_group`: `RectTransform` trống, anchor và pivot giữa; `Focus` ghi `position` và góc xoay lên nó | — |
| 4 | Con `hand` trong `hand_group`: sprite ngón trỏ **vẽ trỏ sang phải**, đặt sao cho **đầu ngón trùng gốc của `hand_group`** | đầu ngón không ở gốc thì `TargetPadding` đo tới giữa sprite, tay đè lên target; sprite vẽ hướng khác thì mọi góc lệch một hằng |
| 5 | Kéo `selfCanvas` · `selfRect` · `handGroup` | NRE ở `Awake` lần Play đầu |
| 6 | Đúng **một** instance trong `Services.unity` | InitArgs không tìm thấy service; consumer nhận null im lặng ở release, NRE ở `Focus` |

### 3.2. Target (mỗi nút được chỉ)

| # | Bước | Thiếu thì hỏng ở đâu |
|---|---|---|
| 1 | `Canvas` + `GraphicRaycaster` lên **đúng object cần sáng**: `Canvas` **để bật**, `GraphicRaycaster` **bỏ tick** (`Focus` tự bật, `Release` trả về) | thiếu raycaster: `LogError` rồi NRE ở `Focus`. `Canvas` tắt thì `overrideSorting` không có tác dụng, target không nổi lên trên lớp tối. Raycaster để bật sẵn không hỏng, chỉ tốn một batch riêng suốt phiên |
| 2 | Root canvas của target là **Screen Space - Overlay** | canvas Camera không nâng lên trên lớp tối Overlay được: tối toàn màn, không gì sáng |
| 3 | Phơi Canvas ra property kiểu `Canvas` (`HighlightCanvas`, `InfoBtnCanvas`) | hợp đồng "target là canvas" nằm ở kiểu tham số, không `GetComponent` lúc chạy |

---

## §4. Hai luật thứ tự trong code

| Ở đâu | Thứ tự | Vì |
|---|---|---|
| `Focus` | bật raycaster **rồi mới** `overrideSorting` | `Graphic` gắn với canvas gần nhất **đang bật**; canvas phải bật sẵn trong prefab, vì `overrideSorting` trên canvas tắt không đổi được gì và nút vẫn thuộc canvas cha dưới lớp tối |
| `Release` | huỷ loop → trả sorting → tắt canvas | không có frame nào canvas lồng đang bật mà sorting đã về thấp; loop không chạm `handGroup` sau khi object tắt |

---

## §5. Bẫy

| Bẫy | Hệ quả |
|---|---|
| Service bị destroy khi đang `Focus` | loop tỉnh frame kế, ghi `position` lên transform đã chết → `MissingReferenceException` ra `Forget()`. Service sống cả đời app nên chưa xảy ra; có đường destroy thì `OnDestroy` phải gọi `Release()` |
| `HandTargetOffset = 0` | tay đứng im, không log |
| Một canvas khác có `sortingOrder > highlightSortingOrder + 1` | đè lên target, tap không ăn |

---

## §6. Quyết định thiết kế

| Quyết định | Vì |
|---|---|
| Nâng sorting của target, không khoét lỗ trên lớp tối | target và lớp tối cùng là Canvas, đổi sorting là cơ chế sẵn của uGUI; không shader, không `ICanvasRaycastFilter`. Đổi lại: hợp đồng target §3.2 và giới hạn Overlay |
| Chữ ký nhận `Canvas` | caller không có Canvas thì không compile, thay vì `GetComponent` null lúc chạy |
| Không `AddComponent` lúc chạy | thứ cần có trên prefab thì dựng trên prefab; thêm lúc chạy phải nhớ gỡ |
| Bật tắt raycaster của target trong `Focus`/`Release`, Canvas luôn bật | raycaster tắt lúc nghỉ thì target không chặn tap của cha; Canvas bật sẵn nên không có lượt gắn lại `Graphic` |
| `SetActive(false)` cả object lúc nghỉ | không việc gì mỗi frame; `FindFromScene` vẫn resolve object inactive |
| Góc xoay suy từ vector, không switch thứ hai theo enum | vector và góc buộc khớp; thêm hướng mới chỉ đụng `GetDirectionVector` |
| Nhấp bằng `CharmPointAndBob` + một `CancellationTokenSource` mỗi `Focus` | vòng lặp là việc của `Charm*` với điều kiện có chủ huỷ; `Release` huỷ và dispose trước khi trả target. Không link `destroyCancellationToken`: `GetCancellationTokenOnDestroy` `AddComponent` lúc chạy và mỗi `Focus` thêm một registration không dispose |
| `Yield` trong loop không mang token, kiểm `IsCancellationRequested` sau khi tỉnh | huỷ là `return` thường, không `OperationCanceledException`; UniTask không đăng ký callback cho token trên `Yield` nên đây không phải chuyện alloc |
| Nhịp tay (1 s, `OutQuad`) là hằng trong `Focus` | bản sắc chung của mọi tutorial; đưa vào config là mỗi caller phải chọn một số họ không có lý do để khác |
| `Focus` khi đang `Focus` → `Release` cái cũ rồi thay | tutorial luôn tuần tự; không để lại canvas nào bị nâng |
| `Direction` dùng chung của Horcrux, `ShowHand` là cờ riêng | `Direction` không có giá trị "không" |

---

## §7. Cố ý không có

| Không có | Thêm lại khi |
|---|---|
| Bám target mỗi frame | target đang tween lúc tutorial → truyền `Transform` cho loop thay vì `Vector3` |
| Guard `ShowHand == false` không chạy loop | profiler thấy một `Lerp` mỗi frame trên object tắt, hoặc `NoHand` thành ca thường |
| Frame viền, nhãn chữ | chữ là màn riêng của consumer; viền chưa ai cần |
| Pool flyer bắn hàng loạt | dựng trên `CharmFlyArc` khi có nhu cầu thật |
| Sáng hơn hai chỗ cùng lúc | yêu cầu mới → overload nhận mảng (hiện tối đa hai: `target` + `extra`) |
| `await` trong API | service không biết nút; mỗi caller một kiểu chờ |
| Canvas Camera / gameplay 3D | khác cơ chế sorting; là hệ khác |
| `OnDestroy` gọi `Release()` | xuất hiện đường destroy service khi đang `Focus` |

---

## §8. Nghiệm thu

Chưa có scene demo trong SDK — kiểm trong scene thật, Play từ `Start.unity`.

| Bảo đảm | Phép kiểm | Kỳ vọng |
|---|---|---|
| Lúc nghỉ không tốn gì | Play, không tutorial | object inactive; Frame Debugger không có batch của nó |
| Target sáng và nhận tap | `Focus` một nút, tap nó | màn tối trừ nút; listener của nút chạy; tap chỗ khác không xuống UI dưới |
| Tay đúng phía, đúng hướng | `Focus` với `TopCenter` rồi `TopLeft` | tay ở dưới trỏ lên, rồi ở góc dưới phải trỏ lên trái; nhấp về phía target |
| Nhấp dưới pause | `Time.timeScale = 0` khi đang `Focus` | tay vẫn nhấp |
| Thay target không kẹt | `Focus(A)` → `Focus(B)` → `Release` | A về giá trị cũ ngay khi `Focus(B)`; sau `Release` cả A lẫn B có raycaster `enabled = false`, `overrideSorting = false` |
| Huỷ giữa chừng | `Focus` rồi tắt Home | object highlight tắt, target về nghỉ, Console không đỏ |
| Target bật sẵn vẫn đúng | target có raycaster tick sẵn, `Focus` rồi `Release` | raycaster vẫn tick, sorting về số cũ |
