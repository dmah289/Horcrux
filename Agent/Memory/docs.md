# docs — kinh nghiệm

Bài học đã trả giá khi làm việc với `docs`. Đọc trọn cùng lúc với rule hoặc skill đó. Hình dạng và vòng đời ở `docs` › Agent.

### Máy canh dựng theo bài học cũ canh một cái hại không còn xảy ra
`2026-11` · vấp 1 · đã đo · đúng với bài học sắp nâng ① sửa gốc hay ② máy canh
**Thấy:** sắp viết hook cho một bài học vấp 2; tái hiện trước thì cái hại nó tả (diff cả file) không xảy ra ở cấu hình hiện tại, chỉ còn cảnh báo vô hại. **Vì:** phần "Vì" đúng ở điều kiện lúc vấp (cấu hình git, dữ liệu trong repo); điều kiện đổi mà dòng "đúng với" không ghi điều kiện đó.
**Làm:** trước khi nâng, dựng ca tái hiện cái hại ở mọi cấu hình có thể gặp; máy canh đo chính cái hại (thứ lọt vào commit, thứ người chơi thấy), không đo dấu hiệu bề mặt — đo dấu hiệu thì kêu ở chỗ vô hại (`docs` › Agent).

### Phép kiểm heuristic chọn cách đo theo cảm tính bắt chưa nửa số ca
`2026-11` · vấp 1 · đã đo
**Thấy:** cảnh báo "khối memory đã nâng" so nguyên văn từng vế chỉ bắt 15/39 khối; đo theo cụm ba từ bắt 31/39, nhầm 2/34. **Vì:** đích viết lại cùng ý bằng lời khác; cách đo nghĩ ra trước khi có dữ liệu chưa được thử với ca nào.
**Làm:** lấy các quyết định đã có (lượt dọn, review trước) làm đáp án, so vài cách đo và ngưỡng, chọn theo số bắt và số nhầm, ghi số vào comment cạnh hằng ngưỡng (`workflow` › Nghiệm thu).

### Agent nhận việc thuộc một rule thì bỏ qua memory nền đầu phiên
`2026-11` · vấp 1 · đã đo · đúng với file hướng dẫn agent của dự án (`CLAUDE.md`) có câu trỏ thẳng tới rule của một loại việc
**Thấy:** eval "ghi bài học" đỏ hai lượt: agent đọc `horcrux.md` rồi nhảy sang rule của việc đó, chỉ grep memory nền thay vì đọc trọn; bản trước, khi câu đó trỏ vào `horcrux.md`, thì xanh. **Vì:** câu "làm việc X theo file Y" trong file hướng dẫn được đọc như lối tắt bỏ qua luật đọc.
**Làm:** câu trỏ tới rule của một loại việc ghi rõ là sau luật đọc đầu phiên, cả ở file hướng dẫn, hàng bảng chỉ dẫn và đầu mục rule đó; sửa luật đọc hay con trỏ thì chạy lại `agent_evals.py` ca chạm nó, so với mốc bản trước.
