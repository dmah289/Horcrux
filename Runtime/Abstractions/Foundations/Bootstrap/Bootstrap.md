# Bootstrap — một con đường khởi tạo duy nhất

`BootstrapRunner` sắp các `BootStep` theo `Order`, await **tuần tự**, và một bước ném exception thì
**vẫn vào được game**. Mỗi nhịp cấp một `CancellationToken` vòng đời: load level mới là mọi loop async
của level trước bị huỷ sạch trước khi bước nào của level mới chạy.

Danh sách bước và giá trị `Order` là **cấu hình trong Inspector**, không phải code.

---

## §1. Bề mặt API

```csharp
// Abstractions/Foundations/Bootstrap
abstract class BootStep : MonoBehaviour
{
    int Order { get; }                                        // nguồn duy nhất: field `order` Inspector
    abstract UniTask InitializeAsync(CancellationToken ct);   // nhịp duy nhất BẮT BUỘC override
    virtual  UniTask ReinitializeAsync(CancellationToken ct);
    virtual  void    AfterReinitialize(CancellationToken ct); // sau khi MỌI bước đã reinit xong
    virtual  void    OnAppPause(bool isPaused);
    virtual  void    OnAppQuit();
}

readonly struct BootProgress                       // payload của ProgressChanged
{
    int    StepIndex;             // bước SẮP chạy; == StepCount ở nhịp đóng
    int    StepCount;
    string StepName;              // tên GameObject của bước; rỗng ở nhịp đóng
    float  Ratio01    { get; }    // StepIndex / StepCount; StepCount <= 0 ⇒ 1f
    bool   IsFinished { get; }    // StepIndex >= StepCount
}

interface IBootstrapService : IService<IBootstrapService>     // cửa duy nhất cho hệ ngoài
{
    bool    IsInitialized { get; }
    UniTask UntilInitializedAsync(CancellationToken ct = default);
}

// Implementations/Foundations/Bootstrap
[Service(typeof(IBootstrapService), FindFromScene = true)]    // Sisus.Init tìm trong scene
sealed class BootstrapRunner : MonoBehaviour, IBootstrapService
{
    event Action<BootProgress> ProgressChanged;   // trước MỖI bước, cộng một nhịp đóng
    UniTask InitializeAsync();                    // game gọi đúng một lần
    UniTask ReinitializeAsync();                  // mỗi lần load level
}
```

Chiều chạy qua danh sách bước — hai hook đi **ngược**:

```
cold boot   ──> InitializeAsync(ct)     async   0 ──────> n-1
level load  ──> ReinitializeAsync(ct)   async   0 ──────> n-1
                AfterReinitialize(ct)   sync    0 ──────> n-1
app pause   ──> OnAppPause(true)        sync  n-1 ──────>   0   NGƯỢC
app resume  ──> OnAppPause(false)       sync    0 ──────> n-1
app quit    ──> OnAppQuit()             sync  n-1 ──────>   0   NGƯỢC
```

---

## §2. Luồng dữ liệu

