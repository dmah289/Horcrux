---
name: compile-check
description: Dùng sau mỗi task sửa code C# trong project Unity, trước khi báo xong hay commit — một lệnh chạy build, Unity recompile và test EditMode rồi in gọn; không chép tay chuỗi ba lệnh vào plan hay task.
---

# compile-check — một lệnh kiểm sau mỗi task

Skill dưới `Agent/horcrux.md` (NT, tầng, ký hiệu định nghĩa ở đó).
`<horcrux>` là gốc Horcrux trong dự án. Chi tiết: `docs/Editor/CompileCheck.md`.

## Luật

1. **Mỗi task code kết bằng một lần `verify.sh` xanh.** Báo cáo dán dòng tool in ra, không viết "đã compile sạch".
   **Kiểm đúng phạm vi task:** tool chỉ chạy test bị chạm bởi file đổi từ lượt test trước, không thêm lượt chạy tay bên
   cạnh. Đụng thứ dò tên không thấy (reflection, chuỗi tên type), trước merge hay release thì `--all-tests`.
2. **Không chép ba lệnh vào plan, task, constraint.** Plan ghi một dòng `bash <verify.sh> --csproj … --tests …` với tên
   của dự án.
3. **Lỗi build sau khi thêm, dời hay pull file là lỗi tạm** (type mới báo không tồn tại): csproj chưa có file. Editor mở thì
   tool Refresh trước bước build để Editor sinh lại csproj; còn lỗi thì chạy lại trước khi kết luận — bước recompile mới là
   sự thật. `--no-unity` không Refresh được.
4. **PlayMode không qua tool này** (cần Play, chiếm Editor); developer chạy trong Window > General > Test Runner.
5. **Thiếu mục thì mở rộng `verify.sh`**, không viết script kiểm mới bên cạnh.
6. **Đọc output thẳng, không pipe qua `tail`, `head`:** tool đã in gọn từng bước; pipe giữ mọi dòng tới khi lệnh xong, lệnh
   bị đẩy nền thì không biết đang kẹt bước nào. Hạn giờ của lệnh gọi phải dài hơn `--timeout` (600 s mỗi assembly).
7. **Không đổi cây làm việc (pull, checkout, merge) khi tool đang chạy:** Editor nạp file mới giữa lượt, kết quả không thuộc
   bản nào. Dừng tool, đổi cây, chạy lại. Ngay sau khi đổi cây, Editor import và báo bận: tool tự chờ tới `--ready-wait`,
   không dựng vòng chờ bên ngoài.
8. **Fail mới là của task, fail có sẵn thì không.** Tool so tên test fail với lượt trước (`(mới)`, `hết fail`, mốc
   `<giờ> @<commit>`); fail mới giữ nhãn `(mới)` tới khi pass, chỉ còn fail có sẵn thì vẫn ghi mốc cho hook. Mốc không ở bản trước task (task trước còn dở, vừa pull) thì chạy một lần trước khi sửa hay merge.
   Báo cáo tách hai nhóm; fail có sẵn báo một dòng, không sửa trong task.

## Lệnh

```bash
bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj <Gameplay.csproj,Editor.csproj,Tests.csproj> --tests <Project.EditModeTests,Horcrux.EditModeTests>
bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj <A.csproj> --no-unity   # máy không mở Editor
```

Tên csproj và assembly test của dự án ghi trong file hướng dẫn agent của dự án; chưa có thì lấy từ `.asmdef` trong
`Assets/` và csproj cùng tên ở gốc. Task chạm Horcrux luôn kèm `com.horcrux.runtime.csproj,com.horcrux.editor.csproj` và
`Horcrux.EditModeTests`.

## Quy trình

1. Sửa code xong, chạy lệnh đầy đủ từ gốc project.
2. `FAIL build`: đọc dòng lỗi, sửa, chạy lại. `build: không có dotnet …`: đặt `MSBUILD=<MSBuild.exe>`, không coi là xanh. `FAIL recompile` mà build ok: assembly chưa có trong `--csproj`, hoặc lỗi ở
   asmdef, define — đọc `errors`. `FAIL unity` (Editor chưa mở): báo là chưa kiểm phần Unity, không coi là xanh.
3. `FAIL tests`: tên test và dòng đầu message in dưới dòng tổng; mở đúng test đó.
4. Dán dòng `verify: OK` và các dòng `tests …: n/m pass` vào báo cáo, kèm dòng `so với lượt …` khi có fail.

## Ngân sách token

- Tool in tối đa 40 dòng lỗi mỗi bước và 300 ký tự mỗi test fail; không đổ Editor.log hay JSON thô vào ngữ cảnh.
- Lệnh chạy 10–40 giây khi chỉ chạy test bị chạm (số giây in ở mỗi bước): chạy tiền cảnh, không song song hai lần
  (recompile và test chiếm Editor).
