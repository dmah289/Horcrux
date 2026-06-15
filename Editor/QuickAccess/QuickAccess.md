# Quick Access Window - Hướng dẫn sử dụng

## Mở cửa sổ

Menu: **manhnd_sdk > Window > Quick Access**

---

## Bố cục

### Thanh công cụ (toolbar)

| Nút | Chức năng |
|---|---|
| **Add Group** | Tạo một group mới |
| **Scene: \<tên\>** | Dropdown chọn scene dùng cho nút Play Game |

### Nút Play Game
1. Lưu scene hiện tại
2. Mở scene đã chọn trong dropdown
3. Vào chế độ Play

Nút bị vô hiệu hóa khi chưa chọn loading scene.

### Build Scenes

Hiển thị tất cả scene trong **Build Settings**.
- Bấm tên scene để mở
- Bấm **Ping** để highlight trong Project window

### Groups

Các nhóm tự tạo để sắp xếp asset và folder.

---

## Groups

### Các nút trên header

| Nút | Chức năng |
|---|---|
| **Tên group (foldout)** | Đóng / mở danh sách item |
| **Edit** | Bật / tắt editor inline |
| **Refresh** | Quét lại root folder của group này và rebuild danh sách loaded |
| **Up / Down** | Sắp xếp thứ tự group |
| **X (đỏ)** | Xóa group (có hộp thoại xác nhận) |

### Editor inline (chế độ Edit)

| Trường | Mô tả |
|---|---|
| **Title** | Tên hiển thị của group |
| **Enable Loading Files From Root Folders** | Bật: tự động load các file bên trong root folder, hiển thị ở mục "From root folders" |
| **Enable Loading Subfolders From Root Folders** | Bật: tự động load các subfolder bên trong root folder, hiển thị ở mục "From root folders" |
| **Enable Loading Recursively** | Bật: load file đệ quy toàn bộ cây thư mục con. Tắt (mặc định): chỉ load file ngay bên dưới root folder |
| **Root Folders** | Danh sách root folder đã đăng ký. Bấm vào đường dẫn để ping, bấm X để xóa |

### Danh sách item

Item chia thành 2 phần:

- **Pinned** - Shortcut ghim trực tiếp. Folder ở đây chỉ là tham chiếu, không scan nội dung bên trong.
- **From root folders** - Cache tự động từ root folder. Được rebuild khi bấm Refresh trên header group.

Mỗi dòng item có:
- **Ping** - Highlight trong Project window
- **Tên** - Bấm để mở (asset) hoặc ping (folder)
- **Remove** - Xóa khỏi group (item loaded sẽ xuất hiện lại khi Refresh)

---

## Kéo thả (Drag and Drop)

Kéo asset hoặc folder từ Project window vào bất kỳ group nào.

| Thao tác | Kết quả | Màu hover |
|---|---|---|
| **Thả bình thường** | **Ghim (Pin)** item vào group | Vàng amber |
| **Giữ Shift + Thả** (chỉ folder) | Thêm folder làm **Root** (tự động load nội dung) | Xanh dương |

- File luôn được ghim (không thể làm root).
- Thả ra ngoài group sẽ bị từ chối.

---

## Tham khảo nhanh

```
Ghim asset/folder       -->  Kéo vào group
Thêm root folder        -->  Giữ Shift + Kéo folder vào group
Refresh một group       -->  Bấm Refresh trên header group
Chơi game               -->  Chọn loading scene, bấm Play Game
Sắp xếp group           -->  Dùng nút Up/Down trên header
```

---

## Cấu trúc file

| File | Vai trò |
|---|---|
| `QuickAccessWindow.cs` | EditorWindow - vẽ giao diện, kéo thả, toolbar |
| `QuickAccessConfig.cs` | ScriptableSingleton - lưu trạng thái, xử lý mọi thay đổi dữ liệu |
| `QuickAccessGroup.cs` | Lớp dữ liệu serializable - chứa các trường của một group và rebuild cache |

Config lưu tại: `ProjectSettings/manhnd_sdk_QuickAccessConfig.asset`
