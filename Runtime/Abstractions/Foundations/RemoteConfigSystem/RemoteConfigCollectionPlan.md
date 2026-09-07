# Plan — Đưa domain remote config về assembly của dự án

> **Loại tài liệu:** Plan — developer tự sửa để nắm logic. Không có bước nào cần agent chạy: bốn task dưới
> là bốn lượt sửa file, và mọi phép kiểm đều là thao tác trong Unity.

**Goal:** Dự án khai biến remote config của mình **trong assembly của chính nó**, không cần `.asmref`, không
để một type nào của dự án bị biên dịch vào `com.horcrux.runtime`. Đổi lại, call site vẫn đọc **không cast, có
kiểu đầy đủ**: `IGameRemoteConfig.Service.RetryCount.Value`.

**Cách đạt:** đổi `partial class` thành **kế thừa**. SDK giữ toàn bộ máy móc trong một `abstract class`; dự án
khai một `sealed class` dẫn xuất trong assembly của nó, mang theo field biến, property có kiểu, và ba thứ
plumbing mà SDK không khai hộ được.

```
Contract   IRCVariableCollection             interface thuần — KHÔNG derive IService
           (SDK, Abstractions)

Máy móc    BaseRemoteConfigCollection        abstract · quét field · wire provider · apply · nút Editor
           (SDK, Implementations)

Domain     IGameRemoteConfig                 interface của dự án — derive contract + IService<chính nó>
           GameRemoteConfig                  sealed · [Service] ×2 · [CreateAssetMenu] · field biến · property
           (dự án, com.FelixFelicis)
```

**Tech Stack:** C#, Unity 6000.3, `Sisus.Init` (`[Service]`), Odin Inspector (`[Button]`, `[GUIColor]`),
`System.Reflection`. **Không** thêm package, **không** đụng asmdef nào, **không** toán.

## Ngữ cảnh đã chốt

| Nhóm | Chốt |
|---|---|
| **Ai gọi** | Dự án khai biến trên `GameRemoteConfig` và đọc qua `IGameRemoteConfig.Service`. SDK không gọi collection ở đâu cả — đã grep `Assets` và `Packages`, không có tham chiếu nào tới `IRCVariableCollection` hay `RCVariableCollection` ngoài chính hai file của chúng |
| **Mục tiêu** | Không type nào của dự án nằm trong assembly SDK · không `.asmref` · call site không cast · một asset duy nhất cho cả dự án · dữ liệu đã author trong asset của dự án thật **không mất** |
| **Ngân sách** | Reflection quét field: một lần mỗi phiên, đã có cache. Apply giá trị: một lần mỗi lượt fetch. Không có gì chạy theo frame |
| **Ranh giới** | SDK: contract, máy móc, attribute `RegisteredRCVar`, nút Editor. Dự án: interface có kiểu, class dẫn xuất, field biến, `[Service]`, `[CreateAssetMenu]`, đường dẫn Resources |
| **Hướng phát triển thật** | Hệ Persistence dùng **đúng khuôn này** — xem `PersistencePlan.md`. Nên mọi quyết định ở đây là quyết cho hai hệ, không phải một |

**Những gì cố ý KHÔNG làm, kèm lý do** (*xoá nó đi thì hỏng ở đâu*):

| Không làm | Vì sao |
|---|---|
| Giữ `partial class` và dùng `.asmref` | Chính là thứ đang phải bỏ: nửa partial của dự án buộc phải biên dịch vào assembly SDK, nên type nó gọi tên cũng phải nằm trong đó — và lan theo cả chuỗi phụ thuộc của type đó |
| Cho `IRCVariableCollection` tiếp tục derive `IService<>` | Interface của dự án derive cả nó lẫn `IService<chính mình>` sẽ thừa hưởng **hai** thành viên `Service` → mọi lần đọc `IGameRemoteConfig.Service` là lỗi biên dịch CS0229. Ràng buộc §0.1 |
| Khai `[Service]` trên `BaseRemoteConfigCollection` | `ServiceAttribute` khai `Inherited = false` — đăng ký viết ở base không tới được subclass. Ràng buộc §0.2 |
| Mỗi biến một ScriptableObject riêng | Đã cân và loại: nút `SearchFirebaseKeyUsage` tồn tại nghĩa là danh sách biến dài; một class cộng một asset cho mỗi biến `bool` đắt hơn nhiều lần cái nó mua, và giết mất chỗ mạnh nhất của hệ này — mở một asset là thấy cả bảng cấu hình, import CSV vào |
| Đổi tên họ `RCVariable*` thành `RemoteConfig*` ngay trong lần này | Là một lượt tìm–thay cơ học nhưng chạm mọi dự án đang dùng SDK; tách riêng ở mục "Mở rộng sau" để không trộn hai loại rủi ro vào một lần sửa |
| Tự đổi asset của dự án thật | Ghi vào dữ liệu đã author. Task 4 mô tả thao tác; developer chạy |

