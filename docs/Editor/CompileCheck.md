# CompileCheck — một lệnh kiểm sau mỗi task

`verify.sh` chạy ba phép kiểm mà mọi task code đều cần, theo thứ tự rẻ tới đắt, và in một dòng kết quả cho mỗi bước: build
các csproj Unity sinh bằng `dotnet` hoặc MSBuild → Unity recompile trong Editor đang mở → test EditMode theo assembly. Mã thoát 1 khi một bước hỏng, nên dùng được
làm điều kiện commit.

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | agent sau mỗi task sửa code, trước khi báo xong hay commit; quy trình ở `Skills/compile-check/SKILL.md` |
| Đầu vào | csproj ở gốc project do Unity sinh (gitignore) · Editor đang mở project (`unity status` ra `ready`) · tên assembly test EditMode |
| Ranh giới | tool chỉ **chạy và tóm tắt**; sửa lỗi là việc đọc code sau đó. Không chạy PlayMode: PlayMode cần Play và chiếm Editor, developer chạy trong Test Runner |
| Chỗ đặt | `Editor/CompileCheck/Tools~/verify.sh`: thư mục đuôi `~` Unity không import. Không có C# |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Chạy Unity batchmode khi Editor đóng | Editor đang mở giữ khoá project; batchmode mất 1–2 phút khởi động cho mỗi lần kiểm, recompile trong Editor mất vài giây |
| Tự tìm csproj và assembly test | mỗi dự án một bộ tên; đoán sai thì kiểm thiếu mà vẫn báo OK |
| Vá đường dẫn csproj sau khi `git mv` | Editor đang mở tự sinh lại csproj; lỗi "file not found" của builder sau khi dời file là lỗi tạm, bước recompile mới là sự thật |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| Giữ cả bước build csproj dù Unity recompile đã đủ | builder báo đủ mọi lỗi của một assembly trong một lần và in chúng ra stdout; Unity dừng ở assembly đầu tiên hỏng và chỉ ghi console |
| Đọc JSON của Unity CLI bằng `raw_decode`, không `json.load` | CLI in thêm một dòng hint của plugin sau JSON; `json.load` ném "Extra data" |
| Lỗi build lọc bằng `sort -u` | MSBuild in mỗi lỗi hai lần (một của compiler, một của tổng kết) |
| `AssetDatabase.Refresh` qua `script eval` trước `script recompile` | Editor không focus thì chưa nạp file sửa từ ngoài; recompile trả `up_to_date` trên bản cũ và bước này báo xanh giả |
| Đợi `unity status` ra `ready` (tối đa 60 s) giữa recompile và test | recompile xong còn domain reload, Pipeline ngắt vài giây; `test run` gửi lúc đó trả `EDITOR_NOT_RUNNING` |

## Luồng dữ liệu

```
--csproj a,b ──► chọn builder (MSBUILD › dotnet › MSBuild Rider/VS) ──► build -v q -clp:ErrorsOnly ──► "build a: ok" | dòng lỗi CS/MSB | mã thoát ≠ 0
--project   ──► unity status ready? ──► AssetDatabase.Refresh ──► script recompile ──► poll recompile_status tới completed|up_to_date ──► errors[]
--tests X,Y ──► test run --mode EditMode --filter_type assembly --filter X ──► (in_progress? poll test status) ──► Summary + test fail
                                                                                                          └──► "verify: OK" | "verify: FAIL build recompile tests"
```

## Lệnh

```bash
bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj <A.csproj,B.csproj> --tests <TestAsm1,TestAsm2> [--project <path>] [--no-unity] [--no-tests] [--timeout 300]
```

