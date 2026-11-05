# EventBus — publish/subscribe theo type

Code: `Runtime/Utilities/EventBus/` · namespace `Horcrux.Runtime.Utilities.EventBus`

Gửi event tới listener **tức thì**, theo **thứ tự đăng ký**, không cấp phát heap khi publish. Danh sách listener sửa được
ngay giữa lúc đang dispatch mà không ai bị bỏ sót hay bị gọi hai lần. Mỗi kiểu event là một bus độc lập:
`EventBus<ToastRequest>` và `EventBus<PlayerDied>` không dùng chung state.

**Không cần wire:** không scene, không asset, không bước boot. Bus là static theo type, dùng được ngay từ code.

---

## §1. Bề mặt API

| Thành phần | Chữ ký | Vai trò |
|---|---|---|
| `IEvent` | `interface IEvent { }` | Marker. Mọi event DTO implement nó |
| `EventBus<T>.Subscribe` | `static Subscription<T> Subscribe(Action<T> callback)` | Đăng ký, trả handle để huỷ |
| `EventBus<T>.Publish` | `static void Publish(in T e = default)` | Bắn event, gọi hết listener ngay trong lời gọi này |
| `EventBus<T>.ActiveListenerCount` | `static int` | Số listener sống |
| `Subscription<T>.Dispose` | `void Dispose()` | Huỷ đăng ký. **Đường huỷ duy nhất** |

`T` bị ràng `where T : struct, IEvent`.

| File | Nội dung |
|---|---|
| `Utilities/EventBus/EventBus.cs` | `IEvent`, `EventBus<T>` |
| `Utilities/EventBus/Subscription.cs` | `Subscription<T>` |
| `Utilities/Common/DeferredSet.cs` | Storage nội bộ. Utility dùng chung, không thuộc bề mặt bus |

---

## §2. Event DTO

| Luật | Lý do |
|---|---|
| **`readonly struct`**, implement `IEvent` | Listener nhận một bản copy. Field mutable tạo ảo giác sửa được payload cho listener sau — thực tế không, và sai im lặng |
| Không dùng class | Constraint chặn ở compile time. Đây là chỗ giữ cho publish không alloc: call site **không thể** `new` một event object mỗi lần bắn |
| Chịu được payload rỗng | `Publish()` luôn hợp lệ và gửi struct zero-init: số về 0, field kiểu ref về `null`. Listener không chịu được `null` thì call site không publish rỗng event đó |

---

## §3. Cách nghe

| Nhu cầu | Cách làm | Giữ handle? |
|---|---|---|
| **Nghe suốt đời object** — thường gặp nhất | `Subscribe` ở `Awake` | **Không.** Entry tự rụng khi object bị destroy |
| **Nghe theo bật/tắt** — object pool, popup | `Subscribe` ở `OnEnable`, `Dispose` ở `OnDisable` | **Có** |
| **Nghe một lần rồi thôi** | `Dispose` handle ngay trong callback | **Có** |
| Lambda · method `static` · method của object không phải `UnityEngine.Object` | `Dispose` khi hết cần | **Có, bắt buộc** |

```csharp
public readonly struct PieceMatched : IEvent
{
    public readonly int Count;
    public PieceMatched(int count) => Count = count;
}

EventBus<PieceMatched>.Publish(new PieceMatched(3));

// Nghe suốt đời object
public sealed class ScoreLabel : MonoBehaviour
{
    private void Awake() => EventBus<PieceMatched>.Subscribe(OnMatched);
    private void OnMatched(PieceMatched e) { }
}

// Nghe theo bật/tắt
public sealed class ComboPopup : MonoBehaviour
{
    private Subscription<PieceMatched> subscription;
    private void OnEnable() => subscription = EventBus<PieceMatched>.Subscribe(OnMatched);
    private void OnDisable() => subscription.Dispose();
    private void OnMatched(PieceMatched e) { }
}

// Nghe một lần
public sealed class FirstMatchHint : MonoBehaviour
{
    private Subscription<PieceMatched> subscription;
    private void Awake() => subscription = EventBus<PieceMatched>.Subscribe(OnFirstMatch);
    private void OnFirstMatch(PieceMatched e) => subscription.Dispose();
}
```

**Tự rụng** khi `callback.Target` là `UnityEngine.Object` đã destroy — đúng một ca, và là ca phổ biến nhất. Disable, method
`static`, object thường **không** rụng; lambda rụng hay không tuỳ cách compiler sinh code, nên call site coi mọi lambda là
phải `Dispose`. Ranh giới này là nguồn leak duy nhất của bus, và nó nằm ở call site.

**Bẫy đáng nhớ nhất:** `Subscribe` ở `OnEnable` mà quên `Dispose` ở `OnDisable` — object đang tắt vẫn nhận event, và lần
bật lại bị chặn vì trùng đăng ký. Dòng `LogError` trong Editor là tín hiệu của đúng lỗi này; sửa call site, đừng tắt log.

---

## §4. Một lượt Publish

