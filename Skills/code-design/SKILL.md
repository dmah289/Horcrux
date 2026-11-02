---
name: code-design
description: Dùng khi viết hoặc sửa code runtime: SOLID, bậc cấu trúc, giới hạn của composite, module và phân tầng phụ thuộc, hiệu năng bộ nhớ và thời gian, bất biến và guard, naming.
---

# code-design — hình dạng, module, hiệu năng, bất biến, naming

Skill nền của Horcrux, đứng dưới `Skills/horcrux_skill.md`: `NT<n>`, ba tầng Luật · Nền tảng · Sổ tay và bảng
chỉ dẫn định nghĩa ở đó. Con trỏ `` `skill` › Mục `` trỏ sang skill khác; `mục X` trỏ trong file này.

## Hình dạng — SOLID, bậc cấu trúc, giới hạn của composite

| | Nội dung |
|---|---|
| **S** | Một class là một responsibility. Tách khi có nhiều hơn một **lý do thay đổi** — định nghĩa mà NT1, NT7 và `reuse-gate` trỏ về; không tách "cho gọn mắt" hay "cho đúng pattern". |
| **O** | Extend, don't modify. `O` cấm **đổi hành vi đường cũ**, không cấm thêm đường mới vào chính class: thêm method hay tham số mà đường cũ không đụng là đạt `O`. Phải đổi hành vi đường cũ là trách nhiệm mới — tách class. `O` không phải lý do leo bậc. |
| **L** | Subtype thay thế được base mà không break behavior, không side-effect lạ. |
| **I** | Interface nhỏ, tách theo consumer; không ép client phụ thuộc method nó không dùng. |
| **D** | **Consumer không tự `new` thứ nó phụ thuộc** — nhận vào (factory, pool, container thì đương nhiên `new`). `D` không đòi interface: trong một hệ, nhận class cụ thể vẫn là nhận vào; qua ranh giới hệ thì theo mục Module. Kéo thả vào `[SerializeField]` cũng là nhận vào (`editor-first`). |

**Bậc cấu trúc leo từ dưới lên, mỗi bậc chỉ leo khi bậc dưới không còn đạt** (NT1). Nơi duy nhất định
nghĩa "khi nào cần interface".

| Bậc | Đủ dùng khi | Leo lên khi |
|---|---|---|
| **Logic tại chỗ** | chạy ở một nơi, đọc một mạch là hiểu hết | có **người gọi thứ hai**, hoặc một ý không còn nhìn hết trong một màn hình |
| **Hàm tách riêng** | đặt được tên nói đúng mục đích (mục Naming); không giữ state giữa các lần gọi | phát sinh **state phải giữ**, hoặc một cụm hàm cùng thao tác trên một nhóm dữ liệu |
| **Class hoặc struct** | có **trách nhiệm gọi được tên** và state riêng (`S`) | có **implementation thứ hai** đang có thật |
| **Delegate làm tham số** | thân thuật toán **giống hệt** ở mọi biến thể, chỉ khác **một thao tác** gọi được tên, biến thể không giữ state riêng. Khai `static readonly Func<…>` với lambda `static`: bắt biến ngoài thành lỗi biên dịch thay vì rác GC âm thầm (NT2). Callback sinh ở call site thì chữ ký **trả lại chủ thể** để handler là method group `static` — bất biến "không cấp phát" nằm ở chữ ký, không ở kỷ luật. Bất biến của vòng lặp — thứ tự, tiến độ, lỗi, điểm thoát khi huỷ — chỉ có **một bản**; nhân vòng lặp ra n bản hay cắm `if` biến thể là nhân bất biến ra n bản (NT7) | biến thể cần **state riêng**, hoặc **hơn một thao tác** đi cùng — lúc đó nó đã là interface |
| **Interface hoặc abstract** | implementation thứ hai **đang có thật**, không phải sắp có — hoặc framework phải gọi sang assembly vendor nó không được tham chiếu (bản cài đặt ở assembly riêng, bật bằng define); lý do là **chiều phụ thuộc**, không phải "phòng khi đổi lib" | — |

**Biến thể là một cửa riêng, không phải cờ trên cửa chung.** Hai chế độ loại trừ nhau là **overload nhận
spec riêng**, không phải một spec mang cờ, và không chồng hiệu ứng của biến thể kia lên; kịch bản mới của
service dùng chung là **consumer mới** đưa nội dung qua tham số, không phải cờ mọc trên service — service
mọc cờ theo kịch bản là gom domain mọi consumer về một class (NT1, NT3); component dùng chung ở nhiều host
**không có cờ chế độ trên instance** — ô quên tick trên một instance không để lại dấu vết (`editor-first`). Hai chế
độ buộc là một giá trị thì `enum` hai phần tử, không `bool` (mục Naming).

