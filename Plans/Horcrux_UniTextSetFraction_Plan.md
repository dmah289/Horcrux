# Plan — `SetFraction`: ghi "x/y" lên UniText không tạo chuỗi

Bản tạm: category-jam `Assets/_CategoryJam/Scripts/Utilities/UniTextFractionExtensions.cs` › `SetFraction`, test `Tests/EditMode/UniTextFractionTests.cs`. Không phụ thuộc Plan nào; category-jam `CategoryBox.cs` chung với Chuyển dự án của `Horcrux_SpriteFill_Plan.md` — gõ cả hai thì chuyển chung một lần.
Kiểm: `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,Horcrux.EditModeTests.csproj --tests Horcrux.EditModeTests`.

## Kịch bản

| Tình huống | Hệ làm gì | Caller thấy |
|---|---|---|
| nhãn tiến độ đổi mỗi tương tác, `(3, 12)` | ghép `3`, `/`, `12` vào span trên stack, `SetText(span)` | "3/12", không cấp phát |
| cùng cặp số với lần trước | `SetText` cùng nội dung | UniText không dựng lại |
| số âm `(-4, 7)` | `TryFormat` ghi cả dấu âm | "-4/7" |
| `(int.MinValue, int.MinValue)` | 23 ký tự, chạm trần buffer | đủ hai số |
| ô chữ chưa kéo trong Inspector | `NullReferenceException` | lỗi setup lộ ở lần Play đầu |

## Flow

```
caller (nhãn tiến độ, dữ liệu đổi) ──SetFraction(curr, total)──► UniTextExtensionMethods.cs (cạnh SetNumber, CharmCount)
        stackalloc 23 ký tự → curr.TryFormat → '/' → total.TryFormat
                                        └──SetText(ReadOnlySpan<char>)──► UniTextBase
```

---

## GĐ1 — xong thì mọi nhãn tiến độ ghi "x/y" không cấp phát

```
caller ──SetFraction+──► UniTextExtensionMethods ──SetText(span)──► UniTextBase
```

`Runtime/Utilities/ExtensionMethods/UniTextExtensionMethods.cs` › sau `SetNumber`, trước `CharmCount` (file không chia region). Có sẵn quanh chỗ thêm:

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
            // Buffer thiếu thì TryFormat trả false, written = 0: chữ mất số im lặng.
            Span<char> chars = stackalloc char[23];
            curr.TryFormat(chars, out var currLength);
            chars[currLength] = '/';
            total.TryFormat(chars.Slice(currLength + 1), out var totalLength);
            // Ép kiểu như SetNumber: cùng overload SetText, cùng luật "cùng nội dung thì không dựng lại".
            self.SetText((ReadOnlySpan<char>)chars.Slice(0, currLength + 1 + totalLength));
        }
```

`Tests/EditMode/UniTextSetFractionTests.cs` (agent, cạnh `UniTextSetNumberTests`; `UniText` trên `GameObject` có `RectTransform`, đọc `RawText`, không cần font):

| Input | Kỳ vọng |
|---|---|
| `(0, 0)` · `(3, 12)` · `(12, 3)` · `(-4, 7)` | chữ nguồn bằng `$"{curr}/{total}"` |
| `(int.MinValue, int.MinValue)` · `(int.MaxValue, int.MaxValue)` | như trên — chạm trần buffer; đỏ khi buffer dưới 23 |
| đổi số sau hai lần ghi | `Is.Not.AllocatingGCMemory()` |
| đối chứng: `SetText($"{n}/20")` | `Is.AllocatingGCMemory()` — cùng phép đo bắt được chuỗi mới |

---

## Chuyển dự án (agent)

| Chỗ | Đổi |
|---|---|
| `docs/Runtime/CharmFlyArc/CharmFlyArc.md` › §3, bảng quyết định | thêm hàng `SetFraction(int, int)` ngay dưới `SetNumber(int)`: ghi "x/y" không cấp phát, cho nhãn tiến độ đổi mỗi tương tác |
| `docs/Editor/TmpToUniText.md` › hàng `SetText("{0}", n)` | thêm `SetFraction(int, int)` cho khuôn `"{0}/{1}"` |
| `Assets/_CategoryJam/Scripts/Utilities/UniTextFractionExtensions.cs` + `.meta` | xoá; giữ `using CategoryJam.Gameplay.Utilities` ở caller (namespace chung, còn type khác dùng) |
| `Assets/_CategoryJam/Tests/EditMode/UniTextFractionTests.cs` + `.meta` | xoá — bảng case đã ở `UniTextSetFractionTests` |
| `Scripts/Entities/Box/CategoryBox.cs`, `Scripts/Hud/PickedTilesCounter.cs` | thêm `using Horcrux.Runtime.Utilities.ExtensionMethods;` nếu chưa có |
| `Assets/_CategoryJam/Scripts/Feel.md` (nhãn số), `Assets/_CategoryJam/Structure.md` (cây `Utilities`, bảng bản tạm chờ Horcrux) | trỏ `UniTextExtensionMethods.SetFraction` của Horcrux, bỏ dòng bản tạm |
| Plan này | xoá cùng `.meta` |
| chơi thử | vào level, đặt tile vào box, nhặt tile → "đã lấp/tổng" trên box, "đã nhặt/tổng" trên HUD tăng đúng như trước; hỏng: nhãn trống, còn một số, thiếu `/` |

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | cache chuỗi theo cặp số | bảng lớn dần theo mọi cặp từng gặp, mỗi caller tự dựng một bảng; span trên stack không giữ gì |
| bỏ | kiểm `self` null | caller kéo ô trong Inspector, thiếu ô thì nổ ngay lần Play đầu |
| bỏ | đếm, tween số | hàm chỉ ghi chữ |
| bỏ | dấu cách quanh `/`, số thực | chưa có người dùng |
| sau | overload nhận `char` ngăn | khi có nhãn cần ký tự khác `/`; bản này không đổi |
