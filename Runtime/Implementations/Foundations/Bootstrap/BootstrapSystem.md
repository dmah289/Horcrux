# Bootstrap — một con đường khởi tạo duy nhất

`BootstrapRunner` sắp các `BootStep` theo `Order`, await **tuần tự**, và một bước ném exception thì
**vẫn vào được game**. Mỗi nhịp chạy cấp một `CancellationToken` vòng đời: load level mới là mọi loop
async của level trước bị huỷ sạch trước khi bước nào của level mới chạy.

Bước không biết nhau và không biết runner. Runner không biết bước làm gì — nó chỉ biết `BootStep`.

Danh sách bước và giá trị `Order` là **cấu hình trong Inspector**, không phải code.

---

## §1. Bề mặt API

`BootStep` — `Horcrux.Runtime.Abstractions.Bootstrap`, `abstract class BootStep : MonoBehaviour`:

| Thành phần | Chữ ký | Vai trò |
|---|---|---|
| `Order` | `int Order { get; }` | Số **nhỏ chạy trước**. Nguồn duy nhất là field `order` trong Inspector |
| `InitializeAsync` | `abstract UniTask InitializeAsync(CancellationToken ct)` | Cold start, chạy một lần. Nhịp duy nhất **bắt buộc** override |
| `ReinitializeAsync` | `virtual UniTask ReinitializeAsync(CancellationToken ct)` | Mỗi lần load level |
| `AfterReinitialize` | `virtual void AfterReinitialize(CancellationToken ct)` | Sync, chạy sau khi **mọi** bước đã reinit xong |
| `OnAppPause` | `virtual void OnAppPause(bool isPaused)` | Pause đi **ngược**, resume đi **xuôi** |
| `OnAppQuit` | `virtual void OnAppQuit()` | Đi **ngược** |

`BootstrapRunner` — `Horcrux.Runtime.Implementations.Bootstrap`, `sealed class BootstrapRunner : MonoBehaviour, IBootstrapService`:

| Thành phần | Chữ ký | Vai trò |
|---|---|---|
| `InitializeAsync` | `UniTask InitializeAsync()` | Chạy nhịp init. Game gọi **đúng một lần** |
| `ReinitializeAsync` | `UniTask ReinitializeAsync()` | Chạy nhịp reinit, rồi `AfterReinitialize` của mọi bước |
| `ProgressChanged` | `event Action<BootProgress>` | Bắn **trước** mỗi bước, cộng một nhịp đóng ở cuối |
| `IsInitialized` | `bool { get; }` | Latch "cold boot xong" |
| `UntilInitializedAsync` | `UniTask UntilInitializedAsync(CancellationToken ct = default)` | Chờ cold boot xong |

`IBootstrapService : IService<IBootstrapService>` — cửa cho hệ ngoài, đúng **2 member**: `IsInitialized`
và `UntilInitializedAsync`. `ProgressChanged` **không** nằm trên interface (§8).

`BootProgress` — `readonly struct`, payload của `ProgressChanged`:

| Thành phần | Chữ ký | Nội dung |
|---|---|---|
| `StepIndex` | `readonly int` | Index của bước **sắp chạy**. Bằng `StepCount` ở nhịp đóng |
| `StepCount` | `readonly int` | Tổng số bước của nhịp này |
| `StepName` | `readonly string` | Tên GameObject của bước. Rỗng ở nhịp đóng |
| `Ratio01` | `float { get; }` | `StepIndex / StepCount`, trong `[0..1]`. `StepCount <= 0` trả `1f` |
| `IsFinished` | `bool { get; }` | True ở nhịp đóng |

| File | Nội dung |
|---|---|
| `Abstractions/Foundations/Bootstrap/BootStep.cs` | `BootStep` |
| `Abstractions/Foundations/Bootstrap/BootProgress.cs` | `BootProgress` |
| `Abstractions/Foundations/Bootstrap/IBootstrapService.cs` | `IBootstrapService` |
| `Implementations/Foundations/Bootstrap/BootstrapRunner.cs` | `BootstrapRunner` |

Runner đăng ký bằng `[Service(typeof(IBootstrapService), FindFromScene = true)]` — `Sisus.Init` tìm nó
trong scene, không cần singleton. `Awake` tự gọi `DontDestroyOnLoad`.

