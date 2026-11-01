---
name: reuse-gate
description: Dùng trước khi sinh bất kỳ code hay Plan nào: khảo sát có sẵn chưa trong hệ đang chạm, Horcrux, dự án, package; có cần không theo nhu cầu thật; đặt ở dự án hay Horcrux và bốn bước chuyển bản tạm vào Horcrux để developer gõ.
---

# reuse-gate — có sẵn chưa → có cần không → đặt ở đâu

Skill nền của Horcrux, đứng dưới `Skills/horcrux_skill.md`: `NT<n>`, ba tầng Luật · Nền tảng · Sổ tay và bảng
chỉ dẫn định nghĩa ở đó. Con trỏ `` `skill` › Mục `` trỏ sang skill khác; `mục X` trỏ trong file này.

Ba câu theo đúng thứ tự, trước cả khi viết Plan hay đoạn mẫu — code trong Plan là code developer gõ nguyên,
thừa ở đó cũng là thừa.

## Có sẵn chưa (NT7)

Khảo sát **toàn bộ**, theo thứ tự: **hệ đang chạm và module nó gọi** → **Horcrux** (`Foundations/`,
`Utilities/`) → **phần còn lại của dự án** → **package đã cài**. Cả dự án lẫn Horcrux đều có thì **ưu tiên
Horcrux**; bản dự án khi đó là bản thứ hai cần đề xuất gộp. Nói rõ đã khảo sát đâu (NT8): trong Plan là
bảng **"Đã khảo sát"** *nguồn · lấy gì · không lấy, vì sao* — cột cuối để người sau không bê lại cái đã loại.

| Cái có sẵn | Xử lý |
|---|---|
| Đáp ứng yêu cầu | dùng lại, không viết bản thứ hai |
| Gần đúng nhưng thiếu | mở rộng nếu **thêm được mà không sửa cái cũ**; không được thì viết mới |
| Không có, hoặc phải **bẻ cong bài toán** cho vừa | viết mới — tái sử dụng không phải lý do làm sai bài toán |

**Không dùng bản Horcrux thì phải đề xuất cải thiện Horcrux** — cái thiếu, cách sửa, công. Lý do có bản dự
án không bao giờ là "Horcrux không có", chỉ là "chưa kịp tiến độ" — và khi đó nó là **bản tạm** của bốn
bước ở "Đặt ở đâu" (NT3). *Đã sai một lần:* viết vòng tween và công thức scale riêng cho một hiệu ứng bay
trong khi Horcrux đã có runner tween và class squash-stretch.

**Bê khuôn có sẵn thì soi kỹ đúng những chỗ khuôn cũ *cố ý* làm** — với mỗi quyết định cố ý, hỏi lý do gốc
còn đúng ở bài toán này không; hết thì đảo và ghi lý do tại đó. Hệ **port** từ dự án khác thì diff với
**bản nguồn hiện tại** và hỏi nhánh nào là lõi trước khi tối giản. *Đã sai một lần:* bê khuôn cố ý serialize
giá trị runtime vào asset sang bài toán lưu tiến độ người chơi — tiến độ đi vào asset rồi vào version control.

**Hệ nguồn đã trả về kiểu thì nhận kiểu, không nhận chuỗi rồi tự parse.** Chuỗi trung gian là parse ở hai
nơi: che lỗi khỏi đường log của hệ nguồn, mất kiểm tra biên dịch trên tên field. Phía consumer chỉ còn
**kiểm luật nghiệp vụ** trên object đã có kiểu (`code-design` › Bất biến).

> **Nền tảng** — remote config và persistence của Horcrux ép kiểu qua cùng một serializer: giá trị có cấu
> trúc khai generic với class dữ liệu thuần, **không** khai `string`; JSON hỏng hệ tự `LogError` và giữ
> cache, consumer không `try/catch`. Serializer khớp property không phân biệt hoa thường nhưng không đổi
> tên: đổi tên field thì key cũ rơi thành `null` im lặng, chỉ cửa kiểm luật nghiệp vụ bắt được. Chi tiết:
> `RemoteConfigSystem.md`.

## Có cần không (NT1)

**Có nhu cầu thật ngay bây giờ thì đưa vào.**