| Cờ | Nghĩa |
|---|---|
| `--csproj` | danh sách csproj cách nhau dấu phẩy, đường dẫn tính từ `--project`; bỏ trống thì bỏ bước build |
| `--tests` | assembly test EditMode (tên trong `.asmdef`), mỗi assembly một lần chạy; bỏ trống thì bỏ bước test |
| `--project` | gốc project Unity; mặc định thư mục hiện tại |
| `--no-unity` | chỉ build csproj, cho máy không mở Editor |
| `--no-tests` | dừng sau recompile |
| `--timeout` | giây chờ mỗi lần `test run`, mặc định 300 |
| `UNITY_CLI` | biến môi trường ép đường dẫn `unity.exe`; mặc định `unity` trên PATH |
| `MSBUILD` | biến môi trường ép đường dẫn `MSBuild.exe`; không đặt thì dùng `dotnet` trên PATH, thiếu nữa thì MSBuild mới nhất của Rider hay Visual Studio trong `Program Files` ổ c–h |

Thời gian: build 25–30 s mỗi csproj, recompile 5–20 s, test EditMode vài giây tới một phút.

## Đọc kết quả

| Thấy gì | Nghĩa là | Soi tiếp ở đâu |
|---|---|---|
| `build X: không có file` | Unity chưa sinh csproj cho assembly đó | mở Editor, Edit > Preferences > External Tools > Regenerate project files |
| `build: không có dotnet …` | máy không có builder nào tool tìm được | đặt `MSBUILD=<đường dẫn MSBuild.exe>` rồi chạy lại |
| `build X: LỖI` kèm `CS2001` | csproj trỏ file đã dời | lỗi tạm sau `git mv`; chờ Editor sinh lại rồi chạy lại, bước recompile quyết |
| `build X: LỖI (mã thoát n, …)` | builder hỏng mà không in lỗi biên dịch | đọc 20 dòng output in kèm |
| `build X: ok` nhưng `recompile: LỖI` | assembly không có trong `--csproj` (asmdef mới, test) hoặc define khác | đọc `errors` in ra; csproj thiếu assembly mới thì thêm tên vào `--csproj` |
| `recompile: không xong sau 180 s` | Editor treo ở dialog, hoặc đang import | nhìn Editor; Unity bận import asset nặng thì chạy lại |
| `tests X: không đọc được kết quả` | assembly không có test, lệnh timeout, hoặc Editor đang Play | thân JSON in kèm 600 ký tự đầu |
| `tests X: n/m pass, k fail` | tên test fail và dòng đầu message in ngay dưới | mở test đó |

## Đã sai một lần

- Máy không có `dotnet`: lệnh build chỉ in "command not found", bước lọc dòng `error CS` không thấy gì nên báo `build: ok` trên code
  đang lỗi. Bài học: chọn builder trước, không có thì FAIL; mã thoát khác 0 cũng là LỖI, không chỉ dòng `error CS`.
- Glob `/?/Program Files` trong Git Bash không khớp ổ đĩa nào (`/` không liệt kê ổ): tìm MSBuild phải duyệt từng chữ ổ.
- Vá csproj bằng tay sau `git mv` rồi kết luận lỗi đường dẫn là lỗi thật: Editor đang mở sinh lại csproj, bản vá bị ghi đè hoặc thừa.
- Chạy batchmode `-runTests` khi Editor đang mở project: khoá `Temp/UnityLockfile`, lệnh treo tới timeout không một dòng lỗi.
- Chạy Unity với `-nographics` để test: test của package PrimeTween fail vì shader, không liên quan code dự án.

## Nghiệm thu

Chạy trên project có lỗi cố ý ở một file: `build` in đúng một dòng lỗi, `recompile: LỖI` in cùng lỗi, mã thoát 1. Sửa lỗi, chạy lại:
mọi bước ok, `verify: OK`, mã thoát 0. Chạy `--no-unity` khi Editor đóng: chỉ có dòng build, mã thoát theo build. Máy không builder (hoặc `MSBUILD` trỏ sai): `verify: FAIL build`,
mã thoát 1. `MSBUILD` trỏ lệnh thoát 1 không in gì (`/usr/bin/false`): `build X: LỖI (mã thoát 1, …)`.
