# Plan — `AssetVariant`: bản asset chọn theo tên đến từ ngoài build, vào Horcrux `Runtime/Abstractions/Composites`

Bản tạm: category-jam `Scripts/Level/LevelSetHost.cs`, `Scripts/Level/LevelSetResolver.cs`; phép kiểm Editor đã ở Horcrux (`Editor/AddressablesLayout/AssetVariantCheck.cs`).
**Phụ thuộc:** không — gõ lúc nào cũng được.
Kiểm (từ gốc dự án): `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,Horcrux.EditModeTests.csproj --tests Horcrux.EditModeTests`.

## Kịch bản

| Tình huống | Hệ làm gì | Caller, QA thấy |
|---|---|---|
| boot, remote = tên có trong build | lấy tập tên theo label, chọn remote, nạp | `Current` = bản đó, `CurrentSource` `Remote` |
| remote rỗng hoặc chỉ khoảng trắng | bỏ qua, không log | bản mặc định, `Default` |
| remote là tên lạ (gõ sai, build cũ chưa có bản) | `LogError` `[<lớp con>]: Remote = "<tên>" không có trong build (label <label>), bỏ qua.` | bản mặc định |
| tên kèm khoảng trắng, xuống dòng hai đầu | bỏ trước khi so | chọn đúng bản |
| bản được chọn nạp hỏng (bundle thiếu) | `LogError`, nạp bản mặc định trong cùng lượt | bản mặc định |
| bản mặc định cũng hỏng | `LogError` `bản mặc định cũng không nạp được.` | `Current` null |
| catalog hỏng khi lấy tập tên | `LogError`, tập rỗng | mọi tên bị coi là lạ, bản mặc định |
| cheat `SelectAsync(tên)` | lưu PlayerPrefs ngay, chọn và nạp lại | `Current` đổi, `Override`; mở lại app vẫn bản đó |
| cheat `ClearOverrideAsync` | xoá khoá, chọn lại | về bản của remote |
| tool Editor gửi tên sang Play (chơi thử) | thắng mọi nguồn | `EditorHandoff` |
| cheat bấm hai bản liền nhau | lượt đầu nạp xong thấy đã bị thay: nhả thứ vừa nạp, dừng cả bước lùi | chỉ bản bấm sau cùng thắng |
| host bị huỷ giữa lượt nạp | như trên | `Current` không đổi |
| đổi bản lúc consumer đang mượn thứ trong bản cũ | nạp bản mới xong mới nhả bản cũ | thứ đang mượn không bị unload |

## Flow

```
Bootstrap ──InitializeAsync──► AAssetVariantHost<TAsset>                  Runtime/Abstractions/Composites/AssetVariant/AAssetVariantHost.cs
cheat dự án ──SelectAsync / ClearOverrideAsync──►│  ├─ LoadAvailableNamesAsync ──LoadResourceLocationsAsync(Label, TAsset)──► Addressables
                                                 │  ├─ ApplyAsync: request = ++_lastLoadRequest
                                                 │  │    BuildCandidates: EditorHandoffName · PlayerPrefs[OverridePrefsKey] · RemoteName   ◄── lớp con dự án
                                                 │  │    TryPick ──────────────────────────────────────────────────► AssetVariantPicker   …/AssetVariantPicker.cs
                                                 │  │    TrySwitchToAsync(tên đã chọn) ──hỏng──► TrySwitchToAsync(defaultVariant.RuntimeKey)
                                                 │  └─ TrySwitchToAsync ──LoadAssetAsync<TAsset>(key)──► Addressables
                                                 │        bị thay / huỷ: Release handle vừa nạp · xong: Release handle cũ, ghi Current, CurrentSource
consumer dự án ◄── Current · CurrentSource · AvailableNames
authoring: AssetVariantCheck.Validate / IsSelectable (Editor, đã có) giữ address = tên asset, label, một group Pack Together, GUID in Catalog
```

## Thuật toán

