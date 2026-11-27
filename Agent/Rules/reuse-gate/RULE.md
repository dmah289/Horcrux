---
name: reuse-gate
description: Dùng ở mọi câu trả lời và trước khi sinh code hay Plan: có sẵn chưa, có cần không, đặt ở dự án hay Horcrux, ai gõ.
---

# reuse-gate — có sẵn chưa → có cần không → đặt ở đâu

Rule dưới `Agent/horcrux.md` (NT, tầng, ký hiệu định nghĩa ở đó). Ba câu theo thứ tự, trước cả Plan hay đoạn
mẫu — code trong Plan là code developer gõ nguyên, thừa ở đó cũng là thừa.

## Có sẵn chưa (NT6)

Khảo sát **toàn bộ**, theo thứ tự: **hệ đang chạm và module nó gọi** → **Horcrux** (`Foundations/`, `Utilities/`, `docs/`,
`Plans/`) → **phần còn lại của dự án** → **package đã cài**. Cả hai cùng có thì **dùng Horcrux**, bản dự án là bản thứ hai
cần đề xuất gộp. Đề xuất có bảng **"Đã khảo sát"**: *nguồn · lấy gì · không lấy, vì sao*.

- **Khảo sát gồm cả quyết định đã ghi** của dự án (bố cục, lớp vẽ, cấu trúc) và **đường di sản song song** cho cùng việc:
  thiết kế trái quyết định cũ không compile error nào bắt.
- **Script dùng một lần cũng là code:** eval dựng, dump, chụp, script đo đi qua cùng câu hỏi trước dòng đầu — viết tay thì
  số của hai lần đo không so được với nhau.
- **Không dùng bản Horcrux thì đề xuất cải tiến Horcrux** (cái thiếu, cách sửa, công) và áp theo mục Đặt ở đâu; bản dự án
  chỉ có lý do "chưa kịp tiến độ" và là **bản tạm**.
- **Chức năng đã có code hay Plan thì đề xuất tối ưu trên bản đó**, không dựng bản song song. Chức năng mới dựa trên Plan
  còn chờ gõ thì coi Plan đó đã triển khai — viết theo API trong Plan, ghi ở đầu Plan mới *phụ thuộc Plan nào*.
- **Bê khuôn có sẵn thì soi chỗ khuôn cũ *cố ý* làm** — lý do gốc còn đúng không; hết thì đảo và ghi lý do. **Hệ nguồn trả
  kiểu thì nhận kiểu**, không nhận chuỗi rồi tự parse.

## Có cần không (NT2)

**Có nhu cầu thật ngay bây giờ thì đưa vào:** interface, abstract → bậc cấu trúc (`code-design` › Hình dạng) · tham số →
có **call site truyền khác mặc định** · overload, wrapper, hàm "cho đủ bộ" → có **người gọi thứ hai**, hoặc nó mã hoá luật
caller không nên biết · guard, nhánh biên, `try/catch` → *cái sai lộ ra lúc nào?* (`code-design` › Bất biến) · code canh hay tự tạo
thứ dựng được lúc authoring → **không bao giờ** (NT5) · tối ưu làm khó đọc → hot path đã đo (NT3) · tách class,
component → **khác lý do thay đổi** · mục, demo → có người đọc cần.

Thứ "có thể cần sau": **rẻ** thêm sau (hàm, mục, tham số có mặc định) → ghi một dòng "Mở rộng sau" · **đắt** (chữ ký mọi
caller, đập cấu trúc, đảo chiều phụ thuộc) → làm ngay.

## Đặt ở đâu — dự án hay Horcrux (NT4, NT7, NT8)

**Đề xuất gồm:** phần mang đi được và phần nối dự án · **tại sao** chung (không gọi type dự án; dự án sau cần ở đâu) · **ưu
và nhược** khi vào Horcrux so với ở dự án (công, độ trễ tiến độ, ràng buộc API) · **use case** — ít nhất hai kịch bản ở
loại game hay hệ khác nhau · người gọi thứ hai đã có thật chưa. **Chưa viết Plan khi developer chưa duyệt;** không duyệt thì
ở lại dự án, ghi "Mở rộng sau".

**Nơi đặt:** không gọi type dự án → Horcrux `Foundations/` (hệ độc lập) hoặc `Utilities/` (helper thuần, không phụ thuộc hệ
nào) · dựng trên nhiều hệ Horcrux, không mang domain → `Composites/` · bọc SDK bên thứ ba → `Vendors/<Vendor>/`, namespace
`Horcrux.Vendors.<Vendor>`, mỗi package phụ thuộc thêm một assembly con · gọi domain dự án → dự án · lõi chung + lớp nối →
**tách** (NT8). Thư mục, namespace chi tiết: `code-design` › tra cứu › Module.

**Ai gõ gì trong Horcrux.**

| Loại | Ai viết | Vì |
|---|---|---|
| code runtime | **developer tự gõ** theo Plan; agent viết Plan, đoạn mẫu, review, test | chạy trong build mọi dự án sau, developer phải hiểu từng dòng |
| code Editor (assembly Editor, `#if UNITY_EDITOR`, `*.Editor.cs`, `Tools~`, test) | **agent viết thẳng**, không bản tạm, không Plan; developer review và veto — trừ khi developer chọn tự gõ, khi đó đi đường của code runtime | hỏng chỉ lộ trên máy developer |
| tài liệu, rule, skill, memory | agent, theo quyền ở `docs` › Agent | |

**Đường của code runtime theo tiến độ.** Không gấp: Plan → developer gõ vào Horcrux → dự án dùng bản Horcrux. **Ưu tiên
tiến độ thì dự án trước, Horcrux sau:** ① agent viết **bản tạm** trong dự án, gom một chỗ, chữ ký theo Plan · ② agent viết
**Plan** (`docs` › Plan) vào `Plans/`, bản tạm ghi ở "Mở rộng sau" kèm đường dẫn Plan; bản tạm còn sửa thì Plan sửa **cùng
lượt** · ③ **developer tự gõ** vào Horcrux · ④ agent **chuyển dự án sang bản Horcrux và xoá bản tạm** cùng một lần, gõ lệch
Plan thì theo code thật; nghiệm thu: grep không còn tên bản tạm, compile sạch.

## Tra cứu — `reuse-gate/reference.md`

| Chạm | Mục |
|---|---|
| port hệ từ dự án khác, bê khuôn, dự án có hai đường cho cùng việc | Khảo sát |
| đọc remote config, persistence, dữ liệu có cấu trúc | Dữ liệu có kiểu |
| chuyển dự án từ bản tạm sang bản Horcrux | Chuyển bản tạm |
