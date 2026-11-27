# CompileCheck — một lệnh kiểm sau mỗi task

`verify.sh` chạy ba phép kiểm mà mọi task code đều cần, theo thứ tự rẻ tới đắt, và in một dòng kết quả kèm số giây cho mỗi
bước: build các csproj Unity sinh bằng `dotnet` hoặc MSBuild → Unity recompile trong Editor đang mở → test EditMode **bị chạm
bởi file đổi từ lượt test trước** (`affected_tests.py`; `--all-tests` thì cả assembly). Mã thoát 1 khi một bước hỏng, nên
dùng được làm điều kiện commit.

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | agent sau mỗi task sửa code, trước khi báo xong hay commit; quy trình ở `Agent/Skills/compile-check/SKILL.md` |
| Đầu vào | csproj ở gốc project do Unity sinh (gitignore) · Editor đang mở project (`unity status` ra `ready`) · tên assembly test EditMode |
| Ranh giới | tool chỉ **chạy và tóm tắt**; sửa lỗi là việc đọc code sau đó. Không chạy PlayMode: PlayMode cần Play và chiếm Editor, developer chạy trong Test Runner |
| Chỗ đặt | `Editor/CompileCheck/Tools~/verify.sh`: thư mục đuôi `~` Unity không import. Không có C# |
| Công cụ anh em | `affected_tests.py` — chọn test bị chạm, verify.sh gọi (mục Chọn test) · `plan_check.py` — kiểm code của Plan trước khi giao (mục cuối file này); hỏi builder qua `verify.sh --print-builder` |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Chạy Unity batchmode khi Editor đóng | Editor đang mở giữ khoá project; batchmode mất 1–2 phút khởi động cho mỗi lần kiểm, recompile trong Editor mất vài giây |
| Tự tìm csproj và assembly test | mỗi dự án một bộ tên; đoán sai thì kiểm thiếu mà vẫn báo OK |
| Vá đường dẫn csproj sau khi `git mv` | Editor đang mở tự sinh lại csproj; lỗi "file not found" của builder sau khi dời file là lỗi tạm, bước recompile mới là sự thật |
| Chọn test bằng graph dựng sẵn (graphify) | graph cũ đi sau mỗi lần sửa, cập nhật mất ~23 s; graph gắn partial class về một file nên đổi file partial kia ra 0 test (đo: đổi một partial của lớp config, test đọc chính lớp đó không được chọn) |
| Chạy nhiều lớp trong một lượt `test run` | bộ lọc `testName` của Unity CLI nhận một chuỗi con; mỗi lớp một lượt |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| Giữ cả bước build csproj dù Unity recompile đã đủ | builder báo đủ mọi lỗi của một assembly trong một lần và in chúng ra stdout; Unity dừng ở assembly đầu tiên hỏng và chỉ ghi console |
| Đọc JSON của Unity CLI bằng `raw_decode`, không `json.load` | CLI in thêm một dòng hint của plugin sau JSON; `json.load` ném "Extra data" |
| Lỗi build lọc bằng `sort -u` | MSBuild in mỗi lỗi hai lần (một của compiler, một của tổng kết) |
| `AssetDatabase.Refresh` qua `script eval` **trước bước build** khi Editor mở | Editor không focus thì chưa nạp file sửa từ ngoài: recompile trả `up_to_date` trên bản cũ và báo xanh giả; file mới từ pull, merge chưa vào csproj nên build báo `CS0246` giả |
| Editor đang bận (import sau pull) thì chờ tới `--ready-wait`, không báo `FAIL unity` | import vài chục file mất vài phút, lệnh gửi lúc đó timeout; không Editor nào mở project thì không chờ |
| Ghi tên test fail vào `Library/HorcruxVerify/<assembly>.fail`; fail mới ghi `+tên` và giữ nhãn `(mới)` tới khi pass | fail có sẵn trước task chỉ phân biệt được với fail task gây ra bằng tên, không bằng số; ghi đè thẳng thì lượt chạy lại thứ hai coi fail mới là có sẵn. `Library` không vào git, mỗi máy một mốc; xoá file để đặt lại mốc |
| Ghi tên mọi test từng chạy vào `<assembly>.seen`; test fail chưa từng chạy mà tên hàm có ở HEAD (`git grep --recurse-submodules`) mang nhãn `(chưa có mốc)`, không chặn | tool chỉ chạy lớp bị chạm nên test có sẵn hay chạy lần đầu: dữ liệu commit trước đó làm nó đỏ thì nhãn `(mới)` chặn hook tới khi developer sửa dữ liệu. Test task vừa viết không có ở HEAD nên vẫn là `(mới)`; test từng chạy qua rồi fail là `(mới)`. Giá: task làm đỏ một test có sẵn chưa từng chạy trên máy này cũng ra `(chưa có mốc)` — đọc message trước khi báo |
| Ghi `Library/HorcruxVerify/last-ok` khi OK, hoặc khi chỉ còn fail có sẵn (dòng cuối `verify: FAIL tests chỉ là fail có sẵn (…)`, vẫn thoát mã 1); hỏng khác thì để nguyên | hook Stop của Agent (`Agent/Tools~/agent_hooks.py`) so mtime file `.cs` agent sửa với mốc này để chặn báo xong khi chưa kiểm; mốc chỉ dời khi task không làm hỏng gì. Chỉ tin `verify: OK` thì assembly còn fail từ trước làm hook chặn mọi lượt |
| Đợi `unity status` ra `ready` (tối đa 60 s) giữa recompile và test | recompile xong còn domain reload, Pipeline ngắt vài giây; `test run` gửi lúc đó trả `EDITOR_NOT_RUNNING` |
| `test run` trả `success: false` thì hỏi `test status`, không báo hỏng ngay | kết nối CLI rớt giữa lượt ("socket hang up") trong khi test vẫn chạy trong Editor. Editor bận (domain reload vì file đổi giữa lượt) thì chờ tới `--ready-wait`; `test status` không có kết quả thì gửi lại `test run` một lần — reload cắt lượt test. Chỉ khi không về `ready` mới báo Editor mất |

