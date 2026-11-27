# code-design — tra cứu

Nền tảng và Sổ tay của `code-design`. Đọc mục được bảng Tra cứu trong `RULE.md` gọi.

## Stack và quy ước

> **Nền tảng** — DI runtime là InitArgs: `[Service(typeof(T))]` đăng ký, `MonoBehaviour<TDep>` + `Init(TDep)` nhận. **Mỗi
> class một khe `Init` framework tự gọi** — base generic đã tiêu khe thì `IInitializable<…>` ở con **không ai gọi**; ca này
> cần `*Initializer` kéo tay. Base gọi `Init` trước `OnAwake` — con viết `OnAwake`, `Awake` riêng che bước tiêm. Initializer
> không phụ thuộc nhau xếp theo assembly → namespace → `FullName`: **đổi tên là đổi thứ tự `Awake`**; cần thứ tự thật thì
> `[InitAfter]`. Editor và tooling không bắt buộc InitArgs. Đổi resolve lười sang sớm là dịch cửa sổ thất bại — hỏi *lúc
> đó thứ cần đã tồn tại chưa?* Bỏ phụ thuộc `Init` của component có `*Initializer` trong prefab, scene: gỡ component
> Initializer trong Editor **lúc script còn compile**, rồi mới xoá file và đổi base — xoá trước thì asset giữ một script
> missing. **Một class một `[Service]`**: attribute thứ hai bị bỏ, chỉ một cảnh báo lúc boot — gộp
> `[Service(typeof(A), typeof(IA), …)]`. Service `FindFromScene` + `LazyInit` bị hỏi trước khi scene chứa nó nạp thì báo
> không thấy và **không tìm lại cả phiên**; client sau đó thiếu phụ thuộc ở chỗ không liên quan.

> **Nền tảng** — `ScriptableObject` dừng ở **dữ liệu** (cấu hình, bảng số, danh mục, preset): không làm kênh sự kiện, biến
> dùng chung, state lúc chạy, không mở `CancellationTokenSource`. Vì: field đổi lúc Play **không quay lại** khi dừng · asset
> nạp khi được chạm lần đầu nên **không có thứ tự khởi tạo** · không có mốc kết thúc. Ngoại lệ: bảng tra dẫn xuất từ dữ
> liệu asset — field không serialize, **host dựng lại tường minh** (`Clear` rồi điền) lúc boot, vì field đó sống qua các lần
> Play khi tắt domain reload.

> **Nền tảng** — region cố định: `Properties` · `Unity Callbacks` · `API` · `Class Methods` · `DI`. Thứ tự
> `Awake`/`OnEnable` giữa object do engine chọn, Script Execution Order là cấu hình ẩn. Engine bảo đảm: trong cây object
> vừa bật, `Awake` cả cây xong trước `Start`.

- **Delegate** khai `static readonly Func<…>` với lambda `static` — bắt biến ngoài thành lỗi biên dịch thay vì rác GC;
  callback sinh ở call site thì chữ ký **trả lại chủ thể** để handler là method group `static`.
- **Interface vì chiều phụ thuộc:** framework phải gọi sang assembly vendor không được tham chiếu (bản cài ở assembly
  riêng, bật bằng define).
- **Composite — giá:** cây composite thì hành vi phải chạy mới biết, dispatch ảo mỗi nút mỗi frame · xé thành nhiều
  `MonoBehaviour` thì thứ tự `Awake`/`Update` không định, wire thiếu lộ lúc chạy.
- **Dọn "dự phòng" ở `OnDestroy`** chạy cả lúc thoát app, khi các hệ kia đã tự dọn trước.
- Service đã tự chọn theo ngữ cảnh thì component chỉ gọi service; host không cần hành vi thì tắt component.

**Sổ tay** — `*.Editor.cs` partial bọc `#if UNITY_EDITOR` · `IInitializable<T>` khi base không phải `MonoBehaviour<T>` ·
không service locator trong hệ; object sinh lúc chạy tự nhận service qua `Init`, không truyền qua nơi `Instantiate` ·
extension ở `<Type>Extensions.<Nhóm>.cs`, namespace **của class gốc** (theo thư mục thì IDE sinh class thứ hai cùng tên,
compile vẫn sạch) · hệ không tự `new GameObject`, host kéo tay vào scene · `[Splitter("References")]` trước
`[Splitter("Configs")]` · `[SerializeField]` không `_`, private runtime có `_` · log mở bằng `[TênHệ]: ` kèm `this` · cụm
núm chuyển động là struct `[Serializable]`, dải bằng `[Min]` · hàm một biểu thức là expression-bodied · method contract
mặc định `public`, explicit interface chỉ cho method gọi sai gây mất dữ liệu im lặng.

