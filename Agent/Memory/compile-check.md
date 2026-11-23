# compile-check — kinh nghiệm

Bài học đã trả giá khi làm việc với `compile-check`. Đọc trọn cùng lúc với rule hoặc skill đó. Hình dạng và vòng đời ở `horcrux.md` › Memory.

### Cần chạy test PlayMode mà Editor không mở project
`2026-10` · vấp 1 · đã đo · đúng với Editor không mở project đó
**Thấy:** cần kết quả test PlayMode nhưng không có Editor nào mở project, CLI và MCP không có gì để nối. **Vì:** test runner chạy trong Editor; Editor đang mở thì khoá project, nên chạy batchmode song song sẽ hỏng.
**Làm:** kiểm không có `Unity.exe` nào mở project, rồi chạy nền `Unity.exe -batchmode -projectPath <p> -runTests -testPlatform PlayMode -testFilter <lớp test> -testResults <xml> -logFile <log>` (vài phút vì compile và ILPP); đọc `test-run` total/passed/failed, `test-case` `result`, `failure/message` trong XML. Editor đang mở thì PlayMode do developer chạy (`compile-check` › Luật).