```
Publish(e)
  depth++
  n = số ô lúc vào                          ô thêm giữa lượt nằm sau n ─► chờ Publish kế
  mỗi ô 0…n-1, theo thứ tự Subscribe:
     ô rỗng (đã Dispose)                    ─► bỏ qua
     owner UnityEngine.Object đã destroy    ─► đánh dấu ô rỗng, bỏ qua
     còn lại                                ─► callback(e); exception ─► LogException, đi tiếp
  depth--
  depth == 0                                ─► dồn danh sách, bỏ ô rỗng
```

| Bảo đảm | Nội dung | Hệ quả cho call site |
|---|---|---|
| Thứ tự | Đúng thứ tự `Subscribe`, không ưu tiên. Là bảo đảm duy nhất về thứ tự | Hai listener mà kết quả phụ thuộc thứ tự giữa chúng là thiết kế dễ vỡ: thứ tự đó theo `Awake`/`OnEnable`, tức thứ tự component trong scene |
| Đồng bộ | `Publish` trả về sau khi listener cuối chạy xong. Không hàng đợi, không hoãn frame | — |
| Huỷ giữa lượt | Huỷ chính mình hay huỷ người khác đều hợp lệ. Listener chưa tới lượt bị bỏ qua ngay lượt đó; đã chạy rồi thì vẫn đã chạy | Không cần hoãn việc huỷ sang cuối frame, không cần cờ `isDispatching` |
| Đăng ký giữa lượt | Listener mới chờ `Publish` kế. Hành vi cố định, không phải điều kiện đua | Cần listener mới xử lý **ngay** event đó thì gọi thẳng nó một lần |
| Exception của listener | Bị log, không thoát ra `Publish`. Các listener khác chạy đủ | Ném exception không chặn được listener sau; muốn chặn thì dùng cờ trong payload hoặc một event khác |
| Owner bị destroy | Rụng ở `Publish` kế. Callback không bị gọi lần nào, không exception nào | — |
| Owner còn sống | Luôn nhận event, kể cả khi ô ngay trước hay ngay sau bị dọn giữa lượt; thứ tự những listener còn sống giữ nguyên | Listener "im lặng không nhận event" thì nguyên nhân không nằm ở cơ chế dọn (§8) |
| Đăng ký trùng | So theo cặp *object chủ* + *method*. Bị bỏ qua, `LogError` (chỉ Editor), trả handle rỗng; callback vẫn chạy đúng một lần mỗi `Publish`. Hai lambda viết giống nhau **không** trùng | Đừng `Subscribe` lại để "refresh" đăng ký — không có tác dụng gì ngoài một dòng log |
| Handle rỗng | Từ đăng ký trùng hoặc `Subscribe(null)`. `Dispose` không làm gì, không huỷ bản đã đăng ký | Không cần kiểm gì trước khi `Dispose`; `Dispose` bừa không phá của người khác |
| `ActiveListenerCount` | Số listener sống, đã trừ ô chờ dọn | — |

---

## §5. Giới hạn

| Giới hạn | Hệ quả |
|---|---|
| **Main-thread only** | Không lock. `Publish` hoặc `Subscribe` từ thread khác làm hỏng danh sách listener |
| **Dọn danh sách chỉ xảy ra trong `Publish`** | Bus **hiếm** bắn event mà call site `Subscribe`/`Dispose` liên tục thì danh sách nội bộ phình đơn điệu: `ActiveListenerCount` vẫn đúng, nhưng mỗi `Subscribe` phải quét qua cả ô chết. Một lần `Publish` là dọn sạch. Event không bao giờ bắn nữa thì ô chết nằm lại — **không** được nói "không bao giờ leak" |
| **Không chặn đệ quy vô hạn** | `StackOverflowException` (§6) |

---

## §6. Publish lồng nhau

Listener publish thêm event ngay trong callback là hợp lệ, không sót không lặp. Lồng **khác kiểu** thì không có tương tác
gì. Lồng **cùng kiểu** cũng an toàn, nhưng là chỗ duy nhất cần biết mình đang làm gì — và nó thường không do ai cố ý viết ra:

| Dạng | Ví dụ tình huống |
|---|---|
| Cascade trong state machine | listener của một trạng thái lại đẩy sang trạng thái khác cùng kiểu event |
| **Lồng gián tiếp qua chuỗi gọi** | listener gọi một service, service gọi service khác, mắt cuối publish lại chính kiểu đó. Không chỗ nào trông giống publish-trong-publish |
| Normalize / self-healing | listener chuẩn hoá dữ liệu rồi bắn lại cùng event |
| Đường lỗi | listener xử lý lỗi rồi bắn lại cùng event với payload lỗi |

**Lồng hữu hạn là bình thường, bus chịu được. Lồng vô hạn là lỗi của call site và bus cố ý không đỡ.** Điều kiện dừng là
trách nhiệm của call site — thường là huỷ đăng ký của chính mình, hoặc một cờ trạng thái, trước khi publish lồng.

---

## §7. Cố ý không có

Bốn mục đầu thêm lại được bằng overload mới, không phải sửa chữ ký cũ.