**`MonoBehaviour` · `ScriptableObject` · class thuần không phải ba bậc của thang trên** — thang trả lời
*chia tới đâu*, ba thứ này trả lời *đóng gói ở đâu*, hỏi **sau** khi đã biết cần một class.

| Nó làm gì | Là gì | Vì |
|---|---|---|
| trả lời từ **tham số truyền vào**, không giữ gì | **class thuần static** | test không dựng gì; không thêm asset có thể quên kéo |
| trả lời từ **dữ liệu của chính nó**, do người dựng đặt | **`ScriptableObject`** | dữ liệu là cấu hình, asset kéo được từ mọi nơi (`editor-first`) |
| **làm gì đó theo thời gian** — loop, chờ, subscribe, ghi ra ngoài | **`MonoBehaviour`**, hoặc class thuần do một `MonoBehaviour` sở hữu | cần một **mốc chết tin được** để tắt; phép kiểm là *có cần tắt được không*, không phải *có cần vòng đời không* |

> **Nền tảng** — `ScriptableObject` dừng ở **dữ liệu**: cấu hình, bảng số, danh mục, preset. Không làm
> kênh sự kiện, biến dùng chung, state đổi lúc chạy; không tự mở `CancellationTokenSource`. Vì: field
> serialize đổi lúc Play **không quay lại** khi dừng · asset chỉ nạp lúc có người chạm lần đầu nên **không
> có thứ tự khởi tạo** · không có mốc kết thúc để đóng token. Ngoại lệ: **bảng tra dẫn xuất từ chính dữ liệu
> của asset** (id → chỉ số) — field không serialize, **host dựng lại bằng hàm tường minh** (`Clear` rồi
> điền) ở boot, vì field không serialize sống qua các lần Play khi tắt domain reload.

**Ba hình dạng composite — viết lý do ra tại chỗ trước khi dùng.** Mặc định là không; thước đo là **phạm
vi bài toán**: cùng cách chia có thể đúng ở hệ nhiều người chạm và thừa ở tính năng cục bộ.

| Hình dạng | Giá phải trả | Chỉ dùng khi |
|---|---|---|
| **Cây composite** — cha và lá cùng interface, duyệt đệ quy | hành vi không nằm ở đâu, phải chạy mới biết; mỗi nút một dispatch ảo mỗi frame (NT2) | **cấu trúc lồng nhau là của dữ liệu thật**, không do người viết chọn |
| **Một tính năng xé thành nhiều MonoBehaviour** | thứ tự `Awake`/`Update` giữa mảnh không định trước, wire thiếu chỉ lộ lúc chạy, mỗi mảnh một message qua ranh giới native | các mảnh **thật sự lắp lẫn** giữa nhiều prefab, và tổ hợp đó **đang tồn tại thật** |
| **Hệ dựng chồng nhiều hệ khác** (kết hợp) | không rời đi một mình (NT3) | không tách nổi thành hệ độc lập cộng lớp nối mỏng; vẫn chỉ ra đường tách (mục Module) |

**`partial` chỉ để giảm độ dài file ở chỗ không có OOP để vi phạm.** Ba ca: Editor tool và Debug của một
đối tượng · static class chứa extension method · khối field serialize của một nhóm Inspector kèm property,
không thân hàm. Ngoài ba ca, class phải chia file mới đọc nổi là class ôm nhiều lý do thay đổi: **tách
class, không tách file** (`S`).

> **Nền tảng** — DI runtime mặc định là InitArgs (`Sisus.Init`): `[Service(typeof(T))]` đăng ký,
> `MonoBehaviour<TDep>` + `Init(TDep)` nhận. **Mỗi class chỉ có MỘT khe `Init` framework tự gọi** — base
> generic đã tiêu khe thì `IInitializable<…>` ở class con **không ai gọi** (log chỉ trong `#if DEV_MODE`);
> ca này bắt buộc một `*Initializer` kéo tay. `Awake` của base gọi `Init` trước `OnAwake`. Initializer
> không phụ thuộc nhau xếp theo assembly → namespace → `FullName` vào `executionOrder`: **đổi tên một
> Initializer là đổi thứ tự `Awake`**; thứ tự cần thật thì `[InitAfter]`. Editor và tooling không bắt buộc
> InitArgs. **Đổi resolve lười sang sớm là dịch cửa sổ thất bại, không bỏ nó** — hỏi: *lúc đó thứ mình cần
> đã tồn tại chưa?*