## Luồng dữ liệu

```
--project   ──► unity status: ready | bận → chờ tới --ready-wait | không Editor ──► (ready) AssetDatabase.Refresh — Editor sinh lại csproj
--csproj a,b ──► chọn builder (MSBUILD › dotnet › MSBuild Rider/VS) ──► build -v q -clp:ErrorsOnly ──► "build a: ok" | dòng lỗi CS/MSB | mã thoát ≠ 0
(ready?)    ──► script recompile ──► poll recompile_status tới completed|up_to_date ──► errors[]          (không ready → FAIL unity)
--tests X,Y ──► affected_tests.py: file đổi từ tested-at → lớp test bị chạm theo assembly (0 lớp → bỏ bước test)
                ──► mỗi lớp (≤ 2) một test run --filter_type testName | quá 2 lớp, --all-tests → --filter_type assembly
                ──► (success false? Editor ready → test status | mất → FAIL unity) ──► (in_progress? poll tới hết --timeout)
                ──► gộp Results ──► so tên với Library/HorcruxVerify/X.fail trong phạm vi lớp vừa chạy, ghi lượt này
                ──► không fail mới → ghi tested-at ──► "verify: OK (n s)" | "verify: FAIL build recompile tests"
```

## Chọn test — `affected_tests.py`

