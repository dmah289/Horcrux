---
name: build-size
description: Dùng khi developer hỏi vì sao build nặng, muốn giảm size, hoặc so build trước–sau. Đo bằng công cụ dựng sẵn trong Horcrux trên gói build và báo cáo Unity sinh sẵn, không đoán từ cỡ thư mục Assets.
---

# build-size — tìm byte nằm ở đâu trước khi gỡ

Skill dưới `Agent/horcrux.md` (NT, tầng, ký hiệu định nghĩa ở đó).
`<horcrux>` là gốc Horcrux trong dự án. Cờ, mục, cách đọc: `docs/Editor/BuildSize.md`. Công cụ hiện mổ APK Android;
platform khác thì mở rộng `apk_report.py` trước khi đo.

## Luật

- **Số của build là số trong gói build.** Không kết luận từ `du Assets/` hay số file: asset không tham chiếu không vào
  build, asset tham chiếu hai đường vào hai lần.
- **Quy nguồn trước, gỡ sau.** Mỗi nhóm byte chỉ ra cơ chế kéo nó vào (Resources, scene trong Build Settings, tham chiếu
  thẳng, addressable, implicit theo addressable, dependency Gradle) rồi mới bàn cách gỡ.
- **Cách gỡ bày thành phương án kèm MB ước tính và rủi ro**, chốt một. Thứ đổi hành vi hay thuộc sản phẩm (R8, stripping,
  Remote Addressables, gỡ SDK, ngôn ngữ, mạng quảng cáo, CDN) là quyết định của developer hoặc team khác.
- **Báo cáo bảng** *nhóm · MB · % · cơ chế kéo vào*; **nghiệm thu bằng hai bảng trước–sau** trên hai bản build.

## Chọn mục theo câu hỏi

| Câu hỏi | Mục | Cần | Quy nguồn tiếp ở |
|---|---|---|---|
| nhóm nào trong APK to nhất | `apk` | APK | mục tương ứng bên dưới |
| group Addressables nào to, bundle phân bố ra sao | `aa` | APK (+ `--layout` cho tên group thật) | group, implicit, trùng |
| bundle chứa gì, asset nào bị kéo theo, cái nào trùng | `layout` | `--layout` | |
| SDK Java nào chiếm dex | `dex` | APK | manifest, `mainTemplate.gradle` |
| player data (scene, Resources, tham chiếu thẳng) kéo asset nào | `log` | `--editor-log`, Editor.log cùng phiên build | grep GUID của asset trong `.unity`, `.prefab`, settings |
| cài đặt strip, codegen, minify, AAB | eval đọc `PlayerSettings.GetManagedStrippingLevel`, `GetIl2CppCodeGeneration`, `Android.minifyRelease`, `EditorUserBuildSettings.buildAppBundle` | Editor, không vào Play | |

Khi dự án dùng TMP: font `m_AtlasPopulationMode: 1` là Dynamic, kéo nguyên file font — đếm glyph so với ngôn ngữ game hỗ trợ.

## Lệnh

```bash
python -X utf8 -I "<horcrux>/Editor/BuildSize/Tools~/apk_report.py" <apk> --layout --editor-log --top 12
python -X utf8 -I "<horcrux>/Editor/BuildSize/Tools~/apk_report.py" <apk> --sections dex --top 40
```

`--sections` đúng mục đang hỏi, `--top` nhỏ; lệnh chạy 1–2 giây.
