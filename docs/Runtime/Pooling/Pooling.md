# Pooling — `PoolService<TKey>`

Pool instance của prefab Addressables theo **key enum của dự án**. Dự án gọi `WarmAsync` khi vào màn dùng pool và
`ReleaseAll` khi rời màn; giữa hai mốc đó pool sống qua mọi level. Mục tiêu: `Get`/`Return` không cấp phát, mọi guard O(1),
prefab và instance ra khỏi memory khi release.

Code: `Runtime/Abstractions/Foundations/Pooling/` (`IPoolService.cs`, `IPoolable.cs`, `PoolableBehaviour.cs`) ·
`Runtime/Implementations/Foundations/Pooling/` (`PoolConfig.cs`, `PoolService.cs`, `PoolService.Editor.cs`) · namespace
`Horcrux.Runtime.Abstractions.Pooling`, `Horcrux.Runtime.Implementations.Pooling` · test: `Tests/PlayMode/PoolServiceTests.cs`.

## Ngữ cảnh đã chốt

| Mục | Nội dung |
|---|---|
| Ranh giới | Horcrux giữ cơ chế: bảng tra, warm, release, guard. Dự án giữ từ vựng: enum key, lớp con mang `[Service]`, thời điểm warm và release, reset trong `IPoolable` của từng prefab |
| Tầm sống | từ `WarmAsync` tới `ReleaseAll`; chơi lại hay sang level kế không chạm pool |
| Asset | prefab qua `AssetReference`; pool giữ handle tới `ReleaseAll` |
| Bộ nhớ | `Get`/`Return` 0 byte heap; chuỗi lỗi chỉ dựng ở nhánh lỗi |
| Reset | hook `OnGetFromPool`/`OnReturnToPool`, không dựa vào `OnEnable`/`OnDisable` (§3) |
| Cố ý KHÔNG làm | giới hạn số instance active · tự `Return` khi instance bị huỷ · gọi `OnReturnToPool` trong `ReleaseAll` · kiểm config lúc boot |

## Trước khi chạy

Dựng từ 0 theo thứ tự; mỗi bước kèm chỗ hỏng nếu thiếu.

1. Khai `enum` key với số tường minh từ `= 1`; số đã lưu vào asset thì chỉ thêm ở cuối.
2. Khai lớp con rỗng, đóng key và mang `[Service]` (Sisus Init):
   ```csharp
   [Service(typeof(IPoolService<MyPool>), FindFromScene = true)]
   public sealed class MyPoolService : PoolService<MyPool> { }
   ```
   Lớp con không viết `OnDestroy`: base có `OnDestroy` private gọi `ReleaseAll`, lớp con viết `OnDestroy` riêng là che nó.
3. Trong scene sống suốt phiên, tạo GameObject, Add Component `MyPoolService`. GameObject này là cha của mọi instance đang
   nằm trong pool.
4. Ô **Pool Configs**: mỗi phần tử điền **Key**, **Prefab** (prefab Addressables), **Initial Pool Size**, **Max Pool Size**.
   Bấm nút **Validate Configs**, Console không có lỗi.
5. Mỗi prefab có đúng một component cài `IPoolable` (thường kế thừa `PoolableBehaviour`) trên GameObject gốc. Kiểu `T` khi
   `Get<T>` là component đó.
6. Chỗ đổi màn của dự án: `await IPoolService<MyPool>.Service.WarmAsync(ct)` trước khi load màn dùng pool, **một lần mỗi lần
   vào**; `IPoolService<MyPool>.Service.ReleaseAll()` khi rời màn.
7. Trước khi cha của một instance bị huỷ (unload scene, `Destroy` root level), `Return` instance đó.