---

## §0. Bốn ràng buộc thật

Không có toán. Bốn sự thật dưới quyết hình dạng code, và cả bốn đã kiểm bằng phép thử chạy được — đọc trước
khi sửa.

### 0.1. Contract của SDK không được derive `IService<>` — nếu không, call site của dự án không biên dịch

`IService<out T>` khai `Service` là thành viên **static** ngay trong interface:

```csharp
public interface IService<out T>
{
    public static T Service => Sisus.Init.Service.Get<T>();
}
```

C# **cho** đọc thành viên static của interface nền qua tên interface dẫn xuất — đó là lý do
`ILevelCheater.Service` hiện nay biên dịch được. Nhưng khi một interface thừa hưởng **hai** instantiation khác
nhau của `IService<>`, cái tên `Service` trở thành nhập nhằng:

```csharp
public interface IRCVariableCollection : IService<IRCVariableCollection> { }               // giữ IService ở contract
public interface IGameRemoteConfig : IRCVariableCollection, IService<IGameRemoteConfig> { }

var x = IGameRemoteConfig.Service;
// error CS0229: Ambiguity between 'IService<IRCVariableCollection>.Service'
//                            and 'IService<IGameRemoteConfig>.Service'
```

Đây là lỗi **biên dịch**, không phải cảnh báo — và nó nổ ở đúng thứ ta đang cố giữ. Nên contract phải là
interface thuần, và chỉ interface của dự án derive `IService<>`.

Mất gì khi bỏ `IService<>` khỏi contract: không mất gì. `IService<T>.Service` chỉ là `Service.Get<T>()`, nên nó
**chạy với bất kỳ `T`**, kể cả `T` không derive `IService<T>`. Code cần contract mà không cần biết interface
của dự án thì viết `IService<IRCVariableCollection>.Service` — dài hơn, nhưng ở SDK hiện nay không có chỗ nào
cần, và khi hệ Persistence cần thì đó là đúng một dòng.

### 0.2. `[Service]` không di truyền — dự án phải tự khai

`ServiceAttribute` khai `Inherited = false`. Nên một `[Service]` viết trên `BaseRemoteConfigCollection` sẽ
**không** áp cho subclass của dự án: service không được đăng ký, và `Service.Get<>()` ném exception ở lần chạm
đầu tiên.

Hệ quả lên hình dạng: `[Service]`, `[CreateAssetMenu]` và đường dẫn Resources đều **chuyển sang dự án**. Đây
không phải nhượng bộ — đường dẫn asset vốn là quyết định của dự án, không phải của SDK.

`AllowMultiple = true`, nên **một** class dẫn xuất đăng ký được dưới **hai** service type. Cần đúng như vậy:
dự án hỏi `IGameRemoteConfig` để có property có kiểu, code SDK (khi nào cần) hỏi `IRCVariableCollection`. Hai
lần `[Service]` cùng trỏ một `ResourcePath` không sinh hai object — `Resources.Load` trả về cùng một instance
asset.

### 0.3. Reflection đọc type lúc chạy, nên scan viết ở base thấy private field của subclass

Đoạn scan hiện có không phải sửa một chữ:

```csharp
cachedRCFields ??= GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
    .Where(field => field.IsDefined(typeof(RegisteredRCVar), false)).ToList();
```

`GetType()` trả về type **thật lúc chạy** — tức class của dự án — nên `GetFields` với `NonPublic | Instance`
lấy được đúng những `private` field mà dự án khai, và bỏ đúng field không mang attribute.

