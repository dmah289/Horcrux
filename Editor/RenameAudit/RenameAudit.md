# RenameAudit — chứng minh một lần đổi tên chỉ đổi tên

Ba phép kiểm cho một lần đổi tên định danh theo bảng ánh xạ `cũ<TAB>mới`: `scan` chứng minh tên cũ **vắng mặt** trên mọi file
YAML (kể cả `propertyPath`, `m_MethodName`); `tokens` chứng minh diff `.cs` chỉ đổi định danh theo bảng và liệt kê mọi chuỗi,
comment đổi; `verify` chứng minh cây gốc áp luật máy móc ra đúng từng byte cây mới, theo cả đường dẫn, không file nào đổi
ngoài tập kỳ vọng. Compile sạch chỉ chứng minh code; ba phép này chứng minh asset, chuỗi và phạm vi.

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | agent, sau mỗi commit của một lần đổi tên và trước khi giao developer bước Editor; mục "Nghiệm thu" của plan đổi tên trỏ về đây |
| Đầu vào | `map.tsv` do plan chốt, `#` là chú thích, so nguyên từ trên biên `[A-Za-z0-9_]`, phân biệt hoa thường · `Assets` (scan) · repo git và rev của cây gốc (tokens, verify) · `paths.tsv` cùng cú pháp cho đường dẫn đổi (verify) |
| Ranh giới | tool **không đổi tên**, không lập bảng; bảng chọn theo **kiểu** của định danh, agent duyệt tay. Chuỗi và comment đổi được **liệt kê**, không phán đúng sai: address, tên GameObject mà code `Find`, key JSON là việc đọc tiếp |
| Chỗ đặt | `Editor/RenameAudit/Tools~/rename_audit.py`; cần `git` trên PATH; thư mục đuôi `~` Unity không import. Không có C# |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Danh sách field serialize lập tay cho `scan` | sót một key là giá trị trong prefab về mặc định lúc import, không một dòng lỗi; quét mọi tên cũ trên mọi YAML là phép chứng minh vắng mặt |
| Chuẩn hoá hai vế khi so chuỗi trong `tokens` | chuỗi là lớp riêng: `"Items"` thành `"Cards"` có thể là thư mục mod hay address Addressables, người đọc phải thấy từng cái |
| Thay chuỗi con (`Card` trong `CacheCardSlot`) | nguyên từ buộc bảng liệt kê đủ từng định danh; giá là bảng dài, được là không đổi nhầm `Discard`, `SDCard` |
| Quét `.meta` mặc định trong `scan` | tên sprite con trong `.png.meta` đổi hay không là quyết định riêng; cần thì thêm `--ext` |
| Raw string `"""…"""` của C# 11 | Unity dự án dùng C# 9; thêm khi có file thật |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| `verify` so với **cây gốc** `--base`, không so với commit ngay trước | luật nhiều bước (dời thư mục rồi đổi từ) áp lên cây đã qua bước 1 thì nhánh đường dẫn cũ hết khớp và luật chung đổi lan sang tên chủ đề nội dung |
| Dòng `cũ<TAB>cũ` là cách bảo vệ nguyên văn | cụm dài thắng cụm ngắn nằm trong nó, nên `Memory Card→Memory Card` chắn `Card→Tile` mà không cần cú pháp ngoại lệ; áp cho cả `paths.tsv` |
| `verify` băm file working tree theo `blob <size>\0` của git, chỉ lấy blob của file lệch sha | 11 000 blob 141 MB qua pipe mất 65 s, băm tại chỗ mất 1,5 s. File y nguyên gốc mà luật đổi được nội dung là `differ` ngay, không cần blob |
| So sau khi quy CRLF về LF, đọc ghi bytes | working copy Windows `core.autocrlf=true` nên mọi file text lệch blob ở từng dòng |
| `tokens` ghép file thêm/xoá bằng bảng áp lên đường dẫn, không chỉ `git -M` | `-M` tính tương đồng theo dòng; file đổi tên có gần hết dòng đổi nên rớt ngưỡng. Không ghép được là vi phạm: đổi tên không sinh file mới |
| `unexpected` lấy từ `git ls-files --cached --others --exclude-standard` | file untracked ngoài tập kỳ vọng cũng là thay đổi; `Library/` theo `.gitignore` không vào |
| Submodule bỏ qua | `ls-tree` ghi nó là `commit`; nghiệm thu riêng trong repo của nó |

## Luồng dữ liệu

```
map.tsv ── read_map ── word_regex: dài trước ngắn sau, biên [A-Za-z0-9_] ─────────────────────────┐
scan:   <root>/**.{unity,prefab,asset,anim,controller,spriteatlasv2,mat} [+.cs,.md] ── finditer ──┴── file:line: cũ
tokens: git diff --name-status -M <base> ── .cs M/R, A↔D ghép bằng bảng trên đường dẫn
        ── git cat-file --batch (cũ) · working tree (mới) ── tokenize ── so theo vị trí:
           ident khác → phải có trong bảng · string/char/comment/#directive khác → liệt kê · số/dấu khác → vi phạm
verify: git ls-tree -r <base> ── (sha, path) ── paths.tsv → path mới ── sha1(working) hay sha1(working quy LF) == sha?
        ── bằng: luật đổi được nội dung → differ, không → sạch · khác: lấy blob, áp luật, so sau quy LF
        git ls-files ── path ∉ tập kỳ vọng → unexpected
```

