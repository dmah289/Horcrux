# JsonWireContract — kiểm wire format JSON của class dữ liệu trong test

Code: `Editor/WireContract/`.

Ba hàm thuần cho test EditMode của dự án, kiểm class dữ liệu mà remote config, nút CopyJson/ImportJson và save đọc–ghi bằng
`JsonConvert` mặc định: **thiếu khoá nào thì parse ném** · **khoá ghi ra đúng bảng cột đã chốt** · **serialize rồi parse lại ra
cùng JSON**. Hàm trả danh sách phát hiện, test của dự án tự `Assert`.

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | test EditMode của dự án cho mỗi DTO remote config hay save có cột đặt tên; tool `RemoteConfigDiff` dùng chung `CollectKeyPaths` |
| Ranh giới | chỉ đọc JSON và type; không log, không phụ thuộc NUnit, không đọc asset |
| Chỗ đặt | `Editor/WireContract/`, assembly `com.horcrux.editor`; test dự án thêm reference `com.horcrux.editor` |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| Parse bằng `JsonConvert` mặc định, không nhận settings | đúng cửa `RemoteConfig<T>` và CopyJson/ImportJson dùng; setting của serializer là một phần của format |
| `RequiredKeyViolations` báo cả khoá type ghi ra mà mẫu thiếu | khoá không có trong mẫu thì không bao giờ bị gỡ thử, phép kiểm xanh mà không kiểm nó |
| Đường khoá gộp phần tử mảng dưới `[]` | đường mô tả hình dạng cột, giống bảng cột trong spec; gỡ một đường là gỡ khoá đó ở mọi phần tử |
| Mẫu không parse được thì ném, không trả dòng | mẫu sai là lỗi của test, lộ ngay lần chạy đầu |

## API

| Hàm | Trả | Dùng khi |
|---|---|---|
| `RequiredKeyViolations<T>(string sampleJson)` | mỗi đường khoá không bắt buộc một dòng: `x: parses without it`, hoặc `x: the type writes it, the sample lacks it`; rỗng là đạt | DTO khai `[JsonObject(ItemRequired = Required.Always)]`: console thiếu khoá phải bị từ chối, không đọc ra 0 |
| `KeyPathsOf(object value)` | đường khoá giá trị ghi ra, sắp xếp: `a`, `rows`, `rows[].x` | so với bảng cột đã chốt, để đổi tên hay thêm field là việc cố ý |
| `RoundTripDiff<T>(T value)` | `null` khi serialize → parse → serialize ra cùng JSON, không thì `json1 -> json2` | giá trị asset qua CopyJson rồi ImportJson không đổi |
| `CollectKeyPaths(JToken, ISet<string>)` | thêm đường khoá vào tập caller đưa | caller cần chọn comparer (RemoteConfigDiff so không phân biệt hoa thường) |

```csharp
[Test]
public void ConfigJson_EveryKeyRequired()
    => CollectionAssert.IsEmpty(JsonWireContract.RequiredKeyViolations<MyConfig>("{\"a\":1,\"rows\":[{\"x\":1}]}"));

[Test]
public void ConfigWireColumns_MatchSpec()
    => CollectionAssert.AreEquivalent(new[] { "a", "rows", "rows[].x" }, JsonWireContract.KeyPathsOf(new MyConfig { rows = { new MyRow() } }));
```

`KeyPathsOf` không thấy cột trong phần tử của list rỗng: điền một phần tử khi muốn so cột của hàng. DTO đòi đủ khoá thì
`JsonConvert` cũng từ chối **ghi** ô `null` (ném `JsonSerializationException`): truyền giá trị đã điền, hoặc dữ liệu asset.

## Trước khi chạy

1. `.asmdef` test của dự án: thêm `com.horcrux.editor` vào **Assembly Definition References**, và `Newtonsoft.Json.dll` vào
   **Assembly References** khi `Override References` bật. Thiếu: không compile được `using Horcrux.Editor.WireContract`.

## Nghiệm thu

`Tests/EditMode/JsonWireContractTests.cs`:

| Ca | Kỳ vọng |
|---|---|
| mọi tầng đòi đủ khoá | rỗng |
| gốc đòi đủ khoá, hàng không | `rows[].x: parses without it` |
| mảng ở gốc | `[].x`; hàng đòi đủ khoá thì rỗng |
| mẫu thiếu khoá `b` của type không đòi khoá | `b: the type writes it, the sample lacks it`, rồi `a: parses without it` |
| mẫu không parse được | ném `JsonSerializationException` |
| `KeyPathsOf` list hai phần tử | `a`, `rows`, `rows[].x` |
| round trip ổn định · setter nhân đôi | `null` · `{"v":2} -> {"v":4}` |
