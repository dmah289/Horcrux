---
name: docs
description: Dùng khi viết hoặc cập nhật tài liệu (.md cho agent, .html cho developer và designer, Plan cho developer tự gõ, Manual cho người dùng tool), khi ghi tri thức vào Agent/ hay memory (cổng ghi, hình dạng, vòng đời, dọn), và ở bước quét cuối chat.
---

# docs — .md, .html, Plan, Manual, Agent

Rule dưới `Agent/horcrux.md` (NT, tầng, ký hiệu định nghĩa ở đó).

Mỗi hệ và tool có tài liệu riêng, **không đặt cạnh code**, tên theo tên hệ, `.md` mở đầu bằng dòng `Code:` trỏ thư mục code
tính từ gốc Horcrux. **Hệ Runtime có đủ cặp** `.md` + `.html` trong `docs/Runtime/<Hệ>/` — utility thuần cũng là hệ Runtime;
**tool Editor chỉ `.md`**, phẳng ở `docs/Editor/`; Plan ở `Plans/`. **Thay đổi hệ thì cập nhật tài liệu trong cùng lần làm.**

| Loại | Người đọc, vai trò | Vòng đời |
|---|---|---|
| **`.md`** | **agent** — bản đặc, đủ mọi tầng tới code; nguồn sinh `.html` | cùng lần làm với mỗi thay đổi |
| **`.html`** | **developer, designer** — dùng hệ không cần đọc code; agent không đọc khi đã có `.md` | cùng lần làm với `.md` |
| **Plan** | developer **tự gõ** — khi được yêu cầu, và code runtime vào Horcrux đã duyệt | hệ dựng xong có tài liệu module thì xoá Plan |
| **Manual** | người dùng tool không phải developer, để **thao tác** | cạnh tài liệu của tool |
| **Rule, Skill, Reference, Memory** | agent mọi dự án — mục Agent | qua cổng ghi (mục Agent) |

Agent viết mọi loại; nghiệm thu là đối chiếu máy móc với code; developer chốt nội dung. Quy trình: phỏng vấn và đối chiếu
(`workflow`) → đọc **tất cả** source, hiểu data flow, lifecycle, lý do mỗi quyết định → `.md` → `.html` chọn từ `.md`.
**Chuỗi sự thật một chiều `code → .md → .html`:** `.html` là tập con chọn lọc của `.md`, dựng được không mở source; lệch thì
sửa xuôi chuỗi. **Tài liệu Horcrux** theo NT8: ví dụ tên trung tính (`Piece`, `IMySaveData`), không kịch bản chơi thử, không
preset số cho một game.

## `.md` — tài liệu cho agent

Tổ chức theo **đường đi của dữ liệu** (input → processing → output), luồng vẽ ASCII; code chỉ xuất hiện khi đoạn code *là*
thứ cần minh hoạ. **Mục bắt buộc "Trước khi chạy" với hệ cần wire tay:** người chưa từng mở hệ dựng lại được **từ 0** chỉ
bằng mục này, viết bằng **thao tác và nhãn thật trên UI**, mỗi bước kèm *"thiếu thì hỏng ở đâu"*. Còn lại là kho mục để
chọn, không phải form — mỗi mục gọi tên được câu hỏi người đọc nó trả lời.

## `.html` — tài liệu dùng hệ cho developer

Bốn phần chọn từ `.md`: **Kịch bản** · **Cách dùng** · **Flow** · **Lưu ý**; thứ chỉ người sửa hệ cần thì không mang sang.
Biểu đồ → bảng → văn xuôi chỉ khi hai dạng trước không biểu thị được. **Trang là chương trình chạy trên máy người đọc** —
NT3 áp nguyên; nội dung không phụ thuộc trang trí. `.md` đổi thì soát lại **cả trang**.

## Plan — để developer tự triển khai

Developer gõ lại để **hiểu hệ, không chỉ để hệ chạy**: Plan mang kịch bản, flow, thuật toán, code; văn xuôi là phương án
cuối, lý do nằm ở comment *vì sao* trong code và dòng Không làm. **Tự chứa**, kể cả Editor setup. **Dựng theo luồng, chia
theo giai đoạn — không theo file:** mỗi giai đoạn nối thêm một chặng, kết ở trạng thái **chạy được, thấy được**; member thêm
vào có caller thật trong cùng giai đoạn (NT2). **Code trong Plan là code developer gõ nguyên và tin:** khớp file thật ở chỗ
neo, đã qua compiler, thuật toán đã qua bản mẫu. Lõi developer viết, test agent viết; việc phát sinh lúc làm Plan thì đầu ra
là Plan, không tạo file code.

