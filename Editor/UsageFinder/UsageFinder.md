# Usage Finder

Trả lời **reverse lookup** đầy đủ: *"Chọn 1 GameObject/asset → **MỌI** field nào đang trỏ tới nó?"* — không sót case nào (list, object nested trong list, `AssetReference`, `ObjectReference`, `ExposedReference`, `[SerializeReference]`). Click 1 field → **điều hướng tới nơi chứa reference** + highlight field trong Inspector.

## Mở cửa sổ

- Menu: **Horcrux > Usage Finder**
- Context menu: right-click asset trong Project → **Find Usages (Horcrux)**
- Kéo bất kỳ GameObject/asset vào ô **Target** → bấm **🔍 Find All Usages**

---

## Một lệnh — tự nhận diện target

Không còn tab. Window tự chọn cơ chế theo loại target:

| Loại target | Cơ chế | Phạm vi |
|-------------|--------|---------|
| **Asset** (có GUID: prefab/.asset/.mat/SO/texture...) | `AssetReferenceScanner` — grep-GUID 2 pha | Toàn project + scene đang mở + scene file trên disk |
| **Scene object** (GameObject/Component đang sống trong scene) | `SceneReferenceScanner` — match instanceID | Mọi scene đang loaded |

Kết quả cả 2 đường gộp về **cùng một danh sách** `UsageEntry` → cùng drawer, cùng filter.

---

## Cơ chế "cách B" — grep GUID trong text (không sót)

Unity serialize **ForceText** (`m_SerializationMode: 2`) → mọi asset là YAML text. Mọi reference — hard-dependency (`guid:`), `AssetReference` (`m_AssetGUID:`), từng phần tử list, object nested — đều ghi **chuỗi GUID target dạng text**.

**Pha 1 — candidate discovery (nhanh, chỉ đọc text):**
Quét text mọi file có đuôi mang-reference trong `Assets/`, tìm substring `targetGuid`. File khớp → **candidate**. Vì đọc text thô, pha 1 **độc lập** với việc walker có nhận diện đúng kind hay không → đây là **lưới an toàn** chống sót. Đặc biệt bắt cả `AssetReference` mà `AssetDatabase.GetDependencies` (và index cũ) **bỏ sót hoàn toàn**.

**Pha 2 — detail extraction (chỉ candidate):**
Load candidate → `SerializedPropertyWalker` tìm field cụ thể trỏ tới target → build field path clickable + navigation context. Nếu candidate khớp pha 1 nhưng pha 2 **không** map được ra field (kind lạ / GUID nằm ngoài field walk tới) → tạo **fallback hit mức-file** để không bao giờ mất một hit của pha 1.

> **Vì sao không dùng dependency index?** Index (reverse `GetDependencies`) nhanh nhưng **sót AssetReference**. Yêu cầu "kể cả AssetReference" loại bỏ nó khỏi vai trò nguồn chính. `Common/AssetReferenceIndex` vẫn còn cho mục đích khác, không dùng ở tab này.

---

## Điều hướng khi click field

| Nguồn reference | Click field làm gì |
|-----------------|--------------------|
| Asset (SO/material/prefab) | Select asset chứa + expand đúng component/property (prefab → mở prefab stage) |
| Scene đang mở | Select GameObject + expand component chứa field |
| Scene file **trên disk** (chưa mở) | Hỏi lưu scene hiện tại → **mở scene** → walk định vị GameObject trỏ tới target → select + expand |

---

## Phạm vi quét (target là asset)

| Nguồn | Xử lý |
|-------|-------|
| Prefab | `LoadAsset` → walk mọi component (kể cả native: `MeshRenderer.sharedMaterial`,...) + con cháu |
| File `.asset` (main + sub-asset) | `LoadAllAssetsAtPath` → walk mọi Object managed (SO, Material, AnimationClip,...) trừ GameObject/Component (đã qua đường prefab) |
| Scene đang mở | Walk live hierarchy — chính xác cả khi có thay đổi chưa lưu |
| Scene file trên disk | Pha 1 grep text bắt → hit mức-file; mở scene khi click |