> **Nền tảng** — region trong class theo thứ tự **cố định** `Properties` · `Unity Callbacks` · `API` ·
> `Class Methods` · `DI` (cuối, gói field nhận vào cùng `Init`): trạng thái → vòng đời → mặt ngoài → thân.
> **Callback của Unity là cửa mỏng: thân nằm trong hàm có tên, cửa chỉ gọi hàm đó.** Thứ tự
> `Awake`/`OnEnable` giữa các object do engine chọn, Script Execution Order là cấu hình ẩn — thứ tự giữa
> nhiều việc do **một chủ gọi** xếp bằng lời gọi tường minh. Chấp nhận để engine gọi khi chủ gọi tường minh
> phải biết một kiểu qua ranh giới không được biết (mục Module), hay kéo tham chiếu qua nhiều object cho một dòng.
> Engine bảo đảm: trong một cây object vừa bật, `Awake` cả cây xong trước `Start` (`editor-first`).

**Sổ tay** — hình dạng file và class trong Horcrux:

| Chỗ | Cách làm |
|---|---|
| Code chỉ có ở Editor | file `*.Editor.cs` khai `partial`, bên trong vẫn `#if UNITY_EDITOR` — file runtime không bị cắt ngang, nút Editor đọc thẳng private |
| Nhận phụ thuộc | `MonoBehaviour<T>` cho component thường · `IInitializable<T>` khi kế thừa một base **không phải** `MonoBehaviour<T>` của InitArgs (base đã là `MonoBehaviour<T>` → `*Initializer`) · không service locator trong hệ · object sinh lúc chạy tự nhận service qua `Init` của chính nó, không truyền qua nơi `Instantiate` |
| Helper lên object có sẵn | extension method, file partial `<Type>Extensions.<Nhóm>.cs`, khai **đúng namespace của class gốc** không theo thư mục — IDE sinh namespace theo thư mục ra class thứ hai cùng tên, compile vẫn sạch |
| Dựng host | hệ không tự `new GameObject`; host là component kéo tay vào scene |
| Field serialize | `[Splitter("References")]` (ô kéo) trước `[Splitter("Configs")]` (số chỉnh) |
| Field private runtime | tiền tố `_`; `[SerializeField]` không `_` — đọc tên biết giá trị từ Inspector hay code |
| Log | mở bằng `[TênHệ]: `, kèm `this` làm context |
| Cụm núm của một helper chuyển động | struct `[Serializable]` gom núm, truyền nguyên; dải hợp lệ bằng `[Min]` |
| Hàm một biểu thức | expression-bodied, kể cả `void` |
| Ẩn method của contract | mặc định `public`; explicit interface implementation chỉ cho method mà gọi sai gây mất dữ liệu im lặng |

## Module — độc lập trước, kết hợp là ngoại lệ

Ranh giới hệ phải **nhìn thấy được** trong cấu trúc dự án; mỗi hệ một trách nhiệm gọi được tên. **Module
không tham chiếu trực tiếp implementation của nhau** — cơ chế trung gian (interface, event bus, dữ liệu
thuần) chọn theo bài toán, miễn đổi implementation một bên mà bên kia không sửa. Trong một hệ không cần
tầng này (`D`). **Consumer cần method nào thì method đó thuộc contract** — đăng ký thêm kiểu cụ thể để với
tới method ngoài contract là gọi tên implementation qua ranh giới; gỡ gói vendor là dự án hết compile, dù
asmdef vendor đã dựng để tắt theo gói.

**Phân tầng theo mức phụ thuộc, quyết từ đầu** (NT3): hệ **độc lập** · hệ **kết hợp** · **Utilities**
static và universal, không phụ thuộc hệ nào.

**Sổ tay** — Horcrux bày tầng ra thư mục, để chỗ đặt file trả lời NT3 mà không mở code: `Abstractions/`
giữ thứ dự án **gọi tên** (interface, base `A*`, model thuần, event, hằng) · `Implementations/` giữ class
cụ thể dự án chỉ **wire** vào scene (host, bootstep, `*Initializer`) · trong mỗi bên `Foundations/` là hệ
độc lập, `Composites/` là hệ kết hợp · `Utilities/` ngoài cả hai. File đặt nhầm bên là một phụ thuộc giấu.

**Cơ chế mở rộng Horcrux cung cấp không được ép dự án đẩy domain sang framework** (NT3). Phép kiểm: *nửa
mà dự án viết có nằm trong ranh giới biên dịch của dự án không?*

