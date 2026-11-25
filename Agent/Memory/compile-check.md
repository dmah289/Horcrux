# compile-check — kinh nghiệm

Bài học đã trả giá khi làm việc với `compile-check`. Đọc trọn cùng lúc với rule hoặc skill đó. Hình dạng và vòng đời ở `horcrux.md` › Memory.

### Cần chạy test mà Editor không mở project
`2026-10` · vấp 1 · đã đo · đúng với Editor không mở project đó
**Thấy:** cần kết quả test PlayMode hay EditMode nhưng không có Editor nào mở project, CLI và MCP không có gì để nối. **Vì:** test runner chạy trong Editor; Editor đang mở thì khoá project (`Temp/UnityLockfile`), nên chạy batchmode song song sẽ hỏng.
**Làm:** kiểm không có `Unity.exe` nào mở project, rồi chạy nền `Unity.exe -batchmode -projectPath <p> -runTests -testPlatform <PlayMode|EditMode> -testFilter <lớp test> -testResults <xml> -logFile <log>` (vài phút vì compile và ILPP); đọc `test-run` total/passed/failed, `test-case` `result`, `failure/message` trong XML. Editor đang mở thì PlayMode do developer chạy (`compile-check` › Luật).

### Assembly test vừa thêm reference vẫn báo thiếu type khi build bằng dotnet
`2026-10` · vấp 1 · đã đo · đúng với Editor đóng, csproj chưa sinh lại
**Thấy:** asmdef test thêm reference (UniTask) mà `dotnet build` csproj cùng tên vẫn báo thiếu type. **Vì:** csproj ở gốc do Editor sinh và không vào git; asmdef đổi mà Editor chưa chạy thì csproj vẫn là bản cũ.
**Làm:** Editor mở thì để `verify.sh` Refresh (`compile-check` › Luật); Editor đóng thì tạm thêm `<Reference Include="UniTask"><HintPath>Library/ScriptAssemblies/UniTask.dll</HintPath></Reference>` vào csproj local, lần Editor sinh lại sẽ thay.

### Grep tên báo không còn caller mà xoá đi lại vỡ, hoặc ngược lại
`2026-10` · vấp 1 · đã đo
**Thấy:** grep tên type để tìm code chết báo sai hai chiều. **Vì:** extension method không gọi tên class ở call site · type trùng tên ở namespace khác làm grep dương tính giả.
**Làm:** chép csproj ra bản dự phòng, bỏ dòng `<Compile Include>` của ứng viên, build mọi csproj tham chiếu assembly đó: sạch mới là không caller trong code; lời gọi từ asset (`m_MethodName` của UnityEvent) và reflection vẫn phải grep riêng (`workflow` › Nghiệm thu).

### Hook dừng lượt nhắc compile-check ở mọi lượt dù task không có fail mới
`2026-11` · vấp 1 · đã đo · đúng với assembly test còn fail có sẵn
**Thấy:** sau mỗi task hook Stop báo "file .cs sửa sau lần verify.sh OK cuối", chạy lại vẫn báo, suốt một plan nhiều task. **Vì:** hook chỉ tin mốc `last-ok` mà `verify.sh` ghi khi dòng cuối là `verify: OK`; assembly test còn fail có sẵn thì dòng cuối luôn `FAIL tests` dù `0 fail mới`.
**Làm:** không chạy lại cho hook khi đang có subagent dùng Editor; dán dòng build, tests và `so với lượt …: 0 fail mới` làm bằng chứng. Nâng cấp ① đề xuất: `verify.sh` ghi `last-ok` khi build, recompile xanh và không có fail mới (`compile-check` › Luật 8).

### Test đỏ hàng loạt mà code đang test không đổi
`2026-11` · vấp 1 · đã đo · đúng với test đọc asset nội dung của dự án
**Thấy:** hàng chục test của tool đỏ cùng lúc dù code tool không đổi gì; bisect code không ra commit nào. **Vì:** test chạy trên asset nội dung người khác đang sửa (level, collection) hoặc ghi cứng id do tool dựng lại, và golden băm cả dữ liệu ngoài phần code sinh ra (field thêm hay bớt của class dữ liệu).
**Làm:** chạy lại test trên đúng bản asset ngày viết test (lấy từ git) để tách lỗi dữ liệu khỏi lỗi code; sửa bằng fixture đóng băng trong thư mục test và id lấy từ asset lúc chạy; soát `FindAssets("t:<Type>")` không giới hạn thư mục có nhặt bản đóng băng không; golden đổi vì field thì ghi lại kèm cách dựng JSON cũ khớp hash cũ. Test cố ý chạy trên dữ liệu sống chỉ khẳng định tính chất đúng với mọi dữ liệu hợp lệ, không khẳng định dữ liệu đang sạch (`workflow` › Nghiệm thu).
