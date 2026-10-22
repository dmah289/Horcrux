# CharmJump, CharmLocalJump, JumpArc — nhảy theo cung (DOJump một lần nhảy)

PrimeTween không có `DOJump`. `CharmJump` dựng lại đúng `DOJump(endValue, height, 1, duration)` của DOTween
trên **PrimeTween**, và mỗi lần gọi cấp phát **0 B**.

Code:
- `Transform/TransformExtensions.Jump.cs`: `CharmJump`, `CharmLocalJump`.
- `Transform/JumpArc.cs`: phần toán thuần.

Cả hai đều nằm trong namespace `Horcrux.Runtime.Utilities.ExtensionMethods`.

**Hợp đồng — khác `CharmFlyArc`, `CharmSquashStretch`, `CharmCount`:**
- trả `Sequence`;
- **không nhận `ct`**;
- `useUnscaledTime = false` mặc định, giống PrimeTween.

Lý do: caller nhét kết quả vào một Sequence lớn hơn, mà PrimeTween không cho tween con trong Sequence mang token. Muốn huỷ thì caller gắn token hoặc gọi `Stop()` lên Sequence ngoài cùng.

**Tiền tố `Charm`** chỉ nói "helper của Horcrux do developer viết", **không nói hợp đồng**. Hợp đồng của từng hàm đọc ở tài liệu của chính nó.

---

## §1. API

```csharp
seq.Group(tr.CharmJump(target, height, duration, Ease.Linear));      // world
seq.Group(tr.CharmLocalJump(target, height, duration));              // local, ease mặc định
tr.localPosition = JumpArc.Evaluate(from, to, height, t);            // tự lái t
```

| Chữ ký | Trả về |
|---|---|
| `Transform.CharmJump(Vector3 endValue, float height, float duration, Easing ease = default, bool useUnscaledTime = false)` | `Sequence`, chạy trên `position` |
| `Transform.CharmLocalJump(...)` (cùng tham số) | `Sequence`, chạy trên `localPosition` |
| `JumpArc.ComputeArcCoefficient(float fromY, float toY, float height)` | `K = (toY − fromY) + 4·height` |
| `JumpArc.EvaluateHeight(float linearY, float arcCoefficient, float t)` | `linearY + K·t·(1 − t)` |
| `JumpArc.Evaluate(Vector3 from, Vector3 to, float height, float t)` | điểm trên cung tại `t` (đã qua ease); không kẹp nên chạy được cả `t` ngoài [0, 1] |

- `height`: độ cao đỉnh cung so với đường thẳng, khi hai đầu cùng độ cao.
- `ease`: lái **cả cú nhảy**, như `SetEase` lên `DOJump`. Mặc định là ease mặc định của PrimeTween (OutQuad), trùng mặc định của DOTween.
- `Ease.Linear`: x đi đều, y theo parabol thời gian, tức đúng chuyển động ném xiên.

---

## §2. Toán

DOTween dựng Y bằng phần bù `Δy·OutQuad(s)` cộng phần nảy `h·OutQuad` chạy Yoyo hai lượt. Hai lượt Yoyo gộp lại đều ra `4h·s(1 − s)`. Tách `s(2 − s) = s + s(1 − s)` thì được:

```
y(s) = (yA + Δy·s)  +  K·s(1 − s),   K = Δy + 4h,   s = ease(t)
        đường thẳng      cái bướu
```

x và z là lerp theo cùng `s`. Mọi toạ độ chỉ phụ thuộc `s`, nên áp `ease` riêng cho từng tween con ra đúng như áp lên cả Sequence.

Mốc kiểm, với `from = (0,0,0)` và `h = 1`:

| to | s | y |
|---|---|---|
| `(4,0,2)` | 0 / 0.5 / 1 | 0 / 1 / 0 |
| `(0,2,0)` | 0.5 | 2.5 |

---

## §3. Luồng dữ liệu

```
CharmJump(B, h, d, ease)
  from = position (đọc MỘT lần lúc gọi)
  K    = ComputeArcCoefficient(from.y, B.y, h)
  Sequence(useUnscaledTime)
   ├─ Tween.PositionX(from.x → B.x, ease)
   ├─ Tween.PositionZ(from.z → B.z, ease)
   └─ Tween.Custom(self, (from.y, 0, K) → (B.y, 1, K), SetJumpY, ease)
        v = lerp(start, end, s) = (y đường thẳng, s, K)
        SetJumpY (static): position.y = EvaluateHeight(v.x, v.z, v.y)
```

---

## §4. Quyết định

| Quyết định | Vì |
|---|---|
| 3 tween (X, Z, Custom cho Y), không dùng một Custom | một Custom phải mang 7 số (from, to, K), không vừa `Vector4` nên phải capture closure, tức cấp phát mỗi lần gọi. Y chỉ cần 3 số, vừa một `Vector3` |
| Setter là `static readonly Action<Transform, Vector3>` với lambda `static` | lỡ capture biến ngoài thì thành lỗi biên dịch, không thành rác GC âm thầm |
| `ease` đưa xuống từng tween con | `Sequence.Create` chỉ nhận enum `Ease`; đưa xuống con thì dùng được cả `Easing` (curve, parametric) mà kết quả không đổi (§2) |
| `useUnscaledTime` chỉ đặt ở `Sequence.Create` | tween con để `false`; PrimeTween chỉ cảnh báo khi con là `true` mà khác cha |
| Hai cửa world/local, không dùng cờ `Space` | biến thể là cửa riêng; toán chung đã nằm trong `JumpArc` |
| Chỉ một lần nhảy | chưa có caller cần `numJumps > 1` |

---

## §5. Bẫy

| Bẫy | Hệ quả |
|---|---|
| Điểm xuất phát chỉ biết **sau** khi Sequence đã dựng (ví dụ được gán trong `ChainCallback`) | `CharmJump` đọc `position` lúc gọi nên nhảy từ chỗ cũ. Ca này tự lái `t` bằng `Tween.Custom` rồi gọi `JumpArc.Evaluate`; closure là giá phải trả |
| B thấp hơn A mà `h < −Δy/2` | vật không bật lên lúc đầu, đi xuống ngay. `h < −Δy/4` (K < 0) thì cung võng xuống dưới cả đường thẳng. DOTween cũng vậy |
| Nhét Sequence `useUnscaledTime: true` vào một Sequence scaled | PrimeTween cảnh báo và Sequence cha thắng |
| Gọi `CharmJump` lên object đang có tween position khác | hai tween cùng ghi `position`; tween nào chạy sau trong frame thắng ở trục nó ghi. Dừng tween cũ trước |
| Dự án có extension method trùng tên với `Horcrux.Runtime.Utilities.ExtensionMethods` (ví dụ `SetAlpha`) | file `using` cả hai namespace thì call site báo lỗi ambiguous (CS0121). File chuyển sang Horcrux thì bỏ `using` namespace extension của dự án, để mọi extension trùng tên dùng bản Horcrux |