```
   Inspector                 Awake                    InitializeAsync() / ReinitializeAsync()
┌──────────────┐      ┌─────────────────┐      ┌──────────────────────────────────────────┐
│ steps: List  │      │ Preserve() task │      │ ① huỷ + dispose token cũ                 │
│  ├ step.order│─────>│ sort ổn định    │─────>│ ② chờ isPhaseRunning == false            │
│  ├ step.order│      │  (Order, idx)   │      │ ③ token mới                              │
│  └ …         │      └─────────────────┘      ├──────────────────────────────────────────┤
└──────────────┘                               │ for i in 0..n-1:                         │
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

Số `order` trong Inspector thành nửa đầu khoá sort, rồi thành vị trí `i` trong `steps` — **vị trí đó
chính là thứ tự chạy**, và cũng chính là `StepIndex` mà splash nhận. `Ratio01` đo "đã xong bao nhiêu
bước", nên lúc bước đầu **đang chạy** tỉ lệ vẫn là 0.

---

## §3. Use case

### 3.1. Viết một bước

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

| Bước của anh | Override |
|---|---|
| Chỉ chạy một lần cả đời app — Firebase, Ads, RemoteConfig | `InitializeAsync` |
| Có state phải dựng lại mỗi level | + `ReinitializeAsync` |
| Cần **đọc state của bước khác** | + `AfterReinitialize` |
| Phải flush khi app xuống nền hoặc thoát | + `OnAppPause` / `OnAppQuit` |

**Hai luật của một bước:**

| Luật | Vì sao |
|---|---|
| `ct` truyền xuống **mọi** await và mọi loop bên trong | Đây là đường duy nhất để reinit huỷ được việc của level trước. Bước không nhận `ct` thì loop của nó sống qua mọi lần load level |
| `.Forget()` thì tự bọc try/catch trong thân loop | `.Forget()` không có ai bắt exception cho nó. Fail-open của runner chỉ phủ phần **await được** của bước |

### 3.2. Editor setup

1. Một GameObject trong scene đầu, add `BootstrapRunner`.
2. Mỗi bước là một component — đặt làm **con của runner** để đi theo `DontDestroyOnLoad` (§6).
3. Kéo mọi bước vào field `steps`, gán `order` trên từng bước.

Lúc Play, `steps` trong Inspector là danh sách **đã sort** — đó là thứ tự chạy thật.

### 3.3. Gọi hai nhịp

Runner **không tự chạy**. Game quyết thời điểm, vì nó phải phối hợp với splash và prompt hệ điều hành.

```csharp
[SerializeField] private BootstrapRunner bootstrap;

private async UniTaskVoid Start()
{
    bootstrap.ProgressChanged += OnProgress;
    await bootstrap.InitializeAsync();     // cold start, đúng một lần cả đời app
    await bootstrap.ReinitializeAsync();   // level đầu
}
```

Mỗi lần load lại level: `await bootstrap.ReinitializeAsync()`, thường từ hook giữa transition của
scene flow để việc huỷ token xảy ra lúc màn hình đã bị che. **Await xong là level đã sẵn sàng** —
đây là chỗ duy nhất biết chắc điều đó (§8).

### 3.4. Splash nghe progress

```csharp
private void OnProgress(BootProgress p)
{
    bar.fillAmount = p.Ratio01;
    label.text = p.IsFinished ? "Done" : p.StepName;
}

private void OnDestroy() => bootstrap.ProgressChanged -= OnProgress;
```

### 3.5. Hệ ngoài chờ cold boot

```csharp
if (IBootstrapService.TryGet(out IBootstrapService bootstrap))
    await bootstrap.UntilInitializedAsync(ct);