| Bước | Luật |
|---|---|
| chọn | duyệt ứng viên theo thứ tự danh sách; `Trim`; rỗng → bỏ qua · có trong tập → chọn · còn lại → `rejected`, đi tiếp. Hết danh sách → false |
| mã lượt | lấy **một lần** đầu `ApplyAsync`; bước chính và bước lùi về mặc định nhận cùng mã |
| sau mỗi lần nạp | `request != _lastLoadRequest` hoặc token huỷ → nhả handle vừa nạp, trả true (lượt hết việc) · nạp hỏng → false (đi bước lùi) |
| ghi | nhả handle cũ **sau** khi giữ handle mới; mọi bản chung một bundle nên bundle không unload giữa hai bước |

Bản mẫu: bản tạm category-jam chạy cùng luật, 7 case EditMode của `TryPick` xanh; đối chứng ghi cạnh case. Code trong Plan đã build cùng lớp con của category-jam, 0 lỗi.

---

## GĐ1 — xong thì lớp con khai label và tên remote là có bản chạy cả phiên, có đường lui về bản mặc định

```
+ Bootstrap ──InitializeAsync──► AAssetVariantHost<TAsset>+ ──TryPick+──► AssetVariantPicker+
                                   ├─ LoadResourceLocationsAsync+ · LoadAssetAsync+ ──► Addressables
                                   └─ RemoteName+ · Label+ ◄── lớp con dự án
```

`Runtime/Abstractions/Composites/AssetVariant/AssetVariantPicker.cs` — file mới:

```csharp
using System.Collections.Generic;

namespace Horcrux.Runtime.Abstractions.AssetVariant
{
    /// <summary>Nơi tên bản đến. Thứ tự ưu tiên do host dựng danh sách ứng viên, không theo thứ tự khai báo.</summary>
    public enum AssetVariantSource
    {
        Default,
        Remote,
    }

    /// <summary>Một tên bản kèm nơi nó đến.</summary>
    public readonly struct AssetVariantCandidate
    {
        public string Name { get; }
        public AssetVariantSource Source { get; }

        public AssetVariantCandidate(string name, AssetVariantSource source)
        {
            Name = name;
            Source = source;
        }
    }

    /// <summary>Chọn tên bản từ các ứng viên theo thứ tự trong danh sách. Hàm thuần: host quyết log ứng viên bị loại.</summary>
    public static class AssetVariantPicker
    {
        /// <summary>
        /// Ứng viên đầu tiên không rỗng và có trong <paramref name="availableNames"/>, so sau khi bỏ khoảng trắng hai đầu. Ứng viên
        /// không rỗng mà không có thì cộng vào <paramref name="rejected"/>. Không ai hợp lệ thì false: caller dùng bản mặc định.
        /// </summary>
        public static bool TryPick(IReadOnlyList<AssetVariantCandidate> candidates, ICollection<string> availableNames,
            List<AssetVariantCandidate> rejected, out AssetVariantCandidate picked)
        {
            foreach (var candidate in candidates)
            {
                // Tên dán lên console hay kèm khoảng trắng, xuống dòng.
                var name = candidate.Name?.Trim();
                // Rỗng là "nguồn này không nói gì" (chưa có cheat, không chơi thử), không phải giá trị sai.
                if (string.IsNullOrEmpty(name)) continue;

                if (availableNames.Contains(name))
                {
                    picked = new AssetVariantCandidate(name, candidate.Source);
                    return true;
                }

                rejected.Add(candidate);
            }

            picked = default;
            return false;
        }
    }
}
```