## Agent

Ghi tri thức là một tác vụ như mọi tác vụ: Luật đọc đầu phiên (`horcrux.md`) đã xong trước bước nào dưới đây.

**Rule, skill, reference.** `RULE.md` và `SKILL.md` chỉ giữ luật và tư tưởng — thứ đúng cho mọi game Unity, viết ở mức *cần
đạt gì*, mỗi tư tưởng một câu, không trình tự cứng, không lệnh chi tiết. Nền tảng, Sổ tay, trình tự đã chạy được vào
`reference.md`, mỗi mục một hàng bảng Tra cứu; cách dùng tool ở `docs/Editor/<Tool>.md`. **Một luật một nơi:** thêm dòng
trước hết là gộp vào luật đang có.

**Cổng ghi** — một điều mới đi từ trên xuống, dừng ở câu đầu tiên trả lời "có":

| # | Câu hỏi | Có → ghi vào | Quyền |
|---|---|---|---|
| 0 | Có "vì sao" lặp lại được không? | *Không → không ghi* (hằng số, tên, trạng thái task, thứ git đã ghi) | |
| 1 | Chỉ đúng cho bài toán hoặc hệ đang làm? | Plan (đang dựng) · tài liệu module (đã dựng) | ghi thẳng |
| 2 | Chỉ đúng cho máy này — đường dẫn cài đặt, bản đã cài, cấu hình local? | memory máy | ghi thẳng |
| 3 | Chỉ đúng cho dự án này? **Khái quát trước**: phần cơ chế đi tiếp xuống câu 5–7, chỉ phần từ vựng dừng ở đây | *ràng buộc* → `CLAUDE.md` của dự án · *sự thật, bài học, quyết định* → `docs/agent-memory/` | đề xuất · ghi thẳng |
| 4 | Nói về chính developer? | mục developer-only | **agent không ghi**, nêu trong báo cáo |
| 5 | Là tư tưởng và công cụ cho một loại việc sẽ lặp lại? | `Skills/<việc>/` (trình tự đã chạy được vào `reference.md` của skill) | đề xuất |
| 6 | Là **lựa chọn** của ta — có phương án hợp lệ khác mà ta không chọn? Đúng cho ít nhất hai loại game? | `Rules/<miền>/RULE.md` | đề xuất |
| 7 | Là **sự thật** hay **bài học** về engine, thư viện, công cụ, cách làm việc? | `Memory/<miền>.md`; chưa rõ miền thì `Memory/inbox.md` | ghi thẳng |

**Khái quát trước khi hạ tầng:** tách *cơ chế* (vì sao vấp, cách làm) khỏi *từ vựng* (tên type, đường dẫn, con số, scene).
Cơ chế viết không gọi tên dự án và lên tầng chung; tầng dự án chỉ giữ từ vựng, trỏ về khối chung; không tách được cơ chế
nào mới ghi trọn ở dự án. Memory ba tầng, ghi ở tầng
**cao nhất** còn đúng vì máy khác chỉ nhận hai tầng đầu: **chung** `Memory/` · **dự án** `docs/agent-memory/<cùng tên>.md`,
đi theo git · **máy** — chỉ đường dẫn cài đặt, bản đã cài, cấu hình local. Quyết định của developer ở tầng dự án ghi
`### <chủ đề>` · `` `YYYY-MM` · developer chốt `` rồi nội dung.

**Qua cổng rồi đối chiếu với nội dung cũ** — mọi rule, skill, memory cùng chủ đề ở cả ba tầng: đã có thì thôi · là trường
hợp riêng của luật đang có thì trỏ về · **bổ trợ nhau hoặc là bản cải tiến thì tổng hợp thành một tri thức** — viết lại khối
cũ bao trọn cả hai, xoá bản thừa, sửa con trỏ, khối memory gộp thì cộng số vấp; tổng hợp theo quyền của file đích · mâu thuẫn thì sửa dòng cũ; mâu thuẫn với
dòng developer đặt hay luật trong rule, skill thì nêu để developer phân xử.

**Khối memory:**

```markdown
### <dấu hiệu nhận ra — không phải tên giải pháp>
`<YYYY-MM>` · vấp <n> · <đã đo | đọc tài liệu | suy luận | chuyển sang, chưa kiểm lại> · đúng với <điều kiện, nếu có>
**Thấy:** <triệu chứng>. **Vì:** <nguyên nhân>. **Làm:** <cách làm>.
```

