# AssetVariantCheck — asset chọn theo tên từ ngoài build có nạp được không

Code: `Editor/AddressablesLayout/AssetVariantCheck.cs` (assembly `com.horcrux.editor`).

Khi tên asset cần nạp đến từ remote config hay save, game không kéo được `AssetReference`: nạp bằng address là chuỗi, và
chuỗi chỉ hỏng lúc chạy. Phép kiểm này chuyển các hỏng đó về lúc authoring. Hàm thuần: ghi lỗi vào danh sách của caller,
không log, không sửa settings; dự án gọi từ nút Kiểm tra của Inspector và từ test EditMode.

## Luật được kiểm

| Luật | Thiếu thì hỏng ở đâu |
|---|---|
| asset là entry Addressables | game không nạp được bản này |
| address = tên asset | giá trị gõ theo tên asset (console, cheat) không tìm thấy bản |
| entry mang label của tập | host lấy tập tên hợp lệ theo label: tên đúng vẫn bị coi là lạ, cheat không liệt kê |
| không entry khác trùng address | không biết nạp bản nào |
| mọi entry cùng label chung một group | khác group là khác bundle: phụ thuộc chung bị nhân bản; nhả bản cũ khi đổi bản unload thứ đang dùng |
| group Pack Together | như trên, trong cùng group |
| group bật Include GUID in Catalog | bản mặc định kéo bằng `AssetReference` nạp theo GUID, không giải được |

## API

| Hàm | Trả về | Ghi chú |
|---|---|---|
| `Validate(Object variant, string label, List<string> errors)` | — | mỗi luật hỏng một dòng; lỗi "khác group" lặp lại ở mỗi bản bị kiểm |
| `IsSelectable(string address, string label)` | `bool` | có entry mang address và label đó không — kiểm giá trị mặc định của remote config |
| `FindAll<T>(string folder)` | `List<T>` | mọi `T` trong thư mục, sắp theo tên |

## Dùng trong dự án

```csharp
foreach (var variant in AssetVariantCheck.FindAll<MyTable>(MyTable.VARIANTS_FOLDER))
    AssetVariantCheck.Validate(variant, MyTableHost.LABEL, errors);
Assert.IsTrue(AssetVariantCheck.IsSelectable(remoteConfig.TableName.Value, MyTableHost.LABEL));
```

Phạm vi lấy theo thư mục dự án quy ước, không theo kiểu toàn project: `FindAssets("t:T")` không giới hạn nhặt cả nháp,
fixture đóng băng, asset của người khác. Tool cho chọn asset ngoài thư mục đó thì cảnh báo ngay ở ô chọn.

Tạo group, entry, label cho đúng luật bằng `AddressablesLayout` (`docs/Editor/AddressablesLayout.md`); phép kiểm này không sửa gì.
