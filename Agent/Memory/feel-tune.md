# feel-tune — kinh nghiệm

Bài học đã trả giá khi làm việc với `feel-tune`. Đọc trọn cùng lúc với rule hoặc skill đó. Hình dạng và vòng đời ở `docs` › Agent.

### Lệnh tới Editor trả 504 "waiting for another command to finish"
`2026-11` · vấp 1 · đã đo · đúng với Editor dùng chung với session khác
**Thấy:** lệnh trả 504 "waiting for another command to finish"; lượt Play kẹt giữa chừng, `playmode stop` không chạy. **Vì:** hàng lệnh của Editor đang bị session khác giữ.
**Làm:** trước `playrun.sh` xem `git status` có ai đang sửa, `editor status` có `compiling` không; PlayerPrefs trả hỏng thì chạy lệnh trả tay `playrun.sh` in ra.

### `eval_file` gọi từ Bash báo "File must have a .cs extension"
`2026-11` · vấp 1 · đã đo · đúng với Git Bash trên Windows
**Thấy:** CLI `script eval_file` gọi từ Git Bash báo file phải có đuôi `.cs` dù đường dẫn đúng. **Vì:** MSYS tự đổi đối số trông như đường dẫn POSIX trước khi truyền cho chương trình Windows.
**Làm:** đặt `MSYS_NO_PATHCONV=1` và truyền đường dẫn Windows đầy đủ lấy từ `cygpath -w`.

### Play ra lỗi thiếu service, event đăng ký trùng sau một eval mở scene
`2026-10` · vấp 1 · đã đo · đúng với service toàn cục nằm ở scene do scene khởi động nạp
**Thấy:** Play báo thiếu service — code không đổi gì. **Vì:** eval `OpenScene` để lại scene khác scene khởi động; Play từ đó bỏ qua bước nạp scene chứa service.
**Làm:** eval nào mở scene thì cuối cùng mở lại scene khởi động; thiếu service lúc Play thì xem scene đang mở trước khi sửa code.

### GC vọt vài MB trong một frame lúc chữ mới hiện
`2026-10` · vấp 1 · suy luận · đúng với font TMP atlas Dynamic
**Thấy:** trace có một frame cấp phát vài MB ngay khi chữ mới lên màn; asset font hiện đổi trong `git status` sau Play. **Vì:** atlas động thêm glyph và dựng lại texture lúc chạy, rồi ghi ngược vào asset.
**Làm:** chốt bằng Profiler đúng frame đó trước khi sửa; đúng thì bake atlas tĩnh theo tập ký tự game dùng (`build-size` › Chọn mục).

### Pipeline Editor không tới được ngay sau khi vào Play qua công cụ
`2026-10` · vấp 1 · đã đo · đúng với Windows, Editor điều khiển qua CLI hoặc MCP
**Thấy:** sau lệnh vào Play mọi lệnh báo pipeline unreachable kéo dài, `serverReachable` false. **Vì:** Editor không focus thì không tick Play, nên không tới được bước bật `runInBackground`.
**Làm:** đưa cửa sổ Editor lên trước bằng PowerShell `(New-Object -ComObject WScript.Shell).AppActivate(<pid>)`, chờ tới khi pipeline tới được, rồi bật `runInBackground` (`PlayFrom` tự bật); MCP nối lại thì nạp lại tool.

### Agent cần nối, hay nối lại, với Unity Editor
`2026-10` · vấp 2 · đã đo · đúng với Unity 6 và Claude Code
**Thấy:** cần nối agent với Editor; hoặc shell báo không có lệnh `unity` dù đã cài, MCP mất kết nối sau `playmode start`. **Vì:** đường Unity hỗ trợ là Unity CLI (`unity mcp`) nói với Editor qua package `com.unity.pipeline` — MCP trong `com.unity.ai.assistant` đã bị bỏ · shell mở trước lúc cài chưa có CLI trong PATH · MCP và CLI dùng chung cầu nối nhưng CLI mở kết nối mới mỗi lệnh.
**Làm:** cài `winget install Unity.CLI` · `unity pipeline install` trong dự án · `unity mcp configure claude-code` · `unity skill install claude-code`; Editor phải đang mở dự án (server tìm qua `Library/Pipeline/.unity-pipeline-port`). Gọi `unity.exe` bằng đường dẫn tuyệt đối (memory máy); MCP rớt thì dùng CLI: `unity cmd --project-path <p> --result-only script eval_file --file <đường dẫn Windows>`, `unity cmd --project-path <p> --timeout 600 --result-only test run --mode EditMode --filter <assembly test>`, `unity recompile --project-path <p>`; kiểm bằng `unity pipeline editors` (cột `serverReachable`). Editor compile lỗi thì vào Safe Mode, pipeline không nạp.

