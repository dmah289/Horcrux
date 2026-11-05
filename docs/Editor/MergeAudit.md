Code: `Editor/MergeAudit/Tools~/merge_audit.py`

# MergeAudit — merge nhánh Unity mà không mất gì im lặng

Bốn phép kiểm cho một lần merge. `plan`, chạy trước merge, chỉ ra chỗ git sẽ ghép sai: commit dời thư mục, cặp rename khác
GUID, asset không đi theo `.meta`, file mới lạc vào thư mục bên kia đã dời. `lines`, chạy sau merge, chứng minh mỗi dòng
một cha thêm vẫn còn trong merge. `assets` chứng minh merge không sinh GUID trùng, `.meta` lệch, tham chiếu gãy, thư mục
sống lại hay entry Addressables mất. `replay` dựng lại merge tự động của một merge commit có sẵn và chỉ ra phần người merge
sửa tay. Git chỉ báo conflict text; ghép nhầm file, đổ thay đổi sang file khác, rơi entry thì im lặng. Bốn phép này đo
đúng những thứ git không báo.

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | agent: trước khi merge nhánh, sau mỗi lần giải conflict, và khi nhận merge commit người khác đã đẩy lên; skill `merge-audit` trỏ về đây |
| Đầu vào | repo git, hai rev (`plan`), một merge commit hai cha (`lines`, `assets`), merge commit hoặc khoảng rev (`replay`). Tất cả đọc từ object git; không đọc working tree, trừ `Library/PackageCache` để nhận GUID của package |
| Ranh giới | tool **chỉ kiểm**, không merge, không sửa. Kết quả là danh sách để agent phân loại: *chủ ý* (một cha sửa hay xoá có lý do) hay *rơi* (merge làm mất) |
| Chỗ đặt | `Editor/MergeAudit/Tools~/merge_audit.py`; cần `git` 2.40+ trên PATH (`merge-tree --write-tree -X`); thư mục đuôi `~` Unity không import. Không có C# |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Tự merge theo bậc | mỗi bậc có conflict riêng phải giải tay, và file mới lạc thư mục cần `git mv` theo quyết định; quy trình nằm ở skill `merge-audit`, tool cấp số liệu cho từng bước |
| Phán dòng mất là đúng hay sai | dòng một cha xoá có chủ ý cũng hiện như dòng rơi; chỉ đọc hai phía mới biết |
| Kiểm `.meta` trong `lines` | `.meta` đổi chủ yếu do Unity tự serialize lại; GUID và cặp ghép đã do `plan`, `assets` lo |
| Kiểm nội dung prefab, scene (fileID trùng, cây Transform) | YAML không thấy override của prefab lồng; kiểm bằng mở trong Editor hoặc quét object đã dựng |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| `plan` coi cặp rename **cùng GUID** là đúng dù độ giống thấp | Unity giữ GUID khi dời, nên GUID trùng là chứng cứ cặp đúng; độ giống chỉ là dấu hiệu khi không có GUID |
| `plan` báo asset mà `.meta` rename 100% nhưng chính asset bị git coi là xoá + thêm | asset sửa nhiều khi dời (prefab dựng lại) rớt ngưỡng 50%; thay đổi của phía kia trên file cũ thành conflict modify/delete hoặc đổ sang file giống nhất |
| `lines` bỏ dòng đã có trong bản base của cùng file | file dời mà không ghép rename thì cả file hiện là "thêm"; dòng base bị phía kia sửa không phải dòng mất |
| `lines` tìm file đích trong merge theo GUID của `.meta` | file một cha sửa có thể đã bị cha kia dời; tra theo đường dẫn thì báo "không còn" sai |
| `assets` chỉ đếm lỗi **mới do merge** | dự án lớn luôn có tham chiếu gãy có sẵn; lỗi có sẵn ở một cha in số đếm, `--all` mới liệt kê |
| Tham chiếu gãy tính là có sẵn khi GUID đó đã gãy ở một cha, kể cả ở file khác | file dời giữa hai cha mang theo tham chiếu gãy cũ |
| `replay` mặc định dựng lại như `git merge` thường | merge commit người khác đẩy lên được tạo bằng ngưỡng rename mặc định; `--find-renames 100` khi merge đó làm theo bậc |

## Luồng dữ liệu

```
plan ours theirs
  merge-base ── log -M100% --diff-filter=R ── commit ≥ --min-renames rename → bậc merge "C^ → C → tip"
  diff -M base side ── cặp R ── GUID(base:old.meta) ≠ GUID(side:new.meta) ∧ phía kia sửa old → GHÉP SAI GUID
                               .meta R mà asset không ghép cùng cặp ∧ phía kia sửa asset   → ASSET KHÔNG THEO META
  diff -M100% base side ── thư mục cũ → mới theo đa số, chỉ thư mục đã biến mất
  diff base other (A) ── file nằm dưới thư mục đã dời → đường dẫn gợi ý cho git mv
  merge-tree --write-tree (mặc định, -Xfind-renames=100%) ── số file conflict mỗi cách

lines merge
  cha k: diff --no-renames base cha (A, M, --ext) ── GUID(cha:path.meta) → đường dẫn trong merge
  dòng (strip, bỏ ngoặc trơn) của cha  −  dòng của merge  −  dòng base cùng GUID  → dòng mất

assets merge
  merge, cha 1, cha 2, base: ls-tree, guid map, grep "guid: …" trên YAML, m_GUID trong AssetGroups
  lỗi(merge) − lỗi(cha 1) − lỗi(cha 2) → mới do merge
```

## Lệnh

