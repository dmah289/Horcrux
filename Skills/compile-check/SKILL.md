---
name: compile-check
description: Dùng sau mỗi task sửa code C# trong project Unity, trước khi báo xong hay commit — một lệnh chạy dotnet build, Unity recompile và test EditMode rồi in gọn; không chép tay chuỗi ba lệnh vào plan hay task.
---

# compile-check — một lệnh kiểm sau mỗi task

Đường dẫn tính từ gốc Horcrux (dự án hiện tại: `Assets/Horcrux/`). Chi tiết ở `docs/Editor/CompileCheck.md`.

## Luật

1. **Mỗi task code kết bằng một lần `verify.sh` xanh.** Kết quả ghi vào báo cáo bằng dòng tool in ra, không bằng "đã compile sạch".
2. **Không chép ba lệnh vào plan, task, constraint.** Plan ghi một dòng: `bash <verify.sh> --csproj … --tests …` với tên của dự án.
3. **Lỗi `dotnet` sau khi dời file là lỗi tạm.** Editor đang mở tự sinh lại csproj; chạy lại trước khi kết luận, và bước recompile mới là sự thật.
4. **PlayMode không qua tool này.** Test PlayMode cần Play và chiếm Editor; developer chạy trong Window > General > Test Runner.
5. **Thiếu mục thì mở rộng `verify.sh`**, không viết script kiểm mới bên cạnh.

## Lệnh

```bash
bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj <Gameplay.csproj,Editor.csproj,Tests.csproj> --tests <Project.EditModeTests,Horcrux.EditModeTests>
```

Tên csproj và assembly test của từng dự án ghi trong file hướng dẫn agent của dự án (`CLAUDE.md`); chưa có thì lấy từ `.asmdef`
trong `Assets/` và csproj cùng tên ở gốc. Horcrux luôn kèm `com.horcrux.runtime.csproj,com.horcrux.editor.csproj` và
`Horcrux.EditModeTests` khi task chạm Horcrux.

```bash
bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj <A.csproj> --no-unity   # máy không mở Editor
```

## Quy trình

1. Sửa code xong, chạy lệnh đầy đủ từ gốc project.
2. `verify: FAIL build`: đọc dòng lỗi in ra, sửa, chạy lại. `FAIL recompile` mà build ok: assembly đó chưa có trong `--csproj`, hoặc lỗi
   nằm ở asmdef, define; đọc `errors`.
3. `FAIL tests`: tên test và dòng đầu message in dưới dòng tổng; mở đúng test đó.
4. Dán dòng `verify: OK` và các dòng `tests …: n/m pass` vào báo cáo của task.

## Ngân sách token

- Tool in tối đa 40 dòng lỗi mỗi bước và 300 ký tự mỗi test fail; không đổ Editor.log hay JSON thô vào ngữ cảnh.
- Lệnh chạy 30–90 giây tuỳ số csproj: chạy tiền cảnh, không song song hai lần (recompile và test chiếm Editor).
