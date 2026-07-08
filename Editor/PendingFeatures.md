# Pending Features — Horcrux Editor Tools

Danh sách các tool đang chờ thiết kế chi tiết và triển khai.

---

## 1. Event Debugger

**Mục đích:** Debug realtime hệ thống `EventBus<T>` — biến event flow từ black box thành transparent.

### Vấn đề giải quyết

`EventBus<T>` decouple hoàn toàn sender và receiver. Khi event không fire, fire sai thứ tự, hoặc không ai subscribe → rất khó trace bằng `Debug.Log`.

### Chức năng chính

| # | Chức năng | Mô tả |
|---|-----------|-------|
| 1 | **Live Event Log** | Hiển thị realtime mọi event được `Raise` trong Play mode — tên type, data fields, timestamp, frame count |
| 2 | **Listener Registry** | Liệt kê tất cả listeners đang subscribe mỗi event type — class name, method, priority |
| 3 | **Dead Event Warning** | Cảnh báo khi event được Raise nhưng không có listener nào subscribe |
| 4 | **Filter & Search** | Lọc theo event type name, sender, hoặc data content |
| 5 | **Event Flow Trace** | Click vào 1 event → hiển thị chain: ai Raise → những ai nhận (theo priority order) → kết quả (success / exception) |

### Tích hợp với hệ thống hiện có

- Cần hook vào `EventBus<T>.Raise()` và `EventBinding<T>.Register()/Deregister()` — có thể dùng `#if UNITY_EDITOR` conditional compilation hoặc callback injection
- `EventBus` hiện dùng static generics → debugger cần reflection hoặc registry pattern để thu thập tất cả active `EventBus<T>` types tại runtime
- `EventBinding` đã có priority-based listener list → debugger đọc trực tiếp

### Cấu trúc dự kiến

```
EventDebugger/
├── EventDebugger.md
├── EventDebuggerWindow.cs        ← EditorWindow — live log + filter + listener view
├── EventDebuggerBridge.cs        ← Runtime hook — inject vào EventBus để capture events
├── EventRecord.cs                ← Data struct: timestamp, eventType, data snapshot, listeners
└── EventDebuggerDrawer.cs        ← Vẽ log entries + listener registry (SRP)
```

### Lưu ý thiết kế

- **Chỉ hoạt động trong Play mode** — không có event nào fire ngoài Play
- **GC-friendly:** EventRecord nên pool/ring-buffer (giữ N events gần nhất, không grow vô hạn)
- **Performance:** Hook phải lightweight — không làm chậm event dispatch. Capture data bằng struct snapshot, không giữ reference
- **Runtime code thay đổi tối thiểu:** Bridge inject qua `#if UNITY_EDITOR` trong EventBus, không sửa public API

---

## 2. Remote Config Scaffolder

**Mục đích:** Wizard tự động generate boilerplate khi thêm `RCVariable` mới — giảm lỗi assembly mismatch và ceremony thủ công.

### Vấn đề giải quyết

Thêm 1 RCVariable mới cần:
1. Tạo file partial interface `IRCVariableCollection` với property declaration
2. Tạo file partial class `RCVariableCollection` với `[RegisteredRCVar]` field + property implementation
3. Đặt đúng folder có `.asmref` trỏ về `com.horcrux.runtime`
4. Đúng namespace `Horcrux.Runtime.Implementations.RemoteConfig`

Sai bất kỳ bước nào → compile error khó hiểu hoặc variable không được register.

### Chức năng chính

| # | Chức năng | Mô tả |
|---|-----------|-------|
| 1 | **Add Variable Wizard** | Nhập tên variable + chọn type (string, int, float, bool, enum, JSON class) → auto-generate cả 2 partial files |
| 2 | **Auto .asmref** | Tự tạo `.asmref` file nếu folder chưa có — trỏ đúng về `com.horcrux.runtime` |
| 3 | **Validation Panel** | Scan tất cả `[RegisteredRCVar]` fields → cảnh báo: thiếu attribute, sai namespace, type mismatch, duplicate key |
| 4 | **Value Preview** | Hiển thị mỗi variable: current runtime value / PlayerPrefs cached value / editor default — 3-tier fallback |
| 5 | **Firebase Key Mapping** | Bảng tổng hợp: variable name ↔ Firebase key — phát hiện key trùng hoặc key rỗng |

### Tích hợp với hệ thống hiện có

- Đọc `RCVariableCollection` singleton (ScriptableObject trong Resources) để lấy danh sách variables
- Parse `[RegisteredRCVar]` attribute qua reflection
- Template generation dùng `AssetDatabase.CreateAsset()` hoặc `File.WriteAllText()` + `AssetDatabase.Refresh()`
- `.asmref` file format đã có pattern từ project hiện tại

