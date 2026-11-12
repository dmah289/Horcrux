---
name: code-design
description: Dùng khi viết hoặc sửa code runtime: SOLID, bậc cấu trúc, giới hạn của composite, module và phân tầng phụ thuộc, hiệu năng bộ nhớ và thời gian, bất biến và guard, naming.
---

# code-design — hình dạng, module, hiệu năng, bất biến, naming

Skill nền dưới `Skills/horcrux_skill.md` (NT, ba tầng, ký hiệu định nghĩa ở đó).

## Hình dạng — SOLID, bậc cấu trúc, composite

- **S** — một class một **lý do thay đổi**; định nghĩa mà NT1, NT7, `reuse-gate` trỏ về. Không tách "cho gọn mắt", "cho đúng pattern".
- **O** — cấm **đổi hành vi đường cũ**, không cấm thêm đường mới vào chính class. Phải đổi đường cũ là trách nhiệm mới: tách class.
- **L** — subtype thay được base, không side-effect lạ. **I** — interface nhỏ, tách theo consumer.
- **D** — **consumer không tự `new` thứ nó phụ thuộc**, nhận vào (factory, pool, container thì `new`). Không đòi interface:
  trong một hệ nhận class cụ thể vẫn là nhận vào. Kéo thả vào `[SerializeField]` cũng là nhận vào.

**Bậc cấu trúc leo từ dưới, chỉ leo khi bậc dưới không còn đạt** (NT1) — nơi duy nhất định nghĩa "khi nào cần interface".

| Bậc | Leo lên khi |
|---|---|
| logic tại chỗ | có **người gọi thứ hai**, hoặc một ý không còn vừa một màn hình |
| hàm tách riêng (tên nói đúng mục đích, không state) | phát sinh **state**, hoặc cụm hàm cùng thao tác một nhóm dữ liệu |
| class hoặc struct (trách nhiệm gọi được tên) | có **implementation thứ hai đang có thật** |
| delegate làm tham số (thân giống hệt, chỉ khác **một thao tác**, biến thể không state) | biến thể cần state hoặc hơn một thao tác — lúc đó là interface |
| interface hoặc abstract | — chỉ khi implementation thứ hai **đang có thật**, hoặc framework phải gọi sang assembly vendor không được tham chiếu (bản cài ở assembly riêng, bật bằng define). Lý do là **chiều phụ thuộc**, không "phòng khi đổi lib" |

Delegate khai `static readonly Func<…>` với lambda `static` — bắt biến ngoài thành lỗi biên dịch thay vì rác GC; callback
sinh ở call site thì chữ ký **trả lại chủ thể** để handler là method group `static`. Bất biến của vòng lặp (thứ tự, tiến độ,
lỗi, điểm thoát khi huỷ) chỉ có **một bản**; nhân vòng lặp hay cắm `if` biến thể là nhân bất biến.

**Biến thể là cửa riêng, không phải cờ trên cửa chung:** hai chế độ loại trừ → overload nhận spec riêng · kịch bản mới của
service dùng chung → **consumer mới** đưa nội dung qua tham số, không mọc cờ trên service (gom domain mọi consumer về một
class) · component dùng ở nhiều host **không có cờ chế độ trên instance** — ô quên tick không để lại dấu vết.

**Hiệu ứng không mang giá:** cửa chạy hiệu ứng không trừ kho, tiền hay lượt; mỗi nguồn trả (nút dùng, phần thưởng, hồi sinh, mua
xong dùng ngay) trừ ở chỗ của nó rồi gọi cùng cửa. Nguồn mới không phải mọc cờ "miễn phí" trên cửa.

**Đóng gói ở đâu** (hỏi sau khi biết cần class): trả lời từ tham số, không giữ gì → **class thuần static** · trả lời từ dữ
liệu của chính nó → **`ScriptableObject`** · làm gì đó theo thời gian (loop, chờ, subscribe, ghi ra ngoài) →
**`MonoBehaviour`** hoặc class thuần do nó sở hữu — cần **mốc chết tin được** để tắt.

> **Nền tảng** — `ScriptableObject` dừng ở **dữ liệu** (cấu hình, bảng số, danh mục, preset): không làm kênh sự kiện, biến
> dùng chung, state lúc chạy, không mở `CancellationTokenSource`. Vì: field đổi lúc Play **không quay lại** khi dừng · asset
> nạp khi được chạm lần đầu nên **không có thứ tự khởi tạo** · không có mốc kết thúc. Ngoại lệ: bảng tra dẫn xuất từ dữ
> liệu asset — field không serialize, **host dựng lại tường minh** (`Clear` rồi điền) lúc boot, vì field đó sống qua các lần
> Play khi tắt domain reload.

