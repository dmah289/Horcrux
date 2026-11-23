---
name: editor-first
description: Dùng khi quyết thứ gì làm ở Editor thay vì code, khi viết tool Editor IMGUI, hoặc khi tool đọc–ghi dữ liệu của người dùng (import/export).
---

# editor-first — Editor lo cấu hình và kết nối, tool, import/export

Skill nền dưới `Skills/horcrux_skill.md` (NT, tầng, ký hiệu định nghĩa ở đó).

## Editor-first

Luật ở NT5. Trước khi viết code cho một việc, hỏi: *developer làm việc này một lần trong Editor được không?* Được thì đó là
bước setup, không phải code. **Dấu hiệu code đang làm việc của Editor:** `GetComponent`/`Find`/`AddComponent`/
`Resources.Load` lấy thứ đã có trên prefab · hằng số tinh chỉnh hardcode · dựng hierarchy bằng code · API `Bind…`/`Attach…`
nhận reference · đọc ngầm giá trị prefab ở `Start` thay vì phơi ô.

- **Kéo thả vào `[SerializeField]` là DI** — composition root là chính cái asset. **API nhận reference là code nối**, chỉ
  khi Inspector **thật sự không kéo được**: cùng scene thì kéo được; khác scene hoặc prefab dựng lúc chạy thì không. Wiring
  là GUID trong file serialize, không grep được — lý do **không rải** nó, không phải lý do bỏ (phân xử ở `horcrux_skill`).
- **Thuộc tính code ghi lúc chạy không còn là ô cấu hình trên asset** — sửa ở đó không tác dụng mà không gì báo. Núm chỉnh
  nằm trên component code; asset chỉ giữ phần code không chạm, tooltip nói rõ phần nào.
- **Bắt buộc nối bằng code thì bên đời ngắn tự trình diện với bên đời dài:** đăng ký và huỷ là **một cặp trong một file**,
  xong **trước tín hiệu cần tới nó**.
- **Điều kiện dựng hệ ghi vào tài liệu, không sinh code canh.** Tạo asset, dựng GameObject, gán reference, đặt layer, thêm
  scene vào build là mục **"Trước khi chạy"** của tài liệu module (`docs` › `.md`); thiếu thì xử lý theo bảng guard
  (`code-design` › Bất biến). Bước setup tay là cách developer hiểu hệ; đổi lại hệ **nợ** mục "Trước khi chạy" đủ dựng lại
  từ 0.
- **Agent làm thao tác Editor qua công cụ (MCP, eval) khi được giao** thì kết quả là **asset đã lưu** developer mở ra thấy,
  không phải code chạy lúc boot làm thay. Soát `git status` ngay sau mỗi lệnh lưu; file ngoài phạm vi thì báo. Trạng thái
  Editor đã đổi (view, scene, Play) trả lại trước khi báo xong. Giá trị developer đổi giữa hai lượt là quyết định của họ.

## Import/export — dữ liệu đi qua tool

File tool đọc–ghi là **của người dùng và hệ khác**. **Không sửa giá trị import** — ngoài tầm hợp lệ thì từ chối hoặc bỏ
entry kèm cảnh báo, không clamp, không bịa entry mới · **chỉ sở hữu phần mình hiểu** — khoá lạ đi qua nguyên vẹn · **Import
và Export sống cạnh nhau**, nghiệm thu bằng round-trip. **Gói asset từ ngoài chỉ nhập phần thứ cần dùng kéo theo.**

## Editor tool

Tiêu chí: **một tool là một đơn vị gói kín** · **thứ dùng chung sống một chỗ** (`Editor/Common/` của Horcrux) · **thứ không
đổi giữa các lần vẽ phải có sẵn** (NT3 áp cho `OnGUI`).

**Tool có người dùng thì UX là thiết kế:** thứ khác vai trò không chung một vùng · điều kiện vẽ và điều kiện bấm suy từ một
nguồn · **không giấu thứ có thật** · kiểm hợp lệ chạy **khi được hỏi**, không chạy nền. **Tool đọc trong Play qua chính
đối tượng runtime** — thấy đúng thứ runtime thấy — không dựng bản thứ hai của getter để chạy ngoài Play.

## Tra cứu — `editor-first/reference.md`

| Chạm | Mục |
|---|---|
| wire prefab, đăng ký–huỷ bằng code, thoát app | Wiring |
| sửa, lưu prefab, scene, YAML, shader qua công cụ; `.meta`, GUID | Sửa asset qua công cụ |
| nhập `.unitypackage`, gói art, plugin | Gói asset từ ngoài |
| tool import, export dữ liệu; bố cục, điều kiện vẽ của tool có người dùng | Import/export và UX của tool |
| viết `OnGUI`, `PropertyDrawer`, cache style, tool đọc đối tượng runtime | IMGUI |
