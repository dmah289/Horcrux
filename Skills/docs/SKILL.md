---
name: docs
description: Dùng khi viết hoặc cập nhật tài liệu: .md cho agent, .html cho developer và designer, Plan cho developer tự gõ, Manual cho người dùng tool, skill, và kỷ luật viết áp cho mọi loại.
---

# docs — .md, .html, Plan, Manual, skill

Skill nền dưới `Skills/horcrux_skill.md` (NT, ba tầng, ký hiệu định nghĩa ở đó).

Mỗi hệ và tool có tài liệu riêng, **không đặt cạnh code**, tên theo tên hệ, `.md` mở đầu bằng dòng `Code:` trỏ thư mục
code tính từ gốc Horcrux; Plan ở `Plans/`. **Hệ Runtime có đủ cặp** trong thư mục riêng `docs/Runtime/<Hệ>/`: `<Hệ>.md`
cho agent đọc rẻ token, `<Hệ>.html` cho developer đọc trực quan. **Tool Editor chỉ `.md`**, nằm phẳng ở `docs/Editor/`.
**Thay đổi hệ thì cập nhật tài liệu trong cùng lần làm** — riêng `.html` theo mốc.

| Loại | Người đọc, vai trò | Vòng đời |
|---|---|---|
| **`.md`** | **agent** — bản đặc, rẻ token; nguồn sinh `.html` | cùng lần làm với mỗi thay đổi |
| **`.html`** | **developer, designer** — trực quan hoá 100% `.md`; agent không đọc khi đã có `.md`. Bắt buộc với hệ Runtime, Editor chưa cần | đồng bộ khi developer yêu cầu hoặc chốt một cụm thay đổi |
| **Plan** | developer **tự gõ** — khi được yêu cầu, và code runtime vào Horcrux đã duyệt | `.md` ở `Plans/`; hệ dựng xong có tài liệu module thì xoá Plan |
| **Manual** | người dùng tool không phải developer, để **thao tác** | cạnh tài liệu của tool |
| **Skill** | agent mọi dự án — luật chung cho game Unity | qua chưng cất (`workflow` › Chưng cất), theo ba tầng và ký hiệu của `horcrux_skill` |

**Agent viết cả năm loại;** nghiệm thu là đối chiếu máy móc với code; developer chốt nội dung. Quy trình: phỏng vấn và đối
chiếu (`workflow`) → đọc **tất cả** source, hiểu data flow, lifecycle, lý do mỗi quyết định → `.md` → `.html` từ `.md`.

**Tài liệu Horcrux chung cho mọi dự án:** ví dụ tên trung tính (`Piece`, `IMySaveData`); không tên, class, đường dẫn của dự
án; không kịch bản chơi thử; không preset số cho một game cụ thể.

**Chuỗi sự thật một chiều `code → .md → .html`:** code là chuẩn; `.html` dựng được từ `.md` không mở source. Lệch thì sửa
xuôi chuỗi. "100%" là không mất nội dung khi chuyển bản, không phải viết nhiều. **Sổ tay** — mỗi lần đồng bộ soát "bản chết"
và "thì hiện tại"; đổi tên hay xoá tài liệu thì grep **tham chiếu chết** trong comment, file hướng dẫn agent, tài liệu khác.

## `.md` — tài liệu cho agent

Tổ chức theo **đường đi của dữ liệu** (input → processing → output), luồng vẽ ASCII. Code chỉ xuất hiện khi đoạn code *là*
thứ cần minh hoạ, trích nguyên văn; chữ ký API thành bảng.

**Mục bắt buộc "Trước khi chạy" với hệ cần wire tay:** người chưa từng mở hệ dựng lại được **từ 0** chỉ bằng mục này. Viết
bằng **thao tác và nhãn thật trên UI** (menu, tên ô Inspector, thứ kéo vào ô nào), mỗi bước kèm *"thiếu thì hỏng ở đâu"*.

**Còn lại là kho mục để chọn, không phải form** — mỗi mục gọi tên được câu hỏi người đọc nó trả lời: Data structures · Core
algorithm · Lifecycle · Framework integration · Design decisions (theo `workflow` › Ngữ cảnh đã chốt) · Safety và error ·
Platform issues · Architecture · Testing · Extension · Performance. Nghiệm thu: lần theo một giá trị từ input tới output
không nhảy section; mỗi so sánh thấy **tiêu chí** và **kết luận**.