**Composite — mặc định là không; viết lý do tại chỗ trước khi dùng.** Cây composite chỉ khi cấu trúc lồng **là của dữ liệu
thật** (giá: hành vi phải chạy mới biết, dispatch ảo mỗi nút mỗi frame) · một tính năng xé thành nhiều `MonoBehaviour` chỉ
khi các mảnh **thật sự lắp lẫn** giữa nhiều prefab đang tồn tại (giá: thứ tự `Awake`/`Update` không định, wire thiếu lộ lúc
chạy) · hệ kết hợp chỉ khi không tách nổi thành hệ độc lập + lớp nối mỏng, vẫn chỉ ra đường tách.

**`partial` chỉ ở chỗ không có OOP để vi phạm:** Editor tool và Debug của một đối tượng · static class extension · khối
field serialize kèm property. Ngoài ba ca, class phải chia file mới đọc nổi là ôm nhiều lý do thay đổi: **tách class**.

> **Nền tảng** — DI runtime là InitArgs: `[Service(typeof(T))]` đăng ký, `MonoBehaviour<TDep>` + `Init(TDep)` nhận. **Mỗi
> class một khe `Init` framework tự gọi** — base generic đã tiêu khe thì `IInitializable<…>` ở con **không ai gọi**; ca này
> cần `*Initializer` kéo tay. Base gọi `Init` trước `OnAwake` — con viết `OnAwake`, `Awake` riêng che bước tiêm. Initializer
> không phụ thuộc nhau xếp theo assembly → namespace → `FullName`: **đổi tên là đổi thứ tự `Awake`**; cần thứ tự thật thì
> `[InitAfter]`. Editor và tooling không bắt buộc InitArgs. Đổi resolve lười sang sớm là dịch cửa sổ thất bại — hỏi *lúc
> đó thứ cần đã tồn tại chưa?* Bỏ phụ thuộc `Init` của component có `*Initializer` trong prefab, scene: gỡ component
> Initializer trong Editor **lúc script còn compile**, rồi mới xoá file và đổi base — xoá trước thì asset giữ một script missing.
> **Một class một `[Service]`**: attribute thứ hai bị bỏ, chỉ một cảnh báo lúc boot — gộp `[Service(typeof(A), typeof(IA), …)]`.
> Service `FindFromScene` + `LazyInit` bị hỏi trước khi scene chứa nó nạp thì báo không thấy và **không tìm lại cả phiên**;
> client sau đó thiếu phụ thuộc ở chỗ không liên quan.

> **Nền tảng** — region cố định: `Properties` · `Unity Callbacks` · `API` · `Class Methods` · `DI`. Thứ tự
> `Awake`/`OnEnable` giữa object do engine chọn, Script Execution Order là cấu hình ẩn. Engine bảo đảm: trong cây object
> vừa bật, `Awake` cả cây xong trước `Start`.

**Gọi tường minh, không giao thứ tự cho callback Unity.** Callback Unity là cửa mỏng: **một** callback của **một chủ** gọi
lần lượt các hàm có tên — thứ tự nằm ở một chỗ, đọc từ trên xuống là biết, không phụ thuộc engine chọn ai trước. Rải việc
vào callback riêng của từng object chỉ khi gọi tường minh **đắt hơn hẳn** (kéo reference xuyên hệ, đảo chiều phụ thuộc) —
nói giá tại chỗ.

**Sổ tay** — `*.Editor.cs` partial bọc `#if UNITY_EDITOR` · `IInitializable<T>` khi base không phải `MonoBehaviour<T>` ·
không service locator trong hệ; object sinh lúc chạy tự nhận service qua `Init`, không truyền qua nơi `Instantiate` ·
extension ở `<Type>Extensions.<Nhóm>.cs`, namespace **của class gốc** (theo thư mục thì IDE sinh class thứ hai cùng tên,
compile vẫn sạch) · hệ không tự `new GameObject`, host kéo tay vào scene · `[Splitter("References")]` trước
`[Splitter("Configs")]` · `[SerializeField]` không `_`, private runtime có `_` · log mở bằng `[TênHệ]: ` kèm `this` · cụm
núm chuyển động là struct `[Serializable]`, dải bằng `[Min]` · hàm một biểu thức là expression-bodied · method contract
mặc định `public`, explicit interface chỉ cho method gọi sai gây mất dữ liệu im lặng.

