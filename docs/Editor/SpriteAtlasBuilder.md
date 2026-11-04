# SpriteAtlasBuilder — một thư mục ảnh thành một atlas v2, chạy lại ra cùng kết quả; soát atlas của màn UI

Hai class. `SpriteAtlasBuilder` dựng; `AtlasAudit` đọc: màn nào nạp atlas nào, texture nào còn lẻ, thứ gì bị chép vào nhiều bundle,
atlas pack ra mấy trang (mục [AtlasAudit](#atlasaudit)).

Năm bước cho bộ icon chia theo thư mục (chủ đề, bộ sưu tập, gói): tìm ảnh theo thứ tự tự nhiên · chuẩn hoá importer từng ảnh · ghi
atlas v2 mỗi thư mục · áp cài đặt importer atlas cho mobile · xoá atlas của thư mục không còn chọn. Dự án quyết thư mục nào, atlas đặt
đâu, đăng ký Addressables ra sao (`AddressablesLayout`); Horcrux giữ từng bước.

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | class setup Editor của dự án, chạy từ menu hay `-executeMethod`, cho bộ icon nhiều thư mục con; agent qua eval khi dựng atlas cho art UI |
| Đầu vào | thư mục ảnh `.png` trong `Assets/`; danh sách `(thư mục, đường dẫn atlas)` do dự án lập |
| Ranh giới | không đọc danh sách chủ đề, không sinh asset metadata, không đăng ký Addressables — ba thứ đó bám schema dự án |
| Chỗ đặt | `Editor/SpriteAtlasBuilder/SpriteAtlasBuilder.cs`, `AtlasAudit.cs`, assembly `com.horcrux.editor` |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Atlas v1 (`.spriteatlas`) | v2 tách packable khỏi importer nên ghi file không kéo theo reimport ảnh |
| Một atlas gom nhiều thư mục | người dùng đầu cần mỗi thư mục một bundle để một màn chỉ nạp thứ nó dùng; gom là `FolderAtlas` với thư mục cha |
| Spec cho importer ảnh (chỉ có `pixelsPerUnit`) | bộ bước chuẩn hoá còn lại giống nhau ở mọi bộ icon đã gặp; có dự án cần khác thì thêm struct, không đổi chữ ký |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| Xoá `m_SpriteSheet.m_Sprites` qua `SerializedObject` | ảnh từng là Sprite Mode Multiple giữ rect con cũ (kèm viền) trong `.meta` sau khi về Single; API importer không có cách xoá |
| Tắt `spriteGenerateFallbackPhysicsShape` | icon không có collider; shape sinh ra chỉ tốn import và `.meta` |
| Ghi atlas trong `StartAssetEditing`, cài importer ở lần `StartAssetEditing` thứ hai sau `Refresh` | importer của atlas chỉ tồn tại sau khi file được import lần đầu |
| `ApplyImportSpec` so từng giá trị trước khi ghi | `SaveAndReimport` cho atlas là pack lại cả atlas; không đổi thì không reimport |
| `RemoveOrphanAtlases` nhận `settings` có thể null | dự án không dùng Addressables vẫn dọn atlas được |

## Luồng dữ liệu

```
thư mục icon ──► FindTexturePaths(folder) ──► .png sắp NaturalCompare
            ──► NormalizeSpriteImporters(paths, ppu): Sprite · Single · ppu · không mipmap · alphaIsTransparency · xoá rect cũ · không physics shape
(thư mục, atlas) ──► WriteFolderAtlases: SpriteAtlasAsset packable = thư mục, Save, Refresh
atlas ──► ApplyImportSpec(paths, AtlasImportSpec.MobileIcons): includeInBuild · padding 4, không xoay, không tight, không dilation
                                                             · không mipmap, Bilinear, sRGB · Android, iPhone: 2048 ASTC_6x6
thư mục atlas ──► RemoveOrphanAtlases(dir, keep, settings): xoá .spriteatlasv2 ngoài keep + entry Addressables
```

## API

| Hàm | Trả về |
|---|---|
| `FindTexturePaths(folderPath, extension = ".png")` | `List<string>` theo thứ tự tự nhiên |
| `NormalizeSpriteImporters(texturePaths, pixelsPerUnit)` | số ảnh phải reimport |
| `WriteFolderAtlases(IReadOnlyList<FolderAtlas>)` | — |
| `ApplyImportSpec(atlasPaths, in AtlasImportSpec)` | số atlas phải reimport |
| `RemoveOrphanAtlases(atlasDir, keepAtlasPaths, settings)` | số atlas đã xoá |
| `NaturalCompare(a, b)` | so tên file không đuôi theo cụm số |
| `AtlasImportSpec.MobileIcons` | padding 4, 2048, ASTC_6x6, Android + iPhone |

## Dùng trong dự án

```csharp
var atlases = topics.Select(t => new FolderAtlas($"{IconsRoot}/{t}", $"{AtlasDir}/{t}.spriteatlasv2")).ToList();
foreach (var atlas in atlases)
    SpriteAtlasBuilder.NormalizeSpriteImporters(SpriteAtlasBuilder.FindTexturePaths(atlas.FolderPath), PixelsPerUnit);
SpriteAtlasBuilder.WriteFolderAtlases(atlases);
SpriteAtlasBuilder.ApplyImportSpec(atlases.Select(a => a.AtlasPath).ToList(), AtlasImportSpec.MobileIcons);
SpriteAtlasBuilder.RemoveOrphanAtlases(AtlasDir, atlases.Select(a => a.AtlasPath).ToHashSet(), settings);
```

Bước nặng (reimport hàng nghìn ảnh) bọc `EditorUtility.DisplayProgressBar` ở dự án.

## Đã sai một lần

- Kết luận icon "có viền lạ" là lỗi art: rect con của Sprite Mode Multiple còn trong `.meta` sau khi chuyển Single.
- Sắp tên file bằng `string.Compare`: "1 (10)" đứng trước "1 (2)", id trong bộ sưu tập lệch với thứ tự hoạ sĩ đánh.

## Nghiệm thu

Chạy hai lần liên tiếp trên cùng bộ icon: lần hai `NormalizeSpriteImporters` và `ApplyImportSpec` trả 0, `git status` không có
`.meta` đổi. Bỏ một thư mục khỏi danh sách rồi chạy: atlas của nó bị xoá kèm entry, các atlas khác không reimport.

## AtlasAudit

Chỉ đọc. Pack xem trước ghi vào cache `Library`, không đổi asset. Dùng trước khi chia atlas (art của màn đang nằm đâu) và sau khi
dựng (còn lẻ, còn trùng, mấy trang).

| Hàm | Trả về |
|---|---|
| `Usage(verbose, params units)` | mỗi đơn vị (prefab, scene, thư mục prefab): group Addressables; mỗi atlas `N tex (on a, off b, dep c)`; mỗi texture lẻ `- \| path WxH \| trạng thái`; mục `shared` atlas hay texture lẻ mà nhiều đơn vị nạp, `DUP` khi sẽ bị chép vào nhiều bundle |
| `Atlases(params paths)` | mỗi atlas (đường dẫn hay thư mục chứa atlas): group, cài đặt theo build target, packable, từng trang `WxH format fill %`, `BIG` sprite quá `BigSpriteShare` trang, `ALSO` texture nằm ở atlas khác |
| `Importers(folder)` | mỗi texture `path WxH \| atlas \| mip \| platform max format` theo build target hiện tại |
| `TextureAtlasMap()` | texture → các atlas (không tính variant) có packable phủ nó |

Trạng thái: `on` có component đang bật giữ texture · `off` chỉ object tắt hay component tắt giữ (vẫn nạp theo prefab) · `dep` đến qua
asset mà cây prefab tham chiếu (material, config, prefab khác, sprite asset TMP) hoặc từ scene (scene không mở để đọc cây).
Texture mà RawImage, material, sprite asset TMP giữ thẳng là texture nguồn, không qua atlas, dù dòng của nó tính vào atlas chứa nó:
bundle của bên giữ chép texture nguồn. `off` của skin cũ gỡ bằng `HiddenArt` (`docs/Editor/UIReskin.md`).

```csharp
// eval C# 6: mỗi màn một đơn vị, thứ mở chồng lên (popup) là đơn vị riêng
return Horcrux.Editor.SpriteAtlasBuilder.AtlasAudit.Usage(false, "Assets/Game/Scenes/Menu.unity",
    "Assets/Game/UI/PopupShop.prefab", "Assets/Game/UI/PopupSetting.prefab")
    + Horcrux.Editor.SpriteAtlasBuilder.AtlasAudit.Atlases("Assets/Game/Arts/UI");
```

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Tự chia atlas từ báo cáo | gom theo màn hay theo vòng đời bundle cần biết màn nào mở chồng lên màn nào; agent đọc báo cáo rồi chốt, `SpriteAtlasBuilder` dựng |
| Mở scene để đọc trạng thái bật tắt | đổi scene đang mở của Editor; scene chỉ có `dep` |
| Ước tính byte | format thật do platform và cài đặt dự án quyết; trang và fill đủ để so hai phương án, byte đo trên build (`BuildSize`) |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| Đường dẫn của sprite lấy bằng `AssetDatabase.GetAssetPath(sprite)` | `sprite.texture` của sprite đã pack trả trang atlas, đường dẫn là file `.spriteatlasv2` |
| Đơn vị không có group (`-`) không tính khi xét `DUP` | prefab lồng trong scene hay popup đi theo bundle của bên chứa nó |
| Texture nằm nhiều atlas thì tính atlas đầu | Unity chỉ dùng một bản khi vẽ; dòng `ALSO` của `Atlases` báo chỗ trùng để gỡ |
| `dep` của prefab không đi qua prefab gốc của variant, prefab lồng (`m_CorrespondingSourceObject`, `m_PrefabAsset`) | build nướng chúng vào prefab; `GetDependencies` đệ quy đi qua prefab gốc và báo cả art mà variant đã gỡ |

**Đã sai một lần**

- Dựng xong ba màn reskin mà không xếp art mới vào atlas: 60 texture lẻ, art dùng chung giữa hai group bị chép vào hai bundle,
  không ai thấy vì màn vẫn vẽ đúng. Chỉ số batch (21, 33, 39 → 10, 15, 12) và dòng `DUP` lộ ra.

**Nghiệm thu**: test `AtlasAuditTests` (texture trong atlas gom một dòng, texture lẻ giữ bởi object tắt báo `off`, atlas dùng
chung hiện ở `shared`, thư mục packable phủ texture con, pack ra một trang có fill) và `HiddenArtTests` (art variant đã gỡ không
còn tính qua prefab gốc). Trên dự án: batch trước–sau trên cùng màn, ảnh
chụp trước–sau sai khác ~0.