**Mặt còn lại, phải biết:** `GetFields` **không** trả về private field khai trên **base class**. Nên
`BaseRemoteConfigCollection` không thể tự khai một biến private rồi mong nó vào danh sách. Hôm nay SDK không
khai biến nào nên không ảnh hưởng; ngày nào cần thì field đó phải là `protected`.

### 0.4. Magic method của Unity bị subclass che — `OnDestroy` phải là `protected virtual`

Đây là bẫy **mới sinh ra** khi đổi từ `partial` sang kế thừa, không có trong bản cũ. Bản cũ có:

```csharp
private void OnDestroy()
{
    if (provider != null) provider.OnFetched -= OnRemoteConfigFetched;
}
```

Với `partial` thì chỉ có một class, nên không có gì che nó. Với kế thừa, một ngày nào đó dự án viết `OnDestroy`
riêng trên class dẫn xuất là base bị che: Unity gọi bản của subclass, `OnFetched` **không** được huỷ đăng ký,
và một listener chết nằm lại trên provider. Không có gì báo.

Chặn bằng cấu trúc: khai `protected virtual void OnDestroy()`. Dự án muốn thêm việc thì `override` và gọi
`base.OnDestroy()`, và compiler nhắc bằng warning nếu họ viết `new` thay vì `override`. Bắt developer nhớ đừng
đặt tên trùng là chặn bằng kỷ luật, và sẽ vỡ ở người thứ hai.

---

## Bản đồ triển khai

| Task | Ở đâu | Nội dung |
|---|---|---|
| 1 | SDK — `Abstractions/Foundations/RemoteConfigSystem/IRCVariableCollection.cs` | contract thành interface thuần |
| 2 | SDK — `Implementations/Foundations/RemoteConfigSystem/` | `RCVariableCollection.cs` → `BaseRemoteConfigCollection.cs`, thành abstract |
| 3 | Dự án — assembly của dự án | hai file mới: interface có kiểu + class dẫn xuất |
| 4 | Dự án thật đã có asset | trỏ asset sang script mới, giữ nguyên dữ liệu đã author |

Thứ tự: **1 → 2 → 3 → 4**. Task 1 và 2 độc lập nhau về nội dung nhưng phải cùng landing trước Task 3.

> **Trong repo `horcrux_algo`:** chưa có asset `RCVariableCollection.asset` nào và chưa có nửa partial nào phía
> dự án — đã kiểm bằng `find` và `grep`. Nên Task 1 và 2 là refactor sạch, Task 3 làm khi có biến đầu tiên,
> Task 4 không phải làm.
>
> **Trong dự án thật:** Task 2 làm class thành `abstract`, nên asset đang có sẽ trỏ vào một type không dựng
> instance được cho tới khi Task 4 xong. Làm 2 → 3 → 4 liền một mạch, và commit asset trước khi bắt đầu để có
> đường lùi.

---

### Task 1: Contract thành interface thuần

**Files:**
- Modify: `Assets/Horcrux/Runtime/Abstractions/Foundations/RemoteConfigSystem/IRCVariableCollection.cs`

**Interfaces:**
- Consumes: `IRCVariable` · `IRemoteConfigProvider` (cùng thư mục).
- Produces: `IRCVariableCollection` — 2 property + 1 method. **Không** còn derive `IService<>`, **không** còn `partial`.

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| Bỏ `: IService<IRCVariableCollection>` | §0.1 — giữ lại là CS0229 ở mọi call site của dự án |
| Bỏ `partial` | Không còn ai gắn thêm nửa thứ hai; `partial` để lại là mời người sau đi lại đúng con đường cũ |
| Ba thành viên giữ nguyên tên và chữ ký | Không có lý do đổi, và đổi là bắt mọi dự án sửa theo |
| Ghi lý do "không derive IService" **vào XML doc của chính interface** | Người sẽ vi phạm là người đang mở đúng file này ra đọc. Một dòng trong plan không chặn được họ; một dòng ở đây thì có |

- [ ] **Step 1: viết lại cả file**

