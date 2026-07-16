# AssetReferenceIndex

Reverse dependency index — foundation cho **Usage Finder** (Tool 1). Trả lời câu hỏi *"asset nào đang tham chiếu asset này?"*, ngược với API forward duy nhất Unity cung cấp.

> Đây là hệ thống dùng chung trong `Common/` — consumer (UsageFinder Tab 1) chỉ phụ thuộc public API, không tự đi quét project (SOLID — D).

---

## 1. Vấn đề

`AssetDatabase.GetDependencies(path)` chỉ cho **forward** map: *"asset A dùng những gì"*. Không có API reverse. Muốn biết *"ai dùng A"* phải **quét forward map toàn project rồi đảo ngược**.

⚠️ **Giới hạn quan trọng:** chỉ bắt **hard dependency** (reference trực tiếp qua GUID trong serialized data). `AssetReference` (Addressables) dùng `m_AssetGUID` và **KHÔNG** phải hard dependency → **không** xuất hiện ở đây. Usage của Addressable là việc của `AddressableUsageScanner` (Tool 3), cơ chế riêng.

---

## 2. Quyết định thiết kế đã chốt

| Quyết định | Lựa chọn | Lý do |
|-----------|----------|-------|
| Độ sâu | **Direct (`recursive:false`)** | Đúng nhu cầu "sửa đây thì đâu đổi theo"; index nhỏ, không nhiễu |
| Cấu trúc | **Forward + Reverse map** | Forward để patch/gỡ chính xác O(k) khi asset đổi trên đường nóng |
| Persistence | **JSON** (Newtonsoft) | Serialize thẳng `Dictionary` (không cần struct trung gian); chỉ lưu forward, derive reverse khi load |

---

## 3. Cấu trúc dữ liệu

```csharp
Dictionary<string, string[]>       _forward;  // referencerPath → direct dep GUIDs
Dictionary<string, List<string>>   _reverse;  // targetGuid     → referencer paths (kết quả query)
```

- **`_reverse` là kết quả query.** `_forward` tồn tại **chỉ** để incremental update gỡ entry cũ chính xác (không phải duyệt toàn bộ `_reverse` để tìm path).
- Lưu **path** (string) thay vì object → không giữ asset load trong RAM.
- Reverse list pre-alloc capacity 2 (đa số target ít referencer), grow-only.

---

## 4. Luồng hoạt động

```
[InitializeOnLoad]
  └─ TryLoadCache()  — KHÔNG build. Load forward từ Library/, derive reverse.
       ├─ OK   → _built = true
       └─ lỗi/thiếu/stale → _built = false (build lazy)

GetReferencers(guid)  ─→ EnsureBuilt() ─→ (chưa built) BuildFull() ─→ query O(1)

AssetPostprocessor.OnPostprocessAllAssets(imported, deleted, moved)
  └─ nếu _built: patch từng entry (RefreshReferencer / RemoveAsset / MoveReferencer)
       └─ MarkCacheDirty() → debounce ghi cache qua delayCall
  └─ nếu !_built: bỏ qua (build lazy sau)
```

---

## 5. Full build

```
foreach path in GetAllAssetPaths():
    if !IsProjectAsset(path) continue          // chỉ Assets/, bỏ folder & Packages/
    deps = GetDependencies(path, recursive:false)
    foreach dep in deps (dep != path):
        guid = AssetPathToGUID(dep)
        _reverse[guid].Add(path)               // đảo ngược
    _forward[path] = depGuids
```

- Progress bar cancelable, update mỗi 128 asset (`i & 0x7F`) — tránh spam.
- Cancel giữa chừng → clear cả 2 map, `_built = false` (không để index dở dang).

---

## 6. Incremental update (event-driven — SKILL #1)

`AssetRefIndexPostprocessor : AssetPostprocessor` gọi 3 patch, **không** full rebuild:

| Sự kiện | Method | Xử lý |
|---------|--------|-------|
| Import / thay đổi | `RefreshReferencer(path)` | Gọi `IndexAsset` (tự `RemoveReferencer` trước → idempotent) |
| Xóa | `RemoveAsset(path, guid)` | Gỡ vai trò referencer + xóa reverse entry của chính nó (target đã biến mất) |
| Di chuyển | `MoveReferencer(from, to)` | Remap key trong `_forward` + cập nhật path trong mọi reverse list |

**Chìa khóa:** `RemoveReferencer` dùng `_forward[path]` để biết chính xác path từng trỏ target nào → gỡ O(k), không duyệt O(n) toàn bộ reverse map.

**Invariant idempotent:** `IndexAsset` luôn `RemoveReferencer(path)` trước khi ghi lại → mỗi `(path, targetGuid)` chỉ tồn tại đúng 1 lần trong reverse. Nhờ vậy `AddReverse` **không cần** check duplicate (`list.Contains`) — tránh O(n²) khi một target bị hàng trăm asset tham chiếu.

---

## 7. Persistence

- File: `Library/Horcrux/AssetRefIndex.json` (Library/ không commit git, per-machine — hợp lý cho cache).
- Dùng **Newtonsoft** (`com.unity.nuget.newtonsoft-json`, precompiled DLL auto-referenced) → serialize thẳng `Dictionary<string,string[]>`, không cần class trung gian.
- **Chỉ serialize `_forward`** → khi load derive lại `_reverse`. Single source of truth, không bao giờ lệch 2 map trên disk.
- Header `formatVersion` + `unityVersion` → mismatch thì bỏ cache, build lazi.
- Ghi debounce qua `EditorApplication.delayCall` (`MarkCacheDirty` → `FlushCache`) — gộp nhiều import lẻ thành 1 lần ghi.

---

## 8. Public API

```csharp
public static bool IsBuilt { get; }
public static void EnsureBuilt();
public static void Rebuild(bool showProgress = true);
public static IReadOnlyList<string> GetReferencers(string targetGuid);  // core
public static int  ReferencerCount(string targetGuid);
```

- `GetReferencers` trả trực tiếp list nội bộ (không copy); rỗng → `Array.Empty` (zero alloc).

---

## 9. Tối ưu (SKILL #3)

- Build 1 lần, query O(1) dictionary.
- `GetAllAssetPaths` duyệt `for` index-based, không LINQ.
- Reuse `DepGuidBuffer` giữa các lần `IndexAsset`.
- Reverse list grow-only, capacity nhỏ.
- Cache ghi debounce — không I/O mỗi asset import lẻ.

---

## 10. Edge cases & giới hạn

| Case | Xử lý |
|------|-------|
| `Packages/`, ProjectSettings, folder | `IsProjectAsset` lọc bỏ (chỉ `Assets/` + có extension) |
| Sub-asset (sprite trong atlas, mesh trong fbx) | Theo GUID file cha — độ mịn mức file ở v1 |
| Scene `.unity` | Là referencer hợp lệ → hiện trong kết quả |
| `AssetReference` (Addressables) | **KHÔNG** bắt được — thuộc Tool 3 |
| Thao tác ngoài Unity (git checkout, sửa tay) | Postprocessor không bắt → cung cấp nút **Rebuild** thủ công + check version khi load |
| Cancel build giữa chừng | Clear map, `_built=false` — không để index dở |

---

## 11. Cấu trúc file

| File | Vai trò |
|------|---------|
| `AssetReferenceIndex.cs` | Index + API + persistence + `AssetRefIndexPostprocessor` (incremental hook) |
| `AssetReferenceIndex.md` | Tài liệu thiết kế (file này) |
