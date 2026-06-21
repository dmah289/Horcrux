# SKILL.md — Horcrux SDK Design Guide

Tài liệu hướng dẫn thiết kế hệ thống cho `Assets/Horcrux`.

## Assembly Structure

Dự án dùng assembly definitions (`.asmdef`) để kiểm soát dependency:

- **`com.horcrux.runtime`** (`Assets/Horcrux/Runtime/`) — Core SDK runtime. Namespace: `Horcrux.Runtime`. References: Init(args), Addressables, UniTask.
- **`com.horcrux.editor`** (`Assets/Horcrux/Editor/`) — Editor-only tools. Namespace: `Horcrux.Editor`. Không có runtime dependencies.

## Namespaces

- `Horcrux.Runtime.*` — SDK runtime (Abstractions, Implementations, Utilities)
- `Horcrux.Editor.*` — SDK editor tools

## Quy tắc chung

### 1. Giảm tính toán lặp

Cache kết quả thay vì tính lại mỗi frame hoặc mỗi lần gọi — giảm áp lực GPU/CPU.

- **Nên:** Lưu kết quả vào field, chỉ tính lại khi dữ liệu nguồn thay đổi (dirty flag, event-driven).
- **Không nên:** Gọi lại `GetComponent`, filter/sort list, format string,... mỗi `Update()` / `OnGUI()` khi input không đổi.

### 2. OOP & SOLID

Đảm bảo khả năng mở rộng chức năng mới dễ dàng về sau.

- **S** — Mỗi class/struct chỉ đảm nhận một trách nhiệm rõ ràng.
- **O** — Mở rộng bằng kế thừa hoặc composition, không sửa trực tiếp class đang hoạt động ổn định.
- **L** — Subclass phải thay thế được base class mà không gây side-effect.
- **I** — Interface nhỏ, chuyên biệt — không ép implement method không dùng đến.
- **D** — Phụ thuộc vào abstraction (interface), không phụ thuộc trực tiếp vào implementation cụ thể.

### 3. Tối ưu cấp phát (GC-friendly)

Hạn chế allocation trên heap để giảm áp lực Garbage Collector, mang lại trải nghiệm mượt mà.

- Ưu tiên `struct` cho data nhỏ, ngắn hạn (event DTO, config, result).
- **Pre-alloc capacity:** Collection khởi tạo với capacity dự đoán trước — giảm resize.
- **Duyệt ngược khi xóa từ list:** `RemoveAt` cuối list là O(1) thay vì O(n).
- Tránh tạo object mới trong hot path (`Update`, `OnGUI`, vòng lặp mỗi frame).
- Dùng pool / cache / `static readonly` thay vì `new` lặp lại.
- Tránh LINQ, boxing, string concat (`$""`, `+`) trong hot path — dùng `StringBuilder` hoặc cache sẵn.
- **Addressables handle tracking:** Lưu lại `AsyncOperationHandle` để `Release()` đúng lúc — tránh memory leak.
- **sqrMagnitude thay magnitude:** Tránh sqrt trong range check.
- **Try/catch per callback:** Trong hệ thống event/callback, 1 listener lỗi không kill các listener khác.

### 4. Không monolithic

Tách hệ thống thành các module nhỏ, rõ ràng trách nhiệm — dễ test, dễ thay thế, dễ mở rộng.

- Mỗi hệ thống là một thư mục riêng biệt với ranh giới rõ ràng.
- Giao tiếp giữa các module qua interface hoặc EventBus, không tham chiếu trực tiếp implementation.

---

## Editor (`Assets/Horcrux/Editor/`)

### Quy tắc thiết kế Editor tool

| # | Quy tắc | Chi tiết |
|---|---------|----------|
| 1 | **Một thư mục = một tool** | Gói gọn tất cả file (window, drawer, data, utility) trong cùng một thư mục. |
| 2 | **Dùng chung qua `Common/`** | Các biến/chức năng dùng chung (màu sắc, GUIContent, GUIStyle, layout options) đặt trong `Common/`. Nếu phát hiện logic có thể tái sử dụng cho tool khác → thêm vào `Common/`. |
| 3 | **Giảm tính toán lặp trong `OnGUI`** | Cache mọi thứ có thể: GUIContent, GUIStyle, kết quả filter/sort, string format. Chỉ tính lại khi dữ liệu thực sự thay đổi, không tính lại mỗi lần `OnGUI` được gọi. |
| 4 | **Hạn chế allocation** | Tránh tạo object mới trong `OnGUI` (new GUIContent, new GUIStyle, string concat, LINQ). Dùng static/readonly fields hoặc cache ở cấp instance. |
| 5 | **File `.md` cho mỗi tool** | Mỗi thư mục tool có một file `.md` mô tả thiết kế hệ thống. Trình bày bằng cây thư mục hoặc bảng cho trực quan. Phải đầy đủ, chính xác — **mỗi khi thay đổi hệ thống phải cập nhật file `.md` tương ứng**. |

