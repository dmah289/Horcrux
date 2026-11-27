# merge-audit — tra cứu

Nền tảng và Sổ tay của `merge-audit`. Đọc mục được bảng Tra cứu trong `SKILL.md` gọi.

## Merge theo bậc

> **Nền tảng** — rename nguyên vẹn (`-X find-renames=100%`) không kéo theo directory rename: file mới phía kia ở thư mục cũ
> nằm lại thư mục cũ. Windows giới hạn 260 ký tự đường dẫn: vượt thì `worktree add` báo `Could not reset index file`,
> `worktree remove` báo `Filename too long`. Con trỏ submodule: phía kia đẩy con trỏ cũ (máy họ chưa cập nhật submodule),
> phía này không đổi so với base, git lấy bản cũ; pull fast-forward thì dấu hiệu là `git status` báo submodule `M` ngay sau
> pull.

**Sổ tay** — một lượt merge đã chạy được:

1. `plan <đích> <nhánh>`: đọc commit dời và bậc, cặp `GHÉP SAI GUID` / `ASSET KHÔNG THEO META`, file mới lạc thư mục, số
   conflict mỗi cách. Không commit dời, không cặp nào → merge thường, sang bước 5.
2. Worktree từ đích, đường dẫn ngắn: `git -c core.longpaths=true worktree add <thư mục tạm> <đích> --detach`; gỡ cũng kèm cờ
   đó. Worktree không cần index của checkout chính sạch, nên không `stash`.
3. Mỗi bậc: `git merge --no-ff --no-commit -X find-renames=100% <mốc bậc>` → `git mv` file lạc thư mục của bậc → giải
   conflict theo Luật → commit bậc.
4. Gói thành một merge commit hai cha: `git commit-tree <tree bậc cuối> -p <đích> -p <nhánh> -F <message>`, đưa nhánh đích
   lên commit đó bằng `merge --ff-only` ở checkout chính (chỉ từ chối khi chạm đúng file đang bẩn), xoá worktree.
5. `lines <merge>`, `assets <merge>`: phân loại từng dòng, sửa phần rơi bằng commit tiếp theo.
6. Checkout đích ở project chính, `compile-check`.
7. Báo cáo: quyết định đã áp, phân loại phát hiện, prefab, scene vừa ghép developer cần mở xem, kịch bản chơi thử cho tính
   năng chạm cả hai bên (`workflow` › Nghiệm thu).

**Sổ tay** — submodule lùi: trả con trỏ bằng commit mới, báo người đẩy. Golden đổi vì schema: đưa field vừa bỏ trở lại đầu
ra, hash phải khớp mốc cũ; khớp mới ghi mốc mới, kèm comment cách tái lập. Prefab ghép xong: fileID không trùng, cây
Transform (`m_Children` ↔ `m_Father`) khớp — `prefab_yaml.py check`.