```
tested-at (HEAD project + submodule ở lượt test trước) ──► git diff <mốc> + file chưa track, mọi repo
  .asmdef .asmref csc.rsp manifest ProjectSettings ──► cả assembly
  .cs        ──► tên type ngoài cùng + extension method (bản hiện tại và bản ở mốc) + lớp cha, interface của project
  asset YAML ──► .cs của object có dòng đổi (m_Script guid) ; test nhắc tên file asset
  ──► file nhắc tên đó, thấy được assembly và namespace ──► lặp tới khi hết ──► lớp test trong tập + lớp mang fail mới "+"
```

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| Dò theo tên trên code hiện tại, không parse C# | 0,8 s cho ~1700 file, không cần graph hay Roslyn; thừa test thì chỉ tốn giây, thiếu test mới là hỏng — thứ dò tên bắt thừa |
| Chỉ lan theo **type ngoài cùng** của file | type lồng gọi qua tên type ngoài; lan theo type lồng thì lớp `Init` lồng trong mọi Initializer kéo cả project (đo: một file lá chạm 212 file thay vì 32) |
| Extension method chỉ lan sang file nhắc **type nhận** (`this R`) khi R là type của project | tên extension hay chung chung; giá trị kiểu R có được là nhờ một type đã trong tập |
| Lớp cha, interface chỉ lan từ **file đổi**, không lan tiếp | nơi gọi qua interface (DI, kéo thả) không nhắc lớp con; lan ở mọi tầng thì base chung của project kéo cả project (327 file thay vì 32) |
| Cùng assembly hoặc reference trực tiếp, rồi namespace (cùng, lồng, `using`, ghi đầy đủ) | tên trùng ở assembly không tham chiếu được (Horcrux không thấy dự án) hay namespace không mở được thì không phải phụ thuộc |
| Lớp mang fail mới (`+` trong `.fail`) luôn chạy lại | code trả về đúng bản mốc thì diff rỗng mà nhãn fail mới treo mãi |
| Quá 2 lớp trong một assembly thì chạy cả assembly | mỗi lượt `test run` tốn ~6 s dựng danh sách test; assembly 496 test chạy cả 23 s = ba lượt một lớp |
| Assembly test EditMode bị chạm mà `--tests` thiếu thì tự thêm | danh sách tay hay thiếu assembly tool, test thiết kế; bỏ sót là bỏ test đang đỏ |
| Mốc `tested-at` chỉ dời khi lượt test không có fail mới | fail mới chưa sửa thì lượt sau vẫn xét lại đủ file từ mốc cũ |
| Chưa có mốc thì chạy cả assembly | lượt đầu trên máy, hoặc commit mốc đã mất sau rebase |

**Chỗ dò tên không thấy** — chạy `--all-tests` khi đụng: gọi qua reflection, chuỗi tên type, `SerializeReference` theo chuỗi ·
test duyệt mọi asset của một loại mà không nhắc tên file · đổi hành vi của type được dùng qua interface ở tầng thứ hai trở đi.
Trước merge, release thì luôn `--all-tests`.

## Lệnh

```bash
bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj <A.csproj,B.csproj> --tests <TestAsm1,TestAsm2> [--all-tests] [--project <path>] [--no-unity] [--no-tests] [--timeout 600] [--ready-wait 600]
```

| Cờ | Nghĩa |
|---|---|
| `--csproj` | danh sách csproj cách nhau dấu phẩy, đường dẫn tính từ `--project`; bỏ trống thì bỏ bước build |
| `--tests` | assembly test EditMode (tên trong `.asmdef`); chạy lớp bị chạm, cộng assembly test bị chạm mà danh sách thiếu; bỏ trống thì bỏ bước test |
| `--all-tests` | chạy cả các assembly trong `--tests`, không chọn: trước merge, release, hoặc khi đụng chỗ dò tên không thấy |
| `--project` | gốc project Unity; mặc định thư mục hiện tại |
| `--no-unity` | chỉ build csproj, cho máy không mở Editor |
| `--no-tests` | dừng sau recompile |
| `--timeout` | giây chờ mỗi assembly test, tính cả lượt hỏi `test status` sau đó; mặc định 600 |
| `--ready-wait` | giây chờ Editor đang bận về `ready` trước bước Unity; mặc định 600 |
| `--print-builder` | in builder sẽ dùng (`dotnet` hoặc đường dẫn Windows của `MSBuild.exe`) rồi thoát, không kiểm gì; không có builder thì mã 1. `plan_check.py` gọi cờ này |
| `UNITY_CLI` | biến môi trường ép đường dẫn `unity.exe`; mặc định `unity` trên PATH |
| `MSBUILD` | biến môi trường ép đường dẫn `MSBuild.exe`; không đặt thì dùng `dotnet` trên PATH, thiếu nữa thì MSBuild mới nhất của Rider hay Visual Studio trong `Program Files` ổ c–h |

