# reuse-gate — kinh nghiệm

Bài học đã trả giá khi làm việc với `reuse-gate`. Đọc trọn cùng lúc với rule hoặc skill đó. Hình dạng và vòng đời ở `horcrux.md` › Memory.

### Một chỉnh sửa chỉ ăn ở một nửa số chỗ gọi
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** một nửa số call site đã qua hệ mới, nửa kia còn đi hệ cũ nên không chỉnh được ở một chỗ, hai nửa lệch nhau. **Vì:** đường di sản song song: nhánh sót lại vẫn chạy, chỉ lệch cấu hình.
**Làm:** đếm call site đường cũ trước khi thêm vào đường mới (`reuse-gate` › tra cứu › Khảo sát).

### State người chơi nằm trong file của version control
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** bê khuôn serialize giá trị runtime vào asset sang bài toán lưu state người chơi — state vào version control. **Vì:** khuôn cũ cố ý ghi vào asset vì lý do của bài toán gốc; lý do đó không còn đúng ở bài toán mới.
**Làm:** bê khuôn có sẵn thì soi chỗ khuôn cũ *cố ý* làm — lý do gốc còn đúng không; hết thì đảo và ghi lý do (`reuse-gate` › Có sẵn chưa).

### Vòng tween tự viết cạnh runner đã có
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** viết vòng tween và công thức riêng trong khi Horcrux đã có runner và hàm đúng hình dạng đó. **Vì:** khảo sát dừng trước Horcrux; cả hai cùng có thì dùng Horcrux.
**Làm:** khảo sát toàn bộ, theo thứ tự hệ đang chạm → Horcrux → phần còn lại của dự án → package đã cài, trước dòng đầu (`reuse-gate` › Có sẵn chưa).

### Khung dump, script đo viết lại ở mỗi task
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** đã đọc skill nêu sẵn hàm dump và hàm đo, vẫn viết khung dump trong eval và script đo riêng, task sau lại viết lần nữa. **Vì:** script dùng một lần cũng là code — viết tay thì số của hai lần đo không so được với nhau.
**Làm:** gọi hàm skill đã nêu; thiếu thì mở rộng công cụ Horcrux, không viết bản riêng (`reuse-gate` › Có sẵn chưa).

### CS0121 giữa hai extension method cùng tên
`2026-10` · vấp 1 · đã đo
**Thấy:** file vừa thêm `using` namespace Horcrux báo CS0121 ambiguous giữa extension của Horcrux và extension cùng tên của dự án. **Vì:** cả hai namespace cùng được `using` trong file đó.
**Làm:** sửa `using` ở chính file đó — giữ bản Horcrux, bỏ `using` namespace extension của dự án nếu file không cần gì khác từ đó; không alias, không đụng file extension nào trong bước này; việc gộp hai bản là đề xuất riêng (`reuse-gate` › Có sẵn chưa).
