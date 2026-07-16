# Usage Finder — Plan tổng thể

Bộ tool Editor trả lời câu hỏi **reverse lookup**: *"GameObject / Component / Asset / Material này đang được **dùng ở đâu**?"* — ngược chiều với `NullRefScanner` (tool đó hỏi *"field nào của tôi đang null?"*, tool này hỏi *"ai đang trỏ vào tôi?"*).

---

## 1. Bối cảnh & giới hạn kỹ thuật (đọc trước khi code)

Unity **không** có API reverse dependency sẵn. Điều này chia bài toán thành **2 nhánh không thể dùng chung một cơ chế**:

| Nhánh | Câu hỏi | Cơ chế bắt buộc | Vì sao |
|-------|---------|-----------------|--------|
| **Asset-level (GUID)** | Material / .asset / texture / prefab này được asset nào tham chiếu? | Reverse index từ `AssetDatabase.GetDependencies` | Unity chỉ cho forward map → phải tự đảo ngược |
| **Instance-level** | Component / GameObject này bị component nào trong scene/prefab trỏ tới? | Quét `SerializedProperty` so `objectReferenceValue == target` | Reference nội scene là instanceID, không nằm trong asset dependency graph |
| **Addressable (GUID scan)** | Prefab addressable này bị `AssetReference` field nào trỏ tới? | Quét thẳng `m_AssetGUID` trong serialized data | ⚠️ `AssetReference` **KHÔNG** là hard dependency → `GetDependencies` **bỏ sót** hoàn toàn |

> **Điểm mấu chốt #1:** `AssetReference` (Addressables) không xuất hiện trong `GetDependencies`. Nếu Tool 1 chỉ dựa vào dependency graph, nó sẽ báo "không ai dùng" cho một asset thực chất đang bị nhiều `AssetReference` trỏ tới. Đây là lý do Tool 3 tồn tại như một cơ chế riêng.

> **Điểm mấu chốt #2:** Reverse index của toàn project khá nặng để build lần đầu (duyệt mọi asset path). Bắt buộc **cache + incremental update event-driven** (SKILL quy tắc #1), không rebuild mỗi lần mở window / domain reload.

---

## 2. Kiến trúc tổng thể

```
Editor/
├── Common/
│   ├── AssetReferenceIndex.cs        ← [FOUNDATION] reverse GUID index (Tool 1 + Tool 3 chia sẻ nền)
│   ├── SerializedPropertyWalker.cs   ← [FOUNDATION] traversal SerializedProperty chung
│   │                                    (tách từ NullRefScannerCore — DRY cho Tool 2 + Tool 3)
│   └── UsageResult.cs                ← data model chung cho kết quả usage (3 tool)
│
├── UsageFinder/                      ← Tool 1 + Tool 3 (2 tab trong 1 window)
│   ├── UsageFinder.md
│   ├── UsageFinderWindow.cs          ← EditorWindow — 2 tab: "Asset Usages" | "Addressable Usages"
│   ├── AssetUsageScanner.cs          ← Tab 1 logic — tra AssetReferenceIndex (static)
│   ├── AddressableUsageScanner.cs    ← Tab 2 logic — GUID scan m_AssetGUID (static)
│   └── UsageResultDrawer.cs          ← vẽ cây kết quả (reusable GUIContent pattern)
│
└── SceneReferenceFinder/            ← Tool 2 (tách riêng — thao tác trên instance Hierarchy)
    ├── SceneReferenceFinder.md
    ├── SceneReferenceFinderWindow.cs
    ├── SceneReferenceScanner.cs      ← so objectReferenceValue == target (dùng SerializedPropertyWalker)
    └── (dùng chung UsageResultDrawer)
```

### Nguyên tắc tái sử dụng (DRY + SKILL "dùng chung qua Common/")

