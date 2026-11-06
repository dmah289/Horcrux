---
name: docs
description: Dùng khi viết hoặc cập nhật tài liệu: .md cho agent, .html cho developer và designer dùng hệ, Plan cho developer tự gõ, Manual cho người dùng tool, skill, và kỷ luật viết áp cho mọi loại.
---

# docs — .md, .html, Plan, Manual, skill

Skill nền dưới `Skills/horcrux_skill.md` (NT, ba tầng, ký hiệu định nghĩa ở đó).

Mỗi hệ và tool có tài liệu riêng, **không đặt cạnh code**, tên theo tên hệ, `.md` mở đầu bằng dòng `Code:` trỏ thư mục
code tính từ gốc Horcrux; Plan ở `Plans/`. **Hệ Runtime có đủ cặp** trong thư mục riêng `docs/Runtime/<Hệ>/`: `<Hệ>.md`
cho agent đọc rẻ token, `<Hệ>.html` cho developer đọc để dùng hệ. **Tool Editor chỉ `.md`**, nằm phẳng ở `docs/Editor/`.
**Thay đổi hệ thì cập nhật tài liệu trong cùng lần làm** — cả `.md` lẫn `.html`.

| Loại | Người đọc, vai trò | Vòng đời |
|---|---|---|
| **`.md`** | **agent** — bản đặc, rẻ token, đủ mọi tầng tới code; nguồn sinh `.html` | cùng lần làm với mỗi thay đổi |
| **`.html`** | **developer, designer** — dùng hệ không cần đọc code: kịch bản, cách dùng, flow, lưu ý; agent không đọc khi đã có `.md`. Bắt buộc với hệ Runtime, Editor chưa cần | cùng lần làm với `.md` |
| **Plan** | developer **tự gõ** — khi được yêu cầu, và code runtime vào Horcrux đã duyệt | `.md` ở `Plans/`; hệ dựng xong có tài liệu module thì xoá Plan |
| **Manual** | người dùng tool không phải developer, để **thao tác** | cạnh tài liệu của tool |
| **Skill** | agent mọi dự án — luật chung cho game Unity | qua chưng cất (`workflow` › Chưng cất), theo ba tầng và ký hiệu của `horcrux_skill` |

**Agent viết cả năm loại;** nghiệm thu là đối chiếu máy móc với code; developer chốt nội dung. Quy trình: phỏng vấn và đối
chiếu (`workflow`) → đọc **tất cả** source, hiểu data flow, lifecycle, lý do mỗi quyết định → `.md` → `.html` chọn từ `.md`.

**Tài liệu Horcrux chung cho mọi dự án:** ví dụ tên trung tính (`Piece`, `IMySaveData`); không tên, class, đường dẫn của dự
án; không kịch bản chơi thử; không preset số cho một game cụ thể.

**Chuỗi sự thật một chiều `code → .md → .html`:** code là chuẩn; `.html` dựng được từ `.md` không mở source. Lệch thì sửa
xuôi chuỗi. `.html` là **tập con chọn lọc** của `.md`: không có gì ở `.html` mà `.md` không có. **Sổ tay** — mỗi lần đồng bộ soát "bản chết"
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

## `.html` — tài liệu dùng hệ cho developer

Người đọc muốn **dùng hệ đúng mà không mở code**. Bốn phần, đều lấy từ `.md`:

| Phần | Trả lời | Lấy từ `.md` |
|---|---|---|
| **Kịch bản** | tình huống X xảy ra thì hệ làm gì, người chơi thấy gì | luật, lifecycle — chuỗi tình huống → kết quả, tên trung tính |
| **Cách dùng** | dựng hệ từ 0, gọi gì, đọc gì | "Trước khi chạy" nguyên vẹn · chỉ API consumer gọi, kèm khi nào gọi |
| **Flow** | dữ liệu đi qua những bước nào | luồng ASCII → diagram |
| **Lưu ý** | chỗ dễ dùng sai | bẫy · Safety · Platform · quyết định trái trực giác chạm tới consumer |