| Thiếu | Hỏng ở đâu |
|---|---|
| bước 1, số ngầm | thêm hay đổi thứ tự member làm lệch **Key** đã lưu trong scene |
| bước 2 | không có service: `IPoolService<MyPool>.Service` throw ở lần gọi đầu |
| bước 2, lớp con viết `OnDestroy` | huỷ service không `ReleaseAll`: bundle prefab nằm lại memory |
| bước 3, scene bị unload | service bị huỷ cùng scene (`OnDestroy` → `ReleaseAll`); lần gọi sau không còn service |
| bước 4, **Prefab** trống | `LogError` `Prefab … is invalid.` ở warm, key đó không có pool |
| bước 4, hai phần tử cùng **Key** | `LogError` `Pool with key '…' already exists.` ở warm, phần tử sau bị bỏ |
| bước 5, không có `IPoolable` | `LogError` `doesn't implement IPoolable.` ở warm, key đó không có pool |
| bước 5, `T` sai | `InvalidCastException` ở lần `Get` đầu; instance vừa lấy ra mồ côi dưới `parent` |
| bước 6, không warm | `Get` log `has not been warmed`, trả `null` |
| bước 6, warm mỗi level | `LogError` `has already existed` cho từng key; pool cũ vẫn dùng được |
| bước 6, không `ReleaseAll` | bundle prefab và instance nằm lại memory sau khi rời màn |
| bước 7 | instance chết theo cha; `Return` sau đó log `has been destroyed before returning`; pool `Instantiate` cái mới |

## 1. Bề mặt API

Truy cập qua `IPoolService<TKey>.Service` (thiếu service thì throw) hoặc `IPoolService<TKey>.TryGet(out service)`.

| Thành viên | Chữ ký | Ghi chú |
|---|---|---|
| Warm | `UniTask WarmAsync(CancellationToken ct)` | load prefab mọi config song song rồi tạo sẵn `initialPoolSize` instance inactive. Gọi chồng khi lần trước chưa xong: `LogError`, bỏ. Huỷ không ném |
| Lấy | `T Get<T>(TKey key, Transform parent) where T : Component, IPoolable` | instance đã active, đã `SetParent(parent)` giữ world transform, đã gọi `OnGetFromPool`. Caller tự đặt vị trí. Key chưa warm: `LogError`, trả `null` |
| Trả | `void Return<T>(T instance) where T : Component, IPoolable` | không cần key. Gọi `OnReturnToPool` rồi cất; stack inactive đầy thì `Destroy` |
| Xả | `void ReleaseAll()` | huỷ warm đang dở, `Destroy` mọi instance active lẫn inactive, `ReleaseAsset` prefab. Không gọi `OnReturnToPool`. `WarmAsync` sau đó dựng lại từ đầu |
| Hook | `IPoolable { void OnGetFromPool(); void OnReturnToPool(); }` | `PoolableBehaviour : MonoBehaviour, IPoolable` là bản rỗng `virtual` để `override` |
| Cấu hình | `PoolConfig<TKey> { TKey key; AssetReference prefab; [Min(0)] int initialPoolSize = 3; [Min(1)] int maxPoolSize = 5; }` | mảng `poolConfigs` trên component; số mặc định là nháp |
| Kiểm config | nút `ValidateConfigs` (partial Editor, Odin `[Button]`) | báo prefab trống và key trùng; không chạy lúc boot |

`maxPoolSize` chặn số instance **nằm trong pool**, không chặn số đang active: `Get` khi stack rỗng luôn `Instantiate` thêm.

## 2. Luồng

```
vào màn   WarmAsync(ct) ─> _warmCts = linked(ct) ─> mỗi config song song (WarmOneAsync):
              key đã có ⇒ LogError, bỏ · prefab trống ⇒ LogError, bỏ
              ─> prefab.LoadAssetAsync ─ huỷ ⇒ ReleaseAsset, bỏ · lỗi ⇒ ReleaseAsset, ném
              ─> GetComponent<IPoolable> ─ không có ⇒ LogError, ReleaseAsset, bỏ
              ─> _entryByKey.TryAdd ─ trùng ⇒ LogError, ReleaseAsset, bỏ
              ─> Instantiate(poolable, pool) × initialPoolSize ─> SetActive(false) ─> Inactive.Push

Get(key, parent)   _entryByKey[key] ─ không có ⇒ LogError, null
                   ─> PopAlive (bỏ instance đã chết, LogError) | Instantiate(prefab) ở gốc scene
                   ─> SetParent(parent) ─> cast T ─> _entryByActiveInstance.Add ─> SetActive(true) ─> OnGetFromPool

Return(instance)   _entryByActiveInstance.Remove ─ không có ⇒ LogError, bỏ
                   ─> instance đã chết ⇒ LogError, bỏ
                   ─> OnReturnToPool ─> Inactive.Count ≥ maxPoolSize ⇒ Destroy
                                      | SetActive(false) ─> SetParent(pool) ─> Inactive.Push

rời màn   ReleaseAll() ─> _warmCts.Cancel ─> Destroy mọi active còn sống ─> Destroy mọi inactive còn sống
                       ─> prefab.ReleaseAsset ─> hai bảng rỗng
```