`Runtime/Abstractions/Composites/AssetVariant/AAssetVariantHost.cs` — file mới:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Bootstrap;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace Horcrux.Runtime.Abstractions.AssetVariant
{
    /// <summary>
    /// Bản đang chạy của một asset có nhiều bản, chọn theo tên đến từ ngoài build. Lúc boot chọn theo tên remote → bản mặc
    /// định, nạp qua Addressables theo address = tên asset. Đứng yên cả phiên.
    /// </summary>
    public abstract class AAssetVariantHost<TAsset> : ABootStep where TAsset : Object
    {
        [SerializeField, Tooltip("Bản chạy khi không tên nào hợp lệ, hoặc bản được chọn nạp hỏng.")]
        private AssetReferenceT<TAsset> defaultVariant;

        private readonly List<string> _availableNames = new();
        private readonly List<AssetVariantCandidate> _candidates = new();
        private readonly List<AssetVariantCandidate> _rejected = new();
        private AsyncOperationHandle<TAsset> _currentHandle;
        private int _lastLoadRequest;

        #region Properties

        /// <summary>Bản đang chạy; null chỉ khi cả bản được chọn lẫn bản mặc định đều nạp hỏng.</summary>
        public TAsset Current { get; private set; }

        public AssetVariantSource CurrentSource { get; private set; }

        /// <summary>Address mang <see cref="Label"/>, sắp theo tên; nạp lúc boot.</summary>
        public IReadOnlyList<string> AvailableNames => _availableNames;

        /// <summary>Label Addressables của mọi bản chọn được; tập tên hợp lệ lấy theo label này.</summary>
        protected abstract string Label { get; }

        /// <summary>Tên remote config của phiên; rỗng là remote không chọn.</summary>
        protected abstract string RemoteName { get; }

        #endregion

        #region API

        public override async UniTask InitializeAsync(CancellationToken ct)
        {
            await LoadAvailableNamesAsync();
            await ApplyAsync(ct);
        }

        #endregion

        #region Class Methods

        private async UniTask LoadAvailableNamesAsync()
        {
            _availableNames.Clear();
            var handle = Addressables.LoadResourceLocationsAsync(Label, typeof(TAsset));
            try
            {
                foreach (var location in await handle.ToUniTask())
                    if (!_availableNames.Contains(location.PrimaryKey)) _availableNames.Add(location.PrimaryKey);
            }
            catch (Exception exception) // catalog Addressables hỏng
            {
                Debug.LogError($"[{GetType().Name}]: không đọc được danh sách bản (label {Label}): {exception.Message}", this);
            }
            finally
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }

            _availableNames.Sort(StringComparer.Ordinal);
        }

        private async UniTask ApplyAsync(CancellationToken ct)
        {
            // Mã của cả lượt, lấy một lần: bước lùi về bản mặc định dùng chung mã với bước chính.
            int request = ++_lastLoadRequest;
            BuildCandidates();
            _rejected.Clear();
            bool isPicked = AssetVariantPicker.TryPick(_candidates, _availableNames, _rejected, out var picked);
            foreach (var rejected in _rejected)
                Debug.LogError($"[{GetType().Name}]: {rejected.Source} = \"{rejected.Name}\" không có trong build (label {Label}), bỏ qua.", this);

            if (isPicked && await TrySwitchToAsync(picked.Name, picked.Source, request, ct)) return;
            if (await TrySwitchToAsync(defaultVariant.RuntimeKey, AssetVariantSource.Default, request, ct)) return;

            Debug.LogError($"[{GetType().Name}]: bản mặc định cũng không nạp được.", this);
        }

        private void BuildCandidates()
        {
            _candidates.Clear();
            _candidates.Add(new AssetVariantCandidate(RemoteName, AssetVariantSource.Remote));
        }

        // false chỉ khi nạp hỏng. Lượt đã bị lượt sau thay hoặc bị huỷ trả true: lượt này không còn việc gì.
        private async UniTask<bool> TrySwitchToAsync(object key, AssetVariantSource source, int request, CancellationToken ct)
        {
            var handle = Addressables.LoadAssetAsync<TAsset>(key);
            TAsset variant = null;
            try
            {
                variant = await handle.ToUniTask();
            }
            catch (Exception exception) // bundle thiếu, hỏng
            {
                Debug.LogError($"[{GetType().Name}]: không nạp được bản {key}: {exception.Message}", this);
            }

            bool isSuperseded = request != _lastLoadRequest || ct.IsCancellationRequested;
            if (variant == null || isSuperseded)
            {
                if (handle.IsValid()) Addressables.Release(handle);
                return isSuperseded;
            }

            // Nạp bản mới xong mới nhả bản cũ: mọi bản chung một bundle (Pack Together), handle mới đã giữ bundle nên thứ bản
            // cũ đang cho mượn (level đang chơi) không bị unload.
            if (_currentHandle.IsValid()) Addressables.Release(_currentHandle);
            _currentHandle = handle;
            Current = variant;
            CurrentSource = source;
            return true;
        }

        #endregion
    }
}
```

`Tests/EditMode/AssetVariantPickerTests.cs` (agent) — tập `{ "Variant_A", "Variant_B" }`, ứng viên dựng tay, mọi ứng viên nguồn `Remote` trừ ca ghi rõ:

| Case | Input | Kỳ vọng |
|---|---|---|
| một tên hợp lệ | `"Variant_B"` | true, `Variant_B`, `Remote`, `rejected` rỗng |
| rỗng bỏ qua im lặng | `null`, `""`, `"Variant_A"` | true, `Variant_A`, `rejected` rỗng — đối chứng: bỏ nhánh rỗng thì `rejected` có 2 phần tử |
| tên lạ rồi tên đúng | `"Variant_Gone"`, `"Variant_A"` | true, `Variant_A`; `rejected` đúng một phần tử `Variant_Gone` — đối chứng: trả false ở tên lạ đầu thì đỏ |
| khoảng trắng hai đầu | `"  "`, `" Variant_B\n"` | true, tên trả về `Variant_B` (đã trim), `rejected` rỗng |
| không ai hợp lệ | `"Typo"` | false, `rejected` một phần tử |
| tên đúng đứng trước tên đúng | `"Variant_B"`, `"Variant_A"` | `Variant_B` — thứ tự danh sách là ưu tiên |

---

## GĐ2 — xong thì QA đổi bản giữa phiên bằng cheat, giữ qua các lần mở app, và tool Editor ép bản cho lượt chơi thử

```
  cheat dự án ──SelectAsync+ / ClearOverrideAsync+──► AAssetVariantHost ──PlayerPrefs[OverridePrefsKey]+
                                                        BuildCandidates: EditorHandoffName+ · Override+ · RemoteName