| Code sẵn có | Tái sử dụng như thế nào |
|-------------|-------------------------|
| `NullRefScannerCore.ScanComponent` (vòng lặp `SerializedProperty`) | **Tách lên** `Common/SerializedPropertyWalker` — dạng callback/visitor để cả NullRefScanner, Tool 2, Tool 3 dùng chung. `NullRefScannerCore` refactor để gọi walker. |
| `NullRefScannerCore.IsAssetReferenceType` + đọc `m_AssetGUID` | Tool 3 dùng lại — chỉ đổi từ "check rỗng" sang "check == targetGuid" |
| `NullRefNavigationHelper` (ping, prefab-stage nav, `GetTransformPath`, `ExpandComponent`) | **Promote lên** `Common/` (rename `NavigationHelper`) — cả 3 tool cần ping/select/mở prefab stage |
| `NullRefResultDrawer` (cây kết quả, reusable GUIContent, row zebra) | Tham chiếu pattern → `UsageResultDrawer` mô phỏng lại (data model khác nên không dùng trực tiếp) |
| `Common/Static*` (Color, GUIContent, GUILayout, Styles) | Dùng trực tiếp — thêm entry mới cho Usage Finder |
| `AssetDatabaseUtility.GetGuid` | Dùng trực tiếp |

> **Refactor điều kiện tiên quyết:** trước khi làm 3 tool, tách `SerializedPropertyWalker` và promote `NavigationHelper` lên `Common/`. Đây là bước đầu tiên, làm cho NullRefScanner vẫn chạy y nguyên (regression-safe: chỉ di chuyển logic, không đổi hành vi).

---

## 3. Foundation A — `AssetReferenceIndex` (reverse GUID index)

**Vai trò:** trái tim của Tool 1. Duy nhất một reverse map GUID → danh sách asset path đang tham chiếu. (Thiết kế chi tiết ở mục 7.)

| Khía cạnh | Thiết kế tóm tắt |
|-----------|------------------|
| Cấu trúc | `Dictionary<string targetGuid, List<string> referencerPaths>` |
| Build | Duyệt `AssetDatabase.GetAllAssetPaths()`, mỗi path `GetDependencies(path, false)` → đảo ngược |
| Incremental | `AssetPostprocessor.OnPostprocessAllAssets` → chỉ cập nhật entry của asset imported/deleted/moved |
| Persistence | Serialize ra `Library/Horcrux/AssetRefIndex.bin` (hoặc SessionState) — tránh full rebuild mỗi domain reload |
| Đối tượng phụ thuộc | Tool 1 (`AssetUsageScanner`) `Depend on abstraction` này |

---

## 4. Foundation B — `SerializedPropertyWalker` (traversal chung)

**Vai trò:** tách vòng lặp `SerializedObject.GetIterator().Next()` (hiện đang khóa trong `NullRefScannerCore.ScanComponent`) thành utility tái sử dụng.

- API dạng **visitor / callback** để mỗi consumer quyết định làm gì với mỗi property:
  - NullRefScanner: check null.
  - Tool 2 (SceneReferenceFinder): so `objectReferenceValue == target`.
  - Tool 3 (AddressableUsageScanner): đọc `m_AssetGUID` == targetGuid.
- Giữ nguyên các tối ưu sẵn có: skip `InternalPropertyPaths`, `IsAssetReferenceType`, `BuildDisplayPath`, non-alloc buffers.
- **Ràng buộc SOLID (O):** thêm walker **không sửa** hành vi NullRefScanner — refactor để nó gọi walker, output không đổi.

---

## 5. Tool 1 + Tool 3 — `UsageFinder` (1 window, 2 tab)

**Menu:** `Horcrux/Usage Finder` + context menu `Assets/Find Usages (Horcrux)` khi right-click asset trong Project.

### Tab 1 — Asset Usages (Tool 1)

> *"Material / .asset / texture / prefab này được asset nào tham chiếu? Sửa ở đây có ảnh hưởng chỗ khác?"*

| Bước | Chi tiết |
|------|----------|
| Input | Asset đang chọn trong Project (hoặc drag vào slot) |
| Lookup | `AssetReferenceIndex.GetReferencers(guid)` → danh sách asset path |
| Drill-down | Với mỗi referencer, mở ra object/component nào bên trong dùng target (dùng SerializedPropertyWalker khi cần chi tiết) |
| Hiển thị | Cây 2 tầng: `Referencer asset → (field/component dùng nó)`. Click → ping asset. |
| Cảnh báo | Nếu index chưa build / stale → nút "Rebuild Index" + progress bar |

### Tab 2 — Addressable Usages (Tool 3)

> *"Prefab addressable này có bị `AssetReference` field nào trỏ tới không?"*

