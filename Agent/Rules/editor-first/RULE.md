---
name: editor-first
description: Dùng khi quyết thứ gì làm ở Editor thay vì code, khi agent thao tác Editor qua công cụ (MCP, CLI, eval) để dựng, sửa, soát asset, khi viết tool Editor IMGUI, hoặc khi tool đọc–ghi dữ liệu của người dùng (import/export).
---

# editor-first — Editor lo cấu hình và kết nối, thao tác qua công cụ, tool, import/export

Rule dưới `Agent/horcrux.md` (NT, tầng, ký hiệu định nghĩa ở đó).

## Editor-first

Luật ở NT5. Trước khi viết code cho một việc, hỏi: *developer làm việc này một lần trong Editor được không?* Được thì đó là
bước setup, không phải code. **Dấu hiệu code đang làm việc của Editor:** `GetComponent`/`Find`/`AddComponent`/
`Resources.Load` lấy thứ đã có trên prefab · hằng số tinh chỉnh hardcode · dựng hierarchy bằng code · API `Bind…`/`Attach…`
nhận reference · đọc ngầm giá trị prefab ở `Start` thay vì phơi ô.

- **Kéo thả vào `[SerializeField]` là DI** — composition root là chính cái asset. **API nhận reference là code nối**, chỉ
  khi Inspector **thật sự không kéo được**: cùng scene thì kéo được; khác scene hoặc prefab dựng lúc chạy thì không. Wiring
  là GUID trong file serialize, không grep được — lý do **không rải** nó, không phải lý do bỏ (`horcrux.md` › Phân xử).
- **Thuộc tính code ghi lúc chạy không còn là ô cấu hình trên asset** — sửa ở đó không tác dụng mà không gì báo. Núm chỉnh
  nằm trên component code; asset chỉ giữ phần code không chạm, tooltip nói rõ phần nào.
- **Bắt buộc nối bằng code thì bên đời ngắn tự trình diện với bên đời dài:** đăng ký và huỷ là **một cặp trong một file**,
  xong **trước tín hiệu cần tới nó**.
- **Điều kiện dựng hệ là mục "Trước khi chạy" của tài liệu module** (`docs` › `.md`): tạo asset, dựng GameObject, gán
  reference, đặt layer, thêm scene vào build. Bước setup tay là cách developer hiểu hệ; đổi lại hệ **nợ** mục đó đủ dựng lại
  từ 0.

## Agent thao tác Editor qua công cụ

Khi được giao làm thao tác Editor qua MCP, CLI, eval:

- **Kết quả là asset đã lưu** developer mở ra thấy, không phải code chạy lúc boot làm thay. Nghiệm thu **sau khi lưu và nạp
  lại** — lỗi pivot, override mất chỉ lộ sau khi nạp.
- **Gọi helper Horcrux, không chép khung vào eval:** mở, sửa, lưu prefab, dump, chụp, đo đã có sẵn và đã compile thật (tìm
  ở `docs/Editor/`); eval chỉ còn phần riêng của bài.
- **Nhìn trước khi sửa:** đọc giá trị qua engine (dump cây, field serialize, override) trước khi chạm — tên file và số trong
  YAML có thể đánh lừa.
- **Soát `git status` ngay sau mỗi lệnh lưu**; file ngoài phạm vi thì báo. Giá trị developer đổi giữa hai lượt là quyết định
  của họ.
- **Trạng thái Editor đã đổi** (scene đang mở, scene khởi động, cỡ Game view, máy Simulator, Play) **trả lại trước khi báo
  xong.**

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
| viết eval, vào Play qua CLI hay MCP | Eval và Play qua công cụ |
| nhập `.unitypackage`, gói art, plugin | Gói asset từ ngoài |
| tool import, export dữ liệu; bố cục, điều kiện vẽ của tool có người dùng | Import/export và UX của tool |
| viết `OnGUI`, nút đổi state hay mở hộp thoại, `PropertyDrawer`, cache style, tool đọc đối tượng runtime | IMGUI |
