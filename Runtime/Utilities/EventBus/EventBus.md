# EventBus — publish/subscribe theo type

Gửi event tới listener **tức thì**, theo **thứ tự đăng ký**, không cấp phát heap khi publish.

Mỗi kiểu event là một bus độc lập ở tầng type: `EventBus<ToastRequest>` và `EventBus<PlayerDied>` không
dùng chung state, không ảnh hưởng nhau ở bất kỳ điều gì tài liệu này nói.

Danh sách listener sửa được **ngay giữa lúc đang dispatch** — đăng ký thêm, huỷ chính mình, huỷ người
khác — không ai bị bỏ sót, không ai bị gọi hai lần.

---

## §1. Bề mặt API

| Thành phần | Chữ ký | Vai trò |
|---|---|---|
| `IEvent` | `interface IEvent { }` | Marker. Mọi event DTO implement nó |
| `EventBus<T>.Subscribe` | `static Subscription<T> Subscribe(Action<T> callback)` | Đăng ký. Trả handle để huỷ |
| `EventBus<T>.Publish` | `static void Publish(in T e = default)` | Bắn event, gọi hết listener ngay trong lời gọi này |
| `EventBus<T>.ActiveListenerCount` | `static int` | Số listener đang hoạt động thật |
| `Subscription<T>.Dispose` | `void Dispose()` | Huỷ đăng ký. **Đường huỷ duy nhất** |

`T` bị ràng `where T : struct, IEvent`.

| File | Nội dung |
|---|---|
| `Utilities/EventBus/EventBus.cs` | `IEvent`, `EventBus<T>` |
| `Utilities/EventBus/Subscription.cs` | `Subscription<T>` |
| `Utilities/Common/DeferredSet.cs` | Storage nội bộ. Utility dùng chung, không thuộc bề mặt bus |

---

## §2. Use case — chọn cách nghe

| Nhu cầu | Cách làm | Giữ handle? |
|---|---|---|
| **Nghe suốt đời object** — thường gặp nhất | `Subscribe` ở `Awake` | **Không.** Entry tự rụng khi object bị destroy |
| **Nghe theo bật/tắt** — object pool, popup | `Subscribe` ở `OnEnable`, `Dispose` ở `OnDisable` | **Có** |
| **Nghe một lần rồi thôi** | `Dispose` handle ngay trong callback | **Có** |
| **Lambda có capture** | `Dispose` khi hết cần | **Có, bắt buộc** |
| **Listener là method `static`** | `Dispose` khi hết cần | **Có, bắt buộc** |

**Cái bẫy duy nhất đáng nhớ:** entry tự rụng khi owner bị **destroy**, không rụng khi owner bị
**disable**. `Subscribe` ở `OnEnable` mà quên `Dispose` ở `OnDisable` cho hai hậu quả cùng lúc — object
đang tắt vẫn nhận event, và lần bật lại bị chặn vì trùng đăng ký.

Ba loại listener **không** tự rụng: lambda có capture, method `static`, và method của object không phải
`UnityEngine.Object`. Cả ba đều buộc giữ handle.

Dòng `LogError` về trùng đăng ký trong Editor là tín hiệu call site đã rơi vào bẫy trên. Sửa call site,
đừng tắt log.

---

## §3. Event DTO

| Luật | Lý do |
|---|---|
| **`readonly struct`**, implement `IEvent` | Listener nhận một bản copy. Field mutable tạo ảo giác sửa được payload cho listener sau — thực tế không, và sai im lặng |
| Không dùng class | Constraint chặn ở compile time. Đây là chỗ giữ cho publish không alloc: call site **không thể** `new` một event object mỗi lần bắn |
| Chịu được payload rỗng | `Publish()` không tham số là hợp lệ và gửi struct zero-init: số về 0, field kiểu ref về `null` |

---

## §4. Bảo đảm — dựa vào được

