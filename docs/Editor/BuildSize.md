# BuildSize — byte của build Android nằm ở đâu

Một lệnh đọc APK và hai báo cáo Unity sinh sẵn, in ra bảng để trả lời *vì sao build nặng* mà không mở Editor: nhóm nào trong APK
chiếm bao nhiêu, bundle Addressables nào chứa asset gì, Java dex thuộc SDK nào, player data kéo asset nào vào. Cỡ `du` trên thư
mục `Assets/` không phải số của build: asset không được tham chiếu không vào build, còn một asset có thể vào hai lần.

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | agent, khi developer hỏi build size; quy trình ở `Skills/build-size/SKILL.md` |
| Đầu vào | APK đã build · `Library/com.unity.addressables/BuildReports/buildlayout_*.json` (Addressables ghi sau mỗi lần build content) · `%LOCALAPPDATA%/Unity/Editor/Editor.log` (khối `Build Report` sau mỗi lần build player) |
| Ranh giới | tool chỉ **đo và quy nguồn**; kết luận gỡ cái gì là việc đọc code, asset, manifest sau đó. AAB chưa hỗ trợ: đổi cấu trúc thư mục, chưa có nhu cầu |
| Chỗ đặt | `Editor/BuildSize/Tools~/apk_report.py`: thư mục đuôi `~` Unity không import, không vào build. Không có C# |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Đọc thư mục Gradle để quy dex về từng thư viện | Gradle chỉ để lại dex đã gộp; parse `classes*.dex` trong APK quy theo package cho cùng câu trả lời và không cần build cache |
| Mở bundle ra đọc | build layout JSON đã ghi từng asset trong từng bundle kèm cỡ, và phân biệt explicit với implicit |
| Lấy số từ `du Assets/` | không nói asset nào vào build; đã dẫn tới nghi oan gameplay trong khi phần lớn nhất là dex |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| Mục `layout` bỏ asset dưới 8 KB | icon lẻ trong một atlas là hàng trăm dòng 1 KB, không đổi kết luận về bundle |
| Cỡ dex là **xấp xỉ** (header class, header method, `insns_size × 2`) | đủ để xếp hạng SDK với nhau; cỡ nén thật của dex lấy ở mục `apk` |
| Mục `aa` không có `--layout` thì gom theo tiền tố tên file | đủ cho group đặt tên thường; bundle built-in của Addressables hiện tên hash, có layout thì ra tên group thật |

## Luồng dữ liệu

```
APK (zip) ──────────────────┬─ apk: nhóm top-level, assets/bin/Data, entry lớn nhất
                            ├─ aa:  bundle theo group (nén, số bundle, nhỏ nhất · trung vị · lớn nhất)
                            └─ dex: class_def → package → byte mã
buildlayout JSON ── resolve {rid} qua references.RefIds ── layout: group → bundle → asset explicit|implicit, asset implicit trùng
Editor.log ── khối "Build Report" cuối ── log: thể loại, asset lớn nhất của player data
```

## Lệnh

```bash
python -X utf8 -I "<horcrux>/Editor/BuildSize/Tools~/apk_report.py" <apk> [--layout [json]] [--editor-log [log]] [--sections apk,aa,dex,layout,log] [--top N]
```

| Cờ | Nghĩa |
|---|---|
| `--layout` không kèm đường dẫn | file `buildlayout_*.json` mới nhất, tính từ thư mục hiện tại là gốc dự án |
| `--editor-log` không kèm đường dẫn | `%LOCALAPPDATA%/Unity/Editor/Editor.log` |
| `--sections` | in đúng mục đang hỏi; mặc định in mọi mục có đầu vào. Gọi tên `layout` hay `log` mà thiếu đầu vào thì lỗi, không im lặng |
| `--top` | số dòng mỗi bảng, mặc định 20 |

Chạy 1–2 giây cho APK 140 MB. Mọi cỡ là **nén** trừ khi bảng ghi rõ.

## Đọc kết quả

| Thấy gì | Nghĩa là | Soi tiếp ở đâu |
|---|---|---|
| `classes*.dex` lớn hơn phần game | SDK Java (ads mediation, Firebase) | mục `dex`: package nào; `Packages/manifest.json`, `mainTemplate.gradle`; R8 minify trong Player Settings |
| `libil2cpp.so` lớn | code C# và generic | stripping level, IL2CPP code generation trong Player Settings |
| `data.unity3d` lớn | scene trong Build Settings, `Resources/`, asset tham chiếu thẳng | mục `log`: asset lớn nhất; một asset vừa ở đây vừa ở bundle là đóng hai lần |
| một bundle lớn với asset **implicit** to | asset không addressable bị kéo theo | làm addressable trong group dùng chung, hoặc bỏ tham chiếu |
| asset implicit trùng ở nhiều bundle | mỗi bundle giữ một bản | làm addressable để chỉ đóng một lần |
| `.TTF` nhiều MB implicit cạnh một `.asset` TMP | font Dynamic kéo nguyên file font vào build | subset font theo ngôn ngữ, hoặc chuyển Static |
| font TMP bật Clear Dynamic Data On Build vẫn ~1 MB trong bundle | cờ đó chạy ở build preprocessor của player, build content Addressables không gọi | chấp nhận (atlas thưa nén LZ4 còn vài trăm KB), hoặc Clear Dynamic Data bằng tay trước khi build content |

## Đã sai một lần

- Đọc buildlayout JSON như cây lồng nhau thấy bundle nào cũng rỗng: mọi object con ghi là `{"rid": n}`, phải tra `references.RefIds`.
- Cộng `du` từng thư mục `Assets/` để tìm thủ phạm: thư mục 115 MB chỉ đóng 20 MB vào build, còn một font 13 MB vào hai lần.

## Nghiệm thu

Chạy trên APK có Addressables và Editor.log cùng phiên build: tổng nén của mục `apk` bằng cỡ file trên đĩa sai số dưới 1 %; tổng mục
`aa` bằng dòng `assets/aa` của mục `apk`; số bundle của mục `aa` bằng số bundle của mục `layout`; một asset có ở mục `layout` implicit
và ở mục `log` là bằng chứng đóng hai lần.
