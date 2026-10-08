# CharmFlyArc, CharmSquashStretch, CharmCount — bay đường cong, nén giãn, đếm số

Ba primitive dựng trên **PrimeTween** (`Tween.Custom`), dùng thời gian **unscaled** (chạy được khi `timeScale = 0`). Trả về `Tween`:
caller `await` được (chuyển ngầm sang `UniTask`) hoặc giữ handle để `Stop()`. Cùng một luật:
**huỷ là dừng tại chỗ** — `SetCancellationToken(ct)` dừng tween, không đặt về trạng thái cuối, không `try/finally`.
`await` mà token bị huỷ thì ném `OperationCanceledException`, caller tự bắt nếu cần. Nảy scale dùng thẳng `Tween.PunchScale`
của PrimeTween, không còn `CharmPunchScale`.

Code: `Transform/TransformExtensions.CharmTween.cs` · `TextMeshProExtensionMethods.CharmTween.cs` · `Transform/ArcFlightSpec.cs` ·
`Transform/SquashStretchSpec.cs` · `PhysXHelper/BezierCurveHelper.cs` · `PhysXHelper/SquashStretch.cs`.

---

## §1. `CharmFlyArc` — chỉ bay

```csharp
await tr.CharmFlyArc(target, spec, ct);
```

Đường bay là bezier bậc hai: `from` = vị trí lúc gọi, điểm điều khiển nằm ở `controlAtRatio` dọc đường thẳng rồi lệch
`controlOffset` theo phương vuông góc (`BezierCurveHelper.ComputeControlPoint`, mặt phẳng XY). **Không đụng scale.**

| Field `ArcFlightSpec` | Ý nghĩa |
|---|---|
| `controlOffset` | độ lệch của cung; dấu chọn phía cong |
| `speed` (`[Min(0.01f)]`) | đơn vị thế giới mỗi giây, tính trên **độ dài cung** |
| `controlAtRatio` (`[Min(0.1f)]`) | vị trí đỉnh cung dọc đường thẳng (0.5 = giữa) |
| `progressEase` | `PrimeTween.Ease`, chỉ lái nhịp của vị trí |

`duration = ApproximateQuadraticBezierLength(from, target, control) / speed`, với `L ≈ (2·|from→target| + |from→control| + |control→target|) / 3`
(chính xác khi điểm điều khiển nằm trên đường thẳng).

| Quyết định | Vì |
|---|---|
| `speed`, không `duration` | quãng bay đổi theo vị trí lúc gọi; tốc độ giữ cảm giác thống nhất (MY_SKILL §4.1) |
| Xấp xỉ độ dài cung, không tích phân | tốc độ là núm chọn bằng mắt; sai số vài phần trăm không thấy |
| Tốc độ là **trung bình** | `progressEase` bóp méo nhịp bên trong nhưng tổng thời gian = độ dài / tốc độ |
| `from == target` cho `duration = 0` | PrimeTween chỉ cảnh báo; tắt bằng `PrimeTweenConfig.warnZeroDuration = false` ở boot của dự án |
| Huỷ giữa chừng dừng tại chỗ | không snap về `target` |

---

## §2. `CharmSquashStretch` — chỉ nén giãn

```csharp
await tr.CharmSquashStretch(spec, ct);
```

`localScale = SquashStretch.GetSquashStretch(t, startScale, endScale, AxisType.Y, CoordinateSystem.XY)` với `t` **đã qua ease**
(`SquashStretchSpec.ease`, kiểu `PrimeTween.Ease`). Giữ thể tích: Y nén thì X phình. Muốn vừa bay vừa nén thì caller
`UniTask.WhenAll(tr.CharmFlyArc(...), tr.CharmSquashStretch(...))`.

| Quyết định | Vì |
|---|---|
| Hai hàm tách, không gộp | gộp làm khó mở rộng; ghép là việc của caller |
| `GetSquashStretch` nhận `t` đã ease | PrimeTween lo ease, hàm chỉ còn toán thuần |
| Ease vọt (`OutBack`, `OutElastic`) cho hiệu ứng giãn | `Mathf.Lerp` không dùng, `math.lerp` không kẹp nên vọt được |

---

## §3. `CharmCount` — đếm số trên TMP

```csharp
await label.CharmCount(start, end, "{0}", duration, startDelay, ct);
```

Mỗi frame `SetText(format, round(lerp(start, end, t)))`, `t` tuyến tính. `startDelay` là `startDelay` của PrimeTween.
Huỷ (hoặc object huỷ) thì dừng ở số đang hiện, không nhảy tới `end`.

| Quyết định | Vì |
|---|---|
| `SetText(format, int)` | overload không cấp phát chuỗi; `format` là mẫu `{0}` của TMP, ví dụ `"x{0}"` |
| `Mathf.Lerp` (kẹp) | không đếm quá `end` |
| Ease cố định `Linear` | đếm số cần nhịp đều |

---

## §4. Bẫy

| Bẫy | Hệ quả |
|---|---|
| Quên điền field của spec | struct không có giá trị mặc định: `speed = 0` bị `[Min]` chặn trong Inspector, nhưng spec dựng bằng code thì chia 0, `await` treo |
| `WhenAll` mà một bên bị huỷ | cùng token thì cùng dừng, mỗi bên dừng tại chỗ, scale/vị trí không đồng bộ về đích |
| Gọi trên object inactive | tween vẫn chạy nhưng frame không vẽ; bật object trước khi gọi |
| Object bị trả pool khi tween đang chạy | phải huỷ token hoặc `Stop()` tween; PrimeTween không tự biết |