## Module — độc lập trước, kết hợp là ngoại lệ

Ranh giới hệ **nhìn thấy được** trong cấu trúc dự án. **Module không tham chiếu implementation của nhau** — trung gian
(interface, event bus, dữ liệu thuần) chọn theo bài toán, miễn đổi một bên không sửa bên kia. **Consumer cần method nào thì
method đó thuộc contract**; đăng ký thêm kiểu cụ thể để với method ngoài contract là gọi tên implementation qua ranh giới.

Phân tầng quyết từ đầu (NT3): hệ **độc lập** · hệ **kết hợp** · **Utilities** static, không phụ thuộc hệ nào. **Sổ tay** —
Horcrux bày tầng ra thư mục: `Abstractions/` giữ thứ dự án **gọi tên** (interface, base `A*`, model, event, hằng) ·
`Implementations/` giữ thứ dự án chỉ **wire** (host, bootstep, `*Initializer`) · mỗi bên chia `Foundations/` (độc lập) và
`Composites/` (kết hợp) · `Utilities/` ngoài cả hai. File đặt nhầm bên là một phụ thuộc giấu. Namespace
`Horcrux.Runtime.<Abstractions|Implementations>.<Hệ>`: tầng `Foundations`/`Composites` chỉ ở thư mục, không vào namespace.

**Cơ chế mở rộng không ép dự án đẩy domain sang framework.** Phép kiểm: *nửa dự án viết có nằm trong ranh giới biên dịch
của dự án không?*

> **Nền tảng** — `partial class` nửa framework nửa dự án: **hỏng** (phải cùng assembly, `.asmref` vào framework thì không
> tra được type dự án). `abstract class` ở framework, dự án khai subclass: **đúng**; phần framework không "tự chạy được"
> là đặc điểm. Bẫy kế thừa im lặng: attribute `Inherited = false` (`[Service]`) không tới subclass · `GetFields(NonPublic)`
> không thấy private của base · magic method Unity trùng tên ở con che bản base — khai `protected virtual`. Lớp nối với code
> di sản không asmdef phải nằm trong Assembly-CSharp.

**Framework sở hữu cơ chế, dự án sở hữu từ vựng.** Tập giá trị dự án đặt tên (khoá, loại, trạng thái) → framework khai
**type parameter** có ràng buộc, dự án chốt bằng class con; không dựng kiểu bọc. Phép kiểm: *dự án có phải cast, gõ số, gõ
chuỗi ở call site hay trong asset không?* Tập giá trị **do dữ liệu ngoài định nghĩa** (bảng remote, nội dung tải về) thì từ
vựng thuộc dữ liệu, không enum: code chỉ gọi tên các **vai** có nghĩa với nó, kiểm "id có hàng trong dữ liệu"; giá phải
nêu: mất dropdown Inspector và tên compile-time.

**Hàm kiểm dữ liệu của framework kiểm bất biến của chính dữ liệu, không kiểm kịch bản:** hình dạng (`null`, rỗng, id hợp
lệ, trùng, tham chiếu nội bộ có đích) và **một vai bắt buộc** — vai rơi về mà mọi dự án đều phải có. Vai còn lại ("đã mua",
"VIP") là kịch bản của từng dự án: dự án tự đảm bảo, kiểm ở nơi gọi tên vai (host, tool Editor), không thành tham số của hàm
kiểm — nhận vai là đóng kịch bản của một dự án vào framework, dự án sau không có vai đó phải truyền số giả hay nhận lỗi giả.

**Trừu tượng hoá cho tái sử dụng khi người dùng thứ hai đang có thật:** hàm thuần → Utilities · state hay nhiều biến thể →
interface · chỉ khác một giá trị → tham số; đặt ở **tầng thấp nhất cả hai với tới**. Sắp viết bản thứ n của cơ chế đã có ở
module anh em thì bản mới là bản dùng chung đầu tiên, bản cũ chuyển sau bằng thêm rồi xoá. Chỗ đặt dự án hay Horcrux theo
`reuse-gate` › Đặt ở đâu, không chờ người dùng thứ hai.