### Bản sửa hiệu năng làm theo nguyên nhân ước từ thông số phần cứng chỉ gỡ được một phần
`2026-11` · vấp 1 · đã đo · đúng với đo FPS trên máy thật
**Thấy:** chốt nguyên nhân GPU bằng phép tính × số pixel ÷ GFLOPS của chip, cộng tương quan FPS giữa hai lần đo cách nhau; sửa xong chỉ lên 4–8 FPS. **Vì:** ước từ thông số lệch nhiều lần (băng thông, trần xung bị siết); hai lần đo cách nhau phút lẫn biến khác (trần xung trôi, quảng cáo, vừa vào màn).
**Làm:** trước khi sửa, tách biến bằng phép thử trên đúng màn, hai bên đo liền nhau ≥ 20 s: bật–tắt thứ nghi ngờ, hoặc đổi độ phân giải (`docs/Editor/DeviceProbe.md` › `res-ab`); báo nguyên nhân kèm phép thử đó, không kèm phép ước.

### Script eval dừng ở lỗi của chính script, việc đang làm chưa chạy hay đã chạy xong
`2026-11` · vấp 1 · đã đo · đúng với `script eval` của Unity CLI/MCP
**Thấy:** `CS0104 'Object' is an ambiguous reference` · `There is no 'MeshRenderer' attached` ở `GetComponent<MeshRenderer>()?.sortingOrder` · `Transform has been destroyed` ở dòng `return` sau khi prefab đã lưu xong. **Vì:** giới hạn của eval và object giả-null · `UnloadPrefabContents` huỷ cây vừa sửa, đọc lại nó sau đó là đọc object đã huỷ.
**Làm:** viết eval theo `editor-first` › tra cứu › Eval và Play qua công cụ · sửa prefab qua `PrefabEdit.Edit` (`docs/Editor/UIReskin.md`), giá trị cần trả dựng thành chuỗi trước khi dỡ; lỗi ở `return` thì soát file trên đĩa trước khi chạy lại · script dài viết file `.cs`, chạy `script eval_file` để sửa một chỗ rồi chạy lại.

### Đường tới cheat trên máy thật kẹt ở màn hướng dẫn hoặc mã PIN
`2026-11` · vấp 1 · đã đo · đúng với build phát hành giấu bảng debug sau thao tác bí mật
**Thấy:** chạm qua adb tới bảng debug thì màn hướng dẫn chặn nút, rồi bảng đòi PIN. **Vì:** lớp hướng dẫn nuốt chạm ngoài ô được chỉ; PIN là mã truy cập, agent không nhập.
**Làm:** chơi qua màn hướng dẫn trước (chạm theo tay chỉ); chuỗi chạm nhanh gửi trong một `adb shell "for …; do input tap …; done"` cho khớp giới hạn giữa hai chạm; tới bước PIN thì gom mọi thao tác cheat vào một lời nhờ developer, kèm "không bật Bypass" khi đang kiểm tracking (`docs/Editor/DeviceProbe.md`).

### Doanh thu quảng cáo dồn về giá trị mặc định (level đầu, placement "0") mà code không gửi giá trị đó
`2026-11` · vấp 1 · đã đo · đúng với SDK quảng cáo tự log doanh thu thẳng lên analytics, còn event của game đi qua cổng có anti-cheat
**Thấy:** một nhóm nhỏ user có doanh thu quảng cáo dồn ở giá trị mặc định của level và placement; code không có chỗ sinh giá trị đó. **Vì:** máy cài APK ngoài store bị anti-cheat của game coi là cheater, cổng analytics của game bỏ mọi event và user property, nhưng SDK quảng cáo log `ad_impression` thẳng và hỏi anti-cheat riêng của SDK (chưa đăng ký nên luôn "không cheat"); báo cáo ghép thiếu nên rơi về mặc định.
**Làm:** đo trên máy cắm cáp sớm, trước khi đoán tiếp: DeviceProbe `events` in nguồn cài, event theo tên và tham số, callback doanh thu của SDK (`docs/Editor/DeviceProbe.md` › Kiểm event analytics). Sửa bằng một luật: đăng ký interface anti-cheat của SDK trỏ về anti-cheat của game, hoặc cho cả hai cùng qua rồi lọc theo nguồn cài ở phía dữ liệu.
