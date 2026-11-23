# Plan — `SetFraction`: ghi "x/y" lên UniText không tạo chuỗi

Ngày 2026-11-23. Nguồn: category-jam `Assets/_CategoryJam/Scripts/Utilities/UniTextFractionExtensions.cs` (`SetFraction`),
`Tests/EditMode/UniTextFractionTests.cs`, đã chạy trong build. Một extension cạnh `SetNumber` trong `UniTextExtensionMethods`: ghép
hai số và dấu `/` vào span trên stack rồi ghi span vào chữ.

**Phân công:** GĐ1 developer gõ, agent viết test theo bảng case rồi compile · GĐ2 (tài liệu) · GĐ3 (dự án chuyển sang bản Horcrux,
xoá bản tạm, xoá Plan) agent làm.
**Chạy được, thấy được:** GĐ1 compile sạch + bảng case xanh trong `Tests/EditMode/UniTextSetFractionTests.cs`; thấy trong game ở GĐ3.
**Lệnh kiểm** (chạy từ gốc dự án): `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,Horcrux.EditModeTests.csproj --tests Horcrux.EditModeTests`.
**Commit:** GĐ1–2 trong `Assets/Horcrux`; GĐ3 ở dự án kèm con trỏ submodule.
**Phụ thuộc:** không. **Chung file:** category-jam `CategoryBox.cs` — GĐ chuyển dự án của `Horcrux_SpriteFill_Plan.md` cũng sửa
dòng `using` của file này; gõ cả hai thì gộp hai GĐ chuyển dự án vào một commit dự án.
**Đã kiểm trước khi ghi:** `UniTextExtensionMethods.cs` thật cộng đoạn GĐ1 qua `csc -langversion:9` trên `LightSide.UniText.dll`,
`UnityEngine.UI.dll`, `PrimeTween.Runtime.dll` và `UnityEngine.CoreModule` của Editor 6000.0.73f1. Thân hàm trùng nguyên văn bản tạm,
bản tạm qua 6 case chữ và 2 case cấp phát (có đối chứng) của `UniTextFractionTests`. `§0`: không — không có toán.

## Ngữ cảnh đã chốt

- **Người dùng:** nhãn tiến độ "x/y" đổi mỗi tương tác — ô đã lấp, vật đã nhặt, câu đã trả lời, quái đã hạ trong đợt.
- **Mục tiêu:** ghi "x/y" lên mọi `UniTextBase` không cấp phát; cùng cặp số với lần trước thì UniText không dựng lại.
- **Ranh giới:** chỉ ghi chữ; không đếm, không tween số.
- **Chỗ đặt, tên:** `Runtime/Utilities/ExtensionMethods/UniTextExtensionMethods.cs`, ngay sau `SetNumber`, cùng namespace
  `Horcrux.Runtime.Utilities.ExtensionMethods`.
- **Cố ý không làm:** ký tự ngăn khác `/`, dấu cách quanh nó — chưa có người dùng · số thực — chưa có người dùng.
- **Quyết định trái trực giác:** không cache chuỗi theo cặp số. Bảng lớn dần theo mọi cặp từng gặp, và mỗi caller tự dựng một bảng (dự
  án nguồn đã có ba bản cho số và cặp số). Span trên stack không giữ gì, không có bảng để quên dọn.
- **Mở rộng sau:** overload nhận `char` ngăn khi có nhãn cần ký tự khác `/`; bản này không đổi.

## Đã khảo sát

| Nguồn | Lấy | Không lấy, vì sao |
|---|---|---|
| category-jam `Scripts/Utilities/UniTextFractionExtensions.cs` (bản tạm) | thân `SetFraction` nguyên văn | namespace dự án |
| category-jam `Tests/EditMode/UniTextFractionTests.cs` | 6 case chữ, case không cấp phát, case đối chứng | — |
| Horcrux `UniTextExtensionMethods.SetNumber` | khuôn span trên stack + ép `(ReadOnlySpan<char>)` | gọi hai lần không ghép được: mỗi lần ghi đè cả chữ |
| Horcrux `UniTextExtensionMethods.CharmCount` | — | khuôn `{0}` qua `StringBuilder` sống trong closure của tween; một số, không hợp cho lần ghi rời |
| UniText `UniTextBase.SetText(ReadOnlySpan<char>)` | ghi span không tạo chuỗi; cùng nội dung với lần trước thì không dựng lại | — |

## Luồng

```
Dữ liệu đổi (curr, total):
    label.SetFraction(curr, total)
        └─ stackalloc 23 ký tự → curr.TryFormat → '/' → total.TryFormat → SetText(span)
```

## Bản đồ hệ cuối

```
caller (nhãn tiến độ) ──SetFraction(curr, total)──► UniTextExtensionMethods ──SetText(ReadOnlySpan<char>)──► UniTextBase
                                                    (cạnh SetNumber, CharmCount)
```

## Giai đoạn 1 — `SetFraction`

**Mục tiêu:** ghi "x/y" lên mọi `UniTextBase` không cấp phát. **Vì sao bây giờ:** là toàn bộ hệ.

**Sơ đồ sau GĐ1** (`+` là phần mới)

```
caller ──SetFraction+──► UniTextExtensionMethods ──SetText(span)──► UniTextBase
```

### `UniTextExtensionMethods.cs` › sau `SetNumber`, trước `CharmCount` (file không chia region)

Có sẵn quanh chỗ thêm:

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