```

`TryGet` khi thiếu runner là hợp lệ; `IService<IBootstrapService>.Service` khi thiếu runner là lỗi cấu hình.

---

## §4. Hai luật thứ tự

### 4.1. Giữa các bước — sort một lần ở `Awake`, khoá kép `(Order, index gốc Inspector)`

| Input (thứ tự Inspector) | Thứ tự chạy |
|---|---|
| `[A(0), B(10), C(0)]` | `A → C → B`, **mọi** lần chạy đều vậy |

| Bất biến | Được giữ bằng |
|---|---|
| ① Chiều ưu tiên khai ở **đúng một chỗ** | `Order` không `virtual`. Chiều "số nhỏ trước" viết ở XML doc của `Order` cho người đọc code, và ở Tooltip của `order` cho người gán số. Đổi chiều là đổi cả hai chỗ **và** comparator |
| ② Trùng `Order` vẫn có thứ tự **xác định** | Nửa sau của khoá là index gốc — không dựa vào `List<T>.Sort`, vì nó **không** ổn định |

### 4.2. Giữa các nhịp — không bao giờ chạy chồng

Nhịp mới huỷ token cũ **trước** khi chờ nhịp cũ thoát: nhịp cũ phải biết mình bị thay trước khi có ai
chờ nó. Nó thoát ở **await kế tiếp của chính nó**, nên bước đang chạy mà không truyền `ct` xuống thì
vẫn chạy hết bước hiện tại.

Giữa lúc kiểm `isPhaseRunning` và lúc set nó thành `true` **không có await nào** — đó là thứ làm hai
lời gọi trong cùng frame không cùng lọt qua. Chèn một await vào giữa hai dòng đó là mở lại đúng lỗi
chạy chồng mà cơ chế này tồn tại để chặn.

Token chết ở ba chỗ: nhịp mới tiếp quản · `OnDestroy` · `OnApplicationQuit` — chỗ cuối huỷ token **sau
khi** hook `OnAppQuit` của mọi bước đã chạy, vì hook cần token còn sống để flush.

---

## §5. Nghiệm thu

Chưa có scene demo trong SDK — các phép kiểm chạy trong scene thật của game.

| Bảo đảm | Phép kiểm | Kỳ vọng |
|---|---|---|
| Thứ tự lặp lại được | 3 bước, hai trong đó trùng `Order`. Vào Play 3 lần, đọc `steps` trong Inspector | Ba lần cùng một thứ tự; trùng `Order` thì bước đứng trước trong Inspector đứng trước |
| Bước lỗi vẫn vào được game | `throw` trong `InitializeAsync` của bước giữa | Console có `Skip (fail-open)` + exception · bước sau vẫn chạy · `IsInitialized == true` |
| Reinit huỷ sạch level trước | Bước có `while (!ct.IsCancellationRequested)` in log mỗi giây. Gọi `ReinitializeAsync()` hai lần liên tiếp | Chỉ còn **một** loop in log |
| Hai nhịp không chạy chồng | Gọi `ReinitializeAsync()` hai lần trong cùng frame, mỗi bước in tên mình | Log không đan xen hai chuỗi |
| Hook app chạy đúng một lần | `Debug.Log` trong `OnAppPause` của một bước, bấm pause trong Editor | Đúng **một** dòng log mỗi bước |
| Progress đủ cho splash | Subscribe `ProgressChanged`, in `Ratio01` và `StepName` | Tỉ lệ đi 0→1 · `IsFinished` đúng một lần mỗi nhịp · nhãn khớp tên GameObject |
| Nhiều consumer chờ cold boot | Hai chỗ cùng `await UntilInitializedAsync()`, một chỗ gọi sau khi đã init xong | Cả ba đều trả về, không chỗ nào ném |

---

## §6. Bẫy phải biết

| Bẫy | Hệ quả |
|---|---|
| **Runner không tự chạy** | Không ai gọi `InitializeAsync()` thì `UntilInitializedAsync` treo vĩnh viễn |
| **Bước phải sống cùng đời runner** | Runner `DontDestroyOnLoad`, bước thì không tự động. Bước bị destroy lúc load scene làm `steps` giữ reference chết ⇒ nhịp sau ném `MissingReferenceException` **thoát ra khỏi** đường fail-open, vì đọc `step.name` để log cũng ném. Đặt bước làm con của runner (§3.2) |
| **Thiết kế cho MỘT call site** | Ba lời gọi xếp hàng thì lời ở giữa không bị loại: nó chạy bằng token mới nhất, và lời sau nó **dùng lại** đúng token đó thay vì được cấp token mới. Người gọi phải là một — scene flow của game |
| **Nhịp bị huỷ không có nhịp đóng** | Splash đứng ở giữa, rồi nhịp mới bắn lại từ index 0 nên tỉ lệ **tụt về sau** — splash phải chịu được điều đó |
| **`ProgressChanged` không tự nhả** | Runner sống qua mọi scene, splash thì không. Thiếu `-=` là Console ồn exception sau mỗi lần đổi scene |
| **Hook pause/quit im lặng trước khi init xong** | Cả hai chặn bằng `IsInitialized`. Quit giữa lúc đang boot: không bước nào được flush |

---

## §7. Quyết định thiết kế

| Quyết định | Lý do |
|---|---|
| `BootStep` là **abstract MonoBehaviour**, không interface | Bước phải serialize được vào `List<>` trong Inspector (Editor-first). Interface cần thêm máy móc reference chưa tồn tại trong SDK |
| `Order` có **một nguồn**: field serialize, property **không virtual** | Cho override `Order` ở code là tạo nguồn sự thật thứ hai, và tài liệu buộc phải chép lại một bảng "Order thật" |
| Sort **tại chỗ** trên `steps`, không giữ list thứ hai | Hai list buộc khớp là chỗ lệch. Đánh đổi đã nhận: thứ tự tác giả gán trong Inspector không còn sau `Awake`. Bù lại mỗi lần chạy đều sort lại từ dữ liệu vừa deserialize nên kết quả không tích luỹ |
| Pause đi **ngược**, resume đi **xuôi** | Pause là "quit không hẹn trước" trên mobile: hệ trên ghi vào hệ nền xong, hệ nền mới chốt sổ. Resume là init-nhẹ: hệ nền tỉnh trước, hệ trên tính dựa vào nó sau |
| `ProgressChanged` **không** nằm trên `IBootstrapService` | Hệ ngoài chỉ cần "xong chưa / chờ xong". Splash nằm cùng scene với runner nên wire thẳng reference concrete |
| Progress là `BootProgress` **theo bước**, không enum phase cứng | Tên phase là nội dung riêng từng game. Enum cứng bắt mọi game map bước→phase, tức một tri thức trùng phải giữ khớp ở hai nơi. Splash vẫn đủ dữ liệu: tỉ lệ + nhãn |
| `initializedSource.Task.Preserve()` cache một bản ở `Awake` | `UniTask` mặc định chỉ await được **một** lần. `UntilInitializedAsync` bị nhiều consumer await ⇒ phải là bản `Preserve` |
| Cờ `isPhaseRunning` + `UniTask.Yield()`, không lưu `UniTask` của nhịp đang chạy | Cùng ràng buộc trên: task của nhịp cũ không await lại được từ nhịp mới |
| `IsInitialized` **không** reset khi reinit | Nó mang nghĩa "cold boot đã xong", không phải "level đã sẵn sàng" |

**Đã sai một lần** — bốn cái sai đã xảy ra thật, và cấu trúc hiện tại làm chúng không xảy ra lại được:

| Cái sai | Cấu trúc chặn nó |
|---|---|
| Hook virtual đặt tên `OnApplicationPause` ngay trên base class của bước. Unity gọi magic method trên **mọi** MonoBehaviour trùng tên, bất kể access modifier ⇒ mỗi bước chạy hook **hai lần**: Unity gọi thẳng, cộng fan-out của runner. Sai âm thầm vì phần lớn handler idempotent | Hook tên `OnAppPause` / `OnAppQuit` — Unity không biết tên này, chỉ runner gọi |
| Hai bước trùng ưu tiên đổi chỗ nhau giữa hai lần chạy. `List<T>.Sort` là introsort, **không ổn định** — bug "lúc được lúc không", khó tái lập nhất | Khoá kép có index gốc (§4.1, bất biến ②) |
| Hai entry point sort **ngược chiều nhau** trên cùng một trường ưu tiên | Một comparator duy nhất, trong runner. Chiều khai ở `Order` + Tooltip (§4.1, bất biến ①) |
| `.Forget()` trần nuốt exception của bước ⇒ loading treo vĩnh viễn | Fail-open có log: bước lỗi được nêu tên, chuỗi đi tiếp |

---

## §8. Cố ý không có

| Không có | Lý do | Thêm lại khi |
|---|---|---|
| `UntilReinitializedAsync` — bản đối xứng cho nhịp reinit | Cold boot là **latch một chiều**: xong rồi xong mãi nên người tới muộn hỏi được. "Level sẵn sàng" là **cạnh lặp lại**: cùng một `true` lúc nghĩa là level này, lúc là level trước — sai âm thầm. Chỗ biết chắc là nơi gọi: `await ReinitializeAsync()` (§3.3). Bước trong chuỗi thì dùng `AfterReinitialize` | Có consumer **ngoài** chuỗi bước và **không phải** call site. Khi đó cần thêm dấu "level đã cũ", không chỉ thêm hàm chờ |
| Timeout và retry cho bước lỗi | Con số timeout hợp lý và thứ retry được đều là tri thức riêng của từng bước, runner không biết. Fail-open đưa quyết định về đúng chỗ đó. Đánh đổi: bước treo thì treo cả chuỗi và treo cả splash | — |
| Guard chặn `InitializeAsync` gọi lần hai | Chỉ có một call site (§6). Đánh đổi: gọi lần hai chạy lại toàn bộ chuỗi — `IsInitialized` là latch, không phải guard | Có hơn một chỗ trong game gọi nhịp init |
| Log liệt kê thứ tự bước sau khi sort | Danh sách `steps` trong Inspector lúc Play **là** danh sách đã sort — mở ra thấy đủ | Cần đọc thứ tự trên build device, chỗ không mở được Inspector |
| Manifest data-driven (ScriptableObject) | Chưa ai cần đổi thứ tự bước mà không compile | Thêm overload nhận config asset — additive, không sửa chữ ký cũ |
| Chạy song song các bước cùng pha | Cold start chưa đo được là chậm | Đo ra được bước nào chờ I/O song song được; thêm cờ trên contract + `WhenAll` |
| Auto-discovery — quét scene hoặc reflection để tìm bước | Magic khó debug. Danh sách trong Inspector lộ đủ | — |
| Expose token vòng đời ra property public | Mọi bước nhận token qua tham số của nhịp. Chưa hệ nào ngoài chuỗi bước cần nó | Xuất hiện hệ ngoài cần đúng token vòng đời của level |
| `IOptionalService<T>` — biến thể service "thiếu là hợp lệ", không có accessor throw | `IBootstrapService` dùng `IService<T>`; consumer gọi `TryGet` là đã tự có nhánh degrade | Cần **compiler** chặn không cho consumer viết nhánh throw |
| Nhịp async "trước khi quit" | `OnApplicationQuit` của Unity là sync, không chờ await được | — |

---

## §9. Chẩn đoán

| Triệu chứng | Nguyên nhân |
|---|---|
| Bước chạy sai thứ tự mong đợi | Trùng `Order` ⇒ thứ tự Inspector quyết (§4.1) · hoặc `order` sửa trên prefab mà bản trong scene có override |
| `[Bootstrap] : X failed at Initialize. Skip (fail-open)` | Bước `X` ném exception, chuỗi đi tiếp. Exception thật ở dòng log ngay sau |
| `MissingReferenceException` thoát ra từ trong runner | Một bước đã bị destroy nhưng còn trong `steps` (§6) |
| Splash đứng lại ở giữa | Nhịp bị huỷ giữa chừng nên không có nhịp đóng (§6) |
| Splash tụt tỉ lệ về 0 rồi chạy lại | Một nhịp mới đã tiếp quản nhịp đang chạy |
| Console ồn exception từ splash sau khi đổi scene | Thiếu `-=` cho `ProgressChanged` (§6) |
| `UntilInitializedAsync` ném `OperationCanceledException` | Runner bị destroy, nó nhả mọi consumer đang chờ · hoặc `ct` của chính consumer bị huỷ |
| `UntilInitializedAsync` không bao giờ trả về | Chưa ai gọi `InitializeAsync()` · hoặc nhịp init bị huỷ trước khi tới cuối nên latch không bật |
| `OnAppPause` / `OnAppQuit` không được gọi | `IsInitialized` còn `false` (§6) |
| Hook app chạy hai lần mỗi bước | Bước tự khai method tên `OnApplicationPause` / `OnApplicationQuit` (§7) |
| Loop async của level trước vẫn chạy sau khi reload | Bước không truyền `ct` xuống loop của nó (§3.1) |
| Bước reinit đọc state của bước khác ra giá trị cũ | Việc đó thuộc `AfterReinitialize`, không thuộc `ReinitializeAsync` |

---

## §10. Bảng metrics

| Phép đo | Giá trị | Ghi chú |
|---|---|---|
| Alloc mỗi bước | 1 `Delegate[]` từ `GetInvocationList()` | Chỉ khi `ProgressChanged` có listener. Không listener ⇒ 0 |
| Alloc của `UntilInitializedAsync` | 0 khi đã init xong hoặc khi `ct` không huỷ được · 1 khi có `ct` thật | `AttachExternalCancellation` mới là chỗ cấp phát |
| Alloc lúc chờ nhịp cũ thoát | 0 | `UniTask.Yield()` |
| Alloc mỗi nhịp | 1 `CancellationTokenSource`; cái của nhịp liền trước bị `Dispose` | Cộng 1 `List` + 1 `Preserve` một lần ở `Awake` |
| Ngân sách thiết kế | cold start **1 lần** + reinit **mỗi lần load level** | **Không** phải hot path — mọi chỗ chọn bản dễ đọc |
