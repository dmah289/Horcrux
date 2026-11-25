# workflow — kinh nghiệm

Bài học đã trả giá khi làm việc với `workflow`. Đọc trọn cùng lúc với rule hoặc skill đó. Hình dạng và vòng đời ở `horcrux.md` › Memory.

### Hai mốc cách xa không so được vì diff thô ngập đổi tên
`2026-10` · vấp 1 · đã đo
**Thấy:** cần chứng minh refactor không đổi thuật toán giữa hai commit cách xa; diff thô hàng chục nghìn dòng vì nhiều đợt đổi tên và dời thư mục. **Vì:** đổi tên và dời file che mọi thay đổi thật.
**Làm:** ghép cặp file bằng `git diff -M20%` rồi ghép tay phần còn lại · áp bảng đổi tên (lấy lại từ git history nếu Plan đổi tên đã xoá) lên bản cũ · diff bỏ `using` và khoảng trắng · **so riêng tập chuỗi ký tự** để đổi tên không che chuỗi · đọc tay mọi cặp còn lệch · hàm trùng tên ở hai namespace thì soát `using` từng file xem resolve về bản nào · prefab lọc guid, fileID nhưng **không** lọc `m_IsActive`, `m_Enabled`, `m_LocalPosition` (`workflow` › tra cứu › Nghiệm thu).

### Commit theo pathspec hỏng cả cụm, hoặc thiếu file mới
`2026-11` · vấp 1 · đã đo
**Thấy:** `git add` file vừa `git rm` báo "did not match" làm hỏng cả lệnh · file mới không vào commit. **Vì:** file đã xoá khỏi index không còn để `git add`; pathspec của commit không nhận file chưa track.
**Làm:** `git add -- <file mới>` trước; file đã `git rm` chỉ để trong pathspec của `git commit -- <paths>`; không chạy commit nền. Commit không pathspec, `git commit -a`, `git add -A` đã có hook chặn (`workflow` › tra cứu › Git).

### `.meta` texture đổi sau khi import mà không ai sửa
`2026-11` · vấp 1 · đã đo
**Thấy:** `.meta` của texture vừa import hiện trong `git status` với mục platform Android, iOS không override. **Vì:** Unity tự thêm mục platform mặc định khi import.
**Làm:** coi là nhiễu, để ngoài commit trừ khi developer muốn giữ.

### Session ở worktree, việc cần Editor không làm được
`2026-11` · vấp 1 · đã đo · đúng với session mở trong git worktree
**Thấy:** tới lúc sửa prefab, scene hay art chưa track mới phát hiện worktree thiếu submodule, thiếu art, và hook chặn ghi checkout chính. **Vì:** Editor và MCP chỉ mở trên checkout chính; mở worktree trong Editor phải init submodule, chép art, import lại Library rất lâu.
**Làm:** việc cần Editor thì hỏi ngay đầu task làm ở đâu; được làm ở checkout chính thì ghi file ở scratchpad rồi chép sang, giữ kiểu xuống dòng của file gốc; không commit nếu developer chưa bảo.

### File chưa track trùng type của Plan xuất hiện giữa lúc viết lại Plan
`2026-11` · vấp 1 · đã đo
**Thấy:** đang viết lại nhiều Plan bằng subagent thì một file code chưa track mang đúng class của một Plan xuất hiện trong Horcrux — developer đã bắt đầu gõ theo bản Plan sắp đổi. **Vì:** Plan là đầu vào developer gõ song song; grep "chưa ai gõ" lúc đầu phiên chỉ đúng tại thời điểm đó.
**Làm:** trước khi viết lại Plan, báo developer danh sách Plan sẽ đổi; trước mỗi commit soát `git status` của thư mục code Horcrux; thấy file trùng thì dừng và hỏi giữ hay đổi Plan, không chạm file đó (`workflow` › Subagent).

### Máy canh dựng theo bài học cũ canh một cái hại không còn xảy ra
`2026-11` · vấp 1 · đã đo · đúng với bài học sắp nâng ① sửa gốc hay ② máy canh
**Thấy:** sắp viết hook cho một bài học vấp 2; tái hiện trước thì cái hại nó tả (diff cả file) không xảy ra ở cấu hình hiện tại, chỉ còn cảnh báo vô hại. **Vì:** phần "Vì" đúng ở điều kiện lúc vấp (cấu hình git, dữ liệu trong repo); điều kiện đổi mà dòng "đúng với" không ghi điều kiện đó.
**Làm:** trước khi nâng, dựng ca tái hiện cái hại ở mọi cấu hình có thể gặp; máy canh đo chính cái hại (thứ lọt vào commit, thứ người chơi thấy), không đo dấu hiệu bề mặt — đo dấu hiệu thì kêu ở chỗ vô hại (`horcrux.md` › Memory).

### Phép kiểm heuristic chọn cách đo theo cảm tính bắt chưa nửa số ca
`2026-11` · vấp 1 · đã đo
**Thấy:** cảnh báo "khối memory đã nâng" so nguyên văn từng vế chỉ bắt 15/39 khối; đo theo cụm ba từ bắt 31/39, nhầm 2/34. **Vì:** đích viết lại cùng ý bằng lời khác; cách đo nghĩ ra trước khi có dữ liệu chưa được thử với ca nào.
**Làm:** lấy các quyết định đã có (lượt dọn, review trước) làm đáp án, so vài cách đo và ngưỡng, chọn theo số bắt và số nhầm, ghi số vào comment cạnh hằng ngưỡng (`workflow` › Nghiệm thu).

### Lệnh xoá bị từ chối, kéo cả cụm lệnh đi theo
`2026-11` · vấp 1 · suy luận · đúng với agent chạy auto mode của Claude Code
**Thấy:** `rm -rf` thư mục trong `Assets`, `AssetDatabase.DeleteAsset` trong eval, `git checkout -- <file>` đều bị từ chối, kể cả khi gộp với lệnh khác — cả cụm bị chặn theo. **Vì:** classifier phía harness coi các lệnh đó nguy hiểm, không giải thích; thử lại cùng ý định không qua.
**Làm:** tách lệnh xoá khỏi lệnh sửa để phần sửa vẫn chạy; bước xoá (trong Project window, để `.meta` đi cùng) và bước trả file giao developer ở mục thao tác Editor cuối báo cáo.

### Script Python nhúng heredoc khớp 0 chỗ hoặc báo SyntaxError
`2026-11` · vấp 2 · đã đo · đúng với Bash tool là Git Bash trên Windows
**Thấy:** script Python nhúng bằng heredoc `<<'EOF'` thay khối C# khớp 0 chỗ mà không báo, hoặc dừng ở `SyntaxError: unterminated string literal` khi chuỗi chứa đường dẫn `\`; `'\\'` thành `'\'`, `$"…"` mất `$`. **Vì:** heredoc trong Bash tool không giữ nguyên văn `\` và `$`.
**Làm:** khối có `$`, `\`, backtick thì sửa bằng Edit, hoặc ghi script bằng Write rồi `python -X utf8 -I script.py`; mọi phép thay theo chuỗi `assert count == 1` trước khi ghi.

### Script Python dừng giữa chừng sau khi đã ghi một phần file
`2026-10` · vấp 1 · đã đo · đúng với console Windows
**Thấy:** script in tiếng Việt ném lỗi encode giữa lượt, một nửa số file đã ghi. **Vì:** console Windows mặc định cp1252; `-I` bỏ qua `PYTHONIOENCODING`.
**Làm:** chạy `python -X utf8 -I`, hoặc script tự `sys.stdout.reconfigure(encoding="utf-8")` ở đầu.