Thời gian (đo 2026-11): build 3 s mỗi csproj khi đã build trước đó, 25–30 s lần đầu · Refresh 3 s · recompile 1–20 s ·
chọn test 1–3 s · mỗi lượt `test run` ~6 s cộng thời gian test · task không chạm test nào: cả lệnh 12–19 s.

## Đọc kết quả

| Thấy gì | Nghĩa là | Soi tiếp ở đâu |
|---|---|---|
| `build X: không có file` | Unity chưa sinh csproj cho assembly đó | mở Editor, Edit > Preferences > External Tools > Regenerate project files |
| `build: không có dotnet …` | máy không có builder nào tool tìm được | đặt `MSBUILD=<đường dẫn MSBuild.exe>` rồi chạy lại |
| `build X: LỖI` kèm `CS2001` | csproj trỏ file đã dời | lỗi tạm sau `git mv`; chờ Editor sinh lại rồi chạy lại, bước recompile quyết |
| `build X: LỖI` kèm `CS2001` cho cả một thư mục không còn trên đĩa, chạy lại vẫn vậy | csproj cũ: asmdef đã xoá hay đổi tên, Unity không xoá csproj của nó ở gốc | không có `.asmdef` nào tên X thì bỏ X khỏi `--csproj`; lấy tên từ `.asmdef`, không từ danh sách csproj ở gốc |
| `build X: LỖI (mã thoát n, …)` | builder hỏng mà không in lỗi biên dịch | đọc 20 dòng output in kèm |
| `build X: ok` nhưng `recompile: LỖI` | assembly không có trong `--csproj` (asmdef mới, test) hoặc define khác | đọc `errors` in ra; csproj thiếu assembly mới thì thêm tên vào `--csproj` |
| `recompile: không xong sau 180 s` | Editor treo ở dialog, hoặc đang import | nhìn Editor; Unity bận import asset nặng thì chạy lại |
| `tests X: không đọc được kết quả` | assembly không có test, lệnh timeout, hoặc Editor đang Play | thân JSON in kèm 600 ký tự đầu |
| `tests X: Editor ngắt giữa lượt test (…) và không về ready` | Editor đóng, crash, hoặc kẹt quá `--ready-wait` lúc test chạy | mở lại Editor rồi chạy lại; `verify: FAIL unity`, không phải lỗi test |
| `tests X: lượt test bị cắt giữa chừng, chạy lại` | domain reload (file đổi giữa lượt, session khác sửa code) cắt lượt test; tool đã gửi lại một lần | kết quả in sau dòng này là của lượt gửi lại |
| `tests X: n/m pass, k fail` | tên test fail và dòng đầu message in ngay dưới; `(mới)` là fail lượt trước chưa có, `(chưa có mốc)` là test có sẵn lần đầu chạy đã fail, `hết fail` là fail lượt trước đã qua | mở test `(mới)` trước; `(chưa có mốc)` thì tìm commit đặt dữ liệu rồi báo developer; dòng `so với lượt <giờ> @<commit>` cho biết mốc — mốc không ở bản trước task thì chạy một lần trên bản đó |
| `… — n lớp bị chạm (s)` sau dòng pass | chỉ chạy các lớp đó; so fail chỉ trong các lớp đó | dòng `tests: n file đổi từ mốc, m file code bị chạm` ngay trên cho biết phạm vi |
| `tests: không test nào bị chạm, bỏ bước test` | file đổi không chạm lớp test nào (chữ, prefab không có script được test, tài liệu) | nghi sót thì chạy lại với `--all-tests` |
| `tests: tự thêm assembly test bị chạm: X` | X có lớp bị chạm mà `--tests` thiếu | thêm X vào lệnh của dự án |
| `tests: chưa có mốc lượt test trước → chạy cả assembly` | lượt đầu trên máy, hoặc `Library/HorcruxVerify/tested-at` đã xoá, hoặc commit mốc mất sau rebase | bình thường; lượt sau chỉ chạy lớp bị chạm |
| `unity: Editor đang <state>, chờ ready` rồi `vẫn <state> sau n s` | Editor kẹt ở hộp thoại, hoặc import lâu hơn `--ready-wait` | nhìn Editor; import nặng thì tăng `--ready-wait` |
| `unity: Editor chưa mở project` | không Editor nào mở project này | mở Editor, hoặc `--no-unity` (không Refresh được nên csproj có thể cũ) |

