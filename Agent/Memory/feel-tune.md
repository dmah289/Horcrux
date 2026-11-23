# feel-tune — kinh nghiệm

Bài học đã trả giá khi làm việc với `feel-tune`. Đọc trọn cùng lúc với rule hoặc skill đó. Hình dạng và vòng đời ở `horcrux.md` › Memory.

### Dò luồng treo tốn hàng chục eval và console thô
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** dò một luồng treo bằng vài chục eval rời và đọc console thô hàng trăm KB, trong khi `playrun.sh` + `PlayProbe` làm cả chuỗi trong một lệnh và in `LOG` đã gọn. **Vì:** viết khung mới thay vì dùng công cụ đã có, và đọc dữ liệu thô vào ngữ cảnh.
**Làm:** không viết script khung mới — chạy `playrun.sh` + `PlayProbe`; luồng dừng giữa chừng thì `console_digest.py` rồi `PlayProbe.Field` (`feel-tune` › Luật; `feel-tune` › Chọn công cụ).

### Lệnh tới Editor trả 504 "waiting for another command to finish"
`2026-11` · vấp 1 · đã đo · đúng với Editor dùng chung với session khác
**Thấy:** lệnh trả 504 "waiting for another command to finish"; lượt Play kẹt giữa chừng, `playmode stop` không chạy. **Vì:** hàng lệnh của Editor đang bị session khác giữ.
**Làm:** trước `playrun.sh` xem `git status` của Horcrux và dự án có ai đang sửa, `editor status` có `compiling` không; PlayerPrefs bị trả hỏng thì chạy lệnh trả tay `playrun.sh` in ra.

### `eval_file` gọi từ Bash báo "File must have a .cs extension"
`2026-11` · vấp 1 · đã đo · đúng với Git Bash trên Windows
**Thấy:** CLI `script eval_file` gọi từ Git Bash báo file phải có đuôi `.cs` dù đường dẫn đúng. **Vì:** MSYS tự đổi đối số trông như đường dẫn POSIX trước khi truyền cho chương trình Windows.
**Làm:** đặt `MSYS_NO_PATHCONV=1` và truyền đường dẫn Windows đầy đủ lấy từ `cygpath -w`.
