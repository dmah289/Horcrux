# Null Reference Scanner

Tool quét và hiển thị tất cả trường `ObjectReference` và `AssetReference` (Addressables) đang null trên các component trong scene/selection/prefab.

## Mở cửa sổ

Menu: **Horcrux > Null Reference Scanner**

---

## Bố cục

### Thanh công cụ (Toolbar)

| Nút | Chức năng |
|-----|-----------|
| **Scene / Selection / Prefabs** | Chọn phạm vi quét |
| **🔍 Scan** | Bắt đầu quét null reference |

### Thanh lọc (Filter Bar)

| Thành phần | Mô tả |
|------------|-------|
| **Filter** | Gõ để lọc kết quả theo tên GameObject, Component, hoặc Field (không phân biệt hoa/thường) |
| **✕** | Xóa bộ lọc |
| **Found: N issues** | Tổng số vấn đề tìm được |

### Kết quả (Results)

Hiển thị dạng cây phân cấp 3 tầng:

```
▼ 🎮 Player (3 issues)              ← GameObject — click để chọn & ping
│  ▼ 📦 PlayerController            ← Component — foldout
│  │    ⚠ rigidbody ── Rigidbody    ← Field null — click để chọn GO
│  │    ⚠ groundCheck ── Transform
│  ▼ 📦 PlayerHealth
│       ⚠ healthBar ── Image
```

### Thanh trạng thái (Status Bar)

Hiển thị tổng số issues và số GameObjects bị ảnh hưởng. Khi có bộ lọc, hiển thị "Showing X of Y issues".

---

## Phạm vi quét (Scope)

| Scope | Mô tả |
|-------|-------|
| **Scene** | Tất cả GameObject trong scene đang mở, bao gồm con cháu |
| **Selection** | Chỉ các GameObject đang chọn trong Hierarchy, bao gồm con cháu |
| **Prefabs** | Tất cả Prefab asset trong toàn bộ project |

---

## Cách sử dụng

1. Mở cửa sổ: **Horcrux > Null Reference Scanner**
2. Chọn scope (**Scene** / **Selection** / **Prefabs**)
3. Bấm **Scan**
4. Duyệt kết quả — bấm vào dòng bất kỳ để chọn GameObject trong Hierarchy
5. Sửa null reference trong Inspector
6. Bấm **Scan** lại để kiểm tra

---

## Phát hiện

| Loại | Mô tả |
|------|-------|
| **Null ObjectReference** | Trường serialized kiểu UnityEngine.Object (Transform, Image, GameObject,...) chưa được gán |
| **Null AssetReference** | Trường `AssetReference` (Addressables) có `m_AssetGUID` rỗng — chưa gán asset. Bao gồm các subclass: `AssetReferenceGameObject`, `AssetReferenceTexture`,... |
| **Missing Script** | Component có script đã bị xóa hoặc không tìm thấy |
| **Nested fields** | Trường trong `[Serializable]` class/struct lồng nhau |
| **Array/List elements** | Phần tử null trong mảng/list của ObjectReference |

---

## Lưu ý

- **Không tự động quét**: Chỉ quét khi bấm Scan. Thay đổi scene/selection không tự động cập nhật kết quả
- **Trường `m_Script` bị bỏ qua**: Script reference trên mỗi component không được báo cáo
- **Prefabs scope có thể chậm**: Tải tất cả prefab trong project — hiển thị progress bar, có thể hủy
- **Foldout state**: Trạng thái mở/đóng giữ qua domain reload (SessionState), mất khi đóng Editor

---

## Cấu trúc file

| File | Vai trò |
|------|---------|
| `NullRefScannerWindow.cs` | EditorWindow — vẽ giao diện, toolbar, kết quả |
| `NullRefScannerCore.cs` | Logic quét — static class, không phụ thuộc UI |
| `NullRefScanResult.cs` | Các lớp dữ liệu: `GameObjectResult`, `ComponentResult`, `FieldResult` |
| `NullRefScanner.md` | Tài liệu thiết kế (file này) |
