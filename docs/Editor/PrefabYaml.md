# PrefabYaml — ghép và kiểm prefab ở mức YAML

Code: `Editor/PrefabYaml/Tools~/prefab_yaml.py`

Chép một cây object (kèm component, con, PrefabInstance lồng) từ prefab này sang prefab khác giữ nguyên fileID, xoá một cây,
sửa field theo dòng, chép cài đặt component từ bản khác theo tên object, rồi kiểm cây Transform còn khớp — không mở Unity.
Dùng khi chuyển phần của một prefab từ nhánh khác sang (VFX, khối UI), khi giải conflict prefab theo `merge-audit` (lấy bản
một phía rồi ghép khối object phía kia thêm), hay khi hoạ sĩ giao lại prefab đổi hết fileID mà `UnityPackageAudit` báo `GÃY`.

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | agent, khi phải ghép prefab giữa hai bản mà kéo thả trong Editor không làm được (bản kia ở nhánh khác, hay phải lặp lại tất định) |
| Đầu vào | file `.prefab` hoặc `.unity` text (Force Text serialization); tên GameObject hoặc tên PrefabInstance lồng (override `m_Name`). Với scene, `graft` cần cha là một object có sẵn, không gắn vào gốc scene |
| Ranh giới | tool chỉ làm việc cấu trúc: chép, xoá, đổi dòng, kiểm. Chép **giữ fileID và GUID** — asset tham chiếu phải có ở dự án đích. Field trỏ object đã xoá **không tự gỡ**: `check` báo, script quyết sửa |
| Chỗ đặt | `Editor/PrefabYaml/Tools~/`, thư mục đuôi `~` Unity không import. Không có C#. Chỉ cần Python 3 |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Đổi fileID khi trùng | fileID trùng hiếm giữa hai bản cùng gốc; đổi số thì phải dò mọi tham chiếu, kể cả từ prefab khác trỏ vào. Trùng là `AssertionError`, ghép tay |
| Nested PrefabInstance nằm trong một PrefabInstance khác | chưa có ca thật; `subtree` ném thay vì chép thiếu |
| Đọc override của nested PrefabInstance | YAML chỉ ghi phần override, không ghi cây con của prefab nguồn; kiểm thứ nằm trong nested instance phải qua Editor |
| CLI cho sửa field | mỗi lần đổi field là một tổ hợp khác; script ngắn import module rõ hơn chuỗi cờ dòng lệnh |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| Doc chép chèn ngay sau Transform cha, không cuối file | Unity không đòi thứ tự, nhưng diff đọc được: khối mới nằm cạnh chỗ gắn |
| Ghi lại bằng LF | Unity ghi YAML bằng LF trên mọi nền tảng; giữ CRLF của working copy Windows làm diff toàn file |
| `check` báo `{fileID: X}` treo, không báo `guid` treo | fileID nội bộ kiểm được trong một file; GUID cần cả dự án — mở prefab trong Editor là phép kiểm đó |
| `copy` ghép theo tên GameObject, không theo fileID | bản hoạ sĩ xuất lại có thể đổi hết fileID; tên là thứ duy nhất còn khớp. Tên trùng trong một prefab thì ném, vì ghép theo tên không chắc |
| `copy` thay cả body component, không chỉ dòng khác | hoạ sĩ đổi nhiều module hạt một lúc (đường cong, gradient); diff sau đó chỉ còn đúng các dòng đã đổi vì phần giống nhau giữ nguyên |

## Lệnh

```bash
python -X utf8 -I "<horcrux>/Editor/PrefabYaml/Tools~/prefab_yaml.py" check  <prefab>...
python -X utf8 -I "<horcrux>/Editor/PrefabYaml/Tools~/prefab_yaml.py" graft  <dst> <src> <src_object> <dst_parent> [--index N]
python -X utf8 -I "<horcrux>/Editor/PrefabYaml/Tools~/prefab_yaml.py" remove <prefab> <object>
python -X utf8 -I "<horcrux>/Editor/PrefabYaml/Tools~/prefab_yaml.py" copy   <dst> <src> --class <!u!> [--class …]
python -X utf8 -I "<horcrux>/Editor/PrefabYaml/Tools~/prefab_yaml.py" selftest
```

