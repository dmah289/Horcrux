Code: `Editor/PartialCommit/Tools~/partial_commit.py`

# PartialCommit — commit đúng phần của mình khi working tree có sửa dở của người khác

Một lệnh liệt kê hunk, một lệnh commit các hunk và file đã chọn. Commit dựng trong **index tạm** từ `HEAD`, nên index thật (thứ
developer đã stage) và working tree không bị đụng; sau commit, path chưa ai stage được đưa index theo commit mới để `git status`
chỉ còn phần chưa commit.

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | agent commit việc của mình khi developer hay session khác đang sửa dở cùng repo, kể cả cùng file (skill `workflow` › commit) |
| Đầu vào | path tính từ gốc repo chứa thư mục hiện tại; submodule thì chạy trong submodule |
| Ranh giới | chỉ tạo commit và dời nhánh hiện tại; không push, không sửa working tree, không chạm path đã stage riêng |
| Chỗ đặt | `Editor/PartialCommit/Tools~/`; cần `git`, Python 3; không có C# |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Chọn hunk theo nội dung (regex) | danh sách đánh số in ngay trước khi commit là đủ; regex chọn nhầm im lặng khi hai bên sửa cùng chữ |
| Tự tách một hunk thành nhiều phần | hunk `-U0` chỉ gộp các dòng liền nhau; không máy nào biết dòng nào của ai — người commit chép hunk ra file, giữ dòng của mình, đưa vào `--patch` |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| Diff `-U0`, áp bằng `--unidiff-zero` | ngữ cảnh 3 dòng gộp sửa của hai người nằm gần nhau vào một hunk; `-U0` tách tới từng dòng liền khối |
| Tool đánh lại số dòng phía mới của mọi hunk trước khi áp | `git apply --unidiff-zero` đặt hunk chỉ-chèn theo số phía mới; số của diff đầy đủ đã cộng độ lệch của hunk bị bỏ qua, giữ nguyên thì dòng chèn rơi sai chỗ không báo lỗi (đã rơi lên đầu file một lần) |
| Không dùng `git commit -- <path>` | commit theo pathspec lấy nguyên file trong working tree, kéo theo sửa dở của người khác trong cùng file |
| Index thật chỉ đổi ở path mà index trùng `HEAD` cũ | path đã stage riêng là ý định của người khác; ghi đè làm mất phần họ đã chọn |

## Dùng

```bash
python -X utf8 -I "<horcrux>/Editor/PartialCommit/Tools~/partial_commit.py" list Skills/workflow/SKILL.md Plans/A.md
python -X utf8 -I "<horcrux>/Editor/PartialCommit/Tools~/partial_commit.py" commit -m @msg.txt \
    --hunks Skills/workflow/SKILL.md:2,5-6 --whole docs/Editor/New.md --whole Plans/Old.md
```

| Tham số | Nghĩa |
|---|---|
| `list <path>...` | in hunk từng file, số từ 1, kèm hai dòng đổi đầu |
| `-m <chuỗi>` · `-m @<file>` | message; `@file` cho message nhiều dòng (UTF-8) |
| `--hunks <path>:<n,n-m>` | các hunk đã chọn của file đó, theo số của `list` chạy trên cùng working tree |
| `--whole <path>` | cả file: thêm, sửa, **xoá** (path không còn trên đĩa), hoặc con trỏ submodule (lấy `HEAD` của submodule) |
| `--patch <file>` | hunk sửa tay, khi hai người sửa hai dòng liền nhau hay cùng một dòng: chép hunk từ `git diff -U0 HEAD -- <path>` (giữ dòng `diff --git`, `---`, `+++`), chỉ giữ dòng của mình, sửa số đếm trong header `@@`; số dòng phía mới tool tự đánh lại; áp như `--hunks` |

Kết quả in `git log --stat` của commit mới. Cảnh báo khi `HEAD` detached (đưa nhánh lên commit bằng `git branch -f <nhánh> HEAD`)
và khi một path có phần đã stage riêng (index giữ nguyên, `git status` thấy chênh với `HEAD` mới).

## Bẫy

| Bẫy | Hệ quả |
|---|---|
| Chạy `list` rồi để working tree đổi trước `commit` | số hunk trôi, commit nhầm hunk; `list` và `commit` liền nhau |
| Một file vừa `--hunks` vừa `--patch` | hunk `-U0` không có ngữ cảnh, hunk áp trước làm trôi số dòng của hunk sau; tool từ chối, gom mọi hunk của file đó vào một patch |
| `--whole` trên file người khác cũng đang sửa | file lấy nguyên lúc commit, kể cả phần họ ghi sau lần soát; dùng `--hunks`, và soát số dòng mỗi file trong `--stat` tool in ra |
| File mới chưa track đưa vào `--hunks` | diff với `HEAD` của file chưa track rỗng; file mới dùng `--whole` |
| Commit trong submodule mà quên con trỏ ở dự án | dự án vẫn trỏ bản cũ; chạy thêm một lần ở dự án với `--whole <đường dẫn submodule>` |