**Consumes / Produces:** consumes `int.TryFormat(Span<char>, …)`, `UniTextBase.SetText(ReadOnlySpan<char>)` · produces
`SetFraction(this UniTextBase, int, int)`.

**Quyết định:**
- 23 = 11 + 1 + 11: `int.MinValue` là 11 ký tự. Buffer thiếu thì `TryFormat` trả `false`, `written` = 0 và chữ mất số im lặng.
- Ép `(ReadOnlySpan<char>)` như `SetNumber`: hai extension đi cùng một overload `SetText`, cùng luật "cùng nội dung thì không dựng lại".
- Không kiểm `self` null: caller kéo ô trong Inspector, thiếu ô nổ ngay lần Play đầu.

**Editor setup:** không.

**Kiểm:** lệnh kiểm ở đầu Plan. Agent viết `Tests/EditMode/UniTextSetFractionTests.cs` cạnh `UniTextSetNumberTests` (chữ `UniText`
trên `GameObject` có `RectTransform`, đọc `RawText`; không cần font):

| Input | Kỳ vọng |
|---|---|
| `(0, 0)` · `(3, 12)` · `(12, 3)` · `(-4, 7)` | chữ nguồn bằng `$"{curr}/{total}"` |
| `(int.MinValue, int.MinValue)` · `(int.MaxValue, int.MaxValue)` | như trên — chạm trần buffer; đỏ khi buffer dưới 23 |
| đổi số sau hai lần ghi | `Is.Not.AllocatingGCMemory()` |
| đối chứng: `SetText($"{n}/20")` | `Is.AllocatingGCMemory()` — cùng phép đo bắt được chuỗi mới |

**Mốc commit:** `feat(unitext): SetFraction ghi "x/y" không tạo chuỗi` (Horcrux).

---

## Giai đoạn 2 — Tài liệu (agent)

- `docs/Runtime/CharmFlyArc/CharmFlyArc.md` › §3, bảng quyết định: thêm hàng `SetFraction(int, int)` cùng file ngay dưới hàng
  `SetNumber(int)` — ghi "x/y" không cấp phát, cho nhãn tiến độ đổi mỗi tương tác.
- `docs/Editor/TmpToUniText.md` › hàng `SetText("{0}", n)`: thêm `SetFraction(int, int)` cho khuôn `"{0}/{1}"`.

**Mốc commit:** `docs(unitext): SetFraction` (Horcrux).

---

## Giai đoạn 3 — category-jam dùng bản Horcrux, xoá bản tạm, xoá Plan (agent)

**Mục tiêu:** hai caller gọi bản Horcrux, bản tạm và test của nó biến mất.

| File | Việc |
|---|---|
| `Assets/_CategoryJam/Scripts/Utilities/UniTextFractionExtensions.cs` + `.meta` | xoá — hết hai extension cùng tên, nên không cần bỏ `using CategoryJam.Gameplay.Utilities` (namespace chung của dự án, file còn dùng type khác trong đó) |
| `Assets/_CategoryJam/Tests/EditMode/UniTextFractionTests.cs` + `.meta` | xoá — bảng case đã ở `UniTextSetFractionTests` của Horcrux từ GĐ1 |
| `Assets/_CategoryJam/Scripts/Entities/Box/CategoryBox.cs` | thêm `using Horcrux.Runtime.Utilities.ExtensionMethods;` nếu chưa có |
| `Assets/_CategoryJam/Scripts/Hud/PickedTilesCounter.cs` | như trên |
| `Assets/_CategoryJam/Scripts/Feel.md` (nhãn số), `Assets/_CategoryJam/Structure.md` (cây `Utilities`, bảng bản tạm chờ Horcrux) | trỏ `UniTextExtensionMethods.SetFraction` của Horcrux, bỏ dòng bản tạm |
| `Assets/Horcrux/Plans/Horcrux_UniTextSetFraction_Plan.md` + `.meta` | xoá |

`CategoryBox.cs` cũng đổi trong GĐ chuyển dự án của `Horcrux_SpriteFill_Plan.md`: gõ cả hai thì làm chung một commit.

**Editor setup:** không — extension static, không prefab nào trỏ.

**Kiểm:** `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj CategoryJam.Gameplay.csproj,CategoryJam.EditModeTests.csproj --tests CategoryJam.EditModeTests,Horcrux.EditModeTests`
xanh · `grep -rn UniTextFractionExtensions Assets/_CategoryJam` rỗng.

**Chơi thử (developer):** vào một level · đặt tile vào box và nhặt tile · nhìn bộ đếm "đã lấp/tổng" trên box và "đã nhặt/tổng" trên
HUD · số tăng đúng mỗi lần như trước, không khác gì bản cũ · dấu hiệu hỏng: nhãn trống, chỉ còn một số, thiếu dấu `/`.

**Mốc commit:** `refactor(unitext): SetFraction dùng bản Horcrux` (dự án, kèm con trỏ submodule).

---

## File → GĐ

| File | GĐ |
|---|---|
| `Runtime/Utilities/ExtensionMethods/UniTextExtensionMethods.cs` | 1 |
| `Tests/EditMode/UniTextSetFractionTests.cs` (agent) | 1 |
| `docs/Runtime/CharmFlyArc/CharmFlyArc.md`, `docs/Editor/TmpToUniText.md` | 2 |
| category-jam `UniTextFractionExtensions.cs` (xoá), `UniTextFractionTests.cs` (xoá), `CategoryBox.cs`, `PickedTilesCounter.cs`, `Feel.md`, `Structure.md`; Plan này (xoá) | 3 |