```csharp
using System.Collections.Generic;

namespace Horcrux.Runtime.Abstractions.RemoteConfigSystem
{
    /// <summary>What every remote config collection can do. A game project derives its own service interface from this one.</summary>
    /// <remarks>
    /// The project side owns the variables. It declares an interface deriving both this one and
    /// <see cref="IService{T}"/> closed over itself, holding one typed property per variable, and a sealed class
    /// deriving BaseRemoteConfigCollection that implements it. That is what keeps every model and key inside the
    /// project's own assembly while a call site still reads a value with no cast.
    /// <para>
    /// This interface must NOT derive <see cref="IService{T}"/>. A project interface deriving both would inherit
    /// two static Service members, and every read of it fails to compile with CS0229, ambiguity. Code that needs
    /// this contract and nothing else asks <see cref="IService{T}"/> closed over this type, which resolves for any
    /// type whether or not that type derives it.
    /// </para>
    /// </remarks>
    public interface IRCVariableCollection
    {
        public IEnumerable<IRCVariable> RCVariables { get; }
        public IRemoteConfigProvider RemoteConfigProvider { get; }

        public void Initialize();
    }
}
```

- [ ] **Step 2: Kiểm chứng** — Unity compile sạch. Không có gì khác cần chạy:
      `grep -rn "IRCVariableCollection" Assets Packages` chỉ ra file này và `RCVariableCollection.cs`.

---

### Task 2: Máy móc thành `abstract class`

**Files:**
- Rename: `Assets/Horcrux/Runtime/Implementations/Foundations/RemoteConfigSystem/RCVariableCollection.cs`
  → `BaseRemoteConfigCollection.cs` — đổi tên **trong Project window của Unity** để `.meta` đi theo, đừng đổi
  bằng file explorer
- Modify: nội dung file đó

**Interfaces:**
- Consumes: `IRCVariableCollection` (Task 1) · `IRCVariable` · `IRemoteConfigProvider` · `System.Reflection` · Odin `[Button]`.
- Produces: `RegisteredRCVar` (attribute, không đổi) · `abstract class BaseRemoteConfigCollection : ScriptableObject, IRCVariableCollection`.

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| `abstract`, không `partial` | Không tạo được asset trực tiếp từ nó là **đúng ý**: một collection không có biến nào thì không có việc gì để làm |
| Bỏ `[Service]` và `[CreateAssetMenu]` | §0.2 — `Inherited = false` nên khai ở đây là khai vào chỗ không ai đọc; và `[CreateAssetMenu]` trên một type abstract tạo ra một menu item luôn thất bại |
| Bỏ `RESOURCE_PATH` | Đường dẫn asset là quyết định của dự án. Để lại một const không ai dùng là để lại câu trả lời cho một câu hỏi mà file này không được hỏi |
| `partial void OnRemoteValuesApplied()` → `protected virtual void OnRemoteValuesApplied() { }` | `partial` method đòi `partial` class, nên cơ chế buộc phải đổi. Hook giữ đúng vai: SDK gọi, dự án cài nếu cần |
| `private void OnDestroy()` → `protected virtual void OnDestroy()` | §0.4 — bẫy mới của kế thừa: subclass đặt trùng tên là base bị che và listener chết nằm lại trên provider, im lặng |
| `rcVariables`, `cachedRCFields`, `provider`, `GetRCFields()` giữ `private` | Dự án không có việc gì với chúng. Mở ra `protected` khi chưa có người dùng thứ hai là mở rộng bề mặt vì lý do không có thật |
| Hai nút Editor ở lại base | Chúng đọc field qua reflection nên chạy đúng trên mọi subclass; Odin vẽ `[Button]` khai ở base trên Inspector của asset dẫn xuất |
| Ghi vào XML doc: scan **không** thấy private field của base | §0.3 — người thêm một biến vào chính base sẽ thấy nó bị bỏ qua mà không có lỗi nào. Cái sai đó lộ ra bằng "biến không bao giờ fetch", loại khó lần nhất |

- [ ] **Step 1: đổi tên file** trong Project window: `RCVariableCollection.cs` → `BaseRemoteConfigCollection.cs`.