## Đã sai một lần

- Máy không có `dotnet`: lệnh build chỉ in "command not found", bước lọc dòng `error CS` không thấy gì nên báo `build: ok` trên code
  đang lỗi. Bài học: chọn builder trước, không có thì FAIL; mã thoát khác 0 cũng là LỖI, không chỉ dòng `error CS`.
- Glob `/?/Program Files` trong Git Bash không khớp ổ đĩa nào (`/` không liệt kê ổ): tìm MSBuild phải duyệt từng chữ ổ.
- Vá csproj bằng tay sau `git mv` rồi kết luận lỗi đường dẫn là lỗi thật: Editor đang mở sinh lại csproj, bản vá bị ghi đè hoặc thừa.
- Chạy batchmode `-runTests` khi Editor đang mở project: khoá `Temp/UnityLockfile`, lệnh treo tới timeout không một dòng lỗi.
- `test run` trả "socket hang up" bị báo `không đọc được kết quả` như test hỏng; lần chạy lại treo không một dòng vì lệnh gọi pipe qua `tail`. Bài học: lỗi kết nối hỏi lại trạng thái Editor; người gọi đọc output thẳng.
- Chạy Unity với `-nographics` để test: test của package PrimeTween fail vì shader, không liên quan code dự án.
- Chạy cả assembly mỗi task dù task chỉ đổi chữ: lượt test là phần chậm nhất và hay bị domain reload cắt phải chạy lại.
  Bài học: chọn test theo file đổi, task không chạm test nào thì bỏ bước test.
- Trả code về đúng bản mốc sau một fail mới: diff rỗng, lớp đó không được chạy lại, nhãn `(mới)` treo mà verify báo OK.
  Bài học: lớp mang fail mới luôn nằm trong lựa chọn.

## Nghiệm thu

Chạy trên project có lỗi cố ý ở một file: `build` in đúng một dòng lỗi, `recompile: LỖI` in cùng lỗi, mã thoát 1. Sửa lỗi, chạy lại:
mọi bước ok, `verify: OK`, mã thoát 0. Chạy `--no-unity` khi Editor đóng: chỉ có dòng build, mã thoát theo build. Máy không builder (hoặc `MSBUILD` trỏ sai): `verify: FAIL build`,
mã thoát 1. `MSBUILD` trỏ lệnh thoát 1 không in gì (`/usr/bin/false`): `build X: LỖI (mã thoát 1, …)`.
`--print-builder` in đường dẫn MSBuild, mã 0; `MSBUILD=/nope`: `build: MSBuild không có ở /nope`, mã 1.

Chọn test (đo 2026-11, project ~1700 file .cs, assembly test 496 test): đổi một chuỗi trong MonoBehaviour không có test →
`không test nào bị chạm`, cả lệnh 12 s · đổi dấu trong một hàm thuần có lớp test → chỉ lớp đó chạy, fail `(mới)` (9 s) ·
trả code về → lớp đó chạy lại, `hết fail` · đổi asset config mà một test so với spec → chọn đúng test đỏ, cộng assembly
test thiết kế `--tests` thiếu · `--all-tests` ra cùng kết quả với cách cũ (492/496, 23 s).

---

## `plan_check.py` — kiểm code của Plan trước khi giao

Code trong Plan là code developer gõ nguyên, nên phải qua compiler; code runtime Horcrux lại là phần developer gõ, agent không
ghi tạm vào `Assets/`. Tool kiểm hai điều trên file mẫu agent viết ở scratchpad: **mỗi khối ` ```csharp ` của Plan nằm nguyên
văn trong một file mẫu**, và **file mẫu build được trong assembly sẽ gọi nó**. Mã thoát 1 khi một điều hỏng.

```bash
python -X utf8 -I "<horcrux>/Editor/CompileCheck/Tools~/plan_check.py" --plan <Plan.md> --csproj <A.csproj> \
  [--sample <mẫu.cs> ...] [--replace <file trong csproj>=<mẫu.cs> ...] [--match <file thật> ...] [--project <gốc>]
