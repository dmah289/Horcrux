Code: `Editor/UnityPackageAudit/Tools~/unitypackage_audit.py`

# UnityPackageAudit — soát .unitypackage trước khi import

Đọc một `.unitypackage` (gói hiệu ứng của hoạ sĩ, asset store, gói xuất từ dự án khác) khi chưa import. Tool in ra hai phần:
- những file prefab, scene gốc cần kéo theo, tức đúng các dòng phải tick;
- phần còn lại so với dự án: giống, khác bản hay mới.

Mục đích là import đúng phần cần, và không để runtime, plugin đóng kèm đè bản dự án đang chạy.

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | agent soát gói rồi viết hướng dẫn import cho developer; developer chạy khi nhận gói mới |
| Đầu vào | file `.unitypackage` và thư mục gốc dự án (chứa `Assets/`) |
| Ranh giới | chỉ đọc và báo. Không import, không giải nén ra đĩa, không sửa dự án. Tick trong hộp thoại import do người dùng làm |
| Chỗ đặt | Python ở `Editor/UnityPackageAudit/Tools~/`, Unity không import, không vào build |
| Phụ thuộc | thư viện chuẩn của Python; chạy được bằng `-I` |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Tự import phần cần | hộp thoại import của Unity là chỗ người dùng thấy và quyết; tool chỉ chỉ dòng nào |
| Đọc tham chiếu của asset nhị phân | asset lưu dạng nhị phân không đọc được bằng chuỗi `guid:`; tool báo `nhị phân` để người dùng biết danh sách có thể thiếu |
| Diff nội dung file khác bản | đủ biết là khác để không import đè; cần diff thì giải nén file đó ra thư mục riêng rồi so |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| Đọc tar trong bộ nhớ, không giải nén | gói là dữ liệu ngoài; không ghi gì ra đĩa thì không có đường dẫn lạ nào chạy được |
| So nội dung bỏ khác biệt `\r\n` | cùng file qua hai máy khác kiểu xuống dòng; khác mỗi xuống dòng mà báo khác bản là báo động giả |
| Mục trùng guid so nội dung với file dự án dù dự án đã dời nó đi chỗ khác | Unity import theo guid: asset đã có guid đó nhận nội dung gói tại chỗ của nó trong dự án, không theo đường dẫn trong gói |
| Gốc mặc định là mọi prefab, scene trong gói | gói hiệu ứng, UI thường dùng qua prefab; gói chỉ có material, script thì đặt `--root` |

## Luồng dữ liệu

```
.unitypackage (tar.gz) ──► mỗi guid: pathname · asset · asset.meta
                           └─► tham chiếu = mọi "guid: …" trong asset YAML và trong .meta (importer trỏ asset khác)
dự án/Assets, Packages ──► mọi .meta → guid → đường dẫn
                           │
gốc (--root hoặc mọi .prefab, .unity) ──► đi theo tham chiếu trong gói → tập cần cho gốc (+ thư mục cha)
                           │             tham chiếu ra ngoài gói → dự án đã có · dựng sẵn của Unity · THIẾU
mỗi mục ──► so guid, đường dẫn, nội dung với dự án → mới · giống · khác bản · trùng đường dẫn khác guid
in: cần cho gốc (từng file) · ngoài gói · không cần cho gốc (gom theo hai cấp thư mục) · file dự án bị chạm nếu import cả gói
```

## Lệnh

```bash
T="<horcrux>/Editor/UnityPackageAudit/Tools~/unitypackage_audit.py"
python -X utf8 -I "$T" <gói.unitypackage> --project <gốc dự án> [--root <chuỗi> …] [--list]
python -X utf8 -I "$T" selftest
```

| Cờ | Nghĩa |
|---|---|
| `--project` | thư mục gốc dự án. Bỏ trống thì chỉ liệt kê gói, mọi mục coi là mới |
| `--root` | chuỗi nằm trong đường dẫn của gốc cần dùng, không phân biệt hoa thường; lặp cờ cho nhiều gốc |
| `--list` | in mọi file không cần cho gốc và mọi file dự án bị chạm, không cắt ở 15 dòng |

Vài giây cho gói vài trăm mục và dự án khoảng 8 000 asset.

## Đọc kết quả

| Nhãn | Nghĩa | Khi import |
|---|---|---|
| `mới` | guid và đường dẫn chưa có trong dự án | tick nếu thuộc phần cần |
| `giống` | dự án có cùng guid, cùng nội dung | tick hay bỏ như nhau |
| `khác bản` | dự án có cùng guid, nội dung khác | tick là ghi đè bản dự án; ngoài phần cần thì bỏ tick |
| `trùng đường dẫn khác guid` | dự án có file cùng đường dẫn nhưng guid khác: hai asset khác nhau tranh một chỗ | bỏ tick; cần cả hai thì import rồi soát Project xem bản nào nằm ở đường dẫn đó |
| `→ dự án đặt ở …` | dự án đã dời asset cùng guid sang chỗ khác | import ghi lên file ở chỗ đó |
| `nhị phân, không đọc được tham chiếu` | asset của gốc lưu dạng nhị phân | danh sách cần có thể thiếu; mở prefab sau khi import để soát ô trống |
| `THIẾU guid …, trỏ từ …` | gốc trỏ asset không có trong gói lẫn dự án | xin gói đủ, hoặc gán lại ô đó sau khi import |
| `import cả gói sẽ chạm n file dự án đang có` | file `khác bản` hoặc trùng đường dẫn nằm ngoài phần cần | đây là lý do không import cả gói |

## Quy trình import

1. Chạy tool với `--project`. Đọc dòng `gốc` để chắc đúng prefab, scene cần; sai thì đặt `--root`.
2. Trong hộp thoại Import Unity Package: bấm **None**, rồi tick đúng các file ở mục `cần cho gốc` (và thư mục chứa chúng).
3. `THIẾU` khác 0 thì dừng, hỏi người gửi gói trước khi import.
4. Sau khi import: dời phần vừa nhập về thư mục của dự án nếu cần. Guid giữ nguyên nên lần import gói sau tool vẫn nhận ra.

## Nghiệm thu

`selftest` dựng một gói và một dự án giả. Gói có prefab trỏ material, script của thư viện và một guid dựng sẵn. Material trỏ
texture. Thư viện có một file giống, một file khác bản, một file mới, một file trùng đường dẫn khác guid. Dự án giữ texture ở chỗ
khác. Phải đọc ra:
- gốc kéo theo đúng prefab, material, texture, script và thư mục cha;
- script khác mỗi kiểu xuống dòng là `giống`;
- file khác nội dung là `khác bản`, có dòng cảnh báo ngoài gốc;
- file chưa có là `mới`;
- cùng đường dẫn khác guid được báo đúng nhãn;
- guid dựng sẵn không tính là thiếu;
- texture đã dời là `giống` kèm chỗ trong dự án;
- `--root` theo chuỗi chỉ lấy material và texture.
