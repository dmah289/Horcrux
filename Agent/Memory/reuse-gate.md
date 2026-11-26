# reuse-gate — kinh nghiệm

Bài học đã trả giá khi làm việc với `reuse-gate`. Đọc trọn cùng lúc với rule hoặc skill đó. Hình dạng và vòng đời ở `horcrux.md` › Memory.

### CS0121 giữa hai extension method cùng tên
`2026-10` · vấp 1 · đã đo
**Thấy:** file vừa thêm `using` namespace Horcrux báo CS0121 ambiguous giữa extension của Horcrux và extension cùng tên của dự án. **Vì:** cả hai namespace cùng được `using` trong file đó.
**Làm:** sửa `using` ở chính file đó — giữ bản Horcrux, bỏ `using` namespace extension của dự án nếu file không cần gì khác từ đó; không alias, không đụng file extension nào trong bước này; việc gộp hai bản là đề xuất riêng (`reuse-gate` › Có sẵn chưa).

### Eval chụp, đo, sửa prefab viết bằng API Unity thô dù Horcrux đã có helper
`2026-11` · vấp 1 · đã đo
**Thấy:** việc hiệu ứng world viết tay camera + `RenderTexture` để chụp so hai cách vẽ, script Python đo khung alpha, `LoadPrefabContents`/`Unload` để dời một object con; Horcrux đã có `UICapture.World`, `DemoCompare.BBox`, `PrefabEdit.Edit`, và bản tay vấp đúng lỗi helper đã xử lý (renderer khác đè, đọc object đã huỷ). **Vì:** ba helper nằm trong `docs/Editor/UIReskin.md`; tìm theo tên miền đang làm (VFX, world, Spine) thì không ra tài liệu mang tên UI.
**Làm:** trước eval dựng, chụp, đo hay sửa prefab, grep `docs/Editor/*.md` theo động từ (`chụp`, `World(`, `Edit(`, `BBox`, `Dump`), không theo tên miền; `UICapture.World` vẽ được cả skeleton Spine ngoài Play khi `pose` dựng mesh (`docs/ToolUsage/Spine.md` › Skeleton trong world) (`reuse-gate` › Có sẵn chưa).

### Nhóm thử hay tập user lẫn người chơi đang chạy bản mặc định
`2026-11` · vấp 1 · suy luận · đúng với giá trị remote chọn nội dung hay quyết một lần rồi lưu save, Firebase Remote Config và A/B Testing
**Thấy:** remote chọn một nội dung trong build (bộ level, bảng cấu hình) hoặc quyết một thứ lưu vào save ở phiên đầu (tập user mặc định), chạy đúng trong Editor, nhưng số liệu nhóm lẫn người chơi bản mặc định. **Vì:** build cũ thiếu nội dung mới nên rơi về mặc định mà vẫn được tính vào nhóm · fetch phiên đầu quá hạn chờ (khởi tạo SDK lần đầu nằm trong hạn đó) thì phiên đó chạy giá trị trong asset; giá trị chỉ chọn nội dung thì áp từ phiên sau, còn giá trị **quyết một lần rồi lưu** thì khoá user vào bản asset mãi mãi · `FetchAsync()` không tham số giữ cache 12 giờ, đổi trên console không thấy ngay khi kiểm.
**Làm:** giá trị lưới đỡ trong asset phải khớp console (so bằng tool diff remote config trước build) · thứ quyết một lần mà đọc remote thì nêu ca fetch trễ ở phiên đầu cho developer chọn: chấp nhận, nới hạn chờ phiên đầu, hay quyết lại khi quyết định cũ đứng trên giá trị asset · experiment lọc theo phiên bản app có nội dung · kiểm nhanh bằng cheat hoặc xoá dữ liệu app (`workflow` › Đối chiếu).