**Nâng bằng kế thừa thì cắt ở ranh *cơ chế / quyết định*:** base giữ phần giống hệt mọi nơi (tham chiếu đích, bộ đệm, định
dạng), lớp con chỉ còn *nói gì, lúc nào* — đọc hết một màn hình. Ô kéo thả ở base khai **kiểu rộng nhất còn đủ member** ·
động từ `protected`, bộ phận `private` · hook vòng đời `protected virtual`, con gọi `base` · base là `MonoBehaviour` thì con
nhận phụ thuộc qua `*Initializer`, ghi vào Editor setup.

## Hiệu năng

Luật ở NT2. **Nghiệm thu:** **chỗ đo** và **số trước–sau**; đo "0 cấp phát" kèm **đối chứng** — cùng dụng cụ phải bắt được
một cấp phát cỡ đang tìm. *Đã sai một lần:* meter theo kích thước heap báo +0 B cho 1000 closure; đếm GC.Alloc
(`Is.Not.AllocatingGCMemory()`) thì thấy.

Trước khi cache hay tối ưu, dừng ở câu "có" đầu tiên: **không cần tính** được không · tính **một lần lúc authoring** được
không · đổi **cấu trúc dữ liệu** để câu hỏi biến mất được không? *Đã sai một lần:* chuỗi nội suy cấp phát mỗi tap với lý do
"tap là tương tác" — nhịp không phải miễn trừ.

> **Nền tảng** — `Update`, polling loop, `OnGUI` bị gọi lại cho **cùng một state**; việc nặng thuộc event handler hoặc
> authoring.

**Sổ tay** — *bộ nhớ:* pool · pre-alloc capacity · reuse buffer `.Clear()` · `struct` cho data nhỏ ngắn hạn ·
`ref`/`in`/`Span<T>` · `static readonly` · không LINQ, boxing, string concat, closure capture ở hot path · số vào
`StringBuilder` bằng `TryFormat` vào `stackalloc` (trên Mono `Append(int)` cấp phát). *Thời gian:* dirty flag · rebuild theo
event · lookup dựng trước · tách tĩnh tính một lần khỏi động incremental · guard thoát sớm · `sqrMagnitude`. *Xoá trong list:*
duyệt ngược hoặc deferred removal.

## Bất biến — bảo vệ bằng cấu trúc, không bằng kỷ luật

**Lý do là "phải nhớ đừng…" thì chỗ đặt sai** — tìm chỗ đặt làm việc "đừng" bất khả thi. *Đã sai một lần:* helper cùng tên
trong class con che **mọi** overload của `UnityEngine.Object`; đưa ra extension method thì hết, vì extension không che
instance method. *Đã sai một lần:* hành động trả xong dùng ngay (không số dư, không nút) nằm chung enum với hàng có số
dư; vòng tặng quà duyệt cả enum nên nó nhận số dư không ai đọc. Tập "tất cả X" chỉ gom phần tử cùng hành vi.

