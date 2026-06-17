# Pooling System

## Flow

```
Initialize(ct)
└── foreach poolConfigs (lazyInit = false)
    └── LoadAndCreatePool(index, ct)
        ├── AssetReference.LoadAssetAsync<GameObject>()  → handle (giữ để Release)
        ├── prefab.GetComponent<IPoolable>()             → prefab cache (giữ để Instantiate)
        └── Pre-warm: Instantiate × initialPoolSize      → Push vào Stack (inactive)

Get<T>(parent, ct)
├── pool tồn tại
│   ├── Stack.Pop() → instance còn sống → dùng lại
│   └── Stack rỗng hoặc toàn destroyed → Instantiate(prefab)
└── pool chưa tồn tại (lazyInit = true)
    └── LazyLoad<T>() → LoadAndCreatePool() → Get bình thường
→ SetParent → SetActive(true) → OnGetFromPool()

Return<T>(instance)
├── instance đã inactive → skip (chống double Return)
├── pool count < maxPoolSize → OnReturnToPool() → SetActive(false) → Push vào Stack
└── pool count >= maxPoolSize → OnReturnToPool() → Destroy

Dispose() / OnDestroy()
└── foreach pool
    ├── Destroy tất cả inactive instances
    └── Addressables.Release(handle)     → asset ra khỏi memory
```

## Memory lifecycle

```
LoadAssetAsync   ──→  prefab vào memory (1 handle, ref count = 1)
Instantiate × N  ──→  clone từ prefab (không tăng ref count)
Release(handle)  ──→  prefab ra khỏi memory (ref count = 0)
```

Dùng `LoadAssetAsync` + `Object.Instantiate`, KHÔNG dùng `Addressables.InstantiateAsync` — tránh ref count tracking từng instance.

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

### 1. Tạo Component implement IPoolable

```csharp
public class EnemyBullet : MonoBehaviour, IPoolable
{
    public void OnGetFromPool()    { /* reset velocity, damage... */ }
    public void OnReturnToPool()   { /* stop particles, cancel tweens... */ }
}
```

### 2. Cấu hình Inspector

Trên GameObject có `PoolManager`, kéo prefab vào `poolConfigs[]`:

| Field | Mô tả |
|-------|-------|
| `prefab` | AssetReference đến prefab (Addressable) |
| `lazyInit` | `true` = chỉ load khi `Get<T>()` lần đầu |
| `initialPoolSize` | Số instance pre-warm lúc init |
| `maxPoolSize` | Giới hạn inactive trong pool, vượt → Destroy |

### 3. Sử dụng runtime

```csharp
// Get — tìm pool, không có thì Instantiate
var bullet = await IPoolManager.Service.Get<EnemyBullet>(firePoint);

// Return — trả về pool, gọi OnReturnToPool()
IPoolManager.Service.Return(bullet);

// Cleanup toàn bộ khi chuyển scene
IPoolManager.Service.Dispose();
```

## Guard tự động

| Case | Xử lý |
|------|--------|
| Pop instance bị Destroy (scene unload) | Skip, tạo mới từ prefab |
| `Return()` cùng instance 2 lần | Phát hiện qua `activeSelf`, skip + log warning |
| `Dispose()` gọi 2 lần | `_pools.Count == 0` → return ngay |
| Prefab không có IPoolable | Log error, Release handle, skip config |
| Duplicate config cho cùng type | Log warning, Release handle thừa, giữ pool đầu |

## Cấu trúc file

```
Abstractions/Pooling/                (contracts)
├── IPoolable.cs                     OnGetFromPool, OnReturnToPool
└── IPoolManager.cs                  Initialize, Get<T>, Return<T>, Dispose

Implementations/Pooling/             (implementations)
├── PoolConfig.cs                    Serializable struct — prefab, lazyInit, sizes
└── PoolManager.cs                   MonoBehaviour [Service] — LoadAssetAsync, Stack pool, lazy load
```
