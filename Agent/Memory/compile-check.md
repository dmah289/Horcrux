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

### Verify xanh mà commit vừa làm có thể không compile
`2026-11` · vấp 1 · suy luận · đúng với nhiều phiên cùng sửa một checkout
**Thấy:** verify xanh, review phát hiện asmdef ở HEAD thiếu reference mà code mới cần (CS0012: đọc member của type có lớp cha ở assembly khác); dòng reference chỉ có trong working tree, do phiên khác thêm, chưa commit. **Vì:** verify build working tree, commit theo pathspec chỉ mang file của task; phần chưa commit của người khác che lỗi của bản commit.
**Làm:** trước khi báo xong, `git status` các file build (asmdef, csproj, `Packages/manifest.json`) có thay đổi chưa commit thì soát code mới có dựa vào nó không; có thì commit riêng đúng dòng đó và báo developer (`workflow` › tra cứu › Git).

### Test EditMode đọc asset cấu hình đổi kết quả sau một lượt Play, code không đổi
`2026-11` · vấp 1 · đã đo · đúng với ScriptableObject nạp qua `Resources.Load` mà game ghi giá trị lúc chạy (remote config)
**Thấy:** chạy `verify.sh` hai lần quanh một lượt Play qua công cụ: test so giá trị asset với spec đổi fail ↔ pass, `git status` của asset sạch, nhãn `(mới)` nhảy qua lại. **Vì:** lúc Play game ghi giá trị remote vào bản trong bộ nhớ của asset; thoát Play bản đó vẫn sống, test EditMode đọc bản bộ nhớ chứ không đọc đĩa.
**Làm:** sau lượt Play qua công cụ, `Resources.UnloadAsset` rồi `AssetDatabase.ImportAsset(path, ForceUpdate)` asset đó trước khi chạy test; fail còn lại sau khi nạp lại mới là dữ liệu trên đĩa (`compile-check` › Luật 8). Lượt mốc trước chạy trên bản bộ nhớ thì fail của dữ liệu đĩa mang nhãn `(mới)` tới khi test qua, hook chặn báo xong theo: báo kèm bằng chứng git (asset không đổi từ mốc), để developer sửa dữ liệu; không xoá mốc để lách.