| Không có | Lý do | Thêm lại khi |
|---|---|---|
| `priority` cho listener | Chưa call site nào cần thứ tự khác thứ tự đăng ký | Xuất hiện hai listener mà thứ tự giữa chúng ảnh hưởng kết quả |
| Handler nhận `in T` | Không phải hot path. `Action<T>` là thứ dev đoán đúng ngay | Profiler chỉ ra việc copy payload trong dispatch là chi phí thật |
| Huỷ theo nhóm — một túi gom nhiều handle | Chưa class nào giữ nhiều handle | Một class giữ từ ba handle trở lên |
| `Clear()` / reset state tường minh | Domain reload tự reset static mỗi lần vào Play Mode. Và `Clear()` gọi giữa lúc dispatch sẽ làm vòng đang chạy đọc ra ngoài biên | Tick *Disable Domain Reload* — lúc đó listener `static` và lambda sống sót qua các lần chạy |
| Cap độ sâu đệ quy | `StackOverflowException` là triệu chứng dễ lần hơn một cap im lặng chặn cascade hợp lệ (§6) | — |
| `IEventBus<T>` để inject | Không có implementation thứ hai. Generic đã giữ Open/Closed và Liskov ở tầng type | — |
| Facade cho phép suy luận `T` từ tham số | Thành hai đường làm cùng một việc, và tạo một tên trùng với namespace | — |
| Bus scoped per-scene / per-match | Không có nhu cầu | — |
| Thread-safe | Main-thread only (§5) | — |
| Deferred / queued dispatch | Bus chỉ dispatch tức thì. Gộp cuối frame là việc của consumer | — |
| API liệt kê mọi bus đang tồn tại | Không có consumer | — |
| Base class cho listener | Loại trừ nhau với base class DI của InitArgs — service cần DI thì không kế thừa được. Cơ chế tự rụng phủ đúng ca đó mà không chiếm chỗ kế thừa | — |
| Dọn danh sách lúc `Dispose` | Việc dọn dồn lại chỉ số, nên chỉ an toàn khi không có vòng dispatch nào đang chạy — `Publish` là chỗ duy nhất biết chắc điều đó. Đánh đổi ở §5 | — |

---

## §8. Chẩn đoán

| Triệu chứng | Nguyên nhân thường gặp |
|---|---|
| Listener không nhận event dù đã `Subscribe` | Đăng ký xảy ra **trong** một callback của cùng bus ⇒ hoãn một nhịp (§4) · hoặc bị chặn vì trùng ⇒ có `LogError` trong Console · hoặc owner đã bị destroy |
| Callback chạy hai lần mỗi `Publish` | Hai `Subscribe` từ hai object khác nhau, hoặc hai lambda khác nhau trỏ cùng một hàm. Dup-guard chỉ chặn cùng cặp *object chủ* + *method* |
| `LogError` trùng đăng ký lặp lại mỗi lần bật object | `Subscribe` ở `OnEnable` mà thiếu `Dispose` ở `OnDisable` (§3) |
| Object đã tắt vẫn phản ứng với event | Cùng nguyên nhân trên — tự rụng không phủ disable |
| Lambda vẫn chạy sau khi object chủ bị destroy | Lambda không chắc tự rụng, phải giữ handle (§3) |
| `ActiveListenerCount` khác số `Subscribe` đã gọi | Có lần đăng ký bị chặn vì trùng, hoặc có ô đã bị dọn |
| `StackOverflowException` trong `Publish` | Đệ quy publish cùng kiểu không có điều kiện dừng (§6) |
| Listener khác im lặng không chạy sau khi một listener lỗi | Không phải hành vi của bus — exception được log và bỏ qua. Tìm nguyên nhân ở chính listener đó |
| Danh sách nội bộ phình dần dù `ActiveListenerCount` đúng | Bus hiếm bắn event trong khi call site `Subscribe`/`Dispose` liên tục (§5) |

---

## §9. Bảng metrics

| Phép đo | Giá trị | Ghi chú |
|---|---|---|
| Alloc mỗi `Publish` | **0 byte** | Không copy payload lên heap, không boxing, không cấp phát khi dọn |
| Alloc mỗi `Subscribe` | **1 delegate** | Không tránh được khi truyền method group. Handle là struct nên không thêm alloc |
| Alloc trên đường lỗi | string format của log | Chỉ khi trùng đăng ký hoặc listener ném exception. Log trùng đăng ký bị cắt khỏi build |
| Tìm bus của một kiểu event | **0** | Không hash, không lookup — bus là static theo type |
| `Publish` | O(n) | `n` = số ô, tính cả ô chờ dọn |
| `Subscribe` | O(n) | Dup-guard quét cả danh sách |
| `Dispose` | O(n) | Tìm ô rồi đánh dấu |
| Dọn một ô của owner đã destroy | O(1) | Vòng dispatch đã biết vị trí |
| `ActiveListenerCount` | O(1) | — |
| Ngân sách thiết kế | ~10–50 dispatch/giây | UI và luồng game. **Không** phải hot path |
| Code size | một bản native cho **mỗi** kiểu event | Đánh đổi đã nhận của bus static theo type |