---

## §2. Luồng dữ liệu

```
   Inspector                 Awake                    InitializeAsync() / ReinitializeAsync()
┌──────────────┐      ┌─────────────────┐      ┌──────────────────────────────────────────┐
│ steps: List  │      │ Preserve() task │      │ ① huỷ + dispose token cũ                 │
│  ├ step.order│─────>│ bỏ ô null (log) │─────>│ ② chờ isPhaseRunning == false            │
│  ├ step.order│      │ sort ổn định    │      │ ③ token mới                              │
│  └ …         │      │  (Order, idx)   │      ├──────────────────────────────────────────┤
└──────────────┘      └─────────────────┘      │ for i in 0..n-1:                         │
                                               │   ProgressChanged(i, n, step.name) ──────┼──> splash
                                               │   await step[i].<nhịp>(ct)               │
                                               │     ├ cancel khi ct huỷ ⇒ dừng êm nhịp   │
                                               │     └ exception khác    ⇒ log, đi tiếp   │
                                               │   ProgressChanged(n, n, "") ─────────────┼──> splash
                                               └──────────────────┬───────────────────────┘
                                                                  │
                                      nhịp reinit ⇒ AfterReinitialize(ct)
                                      nhịp init   ⇒ IsInitialized = true, nhả UntilInitializedAsync()
```

Lần theo **một** giá trị từ đầu tới cuối — số `order` của một bước:

| Chặng | Nó thành cái gì |
|---|---|
| Tác giả gán trong Inspector | field `order` của một `BootStep` |
| `Awake` của runner | `step.Order` là nửa đầu khoá sort; vị trí trong `steps` là nửa sau |
| Sau khi sort | vị trí `i` của bước trong `steps` — **đây chính là thứ tự chạy** |
| Trước mỗi bước | `new BootProgress(i, stepCount, step.name)` |
| Ra khỏi hệ | `Ratio01 = i / stepCount` cho thanh bar, `StepName` cho nhãn splash |
| Nhịp đóng | `BootProgress(n, n, "")` ⇒ `Ratio01 == 1f`, `IsFinished == true` |

`Ratio01` là "đã xong bao nhiêu bước", nên bước đầu **đang chạy** thì tỉ lệ vẫn là 0.

---

## §3. Use case

### 3.1. Viết một bước

Kế thừa `BootStep`, override đúng những nhịp cần dùng. `InitializeAsync` là nhịp duy nhất bắt buộc.

```csharp
public sealed class RemoteConfigBootStep : BootStep
{
    public override async UniTask InitializeAsync(CancellationToken ct)
    {
        await FetchAsync(ct);   // ct đi xuống MỌI await bên trong
    }

    public override UniTask ReinitializeAsync(CancellationToken ct)
    {
        RefreshLoopAsync(ct).Forget();   // loop của level này chết cùng ct
        return UniTask.CompletedTask;
    }
}
```

**Hai luật của một bước:**

| Luật | Vì sao |
|---|---|
| `ct` truyền xuống **mọi** await và mọi loop bên trong | Đây là đường duy nhất để reinit huỷ được việc của level trước. Bước không nhận `ct` thì loop của nó sống qua mọi lần load level |
| `.Forget()` thì tự bọc try/catch trong thân loop | `.Forget()` không có ai bắt exception cho nó. Fail-open của runner chỉ phủ phần **await được** của bước |

Nhịp nào chọn: chỉ chạy một lần (Firebase, Ads, RemoteConfig) ⇒ chỉ `InitializeAsync`. Có state phải
dựng lại mỗi level ⇒ thêm `ReinitializeAsync`. Cần **đọc state của bước khác** ⇒ `AfterReinitialize`,
vì lúc đó mọi bước đã reinit xong.

### 3.2. Editor setup

1. Một GameObject trong scene đầu, add `BootstrapRunner`.
2. Mỗi bước là một component trong scene — đặt làm **con của runner**, để nó đi theo `DontDestroyOnLoad`
   (§7: bước bị destroy khi load scene là một lỗi thoát ra khỏi đường fail-open).
3. Kéo mọi bước vào field `steps` của runner, gán `order` trên từng bước.

