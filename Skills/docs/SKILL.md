---
name: docs
description: Dùng khi viết hoặc cập nhật tài liệu: .md cho agent, .html cho developer và designer, Plan cho developer tự gõ, Manual cho người dùng tool, và kỷ luật viết áp cho mọi loại.
---

# docs — tài liệu .md, .html, Plan, Manual

Skill nền của Horcrux, đứng dưới `Skills/horcrux_skill.md`: `NT<n>`, ba tầng Luật · Nền tảng · Sổ tay và bảng
chỉ dẫn định nghĩa ở đó. Con trỏ `` `skill` › Mục `` trỏ sang skill khác; `mục X` trỏ trong file này.

Mỗi hệ thống và mỗi tool có tài liệu riêng, **không đặt cạnh code**: trong Horcrux nằm phẳng ở `docs/Editor/`
(tool Editor) hoặc `docs/Runtime/` (hệ runtime), tên file theo tên hệ; Plan ở `Plans/`. Mỗi tài liệu mở đầu
bằng dòng `Code:` trỏ thư mục code tính từ gốc Horcrux — chỗ đặt file không còn nói code ở đâu. **Thay đổi
hệ thì cập nhật tài liệu trong cùng lần làm** — riêng `.html` theo nhịp mốc.

| Loại | Vai trò | Vòng đời |
|---|---|---|
| **`.md`** | tài liệu **agent đọc** để hiểu và phát triển hệ — bản đặc, rẻ token; nguồn sinh `.html` | cập nhật **cùng lần làm** với mỗi thay đổi |
| **`.html`** | tài liệu **developer và game designer đọc** — trực quan hoá 100% nội dung `.md`; agent không đọc bản này khi đã có `.md` | đồng bộ từ `.md` theo **mốc** — developer yêu cầu, hoặc chốt xong một cụm thay đổi |
| **Plan** | để developer **tự gõ** — khi developer yêu cầu, và mọi code runtime vào Horcrux (`reuse-gate`) | luôn là `.md`; theo task. Plan cho Horcrux nằm trong `Plans/` ở gốc Horcrux |
| **Manual** (khi tool có người dùng không phải developer) | người dùng đọc để **thao tác** | cạnh tài liệu của tool trong `docs/` |

**Ai viết: agent, cả bốn loại.** Nghiệm thu tài liệu là **đối chiếu máy móc với code** (`workflow` › Nghiệm thu). Developer
**chốt nội dung**: dòng nào lệch thiết kế thì developer phân xử, agent sửa. **Quy trình:** phỏng vấn (`workflow` › Phỏng vấn)
và đối chiếu hiểu biết về code (`workflow` › Đối chiếu với developer) → đọc **tất cả** source, hiểu 100% data flow, lifecycle, lý do mỗi
quyết định → viết `.md` → sinh `.html` từ `.md` → khi được yêu cầu thì viết Plan.

**Chuỗi sự thật một chiều `code → .md → .html`** (`code-design` › Bất biến): code là chuẩn, hai tài liệu phản ánh **100% thiết
kế đang chạy trong code**; `.html` dựng được từ `.md` mà **không mở source**. Không gộp, không xoá hai bản.
Lệch thì sửa xuôi theo chuỗi: `.html` lệch → đối chiếu `.md` với code trước, rồi đồng bộ `.html` từ `.md`.
"100%" là không mất nội dung khi chuyển bản, không phải viết cho nhiều — trần độ dài do NT10 canh.
**Sổ tay** — checklist mỗi lần đồng bộ: soát "bản chết" (NT8) và "ở thì hiện tại" (mục Kỷ luật viết) · đổi tên hay xóa
file tài liệu thì grep **tham chiếu chết** trong code comment, file hướng dẫn agent của dự án, tài liệu khác.

## `.md` — tài liệu cho agent

Tổ chức theo **đường đi của dữ liệu** (input → processing → output), không theo "lý thuyết → thiết kế →
code": người đọc lần theo được một giá trị từ lúc vào đến lúc ra. Luồng dữ liệu vẽ bằng ASCII. **Code chỉ
xuất hiện khi đoạn code *là* thứ cần minh hoạ, và khi đó trích nguyên văn**; chữ ký API thành bảng (mục Kỷ luật viết).