> **Nền tảng — KaTeX:** lệnh có `\` phải nằm trong `$…$`/`$$…$$` (trong backtick hiện raw); mỗi `$$…$$` trên một dòng.
> Quét: strip `$…$` và backtick, còn `\[a-zA-Z]` là lọt.

## `.html` — tài liệu chính

Giữ **cấu trúc section của `.md`**; số trong demo cũng là "bản chết" nếu không đối chiếu code. Trực quan hoá theo loại nội
dung: so sánh → bảng · luồng → diagram · định lượng → công thức · giá trị liên tục → Canvas · nhiều bước → step. Demo chỉ khi
bảng và text không đủ. **Nghiệm thu:** đủ 100% nội dung · single file, không build · TOC khớp · đọc được trên màn hình nhỏ,
khi in, tắt JavaScript, mất mạng · không tương tác thì không tiến trình nào chạy · không thư viện tô màu code.

**Đồng bộ là so lại cả `.md`, không chỉ phần vừa đổi:** `.html` đi theo mốc nên gom lệch của mọi lần làm trước. **Sổ tay**
— quét máy: bỏ thẻ, giải entity, mọi chuỗi trong backtick và mọi dòng khối code của `.md` có trong text `.html` · tiêu đề
`##` khớp `<h2>` · mục lục khớp `id` section.

**Trang là chương trình chạy trên máy người đọc** — NT2 áp nguyên:
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

## Plan — để developer tự triển khai

**Tự chứa:** developer code lại được từ đầu tới cuối không suy đoán, không mở tài liệu khác; task xếp theo thứ tự phụ
thuộc; hệ có lõi toán thì `§0` dẫn giải theo `math-physics` › Cho hiểu sâu. **Dẫn giải dừng ở luồng; Editor setup đầy đủ** —
bước tay thiếu không compile error nào bắt; câu developer hỏi lặp lại thì ghi ngược vào Plan.

**Lõi developer viết, test agent viết:** Plan chỉ có danh sách case, agent viết test sau khi đọc code thật. Việc phát sinh
lúc làm Plan thì đầu ra là Plan, không tạo file code. **Plan chạm cả Horcrux lẫn consumer thì phần Horcrux chỉ thêm**
(member mới mang mặc định, API cũ giữ) — mỗi task một mốc compile sạch và một commit; xoá API cũ là việc riêng. Developer
gõ lệch Plan là bình thường: sau mỗi step agent compile, so code với Plan, phân loại theo `workflow` › Chưng cất.

**Hai hình dạng code:** hệ **chưa có code** — mỗi khối là một file trọn vẹn để chép · hệ **đang có code** — chuỗi chỗ đổi
theo thứ tự: file · dòng · đoạn cũ → đoạn mới.

**Sổ tay** — kho phần mỗi task: bảng "Đã khảo sát" · Files · Interfaces (consumes, produces, chữ ký đủ) · "toán → code" ·
*quyết định và vì sao* · code dán được · Editor setup riêng · bảng case (input → kỳ vọng, biên theo `workflow` › Nghiệm thu) ·
danh sách cheat. **Nghiệm thu:** có "Ngữ cảnh đã chốt" · mọi hàm có caller thật (NT1) · đúng công thức, đúng nhịp,
self-document.

## Kỷ luật viết — mọi loại tài liệu

**Cắt trước khi giao — bắt buộc:** câu dẫn · tóm tắt lại · câu chuyển · nhận xét chất lượng thiết kế · ẩn dụ · một ý hai
lần · lý do dài cho khẳng định hiển nhiên. Cắt xong vẫn dài thì **tách file**.

- **Mỗi tài liệu một người đọc.** Chép nội dung loại này sang loại kia là sai cả hai. Manual theo **nhãn thật trên UI** —
  *bấm gì ra gì, dùng khi nào*, không tên class.
- **Không chép lại thứ người đọc đã cầm:** *xoá dòng này người đọc mất gì?* — "mở file kia xem" thì bỏ. Ngoại lệ: chữ ký
  API trong `.md`.
- **Khối code trong Plan của code đã tồn tại là bản sao nguyên văn**, đồng bộ bằng chép rồi so máy. **Sổ tay** — script dò
  khối mở bằng `using`/`namespace` tới `.cs` giống nhất, thay nguyên khối, chạy tới 0 khối lệch.
- **Thao tác Editor là một mục riêng** ở mọi đầu ra: Plan (Editor setup) · tài liệu ("Trước khi chạy") · **trả lời chat**.
- **Viết cho người đọc lần đầu, ở thì hiện tại**; ngoại lệ là "đã sai một lần" — ghi bài học, không tường thuật. **Sổ tay**
  — grep "trước đây", "bản cũ", "giờ đã".
- **Mâu thuẫn thì SỬA dòng cũ**, không thêm dòng nói ngược; dòng cũ do developer đặt thì nêu để phân xử.
- **Tri thức sinh trong hội thoại vào tài liệu trước khi báo xong:** câu hỏi của người đọc → câu trả lời ở lại tài liệu ·
  bug âm thầm vừa sửa → một dòng "đã sai một lần" · quy ước mới → tài liệu module. **Liệt kê nguyên văn trong báo cáo**;
  đủ chung thì vào skill.
