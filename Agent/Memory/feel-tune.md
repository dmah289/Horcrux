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

### Bố cục trong Play sai dù prefab đúng, chỉ khi Play qua công cụ
`2026-10` · vấp 1 · đã đo · đúng với code đọc `Screen` lúc `Awake`
**Thấy:** vào Play qua MCP hay CLI thì khối gameplay sai cỡ, sai chỗ; developer Play tay thì đúng. **Vì:** Game view của Editor mở bằng công cụ mặc định cỡ ngang nhỏ; code đọc `Screen` một lần lúc `Awake` giữ cỡ đó cả phiên.
**Làm:** đọc và đặt cỡ Game view dọc trước khi Play (`targetSize` của `UnityEditor.GameView` qua reflection), không đổi cỡ giữa lượt Play.

### Play ra lỗi thiếu service, event đăng ký trùng sau một eval mở scene
`2026-10` · vấp 1 · đã đo · đúng với service toàn cục nằm ở scene do scene khởi động nạp
**Thấy:** Play báo thiếu service, event đăng ký hai lần, initializer thiếu phụ thuộc — code không đổi gì. **Vì:** eval `OpenScene` để lại scene khác scene khởi động; Play từ đó bỏ qua bước nạp scene chứa service.
**Làm:** script eval nào mở scene thì cuối cùng mở lại scene khởi động; lỗi thiếu service lúc Play thì xem scene đang mở trước khi sửa code.

### Quay trace mãi không ra hai lần cùng chữ ký, hoặc trùng chữ ký mà lệch giữa chừng
`2026-11` · vấp 1 · đã đo · đúng với game chia dữ liệu ngẫu nhiên lúc boot
**Thấy:** bảy lần quay mới có một cặp cùng chữ ký; ba mươi lần chỉ một lần 0 chênh cả trace, các lần khác lệch từ giữa kịch bản. **Vì:** dữ liệu chia ngẫu nhiên lúc boot quyết chữ ký; vật sinh giữa kịch bản lấy ID ngẫu nhiên mà chữ ký trạng thái đầu không chụp.
**Làm:** xếp mọi bước tất định trước điểm sinh ngẫu nhiên đầu tiên, bước chạm ngẫu nhiên để cuối; quay theo lô khoảng mười ở nền rồi `feeltrace.py pairs`; báo dải frame 0 chênh, không hứa cả trace (`feel-tune` › Luật).

### GC vọt vài MB trong một frame lúc chữ mới hiện
`2026-10` · vấp 1 · suy luận · đúng với font TMP atlas Dynamic
**Thấy:** trace có một frame cấp phát vài MB ngay khi chữ mới lên màn; asset font hiện đổi trong `git status` sau Play. **Vì:** atlas động thêm glyph và dựng lại texture lúc chạy, rồi ghi ngược vào asset.
**Làm:** chốt bằng Profiler đúng frame đó trước khi sửa; đúng thì bake atlas tĩnh theo tập ký tự game dùng (`build-size` › Quy trình).

### Pipeline Editor không tới được ngay sau khi vào Play qua công cụ
`2026-10` · vấp 1 · đã đo · đúng với Windows, Editor điều khiển qua CLI hoặc MCP
**Thấy:** sau lệnh vào Play mọi lệnh báo pipeline unreachable kéo dài, `serverReachable` false. **Vì:** Editor không focus thì không tick Play, nên không tới được bước bật `runInBackground`.
**Làm:** đưa cửa sổ Editor lên trước bằng PowerShell `(New-Object -ComObject WScript.Shell).AppActivate(<pid>)`, chờ vài giây tới khi pipeline tới được, rồi bật `runInBackground` (`PlayFrom` tự bật); MCP nối lại thì nạp lại tool.

### Agent cần điều khiển Unity Editor mà chưa có đường nối
`2026-10` · vấp 1 · đã đo · đúng với Unity 6 và Claude Code
**Thấy:** cần nối agent với Editor; MCP trong `com.unity.ai.assistant` đã bị Unity bỏ, server MCP bên thứ ba cần Node. **Vì:** đường Unity hỗ trợ là Unity CLI (`unity mcp`), nói với Editor qua package `com.unity.pipeline`.
**Làm:** `winget install Unity.CLI` · `unity pipeline install` trong dự án · `unity mcp configure claude-code` (server ở user scope) · `unity skill install claude-code`; server tìm Editor qua `Library/Pipeline/.unity-pipeline-port` nên Editor phải đang mở dự án; kiểm bằng `unity command editor status`. Ưu tiên `unity` qua shell, MCP là đường thứ hai (`feel-tune` › Ngân sách token).

### Lệnh `unity` không tìm thấy dù CLI đã cài, hoặc MCP rớt giữa lượt Play
`2026-10` · vấp 1 · đã đo · đúng với Unity CLI
**Thấy:** shell báo không có lệnh `unity`; hoặc MCP mất kết nối sau `playmode start`. **Vì:** shell mở trước lúc cài chưa có thư mục CLI trong PATH; MCP và CLI dùng cùng cầu nối Editor nhưng CLI mở kết nối mới mỗi lệnh.
**Làm:** gọi `unity.exe` bằng đường dẫn tuyệt đối (memory máy giữ đường dẫn); MCP rớt thì dùng CLI: `unity cmd --project-path <p> --result-only script eval_file --file <đường dẫn Windows>`, `unity cmd --project-path <p> --timeout 600 --result-only test run --mode EditMode --filter <assembly test>`, `unity recompile --project-path <p>`; kiểm kết nối bằng `unity pipeline editors` (cột `serverReachable`). Editor compile lỗi thì vào Safe Mode và pipeline không nạp.