`copy` chép body của component lớp `--class` (vd `198` ParticleSystem) từ `src` sang `dst`, ghép theo tên GameObject và thứ
tự trong lớp; fileID nội bộ trong body (`m_GameObject`, component khác cùng cây) đổi sang bản `dst`, ref không ghép được thì
ném. fileID của `dst` giữ nguyên nên override và ô tham chiếu của prefab lồng nó không đổi. In object chỉ có ở một bên:
có thì hoạ sĩ đã đổi cấu trúc, phần đó phải ghép tay. `src` lấy từ gói: giải nén `<guid>/asset` của gói ra thư mục riêng.

`graft`, `remove` và `copy` ghi đè file tại chỗ rồi chạy `check`. `--index` là vị trí trong `m_Children` của cha (mặc định cuối).
Mã thoát: 0 sạch · 1 `check` báo lỗi · 2 dùng sai, tên không tìm thấy hay trùng, fileID trùng.

`check` kiểm ba thứ: fileID không trùng · `m_Children` của cha ↔ `m_Father` của con (với PrefabInstance lồng là
`m_TransformParent`) · mọi `{fileID: X}` không kèm `guid` trỏ vào doc có trong file. Sau `remove`, dòng
`… trỏ fileID X không có` là field serialize còn trỏ vào cây vừa xoá.

## Ghép kèm sửa field — script ngắn

```python
import sys; sys.dont_write_bytecode = True   # import module sinh __pycache__ trong Tools~, thành file lạ trong git status
sys.path.insert(0, r"<horcrux>/Editor/PrefabYaml/Tools~")
from prefab_yaml import Prefab, check

dst, src = Prefab.load(dst_path), Prefab.load(src_path)
dst.graft(src, src.transform_named('burst'), dst.transform_named('frame'))
view = next(d for d in dst.docs if d.cls == '114' and 'flightTrail:' in d.body)   # MonoBehaviour theo field có trong body
dst.set_line(view, '  flightTrail', '{fileID: 123}')
dst.delete_line(view, '  oldField')
dst.insert_after(view, '  flightTrail', ['  fadeTime: 0.12'])
dst.save(dst_path)
sys.exit(0 if check(Prefab.load(dst_path), dst_path) else 1)
```

| API | Làm |
|---|---|
| `Prefab.load(path)` · `save(path)` · `text()` | đọc, ghi (LF), dựng lại text |
| `go_named(name)` · `instance_named(name)` · `transform_named(name)` | tra doc theo tên; trùng hay không có thì `AssertionError` |
| `transform_of(go)` · `component_of(go, cls)` · `stripped_of(instance)` | Transform, component lớp `cls` (số `!u!`), doc stripped của instance lồng |
| `children(tr)` · `father(tr)` · `subtree(tr)` | đọc cây; `subtree` gồm GameObject, component, con, instance lồng |
| `graft(src, src_tr, dst_parent_tr, index=None)` | chép cây, giữ fileID, gắn vào cha |
| `remove_subtree(tr)` | xoá cây, gỡ khỏi `m_Children` của cha |
| `replace_doc(src, fid)` | thay body doc cùng fileID bằng bản của `src` |
| `set_line(doc, key, value)` · `delete_line(doc, key)` · `insert_after(doc, key, lines)` | sửa dòng `key: …` đầu tiên; `key` kèm thụt lề để chọn đúng tầng |

Lớp `!u!` hay gặp: `1` GameObject · `4` Transform · `224` RectTransform · `114` MonoBehaviour · `198` ParticleSystem ·
`199` ParticleSystemRenderer · `212` SpriteRenderer · `331` SpriteMask · `1001` PrefabInstance.

## Lưu ý

- Prefab đang mở trong Prefab Mode sẽ được Unity lưu đè lên bản YAML vừa ghép: đóng Prefab Mode trước khi chạy.
- `check` sạch chưa đủ: mở prefab trong Editor xem — GUID treo, script mất, override của nested instance chỉ lộ ở đó.
- Field đổi tên ghi bằng key YAML mới cùng lần, không `FormerlySerializedAs`.

## Nghiệm thu

`selftest` dựng hai prefab giả cho `copy`: ghép theo tên, body lấy từ `src` kèm ref có guid, fileID nội bộ đổi sang bản đích,
object chỉ có ở `src` được báo, ref nội bộ không ghép được thì ném. Đối chứng: bỏ bước đổi fileID thì hai phép kiểm báo `SAI`.