Lúc Play, danh sách `steps` trong Inspector là danh sách **đã sort** — đó là thứ tự chạy thật, đọc
được ngay tại đó.

### 3.3. Gọi hai nhịp

Runner **không tự chạy**. Game quyết thời điểm, vì nó phải phối hợp với splash và các prompt hệ điều hành.

```csharp
[SerializeField] private BootstrapRunner bootstrap;

private async UniTaskVoid Start()
{
    bootstrap.ProgressChanged += OnProgress;
    await bootstrap.InitializeAsync();     // cold start, đúng một lần cả đời app
    await bootstrap.ReinitializeAsync();   // level đầu
}
```

Mỗi lần load lại level: `await bootstrap.ReinitializeAsync()` — thường từ hook giữa transition của
scene flow, để việc huỷ token xảy ra lúc màn hình đã bị che.

### 3.4. Splash nghe progress

```csharp
private void OnProgress(BootProgress p)
{
    bar.fillAmount = p.Ratio01;
    label.text = p.IsFinished ? "Done" : p.StepName;
}

private void OnDestroy() => bootstrap.ProgressChanged -= OnProgress;
```

`-=` là **bắt buộc**: runner sống qua mọi scene, splash thì không. Bỏ `-=` là mỗi nhịp sau đó bắn vào
một object đã destroy — exception bị log rồi bỏ qua, nên nó chỉ hiện ra dưới dạng Console ồn.

### 3.5. Hệ ngoài chờ cold boot

```csharp
if (IBootstrapService.TryGet(out IBootstrapService bootstrap))
    await bootstrap.UntilInitializedAsync(ct);
```

`TryGet` cho nhánh degrade khi dự án không dùng runner. `IService<IBootstrapService>.Service` là bản
throw khi thiếu — dùng khi thiếu runner đúng là lỗi cấu hình.

---

## §4. Thứ tự chạy

Sort xảy ra **một lần**, trong `Awake`, ngay trên chính list `steps`. Khoá kép:

```
(Order tăng dần, index gốc trong Inspector tăng dần)
```

| Input (thứ tự Inspector) | Thứ tự chạy |
|---|---|
| `[A(0), B(10), C(0)]` | `A → C → B`, **mọi** lần chạy đều vậy |

Ô null trong `steps` bị log `[Bootstrap] : Step at index {i} is null` rồi **loại khỏi danh sách** —
không phải lỗi chết, các bước còn lại chạy đủ.

**Hai bất biến của mục này:**

| Bất biến | Được giữ bằng |
|---|---|
| ① Chiều ưu tiên khai ở **đúng một chỗ** | `Order` không `virtual`, không ai đè được. Chiều "số nhỏ trước" viết ở XML doc của `Order` cho người đọc code và ở Tooltip của `order` cho người gán số. Đổi chiều là phải đổi cả hai chỗ **và** comparator |
| ② Trùng `Order` vẫn có thứ tự **xác định** | Nửa sau của khoá là index gốc. Không dựa vào tính ổn định của `List<T>.Sort` — nó không có tính đó |

Sort tại chỗ có một đánh đổi đã nhận: thứ tự tác giả gán trong Inspector **không còn** sau `Awake`. Bù
lại không tồn tại list thứ hai phải giữ khớp với list gốc, và mỗi lần chạy đều sort lại từ dữ liệu vừa
deserialize nên kết quả không tích luỹ qua các lần chạy.

---

## §5. Token vòng đời và luật không chạy chồng

Mở một nhịp mới luôn đi đúng ba bước, theo thứ tự đó:

| Bước | Việc | Vì sao phải đúng thứ tự này |
|---|---|---|
| ① | Huỷ + dispose token cũ, tạo token mới | Nhịp cũ phải biết mình bị thay **trước khi** ai chờ nó |
| ② | `while (isPhaseRunning) await UniTask.Yield()` | Bước 5 của nhịp cũ chạy song song bước 0 của nhịp mới là state đan xen |
| ③ | `isPhaseRunning = true`, trả token | — |

Nhịp cũ thoát ở **await kế tiếp của chính nó**: bước đang chạy không truyền `ct` xuống thì nó vẫn chạy
tới hết bước hiện tại rồi vòng lặp mới dừng ở bước sau.