```bash
python -X utf8 -I "<horcrux>/Editor/MergeAudit/Tools~/merge_audit.py" plan   <ours> <theirs> [--min-renames 20]
python -X utf8 -I "<horcrux>/Editor/MergeAudit/Tools~/merge_audit.py" lines  [<merge>] [--ext .cs,.md,...] [--samples 3]
python -X utf8 -I "<horcrux>/Editor/MergeAudit/Tools~/merge_audit.py" assets [<merge>] [--all] [--groups <dir>]
python -X utf8 -I "<horcrux>/Editor/MergeAudit/Tools~/merge_audit.py" replay <merge>... | --range <a>..<b> [--find-renames 100]
```

| Cờ | Nghĩa |
|---|---|
| `--repo` | gốc repo, mặc định thư mục hiện tại |
| `--top` | số mục tối đa mỗi phần, mặc định 40 |
| `--min-renames` | `plan`: số rename nguyên vẹn để coi một commit là commit dời thư mục |
| `--ext` | `lines`: đuôi file text được so dòng |
| `--samples` | `lines`: số dòng mẫu in dưới mỗi file |
| `--all` | `assets`: in cả lỗi có sẵn ở một cha |
| `--groups` | `assets`: thư mục group Addressables, mặc định `Assets/AddressableAssetsData/AssetGroups` |
| `--range` | `replay`: mọi merge commit trong `a..b` |
| `--find-renames` | `replay`: ngưỡng rename khi dựng lại |

Mã thoát: 0 sạch · 1 có phát hiện · 2 dùng sai hoặc git lỗi. Mỗi lệnh chạy dưới 5 giây trên repo vài nghìn asset.
Merge commit tạo bằng `commit-tree` hay merge thường đều đọc được; `lines` và `assets` mặc định đọc `HEAD`.

## Đọc kết quả

| Thấy gì | Nghĩa là | Làm gì |
|---|---|---|
| `plan` có commit dời thư mục | merge một lần sẽ dò rename theo độ giống trên hàng trăm file | merge theo bậc in ra, mọi bậc `-X find-renames=100%` |
| `plan` `[GHÉP SAI GUID] a.meta -> b.meta` | `.meta` gần giống nhau (texture cùng importer) bị ghép chéo | merge theo bậc; sau merge so GUID file `b.meta` với cha giữ nó |
| `plan` `[ASSET KHÔNG THEO META]` | thay đổi của phía kia trên asset cũ không tự sang đường dẫn mới | sau merge, ghép tay vào file mới; `lines` xác nhận |
| `plan` file mới lạc thư mục | rename nguyên vẹn không kéo theo directory rename | `git mv <cũ> <gợi ý>` trước khi commit bậc |
| `lines` `KHÔNG CÒN trong merge` | file một cha thêm hoặc sửa biến mất | chủ ý (tính năng bị bỏ) thì ghi vào báo cáo; không thì lấy lại từ cha |
| `lines` `n dòng mất` | dòng cha thêm không còn | đọc mẫu: phía kia sửa cùng chỗ → chủ ý; không ai sửa → rơi |
| `assets` GUID trùng mới | hai file cùng GUID: Unity sinh GUID mới cho một bên lúc import, tham chiếu trỏ lệch | giữ bản có tham chiếu, xoá bản kia |
| `assets` tham chiếu gãy mới, ghi `từng là X ở cha k` | một cha xoá asset mà file của cha kia còn dùng | gắn asset thay thế, hoặc khôi phục asset |
| `assets` thư mục sống lại | file thêm vào thư mục cũ sau khi bên kia dời | `git mv` sang thư mục mới |
| `assets` entry Addressables mất | entry một cha thêm không còn | thêm lại entry |
| `assets` `gỡ chủ ý theo merge ba chiều` | một cha gỡ entry, cha kia giữ như base | soát ý định: thường là chủ ý (đổi sang asset khác cùng address) |
| `replay` `SỬA NGOÀI CONFLICT` | merge commit có thay đổi ngoài conflict | đọc diff file đó: ghép tay có chủ ý, hay sửa lén trong merge |

## Đã sai một lần

- Merge một lần qua commit dời 400+ file: thay đổi của một prefab đổ sang prefab khác tên gần giống, bốn `.meta` texture
  ghép chéo GUID, git không báo conflict ở cả năm file.
- Unity đang mở project lúc merge thì import `.meta` còn dấu conflict; merge trong worktree riêng, checkout khi cây đã sạch.
- Chỉ đếm dòng của nhánh đích mà bỏ phía kia: dòng của nhánh tính năng rơi không ai thấy.
- Script so dòng không trừ dòng base báo hàng chục file "mất" ở phía đã dời thư mục — toàn dòng base bị phía kia sửa.
- `git cat-file --batch` trả `<rev>:<path> missing`; đường dẫn có dấu cách cũng tách ba mảnh như header thật.

## Nghiệm thu

Repo thật có commit dời 436 file ở một nhánh, nhánh kia sửa prefab và texture `.meta` trong thư mục bị dời. Kỳ vọng và đã
đạt: `plan` in đúng commit dời và bậc, 4 cặp `GHÉP SAI GUID` và 1 `ASSET KHÔNG THEO META` trùng với 5 file merge một lần đã
làm hỏng, 160 file mới lạc thư mục; `lines` trên merge commit liệt kê đúng các file bị bỏ chủ ý; `assets` báo đúng 6 tham
chiếu gãy mới của một prefab tool và tách 3 entry Addressables gỡ chủ ý; `replay` trên hai merge thường ra 0 file khác, trên
merge theo bậc ra đúng phần ghép tay. Ca sai: rev không tồn tại, commit một cha, `replay` không đối số → mã 2 hoặc bỏ qua
có ghi chú.
