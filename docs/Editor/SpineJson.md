Code: `Editor/SpineJson/Tools~/spine_json.py`

# SpineJson — đọc dữ liệu Spine JSON thành bảng

In cây xương, slot, attachment, event, thời lượng và khoá từng anim của file Spine xuất dạng JSON, cùng vùng và chế độ alpha
của atlas, không cần mở Spine Editor hay vào Unity. Dùng khi hoạ sĩ giao skeleton mới: biết xương nào để code bám, anim dài bao
nhiêu, có event chưa, atlas straight hay PMA, trước khi dựng prefab hay viết code gọi anim. Cách gọi Spine trong Unity ở
`docs/ToolUsage/Spine.md`.

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | agent, lúc nhận gói Spine của hoạ sĩ và lúc khớp nhịp code với anim |
| Đầu vào | `.json` của Spine 4.x, `.atlas.txt` định dạng Spine 4 |
| Ranh giới | chỉ đọc và in. Không sửa json (thêm event là việc của hoạ sĩ, `docs/ToolUsage/Spine.md` › Event trong anim) |
| Chỗ đặt | Python ở `Editor/SpineJson/Tools~/`: Unity không import, không vào build |
| Phụ thuộc | thư viện chuẩn của Python; chạy được bằng `-I` |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Đọc `.skel` nhị phân | định dạng nhị phân đổi theo từng bản Spine; gói hoạ sĩ giao cho Unity thường là JSON. Cần thì nhờ hoạ sĩ xuất JSON |
| Tính vị trí world của xương | phải dựng lại inherit, shear, ràng buộc của runtime; pose frame 0 khác setup. Đo trong Unity bằng `Bone.GetWorldPosition` sau `Update(0)` |
| Atlas định dạng Spine 3 (`xy:`, `orig:`) | dự án dùng runtime 4.x không nạp được dữ liệu 3.x |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| Thời lượng anim là khoá muộn nhất của **mọi** timeline (xương, slot, deform, draw order, event) | Spine tính `Animation.Duration` như vậy; chỉ nhìn xương thì sót đoạn mờ dần của slot ở cuối |
| Khoá không ghi `time` tính là mốc 0 | JSON Spine 4 bỏ mọi trường bằng mặc định, kể cả `time: 0` và kênh `x: 0` |
| Khoá không còn kênh nào in `setup` | cùng lý do: khoá về đúng giá trị setup thì JSON không ghi kênh |

## Lệnh

```bash
T="<horcrux>/Editor/SpineJson/Tools~/spine_json.py"
python -X utf8 -I "$T" summary <a.json> [<b.json> …]
python -X utf8 -I "$T" anim    <a.json> <tên anim> [--timeline <chuỗi trong tên xương hay slot>]
python -X utf8 -I "$T" atlas   <a.atlas.txt>
python -X utf8 -I "$T" selftest
```

| Lệnh | In ra |
|---|---|
| `summary` | bản Spine và khung xuất (x, y, rộng, cao) · mỗi xương `tên ← cha · x,y · xoay · scale`, đánh dấu `inherit` khác normal và shear · slot theo thứ tự vẽ dưới lên · attachment từng skin với kiểu, ảnh, cỡ · event khai báo · mỗi anim: thời lượng, timeline nào có khoá, event và mốc |
| `anim` | từng timeline: mốc, giá trị kênh, `bezier` hay `stepped` · deform chỉ in mốc · draw order · event. `--timeline` giữ timeline có tên chứa chuỗi |
| `atlas` | mỗi trang: cỡ, `PMA` hay `straight`, filter · mỗi vùng: bounds, offsets (cắt viền), xoay |
| `selftest` | chạy trên dữ liệu mẫu có đáp án; `FAIL` ở dòng nào là phép đọc đó hỏng |

## Đọc kết quả khi dựng theo anim

- **Xương để code bám** (vật ngoài skeleton đi theo xương): tên xương nằm ở `summary`; khoá của nó ở `anim --timeline <tên>` cho
  biết nó đi xa bao nhiêu, xoay bao nhiêu độ, mốc đổi nhịp. Mốc khoá là ứng viên cho khoảnh khắc neo (keo đứt, chạm đất).
- **Anim chưa có event** (`event: không có`) mà hiệu ứng cần một khoảnh khắc: chọn mốc từ khoá, ghi tên và giây để bàn giao
  hoạ sĩ đặt event (`docs/ToolUsage/Spine.md` › Event trong anim).
- **Khung xuất bằng cỡ màn** (rộng, cao cỡ 1080×2340) nghĩa là hoạ sĩ dựng theo màn thiết kế; cỡ trong game quy theo vật world
  đứng cạnh, không theo khung này (`Agent/Skills/demo-compare/SKILL.md`).
- **Chu kỳ video demo trùng thời lượng một anim** thì video quay từ preview của hoạ sĩ (`Agent/Skills/demo-compare/SKILL.md` ›
  Demo là video).
- **Atlas `PMA` hay `straight`** quyết material và preset texture (`docs/ToolUsage/Spine.md` › Dựng skeleton trên canvas).