**Hai bảng, một chủ ghi.** `_entryByKey: Dictionary<TKey, PoolEntry>` là pool của mỗi key (prefab, `maxPoolSize`, `Stack`
inactive). `_entryByActiveInstance: Dictionary<Component, PoolEntry>` là instance đang active và pool nó thuộc về: `Return`
tra bảng này nên không cần key, và `Remove` trả `false` chính là guard trả hai lần hay object lạ. Mỗi instance ở đúng một
trong ba chỗ: bảng active, stack inactive, hoặc đã `Destroy`. Hash của `UnityEngine.Object` là instanceID, còn dùng được khi
object đã bị huỷ, nên `Remove` vẫn dọn được slot của instance đã chết.

## 3. Vòng đời một instance

Thứ tự callback Unity và hook trong mỗi thao tác. Prefab gốc active thì `Awake`/`OnEnable` chạy ngay lúc `Instantiate`.

| Thao tác | Thứ tự |
|---|---|
| Warm | `Instantiate` con của pool → `Awake`, `OnEnable` → `SetActive(false)` → `OnDisable` |
| `Get`, lấy từ stack | `SetParent(parent)` → `SetActive(true)` → `OnEnable` → `OnGetFromPool` |
| `Get`, stack rỗng | `Instantiate` ở gốc scene → `Awake`, `OnEnable` → `SetParent(parent)` → `OnGetFromPool` |
| `Return` | `OnReturnToPool` → `SetActive(false)` → `OnDisable` → `SetParent(pool)` |
| `Return`, stack đầy | `OnReturnToPool` → `Destroy` (cuối frame `OnDisable`, `OnDestroy`) |
| `ReleaseAll` | `Destroy` (cuối frame `OnDisable`, `OnDestroy`); **không** `OnReturnToPool` |

Hệ quả: `OnEnable` chạy cả lúc warm, và chạy **trước** `OnGetFromPool` khi lấy từ stack → logic bắt đầu vòng đời đặt trong
`OnGetFromPool`. Dọn dẹp bắt buộc (huỷ đăng ký event, dừng tween) đặt ở chỗ cả `OnReturnToPool` lẫn `OnDestroy` cùng gọi,
vì `ReleaseAll` bỏ qua `OnReturnToPool`.

## 4. Viết prefab và gọi pool

```csharp
public sealed class Piece : PoolableBehaviour
{
    private Tween _idle;

    public override void OnGetFromPool() => _idle = Tween.Scale(transform, 1.05f, 0.6f, cycles: -1, cycleMode: CycleMode.Yoyo);

    public override void OnReturnToPool() => StopAll();

    private void OnDestroy() => StopAll();

    private void StopAll()
    {
        _idle.Stop();
        transform.localScale = Vector3.one;
    }
}

// phía dùng
Piece piece = IPoolService<MyPool>.Service.Get<Piece>(MyPool.Piece, row);
piece.transform.localPosition = slotPosition;
// ...
IPoolService<MyPool>.Service.Return(piece);
```