> **Nền tảng** — `partial class` nửa ở framework, nửa ở dự án: **hỏng** — mọi phần phải cùng assembly, nửa
> dự án phải vào assembly framework bằng `.asmref` và không tra được type dự án từ đó (circular reference).
> `abstract class` ở framework, dự án khai subclass: **đúng** — subclass là type dự án, chỉ gọi xuống. Phần
> framework "tự chạy được một mình" biến mất là đặc điểm: tự chạy được nghĩa là đang mang một mẩu domain.
> Ba bẫy kế thừa hỏng-im-lặng: attribute `Inherited = false` (`[Service]` của InitArgs là một) ở base **không**
> tới subclass · `GetFields(NonPublic | Instance)` **không** thấy private field của base · magic method Unity
> subclass đặt trùng tên che hẳn bản base — khai `protected virtual` để `override`. **Lớp nối với code di
> sản không có asmdef phải nằm trong Assembly-CSharp**: asmdef không tham chiếu được Assembly-CSharp.

**Framework sở hữu cơ chế, dự án sở hữu từ vựng.** Tập giá trị dự án đặt tên (khoá tra cứu, loại, trạng
thái) thì framework khai là **type parameter** có ràng buộc, dự án chốt bằng class con; framework không
dựng kiểu bọc thay — kiểu bọc chỉ nói "đây là một khoá", kiểu của dự án nói *khoá nào*, cho compiler và
công cụ authoring biết tập hợp lệ. Bên trong framework hạ về dạng rẻ một lần ở cửa vào. Phép kiểm: *dự án
có phải đổi kiểu bằng tay (cast, gõ số, gõ chuỗi) ở call site hoặc trong asset không?* **Tập giá trị do dữ
liệu ngoài định nghĩa** (bảng remote, nội dung tải về — thêm phần tử không build) thì từ vựng thuộc về
dữ liệu, không khai enum: code chỉ gọi tên các **vai** có nghĩa với nó (mặc định, đích đặc biệt) bằng id dự
án trả về, và kiểm "id có hàng trong dữ liệu" thay cho "id có trong enum". Enum ở đây đóng tập lại và bắt
build cho mỗi phần tử mới; giá phải nêu khi chọn: mất dropdown Inspector và tên compile-time, tên hiển thị
dời vào một cột của dữ liệu.

**Nâng một chức năng lên tái sử dụng khi người dùng thứ hai đang có thật** (NT7): hàm thuần → Utilities ·
có state hay nhiều biến thể → interface rồi tách implementation · chỉ khác một giá trị → tham số. Đặt ở
**tầng thấp nhất mà cả hai với tới được**. Thời điểm: khi sắp viết bản thứ n của một cơ chế đã có ở module
anh em — bản mới là bản dùng chung đầu tiên; bản cũ chuyển sau bằng thêm rồi xoá, không sửa base. Là đề
xuất; phần runtime developer gõ (`reuse-gate`).

**Nâng bằng kế thừa thì cắt ở ranh *cơ chế / quyết định*:** base giữ cơ chế (phần giống hệt ở mọi nơi dùng
— tham chiếu đích, bộ đệm, định dạng), lớp con chỉ còn một quyết định (*nói gì, lúc nào*). Phép kiểm: lớp
con đọc hết trong một màn hình, không chạm bộ phận của base, chỉ gọi động từ của nó. Luật đi kèm: ô kéo
thả ở base khai **kiểu rộng nhất còn đủ member base dùng** · base phơi động từ theo việc (`protected`), bộ
phận `private` (`O`) · hook vòng đời `protected virtual`, lớp con `override` gọi `base` rồi nối nhịp riêng
· base là `MonoBehaviour` thì giá là khe DI — lớp con nhận phụ thuộc qua `*Initializer` gắn tay, ghi vào
Editor setup của từng chỗ dùng.

## Hiệu năng runtime

Luật ở NT2; đây là cách đạt, cho cả **bộ nhớ** (cấp phát, GC) lẫn **thời gian** (phép tính mỗi nhịp).
**Nghiệm thu:** chỉ ra được **chỗ đo** và **số trước–sau** (NT8). Phép đo "0 cấp phát" kèm **đối chứng**:
cùng dụng cụ phải bắt được một cấp phát cỡ đang tìm. *Đã sai một lần:* meter đo theo kích thước heap báo
+0 B cho 1000 closure trong Editor; recorder đếm từng GC.Alloc (`Is.Not.AllocatingGCMemory()`) thì thấy.

Trước khi cache hay tối ưu, ba câu theo thứ tự — "có" ở câu nào dừng ở đó: **không cần tính** nó được không
· tính **một lần lúc authoring** được không (`editor-first`) · đổi **cấu trúc dữ liệu** để câu hỏi tự biến mất được
không? Hết ba câu mới tới kỹ thuật. *Đã sai một lần:* chuỗi nội suy từ một số remote config cấp phát lại
mỗi tap với lý do "tap là tương tác" — nhịp được đem ra miễn trừ đúng việc NT2 nói không được miễn trừ.