```

| Cờ | Nghĩa |
|---|---|
| `--plan` | Plan cần kiểm; mọi khối ` ```csharp ` / ` ```cs ` phải nằm nguyên văn (bỏ qua kiểu xuống dòng) trong một file mẫu hoặc file `--match` |
| `--sample` | file mẫu mới: so khối và thêm vào csproj khi build |
| `--replace` | thay một `<Compile Include>` đang có (đường dẫn như trong csproj, `/` hay `\`) bằng file mẫu — bản tạm của dự án đổi sang bản Plan cùng tên type |
| `--match` | chỉ so khối, không build — file thật chứa "đoạn cũ" của chỗ Plan viết cũ → mới |
| `--csproj` | csproj ở gốc project của assembly gọi code Plan; bỏ trống thì chỉ so khối |

Plan nhiều giai đoạn: mỗi giai đoạn một bộ file mẫu, một lần chạy.

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| Bản chép csproj đặt **cạnh bản gốc** (`PlanCheck.<tên>.csproj`, xoá sau khi build), không ở scratchpad | đường dẫn tương đối trong csproj giữ đúng; viết lại đường dẫn bằng regex từng hỏng vì `\U`, `\b` của đường dẫn Windows. Csproj ở gốc bị gitignore |
| Output và obj về `Temp/PlanCheck/` | không ghi đè dll của assembly thật trong `Temp/Bin/Debug/` mà Editor và `verify.sh` dùng |
| `-p:BuildProjectReferences=false` | chỉ build assembly đang kiểm; dll project tham chiếu lấy từ lần `verify.sh` trước — thiếu thì `CS0006`, tool nhắc chạy `verify.sh` |
| Builder hỏi `verify.sh --print-builder` | luật chọn builder chỉ một bản |
| So khối là **chuỗi con** của file mẫu, không so cả file | một khối có thể là cả file mới, hay một đoạn chèn vào file đã có |
| Code Horcrux mẫu build trong assembly consumer (`Assembly-CSharp`…) hoặc trong bản chép `com.horcrux.runtime.csproj` | consumer chứng minh call site gọi được; `com.horcrux.runtime` chứng minh file Horcrux không lén dùng type dự án |

| Thấy gì | Nghĩa là |
|---|---|
| `khối dòng N: khớp <thư mục>/<file>` | khối ở dòng N của Plan nằm nguyên văn trong file đó |
| `khối dòng N: LỆCH — <file> khớp k dòng đầu, lệch ở dòng m` + hai dòng `Plan:` / `mẫu :` | Plan và mẫu trôi nhau; chép nguyên khối từ mẫu sang Plan |
| `LỆCH — dòng đầu không có trong file mẫu nào` | thiếu `--sample`/`--match`, hoặc dòng đầu của khối đã đổi |
| `build …: --replace: csproj không có <Compile Include="…" />` | đường dẫn bên trái `--replace` không khớp dòng nào trong csproj |
| `build … + file mẫu: LỖI` + dòng `error CS` | file mẫu không compile trong assembly đó |

**Nghiệm thu** (chạy trên một Plan có một khối file mới, consumer `Assembly-CSharp`): khớp nguyên → `plan_check: OK` · sửa một
ký tự trong khối của Plan → `LỆCH` đúng dòng, mã 1 · mẫu gọi API không có → `error CS0117`, mã 1 · `--replace` sai đường dẫn
→ báo không có `<Compile Include>`, mã 1 · mẫu khai type trùng bản tạm thêm bằng `--sample` → `CS0101`; cùng mẫu qua
`--replace` → build ok — chứng minh bản tạm đã rút khỏi build · Plan không có khối C# → `FAIL khối` · sau mọi lượt không
còn `PlanCheck.*.csproj` ở gốc.