```

`AssetVariantPicker.cs` › `AssetVariantSource` — thêm sau `Remote`:

```csharp
        Default,
        Remote,
        Override,
        EditorHandoff,
```

`AAssetVariantHost.cs` › khai báo class — cũ → mới:

```csharp
    /// <summary>
    /// Bản đang chạy của một asset có nhiều bản, chọn theo tên đến từ ngoài build. Lúc boot chọn theo tên remote → bản mặc
    /// định, nạp qua Addressables theo address = tên asset. Đứng yên cả phiên.
    /// </summary>
```

```csharp
    /// <summary>
    /// Bản đang chạy của một asset có nhiều bản, chọn theo tên đến từ ngoài build. Lúc boot chọn theo tên tool Editor gửi sang
    /// → tên cheat đã lưu → remote → bản mặc định, nạp qua Addressables theo address = tên asset. Đứng yên cả phiên; chỉ lệnh
    /// cheat đổi giữa phiên.
    /// </summary>
```

`AAssetVariantHost.cs` › `Properties` — thêm `OverridePrefsKey` sau `Label`, `EditorHandoffName` sau `RemoteName`:

```csharp
        /// <summary>Label Addressables của mọi bản chọn được; tập tên hợp lệ lấy theo label này.</summary>
        protected abstract string Label { get; }

        /// <summary>Khoá PlayerPrefs giữ tên cheat đã chọn qua các phiên.</summary>
        protected abstract string OverridePrefsKey { get; }

        /// <summary>Tên remote config của phiên; rỗng là remote không chọn.</summary>
        protected abstract string RemoteName { get; }

        /// <summary>Tên tool Editor gửi sang Play (chơi thử); null khi không có. Thắng mọi nguồn khác.</summary>
        protected virtual string EditorHandoffName => null;
```

`AAssetVariantHost.cs` › `API` — thêm sau `InitializeAsync`:

```csharp
        /// <summary>Cheat: lưu tên đè remote qua các phiên rồi nạp.</summary>
        public UniTask SelectAsync(string variantName)
        {
            PlayerPrefs.SetString(OverridePrefsKey, variantName);
            // Lưu ngay: QA chọn xong hay tắt app bằng cách vuốt, không qua mốc lưu nào.
            PlayerPrefs.Save();
            return ApplyAsync(destroyCancellationToken);
        }

        /// <summary>Cheat: bỏ tên đã lưu, chọn lại theo remote của phiên.</summary>
        public UniTask ClearOverrideAsync()
        {
            PlayerPrefs.DeleteKey(OverridePrefsKey);
            PlayerPrefs.Save();
            return ApplyAsync(destroyCancellationToken);
        }
