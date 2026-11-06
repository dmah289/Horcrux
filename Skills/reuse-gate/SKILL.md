---
name: reuse-gate
description: Dùng ở mọi câu trả lời để săn chức năng gói lại hay mang sang Horcrux được, và trước khi sinh bất kỳ code hay Plan nào: khảo sát có sẵn chưa trong hệ đang chạm, Horcrux, dự án, package; có cần không theo nhu cầu thật; đặt ở dự án hay Horcrux — đề xuất phần mang đi được kèm lý do, ưu nhược và use case, developer duyệt rồi mới viết Plan.
---

# reuse-gate — có sẵn chưa → có cần không → đặt ở đâu

Skill nền dưới `Skills/horcrux_skill.md` (NT, ba tầng, ký hiệu định nghĩa ở đó). Ba câu theo thứ tự, trước cả Plan hay đoạn
mẫu — code trong Plan là code developer gõ nguyên, thừa ở đó cũng là thừa.

## Có sẵn chưa (NT7)

Khảo sát **toàn bộ**, theo thứ tự: **hệ đang chạm và module nó gọi** → **Horcrux** (`Foundations/`, `Utilities/`, `docs/`)
→ **phần còn lại của dự án** → **package đã cài**. Cả hai cùng có thì **dùng Horcrux**, bản dự án là bản thứ hai cần đề xuất
gộp. Nói rõ đã khảo sát đâu; trong Plan là bảng **"Đã khảo sát"**: *nguồn · lấy gì · không lấy, vì sao*.

Có sẵn đáp ứng → dùng lại · gần đúng → mở rộng nếu **thêm được mà không sửa cái cũ** · phải **bẻ cong bài toán** → viết mới.
**Không dùng bản Horcrux thì đề xuất cải thiện Horcrux** (cái thiếu, cách sửa, công); bản dự án chỉ có lý do "chưa kịp tiến
độ" và là **bản tạm**. *Đã sai một lần:* viết vòng tween và công thức scale riêng trong khi Horcrux đã có runner tween và
squash-stretch.

**Bê khuôn có sẵn thì soi chỗ khuôn cũ *cố ý* làm** — lý do gốc còn đúng không; hết thì đảo và ghi lý do. Hệ **port** từ dự
án khác thì diff với **bản nguồn hiện tại** (kể cả giá trị mặc định trong scene), hỏi nhánh nào là lõi trước khi tối giản.
*Đã sai một lần:* bê khuôn serialize giá trị runtime vào asset sang bài toán lưu tiến độ — tiến độ vào version control.

**Hệ nguồn trả kiểu thì nhận kiểu, không nhận chuỗi rồi tự parse** — parse hai nơi che lỗi khỏi log hệ nguồn, mất kiểm tra
biên dịch tên field.

> **Nền tảng** — remote config và persistence của Horcrux ép kiểu qua cùng serializer: giá trị có cấu trúc khai generic với
> class dữ liệu thuần, không `string`; JSON hỏng thì hệ tự `LogError` và giữ cache, consumer không `try/catch`. Khớp
> property không phân biệt hoa thường nhưng không đổi tên: đổi tên field thì key cũ rơi thành `null` im lặng, chỉ cửa kiểm
> luật nghiệp vụ bắt được. Chi tiết: `docs/Runtime/RemoteConfigSystem/RemoteConfigSystem.md`.

## Có cần không (NT1)

**Có nhu cầu thật ngay bây giờ thì đưa vào:**
- interface, abstract → bậc cấu trúc (`code-design` › Hình dạng);
- tham số → có **call site truyền khác mặc định**;
- overload, wrapper, hàm "cho đủ bộ" → có **người gọi thứ hai**, hoặc nó mã hoá luật caller không nên biết;
- guard, nhánh biên, `try/catch` → *cái sai lộ ra lúc nào?* (`code-design` › Bất biến);
- code canh hay tự tạo thứ dựng được lúc authoring → **không bao giờ**, đó là bước setup trong tài liệu (`editor-first`);
- tối ưu làm khó đọc → hot path đã đo (NT2); tách class, component → **khác lý do thay đổi**; mục, demo → có người đọc cần.

Thứ "có thể cần sau": **rẻ** thêm sau (hàm, mục, tham số có mặc định) → ghi một dòng "Mở rộng sau" · **đắt** (chữ ký mọi
caller, đập cấu trúc, đảo chiều phụ thuộc) → làm ngay.

## Đặt ở đâu — dự án hay Horcrux (NT3)

**Săn chức năng cho Horcrux ở mọi câu trả lời** — viết code, review, giải thích, trả lời câu hỏi đều tính. Với **mỗi chức
năng — hay phần logic trong nó** — sắp viết, đang sửa, đọc ngang qua, hỏi: *gói được thành hàm, class hay service để gọi
lại không?* · *bê sang dự án khác dùng nguyên được không?* Có thì **đề xuất ngay lượt đó**; im lặng viết vào dự án thứ mang
đi được là vi phạm, vì dời chỗ đặt sau là đập cả cây phụ thuộc. Đề xuất chưa phải quyết: gói hay để lặp vẫn theo NT7 và bậc
cấu trúc (`code-design` › Hình dạng) — đề xuất nói người gọi thứ hai đã có thật chưa, developer chốt.

**Đề xuất gồm:** phần mang đi được và phần nối dự án · **tại sao** chung (không gọi type dự án; dự án sau cần ở đâu) · **ưu
và nhược** khi vào Horcrux so với ở dự án (công, độ trễ tiến độ, ràng buộc API) · **use case** — ít nhất hai kịch bản ở loại
game hay hệ khác nhau. **Chưa viết Plan khi developer chưa duyệt.** Không duyệt thì ở lại dự án, ghi "Mở rộng sau".

Nơi đặt: không gọi type dự án → Horcrux `Foundations/` (hệ) hoặc `Utilities/` (helper thuần) · dựng trên nhiều hệ Horcrux,
không mang domain → `Composites/` · gọi domain dự án → dự án · lõi chung + lớp nối → **tách**.

**Developer duyệt thì chọn đường theo tiến độ** (code runtime). Không gấp: Plan → developer gõ vào Horcrux → dự án dùng
bản Horcrux. **Ưu tiên tiến độ thì dự án trước, Horcrux sau**, bốn bước:
1. Agent viết **bản tạm** trong dự án, gom một chỗ, chữ ký theo Plan.
2. Agent viết **Plan** (`docs` › Plan) vào `Plans/`; bản tạm ghi ở "Mở rộng sau" kèm đường dẫn Plan.
3. **Developer tự gõ** vào Horcrux.
4. Agent **chuyển dự án sang bản Horcrux và xoá bản tạm** cùng một lần; gõ lệch Plan thì theo code thật. Nghiệm thu: grep
   không còn tên bản tạm, compile sạch. Bản tạm đã gắn trong asset thì `.meta` bản Horcrux nhận lại GUID của bản tạm.

**Ai gõ gì trong Horcrux.** *Code runtime:* developer tự gõ — chạy trong build mọi dự án sau nên developer phải hiểu từng
dòng; agent viết Plan, đoạn mẫu, review, test. *Code Editor* (assembly Editor, `#if UNITY_EDITOR`, `*.Editor.cs`, `Tools~`,
test): agent viết thẳng, không bản tạm, không Plan — hỏng chỉ lộ trên máy developer; developer review và veto. *Tài liệu:*
agent.