## Module

**Sổ tay** — Horcrux bày tầng ra thư mục: `Abstractions/` giữ thứ dự án **gọi tên** (interface, base `A*`, model, event,
hằng) · `Implementations/` giữ thứ dự án chỉ **wire** (host, bootstep, `*Initializer`) · mỗi bên chia `Foundations/` (độc
lập) và `Composites/` (kết hợp) · `Utilities/` ngoài cả hai. Namespace
`Horcrux.Runtime.<Abstractions|Implementations>.<Hệ>`: tầng `Foundations`/`Composites` chỉ ở thư mục, không vào namespace.

> **Nền tảng** — `partial class` nửa framework nửa dự án: **hỏng** (phải cùng assembly, `.asmref` vào framework thì không
> tra được type dự án). `abstract class` ở framework, dự án khai subclass: **đúng**; phần framework không "tự chạy được"
> là đặc điểm. Bẫy kế thừa im lặng: attribute `Inherited = false` (`[Service]`) không tới subclass · `GetFields(NonPublic)`
> không thấy private của base · magic method Unity trùng tên ở con che bản base — khai `protected virtual`. Lớp nối với code
> di sản không asmdef phải nằm trong Assembly-CSharp.

- **Contract:** đăng ký thêm kiểu cụ thể để với method ngoài contract là gọi tên implementation qua ranh giới.
- **Từ vựng dự án qua type parameter:** dự án chốt bằng class con, không dựng kiểu bọc. Tập giá trị do dữ liệu ngoài định
  nghĩa: code chỉ gọi tên các **vai** có nghĩa với nó, kiểm "id có hàng trong dữ liệu"; giá phải nêu: mất dropdown Inspector
  và tên compile-time.
- **Hàm kiểm dữ liệu của framework** kiểm hình dạng (`null`, rỗng, id hợp lệ, trùng, tham chiếu nội bộ có đích) và **một vai
  bắt buộc** mọi dự án đều phải có. Vai còn lại là kịch bản của từng dự án, kiểm ở nơi gọi tên vai (host, tool Editor).
  Hàm kiểm nhận vai của một dự án là đóng kịch bản dự án đó vào framework: dự án sau không có vai đó phải truyền số giả hay
  nhận lỗi giả.
- **Kế thừa:** lớp con đọc hết trong một màn hình · ô kéo thả ở base khai **kiểu rộng nhất còn đủ member** · động từ `protected`, bộ phận `private` · hook vòng
  đời `protected virtual`, con gọi `base` · base là `MonoBehaviour` thì con nhận phụ thuộc qua `*Initializer`, ghi vào
  Editor setup.

## Hiệu năng

**Sổ tay** — *bộ nhớ:* pool · pre-alloc capacity · reuse buffer `.Clear()` · `struct` cho data nhỏ ngắn hạn ·
`ref`/`in`/`Span<T>` · `static readonly` · không LINQ, boxing, string concat, closure capture ở hot path · số vào
`StringBuilder` bằng `TryFormat` vào `stackalloc` (trên Mono `Append(int)` cấp phát). *Thời gian:* dirty flag · rebuild theo
event · lookup dựng trước · tách tĩnh tính một lần khỏi động incremental · guard thoát sớm · `sqrMagnitude`. *Xoá trong
list:* duyệt ngược hoặc deferred removal. Kết quả chỉ phụ thuộc cấu hình đọc một lần là hằng của phiên: dựng một lần, giữ
trong field.

## Guard

**"Để nổ" phải kiểm vế *nổ*.** Năm hình dạng **không** nổ:
1. giá trị mặc định của kiểu là hợp lệ — khai default trong khai báo, `[Min]`/`[Range]`; DTO parse đòi đủ khoá
   `[JsonObject(ItemRequired = Required.Always)]`, không dùng hàng thưa;
2. host bắt lỗi fail-open (hệ tự đứng, hoặc thiếu nó hỏng ở cửa người chơi chạm);
3. vòng lặp nhận sai token (`async-presentation`);
4. chỗ nổ nằm trong `#if DEBUG` của thư viện;
5. nổ trễ ở ô trống chỉ chạm khi tải cao — nút Validate ở authoring, không lọc null lúc chạy.