| Luật | Vì sao |
|---|---|
| Reset mọi trạng thái trong `OnGetFromPool`/`OnReturnToPool` | instance mang trạng thái của lần dùng trước |
| Đặt vị trí sau `Get` | `SetParent` giữ world transform: instance lấy từ stack đứng ở vị trí lúc trả, instance mới đứng ở vị trí gốc của prefab |
| Không `Return` trong `OnDestroy` | `ReleaseAll` đã xoá bảng trước khi `OnDestroy` chạy: `LogError` `was not found in any pool` cho từng instance |
| Không `Destroy` instance đang ở trong pool | `Get` sau đó gặp instance chết: `LogError` `is destroyed, skip`, lấy cái kế |
| Không giữ tham chiếu sau `Return` | instance có thể bị lấy ra cho chỗ khác, hoặc bị `Destroy` khi stack đầy |
| Một nơi duy nhất gọi `Return` cho mỗi instance | `Return` hai lần: `LogError`, lần sau bỏ qua |

## 5. Huỷ warm và Addressables

Token warm là token con link với token của caller; `ReleaseAll` huỷ nó để load dở không ghi vào bảng sau khi đã release.
`await` qua `SuppressCancellationThrow()`: huỷ là nhánh `bool`, không ném, nên `await WarmAsync` của caller cũng kết thúc bình
thường dù bị huỷ. `finally` gọi `prefab.ReleaseAsset()` khi huỷ hay load lỗi; load lỗi thì ngoại lệ ném tiếp ra `WarmAsync`.

Dùng `ReleaseAsset()` chứ không `Addressables.Release(handle)`: chỉ `ReleaseAsset` xoá handle mà `AssetReference` cache, nếu
không lần `LoadAssetAsync` sau trả handle rỗng.

## 6. Guard

| Ca | Xử lý |
|---|---|
| `Get` key chưa warm | `LogError`, trả `null` |
| `Get` với `T` không phải component `IPoolable` của prefab | `InvalidCastException`; instance đã `SetParent(parent)`, không vào bảng nào, `ReleaseAll` không huỷ nó |
| `Get` với `parent` là `null` | instance nằm ở gốc scene |
| Instance inactive bị `Destroy` từ ngoài | `Get` bỏ qua kèm `LogError`, lấy cái kế hoặc `Instantiate` |
| `Return` hai lần, hoặc instance không lấy từ pool | `LogError`, bỏ qua; không gọi hook, không chạm object |
| `Return(null)` | `ArgumentNullException` từ `Dictionary.Remove` |
| `Return` instance đã bị huỷ | slot active được dọn, `LogError`, không chạm object |
| `Return` khi `Inactive.Count ≥ maxPoolSize` | `OnReturnToPool` rồi `Destroy` |
| `Return` sau `ReleaseAll` | `LogError` `was not found in any pool` |
| `WarmAsync` chồng lên warm đang chạy | `LogError`, bỏ lượt sau |
| `WarmAsync` khi key đã có (gọi lại mà chưa `ReleaseAll`) | `LogError`, bỏ key đó |
| Prefab không có `IPoolable`, hoặc hai config cùng key | `LogError`, `ReleaseAsset`, bỏ config |
| `initialPoolSize > maxPoolSize` | warm tạo đủ `initialPoolSize`; `Return` `Destroy` cho tới khi stack dưới `maxPoolSize` |
| `ReleaseAll` gọi lại khi đã rỗng | không log, không throw |

## 7. Quyết định

| Câu hỏi | Chọn | Tiêu chí → kết luận |
|---|---|---|
| `Return` có cần key? | không — tra `_entryByActiveInstance` | caller chỉ cầm instance; bảng active cũng là guard trả hai lần → một dictionary, hai việc |
| Chặn số active? | không, chỉ chặn số inactive | `Get` từ chối thì caller phải xử lý `null` mỗi chỗ gọi; đỉnh tạm thời được `Return` cắt về `maxPoolSize` |
| `ReleaseAll` gọi `OnReturnToPool`? | không | release xảy ra khi rời màn, object sắp bị huỷ; reset là thừa, dọn dẹp thuộc `OnDestroy` |
| Huỷ warm | `SuppressCancellationThrow` | rời màn giữa lúc warm là luồng bình thường, không phải lỗi; không bắt `OperationCanceledException` ở caller |
| Release prefab | `AssetReference.ReleaseAsset` | xoá cả handle cache trong `AssetReference` → warm lần sau load lại được |
| Kiểm config | nút Editor | lỗi config là lỗi lúc dựng; kiểm lúc boot tốn mỗi lần chạy |

