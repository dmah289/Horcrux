---
name: compile-check
description: Dùng sau mỗi task sửa code C# trong project Unity, trước khi báo xong hay commit — một lệnh chạy build, Unity recompile và test EditMode rồi in gọn; không chép tay chuỗi ba lệnh vào plan hay task.
---

# compile-check — một lệnh kiểm sau mỗi task

Skill dưới `Agent/horcrux.md` (NT, tầng, ký hiệu định nghĩa ở đó).
`<horcrux>` là gốc Horcrux trong dự án. Cờ, luồng, cách đọc kết quả: `docs/Editor/CompileCheck.md`.

## Luật

- **Mỗi task code kết bằng một lần `verify.sh`, chạy khi phần code đã xong** — không sau mỗi sửa nhỏ giữa chừng; sửa lỗi
  verify báo thì chạy lại một lần. Báo cáo dán dòng tool in ra (`verify: OK`, `tests …: n/m pass`, dòng
  `so với lượt …` khi có fail), không viết "đã compile sạch". `FAIL unity` (Editor chưa mở) hay thiếu builder là chưa kiểm,
  không phải xanh.
- **Kiểm đúng phạm vi task:** tool chỉ chạy test bị chạm bởi file đổi; đụng thứ dò tên không thấy (reflection, chuỗi tên
  type), trước merge hay release thì `--all-tests`. Task chạm Horcrux luôn kèm csproj và assembly test của Horcrux.
- **Fail mới là của task, fail có sẵn thì không.** Tool gắn nhãn so với lượt trước (`(mới)`, `hết fail`, `(chưa có mốc)`);
  báo cáo tách hai nhóm, fail có sẵn báo một dòng, không sửa trong task, không chạy lại để đổi kết quả. Mốc không ở bản trước task (vừa pull, task trước
  dở) thì chạy một lần trước khi sửa hay merge.
- **Editor mở thì recompile là phép kiểm biên dịch;** tool chỉ build csproj khi không có Editor hoặc recompile LỖI (để in đủ
  lỗi). Lỗi build ngay sau khi thêm, dời, pull file là lỗi tạm (csproj chưa có file) — recompile mới là sự thật.
- **Không đổi cây làm việc (pull, checkout, merge) khi tool đang chạy:** Editor nạp file giữa lượt, kết quả không thuộc bản
  nào: dừng tool, đổi cây, chạy lại. Ngay sau khi đổi cây, tool tự chờ Editor import xong; không dựng vòng chờ bên ngoài.
- **PlayMode không qua tool này** (cần Play, chiếm Editor): developer chạy trong Test Runner.
- **Plan, task chỉ ghi một dòng lệnh** với tên csproj, assembly test của dự án; không chép ba bước build–recompile–test.

## Lệnh

```bash
bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj <Gameplay.csproj,Editor.csproj,Tests.csproj> --tests <Project.EditModeTests,Horcrux.EditModeTests>
bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj <A.csproj> --no-unity   # máy không mở Editor
```

Tên csproj và assembly test ghi trong file hướng dẫn agent của dự án; chưa có thì lấy từ `.asmdef` trong `Assets/` và
csproj cùng tên ở gốc. Horcrux: `com.horcrux.runtime.csproj,com.horcrux.editor.csproj` và `Horcrux.EditModeTests`.

| Kết quả | Nghĩa |
|---|---|
| `FAIL build` | lỗi C# trong csproj; `build: không có dotnet …` thì đặt `MSBUILD=<MSBuild.exe>` |
| `FAIL recompile` mà build ok | assembly chưa có trong `--csproj`, hoặc lỗi ở asmdef, define — đọc `errors` |
| `FAIL tests` | tên test và dòng đầu message in dưới dòng tổng; mở đúng test đó |

Lệnh chạy 10–40 giây khi chỉ chạy test bị chạm, chạy tiền cảnh; hạn giờ lệnh gọi dài hơn `--timeout`.
