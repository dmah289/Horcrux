# ContractDiff — lớp contract của dự án lệch repo nguồn ở đâu

Hai lệnh cho dự án bê một lớp code từ game nguồn và phải giữ nó khớp nguồn: `contract_diff` so từng file `.cs`/`.asmdef`
trong các root contract với checkout của repo nguồn, in file thiếu, file đã chạm, GUID lệch, file chỉ dự án có;
`warn_count` đếm warning/error compiler không trùng trong Editor.log để biết lệch nào sinh warning mới.

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | agent, khi developer hỏi lớp contract còn khớp nguồn không, trước khi đồng bộ với bản nguồn mới, và sau mỗi lần compile |
| Đầu vào | thư mục `Assets` của repo nguồn (checkout cục bộ) · `Assets` của dự án · danh sách root contract · `%LOCALAPPDATA%/Unity/Editor/Editor.log` |
| Ranh giới | tool chỉ **đo**; file "khác nội dung" là cố ý hay trôi do comment đánh dấu của dự án và tài liệu contract quyết. Không sửa file, không merge |
| Chỗ đặt | `Editor/ContractDiff/Tools~/contract_diff.py`, `warn_count.py`: thư mục đuôi `~` Unity không import, không vào build. Không có C# |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| So bằng `git diff` giữa hai repo | hai repo không chung lịch sử: dự án copy file, không có commit chung để diff |
| Chuẩn hoá sâu hơn BOM, CRLF, khoảng trắng cuối dòng | câu hỏi là file có bị **chạm** không; một dòng comment đánh dấu cũng là chạm, phải hiện |
| Đếm warning trong `Packages/` | regex chỉ bắt `Assets/…`: warning của gói ngoài không sửa được, chỉ làm nhiễu số |
| Tự tìm repo nguồn hay danh sách root | khác mỗi máy, mỗi dự án; thiếu tham số là thoát 2, không đoán |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| Chỉ mục GUID của nguồn quét **toàn bộ** `Assets` nguồn, không chỉ `--roots` | file "chỉ có ở project" có thể là file nguồn đã dời sang root khác; GUID trùng ở ngoài danh sách root mới nói được điều đó |
| GUID khác là lỗi dù nội dung giống | prefab của nguồn trỏ script theo GUID; GUID lệch là Missing Script khi bê prefab sang |
| `copyOnly` không tính vào mã thoát 1 | dự án tự thêm file vào root contract là bình thường; chỉ thiếu, chạm, GUID lệch mới là lệch nguồn |
| `warn_count` đếm tập **không trùng** (file, dòng, loại, mã) | Editor.log ghi lại mọi lần compile trong phiên, một warning xuất hiện n lần |

## Luồng dữ liệu

```
source/Assets ──┬─ walk(--roots, --ext, bỏ thư mục ~) ── a ─┐
                └─ guid_index(toàn bộ Assets nguồn)          │  a−b → missing
project/Assets ─── walk(--roots, --ext, bỏ thư mục ~) ── b ─┼─ a∩b → norm() khác: differ · guid() khác: guid
                                                             └─ b−a → copyOnly (+ ghi chú khi GUID có trong guid_index)
                                                 → stdout một dòng tổng · -o report.md theo từng root

Editor.log ── regex "Assets/…(dòng,cột): warning|error CSxxxx" ── set() ── by code · by root · own(--own) · errors
```

## Lệnh

```bash
python -X utf8 -I "<horcrux>/Editor/ContractDiff/Tools~/contract_diff.py" --source <Assets nguồn> --project <Assets dự án> --roots <a,b/c,...> [--ext .cs,.asmdef] [-o report.md]
python -X utf8 -I "<horcrux>/Editor/ContractDiff/Tools~/warn_count.py" [--log <Editor.log>] [--own <root1,root2>] [-o out.txt]
```

| Cờ | Nghĩa |
|---|---|
| `--roots` | thư mục con của `Assets`, cách nhau bằng dấu phẩy, lồng được (`_Modules/UGUI`); root không có ở một bên thì đếm 0 bên đó |
| `--ext` | đuôi file so sánh; `.meta` luôn đọc kèm để lấy GUID, không nằm trong danh sách |
| `-o` | báo cáo Markdown: mỗi root một mục, bốn bảng thiếu · khác nội dung · GUID khác · chỉ project |
| `--log` | mặc định `%LOCALAPPDATA%/Unity/Editor/Editor.log`, log của Editor đang mở |
| `--own` | thư mục con ngay dưới `Assets/` tính là code của mình; không có thì bỏ mục `own` |

Mã thoát: `contract_diff` 1 khi missing + differ + guid > 0 · `warn_count` 1 khi có error · 2 khi dùng sai. Chạy dưới một giây cho vài trăm file.

## Đọc kết quả

| Thấy gì | Nghĩa là | Soi tiếp ở đâu |
|---|---|---|
| `missing N` | file nguồn chưa bê sang, hoặc đã bị xoá ở dự án | bảng "Thiếu" trong report; luật của tài liệu contract về file nguồn chưa có ai gọi |
| `differ N` | dự án đã chạm file | mở file tìm comment đánh dấu của dự án; không có comment là trôi, không phải cố ý |
| `guid N` | `.meta` lệch | chép lại `.meta` từ nguồn; prefab nguồn trỏ script theo GUID |
| `copyOnly … (GUID trùng source x)` | file nguồn bị dời hoặc đổi tên trong dự án | quyết giữ chỗ nào, ghi vào tài liệu contract |
| `copyOnly` không ghi chú | file dự án tự thêm vào root contract | cân nhắc dời về thư mục của dự án |
| `by root` có root lạ nhiều warning | gói ngoài | bỏ qua |
| `own(…)` nhiều hơn lần đếm trước | code của mình sinh warning mới | `-o out.txt` hai lần rồi diff hai file |
| `errors: […]` | compile hỏng | sửa trước; số warning lúc compile hỏng không có nghĩa |

## Đã sai một lần

- Ghi cứng đường dẫn repo nguồn và danh sách root trong script: máy khác không chạy, dự án sau phải sửa script. Mọi đường dẫn và root là tham số.
- Script in tiếng Việt ra console Windows không có `-X utf8` dừng giữa chừng sau khi đã ghi một phần báo cáo.
- Đếm warning bằng grep thô trên Editor.log cho số tăng theo số lần compile trong phiên; chỉ tập không trùng mới so được giữa hai lần.

## Nghiệm thu

Hai cây nhỏ có đủ năm ca: file chỉ ở nguồn → `missing 1` · file chỉ khác BOM, CRLF, khoảng trắng cuối dòng → **không** differ · file đổi
một từ → `differ 1` · `.meta` khác GUID → `guid 1` · file chỉ ở dự án mang GUID của một file nguồn ở root ngoài `--roots` → `copyOnly`
có ghi chú; file trong `Tools~` không được đếm; mã thoát 1. `warn_count` trên Editor.log sau một lần compile sạch: `errors: []`, mã
thoát 0, `unique` bằng số dòng của file `-o`.
