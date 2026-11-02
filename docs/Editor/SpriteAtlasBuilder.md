# SpriteAtlasBuilder — một thư mục icon thành một atlas v2, chạy lại ra cùng kết quả

Năm bước cho bộ icon chia theo thư mục (chủ đề, bộ sưu tập, gói): tìm ảnh theo thứ tự tự nhiên · chuẩn hoá importer từng ảnh · ghi
atlas v2 mỗi thư mục · áp cài đặt importer atlas cho mobile · xoá atlas của thư mục không còn chọn. Dự án quyết thư mục nào, atlas đặt
đâu, đăng ký Addressables ra sao (`AddressablesLayout`); Horcrux giữ từng bước.

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | class setup Editor của dự án, chạy từ menu hay `-executeMethod`, cho bộ icon nhiều thư mục con |
| Đầu vào | thư mục ảnh `.png` trong `Assets/`; danh sách `(thư mục, đường dẫn atlas)` do dự án lập |
| Ranh giới | không đọc danh sách chủ đề, không sinh asset metadata, không đăng ký Addressables — ba thứ đó bám schema dự án |
| Chỗ đặt | `Editor/SpriteAtlasBuilder/SpriteAtlasBuilder.cs`, assembly `com.horcrux.editor` |

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
