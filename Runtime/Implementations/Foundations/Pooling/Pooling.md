# Pooling — `PoolService<TKey>`

Pool instance của prefab Addressables theo **key enum của dự án**. Dự án gọi `WarmAsync` khi vào màn dùng pool và `ReleaseAll` khi
rời nó; giữa hai mốc đó pool sống qua mọi level. Mục tiêu: `Get`/`Return` không cấp phát, mọi guard O(1), prefab và instance ra
khỏi memory khi release.

**Ranh giới.** Horcrux giữ cơ chế (bảng tra, warm, release, guard). Dự án giữ từ vựng: enum key, lớp con mang `[Service]`, thời
điểm warm và release, hook reset trong `IPoolable` của từng prefab.

## API

| Thành viên | Chữ ký | Ghi chú |
|---|---|---|
| Lấy | `T Get<T>(TKey key, Transform parent) where T : Component, IPoolable` | instance đã active, đã `SetParent(parent)` (giữ world transform), đã gọi `OnGetFromPool`. Caller tự đặt vị trí. Key chưa warm: `LogError`, trả `null` |
| Trả | `void Return<T>(T instance) where T : Component, IPoolable` | gọi `OnReturnToPool` rồi cất; pool đầy thì `Destroy`. Không cần key |
| Xả | `void ReleaseAll()` | huỷ mọi instance active lẫn inactive, `ReleaseAsset` prefab, huỷ warm đang dở. `WarmAsync` lần sau dựng lại từ đầu |
| Warm | `UniTask WarmAsync(CancellationToken ct)` | load prefab mọi config song song rồi pre-warm `initialPoolSize`. Chỉ gọi khi pool rỗng: key đã có là `LogError`. `OnDestroy` gọi `ReleaseAll` |
| Hook prefab | `IPoolable { OnGetFromPool(); OnReturnToPool(); }` · `PoolableBehaviour` là bản rỗng để `override` | mọi lối ra khỏi vòng đời (kể cả `Destroy` khi đầy) đi qua `OnReturnToPool` |
| Cấu hình | `PoolConfig<TKey> { TKey key; AssetReference prefab; [Min(0)] int initialPoolSize = 3; [Min(1)] int maxPoolSize = 5; }` | mảng `poolConfigs` trên component; số mặc định là nháp |
| Kiểm config | nút `ValidateConfigs` trên Inspector (partial Editor) | báo prefab trống và key trùng; không chạy lúc boot |

Truy cập qua `IPoolService<TKey>.Service` (bắt buộc, thiếu thì throw) hoặc `TryGet`.

## Luồng

```
vào màn     await IPoolService<TKey>.Service.WarmAsync(ct) ─> mỗi config song song: key đã có ⇒ LogError, bỏ · prefab trống ⇒ LogError, bỏ
                                                            ─> prefab.LoadAssetAsync ─> GetComponent<IPoolable> ─> entry ─> Instantiate × initialPoolSize (inactive, con của pool)
rời màn     IPoolService<TKey>.Service.ReleaseAll() ─> huỷ warm dở ─> Destroy mọi active ─> Destroy mọi inactive ─> prefab.ReleaseAsset ─> hai bảng rỗng
giữa hai mốc (Retry, level kế)  không chạm pool; entity Return trong Dispose của nó

Get(key, parent)   _entryByKey[key] ─> Pop instance còn sống | Instantiate(prefab) ─> SetParent(parent) ─> cast T ─> _entryByActiveInstance.Add ─> SetActive(true) ─> OnGetFromPool
Return(instance)   _entryByActiveInstance.Remove(instance, out entry) ─> OnReturnToPool ─> Inactive.Count ≥ max ⇒ Destroy | SetActive(false) ─> SetParent(pool) ─> Inactive.Push
```

**Hai bảng, một chủ ghi.** `_entryByKey: Dictionary<TKey, PoolEntry>` là pool của mỗi key (prefab, `maxPoolSize`, `Stack` inactive).
`_entryByActiveInstance: Dictionary<Component, PoolEntry>` là instance đang active và pool nó thuộc về: `Return` tra bảng này nên
không cần key, và `Remove` trả `false` chính là guard trả hai lần hay object lạ. Mỗi instance ở đúng một trong ba chỗ: bảng
active, stack inactive, hoặc đã Destroy. Hash của `UnityEngine.Object` là instanceID nên hai thao tác dictionary không cấp phát.

