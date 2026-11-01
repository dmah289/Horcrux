# SortingAudit — quét sorting layer và order của prefab, scene

Một cửa sổ, một nút Scan: mỗi dòng là một object vi phạm một luật xếp lớp, bấm là chọn object. Phép kiểm máy cho mô hình ba bậc
"layer chỉ khai ở gốc entity, con trong `SortingGroup` ở `Default` với order 0…99"; chạy khi được hỏi sau khi sửa prefab, không chạy nền.

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | developer sau khi sửa prefab hay thêm entity; agent trước khi giao một đợt sửa bố cục vẽ |
| Đầu vào | thư mục gốc chứa prefab (ô **Folder**) và scene đang mở |
| Ranh giới | chỉ báo, không sửa asset; không quét Canvas (UI xếp theo thứ tự con, không theo sorting layer) |
| Chỗ đặt | `Editor/SortingAudit/`, menu `Horcrux > Sorting Audit`, assembly `com.horcrux.editor` |

**Luật quét**

| Luật | Báo khi | Vì |
|---|---|---|
| R1 `DeadLayer` | renderer, `SortingGroup` hay dải của `SpriteMask` trỏ sorting layer không còn trong TagManager | Unity vẽ ở Default không một dòng cảnh báo |
| R2 `ChildDeclaresLayer` | renderer hay group nằm dưới một `SortingGroup` mà khai layer khác `Default` | group nuốt layer của con; layer khai ở con là ý định không thành hiện thực |
| R3 `OrderOutOfRange` | order của con trong group ngoài 0…99 | order lớn ở con là vết của lúc chưa có group, che lệch bậc |
| R4 `Root` | renderer hay group không có group tổ tiên (không phải vi phạm) | bảng layer và order thật của dự án; bật **Show roots (R4)** mới hiện |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Tự sửa asset | người nhìn quyết layer đúng là gì; tool chỉ chỉ chỗ |
| Quét theo mỗi lần asset đổi | lúc đang dựng prefab thì dữ liệu luôn chưa hợp lệ |
| Quét Canvas | mở rộng sau khi có luật cho UI |
| `LoadPrefabContents` | prefab asset đã nạp có nested instance dựng đầy đủ, R2 thấy cả override nằm trong prefab cha |

**Đã sai một lần:** so sorting layer ID bằng số đọc từ YAML, kết luận layer còn sống là đã chết — YAML ghi ID có dấu, TagManager ghi
không dấu. Tool so qua `SortingLayer.IsValid(id)` trên object đã dựng, không đọc file.

## Luồng dữ liệu

```
Folder ──► AssetDatabase.FindAssets("t:Prefab") ──► LoadAssetAtPath<GameObject> ──┐
scene đang mở ──► GetRootGameObjects ────────────────────────────────────────────┤
                                                                                  ▼
                              SortingAuditCore.Scan(root, asset, findings): Renderer · SortingGroup · SpriteMask
                                                                                  ▼
                              cửa sổ: "<n> vi phạm, <m> gốc"; mỗi dòng [Rule] asset › object: message → Selection + Ping
```

Group hỏi group tổ tiên từ `transform.parent`, vì `GetComponentInParent<SortingGroup>` trên chính object có group trả về nó. Renderer
cùng object với group hỏi từ chính nó: Unity sort renderer đó trong group.

## Trước khi chạy

1. Mở scene cần quét (tool đọc scene đang active; scene chưa lưu thì dòng gốc ghi tên root object thay tên scene).
2. `Horcrux > Sorting Audit`, ô **Folder** gõ thư mục prefab của dự án (vd `Assets/_MyGame`), bấm **Scan**. Thiếu bước này thì quét cả
   `Assets`, gồm prefab của package bên thứ ba có layer riêng.
3. Prefab của bên thứ ba có layer riêng thì loại bằng cách thu hẹp **Folder**.

## Nghiệm thu

| Ca | Kỳ vọng |
|---|---|
| Prefab có renderer trỏ layer đã xoá khỏi TagManager | một dòng R1 |
| Renderer nằm dưới object có `SortingGroup`, khai layer khác Default | một dòng R2 |
| Renderer order 3000 trong group | một dòng R3 |
| Renderer không có group tổ tiên | một dòng R4, chỉ hiện khi bật Show roots |
| Renderer cùng object với group, layer Default, order 0 | không dòng |
| `SpriteMask` bật Custom Range, dải trỏ layer chết | một dòng R1 |
| Thư mục đã dọn theo mô hình ba bậc | 0 vi phạm |
