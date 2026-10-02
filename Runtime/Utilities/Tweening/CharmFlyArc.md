# CharmFlyArc, CharmPunchScale, CharmCount — bay đường cong, nảy, đếm số

Ba primitive dựng trên `CharmTween.CastAsync`, dùng thời gian **unscaled** (chạy được khi `timeScale = 0`). Cùng một luật:
**hàm sở hữu trạng thái cuối ở mọi đường ra** (xong, huỷ, thời lượng 0) qua một `try/finally`, caller chỉ `await` hoặc
ghép bằng `WhenAll`. Object Unity bị huỷ giữa chừng cũng là một đường ra, nên `finally` guard `!= null`.

Code: `Transform/TransformExtensions.CharmTween.cs` · `TextMeshProExtensionMethods.CharmTween.cs` · `Tweening/ArcFlightSpec.cs` ·
`Common/BezierCurveHelper.cs` · `PhysXHelper/SquashStretch.cs`.

---

## §1. `CharmFlyArc` — hai overload

Đường bay là bezier bậc hai: `from` = vị trí lúc gọi, điểm điều khiển lệch `controlOffset` so với đường thẳng (`BezierCurveHelper.ComputeControlPoint`).
Spec là struct `[Serializable]`, tune trong Inspector. Struct **không có giá trị mặc định**: field để 0 là scale 0, vật biến mất không báo lỗi.

| Overload | Spec | Scale khi bay | Cuối |
|---|---|---|---|
| `CharmFlyArc(target, ArcFlightSpec, ct)` | `controlOffset · duration · progressEase · startScale · endScale · scaleEase` | đều mọi trục, `startScale → endScale` theo `scaleEase`; `progressEase` chỉ lái vị trí | `position = target`, `scale = endScale` |
| `CharmFlyArc(target, SquashStretchFlightSpec, ct)` | `controlOffset · progressDuration · progressEase · startScale · endScale · recoverAfterLanding · recoverScale · recoverDuration` | trục Y theo `SquashStretch.GetSquashStretch` (giữ thể tích: Y nén thì X phình), `startScale → endScale` | `position = target`; `recoverAfterLanding` bật thì hồi về `recoverScale` trong `recoverDuration`, tắt thì dừng ở dạng nén `endScale` |

| Quyết định | Vì |
|---|---|
| Hai overload cùng tên, khác kiểu spec | chọn kiểu bay bằng kiểu dữ liệu, không bằng cờ |
| `progressEase` lái cả vị trí lẫn tiến độ squash (overload squash) | một nhịp duy nhất; hai ease lệch nhau nhìn như hai vật |
| Mốc cuối đặt trong `finally`, bao cả pha hồi | huỷ giữa pha hồi vẫn ra scale cuối đúng |
| Cố ý không có pool bắn hàng loạt | dựng trên primitive này khi có nhu cầu thật |

Ví dụ giá trị gợi ý (tune lại trong Inspector): badge nhảy `controlOffset 80 · progressDuration 0.35 · InQuad · 1.3 → 0.8 · recover tắt`; token bay `controlOffset −150 · duration 0.6 · InQuad · 1 → 0.25 · Linear`.

---

## §2. `CharmPunchScale` — nảy rồi về

```csharp
await tr.CharmPunchScale(restScale, maxScale, duration, vibrato: 10, delaySeconds, ct, onComplete);
```

Kiểu `DOPunchScale`: dao động tắt dần quanh `restScale`, `localScale = restScale + (maxScale − restScale) · sin(2π · cycles · t) · (1 − t)`
với `cycles = max(1, round(vibrato · duration / 2))`. Lượt đầu đẩy tới `maxScale`, lượt sau lắc ngược dưới `restScale` rồi tắt. `finally` đặt
`localScale = restScale` rồi gọi `onComplete` **một lần**.

| Quyết định | Vì |
|---|---|
| Công thức đóng, không ghép hai pha | hai pha ease riêng luôn có vận tốc nhảy ở đỉnh; một hàm `sin` tắt dần thì liên tục ở mọi điểm |
| `cycles` là số nguyên | `sin(2π·cycles)` = 0 và đạo hàm cuối bằng 0 nên kết thúc phẳng đúng ở `restScale`, không bị `finally` bắt về |
| Không có `ease`, không có `elasticity` | độ tắt tuyến tính `(1 − t)` như DOTween; đổi cảm giác bằng `vibrato` và `duration`. Thêm elasticity khi có nhu cầu thật |
| Caller truyền `restScale` bằng scale đang có | hàm không lerp từ scale hiện tại; khác nhau thì frame đầu nhảy |

| Quyết định | Vì |
|---|---|
| `restScale` là tham số, không hardcode `Vector3.one` | đổi scale nghỉ của prefab không vỡ; caller thường truyền `localScale` hiện tại |
| Huỷ giữa chừng đặt về `restScale` | không để vật kẹt ở scale đỉnh |

---

## §3. `CharmCount` — đếm số trên TMP

```csharp
await label.CharmCount(start, end, "{0}", EaseType.Linear, duration, delaySeconds, ct);
```

Mỗi frame `SetText(format, round(lerp(start, end, t)))`. `finally` đặt `SetText(format, end)`, bao cả pha `delaySeconds`: huỷ hoặc destroy
trong lúc delay vẫn ra số cuối (nếu label còn sống).

| Quyết định | Vì |
|---|---|
| `SetText(format, int)` | overload không cấp phát chuỗi; `format` là mẫu `{0}` của TMP, ví dụ `"x{0}"` |
| `Mathf.Lerp` (kẹp) | ease vọt (`OutBack`) không đếm quá `end` |
| Guard `self != null`, không `self?.` | `?.` bỏ qua phép so sánh null đã overload của Unity: label đã huỷ vẫn bị gọi |

---

## §4. Bẫy

| Bẫy | Hệ quả |
|---|---|
| Quên điền một field của spec | struct không có `Default`: scale 0 làm vật biến mất (squash-stretch kẹp `1e-4`, thành vệt mỏng), không log |
| `WhenAll(punch, count)` mà một bên bị huỷ | cả hai cùng nhận token nên cùng huỷ; mỗi bên tự đặt trạng thái cuối |
| Gọi trên object inactive | `Charm*` vẫn chạy, nhưng frame không vẽ; bật object trước khi gọi |