| Hành vi | Chi tiết |
|---|---|
| **Thứ tự dispatch** | Đúng thứ tự `Subscribe`. Không có cơ chế ưu tiên |
| **Dispatch đồng bộ** | `Publish` trả về khi listener cuối đã chạy xong. Không hàng đợi, không hoãn sang frame sau |
| **Listener đăng ký giữa lúc publish** | **Không** nhận event lần đó. Nhận từ lần `Publish` kế tiếp |
| **Huỷ chính mình trong callback** | Được. Các listener sau vẫn nhận đủ event lần đó |
| **Huỷ người khác trong callback** | Được. Người đã chạy thì đã chạy; người chưa tới lượt sẽ bị bỏ qua ngay lần đó |
| **Listener ném exception** | Bị log, **không** thoát ra `Publish`. Các listener khác vẫn chạy đủ |
| **Owner bị destroy** | Entry rụng ở lần `Publish` kế. Callback **không** bị gọi lần nào, không exception nào |
| **Owner còn sống** | Luôn nhận event, kể cả khi cùng bus có entry khác đang bị dọn |
| **Đăng ký trùng** | Lần thứ hai bị **bỏ qua**, không cộng dồn. Callback vẫn chạy đúng một lần mỗi `Publish` |
| **Handle rỗng** | `Subscribe` trả handle rỗng khi callback `null` hoặc trùng. `Dispose` handle rỗng không làm gì — **không** huỷ oan bản đã đăng ký |
| **`ActiveListenerCount`** | Số thật, đã trừ entry chờ dọn |

---

## §5. Giới hạn

| Giới hạn | Hệ quả |
|---|---|
| **Main-thread only** | Không lock. `Publish` hoặc `Subscribe` từ thread khác làm hỏng danh sách listener |
| **Tự rụng chỉ phủ destroy** | Không phủ disable, không phủ lambda có capture, không phủ method `static` |
| **Dọn chỉ chạy khi có `Publish`** | Event không bao giờ bắn nữa thì entry chết còn nằm lại. Vô hại ở cỡ UI, nhưng **không** được nói "không bao giờ leak" |
| **Danh sách phình khi không ai publish** | Mỗi cặp `Subscribe`/`Dispose` để lại một ô chờ dọn, và việc dọn cũng chỉ xảy ra trong `Publish`. Bus theo nhịp bật/tắt liên tục mà **hiếm** bắn event thì danh sách nội bộ phình đơn điệu: `ActiveListenerCount` vẫn đúng, nhưng mỗi `Subscribe` phải quét qua toàn bộ ô chết. Một lần `Publish` là dọn sạch |
| **Không chặn đệ quy vô hạn** | Listener publish lại chính kiểu nó vô điều kiện thì `StackOverflowException`. Xem §6 |
| **Không có bus scoped** | Không có bản per-scene hay per-match. Bus sống theo domain |

---

## §6. Publish lồng nhau

Listener publish thêm event **ngay trong callback** là hợp lệ, không sót không lặp. Lồng **khác kiểu**
thì hoàn toàn không có tương tác gì. Lồng **cùng kiểu** cũng an toàn, nhưng là chỗ duy nhất cần biết
mình đang làm gì.

Lồng cùng kiểu thường không do ai cố ý viết ra:

| Dạng | Ví dụ tình huống |
|---|---|
| Cascade trong state machine | listener của một trạng thái lại đẩy sang trạng thái khác cùng kiểu event |
| **Lồng gián tiếp qua chuỗi gọi** | listener gọi một service, service gọi service khác, mắt cuối publish lại chính kiểu đó. Không chỗ nào trông giống publish-trong-publish |
| Normalize / self-healing | listener chuẩn hoá dữ liệu rồi bắn lại cùng event |
| Đường lỗi | listener xử lý lỗi rồi bắn lại cùng event với payload lỗi |

Ranh giới: **lồng hữu hạn là bình thường, bus chịu được. Lồng vô hạn là lỗi của call site và bus cố ý
không đỡ.** Điều kiện dừng thuộc trách nhiệm call site — thường là huỷ đăng ký của chính mình, hoặc một
cờ trạng thái, trước khi publish lồng.

---

## §7. Bài học từ kiểm chứng

Những điều dưới đây đã được kiểm bằng test và chốt lại thành hợp đồng. Ghi ở đây vì chúng là chỗ trực
giác hay đoán sai.

**1 — "Huỷ đăng ký giữa lúc dispatch" là an toàn, không phải mẹo.**
Huỷ chính mình, hoặc huỷ người khác, ngay trong callback đều không làm ai bị bỏ sót. Không cần hoãn việc
huỷ sang cuối frame, không cần cờ `isDispatching` ở call site.

**2 — Nhưng "đăng ký giữa lúc dispatch" thì bị hoãn một nhịp.**
Listener mới không nhận event đang chạy. Đây là hành vi cố định, dựa vào được — không phải điều kiện đua.
Nếu logic cần listener mới xử lý **ngay** event đó, gọi thẳng nó một lần thay vì trông vào bus.