| Luật | Nghĩa là |
|---|---|
| **Một sự thật = một chủ sở hữu** | một nơi giữ bản gốc; cache chỉ ra ai dựng lại, khi nào, bám bất biến **đã có** (dirty flag, version). Một cờ tiêu thụ ở **một** nơi. Sự kiện đổi giá trị mang `(old, new)` — consumer tự giữ bản cũ là bản sao thứ hai |
| **Hai bản buộc khớp thì suy từ MỘT nguồn** | đo–vẽ, vẽ–hit-test, điều kiện ẩn–hiện: một hàm · chuỗi định danh hai hệ: khai một lần phía phát, phía nhận trỏ symbol · định danh số chia sẻ với asset: `enum` tường minh từ `= 1`, số đã vào asset là wire format, chỉ thêm ở cuối · vật kề nhau ở world và canvas: neo cùng mép, cùng luật co giãn — cả cỡ camera lẫn đơn vị khoảng cách (`demo-compare` › Tỉ lệ màn) · phản hồi nhiều kênh một khoảnh khắc (tiếng, rung, hình): gọi cạnh nhau trong **một hàm** — kênh nghe event riêng sẽ bắn cả ca đã chặn |
| **Một trạng thái = một cửa ghi, một chủ gọi** | mở, huỷ, chốt trong **một** class; nơi khác chỉ **phát sự thật** (sự kiện tên theo điều đã xảy ra, đồng bộ, một người nghe hành động; cần mốc mới thì thêm sự kiện). Hệ quả luôn xảy ra, rẻ, không ném → trong **setter**. Hai lời gọi luôn đi cùng nhau thì gộp vào trong cửa public; thiếu điều kiện cho một vế thì hoãn cả hai — cờ đã bật cho vế đầu làm vế sau mất vĩnh viễn |
| **Lệnh tới hệ ngoài là ý muốn, đối chiếu ở một cửa** | hệ ngoài bỏ im lặng lệnh tới lúc nó chưa sẵn sàng hay đang bị che: giữ **ý muốn** của caller tách khỏi **thứ đã gửi**; mọi mốc đổi điều kiện gọi **một** hàm đối chiếu, hàm chỉ gửi khi kết quả khác lần gửi trước. Gộp hai cờ thì lệnh tạm ẩn xoá luôn ý muốn |
| **Lý do của trạng thái ghi ở cùng cửa với trạng thái** | cờ bật được từ nhiều đường (khoá, đóng, huỷ) mà người đọc cần biết vì sao → lưu lý do ngay ở cửa ghi, lý do đầu thắng, cờ suy từ lý do. Suy ngược từ field khác sai khi một đường phụ (cheat, sự kiện về muộn) vẫn ghi field đó |
| **Một bảo đảm phủ MỌI đường vào** | mọi cửa ghi đi qua chỗ tạo bảo đảm; **đếm cửa trước, đọc thân sau** — grep mọi API ghi |
| **Một phép biến đổi chỉ áp ở MỘT tầng** | runner đã ease thì thân nhận `t` đã ease; hai tầng cùng ease thì một tầng `Linear` |
| **Chọn cấu trúc theo bảo đảm người dùng dựa vào** | `Dictionary` để tra, không để vẽ danh sách. Thứ tự từ cấu hình ngoài **toàn phần, tất định**: thiếu rơi về thấp nhất, hoà phá bằng định danh ổn định |
| **Mặc định là ca số đông, viết bằng tên** | `=> Mode.Normal`, không dựa phần tử đầu enum |
| **Framework không chọn nguồn ngẫu nhiên** | nhận `Func<int, int> nextIndex` từ caller; dự án truyền `UnityEngine.Random.Range`, test truyền `System.Random(seed)` |
| **Danh sách vừa là lệnh vừa để vẽ** | thực thi gộp theo khoá hệ nhận lệnh, hiển thị theo khoá người dùng nhìn; kho cộng dồn gộp lúc đọc |

**Guard — cái sai lộ ra lúc nào?** Lộ ngay lúc authoring hay nổ ngay lần Play đầu (đã kiểm) → **để nổ**; bảng tra từ dữ liệu
authoring dùng `Add`, không `TryAdd` · sai lúc setup mà không nổ (wire thiếu) → **`LogError` rồi thoát**, kể cả lặp mỗi
lượt · lọt authoring rồi sai âm thầm giữa gameplay (reference chỉ có lúc chạy, null ở một variant, một tổ hợp cấu hình,
sau `Destroy`, dữ liệu ngoài) → **guard đầy đủ** · ca hợp lệ chưa tới lượt → thoát im lặng · không gọi tên được lượt kiểm
nào bắt nó → guard.

**Dữ liệu ngoài kiểm một lần ở cửa vào** bằng hàm thuần kiểm luật nghiệp vụ, sai thì từ chối cả cấu hình kèm `LogError`;
sau cửa consumer tin hợp đồng. Hàm kiểm đặt ở framework thì kiểm gì: mục Module. **Hàm thuần không log:** trả cờ hay
cộng dòng vào `List<string>` của caller; **host quyết log đi đâu, mức nào** — host, tool Editor và test gọi cùng một bản,
log trong hàm thuần đỏ Console mỗi lần tool gọi, bắt test `LogAssert`, và một sự kiện log hai lần khi host cũng log. View runtime không có nhánh vẽ lỗi — chỉ được **xoá vết cũ** của ô dùng lại. **Cấu hình
ngoài đổi gameplay đứng yên suốt phiên:** nhận ở **một** mốc boot có hạn giờ, bản về muộn để phiên sau (để người chơi mới
vào đúng nhóm A/B ngay phiên đầu); mốc "đã xong" bắn ở mọi đường ra — lỗi, quá hạn, mất mạng.

