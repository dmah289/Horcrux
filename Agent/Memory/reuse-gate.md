# reuse-gate — kinh nghiệm

Bài học đã trả giá khi làm việc với `reuse-gate`. Đọc trọn cùng lúc với rule hoặc skill đó. Hình dạng và vòng đời ở `docs` › Agent.

### CS0121 giữa hai extension method cùng tên
`2026-10` · vấp 1 · đã đo
**Thấy:** file vừa thêm `using` namespace Horcrux báo CS0121 ambiguous giữa extension của Horcrux và extension cùng tên của dự án. **Vì:** cả hai namespace cùng được `using` trong file đó.
**Làm:** sửa `using` ở chính file đó — giữ bản Horcrux, bỏ `using` namespace extension của dự án nếu file không cần gì khác từ đó; không alias, không đụng file extension nào trong bước này; việc gộp hai bản là đề xuất riêng (`reuse-gate` › Có sẵn chưa).

### Eval chụp, đo, soát, sửa asset viết bằng API Unity thô dù Horcrux đã có helper
`2026-11` · vấp 3 · đã đo
**Thấy:** viết tay camera + `RenderTexture` để chụp, script Python đo khung alpha, `LoadPrefabContents`/`Unload` để dời object con, trong khi Horcrux đã có `UICapture.World`, `DemoCompare.BBox`, `PrefabEdit.Edit` — bản tay vấp đúng lỗi helper đã xử lý (renderer khác đè, đọc object đã huỷ) · định thêm API soát Addressables vào Horcrux trong khi dự án đã có tool soát đúng bảng đó · viết tay script tóm anim của gói hoạ sĩ. **Vì:** helper nằm trong `docs/Editor/<tên công cụ>.md` của Horcrux hay tài liệu module của dự án; tìm theo tên miền đang làm (VFX, world, Spine) thì không ra.
**Làm:** trước eval dựng, chụp, đo, soát hay sửa asset, grep `docs/Editor/*.md` của Horcrux và `*.md` cạnh code dự án theo động từ và triệu chứng (`chụp`, `soát`, `Audit`, `gãy`, `World(`, `Edit(`, `BBox`, `Dump`), không theo tên miền; viết test cho asset thì grep `Tests/` theo tên type asset trước; `UICapture.World` vẽ được cả skeleton Spine ngoài Play khi `pose` dựng mesh (`docs/ToolUsage/Spine.md` › Skeleton trong world) (`reuse-gate` › Có sẵn chưa).