## Lệnh

```bash
python -X utf8 -I "<horcrux>/Editor/RenameAudit/Tools~/rename_audit.py" scan   <map.tsv> [--root Assets] [--ext .unity,.prefab,...] [--also-cs]
python -X utf8 -I "<horcrux>/Editor/RenameAudit/Tools~/rename_audit.py" tokens <map.tsv> --base <rev> [--repo .] [--paths <glob,...>]
python -X utf8 -I "<horcrux>/Editor/RenameAudit/Tools~/rename_audit.py" verify <map.tsv> --base <rev> [--repo .] [--paths-map <paths.tsv>] [--exclude <glob,...>]
```

| Cờ | Nghĩa |
|---|---|
| `--also-cs` | `scan` quét thêm `.cs` và `.md`: tên cũ còn trong code và tài liệu |
| `--base` | rev của cây **gốc** trước mọi bước đổi tên; `tokens` so working tree với nó, `verify` áp luật lên nó |
| `--paths` | `tokens` chỉ xét file có đường dẫn mới khớp glob |
| `--paths-map` | `cũ<TAB>mới` áp lên đường dẫn với cùng luật nguyên từ: `Item→Card` đổi `Entities/Item/Item.cs.meta`, không đổi `ItemSlot.cs` |
| `--exclude` | glob bỏ qua ở cả cây gốc lẫn working tree; `*` khớp cả `/` |

Mã thoát: 0 sạch · 1 còn hit, có vi phạm, có dòng `differ`/`missing`/`unexpected`/`collision` · 2 dùng sai hoặc git lỗi.

## Đọc kết quả

| Thấy gì | Nghĩa là | Soi tiếp ở đâu |
|---|---|---|
| `scan` ra `x.prefab:N: _targetItemLocalScale` | key YAML chưa đổi theo field | dòng đó: `propertyPath` là override của variant hay instance, `m_MethodName` là UnityEvent |
| `scan` hit trong `.asset` của Addressables | address hay tên group còn tên cũ | bảng "Chuỗi" của plan |
| `tokens` `không có trong bảng` | định danh đổi ngoài kế hoạch: lan sang biến không phải thực thể, hoặc bảng thiếu | kiểu của nó là thực thể → thêm vào bảng; không → hoàn tên |
| `tokens` `số token khác` | diff có sửa logic | `git diff <base> -- file`, tách sang commit khác |
| `tokens` `strings/comments changed` | chuỗi đổi | từng dòng: log, header → xong; address, tên GameObject, key JSON → kiểm phía đọc |
| `tokens` `file thêm mới` | `.cs` không ghép với file nào bị xoá | tên mới không suy từ bảng áp lên tên cũ: thêm dòng bảng, hoặc đó là file thật sự mới |
| `verify` `differ: … (tên cũ còn nguyên)` | file chưa đổi | `scan` ra đúng dòng |
| `verify` `differ` không ghi chú | file đổi khác luật | `git diff <base> -- file` |
| `verify` cặp `missing` và `unexpected` cùng tên file | thiếu dòng `paths.tsv` | thêm dòng, chạy lại |
| `verify` `collision` | hai đường dẫn gốc về cùng một đường dẫn mới | `paths.tsv` gộp hai file |

## Đã sai một lần

- Bảng lập từ grep theo **tên** bắt nhầm biến lặp chung (`remainderItems`, `lastItem`): phạm vi chọn theo kiểu, mở ngữ cảnh từng định danh mơ hồ trước khi chạy, nghiệm thu bằng đọc diff của file ngoài vùng thực thể.
- Compile sạch và so token `.cs` không thấy key YAML sót; chỉ quét mọi tên cũ trên mọi YAML kể cả `propertyPath` mới lộ.
- Luật đường dẫn chạy trên cây đã qua bước trước đổi nhầm 40 tên chủ đề nội dung; so với cây gốc bắt ngay.
- Chuẩn hoá hai vế khi so chuỗi che mất thư mục mod bị đổi theo; chuỗi liệt kê hết.
- Coi file working tree trùng sha gốc là sạch: file sót tên cũ y nguyên gốc nên lọt. Sha trùng chỉ nói "chưa chạm", còn phải hỏi luật có đổi được nội dung không.
- Script in tiếng Việt không `-X utf8` dừng giữa chừng trên console Windows.

## Nghiệm thu

Repo tạm: commit cây gốc có `.cs`, `.prefab` chứa `propertyPath: _targetItemLocalScale` và `m_MethodName: OnItemPicked`, một file
nhị phân; working tree đổi theo bảng, `.cs` ghi CRLF, cố ý sót một `propertyPath`, đổi `"Items"`→`"Cards"`, thêm một file untracked.
Kỳ vọng: `scan` 1 hit đúng dòng, mã 1 · `tokens` 0 vi phạm, liệt kê chuỗi và comment đổi, mã 0 · `verify` `differ` đúng file sót và
`unexpected` file lạ, mã 1 · sửa file sót và `--exclude` file lạ → cả ba mã 0 · xoá một file → `missing`. Đối chứng trên repo thật:
bảng `Tile→Tile` ra 0 dòng; `Tile→Card` ra đúng số file của `git grep -l -w -I Tile <base>`.
