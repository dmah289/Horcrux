---
name: docs
description: Dùng khi viết hoặc cập nhật tài liệu: .md cho agent, .html cho developer và designer dùng hệ, Plan cho developer tự gõ, Manual cho người dùng tool, file trong Agent/, và kỷ luật viết áp cho mọi loại.
---

# docs — .md, .html, Plan, Manual, Agent

Rule dưới `Agent/horcrux.md` (NT, tầng, ký hiệu định nghĩa ở đó).

Mỗi hệ và tool có tài liệu riêng, **không đặt cạnh code**, tên theo tên hệ, `.md` mở đầu bằng dòng `Code:` trỏ thư mục code
tính từ gốc Horcrux. **Hệ Runtime có đủ cặp** trong `docs/Runtime/<Hệ>/` — utility thuần cũng là hệ Runtime; **tool Editor
chỉ `.md`**, nằm phẳng ở `docs/Editor/`; Plan ở `Plans/`. **Thay đổi hệ thì cập nhật tài liệu trong cùng lần làm.**

| Loại | Người đọc, vai trò | Vòng đời |
|---|---|---|
| **`.md`** | **agent** — bản đặc, rẻ token, đủ mọi tầng tới code; nguồn sinh `.html` | cùng lần làm với mỗi thay đổi |
| **`.html`** | **developer, designer** — dùng hệ không cần đọc code; agent không đọc khi đã có `.md` | cùng lần làm với `.md` |
| **Plan** | developer **tự gõ** — khi được yêu cầu, và code runtime vào Horcrux đã duyệt | hệ dựng xong có tài liệu module thì xoá Plan |
| **Manual** | người dùng tool không phải developer, để **thao tác** | cạnh tài liệu của tool |
| **Rule, Skill, Memory** | agent mọi dự án — `Agent/`: luật chung, quy trình một loại việc, bài học đã trả giá | qua cổng ghi (`horcrux.md` › Cổng ghi) |

**Agent viết cả năm loại;** nghiệm thu là đối chiếu máy móc với code; developer chốt nội dung. Quy trình: phỏng vấn và đối
chiếu (`workflow`) → đọc **tất cả** source, hiểu data flow, lifecycle, lý do mỗi quyết định → `.md` → `.html` chọn từ `.md`.

**Tài liệu Horcrux chung cho mọi dự án** (NT8): ví dụ tên trung tính (`Piece`, `IMySaveData`); không tên, class, đường dẫn
của dự án; không kịch bản chơi thử; không preset số cho một game cụ thể.

**Chuỗi sự thật một chiều `code → .md → .html`:** code là chuẩn; `.html` là **tập con chọn lọc** của `.md`, dựng được từ
`.md` không mở source. Lệch thì sửa xuôi chuỗi.

## `.md` — tài liệu cho agent

Tổ chức theo **đường đi của dữ liệu** (input → processing → output), luồng vẽ ASCII; code chỉ xuất hiện khi đoạn code *là*
thứ cần minh hoạ. **Mục bắt buộc "Trước khi chạy" với hệ cần wire tay:** người chưa từng mở hệ dựng lại được **từ 0** chỉ
bằng mục này, viết bằng **thao tác và nhãn thật trên UI**, mỗi bước kèm *"thiếu thì hỏng ở đâu"*. **Còn lại là kho mục để
chọn, không phải form** — mỗi mục gọi tên được câu hỏi người đọc nó trả lời.

## `.html` — tài liệu dùng hệ cho developer

Người đọc muốn **dùng hệ đúng mà không mở code**. Bốn phần, đều chọn từ `.md`: **Kịch bản** (tình huống xảy ra thì hệ làm
gì, người chơi thấy gì) · **Cách dùng** (dựng từ 0, gọi gì, đọc gì) · **Flow** · **Lưu ý** (chỗ dễ dùng sai). Thứ chỉ người
sửa hệ cần thì không mang sang. **Hình thức:** biểu đồ → bảng → văn xuôi chỉ khi hai dạng trước không biểu thị được.
**Trang là chương trình chạy trên máy người đọc** — NT3 áp nguyên; nội dung không phụ thuộc trang trí. `.md` đổi thì soát
lại **cả trang**, không chỉ phần vừa đổi.

## Plan — để developer tự triển khai

Developer gõ lại tập trung vào **kịch bản, thuật toán, logic, flow, code** — Plan chỉ mang những thứ đó; văn xuôi là
phương án cuối.

- **Bảy khối, thứ tự cố định:** dòng mở · Kịch bản · Flow · Thuật toán (khi có lõi) · giai đoạn developer gõ · Chuyển dự
  án (agent) · Không làm · Mở rộng sau. Khảo sát nằm ở đề xuất trong chat, không ở Plan.