- [ ] **Step 2: nội dung file** — chỉ phần đầu đổi; từ `GetRCFields()` xuống hết file giữ **nguyên văn**:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Horcrux.Runtime.Abstractions.RemoteConfigSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.RemoteConfigSystem
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class RegisteredRCVar : Attribute { }

    /// <summary>Scans the declared variables, wires the provider, and pushes every fetched value into them.</summary>
    /// <remarks>
    /// A game project derives one sealed class from this IN ITS OWN ASSEMBLY and declares the variables there, so
    /// no remote config model ever has to be compiled into this SDK. The derived class carries the three things
    /// missing here on purpose — the Service registration, the CreateAssetMenu entry and the Resources path —
    /// because ServiceAttribute is declared Inherited = false and a registration written here would never reach it.
    /// <para>
    /// The scan reads the runtime type, so it picks up the private fields of the derived class. It does NOT pick up
    /// private fields declared on this class: a variable added here would have to be protected, and one left
    /// private is dropped with no error at all.
    /// </para>
    /// </remarks>
    public abstract class BaseRemoteConfigCollection : ScriptableObject, IRCVariableCollection
    {
        private List<IRCVariable> rcVariables = new();
        private List<FieldInfo> cachedRCFields;
        private IRemoteConfigProvider provider;

        public IEnumerable<IRCVariable> RCVariables => rcVariables;
        public IRemoteConfigProvider RemoteConfigProvider => provider;

        /// <summary>Runs once after every fetched value has been applied. Override to react in a single place.</summary>
        protected virtual void OnRemoteValuesApplied() { }

        #region Unity Callbacks

        /// <summary>Drops the provider subscription. An override MUST call base, or the dead listener stays behind.</summary>
        protected virtual void OnDestroy()
        {
            if (provider != null)
                provider.OnFetched -= OnRemoteConfigFetched;
        }

        #endregion

        // ── from GetRCFields() down to the end of the file: unchanged ──
    }
}
```

- [ ] **Step 3: Kiểm chứng**

| Input | Kỳ vọng |
|---|---|
| Unity recompile sau Task 1 + 2 | không lỗi, không warning mới |
| Menu `Create` → `Horcrux` | **không còn** mục `RCVariableCollection` — đúng, vì type đã abstract |
| `grep -rn "RCVariableCollection" Assets Packages` | chỉ còn tên interface `IRCVariableCollection`; tên class cũ không còn ở bất kỳ đâu, kể cả comment và chuỗi debug |

---

### Task 3: Hai file phía dự án

**Files** — đặt trong assembly của dự án, thư mục nào cũng được miễn nằm trong phạm vi `com.FelixFelicis`
(gợi ý: `Assets/FelixFelicis/Implements/RemoteConfig/`):
- Create: `IGameRemoteConfig.cs`
- Create: `GameRemoteConfig.cs`

**Interfaces:**
- Consumes: `IRCVariableCollection` · `IService<T>` · `BaseRemoteConfigCollection` · `RCVariable<T>` · `RegisteredRCVar`.
- Produces: `IGameRemoteConfig` (1 property mỗi biến) · `GameRemoteConfig` (sealed).

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| Hai file, không một | Interface là thứ mọi call site đọc; class là chi tiết triển khai. Tách ra thì thêm một biến là sửa hai chỗ liền nhau, và IntelliSense ở call site chỉ thấy phần nó cần |
| `IGameRemoteConfig : IRCVariableCollection, IService<IGameRemoteConfig>` | Derive contract để có `Initialize()` và `RCVariables`; derive `IService<chính mình>` để có `Service` không nhập nhằng (§0.1) |
| Hai dòng `[Service]`, cùng một `ResourcePath` | Một asset trả lời cho cả hai cửa: dự án hỏi `IGameRemoteConfig`, code SDK hỏi `IRCVariableCollection`. `AllowMultiple = true` cho phép; `Resources.Load` trả cùng một instance nên không sinh hai object (§0.2) |
| `sealed` | Không có implementation thứ hai, và không có lý do để có. Mở một chain ba tầng khi chưa ai cần là thêm một bậc người đọc phải leo |
| Field `private` + property `public` | Field là chỗ Unity serialize và chỗ Inspector vẽ; property là hợp đồng. Phơi field ra `public` là cho mọi caller gán được tham chiếu biến — thứ chỉ authoring được phép làm |
| `ResourcePath` giữ đúng `"Config/RCVariableCollection"` | Ở dự án thật, asset đang nằm đúng chỗ đó. Giữ chuỗi này là **không phải di chuyển asset** ở Task 4 — và đường dẫn Resources của một asset đã ship cũng không nên đổi vì lý do thẩm mỹ |

- [ ] **Step 1: `IGameRemoteConfig.cs`**

```csharp
using Horcrux.Runtime.Abstractions;
using Horcrux.Runtime.Abstractions.RemoteConfigSystem;