- Bảng tra từ dữ liệu authoring dùng `Add`, không `TryAdd`. `LogError` rồi thoát áp cả khi lặp mỗi lượt.
- Log trong hàm thuần đỏ Console mỗi lần tool gọi, bắt test `LogAssert`, và một sự kiện log hai lần khi host cũng log.
- Cấu hình ngoài về muộn để phiên sau, để người chơi mới vào đúng nhóm thử nghiệm ngay phiên đầu; mốc "đã xong" của lượt
  nhận bắn ở mọi đường ra.
- View runtime không có nhánh vẽ lỗi — chỉ được **xoá vết cũ** của ô dùng lại.

> **Nền tảng** — serializer của Unity không giữ `null` cho `string` và class `[Serializable]` lồng: đọc từ asset ra `""` và
> object rỗng. State có mặc định author trong asset thì nghĩa *"chưa có"* không đặt vào `null` của chính field đó — suy từ
> field hệ quyết được (id 0 = chưa quyết) hoặc chuẩn hoá ở cửa quyết nghĩa.

## Bất biến

Bốn bất biến chuyên biệt, cùng gốc "một trạng thái = một cửa ghi":

| Luật | Nghĩa là |
|---|---|
| **Thao tác có giá: hiệu ứng tách khỏi giá** | cửa chạy hiệu ứng không trừ gì; mỗi nguồn trả trừ ở chỗ của nó rồi gọi cùng cửa. Kiểm chỗ nhận **trước** khi trừ; trừ và ghi nhận cùng một cửa ghi |
| **Đếm ở ranh mỗi phần tử qua đúng một lần** | thường là lúc rời nguồn, không phải chỗ chứa trung gian; mọi đường qua ranh phát cùng một sự kiện |
| **Lệnh tới hệ ngoài là ý muốn, đối chiếu ở một cửa** | hệ ngoài bỏ im lặng lệnh tới lúc nó chưa sẵn sàng hay đang bị che: giữ **ý muốn** của caller tách khỏi **thứ đã gửi**; mọi mốc đổi điều kiện gọi **một** hàm đối chiếu |
| **Lý do của trạng thái ghi ở cùng cửa với trạng thái** | giá trị đổi được từ nhiều đường mà luật sau cần biết vì sao → lưu **vì sao giá trị hiện tại như vậy** ở cửa ghi, không lưu cờ mang tên một kịch bản; luật liệt kê nhóm lý do **được áp**, để lý do thêm sau rơi về phía an toàn |

- **Cấu trúc theo bảo đảm:** thứ tự từ cấu hình ngoài thiếu thì rơi về thấp nhất, hoà phá bằng định danh ổn định · mặc định
  viết `=> Mode.Normal`, không dựa phần tử đầu enum · nguồn ngẫu nhiên nhận `Func<int, int> nextIndex` từ caller: dự án
  truyền `UnityEngine.Random.Range`, test truyền `System.Random(seed)`.
- **Hiệu ứng không mang giá:** nguồn trả mới (nút dùng, phần thưởng, mua xong dùng ngay) không phải mọc cờ "miễn phí" trên
  cửa chạy hiệu ứng.
- **Kiểm chỗ nhận trước khi trừ:** bên nhận bỏ qua im lặng khi đầy thì người chơi mất giá mà không gì báo. Kiểm lúc bấm,
  không lúc mở màn — đầy có thể tới giữa chừng (hồi theo giờ, quà về); hết chỗ thì từ chối kèm phản hồi, không trừ, không
  mở bước trả tiếp theo.
- **Giá theo kết cục mà người chơi rời được giữa chừng thì đặt cọc trong save:** ghi cọc ở mốc cam kết (lần tác động đầu,
  không lúc vào), tất toán ở mọi đường ra — kết cục, bỏ có xác nhận, boot thấy cọc còn (phiên chết giữa chừng). Chưa có cọc
  thì rời không giá, không hỏi. Trừ thật và xoá cọc cùng một cửa ghi — chết giữa hai bước là trừ hai lần.
- **Hai lời gọi luôn đi cùng nhau:** thiếu điều kiện cho một vế thì hoãn cả hai — cờ đã bật cho vế đầu làm vế sau mất vĩnh
  viễn.
- **Ý muốn tách khỏi thứ đã gửi:** hàm đối chiếu chỉ gửi khi kết quả khác lần gửi trước. Gộp hai cờ thì lệnh tạm ẩn xoá
  luôn ý muốn.
- **Lý do của trạng thái:** suy ngược từ field khác sai khi một đường phụ (cheat, sự kiện về muộn) vẫn ghi field đó. Input
  bị luật bỏ qua vẫn được ghi: tracking cần sự thật dù nó không quyết.