Giữa lúc kiểm `isPhaseRunning` và lúc set nó thành `true` **không có await nào** — đó là thứ làm cho hai
lời gọi trong cùng frame không cùng lọt qua. Chèn một await vào giữa hai dòng đó là mở lại đúng lỗi
chạy chồng mà mục này tồn tại để chặn.

Token chết ở ba chỗ: nhịp mới tiếp quản · `OnDestroy` · `OnApplicationQuit` — chỗ cuối huỷ token **sau
khi** hook `OnAppQuit` của mọi bước đã chạy, vì hook cần token còn sống để flush.

---

## §6. Bảo đảm

| Bảo đảm | Nội dung |
|---|---|
| Thứ tự | `(Order, index Inspector)`. Lặp lại y hệt mọi lần chạy |
| Tuần tự | Bước `i+1` chỉ bắt đầu sau khi await của bước `i` xong. Không có song song |
| Bước ném exception | Log tên bước + tên nhịp + exception, **đi tiếp** bước kế. Nhịp vẫn tính là xong |
| Bước ném `OperationCanceledException` khi token đã huỷ | Dừng êm **cả nhịp**, không log như lỗi. Đây là nhịp mới tiếp quản, không phải bước hỏng |
| Hai nhịp chạy chồng | Không xảy ra (§5) |
| `IsInitialized` | Latch một chiều. Reinit không reset. Bước lỗi vẫn tính là đã chạy |
| `UntilInitializedAsync` | Await được **nhiều lần**, bởi **nhiều consumer**. Đã xong thì trả về ngay |
| `ct` của consumer | Huỷ chỉ lời chờ đó. Boot vẫn chạy tiếp |
| `AfterReinitialize` | Chỉ chạy khi nhịp reinit không bị huỷ, và chỉ sau khi mọi bước đã reinit xong |
| Hook pause/quit | try/catch quanh **từng** bước. Một bước lỗi không chặn các bước còn lại |
| Listener của `ProgressChanged` | Exception bị log, không thoát ra runner. Các listener khác chạy đủ |
| Ô null trong `steps` | Log rồi loại ở `Awake` (§4) |
| `Ratio01` | Luôn trong `[0..1]`. Phép chia nằm đúng một chỗ nên mọi splash lấp bar giống nhau |

---

## §7. Giới hạn

| Giới hạn | Hệ quả |
|---|---|
| **Runner không tự chạy** | Không có `Start()` gọi init. Không ai gọi `InitializeAsync()` thì `UntilInitializedAsync` treo vĩnh viễn |
| **Thiết kế cho MỘT call site** | Ba lời gọi xếp hàng thì lời ở giữa không bị loại: nó chạy bằng token mới nhất, và lời sau nó **dùng lại** đúng token đó thay vì được cấp token mới. Người gọi phải là một — scene flow của game |
| **Không chặn gọi `InitializeAsync` hai lần** | Lần hai chạy lại toàn bộ chuỗi. `IsInitialized` là latch, không phải guard |
| **Bước phải sống cùng đời runner** | Runner `DontDestroyOnLoad`, bước thì không tự động. Bước bị destroy lúc load scene làm `steps` giữ reference chết ⇒ nhịp sau ném `MissingReferenceException` **thoát ra khỏi** đường fail-open, vì đọc `step.name` để log cũng ném. Đặt bước làm con của runner (§3.2) |
| **Hook pause/quit im lặng trước khi init xong** | Cả hai chặn bằng `IsInitialized`. Quit giữa lúc đang boot: không bước nào được flush |
| **Nhịp bị huỷ không có nhịp đóng** | `ProgressChanged` không bắn `IsFinished` ⇒ splash đứng ở giữa. Nhịp mới bắn lại từ index 0 nên tỉ lệ **tụt về sau** — splash phải chịu được điều đó |
| **`ProgressChanged` không tự nhả** | Runner sống mãi. Listener của scene phải `-=` (§3.4) |
| **Main-thread only** | Không lock quanh `isPhaseRunning`, `steps`, `IsInitialized` |
| **Không có timeout mỗi bước** | Bước treo thì treo cả chuỗi và treo cả splash. Timeout là việc của bước |
| **`steps` chốt ở `Awake`** | Không có API thêm hoặc bớt bước lúc chạy |