Đuôi file quét: `.prefab .unity .asset .mat .anim .controller .playable .preset .mask .spriteatlas .terrainlayer .rendertexture ...` (danh sách `ReferenceCarryingExtensions`).

---

## An toàn khi hủy (không khẳng định sai)

Cả 2 pha có **progress bar cancelable**. Hủy giữa chừng → kết quả một phần → window đánh dấu `incomplete` → drawer hiện **cảnh báo vàng** "quét bị hủy, kết quả không đầy đủ", **KHÔNG** hiện "không có tham chiếu nào". Chỉ khi quét hoàn tất mà 0 kết quả mới báo "✅ không tìm thấy field nào trỏ tới".

---

## Bố cục

```
┌────────────────────────────────────────────┐
│ Target: [ ObjectField                    ]   │
│ [        🔍 Find All Usages              ]   │
│ [ 🔍 filter                            ] [✕] │
│ ──────────────────────────────────────────  │
│ ▼ 📄 Bar.asset            .../Bar.asset      │ ← referencer (click → ping asset)
│      Bar (Enemy) > Spawn Ref                 │ ← field hit (click → điều hướng + highlight)
│      Bar (Enemy) > Waves[2] > Prefab         │ ← bắt cả trong list/nested
│ ▼ 🎮 Player               MainScene          │ ← scene object referencer
│      Health > Target                         │
│ ──────────────────────────────────────────  │
│ 3 fields in 2 referencers                    │ ← status bar
└────────────────────────────────────────────┘
```

---

## Caching Strategy (zero alloc trong OnGUI)

| Giai đoạn | Khi nào | Cache gì |
|-----------|---------|----------|
| Scan time | Bấm Find All Usages | `displayLabel`, `pathLabel`, `typeName`, `foldoutKey`, `icon` trên `UsageEntry`; `displayLabel` trên `UsageFieldHit` |
| Filter rebuild | Layout phase khi `_filterDirty` | `_filteredResults` (thêm thẳng entry gốc, không tạo object mới) |
| Draw time | Mỗi OnGUI | Chỉ update `.text`/`.image` trên reusable GUIContent |

---

## Dùng chung từ `Common/`

| Thành phần | Dùng để |
|-----------|---------|
| `SerializedPropertyWalker` | Pha 2 — traversal + `FieldHitVisitor` so từng kind |
| `NavigationHelper` | Điều hướng: `SelectAndExpandAsset`, `SelectAndPingProperty` |
| `Static*` (Color, GUILayout, Styles) | Toolbar, nút, row styling |

---

## Cấu trúc file

| File | Vai trò |
|------|---------|
| `UsageFinderWindow.cs` | EditorWindow — 1 lệnh, tự nhận diện target, gộp scene-object về `UsageEntry`, filter, status |
| `AssetReferenceScanner.cs` | Cơ chế cách B — grep-GUID 2 pha; walk prefab/.asset/open-scene; mở scene disk khi click |
| `UsageResultDrawer.cs` | Vẽ referencer → field hit clickable; điều hướng khi click |
| `UsageResult.cs` | Data model `UsageEntry` + `UsageFieldHit` (immutable, cached, navigation context) |
| `UsageFinder.md` | Tài liệu thiết kế (file này) |

---

## Lưu ý

- **Không sót** là mục tiêu số 1: pha 1 (text grep) là lưới an toàn; pha 2 chỉ làm đẹp + clickable, không thêm/bớt nguồn.
- **Fallback file-level**: nếu pha 2 không xác định field, vẫn giữ hit "⚠️ chứa reference" — thà thừa còn hơn sót.
- **Scene chưa mở** vẫn được bắt (qua text grep), click → tự mở scene rồi định vị.
- **Sub-asset làm target** (sprite trong atlas): pha 1 theo GUID file cha.
- Cơ chế phụ thuộc **ForceText serialization**. Nếu project đổi sang binary → pha 1 không grep được (hiện project đang ForceText ✓).