**3 — Tự rụng phủ đúng một ca, và ca đó là ca phổ biến nhất.**
Method của `MonoBehaviour` bị destroy: rụng sạch, không exception. Mọi ca khác — disable, lambda có
capture, method `static` — **không** rụng. Ranh giới này là nguồn leak duy nhất của bus, và nó nằm ở call
site chứ không ở bus.

**4 — Owner còn sống thì luôn nhận event, không có ngoại lệ nào.**
Kể cả khi cùng bus đang có entry chết bị dọn ngay trước hoặc ngay sau nó. Nếu một listener "im lặng
không nhận event", nguyên nhân không nằm ở cơ chế dọn — xem §9.

**5 — Đăng ký trùng bị bỏ qua, không cộng dồn.**
So sánh theo cặp *object chủ* + *method*, nên hai lần `Subscribe` cùng một method của cùng một object là
trùng. Hai lambda viết giống nhau **không** trùng — chúng là hai method khác nhau. Hệ quả cho call site:
đừng dùng `Subscribe` lại như một cách "refresh" đăng ký, nó không có tác dụng gì ngoài một dòng log.

**6 — Handle rỗng an toàn tuyệt đối.**
Không cần kiểm gì trước khi `Dispose`. Và quan trọng hơn: handle rỗng nhận được từ một lần đăng ký trùng
**không** huỷ được bản đã đăng ký trước đó — nên `Dispose` bừa không phá của người khác.

**7 — Exception của listener không cắt được chuỗi dispatch.**
Nó bị log rồi bỏ qua. Đừng thiết kế luồng dựa trên việc "ném exception để chặn các listener sau" — cách
đó không hoạt động. Muốn chặn thì phải là một cờ trong payload hoặc một event khác.

**8 — `Publish()` không tham số luôn hợp lệ.**
Payload là struct zero-init. Nếu event có field kiểu ref thì listener nhận `null` ở field đó — listener
phải chịu được, hoặc event đó không nên cho phép publish rỗng ở call site.

**9 — Entry chết nằm giữa danh sách không ảnh hưởng ai phía sau.**
Thứ tự dispatch của những listener còn sống giữ nguyên, kể cả khi có entry bị dọn ở giữa lượt.

**10 — Thứ tự đăng ký là thứ tự dispatch, và đó là bảo đảm duy nhất về thứ tự.**
Không có ưu tiên. Hai listener mà kết quả phụ thuộc thứ tự giữa chúng là một thiết kế dễ vỡ — vì thứ tự
đó phụ thuộc `Awake`/`OnEnable` chạy trước, tức là phụ thuộc thứ tự component trong scene.

---

## §8. Quyết định thiết kế

### 8.1 Trông như thiếu sót — cố ý như vậy

| Trông như | Thực ra |
|---|---|
| Không có `Clear()` để reset bus | Bỏ đi được một cái bẫy: gọi giữa lúc dispatch sẽ làm vòng dispatch đang chạy đọc ra ngoài biên. Static state tự reset mỗi lần vào Play Mode nhờ domain reload |
| Không cap độ sâu đệ quy | `StackOverflowException` là triệu chứng dễ lần hơn một cap im lặng chặn cascade hợp lệ (§6) |
| Không có `IEventBus<T>` để inject | Không có implementation thứ hai. Generic đã giữ được Open/Closed và Liskov ở tầng type |
| Không có facade cho phép suy luận `T` từ tham số | Sẽ thành hai đường làm cùng một việc, và tạo một tên trùng với namespace. Giữ một đường `EventBus<T>.Publish` |
| Dọn danh sách chỉ xảy ra trong `Publish`, không xảy ra lúc `Dispose` | Việc dọn dồn lại chỉ số, nên chỉ an toàn khi không có vòng dispatch nào đang chạy. `Publish` là chỗ duy nhất biết chắc điều đó. Đánh đổi là giới hạn thứ tư ở §5 |

### 8.2 Cố ý không làm

