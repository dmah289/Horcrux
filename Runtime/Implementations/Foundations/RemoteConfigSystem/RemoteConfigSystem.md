# Remote Config System

## Flow

```
Provider fetch xong
└── fire OnFetched
    └── RCVariableCollection.OnRemoteConfigFetched()
        ├── RCVariable<int>.ApplyRemoteValue(provider)
        │   └── provider.TryGetRemoteValue(key) → parse → cache PlayerPrefs
        └── RCVariable<T>.ApplyRemoteValue(provider)
            └── ...
```

Thứ tự khởi tạo không quan trọng nhờ `IsFetched`:
- Collection init trước → subscribe event → đợi provider fire
- Provider init trước → Collection init sau → thấy `IsFetched == true` → fetch ngay

## Thứ tự ưu tiên lấy giá trị

Khi `allowFetching = true`:

```
1. Remote Provider  → fetch thành công + parse OK → dùng, cache vào PlayerPrefs
2. PlayerPrefs      → có cache từ lần trước + parse OK → dùng
3. Editor Default   → giữ giá trị set trên Inspector
```

Khi `allowFetching = false`: luôn dùng giá trị Editor, bỏ qua remote.

Giá trị chỉ cache vào PlayerPrefs khi parse thành công. Remote trả sai format sẽ KHÔNG ghi đè cache.

## Kiểu hỗ trợ

`string`, `int`, `long`, `bool`, `float`, `double` (InvariantCulture), `enum` (tên hoặc số), kiểu phức tạp qua `JsonConvert`.

## Cách dùng

### 1. Triển khai provider (game project)

```csharp
[Service(typeof(IRemoteConfigProvider))]
public class FirebaseRemoteConfigProvider : IRemoteConfigProvider
{
    public event Action OnFetched;
    public bool IsFetched { get; private set; }

    public bool TryGetRemoteValue(string firebaseKey, out string value) { /* ... */ }

    public async UniTask FetchAsync()
    {
        // ... fetch từ Firebase Remote Config
        IsFetched = true;
        OnFetched?.Invoke();
    }
}
```

### 2. Khai báo biến (partial class)

Vì `RCVariableCollection` nằm trong assembly `com.horcrux.runtime` (submodule), file partial class ở game project **phải** được route về cùng assembly bằng `.asmref`.

**Bước 1:** Tạo thư mục riêng cho partial class (KHÔNG đặt chung với file `.cs` khác thuộc assembly khác):

```
Assets/.../RemoteConfig/
├── com.horcrux.runtime.asmref
└── RCVariableCollection.Level.cs
```

**Bước 2:** Tạo file `.asmref` trỏ về assembly gốc:

```json
// com.horcrux.runtime.asmref
{
    "reference": "com.horcrux.runtime"
}
```

> ⚠️ Tất cả `.cs` trong thư mục chứa `.asmref` sẽ thuộc assembly `com.horcrux.runtime`.
> Không đặt file `.asmref` vào thư mục đã có file thuộc assembly khác — sẽ gây conflict.

**Bước 3:** Khai báo biến trong partial class **và** partial interface — **phải tạo cả hai**:

```csharp
// File: IRCVariableCollection.Level.cs (cùng thư mục .asmref)
namespace Horcrux.Runtime.Abstractions.RemoteConfigSystem
{
    public partial interface IRCVariableCollection
    {
        public RCVariable<int> MaxRetryCount { get; }
    }
}
```

```csharp
// File: RCVariableCollection.Level.cs (cùng thư mục .asmref)
using Horcrux.Runtime.Abstractions.RemoteConfigSystem;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.RemoteConfigSystem
{
    public partial class RCVariableCollection
    {
        [RegisteredRCVar]
        [SerializeField] private RCVariable<int> maxRetryCount;

        public RCVariable<int> MaxRetryCount => maxRetryCount;
    }
}
```

> ⚠️ **Bắt buộc tạo cả 2 file:** mỗi property khai báo trong `RCVariableCollection` phải có property tương ứng trong `IRCVariableCollection`. Nếu chỉ tạo partial class mà không tạo partial interface, code bên ngoài sẽ không truy cập được qua interface.

> ⚠️ Partial class/interface yêu cầu **cùng assembly + cùng namespace**. Thiếu một trong hai sẽ không compile.

### 3. Khởi tạo (thứ tự tùy ý)

```csharp
RCVariableCollection.Instance.Initialize();
FirebaseRemoteConfigProvider.Initialize(); // → OnFetched?.Invoke()
```

### 4. Đọc giá trị

```csharp
int max = RCVariableCollection.Instance.MaxRetryCount;          // implicit operator
int max = RCVariableCollection.Instance.MaxRetryCount.Value;    // explicit
```

## Cấu trúc file

```
_Core/RemoteConfigSystem/              (contracts)
├── IRemoteConfigProvider.cs           Interface cho provider — OnFetched, IsFetched, TryGetRemoteValue
├── IRCVariable.cs                     Interface cho biến — FirebaseKey, AllowFetching, ApplyRemoteValue
└── IRCVariableCollection.cs           Interface cho collection — RCVariables, RemoteConfigProvider, Initialize

_Module/RemoteConfigSystem/            (implementations)
├── RCVariable.cs                      RCVariable<T> — giữ giá trị, parse, cache PlayerPrefs
└── RCVariableCollection.cs            ScriptableObject singleton — thu thập [RegisteredRCVar], subscribe event, điều phối apply
```

## Inspector (mỗi RCVariable)

| Field | Mô tả |
|-------|-------|
| `firebaseKey` | Key trên Remote Config server |
| `allowFetching` | Bật/tắt fetch từ remote |
| `value` | Giá trị mặc định (dùng khi remote + cache đều fail) |

## Editor Tools

| Tool | Vị trí | Chức năng |
|------|--------|-----------|
| CopyJsonToClipboard | RCVariable | Copy giá trị hiện tại thành JSON |
| ImportDefaultValue | RCVariable | Import giá trị mặc định từ string |
| SearchFirebaseKeyUsage | RCVariableCollection | Tìm field đang dùng một firebase key |
| EnableAllFetching | RCVariableCollection | Bật `allowFetching = true` cho tất cả biến |
