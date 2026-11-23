# feel-tune — kinh nghiệm

Bài học đã trả giá khi làm việc với `feel-tune`. Đọc trọn cùng lúc với rule hoặc skill đó. Hình dạng và vòng đời ở `horcrux.md` › Memory.

### Dò luồng treo tốn hàng chục eval và console thô
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** dò một luồng treo bằng vài chục eval rời và đọc console thô hàng trăm KB, trong khi `playrun.sh` + `PlayProbe` làm cả chuỗi trong một lệnh và in `LOG` đã gọn. **Vì:** viết khung mới thay vì dùng công cụ đã có, và đọc dữ liệu thô vào ngữ cảnh.
**Làm:** không viết script khung mới — chạy `playrun.sh` + `PlayProbe`; luồng dừng giữa chừng thì `console_digest.py` rồi `PlayProbe.Field` (`feel-tune` › Luật; `feel-tune` › Chọn công cụ).
