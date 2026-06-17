# Pooling System

## Flow

```
Initialize(ct)
└── foreach poolConfigs
    └── LoadAndCreatePool(config, ct)
        ├── AssetReference.LoadAssetAsync<GameObject>()  → handle (giữ để Release)
        ├── prefab.GetComponent<IPoolable>()             → prefab cache (giữ để Instantiate)
        └── Pre-warm: Instantiate × initialPoolSize      → Add vào List (inactive)

Get<T>(parent)
├── pool tồn tại
│   ├── Lấy từ cuối List → instance còn sống → dùng lại
│   └── List rỗng hoặc toàn destroyed → Instantiate(prefab)
└── pool không tồn tại → throw InvalidOperationException
→ SetParent → SetActive(true) → OnGetFromPool()

Return<T>(instance)
├── pool count >= maxPoolSize → Destroy (không gọi OnReturnToPool)
├── instance đã nằm trong Inactive list → skip (chống double Return)
└── pool chưa đầy → OnReturnToPool() → SetActive(false) → Add vào list

Dispose() / OnDestroy()
└── foreach pool
    ├── Destroy tất cả inactive instances
    └── Addressables.Release(handle)     → asset ra khỏi memory
```

## Lifetime

`PoolManager` gọi `DontDestroyOnLoad(this)` trong `Awake()` → tồn tại xuyên scene.
Mọi instance inactive đều `SetParent(transform)` về PoolManager → cũng nằm trong DontDestroyOnLoad, không bị destroy khi chuyển scene.

## Memory lifecycle

```
LoadAssetAsync   ──→  prefab vào memory (1 handle, ref count = 1)
Instantiate × N  ──→  clone từ prefab (không tăng ref count)
Release(handle)  ──→  prefab ra khỏi memory (ref count = 0)
```

## Prefab yêu cầu

Mỗi prefab pool **bắt buộc** có Component implement `IPoolable`:

```csharp
public interface IPoolable
{
    void OnGetFromPool();     // reset state khi lấy ra
    void OnReturnToPool();    // cleanup khi trả về
}
```

## Cách dùng

### 1. Tạo Component implement `IPoolable` hoặc kế thừa `PoolableBehaviour`:

```csharp
public class EnemyBullet : PoolableBehaviour
{
    public override void OnGetFromPool()    { /* reset velocity, damage... */ }
    public override void OnReturnToPool()   { /* stop particles, cancel tweens... */ }
}
```

### 2. Cấu hình Inspector

Trên GameObject có `PoolManager`, kéo prefab vào `poolConfigs[]`:

| Field | Mô tả |
|-------|-------|
| `prefab` | AssetReference đến prefab (Addressable) |
| `initialPoolSize` | Số instance pre-warm lúc init |
| `maxPoolSize` | Giới hạn inactive trong pool, vượt → Destroy |

### 3. Sử dụng runtime

```csharp
// Get — lấy từ pool (sync), không có pool → throw
var bullet = IPoolManager.Service.Get<EnemyBullet>(firePoint);

// Return — trả về pool, gọi OnReturnToPool()
IPoolManager.Service.Return(bullet);

// Cleanup toàn bộ khi chuyển scene
IPoolManager.Service.Dispose();
```

## Nhiều prefab, cùng behavior (variant pattern)

Pool dùng `Type` làm key → mỗi prefab cần 1 type riêng. Khi nhiều prefab chung behaviour, tạo abstract base chứa logic chung + thin subclass cho mỗi variant:

```csharp
// Shared behavior — abstract, không gắn trực tiếp vào prefab
public abstract class BulletBase : PoolableBehaviour
{
    [SerializeField] private float speed;
    [SerializeField] private int damage;

    public override void OnGetFromPool()    { /* reset velocity, trail... */ }
    public override void OnReturnToPool()   { /* stop particles, cancel tweens... */ }

    // ... shared logic: Move(), Hit(), etc.
}

// Variant — 1 dòng mỗi loại, gắn vào prefab tương ứng
public class RedBullet : BulletBase { }
public class BlueBullet : BulletBase { }
```

**Inspector**: mỗi variant là 1 entry trong `poolConfigs[]`:

| Config | Prefab | Component | Pool Key |
|--------|--------|-----------|----------|
| [0] | Bullet_Red.prefab | `RedBullet` | `typeof(RedBullet)` |
| [1] | Bullet_Blue.prefab | `BlueBullet` | `typeof(BlueBullet)` |

**Runtime**:

```csharp
var red  = IPoolManager.Service.Get<RedBullet>(firePoint);   // pool riêng
var blue = IPoolManager.Service.Get<BlueBullet>(firePoint);   // pool riêng

IPoolManager.Service.Return(red);
IPoolManager.Service.Return(blue);
```

Cách này hoạt động vì:
- `LoadAndCreatePool` lưu `poolable.GetType()` → runtime type (subclass), không phải base
- `Get<T>()` dùng `typeof(T)` → match đúng subclass
- `Return()` dùng `instance.GetType()` → match đúng subclass

## Guard tự động

| Case | Xử lý |
|------|--------|
| Pop instance bị Destroy (scene unload) | Skip, tạo mới từ prefab |
| `Return(null)` | Throw `ArgumentNullException` |
| `Return()` instance không thuộc pool nào | `Destroy` + log warning |
| `Return()` khi pool đầy (`>= maxPoolSize`) | `Destroy` trực tiếp, không gọi `OnReturnToPool` |
| `Return()` cùng instance 2 lần | Phát hiện qua `Inactive.Contains`, skip + log warning |
| `Dispose()` gọi 2 lần | `_pools.Count == 0` → return ngay |
| Prefab không có IPoolable | Log error, Release handle, skip config |
| Duplicate config cho cùng type | Log warning, Release handle thừa, giữ pool đầu |

## Cấu trúc file

```
Abstractions/Pooling/                (contracts)
├── IPoolable.cs                     OnGetFromPool, OnReturnToPool
├── IPoolManager.cs                  Initialize, Get<T>, Return<T>, Dispose
└── PoolableBehaviour.cs             Abstract MonoBehaviour base — convenience cho consumer

Implementations/Pooling/             (implementations)
├── PoolConfig.cs                    Serializable struct — prefab, sizes
└── PoolManager.cs                   MonoBehaviour [Service] — LoadAssetAsync, List pool
```
