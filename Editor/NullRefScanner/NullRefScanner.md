# Null Reference Scanner

Tool quét và hiển thị tất cả trường `ObjectReference`, `AssetReference` (Addressables), `[SerializeReference]`, và `ExposedReference` đang null trên các component trong scene/selection/prefab.

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
| **Filter** | Gõ để lọc kết quả theo tên GameObject, Component, Field, displayPath, hoặc kind tag (không phân biệt hoa/thường) |
| **✕** | Xóa bộ lọc |
| **Found: N issues** | Tổng số vấn đề tìm được (dirty-flag cached) |

### Kết quả (Results)

Hiển thị dạng cây phân cấp 3 tầng, **sắp xếp theo severity giảm dần** (bug nghiêm trọng hiện đầu):

```
▼ 🎮 EnemySpawner (2 issues)            ← MissingRef — hiện đầu
│  ▼ 📦 EnemyAI
│  │    ✖ target ── Transform [missing]       ← đỏ — object bị xóa
│  │    ◇ strategy ── IStrategy [managed ref] ← đỏ — [SerializeReference] null
▼ 🎮 Player (2 issues)                  ← Unassigned — hiện sau
│  ▼ 📦 PlayerController
│       ⚠ rigidbody ── Rigidbody [unassigned] ← amber — chưa gán
│       ⚠ spawnRef ── AssetReferenceGO [asset ref]
```

### Thanh trạng thái (Status Bar)

Hiển thị tổng số issues và số GameObjects bị ảnh hưởng. Khi có bộ lọc, hiển thị "Showing X of Y issues".

---

## Phạm vi quét (Scope)

| Scope | Mô tả |
|-------|-------|
| **Scene** | Tất cả GameObject trong scene đang mở, bao gồm con cháu |
| **Selection** | Chỉ các GameObject đang chọn trong Hierarchy, bao gồm con cháu |
| **Prefabs** | Tất cả Prefab asset đang chọn trong Project window |

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

| Loại | Tag | Icon | Mô tả |
|------|-----|------|-------|
| **Unassigned ObjectReference** | `[unassigned]` | ⚠ amber | Trường serialized kiểu UnityEngine.Object chưa được gán (instanceID == 0). Có thể intentional. |
| **Missing Reference** | `[missing]` | ✖ đỏ | Object từng được gán nhưng đã bị xóa/missing (instanceID != 0). **Luôn là bug.** |
| **Null AssetReference** | `[asset ref]` | ⚠ amber | Trường `AssetReference` (Addressables) có `m_AssetGUID` rỗng. Bao gồm subclass. |
| **Null ManagedReference** | `[managed ref]` | ◇ đỏ | Trường `[SerializeReference]` có giá trị null. |
| **Null ExposedReference** | `[exposed ref]` | ⚠ amber | `ExposedReference<T>` (Timeline/Playable) có `exposedName` rỗng. |
| **Missing Script** | — | ⚠ bold | Component có script đã bị xóa hoặc không tìm thấy |
| **Nested fields** | — | — | Trường trong `[Serializable]` class/struct lồng nhau |
| **Array/List elements** | — | — | Phần tử null trong mảng/list của ObjectReference |

---

## Caching Strategy

Zero allocation trong OnGUI — tất cả display strings cached theo 3 giai đoạn:

| Giai đoạn | Khi nào chạy | Cache gì |
|-----------|-------------|----------|
| **Scan time** | User bấm Scan | `goLabel`, `foldoutKey`, `compLabel`, `displayLabel`, `kindTag`, `totalIssueCount`, `maxSeverity` |
| **Filter rebuild** | Layout phase khi `_filterDirty` | `_filteredResults`, `_cachedTotalIssues`, `_cachedFilteredIssues` |
| **Draw time** | Mỗi OnGUI frame | Chỉ update `.text` trên reusable GUIContent — zero string allocation |

---

## Lưu ý

- **Không tự động quét**: Chỉ quét khi bấm Scan. Thay đổi scene/selection không tự động cập nhật kết quả
- **Internal properties bị bỏ qua**: Tất cả base class properties của MonoBehaviour (`m_Script`, `m_GameObject`, `m_PrefabInstance`,...) bị skip
- **Bao phủ [HideInInspector]**: Dùng `Next()` thay vì `NextVisible()` — quét cả field ẩn nhưng vẫn serialized
- **Severity sort**: Kết quả tự động sắp xếp — MissingRef/NullManagedRef hiện đầu (luôn là bug)
- **Scroll reset**: Scroll position reset về đầu mỗi lần scan mới
- **Prefabs scope có thể chậm**: Tải tất cả prefab đang chọn — hiển thị progress bar, có thể hủy
- **Foldout state**: Trạng thái mở/đóng giữ qua domain reload (SessionState), mất khi đóng Editor
- **[SerializeReference] cần Unity 2021.2+**: Check null managed reference sử dụng API có từ Unity 2021.2
- **Non-alloc GetComponents**: Dùng static `List<Component>` buffer thay vì allocate array mỗi lần

---

## Cấu trúc file

| File | Vai trò |
|------|---------|
| `NullRefScannerWindow.cs` | EditorWindow — orchestration: toolbar, filter, scroll, status bar |
| `NullRefResultDrawer.cs` | Vẽ cây kết quả 3 tầng (GO → Component → Field) với reusable GUIContent |
| `NullRefKindDisplay.cs` | Static lookup table — icon, tag, color, severity cho mỗi NullRefKind |
| `NullRefScannerCore.cs` | Logic quét — static class, không phụ thuộc UI. Ủy quyền traversal cho `Common/SerializedPropertyWalker`, chỉ giữ `NullCheckVisitor` (quyết định reference nào null) |
| `NullRefScanResult.cs` | Data model: `NullRefKind`, `FieldResult`, `ComponentResult`, `GameObjectResult` — immutable, cached display data |
| `NullRefScanner.md` | Tài liệu thiết kế (file này) |

> **Dùng chung từ `Common/`:** traversal `SerializedProperty` (`SerializedPropertyWalker`) và điều hướng selection/ping/prefab-stage (`NavigationHelper`) — tách ra để UsageFinder và SceneReferenceFinder tái sử dụng.