| Bước | Chi tiết |
|------|----------|
| Input | Asset addressable đang chọn |
| Lookup | Lấy GUID target → quét `m_AssetGUID == targetGuid` trong toàn project (prefab/scene/.asset) |
| Cơ chế | Reuse `IsAssetReferenceType` + đọc `m_AssetGUID` từ SerializedPropertyWalker |
| Hiển thị | Cây: `Asset chứa AssetReference → field path`. Click → ping + mở tới field |
| Lưu ý | Quét toàn project chậm → progress bar + cancel; cân nhắc cache theo GUID |

### UX gộp 2 tab

- Cùng 1 selection nguồn (asset đang chọn) → chuyển tab không mất input.
- Toolbar: `[Asset Usages] [Addressable Usages]` toggle (pattern giống scope toggle của NullRefScanner).
- Filter bar + status bar tái sử dụng pattern NullRefScannerWindow.

---

## 6. Tool 2 — `SceneReferenceFinder`

> *"Nếu tôi xóa GameObject / Component này, có gây null reference ở đâu trong scene/prefab không?"*

**Menu:** context menu Hierarchy `GameObject/Find References In Scene (Horcrux)` + window.

| Bước | Chi tiết |
|------|----------|
| Input | GameObject / Component đang chọn trong Hierarchy |
| Scope | Scene hiện tại / Selection / Prefab đang mở (pattern scope giống NullRefScanner) |
| Cơ chế | Duyệt mọi component qua `SerializedPropertyWalker`, so `objectReferenceValue == target` (target có thể là GO, Component, hoặc mọi Component trên GO) |
| Hiển thị | Cây 3 tầng `GO → Component → field trỏ tới target` (tái sử dụng `UsageResultDrawer`) |
| Giá trị | Trước khi xóa → biết chính xác chỗ nào sẽ thành null ref |
| Edge case | Target là GameObject → phải match cả reference tới GO lẫn tới từng Component con của nó |

- **Tách riêng khỏi UsageFinder** vì thao tác trên **instance trong Hierarchy** (runtime scene object), khác bản chất với asset/GUID.

---

## 7. Thiết kế chi tiết `AssetReferenceIndex` (làm đầu tiên)

> Chi tiết đầy đủ được bổ sung trong file `Common/AssetReferenceIndex.md` khi triển khai. Dưới đây là bản thiết kế nền.

### 7.0 Quyết định đã chốt

| Quyết định | Lựa chọn | Lý do |
|-----------|----------|-------|
| Độ sâu index | **Direct (`recursive:false`)** | Đúng nhu cầu "sửa đây thì đâu đổi theo" = referencer trực tiếp; index nhỏ, không nhiễu |
| Incremental | **Forward + Reverse map** | Giữ thêm `_forward` (path→depGuids) để patch/gỡ O(k) chính xác trên đường nóng khi save asset |
| Persistence | **JSON** | Dễ debug + versioning; đủ nhanh cho vài nghìn entry; tối ưu binary sau nếu cần |

### 7.1 Trách nhiệm (SRP)

Duy nhất: **duy trì và cung cấp reverse map GUID → referencers**. Không biết gì về UI, không biết gì về Addressable scan (đó là Tool 3 riêng vì cơ chế khác).

### 7.2 Cấu trúc dữ liệu

```csharp
// forward: assetGuid → các guid nó phụ thuộc (build tạm khi quét)
// reverse: targetGuid → các assetPath đang tham chiếu targetGuid  (LƯU LẠI, đây là kết quả)
private static Dictionary<string, List<string>> _reverse;   // guid → referencer paths
private static bool _built;
private static bool _dirty;
```

- Lưu **path** thay vì object → không giữ reference nặng, không giữ asset load trong RAM.
- Value là `List<string>` pre-alloc, grow-only.

### 7.3 Build lần đầu (full)

```
foreach path in AssetDatabase.GetAllAssetPaths():
    if !IsProjectAsset(path) continue          // bỏ Packages/, chỉ Assets/
    foreach dep in GetDependencies(path, recursive:false):
        if dep == path continue                 // self-dep
        guid = AssetPathToGUID(dep)
        _reverse[guid].Add(path)                // đảo ngược
```

- `recursive:false` → chỉ direct dependency (tránh nổ bậc 2, reverse map chính xác theo tầng trực tiếp).
- Bọc `EditorUtility.DisplayProgressBar` + cho phép cancel (project lớn có thể vài nghìn asset).
- Chạy **1 lần**, kết quả cache ra disk.