- Tên trung tính, không tường thuật, không tên màn, tính năng, kịch bản của game nào. **Ghi ngay lúc vấp**, không đợi cuối
  phiên; trước khi ghi tìm dấu hiệu ở cả ba tầng: đã có thì sửa khối cũ; khối ở tầng thấp mà cơ chế khái quát được thì
  dời lên.
- **Vấp lại dù đã ghi** thì tăng số vấp — chỉ khi đó. Lên 2 thì **buộc đề xuất nâng cấp**, bậc cao nhất làm được, nâng xong
  xoá khối: ① *sửa gốc* — đổi API, tool của Horcrux để không vấp được nữa (đề xuất) · ② *máy canh* — hook, phép kiểm trong
  tool (đề xuất) · ③ *thành luật* (đề xuất) · ④ *thành tra cứu* — sự thật ổn định về engine, thư viện → `reference.md`
  (ghi thẳng, báo để veto).
- **Xoá** khi điều kiện "đúng với" đã hết hoặc hệ liên quan đã viết lại. **Dọn** khi file vượt trần hoặc developer yêu cầu:
  gộp khối trùng · kiểm lại khối *suy luận*, *chuyển sang* · khối trên sáu tháng mà vấp 1 thì xác minh hoặc xoá · xếp
  `inbox.md` về miền · báo bảng *giữ / gộp / nâng / xoá*. Xoá, dời khối bằng `Agent/Tools~/check_agent.py memory drop|move`.

**Trần:** `horcrux.md` 26 KB · mỗi `RULE.md` 13 KB · mỗi file memory 8 KB; vượt thì không ghi thêm tới khi dọn hoặc dời bớt —
`RULE.md` vượt trần là dấu hiệu chi tiết chưa dời sang tra cứu. Phần đọc đầu phiên thêm vào thì cắt chỗ khác. Kiểm bằng
`Agent/Tools~/check_agent.py all`. **Sửa luật thì đo** (`Agent/Tools~/`): context nạp trước–sau (`context_audit.py plan`) ·
ca hành vi chạm luật đó (`agent_evals.py run --rule <tên>`); dọn hay đề xuất nâng lấy số từ `context_audit.py hooks`, `files`.

**Báo cáo cuối phiên** kết bằng khối:

```
Đã ghi      <file> · "<tiêu đề>" (mới | vấp n → n+1)
Đề xuất     <nâng bậc nào, hoặc luật mới cho file nào> — vì <lý do>
Về bạn      <nhận xét cho mục developer-only, nếu có>
Không ghi   <nói rõ khi phiên không có gì>
```

## Kỷ luật viết — mọi loại tài liệu

Thêm vào NT12. **Cắt trước khi giao:** câu dẫn · tóm tắt lại · câu chuyển · nhận xét chất lượng thiết kế · ẩn dụ · một ý
hai lần · lý do dài cho khẳng định hiển nhiên; cắt xong vẫn dài thì **tách file**. Bảng cho so sánh, diagram cho luồng, công
thức cho định lượng, một câu cho trực giác; dễ → khó, tổng quan → chi tiết.

- **Mỗi tài liệu một người đọc.** Chép nội dung loại này sang loại kia là sai cả hai. Manual theo **nhãn thật trên UI** —
  *bấm gì ra gì, dùng khi nào*, không tên class.
- **Không chép lại thứ người đọc đã cầm:** *xoá dòng này người đọc mất gì?* — "mở file kia xem" thì bỏ. Ngoại lệ: chữ ký
  API trong `.md`.
- **Thao tác Editor là một mục riêng** ở mọi đầu ra: Plan (Editor setup) · tài liệu ("Trước khi chạy") · báo cáo chat.
- **Viết cho người đọc lần đầu, ở thì hiện tại**; ngoại lệ là bẫy — ghi bài học, không tường thuật.
- **Mâu thuẫn thì sửa dòng cũ**, không thêm dòng nói ngược. Ngoại lệ: dòng developer đặt và luật trong rule, skill — nêu để
  developer phân xử.
- **Tri thức sinh trong hội thoại vào tài liệu trước khi báo xong:** câu hỏi của người đọc → câu trả lời ở lại tài liệu ·
  bug âm thầm vừa sửa → một dòng bẫy · quy ước mới → tài liệu module. Liệt kê nguyên văn trong báo cáo; đủ chung thì qua
  cổng ghi.

## Tra cứu — `docs/reference.md`

| Chạm | Mục |
|---|---|
| viết `.md` của hệ, công thức KaTeX, đồng bộ tài liệu | `.md` |
| viết, sửa, nghiệm thu trang `.html`; demo, Canvas | `.html` |
| viết, sửa Plan; so code developer gõ với Plan | Plan |