namespace FelixFelicis.RemoteConfig
{
    /// <summary>Every remote config variable this game has, typed. Read one through IGameRemoteConfig.Service.</summary>
    /// <remarks>
    /// Deriving IService here — and NOT on IRCVariableCollection — is what keeps Service unambiguous. Adding a
    /// variable means one property here and one field on GameRemoteConfig; both are required, since nothing
    /// outside the class can reach a private field.
    /// </remarks>
    public interface IGameRemoteConfig : IRCVariableCollection, IService<IGameRemoteConfig>
    {
        // One property per variable, e.g.:
        // RCVariable<int> RetryCount { get; }
    }
}
```

- [ ] **Step 2: `GameRemoteConfig.cs`**

```csharp
using Horcrux.Runtime.Abstractions.RemoteConfigSystem;
using Horcrux.Runtime.Implementations.RemoteConfigSystem;
using Sisus.Init;
using UnityEngine;

namespace FelixFelicis.RemoteConfig
{
    /// <summary>This game's remote config variables. Lives in the game assembly so no model reaches the SDK.</summary>
    /// <remarks>Registered twice on purpose: the game reads IGameRemoteConfig for typed access, SDK code reads
    /// IRCVariableCollection for the contract, and both resolve to this one asset.</remarks>
    [Service(typeof(IGameRemoteConfig), ResourcePath = ResourcePath)]
    [Service(typeof(IRCVariableCollection), ResourcePath = ResourcePath)]
    [CreateAssetMenu(menuName = "FelixFelicis/RCVariableCollection", fileName = "RCVariableCollection")]
    public sealed class GameRemoteConfig : BaseRemoteConfigCollection, IGameRemoteConfig
    {
        // Must match where the asset sits under a Resources folder, or the service never resolves.
        private const string ResourcePath = "Config/RCVariableCollection";

