# EventBus System — Phân tích kiến trúc & Tối ưu hóa

## 1. Phân tích tối ưu bộ nhớ: `struct` constraint

### 1.1 Tại sao `where T : struct, IEventDTO` không cấp phát heap?

#### Boxing là gì?

Trong .NET, bộ nhớ được chia thành hai vùng chính: **Stack** và **Heap**. Stack là vùng nhớ nhanh, được quản lý tự động theo kiểu LIFO (vào sau ra trước), không cần Garbage Collector can thiệp. Heap là vùng nhớ lớn hơn nhưng chậm hơn, và mọi object trên heap đều phải được GC quét và thu hồi khi không còn ai tham chiếu.

Khi một **value type (struct)** — vốn sống trên stack — bị ép sang kiểu **interface** hoặc **object**, CLR buộc phải thực hiện thao tác gọi là **boxing**. Cụ thể, CLR sẽ cấp phát một vùng nhớ trên heap (khoảng 16–24 bytes cho header + dữ liệu), rồi copy toàn bộ dữ liệu từ struct trên stack sang vùng heap mới này, và cuối cùng trả về một reference trỏ đến object wrapper đó.

Ví dụ, khi bạn có một struct `PlayerDiedEvent { Score = 100 }` đang nằm trên stack, nếu nó bị cast sang `IEventDTO` mà không có `struct` constraint, CLR sẽ tạo một bản copy của nó trên heap, bọc trong một object wrapper có đầy đủ Object Header và Type Pointer. Mỗi lần boxing xảy ra là một lần cấp phát heap, tích lũy dần sẽ tạo ra GC pressure, và khi GC chạy để dọn dẹp các object này, nó có thể gây ra frame spike — điều cực kỳ nghiêm trọng trong game.

#### So sánh IL (Intermediate Language) sinh ra

Khi C# compiler sinh ra mã IL cho generic method, có sự khác biệt rất lớn giữa hai trường hợp:

**Trường hợp có `struct` constraint** (`where T : struct, IEventDTO`): Compiler sinh ra chuỗi lệnh IL bắt đầu bằng `ldarg.1` để load tham số dto từ stack, tiếp theo là opcode `constrained. T`, rồi mới đến `callvirt Action<T>.Invoke`. Nhờ `constrained.`, JIT compiler biết chắc T là struct nên sẽ tạo ra native code chuyên biệt (specialized), truyền struct trực tiếp by value mà không cần boxing. Toàn bộ quá trình không có bất kỳ cấp phát heap nào.

**Trường hợp không có `struct` constraint** (`where T : IEventDTO`): Compiler sinh ra chuỗi lệnh bắt đầu bằng `ldarg.1`, nhưng ngay sau đó là lệnh `box T` rồi mới đến `callvirt`. Lệnh `box T` chính là thủ phạm gây cấp phát heap. Vì compiler không biết chắc T là value type hay reference type, nó buộc phải chèn lệnh box để đảm bảo tính đúng đắn trong mọi trường hợp. Nếu T thực sự là struct, mỗi lần gọi sẽ phát sinh một allocation trên heap.

#### Cơ chế `constrained.` opcode

Khi compiler thấy `struct` constraint, nó sinh opcode `constrained.` trước interface method call.
Điều này cho phép JIT **resolve trực tiếp** đến implementation của struct mà **không cần boxing**:

```
// IL với struct constraint:
constrained. !!T
callvirt instance void IEventDTO::SomeMethod()
// → JIT gọi trực tiếp SomeMethod trên struct, không box

// IL KHÔNG CÓ struct constraint:
box !!T                                          // ← BOXING! Heap allocation!
callvirt instance void IEventDTO::SomeMethod()
```

Cơ chế `constrained.` hoạt động như sau: khi JIT compiler gặp opcode này, nó biết rằng kiểu T được đảm bảo là struct, nên thay vì tạo vtable lookup thông qua interface (điều đòi hỏi object reference trên heap), JIT sẽ resolve trực tiếp đến method implementation của struct đó. Struct không có vtable riêng như class, nhưng nhờ `constrained.`, JIT có thể tìm đúng method body và gọi trực tiếp (effectively là một static call trên struct data), bỏ qua hoàn toàn nhu cầu boxing.

#### JIT Generic Specialization