## 8. Chẩn đoán

| Log `[PoolService]: …` | Nguyên nhân |
|---|---|
| `pool '{key}' has not been warmed.` | `Get` trước `WarmAsync`, sau `ReleaseAll`, warm bị huỷ, hoặc config của key lỗi lúc warm |
| `{instance} was not found in any pool.` | `Return` hai lần, object không lấy từ pool, `Return` sau `ReleaseAll` (kể cả trong `OnDestroy`) |
| `instance in pool {key} has been destroyed before returning.` | cha của instance bị huỷ trước khi `Return` (Trước khi chạy, bước 7) |
| `An instance in pool {key} is destroyed, skip.` | có chỗ `Destroy` instance sau khi đã `Return` |
| `WarmAsync is running, skip overlap calling.` | gọi `WarmAsync` khi lần trước chưa xong |
| `Pool {key} has already existed.` | `WarmAsync` lần hai mà chưa `ReleaseAll` |
| `Prefab {key} is invalid.` | ô **Prefab** trống |
| `prefab {name} of {key} doesn't implement IPoolable.` | prefab thiếu component `IPoolable` trên GameObject gốc |
| `Pool with key '{key}' already exists.` | hai phần tử **Pool Configs** cùng **Key** |
| `poolConfigs[{i}] '{key}' has not assigned prefab yet.` | từ **Validate Configs**: ô **Prefab** trống |
| `poolConfigs[{j}] and [{i}] have the same key '{key}'.` | từ **Validate Configs**: key trùng |

## 9. Bộ nhớ theo nhịp

Mỗi `Get`/`Return`: 0 byte heap sau khi hai dictionary đã đủ sức chứa (hai thao tác dictionary, một push/pop, một cast tham
chiếu). Mỗi warm: một mảng `UniTask[n]` và một CTS. Mỗi instance: một slot dictionary (~24 B) khi active, một slot stack
(8 B) khi inactive; `Stack` đặt capacity = `maxPoolSize` ngay từ đầu nên không cấp phát lại.

## Cấu trúc file

```
Abstractions/Foundations/Pooling/
├── IPoolable.cs            OnGetFromPool, OnReturnToPool
├── IPoolService.cs         WarmAsync, Get<T>, Return<T>, ReleaseAll — IService<IPoolService<TKey>>
└── PoolableBehaviour.cs    MonoBehaviour + IPoolable rỗng để override

Implementations/Foundations/Pooling/
├── PoolConfig.cs           [Serializable] key, prefab, initialPoolSize, maxPoolSize
├── PoolService.cs          abstract partial PoolService<TKey> : MonoBehaviour — hai bảng, WarmAsync, Get/Return, ReleaseAll
└── PoolService.Editor.cs   partial, #if UNITY_EDITOR, nút ValidateConfigs
```

## Tests

`Tests/PlayMode/PoolServiceTests.cs` — host `TestPoolService : PoolService<TestPool>` (`TestPoolService.cs`, cùng
`TestPoolable` đếm số lần hook và `OtherPoolable` cho ca cast sai). Không chạm Addressables: entry `PoolEntry` chèn qua
reflection vào `_entryByKey` với prefab là GameObject tạo lúc chạy và `AssetReference` rỗng (`IsValid()` false nên
`ReleaseAll` bỏ qua `ReleaseAsset`). Phủ: `Get` khi pool rỗng / có sẵn / `parent` null / key chưa warm / `T` sai / instance
inactive đã bị huỷ ngoài; `Return` bình thường / hai lần / instance lạ / chạm `maxPoolSize` (`Destroy` sau một frame) /
`null` / sau `ReleaseAll`; `ReleaseAll` với 2 active + 3 inactive rồi gọi lần hai; 10 000 vòng `Get`/`Return` đo 0 B bằng
`AllocationMeter` (`Tests/PlayMode/AllocationMeter.cs`, dùng chung với `EnumKeyedLookupAllocationTests`; số thật trên player
IL2CPP). `WarmAsync` (load, chặn gọi chồng, huỷ từ `ReleaseAll`) cần Addressables build thật — kiểm tay trong Play.
