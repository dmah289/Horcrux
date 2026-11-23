# Plan — `SetFraction` lên Horcrux

Nguồn: bản tạm của một dự án game, dùng cho nhãn "đã lấp/tổng" trên vật chứa và "đã nhặt/tổng" trên HUD, đã chạy trong build. Một
extension cạnh `SetNumber` trong `UniTextExtensionMethods`.

**Phân công:** GĐ1 developer gõ, agent viết test theo bảng case rồi compile · GĐ2 (dự án chuyển sang bản Horcrux) và GĐ3 (tài liệu,
xoá Plan) agent làm. GĐ1, GĐ3 commit trong `Assets/Horcrux`, GĐ2 commit ở dự án; mỗi lần đều kèm con trỏ submodule. Không phụ thuộc
Plan nào, không có `§0`.

## Ngữ cảnh đã chốt

- **Người dùng:** nhãn tiến độ "x/y" đổi mỗi tương tác — ô đã lấp, vật đã nhặt, câu đã trả lời. Dự án nguồn có hai caller.
- **Chỗ đặt, tên:** `Runtime/Utilities/ExtensionMethods/UniTextExtensionMethods.cs`, ngay sau `SetNumber`, cùng namespace
  `Horcrux.Runtime.Utilities.ExtensionMethods`.
- **Vì sao không cache chuỗi theo cặp số:** bảng lớn dần theo mọi cặp từng gặp, và mỗi caller tự dựng một bảng (dự án nguồn đã có ba
  bản cho số và cặp số). Span trên stack không giữ gì, không có bảng để quên dọn.
- **Cố ý không làm:** ký tự ngăn khác `/`, dấu cách quanh nó — chưa có người dùng; cần thì thêm overload nhận `char`, không đổi bản
  này · số thực.

## Đã khảo sát

| Nơi | Kết luận |
|---|---|
| `UniTextExtensionMethods.SetNumber` | một số; gọi hai lần không ghép được vì mỗi lần ghi đè cả chữ |
| `UniTextExtensionMethods.CharmCount` | khuôn `{0}` qua `StringBuilder` sống trong closure của tween; một số, không hợp cho lần ghi rời |
| `UniTextBase.SetText(ReadOnlySpan<char>)` | ghi span không tạo chuỗi; cùng nội dung với lần trước thì không dựng lại |

## Luồng mô tả

```
Dữ liệu đổi (curr, total):
    label.SetFraction(curr, total)
        └─ stackalloc 23 ký tự → curr.TryFormat → '/' → total.TryFormat → SetText(span)
```

## Giai đoạn 1 — `SetFraction`

**Mục tiêu:** ghi "x/y" lên mọi `UniTextBase` không cấp phát. **Vì sao trước:** là toàn bộ hệ.

`UniTextExtensionMethods.cs` › sau `SetNumber`, trước `CharmCount`. Có sẵn quanh chỗ thêm:

```csharp
            value.TryFormat(digits, out var written);
            self.SetText((ReadOnlySpan<char>)digits.Slice(0, written));
        }
```

Thêm ngay dưới:

```csharp

        /// <summary>Ghi "curr/total" không tạo chuỗi; cùng cặp số với lần trước thì UniText không dựng lại.</summary>
        public static void SetFraction(this UniTextBase self, int curr, int total)
        {
            // Hai int tối đa 11 ký tự mỗi số (kể cả dấu âm) và một dấu '/'.
            Span<char> chars = stackalloc char[23];
            curr.TryFormat(chars, out var currLength);
            chars[currLength] = '/';
            total.TryFormat(chars.Slice(currLength + 1), out var totalLength);
            self.SetText((ReadOnlySpan<char>)chars.Slice(0, currLength + 1 + totalLength));
        }
```

- 23 = 11 + 1 + 11: `int.MinValue` là 11 ký tự. Buffer thiếu thì `TryFormat` trả `false`, `written` = 0 và chữ mất số im lặng.
- Ép `(ReadOnlySpan<char>)` như `SetNumber`: hai extension đi cùng một overload `SetText`, cùng luật "cùng nội dung thì không dựng lại".
- Không kiểm `self` null: caller kéo ô trong Inspector, thiếu ô nổ ngay lần Play đầu.

**Editor setup:** không.

**Kiểm:** compile sạch; test EditMode `Horcrux.EditModeTests` cạnh `UniTextSetNumberTests` (agent viết sau khi đọc code thật):

| Input | Kỳ vọng |
|---|---|
| `(0, 0)` · `(3, 12)` · `(12, 3)` · `(-4, 7)` | chữ nguồn bằng `$"{curr}/{total}"` |
| `(int.MinValue, int.MinValue)` · `(int.MaxValue, int.MaxValue)` | như trên — chạm trần buffer |
| đổi số sau hai lần ghi | `Is.Not.AllocatingGCMemory()` |
| đối chứng: `SetText($"{n}/20")` | `Is.AllocatingGCMemory()` — cùng phép đo bắt được chuỗi mới |

**Mốc commit:** compile sạch + bảng case xanh → commit trong `Assets/Horcrux`.

## Giai đoạn 2 — dự án chuyển sang bản Horcrux (agent)

**Mục tiêu:** dự án dùng bản Horcrux, bản tạm biến mất. **Vì sao sau:** cần bản Horcrux compile được và test xanh trước.

- Đổi `using` từ namespace của bản tạm sang `Horcrux.Runtime.Utilities.ExtensionMethods` ở mọi caller (nhãn đếm trên vật chứa, HUD
  tiến độ). Caller nào đã có cả hai `using` thì bỏ `using` bản tạm, không để hai extension cùng tên.
- Xoá file bản tạm cùng `.meta`; xoá test bản tạm: test Horcrux ở GĐ1 đã phủ bảng case.
- Cập nhật tài liệu dự án nhắc bản tạm.

**Kiểm:** grep tên class bản tạm ra 0; compile sạch; test xanh. **Mốc commit:** một commit ở dự án kèm con trỏ submodule.

## Giai đoạn 3 — Tài liệu (agent)

- `docs/Runtime/CharmFlyArc/CharmFlyArc.md` › §3, bảng quyết định: thêm hàng `SetFraction(int, int)` cùng file ngay dưới hàng
  `SetNumber(int)` — ghi "x/y" không cấp phát, cho nhãn tiến độ đổi mỗi tương tác.
- `docs/Editor/TmpToUniText.md` › hàng `SetText("{0}", n)`: thêm `SetFraction(int, int)` cho khuôn `"{0}/{1}"`.

Xong thì **xoá Plan này** (`Horcrux_UniTextSetFraction_Plan.md` cùng `.meta`).

**Mốc commit:** `docs(unitext): SetFraction`, xoá Plan.

## Use case

Ô đã lấp trên vật chứa · số vật đã nhặt trong màn · số câu đã trả lời trong quiz · số quái đã hạ trong đợt.

## File → giai đoạn

| File | GĐ |
|---|---|
| `Runtime/Utilities/ExtensionMethods/UniTextExtensionMethods.cs` | 1 |
| Test EditMode `UniTextSetFractionTests` | 1 (viết), 2 (xoá bản dự án) |
| Caller và bản tạm của dự án | 2 |
| `docs/Runtime/CharmFlyArc/CharmFlyArc.md`, `docs/Editor/TmpToUniText.md` | 3 |
