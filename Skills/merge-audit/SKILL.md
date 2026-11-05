---
name: merge-audit
description: Dùng khi merge hai nhánh của project Unity — nhất là khi một nhánh có commit dời thư mục — hoặc khi nhận merge commit người khác đã đẩy lên. Merge theo bậc trong worktree riêng, kiểm trước và sau bằng công cụ dựng sẵn trong Horcrux, vì git không báo khi ghép nhầm file hay làm rơi dòng.
---

# merge-audit — merge nhánh Unity, chứng minh không mất gì

Skill công cụ. `<horcrux>` là gốc Horcrux trong dự án. Chi tiết lệnh và cách đọc kết quả: `docs/Editor/MergeAudit.md`.

## Luật

1. **Không kết luận merge sạch từ "không conflict".** Git dò rename theo độ giống; `.meta` cùng importer giống nhau ~90%,
   prefab dựng lại rớt ngưỡng — thay đổi đổ sang file khác không một dòng báo. Bằng chứng là `lines` và `assets` sau merge.
2. **Một nhánh có commit dời thư mục thì merge theo bậc**: tới cha commit dời → tới commit dời → tới đỉnh, mọi bậc
   `-X find-renames=100%`. Rename nguyên vẹn không kéo theo directory rename: file mới phía kia ở thư mục cũ phải `git mv`
   tay — `plan` liệt kê sẵn đường dẫn đích.
3. **Merge trong worktree riêng**, checkout vào nhánh làm việc khi cây đã sạch: Unity đang mở import `.meta` và asset còn
   dấu conflict, sinh GUID mới cho file nó đọc không được.
4. **File cấu hình gộp hợp hai bên, không chọn một bên:** dependency và scope trong `Packages/manifest.json` cùng lock,
   define symbol theo platform, scope registry, assembly reference của asmdef. Chọn một bên là gỡ package hay define bên kia
   đang dùng; lỗi chỉ lộ lúc compile ở máy khác.
5. **Prefab, scene conflict không lấy nguyên một bên.** Lấy bản của phía đổi cấu trúc nhiều hơn rồi ghép khối object phía
   kia thêm; kiểm fileID không trùng và cây Transform (`m_Children` ↔ `m_Father`) khớp, rồi mở trong Editor xem.
6. **Conflict ngữ nghĩa là quyết định của developer** (NT5): hai bên đổi cùng hành vi khác hướng, một bên bỏ tính năng bên
   kia vừa sửa, hai số đo cho cùng thứ. Bày phương án *giữ gì · mất gì*, chốt một đề xuất, hỏi; không chọn hộ.
7. **Mọi dòng `lines`, `assets` báo đều được phân loại** trong báo cáo: *chủ ý* (một cha sửa, xoá, gỡ có lý do — nêu lý do)
   hay *rơi* (sửa ngay). Lỗi có sẵn ở một cha thì báo, không sửa trong merge.
8. **Test golden đổi vì schema đổi thì chứng minh tương đương trước khi ghi mốc mới**: đưa field vừa bỏ trở lại đầu ra,
   hash phải khớp mốc cũ; khớp mới ghi mốc mới, kèm comment cách tái lập.
9. **Merge commit đã lên remote thì sửa bằng commit mới**, không amend, không force push — developer có thể push ngay sau
   khi agent checkout. Merge người khác đẩy lên sau đó kiểm bằng `replay --range`.
10. **Thiếu phép kiểm thì mở rộng `merge_audit.py`**, không viết script tạm bên cạnh.

## Quy trình

1. `plan <đích> <nhánh>`: đọc commit dời và bậc, cặp `GHÉP SAI GUID` / `ASSET KHÔNG THEO META`, file mới lạc thư mục,
   số conflict mỗi cách. Không có commit dời và không có cặp nào → merge thường, sang bước 5.
2. Tạo worktree từ đích: `git worktree add <thư mục tạm> <đích> --detach`.
3. Mỗi bậc: `git merge --no-ff --no-commit -X find-renames=100% <mốc bậc>` → `git mv` file lạc thư mục của bậc → giải
   conflict theo luật 4–6 → commit bậc.
4. Gói thành một merge commit hai cha để lịch sử như merge thường:
   `git commit-tree <tree của bậc cuối> -p <đích> -p <nhánh> -F <message>`, trỏ nhánh đích vào commit đó, xoá worktree.
5. `lines <merge>` và `assets <merge>`: phân loại từng dòng theo luật 7, sửa phần rơi bằng commit tiếp theo.
6. Checkout nhánh đích ở project chính, chạy `compile-check` (build, recompile, test EditMode). Lỗi do API một bên xoá mà
   code bên kia còn gọi: sửa theo ý định của bên xoá, ghi vào báo cáo.
7. Báo cáo: quyết định đã áp, phân loại phát hiện, việc Editor developer cần mở xem (prefab, scene vừa ghép; tính năng
   chạm cả hai bên — kịch bản theo `workflow` › Nghiệm thu).

## Lệnh

```bash
python -X utf8 -I "<horcrux>/Editor/MergeAudit/Tools~/merge_audit.py" plan <đích> <nhánh>
python -X utf8 -I "<horcrux>/Editor/MergeAudit/Tools~/merge_audit.py" lines  <merge>
python -X utf8 -I "<horcrux>/Editor/MergeAudit/Tools~/merge_audit.py" assets <merge>
python -X utf8 -I "<horcrux>/Editor/MergeAudit/Tools~/merge_audit.py" replay --range <mốc cũ>..<đích>
```

## Ngân sách token

- `--top` nhỏ cho lần đọc đầu, mở rộng theo phần cần soát; `lines --samples 2` đủ để phân loại.
- Không đổ `git diff` cả merge vào ngữ cảnh; mở diff từng file mà tool chỉ ra.
- Mỗi lệnh dưới 5 giây, chạy tiền cảnh.
