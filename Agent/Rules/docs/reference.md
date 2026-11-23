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

| Khối | Chứa | Không chứa |
|---|---|---|
| **dòng mở**, ≤3 dòng | nguồn bản tạm kèm đường dẫn · phụ thuộc, chung file với Plan nào, gõ trước hay sau · lệnh kiểm | phân công, repo commit, đã compile gì |
| **Kịch bản** | bảng *tình huống → hệ làm gì → caller hoặc người chơi thấy gì*, gồm biên; là kịch bản chơi thử khi chuyển dự án | người dùng, mục tiêu viết thành văn |
| **Flow** | một sơ đồ ASCII bản cuối: ai gọi ai, dữ liệu đi đâu, mỗi nút kèm file | luồng tả bằng văn, bản đồ thứ hai |
| **Thuật toán** — khi có lõi toán hay logic không hiển nhiên | công thức, bất biến, code lõi theo `math-physics` › Cho hiểu sâu · một dòng bản mẫu: số lượt, đối chứng làm đỏ | dẫn giải lặp lại code |
| **GĐn — *xong thì làm thêm được gì*** | sơ đồ sau giai đoạn, phần mới đánh `+` · chỗ thêm theo neo · Editor setup bước đánh số · bảng case *input → kỳ vọng* | Mục tiêu, Vì sao bây giờ, Quyết định, consumes/produces, mốc commit |
| **Chuyển dự án (agent)** | file dự án chuyển, bản tạm xoá, tài liệu hệ, mỗi hành vi đổi một dòng | đoạn cũ → mới, kịch bản chơi thử riêng |
| **Không làm · Mở rộng sau** | mỗi dòng một phương án bỏ hoặc để sau — vì sao trong một câu | |

Khối không có nội dung thì bỏ, không ghi "không". Tên dự án nguồn chỉ ở dòng mở và Chuyển dự án.

- **Tự chứa:** code lại được từ đầu tới cuối không suy đoán, không mở tài liệu khác.
- **Theo luồng:** mỗi giai đoạn xuyên qua mọi file chặng đó cần, kết ở một mốc compile sạch và một commit; trong giai đoạn,
  call site trước, phần được gọi sau. Thứ tự theo luồng, không theo cây phụ thuộc type. Developer gõ lệch Plan là bình
  thường.
- **Lý do ở hai chỗ:** comment *vì sao* trong đoạn code, chỗ code làm điều trái trực giác (trần `code-design` › Naming) —
  developer gõ luôn vào code thật · phương án bỏ ở khối cuối. Câu developer hỏi lặp lại thì ghi ngược vào một trong hai chỗ.
- **File đã có thì chỉ chỗ thêm, không chép lại:** neo `File` › region › member có thật, không số dòng, kèm vài dòng có sẵn
  quanh chỗ thêm; đoạn code của code đã tồn tại khớp nguyên văn file thật. Hệ đang có code: chỗ đổi viết đoạn cũ → đoạn
  mới. File mới thì khung đủ nhưng chỉ chứa thứ giai đoạn đó dùng. Code tạm để chặng đầu chạy chỉ khi vài dòng và giai đoạn
  thay nó nói vì sao thay.
- **Code trong Plan đã qua compiler, thuật toán đã qua bản mẫu** cùng thuật toán, ngôn ngữ nào cũng được, vét cạn các bảo
  đảm — developer gõ nguyên và tin. Kết quả kiểm khác ghi trong báo cáo chat, không ghi vào Plan.
- **Plan chạm cả Horcrux lẫn consumer thì phần Horcrux chỉ thêm** (member mới mang mặc định, API cũ giữ); xoá API cũ là việc
  riêng. Sau mỗi giai đoạn developer gõ, agent compile, so code với Plan, phân loại chỗ lệch theo `workflow` › Chưng cất.
- Plan chỉ có Horcrux: giai đoạn developer gõ "thấy được" là compile sạch và bảng case xanh; thấy trong game ở Chuyển dự
  án — dự án sang bản Horcrux, xoá bản tạm, xoá Plan, chơi thử theo bảng Kịch bản.
- Code Editor không có test thì caller là nút của dự án: nối dự án ngay trong giai đoạn, xoá bản tạm cùng tên cùng lúc.
- Nhiều utility độc lập trong một Plan: mỗi cơ chế một Kịch bản, Flow và chuỗi giai đoạn riêng; chung dòng mở, Chuyển dự
  án, Không làm.

**Sổ tay** — đồng bộ đoạn code với file thật bằng chép rồi so máy: script dò mỗi đoạn tới file và neo nó ghi, so với vùng
tương ứng, thay nguyên đoạn, chạy tới 0 đoạn lệch. Đo văn xuôi: bỏ khối code, sơ đồ, dòng bảng, heading; phần còn lại chia
tổng ký tự.

**Nghiệm thu Plan:** đủ khối theo thứ tự · văn xuôi ≤ 20% · đọc giai đoạn nào cũng trả lời được "code này ai gọi" từ sơ đồ
· không member nào có trước giai đoạn có caller của nó · không đoạn văn quyết định ngoài comment và khối cuối · không đoạn
nào chép lại cả file đã có · mọi neo trỏ member có thật · đúng công thức, đúng nhịp, self-document.
