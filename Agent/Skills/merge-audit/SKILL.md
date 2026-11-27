---
name: merge-audit
description: Dùng khi merge hai nhánh của project Unity — nhất là khi một nhánh có commit dời thư mục — hoặc khi nhận merge commit người khác đã đẩy lên, kể cả `git pull` sinh merge commit. Merge theo bậc trong worktree riêng, kiểm trước và sau bằng công cụ dựng sẵn trong Horcrux, vì git không báo khi ghép nhầm file hay làm rơi dòng.
---

# merge-audit — merge nhánh Unity, chứng minh không mất gì

Skill dưới `Agent/horcrux.md` (NT, tầng, ký hiệu định nghĩa ở đó).
`<horcrux>` là gốc Horcrux trong dự án. Lệnh và cách đọc kết quả: `docs/Editor/MergeAudit.md`.

## Luật

- **"Không conflict" không phải bằng chứng merge sạch.** Git dò rename theo độ giống; `.meta` cùng importer giống nhau ~90%,
  prefab dựng lại rớt ngưỡng — thay đổi đổ sang file khác không một dòng báo. Bằng chứng là `lines` và `assets` sau merge.
  `git pull` ra merge commit cũng là merge; con trỏ submodule lùi im lặng khi phía kia đẩy con trỏ cũ.
- **Một nhánh có commit dời thư mục thì merge theo bậc** (tới cha commit dời → commit dời → đỉnh), mọi bậc rename nguyên
  vẹn; file mới phía kia ở thư mục cũ phải dời tay — `plan` liệt kê sẵn.
- **Merge ngoài checkout Unity đang mở:** Editor import `.meta` và asset còn dấu conflict, sinh GUID mới cho file nó đọc
  không được. Nhánh đã tách hai phía thì `pull` là merge: dựng worktree trước lệnh merge đầu tiên; file developer đang sửa
  hay đã stage để nguyên.
- **File cấu hình gộp hợp hai bên, không chọn một bên:** dependency và scope trong `Packages/manifest.json` cùng lock, define
  symbol theo platform, assembly reference của asmdef — chọn một bên là gỡ thứ bên kia đang dùng.
- **Prefab, scene conflict không lấy nguyên một bên:** lấy bản đổi cấu trúc nhiều hơn rồi ghép khối object phía kia bằng
  PrefabYaml (`graft`, `check`), không sửa YAML tay; rồi mở trong Editor xem.
- **Conflict ngữ nghĩa là quyết định của developer** (NT1): hai bên đổi cùng hành vi khác hướng, một bên bỏ tính năng bên
  kia vừa sửa, hai số đo cho cùng thứ. Bày phương án *giữ gì · mất gì*, chốt một đề xuất, hỏi.
- **Mọi dòng `lines`, `assets` báo đều được phân loại:** *chủ ý* (nêu lý do) hay *rơi* (sửa ngay). Lỗi có sẵn ở một cha thì
  báo, không sửa trong merge.
- **Test golden đổi vì schema đổi thì chứng minh tương đương trước khi ghi mốc mới.**
- **Merge đã lên remote thì sửa bằng commit mới**, không amend, không force push — developer có thể đã push ngay sau khi
  agent checkout. Merge người khác đẩy lên kiểm bằng
  `replay --range`.
- **Sau merge chạy `compile-check`;** lỗi do API một bên xoá mà bên kia còn gọi thì sửa theo ý định bên xoá, ghi vào báo cáo.

## Lệnh

```bash
python -X utf8 -I "<horcrux>/Editor/MergeAudit/Tools~/merge_audit.py" plan <đích> <nhánh>
python -X utf8 -I "<horcrux>/Editor/MergeAudit/Tools~/merge_audit.py" lines  <merge>
python -X utf8 -I "<horcrux>/Editor/MergeAudit/Tools~/merge_audit.py" assets <merge>
python -X utf8 -I "<horcrux>/Editor/MergeAudit/Tools~/merge_audit.py" replay --range <mốc cũ>..<đích>
python -X utf8 -I "<horcrux>/Editor/PrefabYaml/Tools~/prefab_yaml.py" graft <đích> <nguồn> <object> <cha> · check <file>...
```

`--top` nhỏ cho lần đọc đầu, `lines --samples 2` đủ để phân loại; mở diff từng file tool chỉ ra. Mỗi lệnh dưới 5 giây.

## Tra cứu — `merge-audit/reference.md`

| Chạm | Mục |
|---|---|
| dựng worktree, merge từng bậc, gói merge commit, submodule lùi, golden test | Merge theo bậc |