**"Để nổ" phải kiểm vế *nổ*.** Năm hình dạng **không** nổ: giá trị mặc định của kiểu là hợp lệ (default trong khai báo,
`[Min]`/`[Range]`; DTO parse đòi đủ khoá `[JsonObject(ItemRequired = Required.Always)]`, không dùng hàng thưa) · host bắt
lỗi fail-open (hệ tự đứng, hoặc thiếu nó hỏng ở cửa người chơi chạm) · vòng lặp nhận sai token (`async-presentation`) · chỗ
nổ nằm trong `#if DEBUG` của thư viện · nổ trễ ở ô trống chỉ chạm khi tải cao (nút Validate ở authoring, không lọc null lúc
chạy). *Đã sai một lần:* field khoảng thời gian không default thành 0, ghi đĩa mỗi frame, không một dòng log.

**`try/catch` chỉ khi gọi được tên thứ ném ra** (I/O, parse, network, reflection, dữ liệu ngoài, code người khác trong vòng
lặp của mình); bắt rồi phải xử lý, không nuốt. `catch (Exception)` trần chỉ ở biên trên cùng. **Quy ước chỉ thay được guard
khi viết ở chỗ người vi phạm đang nhìn** (Tooltip, XML doc, tài liệu module).

## Naming — self-documenting code

- **Tên là lời giải thích thứ nhất, comment là phương án cuối.** Trần **5 từ dài** (6–7 từ ngắn vẫn đạt); chỉ viết tắt
  quen mắt (`curr`, `prev`, `max`, `min`, `Utc`). Vẫn vượt trần là khái niệm chưa tách đủ.
- **Tên mang đủ ngữ nghĩa:** *lúc nào* `curr`/`last`/`prev`/`next` (`currLevelIndex`) · *đếm hay vị trí* `Amount`/`Index`
  · *tổng* `total` · *khoá* `Name` chuỗi, `Id` số · *đã xảy ra* quá khứ phân từ theo góc người chơi hay dữ liệu
  (`collected`, `claimed`), không từ mờ (`introDone`) · `IsPanelOpen` khác `IsPanelOpening` · tính từ trước danh từ.
- **Tiền tố:** interface `I…`, abstract `A…`. **Method nói mục đích** (không `Process`, `Handle`, `DoWork`); boolean đọc như
  câu hỏi; cờ nói **cái gì** đang chờ (`hasPendingPassRequest`, không `isDirty`); `Evaluate` lấy mẫu tại `t`, `Compute`
  dẫn xuất; `Setup(data)` cho view nhận dữ liệu, `Bind`/`Attach` chỉ cho nối reference.
- **Casing theo quyền truy cập:** private camelCase, public field (DTO) PascalCase; khoá wire format sinh từ tên field thì
  casing là hợp đồng.
- **GameObject `abc_xyz_0`** (chữ thường, số, `_`), cả tay lẫn `new GameObject(...)`; root prefab mang tên file; component
  bên thứ ba tự ép tên thì giữ, ghi ngoại lệ tại chỗ.
- **Hai chế độ loại trừ là `enum`, không `bool`**; chuỗi bước lúc có lúc không là `enum` bước, bước đầu quyết một lần lúc mở.
- **Đại lượng hình học theo hai đầu mút:** `pivotToTopViewDist` — `A − B` đọc thành hình.
- **Comment mặc định là không có**; chỉ nói **tại sao** (trái trực giác, bẫy đã sai, công thức nguồn). API public có XML doc.
  Trần **17–20 từ** comment, tooltip · **35 từ** `<summary>` · **15 từ** thẻ khác — chạm trần thì sửa gốc hoặc dời vào tài liệu.
- **Một từ = một nghĩa toàn hệ.** **Tên hệ theo cơ chế, không theo người dùng đầu tiên.** Trùng tên lớp di sản dự án:
  framework giữ tên, dự án giải bằng alias `using` ở lớp nối.
- **Đổi tên là đổi cả hệ** (NT10): code, comment, chuỗi debug, tài liệu cùng lần, kiểm bằng grep; `.cs` và thư mục đổi
  trong Project window để giữ GUID. Khoá wire format, dữ liệu ngoài repo không đổi theo. Asset trong repo đổi key YAML cùng
  lần, không `FormerlySerializedAs` — attribute đó chỉ là cầu cho asset không sửa tay được, gỡ sau khi asset đã lưu tên mới.
  **Đổi đơn vị cũng vậy:** grep mọi hằng mang đơn vị trước lần chạy đầu. **Sổ tay** — phạm vi chọn theo **kiểu**, không
  theo tên; quét **mọi** định danh cũ trên **mọi** YAML (kể cả `propertyPath`, `m_MethodName`); đổi bằng luật máy móc thì
  áp lên cây gốc rồi so từng blob, file đổi ngoài tập kỳ vọng là lỗi.