CLR xử lý generic specialization theo hai nhóm hoàn toàn khác nhau, và đây là điểm mấu chốt:

Đối với **value types (struct)**, JIT compiler tạo ra **một bản native code riêng biệt** cho mỗi kiểu struct cụ thể. Ví dụ: `EventBus<PlayerDiedEvent>` sẽ có native code #1, `EventBus<ScoreChangedEvent>` sẽ có native code #2, `EventBus<LevelCompleteEvent>` sẽ có native code #3. Lý do là mỗi struct có kích thước khác nhau (PlayerDiedEvent có thể 4 bytes, ScoreChangedEvent có thể 12 bytes), nên JIT phải tạo code riêng để biết chính xác bao nhiêu byte cần copy, offset nào để truy cập field, dùng register nào để truyền tham số. Kết quả là code được tối ưu hoàn toàn cho từng struct type, không có overhead nào.

Đối với **reference types (class)**, JIT compiler **dùng chung một bản native code** cho tất cả. `EventBus<SomeClassA>` và `EventBus<SomeClassB>` dùng cùng một đoạn native code. Điều này vì mọi reference type đều có cùng kích thước trên stack: 8 bytes (một con trỏ trên hệ thống 64-bit). JIT không cần phân biệt chúng ở mức native code.

Khi **không có `struct` constraint**, JIT không biết trước T là value hay reference type tại thời điểm biên dịch generic method body. Do đó, nó phải compile code theo kiểu phòng hờ — thường là sử dụng boxing path để đảm bảo hoạt động đúng với cả hai trường hợp. Đây chính là lý do gây ra cấp phát heap không mong muốn.

#### `default(T)` — Sự khác biệt tinh tế

Trong `EventBus<T>.Raise(T eventDTO = default)`:

| Constraint | `default(T)` | Hệ quả |
|---|---|---|
| `where T : struct` | Zero-initialized struct | ✅ Luôn hợp lệ, không null, không box |
| `where T : IEventDTO` | `null` nếu T là class | ⚠️ Tiềm ẩn NullReferenceException |

Khi có `struct` constraint, `default(T)` luôn trả về một struct với tất cả field được gán giá trị mặc định (0 cho số, false cho bool, null cho reference field bên trong). Struct này hoàn toàn hợp lệ, có thể sử dụng ngay, và quan trọng nhất là nó nằm trên stack, không phát sinh boxing.

Ngược lại, khi không có `struct` constraint, nếu T thực sự là một class, `default(T)` sẽ là `null`. Điều này có nghĩa là bất kỳ ai gọi `EventBus<T>.Raise()` mà không truyền tham số sẽ nhận được `null` — và nếu bất kỳ listener nào cố gắng truy cập thuộc tính trên event object đó, NullReferenceException sẽ xảy ra ngay lập tức.

### 1.2 Tổng kết `struct` constraint

```
where T : struct, IEventDTO
    ✅ JIT tạo specialized native code riêng cho mỗi struct type
    ✅ constrained. opcode → interface dispatch không boxing
    ✅ default(T) luôn valid, không null
    ✅ Không thể vô tình dùng class type (compile-time safety)
    ✅ Toàn bộ data flow qua stack / CPU registers
    ✅ Zero heap allocation per Raise()

where T : IEventDTO (no struct)
    ❌ JIT dùng shared code path cho reference types
    ❌ Interface dispatch có thể box struct thành heap object
    ❌ default(T) có thể là null → bug tiềm ẩn
    ❌ Cho phép class type → heap allocation từ new()
    ❌ Boxing = thêm GC object mỗi lần Raise()
```

---

## 2. Đánh giá thiết kế hiện tại

### 2.1 Điểm mạnh

| Đặc điểm | Phân tích |
|---|---|
| **Zero heap allocation** | `struct` DTO + `struct` constraint → toàn bộ data flow qua stack |
| **Priority-based dispatch** | Listener có `priority` thấp hơn được gọi trước → kiểm soát thứ tự |
| **Exception isolation** | `try/catch` per callback → 1 listener crash không ảnh hưởng listener khác |
| **Đơn giản, ít boilerplate** | Static generic pattern → không cần DI, không cần instance |
| **Type-safe** | Mỗi event type có bus riêng → compile-time checking |