| Thứ định thêm | Nhu cầu thật là |
|---|---|
| Interface, abstract | theo bậc cấu trúc `code-design` › Hình dạng: **implementation thứ hai đang có thật**, hoặc framework phải gọi sang assembly vendor nó không được tham chiếu |
| Tham số | có **call site truyền khác mặc định** |
| Overload, wrapper, hàm "cho đủ bộ" | có **người gọi thứ hai** cần đúng nó, hoặc nó mã hoá một luật caller không nên biết. Họ helper cùng tiền tố không kéo nghĩa vụ dựng đủ bộ; hai hàm gọi cùng thân với hai bộ hằng trong khi caller đã nói hướng bằng trạng thái của nó là tên nói hai lần một điều |
| Guard, nhánh biên, `try/catch` | theo câu hỏi duy nhất ở `code-design` › Bất biến: *cái sai đó lộ ra lúc nào?* |
| Code canh hay tự tạo thứ dựng được lúc authoring | **không có nhu cầu**: đó là bước setup, viết vào tài liệu (`editor-first`) |
| Tối ưu làm code khó đọc hơn | hot path đã xác nhận bằng số đo (NT2) |
| Tách lớp, class, component | **khác lý do thay đổi** (`code-design` › Hình dạng) |
| Chương, mục, demo | có người đọc cần nó để làm một việc cụ thể |

Thứ "có thể cần sau" chia theo **giá thêm sau** (NT1): **rẻ** — thêm hàm, mục mới, tham số tuỳ chọn có mặc
định (call site cũ không đổi) — thì để lại, ghi một dòng ở "Mở rộng sau" · **đắt** — chữ ký mọi caller
phải sửa, đập cấu trúc, đảo chiều phụ thuộc — thì làm ngay. Tính mở rộng đến từ Open/Closed, không từ viết
sẵn thứ chưa ai cần.

## Đặt ở đâu — dự án hay Horcrux (NT3)

**Săn chức năng cho Horcrux suốt task, không chỉ lúc sắp viết mới.** Với **mỗi chức năng** chạm tới — sắp
viết, đang sửa, đọc ngang qua — hỏi: *bê sang dự án khác dùng nguyên được không?* Có thì **đề xuất ngay
trong lượt đó**: phần mang đi được · phần nối dự án · công. **Im lặng viết vào dự án thứ vốn mang đi được
là vi phạm**, vì sửa chỗ đặt sau là đập cả cây phụ thuộc. Phân loại là đề xuất kèm giá, developer quyết (NT5).

| Chức năng | Nơi đặt | Ai viết |
|---|---|---|
| Không gọi tên type dự án; dự án sau cũng cần | Horcrux — `Foundations/` nếu là hệ, `Utilities/` nếu là helper thuần | runtime: **developer gõ theo Plan** (`docs` › Plan) · Editor và tài liệu: agent |
| Dựng trên nhiều hệ Horcrux, vẫn không mang domain dự án | Horcrux — `Composites/` | như trên |
| Gọi tên domain của dự án | dự án | agent |
| Lõi chung + lớp nối dự án | **tách**: lõi vào Horcrux theo Plan; lớp nối agent viết trong dự án | cả hai |

**Developer đồng ý thì đi bốn bước — tiến độ trước, Horcrux sau** (cho code runtime; code Editor agent
viết thẳng vào Horcrux, không bản tạm, không Plan):

1. **Agent viết bản tạm trong dự án** để task không đứng chờ — gom một chỗ, chữ ký theo Plan ở bước 2.
2. **Agent viết Plan** cho phần Horcrux (`docs` › Plan), đặt trong `Plans/` ở gốc Horcrux — một thư mục đủ thấy mọi
   việc Horcrux đang nợ. Bản tạm ghi ở "Mở rộng sau" kèm đường dẫn Plan, để nó không trôi thành bản vĩnh viễn.
3. **Developer tự gõ** vào Horcrux theo Plan.
4. **Agent chuyển dự án sang bản Horcrux và xoá bản tạm** trong cùng một lần (NT7). Developer gõ lệch Plan
   thì theo code thật (`docs` › Plan). Nghiệm thu: grep không còn tên bản tạm, compile sạch. Bản tạm đã gắn trong
   asset thì `.meta` bản Horcrux nhận lại GUID của bản tạm, nếu không mọi chỗ đã gắn thành Missing Script.

**Ai gõ gì — luật cho mọi code vào Horcrux.** *Code runtime* developer tự gõ, trong hay ngoài Plan:
framework chạy trong build của mọi dự án sau nên developer phải hiểu và chịu trách nhiệm từng dòng; agent
viết Plan, đoạn mẫu, review, test. *Code Editor* — assembly chỉ có ở Editor, `#if UNITY_EDITOR`, partial
`*.Editor.cs`, script ngoài Unity (`Tools~`), test — agent viết thẳng: hỏng thì lộ trên máy developer,
không tới người chơi; developer review và veto. *Tài liệu* agent viết (`docs`). Áp cho cả việc nâng chức năng
đang có lên Horcrux khi có người dùng thứ hai (`code-design` › Module).