- **Gõ xong là hiểu hệ, không chỉ hệ chạy:** ai gọi thấy ở sơ đồ, vì sao thấy ở comment trong code (trần `code-design` ›
  Naming) và dòng Không làm — không đoạn văn quyết định. **Tự chứa**, kể cả Editor setup — bước tay thiếu không compile
  error nào bắt.
- **Dựng theo luồng, chia theo giai đoạn — không theo file:** mỗi giai đoạn nối thêm một chặng, kết ở trạng thái **chạy
  được, thấy được**; member thêm vào có caller thật trong cùng giai đoạn (NT2). Lá trước gốc là gõ class chưa biết ai dùng.
- **Code trong Plan là code developer gõ nguyên và tin:** khớp file thật ở chỗ neo, thuật toán đã qua bản mẫu chạy được.
- **Lõi developer viết, test agent viết.** Việc phát sinh lúc làm Plan thì đầu ra là Plan, không tạo file code.

## Agent

- **`RULE.md` và `SKILL.md` chỉ giữ luật và tư tưởng** — thứ đúng cho mọi game Unity, viết ở mức *cần đạt gì*, mỗi tư
  tưởng một câu, không trình tự cứng. Chi tiết triển khai của luật, Nền tảng, Sổ tay vào `reference.md`, mỗi mục có một
  hàng trong bảng Tra cứu; skill ngắn không có `reference.md` thì giữ Sổ tay tại chỗ.
- **Một luật một nơi.** Thêm dòng trước hết là **gộp vào luật đang có**; luật mới phải qua phép kiểm *đúng cho ít nhất hai
  loại game* và developer duyệt.
- **Bài học vào `Memory/`**, đúng hình dạng ở `horcrux.md` › Memory: tiêu đề là dấu hiệu, tên trung tính — không tường
  thuật, không tên màn, tính năng, kịch bản của game nào.
- **Trần** ở `horcrux.md` › Cổng ghi. Phần đọc đầu phiên thêm vào thì cắt chỗ khác; `RULE.md` vượt trần là dấu hiệu chi
  tiết chưa dời sang tra cứu.
- **Sửa luật thì đo, không đoán** (`Agent/Tools~/`): context nạp trước–sau (`context_audit.py plan`) · ca hành vi chạm
  luật đó, trước và sau khi sửa (`agent_evals.py run --rule <tên>`). Dọn hay đề xuất nâng bậc lấy số từ
  `context_audit.py hooks` (luật hook chặn nhiều) và `files` (file nặng mà ít phiên cần, file không phiên nào đọc).

## Kỷ luật viết — mọi loại tài liệu

**Cắt trước khi giao — bắt buộc:** câu dẫn · tóm tắt lại · câu chuyển · nhận xét chất lượng thiết kế · ẩn dụ · một ý hai
lần · lý do dài cho khẳng định hiển nhiên. Cắt xong vẫn dài thì **tách file**.

- **Mỗi tài liệu một người đọc.** Chép nội dung loại này sang loại kia là sai cả hai. Manual theo **nhãn thật trên UI** —
  *bấm gì ra gì, dùng khi nào*, không tên class.
- **Không chép lại thứ người đọc đã cầm:** *xoá dòng này người đọc mất gì?* — "mở file kia xem" thì bỏ. Ngoại lệ: chữ ký
  API trong `.md`.
- **Thao tác Editor là một mục riêng** ở mọi đầu ra: Plan (Editor setup) · tài liệu ("Trước khi chạy") · **trả lời chat**.
- **Viết cho người đọc lần đầu, ở thì hiện tại** (NT12); ngoại lệ là bẫy — ghi bài học, không tường thuật.
- **Mâu thuẫn thì sửa dòng cũ**, không thêm dòng nói ngược. Ngoại lệ: dòng developer đặt và luật trong rule và skill — nêu để
  developer phân xử.
- **Tri thức sinh trong hội thoại vào tài liệu trước khi báo xong:** câu hỏi của người đọc → câu trả lời ở lại tài liệu ·
  bug âm thầm vừa sửa → một dòng bẫy · quy ước mới → tài liệu module. **Liệt kê nguyên văn trong báo cáo**; đủ chung thì
  theo `workflow` › Chưng cất.

## Tra cứu — `docs/reference.md`

| Chạm | Mục |
|---|---|
| viết `.md` của hệ, công thức KaTeX, đồng bộ tài liệu | `.md` |
| viết, sửa, nghiệm thu trang `.html`; demo, Canvas | `.html` |
| viết, sửa Plan; so code developer gõ với Plan | Plan |