**Huỷ warm.** Token warm là token con link với token của caller; `ReleaseAll` huỷ nó để load dở không ghi vào bảng sau khi đã release.
`await` qua `SuppressCancellationThrow()`: huỷ là nhánh `bool`, không ném; `finally` gọi `prefab.ReleaseAsset()` khi huỷ hay load lỗi.
Dùng `ReleaseAsset()` chứ không `Addressables.Release(handle)`: chỉ `ReleaseAsset` xoá handle mà `AssetReference` cache, nếu không
lần `LoadAssetAsync` sau trả handle rỗng.

## Trước khi chạy

| # | Thao tác | Thiếu thì |
|---|---|---|
| 1 | Dự án khai `enum` key số tường minh từ 1 và lớp con `[Service(typeof(IPoolService<Key>), FindFromScene = true)] class XPoolService : PoolService<Key> { }` | không có service để `IPoolService<Key>.Service` tra; throw ở lần gọi đầu |
| 2 | Scene persistent: GameObject có `XPoolService`, điền `poolConfigs` (key, prefab Addressables, hai cỡ); bấm `ValidateConfigs` | prefab trống hay key trùng: `LogError` ở warm, key đó không có pool |
| 3 | Dự án `await IPoolService<Key>.Service.WarmAsync(ct)` trước khi load màn dùng pool, **đúng một lần mỗi lần vào** (đọc chuyển màn ở chỗ đổi scene, không gọi mỗi level) | không gọi: `Get` log `has not been warmed`, trả `null` · gọi mỗi level: `LogError` `has already existed` cho từng key |
| 4 | Mỗi prefab có một component `IPoolable` (hoặc `PoolableBehaviour`); `T` khi `Get` đúng component đó | thiếu `IPoolable`: `LogError` ở warm, bỏ config · `T` sai: `InvalidCastException` ở lần `Get` đầu |
| 5 | Dự án gọi `ReleaseAll()` khi rời màn dùng pool; entity **không** `Return` trong `OnDestroy` | không gọi: bundle prefab nằm lại memory · `Return` trong `OnDestroy`: `LogError` khi `ReleaseAll` huỷ active |

## Guard

| Ca | Xử lý |
|---|---|
| `Get` key chưa warm | `LogError`, trả `null` |
| Instance inactive bị Destroy từ ngoài | `Get` bỏ qua kèm `LogError`, lấy cái kế hoặc Instantiate |
| `Return` hai lần, hoặc instance không lấy từ pool | `LogError`, bỏ qua |
| `Return(null)` | `ArgumentNullException` từ `Dictionary.Remove` |
| `Return` instance đã Destroy | slot active được dọn, `LogError`, không chạm object |
| `Return` khi `Inactive.Count ≥ maxPoolSize` | `OnReturnToPool` rồi `Destroy` |
| Warm chồng lên warm đang chạy | `LogError`, bỏ lượt sau |
| `WarmAsync` khi key đã có (gọi lại mà chưa `ReleaseAll`) | `LogError`, bỏ key đó |
| Prefab không có `IPoolable`, hoặc hai config cùng key | `LogError`, `ReleaseAsset`, bỏ config |
| `ReleaseAll` gọi lại khi đã rỗng | không log, không throw |

## Bộ nhớ theo nhịp

Mỗi `Get`/`Return`: 0 byte heap sau khi hai dictionary đã đủ sức chứa (hai thao tác dictionary, một push/pop, một cast tham chiếu);
chuỗi lỗi chỉ dựng ở nhánh lỗi. Mỗi warm: một mảng `UniTask[n]` và một CTS. Mỗi instance: một slot dictionary (~24 B) khi
active, một slot stack (8 B) khi inactive; `Stack` đặt capacity = `maxPoolSize` ngay từ đầu nên không cấp phát lại.

## Cấu trúc file

```
Abstractions/Foundations/Pooling/
├── IPoolable.cs            OnGetFromPool, OnReturnToPool
├── IPoolService.cs         Get<T>, Return<T>, ReleaseAll — IService<IPoolService<TKey>>
└── PoolableBehaviour.cs    MonoBehaviour + IPoolable rỗng để override

Implementations/Foundations/Pooling/
├── PoolConfig.cs           [Serializable] key, prefab, initialPoolSize, maxPoolSize
├── PoolService.cs          abstract partial PoolService<TKey> : MonoBehaviour — hai bảng, WarmAsync, Get/Return, ReleaseAll
└── PoolService.Editor.cs   partial, #if UNITY_EDITOR, nút ValidateConfigs
```

## Tests

`Tests/PlayMode/PoolServiceTests.cs` (viết sau khi có caller đầu tiên): host `TestPoolService : PoolService<TestPool>`, entry chèn
qua reflection với prefab tạo lúc chạy; các case ở bảng Guard, cộng 10 000 vòng `Get`/`Return` đo 0 B cấp phát.