**Một mục bắt buộc: "Trước khi chạy", với mọi hệ cần wire tay** (`editor-first`). Nghiệm thu: người chưa từng mở hệ
dựng lại được **từ 0** chỉ bằng mục này, theo thứ tự, không đọc code hay hỏi ai. Viết bằng **thao tác và
nhãn thật trên UI** — đường dẫn menu, tên ô trong Inspector, thứ kéo vào ô nào — mỗi bước kèm *"thiếu bước
này thì hỏng ở đâu"*. Hệ không cần wire gì thì không có mục này.

**Còn lại là kho mục để chọn, không phải form để điền.** Mỗi mục đưa vào gọi tên được **câu hỏi của người
đọc** mà nó trả lời; hệ nhỏ có ba mục là bình thường. Kho: Data structures · Core algorithm · Lifecycle ·
Framework integration · Design decisions (nội dung theo `workflow` › Ngữ cảnh đã chốt, kèm "đã sai một lần") · Safety và error ·
Platform issues · Architecture (file tree kèm vai trò) · Testing · Extension · Performance. **Nghiệm thu:**
lần theo được một giá trị từ input tới output mà không nhảy section · mỗi so sánh đều thấy **tiêu chí** và
**kết luận**.

> **Nền tảng — KaTeX:** mọi lệnh có `\` (`\frac`, `\sqrt`…) **phải** nằm trong `$…$` hoặc `$$…$$`; trong
> backtick sẽ hiện raw text. Mỗi block `$$…$$` trên **một dòng**. Chốt xong quét: strip hết `$…$` và
> backtick, còn sót `\[a-zA-Z]` nào là lọt.

## `.html` — tài liệu chính

Giữ **cấu trúc section của `.md`** để hai bản đối chiếu được; số liệu trong demo cũng là "bản chết" nếu
không đối chiếu lại code (NT8). Trực quan hoá **theo loại nội dung**: so sánh thì bảng · luồng dữ liệu thì
diagram · quan hệ định lượng thì công thức · giá trị biến thiên liên tục thì Canvas · quá trình nhiều bước
thì step. Demo chỉ làm khi bảng và text **không đủ** để thấy hành vi.

**Nghiệm thu:** đủ 100% nội dung nguồn · single file, không build · TOC khớp section thật · đọc được trên
màn hình nhỏ, khi in, khi tắt JavaScript và khi mất mạng · mở trang không tương tác thì không tiến trình nào
chạy · mỗi demo thao tác được và cho thấy đúng hành vi. Không dùng thư viện tô màu code (NT1).

**Trang tài liệu là một chương trình chạy trên máy người đọc — NT2 và `code-design` › Bất biến áp nguyên.** Luật khung:

| Luật | Nghĩa là |
|---|---|
| **Nội dung không phụ thuộc trang trí** | hiệu ứng (fade-in khi cuộn, thanh tiến trình) chỉ bật khi script xác nhận chạy được — một class gắn lên `<html>` ở đầu `<head>`; thiếu script, thiếu mạng, in ra giấy, `prefers-reduced-motion` thì nội dung vẫn hiện đủ, không ẩn, không chuyển động. Công thức render một lần lúc load, thư viện không tải được hay TeX sai thì hiện nguyên văn TeX, không để trống |
| **Một nguồn màu** | bảng màu khai một lần ở `:root`; Canvas không đọc được `var()` nên script đọc bảng đó **một lần** vào một object hằng, mọi lệnh vẽ dùng object đó |
| **Mỗi khối một việc, tên theo nội dung** | khối nội dung có viền · ghi chú ba sắc (mặc định, tốt, bẫy) · công thức và công thức chốt (viền nổi) · sơ đồ ASCII giữ nguyên khoảng trắng · lưới số liệu tổng kết (đặt cuối tài liệu) · danh sách bước có trạng thái đã qua · thanh đo · số đọc font mono. Bảng tự được bọc khung cuộn ngang để không bị cắt trên màn hình hẹp — việc tác giả hay quên thì script làm một lần lúc load |
| **Demo do sự kiện kích hoạt, mỗi frame tối đa một lần vẽ** | không vòng `requestAnimationFrame` chạy mãi; sự kiện chỉ đặt dirty flag và xin **một** frame, frame vẽ rồi tự xoá cờ; rời demo thì `cancelAnimationFrame`; interval dừng hẳn khi hết bước hay đặt lại; observer bỏ theo dõi phần tử ngay khi đã hiện. Phần tĩnh (nền, trục) tính **một lần**, phần động vẽ đè — tính rẻ thì làm thẳng trong handler |
| **Canvas vẽ theo toạ độ logic, nét theo mật độ điểm** | kích thước bitmap nhân `devicePixelRatio` chặn ở 2 (hơn nữa tốn pixel mà mắt không thấy khác), `ctx.scale` rồi vẽ theo cỡ logic; riêng dữ liệu pixel thô không chịu ma trận nên dựng theo cỡ bitmap |
| **Cấm trong draw loop** (đã trả giá) | `shadowBlur` · `createImageData` mỗi sự kiện · toán per-pixel mỗi `mousemove` · `innerHTML` trong vòng lặp · màu dạng `var()` đưa thẳng cho Canvas |

**Sổ tay** — bốn dạng demo đủ cho mọi hệ, chọn dạng rẻ nhất cho thấy được hành vi: *Canvas theo con trỏ*
(giá trị tại điểm chuột) · *quá trình nhiều bước* (chỉ DOM, nút tiếp/chạy/đặt lại) · *input đổi thành kết
quả* (slider → số và thanh đo) · *đồ thị tĩnh* (vẽ một lần, không listener). Trang gồm: header (tên hệ, một
câu mô tả, nhãn từ khoá) → mục lục trỏ `id` từng section → `<main>` các section → footer ghi dự án, hệ,
namespace.

## Plan — để developer tự triển khai

Tiêu chí: **tự chứa**. Developer code lại được từ đầu đến cuối mà **không phải suy đoán**, không phải mở
tài liệu khác. Các task xếp theo **thứ tự phụ thuộc**, mỗi task chỉ cần thứ đã có ở task trước. Hệ có lõi
toán thì mục `§0` của Plan dẫn giải tại chỗ theo mạch `math-physics` › Cho hiểu sâu.

**Lõi developer viết, test agent viết.** Plan **chỉ có danh sách case sẽ kiểm**, không có code test;
developer xong lõi → agent đọc code thật rồi mới viết test, vì chữ ký lúc viết Plan còn là nháp. Việc phát
sinh giữa lúc làm Plan thì đầu ra là Plan, **không tạo file code**; chỗ đặt chưa rõ thì hỏi (NT5).

**Plan chạm cả Horcrux lẫn consumer thì phần Horcrux chỉ thêm** (`O`) — member mới mang mặc định, API cũ
giữ nguyên — để task framework xong là compile sạch, consumer chưa áp dụng vẫn chạy; mỗi task là một mốc
compile sạch **và một commit riêng**. Xoá API cũ là việc riêng sau khi mọi consumer đã chuyển. **Developer
gõ lệch plan là bình thường** (NT6): sau mỗi step agent compile rồi so code với plan, phân loại theo `workflow` › Chưng cất.

**Hai loại Plan, hai hình dạng code.** Hệ **chưa có code** thì mỗi khối là **một file trọn vẹn** để chép.
Hệ **đang có code** thì Plan là **chuỗi chỗ đổi theo thứ tự**: file · dòng hiện tại · đoạn cũ → đoạn mới;
dòng không nhắc là giữ nguyên. Chép cả file cho hệ đang có là bắt developer tự so từng dòng.

**Dẫn giải của Plan dừng ở luồng; Editor setup thì đầy đủ.** Developer có agent bên cạnh để hỏi chỗ chưa
rõ; câu hỏi lặp lại thì ghi ngược vào Plan (mục Kỷ luật viết). Bước tay thiếu không có compile error nào bắt.

**Sổ tay** — kho phần cho mỗi task, chỉ lấy phần cần: bảng "Đã khảo sát" (`reuse-gate`) · Files · Interfaces
(consumes, produces, chữ ký đầy đủ) · bảng "toán → code" trỏ về `§0` · bảng lý do mỗi quyết định — *quyết
định và vì sao*, không kể code làm gì · code hoàn chỉnh dán được · Editor setup — mục riêng · bảng case
kiểm thử (input → kỳ vọng, biên theo `workflow` › Nghiệm thu) · danh sách cheat (`workflow` › Nghiệm thu). **Nghiệm thu riêng:** có "Ngữ cảnh đã
chốt" (`workflow` › Ngữ cảnh đã chốt) · mọi hàm có caller thật hoặc lý do phòng xa nói được ra (NT1) · code vừa đủ, đúng công thức
đã chốt, đúng nhịp (NT2), self-document (`code-design` › Naming) · công thức đã đối chiếu với code (`math-physics` › Đối chiếu công thức).

## Kỷ luật viết và bảo trì — áp cho mọi loại tài liệu

**Cắt trước khi giao — bắt buộc** (NT10). Bản đầu luôn có nước. Cắt theo thứ tự: câu dẫn *"phần này sẽ nói
về…"* · tóm tắt lại thứ vừa nói · câu chuyển tiếp · nhận xét về chất lượng thiết kế · ẩn dụ · cùng một ý
viết hai lần · lý do dài hơn một câu cho một khẳng định hiển nhiên. Cắt xong mà vẫn dài thì nội dung thật
sự lớn — **tách file**, đừng nén chữ. Cách viết câu theo `workflow` › Văn phong.

| Luật | Nghĩa là |
|---|---|
| **Mỗi tài liệu một người đọc** | chép nội dung loại này sang loại kia là sai cả hai; cặp `.md`–`.html` là cùng nội dung, hai người đọc. Manual viết theo **nhãn thật trên UI**, trả lời *"bấm gì ra gì, dùng khi nào"*, không tên class |
| **Không chép lại thứ người đọc đã cầm trong tay** | `.md`/`.html` cho người mở được **code**: cột "vai trò" nói lại tên hàm, `O(n log n)` của một `Sort` — bỏ. Plan cho người **chưa có code**; Manual cho người **không đọc code**. Phép kiểm: *xoá dòng này người đọc mất gì* — "mở file kia xem" thì bỏ. Ngoại lệ: chữ ký API trong `.md`, vì `.html` dựng từ nó không mở source |
| **Code đã tồn tại thì khối code trong Plan là bản sao nguyên văn** | đồng bộ bằng chép file rồi **so sánh máy**, không gõ lại (NT8). **Sổ tay** — script dò mỗi khối mở bằng `using`/`namespace` tới file `.cs` giống nhất, thay nguyên khối, chạy tới khi 0 khối lệch |
| **Thao tác Editor là một mục riêng** | mọi việc tay trong Editor gom về **một mục riêng** ở mọi đầu ra: Plan (Editor setup) · tài liệu module ("Trước khi chạy") · **câu trả lời trong chat**. Bước setup trộn vào logic là bước bị đọc lướt rồi quên |
| **Viết cho người đọc lần đầu, ở thì hiện tại** | ngoại lệ duy nhất: sổ *"đã sai một lần"* trong mục quyết định thiết kế — ghi **bài học**, không tường thuật, không lưu tên cũ. **Sổ tay** — grep "trước đây", "bản cũ", "giờ đã" |
| **Mâu thuẫn thì SỬA dòng cũ** | không thêm dòng thứ hai nói ngược. Dòng cũ ghi quyết định hoặc ranh giới do developer đặt thì không tự sửa — nêu để developer phân xử (NT5) |
| **Tri thức sinh trong hội thoại phải vào tài liệu trước khi báo hoàn thành** | *câu hỏi của người đọc* là bằng chứng tài liệu chưa rõ — câu trả lời để lại trong tài liệu · *bug sai âm thầm vừa sửa* thành một dòng "đã sai một lần" · *quy ước mới* ghi ở **tài liệu module** ở thì hiện tại, không tường thuật task. **Liệt kê nguyên văn trong báo cáo** để developer veto; đủ chung cho mọi dự án thì vào bộ skill theo `workflow` › Chưng cất |