> **Nền tảng** — `Update`, polling loop, `OnGUI` bị gọi lại liên tục cho **cùng một state**; việc nặng trong
> đó thuộc về event handler hoặc lúc authoring.

**Sổ tay** — *bộ nhớ:* pool thay `Instantiate`/`Destroy` lặp · pre-alloc capacity · reuse buffer bằng
`.Clear()` · `struct` cho data nhỏ ngắn hạn · `ref`/`in`/`Span<T>` thay copy · `static readonly` thay `new`
lặp · tránh LINQ, boxing, string concat ở hot path · không closure capture — cache delegate thành `static
readonly` hoặc field · ghi số vào `StringBuilder` bằng `TryFormat` vào `stackalloc` rồi
`Append(ReadOnlySpan<char>)` — trên Mono của Unity cả `Append(int)` cũng cấp phát. *Thời gian:* dirty flag ·
rebuild theo event · lookup dictionary dựng trước · tách phần tĩnh tính một lần khỏi phần động incremental ·
precompute hằng nặng ngoài vòng lặp · guard thoát sớm · `sqrMagnitude` khi chỉ so khoảng cách. *Sửa list an
toàn:* duyệt ngược khi xoá, hoặc deferred removal.

## Bất biến — bảo vệ bằng cấu trúc, không bằng kỷ luật

Bất biến giữ bằng "mọi người nhớ làm đúng" vỡ ở đúng người thứ hai. Sắp xếp code sao cho cái sai **không
thể xảy ra**. **Quyết định mà lý do là "phải nhớ đừng…" thì chỗ đặt sai, không phải tên sai** — hỏi: *có
chỗ đặt nào làm việc "đừng" đó bất khả thi không?* Thường có, và rẻ. *Đã sai một lần:* helper cùng tên
trong class con che **toàn bộ** overload của `UnityEngine.Object`; đổi tên thì ràng buộc vẫn còn, đưa ra
**extension method** thì ràng buộc biến mất, vì extension không bao giờ che instance method.

| Luật | Nghĩa là |
|---|---|
| **Một sự thật = một chủ sở hữu** | mỗi dữ liệu có đúng một nơi giữ bản gốc; mọi cache chỉ ra được ai dựng lại, khi nào, và bám vào bất biến **đã có** (dirty flag, version counter), không dựng bất biến thứ hai. **Một cờ chỉ được tiêu thụ ở đúng một nơi** |
| **Hai bản buộc phải khớp thì suy từ MỘT nguồn** (ngoại lệ của "để lặp", NT7) | *công thức* đo–vẽ, vẽ–hit-test, điều kiện ẩn–hiện: cùng một hàm, nếu không lệch kiểu nhìn-đúng-bấm-trượt · *chuỗi định danh hai hệ phải khớp* (khoá, tên event): khai **một lần phía phát**, phía nhận trỏ symbol · *định danh số chia sẻ với asset hay hệ khác*: `enum` gán số tường minh từ `= 1` (`0` là `default`), không `const int`; số đã nằm trong asset là wire format — không đánh số lại, thêm ở cuối · *vị trí hai vật kề nhau ở hai hệ toạ độ* (world và canvas): neo cùng một mép, cùng luật co giãn · *phản hồi nhiều kênh của một khoảnh khắc* (tiếng, rung, hình): gọi cạnh nhau trong **cùng một hàm**, kênh nghe event riêng sẽ bắn cả ở ca hàm phản hồi đã chặn |
| **Một trạng thái = một cửa ghi, một chủ gọi** | mở, huỷ, chốt của một trạng thái trong **một** class; nơi khác chỉ **phát sự thật** — sự kiện tên theo điều đã xảy ra, publish đồng bộ, đúng một người nghe hành động; cần mốc chưa có thì thêm sự kiện mới, không mượn sự kiện cũ. Hệ quả luôn xảy ra, rẻ, không ném thì trong **setter** (`SetX(v)` cạnh field `x` là hai cửa ghi); tuỳ chọn hay tốn kém giữ method. **Hai lời gọi luôn đi cùng nhau là một kỷ luật**: tiền đề đi vào **trong** mỗi cửa public, thành `private` — mặt ngoài nói trạng thái caller muốn thấy, không nói cơ chế |
| **Một bảo đảm phải phủ MỌI đường vào** | "mất không quá X", "luôn hợp lệ" thì mọi cửa ghi đi qua chỗ tạo bảo đảm; cửa hẹp là thân chung của cửa rộng. Phép kiểm: **đếm cửa trước, đọc thân sau** — grep mọi API ghi, từng cửa chỉ ra nó đi qua chỗ tạo bảo đảm hoặc ghi tại chỗ là không |
| **Một phép biến đổi chỉ áp ở MỘT tầng** | runner đã ease `t` thì thân nhận `t` đã ease; hai tầng cùng có tham số ease thì một tầng cố định `Linear` |
| **Chọn cấu trúc theo bảo đảm người dùng dựa vào** | `Dictionary` đúng để tra cứu, sai khi kết quả là danh sách để vẽ — thứ tự duyệt không bảo đảm. Thứ tự từ cấu hình ngoài phải **toàn phần và tất định**: thiếu rơi về ưu tiên thấp nhất; hoà phá bằng định danh ổn định, không bằng thứ tự đăng ký |
| **Mặc định là ca số đông, viết bằng tên** | ca số ít phải khai rõ; mặc định viết tường minh (`=> Mode.Normal`), không dựa phần tử đầu enum |
| **Framework không chọn nguồn ngẫu nhiên** | hàm cần ngẫu nhiên nhận nguồn từ caller (`Func<int, int> nextIndex`: `count → [0, count)`), không gọi `UnityEngine.Random` hay `new System.Random` bên trong. Dự án truyền `UnityEngine.Random.Range` để giữ nước đi đã author; test truyền `System.Random(seed)` để tất định. Đổi nguồn là đổi kết quả theo seed (NT6) |
| **Danh sách vừa là lệnh vừa để vẽ thì hỏi hai vai có chung khoá gộp không** | vai thực thi gộp theo khoá hệ nhận lệnh, vai hiển thị theo khoá người dùng nhìn. Kho cộng dồn nhiều chu kỳ gộp lúc đọc ra, không lúc ghi vào |