---

## §8. Quyết định thiết kế

| Quyết định | Lý do |
|---|---|
| `BootStep` là **abstract MonoBehaviour**, không interface | Bước phải serialize được vào `List<>` trong Inspector (Editor-first). Interface cần thêm máy móc reference chưa tồn tại trong SDK |
| `Order` có **một nguồn**: field serialize, property **không virtual** | Cho override `Order` ở code là tạo nguồn sự thật thứ hai, và tài liệu buộc phải chép lại một bảng "Order thật" |
| Sort **tại chỗ** trên `steps`, không giữ list thứ hai | Hai list buộc khớp là chỗ lệch. `Awake` sort lại từ dữ liệu vừa deserialize nên vẫn xác định (§4) |
| Chỉ `InitializeAsync` là `abstract`, phần còn lại `virtual` rỗng | Bước chỉ-init-một-lần là ca phổ biến nhất — không ép implement nhịp không dùng |
| Hook tên `OnAppPause` / `OnAppQuit`, không phải tên Unity | Xem "đã sai một lần" dưới đây |
| Pause đi **ngược**, resume đi **xuôi** | Pause là "quit không hẹn trước" trên mobile: hệ trên ghi vào hệ nền xong, hệ nền mới chốt sổ. Resume là init-nhẹ: hệ nền tỉnh trước, hệ trên tính dựa vào nó sau |
| `ProgressChanged` **không** nằm trên `IBootstrapService` | Hệ ngoài chỉ cần "xong chưa / chờ xong". Splash nằm cùng scene với runner nên wire thẳng reference concrete — nhận vào một class cụ thể vẫn là "nhận vào" |
| Progress là `BootProgress` **theo bước**, không enum phase cứng | Tên phase là nội dung riêng từng game. Enum cứng bắt mọi game map bước→phase, tức một tri thức trùng phải giữ khớp ở hai nơi. Splash vẫn đủ dữ liệu: tỉ lệ + nhãn |
| `Ratio01` là property trên struct | Splash nào cũng cần đúng phép chia đó. Đo và vẽ suy từ một nguồn |
| `initializedSource.Task.Preserve()` cache một bản ở `Awake` | `UniTask` mặc định chỉ await được **một** lần. `UntilInitializedAsync` bị nhiều consumer await ⇒ phải là bản `Preserve` |
| Cờ `isPhaseRunning` + `UniTask.Yield()`, không lưu `UniTask` của nhịp đang chạy | Cùng ràng buộc trên: task của nhịp cũ không await lại được từ nhịp mới |
| Hai delegate `static readonly` cho hai nhịp | Một vòng chạy dùng chung cho cả hai, và không cấp phát closure mỗi nhịp |
| `RaiseProgress` gọi từng handler trong try/catch riêng | Một listener lỗi không được cắt chuỗi thông báo của các listener sau |
| `[Service(FindFromScene = true)]`, không singleton | Đăng ký qua DI như các service khác của SDK; runner vẫn là object trong scene để wire được trong Inspector |
| `IsInitialized` **không** reset khi reinit | Nó mang nghĩa "cold boot đã xong", không phải "level đã sẵn sàng" |

**Đã sai một lần** — bốn cái sai đã xảy ra thật, và cấu trúc hiện tại làm chúng không xảy ra lại được:

| Cái sai | Cấu trúc chặn nó |
|---|---|
| Hook virtual đặt tên `OnApplicationPause` ngay trên base class của bước. Unity gọi magic method trên **mọi** MonoBehaviour trùng tên, bất kể access modifier ⇒ mỗi bước chạy hook **hai lần**: Unity gọi thẳng, cộng fan-out của runner. Sai âm thầm vì phần lớn handler idempotent | Hook tên `OnAppPause` / `OnAppQuit` — Unity không biết tên này, chỉ runner gọi |
| Hai bước trùng ưu tiên đổi chỗ nhau giữa hai lần chạy. `List<T>.Sort` là introsort, **không ổn định** — bug "lúc được lúc không", khó tái lập nhất | Khoá kép có index gốc (§4, bất biến ②) |
| Hai entry point sort **ngược chiều nhau** trên cùng một trường ưu tiên | Một comparator duy nhất, trong runner. Chiều khai ở `Order` + Tooltip (§4, bất biến ①) |
| `.Forget()` trần nuốt exception của bước ⇒ loading treo vĩnh viễn | Fail-open có log: bước lỗi được nêu tên, chuỗi đi tiếp |