```

`AAssetVariantHost.cs` › `Class Methods` › `BuildCandidates` — cũ → mới:

```csharp
        private void BuildCandidates()
        {
            _candidates.Clear();
            _candidates.Add(new AssetVariantCandidate(RemoteName, AssetVariantSource.Remote));
        }
```

```csharp
        private void BuildCandidates()
        {
            _candidates.Clear();
            _candidates.Add(new AssetVariantCandidate(EditorHandoffName, AssetVariantSource.EditorHandoff));
            _candidates.Add(new AssetVariantCandidate(PlayerPrefs.GetString(OverridePrefsKey, string.Empty), AssetVariantSource.Override));
            _candidates.Add(new AssetVariantCandidate(RemoteName, AssetVariantSource.Remote));
        }
```

`Tests/EditMode/AssetVariantPickerTests.cs` (agent) — ứng viên theo thứ tự `BuildCandidates`: `EditorHandoff`, `Override`, `Remote`:

| Case | Input | Kỳ vọng |
|---|---|---|
| chỉ remote | `""`, `""`, `"Variant_B"` | `Remote` |
| cheat thắng remote | `""`, `"Variant_A"`, `"Variant_B"` | `Variant_A`, `Override` |
| chơi thử thắng cheat | `"Variant_B"`, `"Variant_A"`, `"Variant_A"` | `Variant_B`, `EditorHandoff` |
| cheat lạ | `""`, `"Variant_Gone"`, `"Variant_A"` | `Remote`; `rejected[0]` là `Variant_Gone`, nguồn `Override` |

Hành vi của host (Addressables, PlayerPrefs) nghiệm thu bằng chơi thử ở Chuyển dự án.

---

## Chuyển dự án (agent)

| Chỗ | Đổi |
|---|---|
| `Scripts/Level/LevelSetHost.cs` | thành `LevelSetHost : AAssetVariantHost<CategoryLevelDatabase>, IInitializable<IGameRemoteConfig>`, giữ `[Service(FindFromScene = true)]` (attribute không kế thừa) và `LABEL`; override `Label` = `LABEL` · `OverridePrefsKey` = `"level_set_cheat"` · `RemoteName` = `_remoteConfig.LevelSet.Value` · `EditorHandoffName` = `levelSetName` của `PlaytestHandoff.TryRead`, không có thì null. Bỏ mọi thứ còn lại |
| `Scripts/Level/LevelSetResolver.cs`, `Tests/EditMode/LevelSetResolverTests.cs` | xoá cùng `.meta`; grep `LevelSetResolver`, `LevelSetCandidate`, `LevelSetSource` trong `Assets/` = 0 |
| `_Game/Scripts/Cheat/LevelSetCheatDebugOptions.cs` | `AvailableSets` → `AvailableNames` |
| `Scenes/Launcher.unity` › `level_set_host` | field đổi tên `defaultSet` → `defaultVariant`, giá trị cũ rơi: đặt **Default Variant** = `LevelSet_Base` qua MCP, lưu, soát `git status` chỉ còn scene đó |
| PlayerPrefs | cheat lưu ở khoá `level_set_cheat` dạng chuỗi thô; bộ QA chọn trước khi chuyển (`KPrefs` `level_set_override`, JSON) bị quên một lần, khoá cũ còn đó không ai đọc |
| `docs/Runtime/AssetVariant/AssetVariant.md` | tạo, mở `Code: Runtime/Abstractions/Composites/AssetVariant/`: luồng boot → chọn → nạp → lùi · bảng nguồn và thứ tự · bảng API (`Current`, `CurrentSource`, `AvailableNames`, `SelectAsync`, `ClearOverrideAsync`, bốn member lớp con) · Trước khi chạy: lớp con kèm `[Service]` và `*Initializer` nếu cần, component trên object con của `BootstrapService` nằm trong **Steps**, **Default Variant**, group Addressables theo bảng luật của `docs/Editor/AssetVariantCheck.md` · Lưu ý AB test: build cũ thiếu bản mới rơi về mặc định mà vẫn tính vào nhóm thử (lọc experiment theo phiên bản app) · fetch phiên đầu trễ thì phiên đó chạy mặc định · `FetchAsync()` không tham số giữ cache 12 giờ, kiểm nhanh bằng cheat hoặc xoá dữ liệu app |
| `docs/Runtime/AssetVariant/AssetVariant.html` | tạo theo `docs` › `.html`: Kịch bản (bảng trên) · Cách dùng · Flow · Lưu ý (ba ý AB test, tên lạ rơi về mặc định kèm `LogError`, bản đứng yên cả phiên) |
| `docs/Editor/AssetVariantCheck.md` | dòng cuối trỏ `docs/Runtime/AssetVariant/AssetVariant.md` |
| `Agent/Rules/async-presentation/reference.md` › Stack async | dòng Addressables: "không string key" thêm vế — tên đến từ ngoài build (remote, save) đi qua `AssetVariant` |
| `Agent/Memory/async-presentation.md`, `Agent/Memory/inbox.md` | xoá khối "Key Addressables là chuỗi đến từ remote hay save" (đã thành host và phép kiểm) và khối "Nhóm thử của AB test lẫn người chơi đang chạy bản mặc định" (đã vào Lưu ý của tài liệu hệ) |
| `Editor/Levels/LevelDatabase.md` | dòng `Code:` trỏ host Horcrux · bảng nguồn: hàng Cheat `PlayerPrefs` `level_set_cheat` · bỏ gạch "Mở rộng sau" của bản tạm · Trước khi chạy #2: **Default Variant** · Kiểm thử: bỏ `LevelSetResolverTests`, thứ tự nguồn ở `AssetVariantPickerTests` của Horcrux |
| `Structure.md` › `Level/` | `LevelSetHost` là lớp con của `AAssetVariantHost` (Horcrux); bỏ `LevelSetResolver` |
| Plan này | xoá cùng `.meta` |
| chơi thử | Editor, remote mặc định: vào level 1, cheat **Cheat Level Set** hàng đầu `LevelSet_Base · Default` hoặc `· Remote` · bấm một bộ khác → hàng đầu đổi, `Override`; thoát Play, vào lại → vẫn bộ đó · **Theo remote** → về bộ cũ · Level Editor chọn bộ rồi Play test → `EditorHandoff` · đặt Value của `level_set` thành `Typo` → một `LogError` có `Typo`, chơi bộ mặc định. Hỏng: hàng đầu không đổi sau khi bấm, bộ mất sau khi vào lại, level đang chơi mất hình sau khi đổi bộ |

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | dự án tự dựng danh sách ứng viên, hoặc type parameter cho nguồn | thứ tự ưu tiên là cơ chế; để dự án dựng thì mỗi dự án một thứ tự |
| bỏ | base nhận `RemoteConfig<string>` | tên có thể đến từ nguồn khác remote config (server riêng, save) |
| bỏ | lưu tên cheat qua Persistence | entry Persistence là field cứng trong collection, mỗi host một field; ở đây chỉ cần một chuỗi theo khoá |
| bỏ | xoá tên cheat khi bị loại | QA chuyển giữa các build có tập bản khác nhau; `LogError` mỗi lần boot đủ báo |
| bỏ | nhả `_currentHandle` ở `OnDestroy` | host sống cả app; dọn lúc thoát app chạy sau khi Addressables đã tự dọn |
| bỏ | đổi bản khi remote fetch về trễ giữa phiên | cấu hình ngoài đổi gameplay đứng yên cả phiên |
| bỏ | kiểm group, label lúc chạy | lỗi authoring, bắt ở `AssetVariantCheck` |
| bỏ | tự tạo entry Addressables lúc chạy | setup thiếu bị giấu; tạo bằng `AddressablesLayout` lúc authoring |
| sau | panel cheat chung (hàng bản đang chạy, một nút mỗi bản, nút theo remote) ở `Vendors/SRDebugger` | khi Plan `DebugOptionsCategory` đã gõ; host không đổi |
| sau | remote load path, content update | khi có dự án nạp bản từ server; host không đổi |
| sau | event báo bản đổi giữa phiên | khi có consumer giữ cache theo bản; hiện consumer đọc `Current` mỗi lần |