**Không mang sang:** chữ ký API đầy đủ · cấu trúc file · test · thuật toán bên trong · performance · quyết định chỉ người sửa
hệ cần — cần thì mở `.md`. Kịch bản ở đây là **kịch bản dùng**, không phải kịch bản chơi thử của một game.

**Thứ tự ưu tiên hình thức:** flowchart, biểu đồ các loại → bảng → văn xuôi chỉ khi không biểu thị được bằng hai dạng
trước. Theo loại nội dung: luồng, rẽ nhánh → flowchart · thứ tự, vòng đời → sơ đồ trạng thái hoặc timeline · so sánh, tình
huống → kết quả → bảng · định lượng → công thức, đồ thị · giá trị liên tục → Canvas · nhiều bước → step; số trong demo
cũng là "bản chết" nếu không đối chiếu `.md`. Demo chỉ khi biểu đồ và bảng không đủ.
**Nghiệm thu:** người mới đọc xong dựng và dùng được hệ không mở code · single file, không build · TOC khớp · đọc được trên
màn hình nhỏ, khi in, tắt JavaScript, mất mạng · không tương tác thì không tiến trình nào chạy · không thư viện tô màu code.

**Đồng bộ cùng lần làm với `.md`, soát lại cả trang, không chỉ phần vừa đổi:** `.md` đổi ở luật, "Trước khi chạy", luồng,
bẫy hay API consumer gọi thì `.html` đổi theo. **Sổ tay** — quét máy: bỏ thẻ, giải entity; mọi chuỗi trong `<code>` của
`.html` có trong `.md` (không bịa, không bản chết) · mọi dòng "Trước khi chạy" của `.md` có trong `.html` · mục lục khớp `id`
section.

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

**Gõ xong là hiểu hệ, không chỉ hệ chạy:** developer biết mỗi phần ai gọi, vì sao có, thiếu thì hỏng đâu — đủ để tự
maintain. **Tự chứa:** code lại được từ đầu tới cuối không suy đoán, không mở tài liệu khác; hệ có lõi toán thì `§0` dẫn
giải theo `math-physics` › Cho hiểu sâu. **Editor setup đầy đủ** — bước tay thiếu không compile error nào bắt; câu
developer hỏi lặp lại thì ghi ngược vào Plan.

**Dựng theo luồng, chia theo giai đoạn — không theo file.** Mở đầu bằng **luồng mô tả** (kịch bản dùng hệ từ đầu tới cuối,
cắt thành chặng) và **bản đồ hệ cuối** (ai làm gì, ai gọi ai). Mỗi giai đoạn nối thêm một chặng, xuyên qua mọi file chặng
đó cần, kết ở trạng thái **chạy được, thấy được**. Thứ tự theo luồng, không theo cây phụ thuộc type — lá trước gốc là gõ
class chưa biết ai dùng.

**Mỗi giai đoạn chỉ ra chỗ dùng ngay trong nó:** member thêm vào có caller thật trong cùng giai đoạn (NT1 áp từng giai
đoạn); call site trước, phần được gọi sau. Kèm sơ đồ tương tác sau giai đoạn, phần mới đánh dấu, và *vì sao chặng này
đến lúc này*.

**File đã có thì chỉ chỗ thêm, không chép lại:** neo theo file, region, tên member có thật — không số dòng, developer gõ
lệch là dòng trôi — kèm vài dòng có sẵn quanh chỗ thêm. File mới thì khung đủ nhưng chỉ chứa thứ giai đoạn đó dùng. Hệ
đang có code: giai đoạn 0 là bản đồ hệ hiện tại, chỗ đổi viết đoạn cũ → đoạn mới. Code tạm để chặng đầu chạy chỉ khi vài
dòng và giai đoạn thay nó nói vì sao thay — mọc thêm hơn viết lại.