### Cấu trúc dự kiến

```
RemoteConfigScaffolder/
├── RemoteConfigScaffolder.md
├── RCScaffolderWindow.cs         ← EditorWindow — wizard + validation panel + value preview
├── RCTemplateGenerator.cs        ← Static — generate partial interface/class files từ template
├── RCValidator.cs                ← Static — scan [RegisteredRCVar], check namespace/type/key
└── RCValueInspector.cs           ← Drawer — hiển thị 3-tier value fallback cho mỗi variable
```

### Lưu ý thiết kế

- **Code generation an toàn:** Kiểm tra file đã tồn tại trước khi ghi — không overwrite code user đã sửa
- **Template dạng const string:** Không dùng T4 hay external template engine — giữ self-contained trong SDK
- **Namespace detection:** Tự detect namespace từ folder structure hoặc từ `.asmdef`/`.asmref` gần nhất
- **Undo support:** Sau khi generate, ghi log đường dẫn file đã tạo — user có thể xóa thủ công nếu sai

---

## 3. Runtime Inspector / Tweaker

**Mục đích:** Thay đổi giá trị runtime trong Play mode mà không cần pause — iterate gameplay nhanh hơn.

### Vấn đề giải quyết

Khi test gameplay, muốn thử thay đổi tốc độ, damage, spawn rate... phải:
1. Pause Play mode
2. Tìm đúng GO trong Hierarchy
3. Sửa giá trị trong Inspector
4. Unpause

Hoặc phải thêm `Debug.Log` rồi đọc Console. Rất chậm cho iteration loop.

### Chức năng chính

| # | Chức năng | Mô tả |
|---|-----------|-------|
| 1 | **`[Tweak]` Attribute** | Đánh dấu field cần expose: `[Tweak] public float moveSpeed = 5f;` → tự động hiện trong Tweaker window |
| 2 | **Live Tweak Panel** | EditorWindow hiển thị tất cả `[Tweak]` fields đang active trong scene — slider/toggle/input tùy type |
| 3 | **Auto-discovery** | Khi Enter Play mode, scan scene tìm tất cả MonoBehaviour có field `[Tweak]` → build panel tự động |
| 4 | **Preset System** | Lưu/load bộ giá trị đã tweak thành preset (ScriptableObject). VD: "Easy", "Hard", "Stress Test" |
| 5 | **Reset to Default** | Mỗi field có nút reset về giá trị ban đầu (capture lúc Enter Play mode) |
| 6 | **Group by Component** | Hiển thị theo nhóm: GO name → Component name → Fields — cùng pattern 3 tầng như NullRefScanner |

### Tích hợp với hệ thống hiện có

- `[Tweak]` attribute đặt trong `Horcrux.Runtime.Utilities` — tương tự `[Splitter]` attribute đã có
- Window dùng `Common/` shared styles, colors, layout options
- Preset lưu dạng ScriptableObject trong `Assets/_Project/TweakPresets/` (hoặc user-configurable)
- Auto-discovery dùng `FindObjectsByType<MonoBehaviour>()` + reflection cache `[Tweak]` fields

### Cấu trúc dự kiến

```
RuntimeTweaker/
├── RuntimeTweaker.md
├── RuntimeTweakerWindow.cs       ← EditorWindow — scan, display, preset management
├── TweakFieldInfo.cs             ← Cached field metadata: owner, FieldInfo, type, default value, group
├── TweakDrawer.cs                ← Vẽ field controls theo type (float→slider, bool→toggle, enum→popup...)
├── TweakPreset.cs                ← ScriptableObject — lưu Dictionary<string, object> serialized
├── TweakScanner.cs               ← Static — scan scene cho [Tweak] fields, build TweakFieldInfo list

Runtime (attribute):
└── Assets/Horcrux/Runtime/Utilities/TweakAttribute.cs  ← [Tweak(min, max, group)] PropertyAttribute
```

### Lưu ý thiết kế

- **Chỉ hoạt động trong Play mode** — scan khi Enter Play, clear khi Exit Play
- **Reflection cache:** Scan + cache `FieldInfo` 1 lần khi Enter Play, không scan mỗi frame
- **Serialization cho preset:** Dùng JSON (Newtonsoft.Json đã có trong project) để serialize giá trị — hỗ trợ primitive types + Vector2/3 + Color
- **GC-friendly:** Drawer dùng reusable GUIContent pattern như NullRefScanner — zero alloc trong OnGUI
- **Không ảnh hưởng build:** `[Tweak]` attribute là empty PropertyAttribute, không thêm logic runtime. Scanner nằm trong Editor assembly
- **Default value capture:** Snapshot giá trị tại `EditorApplication.playModeStateChanged` (entering Play) → cho phép Reset