### Kỹ thuật tối ưu Editor

**Caching theo cấp độ:**

| Cấp | Kỹ thuật | Khi nào dùng |
|-----|----------|-------------|
| **Static eager** | `static readonly` | Giá trị bất biến: Color, GUILayoutOption[], text-only GUIContent |
| **Static lazy + guard** | Init 1 lần với `Ensure()` guard | Cần `GUI.skin` hoặc `EditorGUIUtility` mới có (GUIStyle, icon GUIContent) |
| **Instance lazy** | Null-check init ở cấp window | Style riêng cho từng tool, không cần chia sẻ |
| **Dirty-flag cache** | Chỉ rebuild khi dữ liệu thay đổi | Layout options khi window resize |
| **Event-phase cache** | Tính toán nặng chỉ ở `EventType.Layout` | Data transform (filter, sort, format) — Repaint dùng lại kết quả |

**Các kỹ thuật bổ sung:**
- **Dynamic GUIContent:** Tạo 1 instance duy nhất, chỉ cập nhật `.text` khi giá trị thực sự thay đổi.
- **Reuse collections:** Dùng `.Clear()` trên collection có sẵn thay vì tạo mới mỗi frame.
- **Deferred removal:** Không xóa phần tử trong list đang iterate — đánh dấu rồi xử lý sau vòng lặp.
- **Reusable GUIContent cho CalcHeight:** 1 instance dùng chung để đo chiều cao text, chỉ cập nhật `.text`.

---

## Runtime (`Assets/Horcrux/Runtime/`)

### Tư tưởng thiết kế

Luôn cố gắng **decouple** và giúp developer **dễ tích hợp** nhất có thể. Áp dụng SOLID bằng cách sử dụng package **InitArgs** để đăng ký service, inject dependency qua constructor-style — tránh phụ thuộc trực tiếp giữa các implementation. Ưu tiên dùng **UniTask** cho mọi async operation để tránh allocation từ coroutine/Task, giảm áp lực GC. Sử dụng **Addressables** để load/unload asset theo nhu cầu, giảm áp lực lên RAM — ưu tiên `AssetReference` thay vì string key để đảm bảo type-safe và tránh lỗi runtime.

### Cấu trúc thư mục

Runtime chia thành 3 nhánh chính: **Abstractions**, **Implementations**, **Utilities**.

Mỗi hệ thống trừu tượng trong `Abstractions/` có **một hệ thống triển khai tương ứng** trong `Implementations/` (cùng tên thư mục).

Trong cả `Abstractions/` và `Implementations/` có 2 phân loại:

| Phân loại | Ý nghĩa |
|-----------|---------|
| **Foundations** | Hệ thống hoạt động **độc lập**, có thể mang sang dự án khác mà không phụ thuộc hệ thống nào trong SDK. |
| **Composites** | Hệ thống **kết hợp từ 2+ Foundations** — phụ thuộc vào các Foundation khác để hoạt động. |

### Quy tắc thiết kế Runtime

| # | Quy tắc | Chi tiết |
|---|---------|----------|
| 1 | **Abstractions ↔ Implementations** | Mỗi hệ thống có folder cùng tên ở cả 2 nhánh. Abstractions chứa interface/abstract, Implementations chứa triển khai cụ thể. |
| 2 | **Foundations vs Composites** | Foundation = độc lập, portable. Composite = kết hợp 2+ Foundations. Phân loại đúng để đảm bảo tính tái sử dụng. |
| 3 | **Decouple qua InitArgs** | Service đăng ký bằng `[Service(typeof(T), FindFromScene = true)]`. Consumer nhận dependency qua `MonoBehaviour<TDep>` + `Init(TDep)`. Không `new` implementation trực tiếp. |
| 4 | **File `.md` cho mỗi hệ thống** | Đặt trong folder `Implementations/` của hệ thống tương ứng. Mô tả thiết kế, use case, luồng hoạt động — trình bày bằng cây thư mục hoặc bảng cho trực quan. **Mỗi khi thay đổi hệ thống phải cập nhật file `.md` tương ứng.** |
| 5 | **Utilities = static & universal** | Extension methods hoặc hệ thống static mà dự án nào cũng cần. Không phụ thuộc vào bất kỳ Foundation/Composite nào. |

### Kỹ thuật tối ưu Runtime

- **CancellationToken:** Truyền qua async chain để hủy an toàn khi MonoBehaviour bị destroy.
- **Reuse tài nguyên nặng:** Cache RenderTexture, Texture2D,... qua nhiều lần sử dụng — dọn dẹp trong `OnDestroy()`.