**Guard — một câu hỏi duy nhất: cái sai đó lộ ra lúc nào?** Đích là build không có ca null hay ca sai nào;
guard hay không chỉ là hai đường tới đích.

| Cái sai lộ ra… | Xử lý | Vì sao |
|---|---|---|
| **Ngay lúc authoring hoặc nổ ngay lần Play đầu** — đã kiểm là nổ thật | **để nó nổ** | exception thô và `LogError` chặn developer ngang nhau; đổi là trả phí vĩnh viễn trong build. Bảng tra từ dữ liệu authoring dùng `Add`, không `TryAdd` |
| **Sai lúc setup nhưng không nổ** — wire thiếu, hệ chạy tiếp thiếu một tính năng | **`LogError` rồi thoát**, kể cả lặp mỗi lượt | thoát im lặng biến setup thiếu thành tính năng vắng mặt cả phiên |
| **Lọt qua authoring rồi sai âm thầm** giữa gameplay | **guard đầy đủ** | Editor không bắt chắc: reference chỉ có lúc runtime · null ở một variant · sai ở một tổ hợp cấu hình · null sau `Destroy` · dữ liệu từ ngoài |
| **Ca hợp lệ** — chưa tới lượt | thoát im lặng | không phải lỗi |
| **Không gọi tên được** lượt kiểm nào bắt nó | guard | "chắc không xảy ra" không phải bằng chứng (NT8) |

**Dữ liệu ngoài kiểm một lần ở cửa vào; sau cửa, consumer tin hợp đồng.** Guard ở cửa là hàm thuần kiểm
luật nghiệp vụ, sai thì từ chối cả cấu hình kèm `LogError` — không mỗi consumer một nhánh (cùng luật `editor-first` › Import/export).
View runtime không có nhánh vẽ lỗi: chủ dữ liệu `LogError` kèm `this` khi tra thiếu là toàn bộ chẩn đoán;
nhánh duy nhất view được có là **xoá vết cũ** của ô dùng lại (gán `null` sprite rồi thoát). "Không giấu
thứ có thật" (`editor-first` › Editor tool) là cho tool Editor, nơi người nhìn sửa được dữ liệu; trong build người nhìn là người chơi.

**Giá trị cấu hình ngoài đổi hành vi gameplay thì đứng yên suốt phiên:** nhận ở **một** mốc boot có giới
hạn thời gian, bản về muộn để phiên sau — đổi giữa ván là hai luật chơi trên một ván. Chờ ở boot vì người
chơi mới phải vào đúng nhóm A/B ngay phiên đầu. Mốc "đã xong" bắn ở **mọi** đường ra: lỗi, quá hạn, mất mạng.

**"Để nó nổ" đứng được nhờ vế *nổ* — phải kiểm.** Năm hình dạng nó **không** nổ:

| Hình dạng | Vì sao im lặng | Trả về chỗ nổ bằng |
|---|---|---|
| **Giá trị mặc định của kiểu là hợp lệ** — `float` 0, `bool` false, list rỗng, enum đầu | ô trống không phân biệt với ô cố ý | **authoring**: default trong khai báo field, kẹp dải `[Min]`/`[Range]`. **Dữ liệu ngoài parse vào DTO** (JSON remote, CSV): khoá thiếu được điền default im lặng — DTO đòi đủ khoá (`[JsonObject(ItemRequired = Required.Always)]`) để parse ném và hệ giữ bản cũ; không dùng hàng thưa "chỉ ghi ô khác mặc định" |
| **Host bắt lỗi fail-open** — vòng dispatch, chain boot log rồi bỏ qua | thiếu wire thành một dòng log lúc boot rồi cả phiên thiếu hệ | hệ tự đứng không cần host, hoặc thiếu nó phải hỏng ở cửa người chơi chạm |
| **Vòng lặp nhận sai token** | huỷ là hợp đồng, không log (`async-presentation`) | mỗi loop chỉ ra token là đời của ai |
| **Chỗ nổ nằm trong `#if DEBUG`** | release không có dòng đó | đọc `#if` quanh guard của thư viện ngoài trước khi tin |
| **Nổ trễ** — mảng kéo tay có ô trống chỉ chạm khi tải cao | lần Play đầu qua; hỏng lúc tải cao | nút Validate ở authoring (`editor-first` › Editor tool) — không sinh bản sao lọc null lúc chạy, đó là code che lỗi setup |

*Đã sai một lần:* field khoảng thời gian không default, quên điền ra 0, ghi đĩa **mỗi frame**; cùng hệ gắn
loop tự lưu vào token pha boot mà runner refresh mỗi lần load level — loop chết từ level hai. Không một dòng log.

**`try/catch` đi qua đúng câu hỏi đó.** Chỉ bọc khi **gọi được tên thứ ném ra**: API thật sự ném (I/O,
parse, network, reflection, dữ liệu ngoài) hoặc code của người khác chạy trong vòng lặp của mình. Khối
`catch` cho ca không bao giờ xảy ra nói dối rằng chỗ này có rủi ro. Bắt rồi phải làm gì đó; nuốt là biến
lỗi tỏ thành âm thầm. `catch (Exception)` trần chỉ ở biên trên cùng: vòng dispatch, entry point của tool.
**Quy ước chỉ thay được guard khi viết ở chỗ người vi phạm đang nhìn** — Tooltip, XML doc, tài liệu module
— không phải một lượt chat.

## Naming — self-documenting code

- **Tên là lời giải thích thứ nhất, comment là phương án cuối.** Phải kèm comment mới hiểu nghĩa là tên
  chưa đạt — đổi tên trước. Trần là **5 từ dài**, 6–7 từ ngắn đọc một mạch vẫn đạt
  (`elapsedSecondsSinceLastSave`). Viết tắt **quen mắt** — `curr` · `prev` · `max` · `min` · `Utc` — không
  tự chế. Viết tắt rồi vẫn vượt trần là khái niệm chưa tách đủ.
- **Tên mang đủ ngữ nghĩa về giá trị nó giữ** — *lúc nào*: `curr` · `last` · `prev` · `next`
  (`currLevelIndex`, không `levelIndex`); field cùng nói về một đối tượng dùng chung tiền tố · *đếm hay vị
  trí*: hậu tố `Amount`, `Index`; type mang đúng một index thì tên số ít · *tổng dồn*: `total` · *khoá tra
  cứu*: `Name` cho chuỗi, `Id` cho số · *đã xảy ra gì*: quá khứ phân từ theo góc nhìn người chơi hay dữ liệu
  (`collected`, `animated`, `claimed`), không từ mờ (`introDone`) hay từ tả hành động của hệ · *`-ing` là
  tiến trình, không phải trạng thái*: `IsPanelOpen` khác `IsPanelOpening` · biến cục bộ cùng luật; tính từ
  đứng trước danh từ (`inclusiveEndIndex`).
- **Tiền tố loại type**: interface `I…`, abstract class `A…` — nhìn tên biết không `new` được.
- **Method nói rõ mục đích**: `EnsureMaterial()`, `SwapWriteBuffer()`; không `Process`, `Handle`, `DoWork`.
  Boolean đọc như câu hỏi: `IsPickable`, `HasPendingInput`. Cờ nói **cái gì** đang chờ hay đang chạy, không
  mượn hình ảnh cơ chế: `hasPendingPassRequest`, không `isDirty`. **Tên hàm khớp thứ nó trả về**: hàm trả
  một điểm trên đường cong là `EvaluateQuadraticBezier`, không `Get…Curve`; `Evaluate` cho lấy mẫu tại `t`,
  `Compute` cho dẫn xuất một đại lượng. **`Setup(data)` cho view nhận dữ liệu để vẽ; `Bind`/`Attach` chỉ cho
  nối reference** (`editor-first`).