---

## §9. Cố ý không có

| Không có | Lý do | Thêm lại khi |
|---|---|---|
| Log liệt kê thứ tự bước sau khi sort | Danh sách `steps` trong Inspector lúc Play **là** danh sách đã sort — mở ra thấy đủ | Cần đọc thứ tự trên build device, chỗ không mở được Inspector |
| Manifest data-driven (ScriptableObject) | Chưa ai cần đổi thứ tự bước mà không compile | Thêm overload nhận config asset — additive, không sửa chữ ký cũ |
| Chạy song song các bước cùng pha | Cold start chưa đo được là chậm | Đo ra được bước nào chờ I/O song song được; thêm cờ trên contract + `WhenAll` |
| Auto-discovery — quét scene hoặc reflection để tìm bước | Magic khó debug. Danh sách trong Inspector lộ đủ | — |
| Expose token vòng đời ra property public | Mọi bước nhận token qua tham số của nhịp. Chưa hệ nào ngoài chuỗi bước cần nó | Xuất hiện hệ ngoài cần đúng token vòng đời của level |
| Reset `IsInitialized` khi reinit | Nó là latch "cold boot xong" một chiều, và consumer cần đúng nghĩa đó | — |
| `UntilReinitializedAsync` — bản đối xứng cho nhịp reinit | Cold boot là **latch một chiều**: xong rồi xong mãi nên người tới muộn hỏi được. "Level sẵn sàng" là **cạnh lặp lại**: cùng một `true` lúc nghĩa là level này, lúc là level trước — sai âm thầm. Chỗ biết chắc là nơi gọi: `await ReinitializeAsync()` (§3.3). Bước trong chuỗi thì dùng `AfterReinitialize` | Có consumer **ngoài** chuỗi bước và **không phải** call site. Khi đó cần thêm dấu "level đã cũ", không chỉ thêm hàm chờ |
| `IOptionalService<T>` — biến thể service "thiếu là hợp lệ", không có accessor throw | `IBootstrapService` dùng `IService<T>`; consumer gọi `TryGet` là đã tự có nhánh degrade | Cần **compiler** chặn không cho consumer viết nhánh throw |
| Timeout mỗi bước | Timeout hợp lý là con số riêng của từng bước, runner không biết | — |
| Retry bước lỗi | Bước biết cái gì retry được, runner không biết. Fail-open đưa quyết định về đúng chỗ đó | — |
| Guard chặn `InitializeAsync` gọi lần hai | Chỉ có một call site (§7), thêm guard là đỡ một cái sai chưa xảy ra | Có hơn một chỗ trong game gọi nhịp init |
| Nhịp async "trước khi quit" | `OnApplicationQuit` của Unity là sync, không chờ await được | — |

---

## §10. Chẩn đoán

