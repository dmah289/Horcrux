# AddressablesLayout — bố trí Addressables khai bằng bảng tĩnh, áp lại bao nhiêu lần cũng ra một kết quả

Bốn hàm lõi để dự án khai *group nào, packing gì, asset nào ở address nào với label nào* trong một bảng tĩnh rồi áp bằng một nút
hay một lệnh headless. Mỗi hàm là idempotent: group có rồi thì chỉnh schema, entry có rồi thì dời về đúng group và đặt lại address,
label thừa gỡ. Dự án giữ bảng và nút; Horcrux giữ cơ chế.

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | class Editor của dự án có `[MenuItem]` và hàm `static` gọi được bằng `-executeMethod`; mỗi dự án một bảng `(path, address, labels)` |
| Đầu vào | `AddressableAssetSettingsDefaultObject.GetSettings(false)` của dự án; đường dẫn asset trong `Assets/` |
| Ranh giới | không build content, không chạm `BuildPath` ngoài local, không quyết group nào tồn tại — bảng của dự án quyết |
| Chỗ đặt | `Editor/AddressablesLayout/AddressablesLayout.cs`, assembly `com.horcrux.editor` (tham chiếu `Unity.Addressables.Editor`) |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Kiểu `LayoutSpec` khai cả bố trí rồi `Apply(spec)` | ba người dùng đầu có ba hình bảng khác nhau (có label, không label, popup theo thư mục); một spec chung mọc cờ theo từng dự án. Dự án ghép bốn hàm trong 20 dòng |
| Remote build path, content update | chưa có dự án dùng remote; thêm sau là tham số của `EnsureGroup` |
| `BuildContent()` | một dòng gọi `AddressableAssetSettings.BuildPlayerContent`; dự án cần headless build giữ ở class của nó |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| Hai overload `EnsureEntry`, một không chạm label | người gọi không nói gì về label thì không được gỡ label người khác đặt; overload có `labels` mới là "tập label đúng bằng thế này" |
| Asset thiếu ghi vào `errors` thay vì ném ngay | một lần áp đi hết bảng rồi báo mọi đường dẫn sai một lượt; người gọi ném ở cuối nếu muốn headless dừng |
| `Save` là hàm riêng, gọi một lần cuối | mỗi `SetDirty` + `SaveAssets` giữa chừng ghi đĩa lặp cho cùng settings |

## Luồng dữ liệu

```
bảng tĩnh của dự án ──► EnsureGroup(settings, name, packing[, compression]) ──► group với BundledAssetGroupSchema (local build/load path, IncludeInBuild)
                    ──► EnsureEntry(settings, group, assetPath, address[, labels], errors) ──► entry CreateOrMoveEntry + SetAddress [+ label đúng tập]
                    ──► RemoveEntriesByAddress(settings, address) ──► gỡ address không còn code nào load
                    ──► Save(settings) ──► SetDirty(BatchModification) + AssetDatabase.SaveAssets
errors[] ──► người gọi in hoặc ném (headless dừng với mã lỗi)
```

## API

| Hàm | Trả về | Ghi chú |
|---|---|---|
| `EnsureGroup(settings, name, packing, compression = LZ4)` | `AddressableAssetGroup` | tạo kèm `BundledAssetGroupSchema` và `ContentUpdateGroupSchema`; luôn đặt local path |
| `EnsureEntry(settings, group, assetPath, address, errors)` | `bool` | label giữ nguyên |
| `EnsureEntry(settings, group, assetPath, address, labels, errors)` | `bool` | `settings.AddLabel` cho label chưa có; tập label của entry thành đúng `labels` |
| `RemoveEntriesByAddress(settings, address)` | số entry đã gỡ | quét mọi group |
| `Save(settings)` | — | gọi một lần sau cả đợt |

## Dùng trong dự án

Một class Editor của dự án, bảng `static readonly` và một `[MenuItem]`:

```csharp
var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
var errors = new List<string>();
var share = AddressablesLayout.EnsureGroup(settings, "ShareAsset", BundledAssetGroupSchema.BundlePackingMode.PackTogether);
foreach (var (path, address) in ShareEntries)
    AddressablesLayout.EnsureEntry(settings, share, path, address, new[] { "share_ui" }, errors);
AddressablesLayout.RemoveEntriesByAddress(settings, "OldAddress");
AddressablesLayout.Save(settings);
if (errors.Count > 0) throw new InvalidOperationException("[Layout] " + string.Join("; ", errors));
```

Headless: `Unity.exe -batchmode -quit -projectPath <p> -executeMethod <Namespace.Class.Apply>`.

## Đã sai một lần

- Address không còn code nào load vẫn nằm trong group: art của nó đóng hai lần, một trong bundle scene và một trong bundle group.
  Bảng của dự án có mục address đã gỡ, áp qua `RemoveEntriesByAddress`.
- Ba bản chép cùng ba hàm trong một dự án (layout UI, generator level, setup atlas) lệch nhau ở chỗ có đặt `IncludeInBuild` và
  compression hay không; group tạo bởi bản thiếu đi theo mặc định của Addressables.

## Nghiệm thu

Áp hai lần liên tiếp: lần hai `git diff` trên `AddressableAssetsData/` rỗng. Đổi packing trong bảng rồi áp: chỉ dòng `m_BundleMode`
của group đó đổi. Xoá một dòng khỏi bảng không gỡ entry: entry là của dự án tới khi bảng nói gỡ qua `RemoveEntriesByAddress`.
