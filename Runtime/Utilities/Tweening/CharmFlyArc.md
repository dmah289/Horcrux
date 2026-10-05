# CharmFlyArc, CharmPunchScale, CharmCount — bay đường cong, nảy, đếm số

Ba primitive dựng trên `CharmTween.CastAsync`, dùng thời gian **unscaled** (chạy được khi `timeScale = 0`). Cùng một luật:
**hàm sở hữu trạng thái cuối ở mọi đường ra** (xong, huỷ, thời lượng 0) qua một `try/finally`, caller chỉ `await` hoặc
ghép bằng `WhenAll`. Object Unity bị huỷ giữa chừng cũng là một đường ra, nên `finally` guard `!= null`.

Code: `Transform/TransformExtensions.CharmTween.cs` · `TextMeshProExtensionMethods.CharmTween.cs` · `Tweening/ArcFlightSpec.cs` ·
`Common/BezierCurveHelper.cs` · `PhysXHelper/SquashStretch.cs` · `PhysXHelper/HarmonicOscillator.cs`.

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
await tr.CharmPunchScale(restScale, amplitude, duration, ct, cycles: 2);
```

Mỗi **cycle** là một nhịp rest → cực trị → rest. Cycle lẻ lún xuống `lower`, cycle chẵn phồng lên `upper`;
mặc định `cycles = 2` là rest → lower → rest → upper → rest. Tính qua `HarmonicOscillator.GetHarmonicDisplacement`:

$$s = \sin(\pi N t), \qquad x = -A\,s\,|s|, \qquad \text{localScale} = \text{restScale}\cdot(1 + x)$$

với `t` ∈ [0, 1] là thời gian chuẩn hoá của `CastAsync`, `A` = `amplitude`, `N` = `cycles`. Đáy và đỉnh
đúng bằng `restScale·(1 ∓ A)`.

Dẫn từ `sin` thuần:

1. `sin(πNt)` qua mốc rest ở mỗi `t = k/N` và đổi dấu sau mỗi nhịp — đúng thứ tự xuống, lên. Nhưng nó qua rest ở vận tốc lớn nhất, nên dừng cuối bị khựng.
2. Bình phương `sin²θ = (1 − cos 2θ)/2` giữ đúng hình một nhịp, vận tốc 0 ở rest — nhưng mất dấu, nhịp nào cũng cùng một phía.
3. `s·|s|` lấy độ lớn của bước 2 và dấu của bước 1. Đạo hàm `2|s|·s′` bằng 0 mỗi khi `s = 0`: qua rest êm, kể cả mốc cuối.

**Bản chất vật lý.** Dòng `wave` không phải chuyển động của vật — nó là đồng hồ (điểm 0 = ranh giới nhịp) và nguồn
dấu (σ = sgn(s): +1 nhịp lún, −1 nhịp phồng). Dòng `displacement` mới là chuyển động; hạ bậc ra:

$$x = -\sigma\tfrac{A}{2} + \sigma\tfrac{A}{2}\cos(\Omega t), \qquad \Omega = 2\pi N$$

tức **lò xo không ma sát có điểm cân bằng nhảy**: $m\ddot{x} = -k(x - x_{eq})$ với $x_{eq} = -\sigma A/2$ (giữa rest và
cực trị), $\sqrt{k/m} = \Omega$. Mỗi nhịp vật thả từ rest với vận tốc 0, đi đúng một chu kỳ lò xo, về rest với vận tốc 0;
lúc đó điểm cân bằng nhảy sang phía kia. Hệ quả:

| Đại lượng | Giá trị | Ở đâu |
|---|---|---|
| Tốc độ lớn nhất | $\tfrac{A}{2}\Omega = A\pi N$ — bằng sin thuần | giữa đường (x = ∓A/2), không ở rest |
| Gia tốc lớn nhất | $\tfrac{A}{2}\Omega^2$ | ở rest và cực trị |
| Theo giây | $\Omega_s = 2\pi N / \text{duration}$ | A = 0.05, N = 2, 0.5 s → Ω ≈ 25.1 rad/s, v ≈ 0.63 scale/s, a ≈ 15.8 scale/s² |

Chỗ duy nhất không vật lý: ở mốc rest giữa hai nhịp σ đổi dấu, gia tốc nhảy từ $-\tfrac{A}{2}\Omega^2$ sang
$+\tfrac{A}{2}\Omega^2$. Tăng `cycles` giữ `duration` thì gia tốc tăng theo $N^2$.

| t (A = 0.05, rest = 1) | 0 | 0.25 | 0.5 | 0.75 | 1 |
|---|---|---|---|---|---|
| `cycles = 1` | 1 | 0.975 | **0.95** | 0.975 | 1 |
| `cycles = 2` | 1 | **0.95** | 1 | **1.05** | 1 |

`cycles = 3`: lower ở t = 1/6, upper ở 1/2, lower ở 5/6.

| Quyết định | Vì |
|---|---|
| `amplitude` là **tỉ lệ** của `restScale`, không phải scale đích | người tune nhập đúng độ lệch nhìn thấy; rest không đều trục vẫn giữ tỉ lệ |
| `s·\|s\|`, không `sin` thuần | vận tốc 0 ở mọi mốc rest: không khựng ở cuối, không gãy giữa hai nhịp |
| Truyền `cycles · 0.5` vào ô `frequency`, `t` vào ô `time` | một cycle là nửa chu kỳ sin; pha chỉ phụ thuộc tích `frequency · time`, đơn vị chuẩn hoá tránh chia cho `duration` (NaN khi bằng 0) |
| `cycles` là `int` | `sin(πN) = 0`: frame cuối (`Easer` kẹp t ≤ 1) đã ở đúng `restScale` |
| Không envelope tắt dần | mọi nhịp cùng biên độ, số nhịp đếm được bằng mắt |
| Gia tốc đổi dấu tức thì ở mốc rest giữa hai nhịp | vị trí và vận tốc liền; chỉ đạo hàm bậc hai nhảy. Có thấy hay không thì chơi thử mới biết |
| `amplitude` ≥ 1 | đáy ≤ 0, vật biến mất hoặc lật — caller kẹp ô Inspector bằng `[Range(0, 1)]` |
| `restScale` là tham số, caller thường truyền `localScale` hiện tại | đổi scale nghỉ của prefab không vỡ. Gọi chồng khi lượt trước đang lắc thì scale giữa sóng thành trục nghỉ mới, `finally` chốt vật ở đó |
| Huỷ hoặc xong đều đặt về `restScale` (guard `!= null`) | không để vật kẹt ở scale đỉnh |

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