| Triệu chứng | Nguyên nhân thường gặp |
|---|---|
| Bước chạy sai thứ tự mong đợi | Hai bước trùng `Order` ⇒ thứ tự là thứ tự trong Inspector (§4) · hoặc `order` sửa trên prefab mà bản trong scene có giá trị override |
| `[Bootstrap] : Step at index N is null` | Ô trống trong `steps`, hoặc reference chết. Bước đó bị loại, chuỗi vẫn chạy |
| `[Bootstrap] : X failed at Initialize. Skip (fail-open)` | Bước `X` ném exception; chuỗi đi tiếp. Exception thật nằm ở dòng log ngay sau |
| Splash đứng lại ở giữa | Nhịp bị huỷ giữa chừng nên không có nhịp đóng (§7) |
| Splash tụt tỉ lệ về 0 rồi chạy lại | Một nhịp mới đã tiếp quản nhịp đang chạy |
| `UntilInitializedAsync` ném `OperationCanceledException` | Runner bị destroy — nó nhả mọi consumer đang chờ · hoặc `ct` của chính consumer bị huỷ |
| `UntilInitializedAsync` không bao giờ trả về | Chưa ai gọi `InitializeAsync()` · hoặc nhịp init bị huỷ trước khi tới cuối nên latch không bật |
| `MissingReferenceException` thoát ra từ trong runner | Một bước đã bị destroy nhưng còn trong `steps` (§7) |
| `OnAppPause` / `OnAppQuit` không được gọi | `IsInitialized` còn `false` — cold boot chưa xong (§7) |
| Hook app chạy hai lần mỗi bước | Bước tự khai method tên `OnApplicationPause` hoặc `OnApplicationQuit`. Đổi sang override `OnAppPause` / `OnAppQuit` (§8) |
| Loop async của level trước vẫn chạy sau khi reload | Bước không truyền `ct` xuống loop của nó (§3.1) |
| Bước reinit đọc state của bước khác ra giá trị cũ | Việc đó thuộc `AfterReinitialize`, không thuộc `ReinitializeAsync` |
| Console ồn exception từ splash sau khi đổi scene | Thiếu `-=` cho `ProgressChanged` (§3.4) |

---

## §11. Nghiệm thu

Chưa có scene demo trong SDK — các phép kiểm dưới đây chạy trong scene thật của game.

| Tiêu chí | Phép kiểm | Kỳ vọng |
|---|---|---|
| Thứ tự lặp lại được | 3 bước, hai trong đó trùng `Order`. Vào Play 3 lần, đọc list `steps` trong Inspector | Ba lần cho cùng một thứ tự, và trùng `Order` thì bước đứng trước trong Inspector đứng trước |
| Một bước lỗi vẫn vào được game | `throw` trong `InitializeAsync` của bước giữa | Console có `Skip (fail-open)` + exception · các bước sau vẫn chạy · `IsInitialized == true` |
| Reinit huỷ sạch level trước | Bước có `while (!ct.IsCancellationRequested)` in log mỗi giây. Gọi `ReinitializeAsync()` hai lần liên tiếp | Chỉ còn **một** loop in log |
| Hai nhịp không chạy chồng | Gọi `ReinitializeAsync()` hai lần trong cùng frame, mỗi bước in tên mình | Log không đan xen hai chuỗi |
| Hook app chạy đúng một lần | `Debug.Log` trong `OnAppPause` của một bước, bấm pause trong Editor | Đúng **một** dòng log mỗi bước |
| Progress đủ cho splash | Subscribe `ProgressChanged`, in `Ratio01` và `StepName` | Tỉ lệ đi từ 0 lên 1 · `IsFinished` đúng một lần mỗi nhịp · nhãn khớp tên GameObject của bước |
| Ô null không làm chết chuỗi | Để một ô `steps` trống | Một `LogError` · các bước còn lại chạy đủ |

---

## §12. Bảng metrics

| Phép đo | Giá trị | Ghi chú |
|---|---|---|
| Sort | O(n log n), `n` = số bước | Một lần, trong `Awake` |
| Một nhịp | O(n) + chi phí thật của từng bước | Tuần tự, không song song |
| Alloc ở `Awake` | 1 `List<(BootStep, int)>` + 1 bản `Preserve` của task | Một lần cả đời app |
| Alloc mỗi nhịp | 1 `CancellationTokenSource` | Bản cũ được `Dispose` |
| Alloc mỗi bước | 1 `Delegate[]` từ `GetInvocationList()` | Chỉ khi `ProgressChanged` có listener. Không listener ⇒ 0 |
| Alloc lúc chờ nhịp cũ thoát | 0 | `UniTask.Yield()` |
| Alloc của `UntilInitializedAsync` | 0 khi đã init xong hoặc khi `ct` không huỷ được · 1 khi có `ct` thật | `AttachExternalCancellation` mới là chỗ cấp phát |
| Delegate cho hai nhịp | 2, `static readonly` | Tạo một lần cho cả đời app |
| Alloc trên đường lỗi | string format của log | Chỉ khi có bước lỗi hoặc ô null |
| Ngân sách thiết kế | cold start **1 lần** + reinit **mỗi lần load level** | **Không** phải hot path — mọi chỗ chọn bản dễ đọc |