| Không làm | Lý do |
|---|---|
| `priority` cho listener | Chưa call site nào cần thứ tự khác thứ tự đăng ký |
| Handler nhận `in T` | Không phải hot path. `Action<T>` là thứ dev đoán đúng ngay |
| Bus scoped per-scene / per-match | Không có nhu cầu |
| Thread-safe | Main-thread only (§5) |
| Deferred / queued dispatch | Bus chỉ dispatch tức thì. Gộp cuối frame là việc của consumer |
| API liệt kê mọi bus đang tồn tại | Không có consumer |
| Base class cho listener | Loại trừ nhau với base class DI của InitArgs — service cần DI thì không kế thừa được. Cơ chế tự rụng phủ đúng ca đó mà không chiếm chỗ kế thừa |

Bốn mục đầu là **thêm được sau** bằng overload mới, không phải sửa chữ ký cũ (§10).

---

## §9. Chẩn đoán

| Triệu chứng | Nguyên nhân thường gặp |
|---|---|
| Listener không nhận event, dù đã `Subscribe` | Đăng ký xảy ra **trong** một callback của cùng bus ⇒ hoãn một nhịp (§4) · hoặc đăng ký bị chặn vì trùng ⇒ có `LogError` trong Console · hoặc owner đã bị destroy |
| Callback chạy hai lần mỗi `Publish` | Hai `Subscribe` từ hai object khác nhau, hoặc hai lambda khác nhau trỏ cùng một hàm. Dup-guard chỉ chặn cùng cặp *object chủ* + *method* |
| `LogError` trùng đăng ký lặp lại mỗi lần bật object | `Subscribe` ở `OnEnable` mà thiếu `Dispose` ở `OnDisable` (§2) |
| Object đã tắt vẫn phản ứng với event | Cùng nguyên nhân trên. Tự rụng không phủ disable |
| Lambda vẫn chạy sau khi object chủ bị destroy | Lambda có capture không tự rụng. Phải giữ handle và `Dispose` (§2) |
| `ActiveListenerCount` khác số `Subscribe` đã gọi | Có lần đăng ký bị chặn vì trùng, hoặc có entry đã bị dọn |
| `StackOverflowException` trong `Publish` | Đệ quy publish cùng kiểu không có điều kiện dừng (§6) |
| Listener khác im lặng không chạy sau khi một listener lỗi | Không phải hành vi của bus — exception được log và bỏ qua, các listener sau vẫn chạy. Tìm nguyên nhân ở chính listener đó |
| Danh sách nội bộ phình dần dù `ActiveListenerCount` đúng | Bus hiếm bắn event, trong khi call site `Subscribe`/`Dispose` liên tục (§5) |

---

## §10. Mở rộng

Cả bốn hướng là **thêm**, không sửa: chữ ký hiện tại không phải đổi.

| Hướng | Chờ tín hiệu nào |
|---|---|
| Thứ tự dispatch tuỳ biến (`priority`) | Xuất hiện hai listener mà thứ tự giữa chúng ảnh hưởng kết quả |
| Handler nhận `in T` | Profiler chỉ ra việc copy payload trong dispatch là chi phí thật |
| Huỷ theo nhóm (một túi gom nhiều handle) | Một class giữ từ ba handle trở lên |
| Reset state tường minh | Tick *Disable Domain Reload*. Lúc đó static không còn tự reset mỗi lần Play, và listener `static` cùng lambda có capture sẽ sống sót qua các lần chạy |

---

## §11. Bảng metrics

| Phép đo | Giá trị | Ghi chú |
|---|---|---|
| Alloc mỗi `Publish` | **0 byte** | Không copy payload lên heap, không boxing, không cấp phát khi dọn |
| Alloc mỗi `Subscribe` | **1 delegate** | Không tránh được khi truyền method group. Handle là struct nên không thêm alloc |
| Alloc trên đường lỗi | string format của log | Chỉ khi trùng đăng ký hoặc listener ném exception. Log trùng đăng ký bị cắt khỏi build |
| Tìm bus của một kiểu event | **0** | Không hash, không lookup — bus là static theo type |
| `Publish` | O(n) | `n` = số entry, tính cả entry chờ dọn |
| `Subscribe` | O(n) | Dup-guard quét cả danh sách |
| `Dispose` | O(n) | Tìm entry rồi đánh dấu |
| Dọn một entry của owner đã destroy | O(1) | Vòng dispatch đã biết vị trí |
| `ActiveListenerCount` | O(1) | — |
| Ngân sách thiết kế | ~10–50 dispatch/giây | UI và luồng game. **Không** phải hot path |
| Code size | một bản native cho **mỗi** kiểu event | Đánh đổi đã nhận của bus static theo type |