### 7.4 Incremental update (event-driven — SKILL #1)

```csharp
class AssetRefIndexPostprocessor : AssetPostprocessor
{
    static void OnPostprocessAllAssets(
        string[] imported, string[] deleted,
        string[] moved, string[] movedFrom)
    {
        // Chỉ cập nhật entry liên quan, KHÔNG full rebuild
        foreach p in deleted:   RemoveAsReferencer(p) + RemoveAsTarget(guidOf(p))
        foreach p in imported:  RefreshReferencer(p)   // xóa entry cũ của p, quét lại dep của p
        foreach (from,to) in moved: remap path
    }
}
```

- `RefreshReferencer(path)`: xóa mọi chỗ `path` xuất hiện trong value list cũ → quét lại `GetDependencies(path,false)` → add lại. Giữ index luôn đúng mà không đụng tới asset khác.

### 7.5 Persistence

| Lựa chọn | Ưu | Nhược | Quyết định |
|----------|-----|-------|-----------|
| `SessionState` (string) | Đơn giản, sống qua domain reload | Mất khi đóng Editor, giới hạn size | Fallback |
| File `Library/Horcrux/AssetRefIndex.*` | Sống qua cả restart Editor, không giới hạn | Cần serialize/versioning | **Chọn** — binary hoặc JSON |

- Ghi `version` + `unityVersion` để invalidate khi cần.
- Load lúc `[InitializeOnLoad]`; nếu file lỗi/thiếu → mark `_dirty`, build lazy khi lần đầu query.

### 7.6 Public API (abstraction cho Tool 1)

```csharp
public static class AssetReferenceIndex
{
    public static bool  IsBuilt { get; }
    public static void  EnsureBuilt();                       // build nếu chưa / stale
    public static void  Rebuild(bool showProgress = true);   // force full rebuild
    public static IReadOnlyList<string> GetReferencers(string targetGuid); // core query
    public static int   ReferencerCount(string targetGuid);
}
```

- Tool 1 **chỉ** phụ thuộc interface này (D) — không tự đi quét project.

### 7.7 Tối ưu (SKILL #3)

- Reverse map build 1 lần, query O(1).
- Non-alloc: reuse buffer khi quét dependency; `List` value grow-only.
- Không LINQ / string concat trong path nóng (query).
- `GetAllAssetPaths` trả mảng lớn → duyệt index-based, không foreach-LINQ.

### 7.8 Edge cases

| Case | Xử lý |
|------|-------|
| Asset trong `Packages/` | Bỏ qua (chỉ index `Assets/`) — configurable |
| Sub-asset (sprite trong atlas, mesh trong fbx) | GUID cha giống nhau → cân nhắc `localId`; v1 chỉ theo GUID cha |
| Scene file `.unity` | Là referencer hợp lệ → hiện trong kết quả Tool 1 |
| AssetReference target | **KHÔNG** bắt được ở đây → thuộc Tool 3 |
| Circular dep | `recursive:false` nên không lặp vô hạn |

---

## 8. Thứ tự triển khai đề xuất

1. **Refactor nền (regression-safe):**
   - Tách `Common/SerializedPropertyWalker` từ `NullRefScannerCore`, refactor NullRefScanner gọi walker (verify output không đổi).
   - Promote `NullRefNavigationHelper` → `Common/NavigationHelper`.
2. **`Common/AssetReferenceIndex`** (+ `.md`) — foundation Tool 1. ← **bắt đầu ở đây**
3. **Tool 1 + Tool 3** (`UsageFinder`, 2 tab) — dùng index + GUID scan.
4. **Tool 2** (`SceneReferenceFinder`) — dùng walker.
5. Cập nhật `PendingFeatures.md` (đánh dấu hoàn thành) + mỗi tool 1 file `.md` (SKILL quy tắc #5).

---

## 9. Ánh xạ câu hỏi gốc → tool

| Câu hỏi người dùng | Tool | Nhánh |
|--------------------|------|-------|
| Component này được tham chiếu ở đâu trên scene/prefab? | Tool 2 | Instance |
| Xóa component/GO này có gây null ref không? | Tool 2 | Instance |
| Material / .asset này dùng ở đâu, sửa có ảnh hưởng chỗ khác? | Tool 1 | GUID index |
| Prefab addressable có bị AssetReference nào trỏ tới? | Tool 3 | GUID scan |