        // One field per variable, e.g.:
        // [RegisteredRCVar, SerializeField] private RCVariable<int> retryCount;
        // public RCVariable<int> RetryCount => retryCount;
    }
}
```

**Editor setup — bước thật:**

1. Project window → chuột phải → `Create` → `FelixFelicis` → `RCVariableCollection`.
2. Đặt asset vào `Assets/<…>/Resources/Config/RCVariableCollection.asset` — đường dẫn **sau** `Resources/` phải
   khớp `ResourcePath`, sai là service không resolve và `Service.Get<>()` ném exception.
3. Với mỗi biến: điền `Firebase Key`, đặt `Allow Fetching`, đặt giá trị mặc định.

- [ ] **Step 3: Kiểm chứng** — bốn hàng đầu chỉ Unity kiểm được, `dotnet` không:

| Input | Kỳ vọng |
|---|---|
| Thêm một `[RegisteredRCVar, SerializeField] private RCVariable<int>` rồi mở asset | Inspector vẽ đủ `firebaseKey`, `allowFetching`, `value` — chứng minh Unity serialize được generic field khai trên subclass |
| Mở Inspector của asset | Hai nút `Search Firebase Key Usage` và `Enable All Fetching` **hiện** — chứng minh Odin vẽ `[Button]` khai ở base |
| Đọc `IGameRemoteConfig.Service` và `IService<IRCVariableCollection>.Service` trong cùng một lần chạy | Cả hai trả về **cùng một** object — `ReferenceEquals` true |
| `IGameRemoteConfig.Service.Initialize()` với một biến đã khai | Provider được wire; `RCVariables` có đúng một phần tử |
| Khai field mà **quên** `[RegisteredRCVar]` | Biến không có trong `RCVariables` và **không có log nào** — đường hỏng im lặng đã biết của khuôn này, ghi ra để nhận ra khi gặp |
| Đặt asset sai đường dẫn Resources | Exception ở lần chạm `Service` đầu tiên, không phải giá trị mặc định im lặng |

---

### Task 4: Trỏ asset đang có sang script mới

Chỉ làm ở dự án thật đã có `RCVariableCollection.asset`. **Đây là thao tác ghi vào dữ liệu đã author** — commit
hoặc copy asset ra ngoài trước khi bắt đầu.

Vì sao phải làm: file `.asset` trỏ script bằng **GUID**, và type cụ thể của asset vừa đổi từ
`RCVariableCollection` sang `GameRemoteConfig`. Không trỏ lại thì Unity thấy một asset mang script abstract và
không dựng được nó.

Vì sao dữ liệu không mất: Unity deserialize theo **tên field**. Nửa partial cũ của dự án khai
`[RegisteredRCVar, SerializeField] private RCVariable<int> retryCount;` — chuyển nguyên văn vào thân
`GameRemoteConfig` và **giữ đúng tên field** thì mọi `firebaseKey`, `allowFetching` và giá trị mặc định đã
author quay về đủ.

- [ ] **Step 1** — Đóng Unity.
- [ ] **Step 2** — Chuyển nội dung nửa partial cũ (`RCVariableCollection.<…>.cs` và `IRCVariableCollection.<…>.cs`
      trong thư mục có `.asmref`) vào `GameRemoteConfig` và `IGameRemoteConfig`, **giữ nguyên từng tên field**.
      Xoá hai file cũ, và xoá cả file `.asmref` — nó không còn việc gì.
- [ ] **Step 3** — Mở `GameRemoteConfig.cs.meta`, copy giá trị `guid`.
- [ ] **Step 4** — Mở `RCVariableCollection.asset` bằng text editor, tìm dòng
      `m_Script: {fileID: 11500000, guid: <cũ>, type: 3}`, thay `<cũ>` bằng guid vừa copy. Giữ nguyên
      `fileID: 11500000` và `type: 3`.
- [ ] **Step 5** — Mở Unity.

- [ ] **Step 6: Kiểm chứng**

| Input | Kỳ vọng |
|---|---|
| Mở asset trong Inspector | Header hiện `Game Remote Config`, **không** có "Script is missing" |
| Từng biến | `firebaseKey`, `allowFetching`, giá trị mặc định **đúng như trước khi sửa** — so với bản commit trước |
| `git diff` trên file `.asset` | Chỉ đúng một dòng `m_Script` đổi. Có dòng dữ liệu nào đổi theo là dấu hiệu một tên field bị lệch — quay lại Step 2 |
| Chạy game, fetch remote config | Giá trị về đúng như trước |

---

## Nghiệm thu tổng

| Tiêu chí của Goal | Bằng chứng |
|---|---|
| Không type nào của dự án nằm trong assembly SDK | `find` không còn file `.asmref` nào; mọi file trong `Assets/Horcrux` chỉ gọi tên type của `Horcrux.*` |
| Không cần `.asmref` | như trên |
| Call site không cast, có kiểu đầy đủ | `IGameRemoteConfig.Service.RetryCount.Value` biên dịch, và IntelliSense liệt kê đủ biến |
| Một asset cho cả dự án | đúng một `RCVariableCollection.asset` dưới `Resources/Config/` |
| Dữ liệu đã author không mất | `git diff` trên `.asset` chỉ đổi dòng `m_Script` |
| SDK vẫn bê sang dự án khác được | `Assets/Horcrux` không tham chiếu `FelixFelicis` ở bất cứ đâu; submodule commit được một mình |

## Mở rộng sau

**Đổi cả họ tên `RCVariable*` → `RemoteConfig*`.** Sau lần sửa này hệ có hai từ vựng cho một khái niệm:
`BaseRemoteConfigCollection` và `IGameRemoteConfig` nói `RemoteConfig`, còn `IRCVariableCollection`,
`IRCVariable`, `RCVariable<T>`, `RegisteredRCVar` nói `RCVariable`. Một khái niệm nên có một tên, nên việc này
đáng làm — nhưng là một lượt tìm–thay **chạm mọi dự án đang dùng SDK**, nên nó là một lần sửa riêng với một lần
nghiệm thu riêng, không trộn vào lần này.

Giá phải trả khi làm: chỉ là code. Tên type không nằm trong file `.asset` — asset lưu tên **field**, không lưu
tên type của field — nên không có dữ liệu nào phải chuyển. Phần đắt là mỗi dự án phải sửa nửa khai báo biến của
nó rồi compile lại.

**Đưa `IRemoteConfigProvider` qua cùng khuôn.** Chưa xét trong lần này. Nếu một dự án cần provider riêng thì đó
là implementation thứ hai, và lúc đó mới có nhu cầu thật.
