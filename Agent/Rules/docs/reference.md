# docs — tra cứu

Nền tảng và Sổ tay của `docs`. Đọc mục được bảng Tra cứu trong `SKILL.md` gọi.

## `.md`

Code trích nguyên văn; chữ ký API thành bảng. **Nghiệm thu:** lần theo một giá trị từ input tới output không nhảy section;
mỗi so sánh thấy **tiêu chí** và **kết luận**.

**Kho mục:** Data structures · Core algorithm · Lifecycle · Framework integration · Design decisions (theo Ngữ cảnh đã chốt ở
`workflow` › Báo cáo và trình bày) · Safety và error · Platform issues · Architecture · Testing · Extension · Performance. "Trước khi chạy" ghi
menu, tên ô Inspector, thứ kéo vào ô nào.

> **Nền tảng — KaTeX:** lệnh có `\` phải nằm trong `$…$`/`$$…$$` (trong backtick hiện raw); mỗi `$$…$$` trên một dòng.
> Quét: strip `$…$` và backtick, còn `\[a-zA-Z]` là lọt.

**Sổ tay** — mỗi lần đồng bộ soát "bản chết" và "thì hiện tại" (grep "trước đây", "bản cũ", "giờ đã"); đổi tên hay xoá tài
liệu thì grep **tham chiếu chết** trong comment, file hướng dẫn agent, tài liệu khác.

## `.html`

| Phần | Trả lời | Lấy từ `.md` |
|---|---|---|
| **Kịch bản** | tình huống X xảy ra thì hệ làm gì, người chơi thấy gì | luật, lifecycle — chuỗi tình huống → kết quả, tên trung tính |
| **Cách dùng** | dựng hệ từ 0, gọi gì, đọc gì | "Trước khi chạy" nguyên vẹn · chỉ API consumer gọi, kèm khi nào gọi |
| **Flow** | dữ liệu đi qua những bước nào | luồng ASCII → diagram |
| **Lưu ý** | chỗ dễ dùng sai | bẫy · Safety · Platform · quyết định trái trực giác chạm tới consumer |

**Không mang sang:** chữ ký API đầy đủ · cấu trúc file · test · thuật toán bên trong · performance · quyết định chỉ người
sửa hệ cần. Demo chỉ khi biểu đồ và bảng không đủ. **Nghiệm thu:** người mới đọc xong dựng và dùng được hệ không mở code ·
single file, không build · TOC khớp · đọc được trên màn hình nhỏ, khi in, tắt JavaScript, mất mạng · không tương tác thì
không tiến trình nào chạy.

**Hình thức theo loại nội dung:** luồng, rẽ nhánh → flowchart · thứ tự, vòng đời → sơ đồ trạng thái hoặc timeline · so
sánh, tình huống → kết quả → bảng · định lượng → công thức, đồ thị · giá trị liên tục → Canvas · nhiều bước → step. Số
trong demo cũng là "bản chết" nếu không đối chiếu `.md`. Kịch bản là **kịch bản dùng**, không phải kịch bản chơi thử của
một game. Không thư viện tô màu code. `.md` đổi ở luật, "Trước khi chạy", luồng, bẫy hay API consumer gọi thì `.html` đổi
theo.

**Sổ tay** — quét máy: bỏ thẻ, giải entity; mọi chuỗi trong `<code>` của `.html` có trong `.md` · mọi dòng "Trước khi chạy"
của `.md` có trong `.html` · mục lục khớp `id` section.

**Trang là chương trình:**
- **Nội dung không phụ thuộc trang trí:** hiệu ứng chỉ bật khi script xác nhận chạy (class trên `<html>` ở đầu `<head>`);
  thiếu script, mạng, khi in, `prefers-reduced-motion` vẫn đủ nội dung. Công thức render một lần; lỗi thì hiện nguyên TeX.
- **Một nguồn màu** ở `:root`; Canvas không đọc `var()` nên script đọc bảng **một lần** vào object hằng.
- **Mỗi khối một việc:** khối nội dung · ghi chú ba sắc (mặc định, tốt, bẫy) · công thức chốt viền nổi · sơ đồ ASCII · lưới
  số liệu cuối trang · danh sách bước · thanh đo · số font mono. Bảng tự bọc khung cuộn ngang lúc load.
- **Demo do sự kiện kích hoạt, mỗi frame tối đa một lần vẽ:** sự kiện đặt dirty flag và xin **một** frame; rời demo thì
  `cancelAnimationFrame`; interval dừng khi hết bước; phần tĩnh tính một lần. Canvas nhân `devicePixelRatio` chặn ở 2.
- **Cấm trong draw loop:** `shadowBlur` · `createImageData` mỗi sự kiện · toán per-pixel mỗi `mousemove` · `innerHTML`
  trong vòng lặp · màu `var()` cho Canvas.

**Sổ tay** — bốn dạng demo, chọn rẻ nhất: Canvas theo con trỏ · quá trình nhiều bước (DOM) · input → kết quả (slider) · đồ
thị tĩnh. Trang: header → mục lục trỏ `id` → `<main>` → footer ghi hệ, namespace.

## Plan

- **Tự chứa:** code lại được từ đầu tới cuối không suy đoán, không mở tài liệu khác.
- **Theo luồng:** mở đầu bằng **luồng mô tả** và **bản đồ hệ cuối**; mỗi giai đoạn xuyên qua mọi file chặng đó cần; trong
  giai đoạn, call site trước, phần được gọi sau. Plan chỉ có danh sách case test theo giai đoạn. Developer gõ lệch Plan là
  bình thường.
- **Phần dẫn đủ hiểu, sức dồn vào code:** lý giải sâu đặt cạnh đoạn code nó giải thích, không dồn lên đầu.
- **File đã có thì chỉ chỗ thêm, không chép lại:** neo theo file, region, tên member có thật — không số dòng. Đoạn code của
  code đã tồn tại khớp nguyên văn chỗ neo trong file thật.
- **Thuật toán trong Plan chứng minh bằng bản mẫu chạy được trước khi ghi** — code Plan chưa qua compiler mà developer gõ
  nguyên và tin.
- **Plan chạm cả Horcrux lẫn consumer thì phần Horcrux chỉ thêm** (member mới mang mặc định, API cũ giữ); mỗi giai đoạn một
  mốc compile sạch và một commit. Sau mỗi giai đoạn developer gõ, agent compile, so code với Plan, phân loại chỗ lệch theo
  `workflow` › Chưng cất.
- Hệ có lõi toán thì `§0` dẫn giải theo `math-physics` › Cho hiểu sâu; `§0` ghi phép kiểm của bản mẫu, số lượt và đối
  chứng. Bản mẫu cùng thuật toán, ngôn ngữ nào cũng được, vét cạn các bảo đảm.
- Câu developer hỏi lặp lại thì ghi ngược vào Plan. Phần trước giai đoạn đầu (Ngữ cảnh đã chốt, Đã khảo sát, luồng, bản đồ)
  chỉ đủ để định hướng; công viết đổ vào neo đúng, đoạn mới đủ gõ, chú thích chỗ dễ gõ sai.
- Mỗi giai đoạn kèm sơ đồ tương tác sau giai đoạn, phần mới đánh dấu, và *vì sao chặng này đến lúc này*. Thứ tự theo luồng,
  không theo cây phụ thuộc type.
- Neo kèm vài dòng có sẵn quanh chỗ thêm. File mới thì khung đủ nhưng chỉ chứa thứ giai đoạn đó dùng. Hệ đang có code:
  giai đoạn 0 là bản đồ hệ hiện tại, chỗ đổi viết đoạn cũ → đoạn mới. Code tạm để chặng đầu chạy chỉ khi vài dòng và giai
  đoạn thay nó nói vì sao thay.
- Xoá API cũ của Horcrux là việc riêng, không nằm trong Plan thêm.

**Sổ tay** — đầu Plan: "Ngữ cảnh đã chốt" · bảng "Đã khảo sát" · phụ thuộc Plan nào · luồng mô tả · bản đồ hệ cuối · `§0`.
Mỗi giai đoạn: mục tiêu (chặng nào, xong thì hệ làm thêm được gì) · vì sao bây giờ · sơ đồ tương tác · chỗ thêm theo neo
(`File` › region › neo → đoạn mới) · Interfaces nó consumes, produces · "toán → code" · *quyết định, phương án bỏ, vì sao* ·
Editor setup của giai đoạn · kiểm (compile, thấy gì, bảng case input → kỳ vọng, biên theo `workflow` › tra cứu › Nghiệm
thu) · danh sách cheat · mốc commit. Cuối Plan: bảng file → giai đoạn đã chạm.

- Plan chỉ có Horcrux: "chạy được, thấy được" mỗi giai đoạn là compile sạch và bảng case xanh, "thấy trong game" là giai
  đoạn cuối — dự án chuyển sang bản Horcrux, xoá bản tạm, kèm kịch bản chơi thử.
- Code Editor không có test thì caller là nút của dự án: nối dự án ngay trong giai đoạn, xoá bản tạm cùng tên cùng lúc.
- Nhiều utility độc lập trong một Plan: mỗi cơ chế một luồng, bản đồ và chuỗi giai đoạn riêng, chung Ngữ cảnh đã chốt, Đã
  khảo sát, tài liệu và chuyển dự án.
- Đồng bộ đoạn code với file thật bằng chép rồi so máy: script dò mỗi đoạn tới file và neo nó ghi, so với vùng tương ứng,
  thay nguyên đoạn, chạy tới 0 đoạn lệch.

**Nghiệm thu Plan:** có "Ngữ cảnh đã chốt" · phần trước giai đoạn đầu đọc lướt được · đọc giai đoạn nào cũng trả lời được
"code này ai gọi" · không member nào có trước giai đoạn có caller của nó · không đoạn nào chép lại cả file đã có · mọi neo
trỏ member có thật · đúng công thức, đúng nhịp, self-document.