**Lõi developer viết, test agent viết:** Plan chỉ có danh sách case theo giai đoạn, agent viết test sau khi đọc code thật.
Việc phát sinh lúc làm Plan thì đầu ra là Plan, không tạo file code. **Plan chạm cả Horcrux lẫn consumer thì phần Horcrux
chỉ thêm** (member mới mang mặc định, API cũ giữ) — mỗi giai đoạn một mốc compile sạch và một commit; xoá API cũ là việc
riêng. Developer gõ lệch Plan là bình thường: sau mỗi giai đoạn agent compile, so code với Plan, phân loại theo `workflow`
› Chưng cất.

**Sổ tay** — đầu Plan: "Ngữ cảnh đã chốt" · bảng "Đã khảo sát" · phụ thuộc Plan nào · luồng mô tả · bản đồ hệ cuối · `§0`.
Mỗi giai đoạn: mục tiêu (chặng nào, xong thì hệ làm thêm được gì) · vì sao bây giờ · sơ đồ tương tác · chỗ thêm theo neo
(`File` › region › neo → đoạn mới) · Interfaces nó consumes, produces · "toán → code" · *quyết định, phương án bỏ, vì sao* ·
Editor setup của giai đoạn · kiểm (compile, thấy gì, bảng case input → kỳ vọng, biên theo `workflow` › Nghiệm thu) · danh
sách cheat · mốc commit. Cuối Plan: bảng file → giai đoạn đã chạm. **Nghiệm thu:** có "Ngữ cảnh đã chốt" · đọc giai đoạn
nào cũng trả lời được "code này ai gọi" · không member nào có trước giai đoạn có caller của nó · không đoạn nào chép lại
cả file đã có · mọi neo trỏ member có thật · đúng công thức, đúng nhịp, self-document.

## Kỷ luật viết — mọi loại tài liệu

**Cắt trước khi giao — bắt buộc:** câu dẫn · tóm tắt lại · câu chuyển · nhận xét chất lượng thiết kế · ẩn dụ · một ý hai
lần · lý do dài cho khẳng định hiển nhiên. Cắt xong vẫn dài thì **tách file**.

- **Mỗi tài liệu một người đọc.** Chép nội dung loại này sang loại kia là sai cả hai. Manual theo **nhãn thật trên UI** —
  *bấm gì ra gì, dùng khi nào*, không tên class.
- **Không chép lại thứ người đọc đã cầm:** *xoá dòng này người đọc mất gì?* — "mở file kia xem" thì bỏ. Ngoại lệ: chữ ký
  API trong `.md`.
- **Đoạn code trong Plan của code đã tồn tại khớp nguyên văn chỗ neo của nó trong file thật**, đồng bộ bằng chép rồi so
  máy. **Sổ tay** — script dò mỗi đoạn tới file và neo nó ghi, so với vùng tương ứng, thay nguyên đoạn, chạy tới 0 đoạn lệch.
- **Thao tác Editor là một mục riêng** ở mọi đầu ra: Plan (Editor setup) · tài liệu ("Trước khi chạy") · **trả lời chat**.
- **Viết cho người đọc lần đầu, ở thì hiện tại**; ngoại lệ là "đã sai một lần" — ghi bài học, không tường thuật. **Sổ tay**
  — grep "trước đây", "bản cũ", "giờ đã".
- **Mâu thuẫn thì SỬA dòng cũ**, không thêm dòng nói ngược; dòng cũ do developer đặt thì nêu để phân xử.
- **Tri thức sinh trong hội thoại vào tài liệu trước khi báo xong:** câu hỏi của người đọc → câu trả lời ở lại tài liệu ·
  bug âm thầm vừa sửa → một dòng "đã sai một lần" · quy ước mới → tài liệu module. **Liệt kê nguyên văn trong báo cáo**;
  đủ chung thì vào skill.