- **Sự kiện phát sự thật:** tên theo điều đã xảy ra, đồng bộ, một người nghe hành động; cần mốc mới thì thêm sự kiện. Sự
  kiện đổi giá trị mang `(old, new)`. Định danh số chia sẻ với asset là wire format, chỉ thêm ở cuối.
- **Bộ đếm theo phạm vi:** reset ở cửa mở phạm vi mà mọi đường vào cùng đi qua; phạm vi đọc từ hợp đồng của dữ liệu dùng nó; hàm reset không caller là bộ đếm sống quá phạm vi.
- **Đếm tiến độ:** vật chuyển giữa hai chỗ chứa bị đếm lại, đường tắt bỏ qua chỗ chứa bị sót; số đếm và tổng phát chung từ
  một chủ.
- **Danh sách vừa là lệnh vừa để vẽ:** thực thi gộp theo khoá hệ nhận lệnh, hiển thị theo khoá người dùng nhìn; kho cộng dồn
  gộp lúc đọc.
- **Hai bản buộc khớp — thêm:** định danh số chia sẻ với asset là `enum` tường minh từ `= 1` · phía nhận chuỗi định danh trỏ
  symbol phía phát · vật kề nhau ở world và canvas neo cùng mép, cùng luật co giãn (`demo-compare` › Tỉ lệ màn) · kênh phản
  hồi nghe event riêng sẽ bắn cả ca đã chặn · consumer tự giữ bản cũ của giá trị là bản sao thứ hai.

## Naming

- **Trần tên:** 5 từ dài, 6–7 từ ngắn vẫn đạt; viết tắt quen mắt: `curr`, `prev`, `max`, `min`, `Utc`. Thẻ XML khác
  `<summary>` trần 15 từ.
- **Tên mang đủ ngữ nghĩa:** *lúc nào* `curr`/`last`/`prev`/`next` · *đếm hay vị trí* `Amount`/`Index` · *tổng* `total` ·
  *khoá* `Name` chuỗi, `Id` số · *đã xảy ra* quá khứ phân từ (`collected`), không từ mờ (`introDone`) · tính từ trước danh
  từ · đại lượng hình học theo hai đầu mút (`pivotToTopViewDist`).
- **Động từ:** `Evaluate` lấy mẫu tại `t`, `Compute` dẫn xuất; `Setup(data)` cho view nhận dữ liệu, `Bind`/`Attach` chỉ cho
  nối reference.
- `IsPanelOpen` khác `IsPanelOpening` · cờ `hasPendingPassRequest`, không `isDirty` · quá khứ phân từ theo góc người chơi
  hay dữ liệu (`collected`, `claimed`).
- **Chuỗi bước lúc có lúc không** là `enum` bước, bước đầu quyết một lần lúc mở.
- **Casing theo quyền truy cập:** private camelCase, public field (DTO) PascalCase; khoá wire format sinh từ tên field thì
  casing là hợp đồng.
- **GameObject `abc_xyz_0`** (chữ thường, số, `_`), cả tay lẫn `new GameObject(...)`; root prefab mang tên file; component
  bên thứ ba tự ép tên thì giữ, ghi ngoại lệ tại chỗ.
- **Trùng tên lớp di sản dự án:** framework giữ tên, dự án giải bằng alias `using` ở lớp nối.
- **Đoạn tên namespace không trùng type hay gọi của engine, runtime** (`Time`, `Random`, `Debug`, `Input`, `Object`): code
  nằm trong namespace anh em gõ `Time.` sẽ trỏ vào namespace đó (CS0234). Tên hệ thêm hậu tố (`TimeSystem`).
- **Đổi tên:** `.cs` và thư mục đổi trong Project window để giữ GUID. Khoá ngoài repo (field DTO của remote config, save,
  tracking): giữ khoá dưới tên mới, hay đổi khoá kèm giá (máy còn build cũ, cấu hình ngoài phải đổi cùng lần phát hành).
  Asset trong repo đổi key YAML cùng lần, không `FormerlySerializedAs` — attribute đó chỉ là cầu cho asset không sửa tay
  được, gỡ sau khi asset đã lưu tên mới. Đổi đơn vị: grep mọi hằng mang đơn vị trước lần chạy đầu.

**Sổ tay** — phạm vi đổi tên chọn theo **kiểu**, không theo tên; quét **mọi** định danh cũ trên **mọi** YAML (kể cả
`propertyPath`, `m_MethodName`); đổi bằng luật máy móc thì áp lên cây gốc rồi so từng blob, file đổi ngoài tập kỳ vọng là
lỗi.
