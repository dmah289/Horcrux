---
name: build-size
description: Dùng khi developer hỏi vì sao build Android nặng, muốn giảm size, hoặc so build trước–sau. Đo bằng công cụ dựng sẵn trong Horcrux trên APK và báo cáo Unity sinh sẵn, không đoán từ cỡ thư mục Assets.
---

# build-size — tìm byte nằm ở đâu trước khi gỡ

Đường dẫn tính từ gốc Horcrux (dự án hiện tại: `Assets/Horcrux/`). Chi tiết ở `Editor/BuildSize/BuildSize.md`.

## Luật

1. **Số của build là số trong APK.** Không kết luận từ `du Assets/` hay số file trong thư mục: asset không tham chiếu không vào build,
   asset tham chiếu hai đường vào hai lần.
2. **Quy nguồn trước, gỡ sau.** Mỗi nhóm byte phải chỉ ra được cơ chế kéo nó vào (Resources, scene trong Build Settings, tham chiếu
   thẳng, addressable, implicit theo addressable, dependency Gradle) rồi mới bàn cách gỡ.
3. **Cách gỡ bày thành phương án kèm số và giá**, chốt một; thứ đổi hành vi (R8, stripping, Remote Addressables, gỡ adapter ads) là
   quyết định của developer hoặc team khác.
4. **Không viết script mổ APK mới.** Thiếu mục thì mở rộng `apk_report.py`.

## Chọn mục theo câu hỏi

| Câu hỏi | Mục | Cần |
|---|---|---|
| Nhóm nào trong APK to nhất | `apk` | APK |
| Group Addressables nào to, bundle phân bố ra sao | `aa` | APK (+ `--layout` để ra tên group thật) |
| Bundle chứa asset gì, asset nào bị kéo theo, cái nào trùng | `layout` | `--layout` |
| SDK Java nào chiếm dex | `dex` | APK |
| Player data (scene, Resources, tham chiếu thẳng) kéo asset nào | `log` | `--editor-log`, Editor.log cùng phiên build |

## Lệnh

```bash
python -X utf8 -I "<horcrux>/Editor/BuildSize/Tools~/apk_report.py" <apk> --layout --editor-log --top 12
python -X utf8 -I "<horcrux>/Editor/BuildSize/Tools~/apk_report.py" <apk> --sections dex --top 40
```

Cài đặt ảnh hưởng size đọc một lần qua eval, không vào Play:

```csharp
return $"strip={UnityEditor.PlayerSettings.GetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.Android)} " +
       $"codegen={UnityEditor.PlayerSettings.GetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.Android)} " +
       $"minify={UnityEditor.PlayerSettings.Android.minifyRelease} aab={UnityEditor.EditorUserBuildSettings.buildAppBundle}";
```

## Quy trình

1. Chạy lệnh đầy đủ một lần; đọc mục `apk` để xếp hạng nhóm.
2. Với mỗi nhóm lớn, quy nguồn: `dex` → manifest và `mainTemplate.gradle` · `aa`/`layout` → group, implicit, trùng · `log` → Resources,
   scene, tham chiếu thẳng (grep GUID của asset trong `.unity`, `.prefab`, `TMP Settings.asset`).
3. Font TMP: `m_AtlasPopulationMode: 1` là Dynamic, kéo nguyên file font; đếm glyph của font so với ngôn ngữ game hỗ trợ.
4. Báo cáo theo bảng nhóm · MB · % · cơ chế kéo vào; phương án gỡ kèm MB ước tính và rủi ro; hỏi developer thứ thuộc sản phẩm (ngôn
   ngữ, mạng ads, CDN).
5. Sau khi sửa: build lại, chạy lại lệnh, so hai bảng.

## Ngân sách token

- `--sections` đúng mục đang hỏi, `--top` nhỏ; không đổ cả buildlayout JSON hay Editor.log vào ngữ cảnh.
- Lệnh chạy 1–2 giây, chạy tiền cảnh.