- **Casing theo quyền truy cập, không theo loại member**: private field camelCase · public field (DTO)
  PascalCase như property. Khoá wire format sinh từ tên field thì casing là hợp đồng: chốt trước khi bản dữ
  liệu đầu tiên rời máy developer.
- **Tên GameObject theo dạng `abc_xyz_0`: chỉ chữ thường, số và `_`** — một dạng cho mọi nơi đặt tên (tay
  trong Editor lẫn `new GameObject(...)`, cùng nhóm đánh số hậu tố) nên tên đoán được và tìm được bằng
  text. Chỉ áp cho GameObject. Root của prefab mang tên file (Unity ép lại khi lưu). Component bên thứ ba tự
  đổi tên object thì giữ đúng tên nó ép, ghi ngoại lệ tại chỗ — nếu không mỗi lần mở scene sinh một override
  tên trên instance prefab.
- **Hai chế độ loại trừ nhau là `enum` hai phần tử, không `bool`**: `ScrollDirection.Upwards` đọc được ở cả
  Inspector lẫn call site; chế độ thứ ba thêm vào `bool` là đổi kiểu. **Chuỗi bước có bước lúc có lúc không
  là `enum` bước, không bộ đếm**; bước đầu có hay không quyết **một lần** lúc mở.
- **Đại lượng hình học đặt tên theo hai đầu mút**, dạng `<từ>To<đến>Dist` (`pivotToTopViewDist`): đủ hai
  đầu thì `A − B` đọc thành hình vẽ ngay tại dòng code.
- **Comment chỉ khi thật sự cần — mặc định là không có.** Đoạn cần comment mới theo dõi được thì tách hàm có
  tên, đảo điều kiện, đặt biến trung gian — sửa code, không chú thích code. Comment còn lại **chỉ** nói
  **tại sao** (quyết định trái trực giác, bẫy đã sai một lần, công thức nguồn), không nói **cái gì**. API
  public có XML doc, `<param>` cho tham số có contract không hiển nhiên. Trần: **17–20 từ** cho comment và
  tooltip · **35 từ** cho `<summary>` · **15 từ** cho mỗi thẻ còn lại — chạm trần là sửa gốc trước khi viết
  thêm chữ, hoặc phần đang viết thuộc về tài liệu (`docs`).
- **Một từ = một nghĩa trong toàn hệ thống.** Khái niệm không đặt nổi tên riêng thường là khái niệm chưa
  rõ. **Tên hệ theo cơ chế nó cung cấp, không theo người dùng đầu tiên**: hệ overlay đặt tên theo tutorial
  thành sai khi màn nhận thưởng cũng dùng nó. **Trùng tên với lớp di sản dự án cố ý giữ** thì framework
  **giữ tên của nó**; dự án giải trùng bằng alias `using` ở đúng file lớp nối.
- **Đổi tên là đổi cả hệ** (NT10): code, comment, chuỗi debug, mọi tài liệu cùng một lần, kiểm bằng grep;
  file `.cs` và thư mục đổi tên **trong Project window** để giữ GUID. **Ranh giới:** khoá wire format và dữ
  liệu đã serialize ngoài repo là hợp đồng với hệ khác, không đổi theo. Asset trong repo thì đổi luôn key
  YAML cùng lần làm, không gắn `FormerlySerializedAs` — attribute đó là cầu cho asset không sửa tay được
  (nhiều scene, variant, dữ liệu người dùng), gỡ **sau khi** asset đã lưu theo tên mới; gỡ sớm thì Unity nạp
  entry rỗng không một dòng lỗi. **Đổi đơn vị cũng là đổi cả hệ:** grep mọi hằng mang đơn vị trong code
  (khoảng cách, lề, lực, scale bù cho cha cũ) **trước** lần chạy đầu; dựng lại prefab thì đặt lại scale của
  mọi transform.
  **Sổ tay** — phạm vi đổi tên một thực thể chọn theo **kiểu**, không theo tên; tên do người đề xuất cũng
  kiểm nghĩa bằng code trước khi đổi. Nghiệm thu: quét **mọi** định danh cũ trên **mọi** file YAML (kể cả
  `propertyPath` của variant, `m_MethodName` của UnityEvent), không dựa danh sách lập tay — compile sạch chỉ
  chứng minh code; chuỗi ký tự là một lớp riêng, liệt kê hết, không chuẩn hoá hai vế khi so diff. Đổi tên
  bằng luật máy móc thì áp luật lên **cây gốc** rồi so từng blob với cây mới theo cả đường dẫn; luật nhiều
  bước vẫn so với cây gốc; file đổi ngoài tập kỳ vọng là lỗi.
