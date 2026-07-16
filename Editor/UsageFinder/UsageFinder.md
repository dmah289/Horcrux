# Usage Finder

Trả lời câu hỏi **reverse lookup** ở mức asset: *"Asset / Material / ScriptableObject / prefab này đang được **dùng ở đâu**?"* — để biết sửa nó có ảnh hưởng chỗ khác không, hoặc xóa có an toàn không.

Gộp 2 tab cùng làm việc trên **một asset nguồn**, nhưng dùng **2 cơ chế khác nhau** (bắt buộc tách vì bản chất kỹ thuật khác nhau).

## Mở cửa sổ

- Menu: **Horcrux > Usage Finder**
- Context menu: right-click asset trong Project → **Find Usages (Horcrux)**

---

## Hai tab

| Tab | Trả lời | Cơ chế | Tốc độ |
|-----|---------|--------|--------|
| **Asset Usages** | Asset nào **trực tiếp** tham chiếu target (hard dependency) | Query `AssetReferenceIndex` (reverse dependency index) | Nhanh — O(1), tự động khi đổi target |
| **Addressable Usages** | `AssetReference` (Addressables) nào trỏ tới target | Quét `m_AssetGUID` toàn project qua `SerializedPropertyWalker` | Chậm — cần bấm **Scan** |

> **Vì sao 2 cơ chế?** `AssetReference` lưu qua `m_AssetGUID` và **KHÔNG** phải hard dependency → không nằm trong dependency graph của Unity. Tab "Asset Usages" (dựa dependency index) sẽ **bỏ sót** hoàn toàn Addressable usage. Muốn tìm chúng phải quét thẳng serialized data — đó là tab riêng.

---

## Tab 1 — Asset Usages

| Bước | Hành vi |
|------|---------|
| Đổi Target | Query lại **tức thì** (không cần bấm nút) |
| Kết quả | Danh sách asset đang trực tiếp trỏ tới target — click để ping trong Project |
| `↻ Rebuild Index` | Full rebuild index — dùng sau thay đổi ngoài Unity (git checkout, sửa file tay) mà postprocessor không bắt được |
| "No other asset references this" | An toàn để sửa/xóa mà không ảnh hưởng asset khác |

- Index build **lazy** ở query đầu tiên (progress bar), sau đó tự cập nhật incremental qua `AssetPostprocessor`. Xem `Common/AssetReferenceIndex.md`.

## Tab 2 — Addressable Usages

| Bước | Hành vi |
|------|---------|
| Đổi Target | Xóa kết quả cũ, **chờ** bấm Scan (quét nặng) |
| `🔍 Scan Addressable Usages` | Quét Prefab + mọi file `.asset` (main **và** sub-asset) toàn project + Scene đang mở |
| Kết quả | Cây 2 tầng: `Asset chứa AssetReference → field path` (foldout để xem chi tiết field) |
| Progress bar | Cancelable — project lớn quét lâu |

**Phạm vi quét `.asset`:** gộp 2 nguồn để không lọt AssetReference:

| Nguồn | Bắt được gì |
|-------|-------------|
| `FindAssets("t:ScriptableObject")` | SO subclass ở **mọi extension** (kể cả `.asset` đổi tên) |
| Mọi file `*.asset` qua `GetAllAssetPaths()` | **Container**: file mà main asset không phải SO nhưng chứa **sub-asset** SO nested — kiểu collection gom AssetReference để load Addressable |

Mỗi file được `LoadAllAssetsAtPath` → walk **cả main asset lẫn từng sub-asset**. Chỉ Object managed (`ScriptableObject`/`MonoBehaviour`) mới có thể khai báo `AssetReference` field nên loại còn lại được bỏ qua an toàn. Path từ 2 nguồn được **dedupe** qua `HashSet` (không quét trùng).

**Scene:** chỉ các **Scene đang mở**. Scene chưa mở không được quét (phải load/unload — destructive). Muốn phủ hết → mở scene đó rồi Scan lại.

---

## Bố cục

```
┌────────────────────────────────────────────┐
│ [ Asset Usages ] [ Addressable Usages ]     │ ← tab (active = xanh)
│ Target: [ ObjectField                    ]   │
│ [ ↻ Rebuild Index ] / [ 🔍 Scan ... ]        │ ← đổi theo tab
│ [ 🔍 filter                            ] [✕] │
│ ──────────────────────────────────────────  │
│ 📄 Foo.prefab            Assets/.../Foo.prefab│
│ ▼ 📄 Bar.asset           Assets/.../Bar.asset │ ← foldout khi có detail
│      Bar (Enemy) > Spawn Ref                 │
│ ──────────────────────────────────────────  │
│ Found 2 referencers                          │ ← status bar
└────────────────────────────────────────────┘
```

---

## Caching Strategy (zero alloc trong OnGUI)

| Giai đoạn | Khi nào | Cache gì |
|-----------|---------|----------|
| Scan/query time | Đổi target / bấm Scan | `displayLabel`, `pathLabel`, `typeName`, `foldoutKey`, `icon` trên mỗi `UsageEntry` |
| Filter rebuild | Layout phase khi `_filterDirty` | `_filteredResults` |
| Draw time | Mỗi OnGUI | Chỉ update `.text`/`.image` trên reusable GUIContent |

---

## Dùng chung từ `Common/`

| Thành phần | Dùng để |
|-----------|---------|
| `AssetReferenceIndex` | Tab 1 — reverse dependency index |
| `SerializedPropertyWalker` | Tab 2 — traversal + `AssetRefMatchVisitor` đọc `m_AssetGUID` |
| `Static*` (Color, GUILayout, Styles) | Toolbar, nút, row styling |

---

## Cấu trúc file

| File | Vai trò |
|------|---------|
| `UsageFinderWindow.cs` | EditorWindow — 2 tab, target field, filter, status, rebuild/scan, context MenuItem |
| `UsageResultDrawer.cs` | Vẽ danh sách referencer (asset → detail lines) với reusable GUIContent |
| `AssetUsageScanner.cs` | Tab 1 logic — query `AssetReferenceIndex` (static) |
| `AddressableUsageScanner.cs` | Tab 2 logic — GUID scan `m_AssetGUID` qua walker; quét prefab + `.asset` (main + sub-asset) + open scenes (static) |
| `UsageResult.cs` | Data model `UsageEntry` — immutable, cached display data |
| `UsageFinder.md` | Tài liệu thiết kế (file này) |

---

## Lưu ý

- **Tab 1 không bắt AssetReference** (Addressables) — luôn kiểm tra Tab 2 cho asset addressable.
- **Tab 2 bắt cả sub-asset** — collection SO gom AssetReference vào SO con nested vẫn được phát hiện.
- **Sub-asset làm target** (sprite trong atlas): Tab 1 theo GUID file cha — độ mịn mức file.
- **Index stale** khi sửa ngoài Unity → bấm `↻ Rebuild Index`.
- **Scene chưa mở** không nằm trong phạm vi Tab 2.
