# Plan — Haptic System: rung theo preset lên Horcrux

Ngày 2026-10-29. Người dùng đầu tiên: category-jam — rung khi chạm tile và khi nắp box đóng. Thay plan cũ (rung theo
biên độ, backend JNI tự viết) đã bỏ: developer chốt chỉ dùng preset dựng sẵn của nền tảng, và `com.horcrux.runtime`
tham chiếu thẳng Nice Vibrations — không backend, không asmdef vendor.

**Phân công:**
- Task 1 (Horcrux runtime): developer gõ.
- Task 2 (nối dự án), Task 3 (test), Task 4 (tài liệu module): agent làm.

Horcrux là submodule: Task 1, 3, 4 commit **trong `Assets/Horcrux`**; Task 2 commit ở repo dự án kèm con trỏ submodule.

---

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | category-jam: `Tile.PlayPressFeedback` và `Box.HandleLidClosed` · dự án sau cần rung theo khoảnh khắc gameplay |
| Mục tiêu | gameplay gọi `_haptic.Play(GameHaptic.TapTile)` như gọi SFX; preset chọn trong Inspector, đổi không cần compile; Setting tắt rung thì im |
| Nhịp | theo sự kiện — một lần chạm, một lần đóng nắp. Không hot path; đường phát không cấp phát (bảng tra dựng ở `Awake`) |
| Chỗ đặt | cơ chế: `com.horcrux.runtime` (`Abstractions/Foundations/Haptics`, `Implementations/Foundations/Haptics`), asmdef thêm tham chiếu `Lofelt.NiceVibrations` · từ vựng (`GameHaptic`) và lớp nối Setting: dự án |
| Ranh giới | **interface** (`IHapticService`, `IHapticSetting`) không lộ kiểu nào của Nice Vibrations; chỉ host và `HapticEntry` biết lib · dự án gọi qua interface, chỉ chạm `PresetType` trong Inspector |
| Phụ thuộc | Nice Vibrations v4 thành phụ thuộc bắt buộc của Horcrux, cùng hàng PrimeTween, UniTask — developer chốt 2026-10-29 |
| Cảm giác | hướng chill: chạm = `LightImpact`, đóng nắp = `SoftImpact` (cùng độ mạnh, dài gấp 4). Rung lúc **chạm xuống**, cùng nhịp SFX `TapTile` và cú nhún · tap bị từ chối **không** rung thêm · đóng nắp **một** kiểu, không đổi theo combo — developer chốt 2026-10-29 |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| `IHapticBackend` + asmdef `Vendors/NiceVibrations` + define | developer chốt: một lib, một implementation, không có kế hoạch đổi lib. Lớp gián tiếp chỉ để "phòng khi đổi lib" là dư |
| Enum preset riêng của Horcrux (`HapticPreset`) | lý do duy nhất của nó là giữ asset độc lập với lib; runtime đã tham chiếu lib thì đó là một enum chép lại cộng một `switch` 9 nhánh để dịch. Entry lưu thẳng `HapticPatterns.PresetType` |
| Rung theo biên độ / thời lượng tự chỉnh (`PlayEmphasis`, `PlayConstant`, clip `.haptic`) | developer chốt chỉ dùng preset. Thêm sau là method mới trên `IHapticService`, không đổi `Play` |
| Throttle theo entry (`minInterval` như Audio) | một ngón tay không chạm đủ dày để cần; nắp đóng dồn (Magnet) cách nhau theo trình diễn. Cần thì thêm field vào `HapticEntry`, không đổi chữ ký |
| Catalog `ScriptableObject` như Audio | Audio cần catalog vì clip là asset lớn dùng chung; ở đây mỗi entry là hai enum, mảng trên host là đủ — bớt một class con và một asset |
| `IsSupported` | lib tự bỏ qua thiết bị không hỗ trợ; chưa caller nào cần hỏi |
| Chuyển rung nút UI (`KButton` → `VibrationController` của Kelsey) sang hệ này | ngoài phạm vi; ghi ở "Mở rộng sau" |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| Dự án có **hai** cổng rung: `HapticController.hapticsEnabled` (lib, Kelsey ghi cho nút UI) và `IHapticSetting.IsHapticEnabled` (gameplay) | Horcrux tham chiếu được lib nhưng vẫn không ghi vào trạng thái static mà Kelsey cũng ghi (hai cửa ghi một trạng thái). Hai cổng đọc **cùng một** save qua hai bridge nên luôn khớp. Gộp khi nút UI chuyển sang `IHapticService` |
| Rung gọi **ngay sau dòng SFX** trong `Tile`/`Box`, không qua event cho Bridge nghe | tiếng và rung là hai phản hồi của một khoảnh khắc — suy từ một chỗ. `TilePressed` bắn cả khi `HasAllowLevelInteraction` tắt, còn `PlayPressFeedback` thì không; nghe event là có lúc rung mà không có tiếng |
| `HapticController.Init()` gọi trong `Awake` của host | lib nạp lười ở lần phát đầu ⇒ cú rung đầu khựng. `Awake` chạy trên main thread lúc scene Service nạp — lib cấm gọi từ static constructor hay luồng khác |
| Entry chưa chọn preset phát `Selection` | `PresetType.Selection = 0` nên ô mới trong Inspector mặc định là `Selection`, không có giá trị "chưa gán" để bắt. Giá trị hiện rõ trong Inspector; đổi lại thì phải giữ enum riêng như trên |

**Mở rộng sau** (đều là thêm, không sửa cũ)
- Nút UI (`KButton`) gọi `IHapticService` thay `IVibrationService` của Kelsey ⇒ bỏ cổng thứ hai.
- Throttle theo entry · rung theo biên độ · preset đổi theo bậc (combo) bằng nhiều id, như Audio mục "một tiếng nhiều bậc".

---

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| Kelsey `IVibrationService` + `VibrationController` (dự án) | lib Nice Vibrations v4 đã cài, đã chạy cho nút UI | Kelsey là di sản, `CategoryJam.Gameplay.asmdef` không tham chiếu; tên hàm theo cường độ (`PlayVibrateHeavy`) chứ không theo khoảnh khắc; namespace dự án cũ |
| Horcrux Audio (`IAudioService<TSfx,TMusic>`, `AudioService`, `GameplayAudioToggle`) | khuôn: enum của dự án làm id · host generic abstract kế thừa `MonoBehaviour` thường + class con rỗng mang `[Service]` · `IAudioSetting` không generic cho bridge Setting · `LogError` khi id thiếu · trùng id nổ ở `Awake` | catalog SO · throttle · voice (lý do ở bảng "Cố ý KHÔNG làm") |
| Nice Vibrations v4 `HapticPatterns.PresetType` | 9 preset · `PlayPreset` · `HapticController.Init()` (gọi nhiều lần an toàn) | `PlayEmphasis` / `PlayConstant` / `HapticClip` (developer chốt chỉ preset) · cổng `hapticsEnabled` (Kelsey đang giữ) |
| Plan cũ (biên độ, `AndroidJavaObject`, `HapticRampChannel`) | — | rung theo biên độ, backend JNI tự viết, lớp `IHapticBackend` — developer bỏ cả ba |

**Bảng preset** (đọc từ `HapticPatterns.cs`, để chọn trong Inspector):

| Preset | Android có chỉnh biên độ | Android không chỉnh biên độ | iOS |
|---|---|---|---|
| `LightImpact` | 0.156 × 40 ms | 40 ms mức tối đa | Impact light |
| `SoftImpact` | 0.156 × 160 ms | 160 ms mức tối đa | Impact soft |
| `Selection` | 0.471 × 40 ms | 40 ms mức tối đa | Selection |
| `MediumImpact` | 0.471 × 80 ms | 80 ms mức tối đa | Impact medium |
| `RigidImpact` | 1.0 × 40 ms | 40 ms mức tối đa | Impact rigid |
| `HeavyImpact` | 1.0 × 160 ms | 160 ms mức tối đa | Impact heavy |

Máy Android không chỉnh biên độ phát **mọi** preset ở mức tối đa trong đúng thời lượng của nó: preset dài (`Soft`, `Heavy`) là
preset mạnh nhất trên máy đó.

---

## Task 1 — Horcrux runtime (developer gõ)

Bốn file và một dòng asmdef, không ai dùng ⇒ compile sạch ngay. Namespace theo Audio.

### Files

| File | Vai trò |
|---|---|
| `Runtime/com.horcrux.runtime.asmdef` | thêm `"Lofelt.NiceVibrations"` vào `references` |
| `Runtime/Abstractions/Foundations/Haptics/IHapticSetting.cs` | cổng bật/tắt, không generic — bridge Setting không phải gọi tên enum của dự án |
| `Runtime/Abstractions/Foundations/Haptics/IHapticService.cs` | thứ gameplay gọi |
| `Runtime/Implementations/Foundations/Haptics/HapticEntry.cs` | một dòng id → preset |
| `Runtime/Implementations/Foundations/Haptics/HapticService.cs` | host: nạp lib, bảng tra, cổng, phát |

### Code

```json
    "references": [
        "InitArgs",
        "InitArgs.Services",
        "Unity.Addressables",
        "Unity.ResourceManager",
        "UniTask",
        "UniTask.Addressables",
        "Unity.Mathematics",
        "UnityEngine.UI",
        "Unity.TextMeshPro",
        "PrimeTween.Runtime",
        "Lofelt.NiceVibrations"
    ],
```

```csharp
namespace Horcrux.Runtime.Abstractions.Haptics
{
    public interface IHapticSetting
    {
        bool IsHapticEnabled { get; set; }
    }
}
```

```csharp
using System;

namespace Horcrux.Runtime.Abstractions.Haptics
{
    /// <summary>
    /// Rung theo id enum của dự án; mỗi id gắn một preset Nice Vibrations trong Inspector của host.
    /// Enum bắt đầu từ 1, kiểu nền <c>int</c>.
    /// </summary>
    public interface IHapticService<THaptic> : IHapticSetting, IService<IHapticService<THaptic>>
        where THaptic : struct, Enum
    {
        void Play(THaptic haptic);
    }
}
```

```csharp
using System;
using Lofelt.NiceVibrations;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Haptics
{
    [Serializable]
    public class HapticEntry<THaptic> where THaptic : struct, Enum
    {
        [SerializeField] private THaptic id;
        [SerializeField] private HapticPatterns.PresetType preset;

        public THaptic Id => id;
        public HapticPatterns.PresetType Preset => preset;
    }
}
```

```csharp
using System;
using System.Collections.Generic;
using Horcrux.Runtime.Abstractions.Haptics;
using Horcrux.Runtime.Utilities;
using Lofelt.NiceVibrations;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Haptics
{
    /// <summary>Host rung: tra id → preset trong bảng kéo tay, chặn theo <see cref="IsHapticEnabled"/>, phát qua Nice Vibrations.</summary>
    public abstract class HapticService<THaptic> : MonoBehaviour, IHapticService<THaptic>
        where THaptic : struct, Enum
    {
        #region Properties

        [Splitter("Configs")]
        [SerializeField] private HapticEntry<THaptic>[] entries = Array.Empty<HapticEntry<THaptic>>();

        private readonly Dictionary<THaptic, HapticPatterns.PresetType> _presetsById = new();

        public bool IsHapticEnabled { get; set; }

        #endregion

        #region Unity Callbacks

        private void Awake()
        {
            // Lib nạp lười ở lần phát đầu; nạp lúc boot để cú rung đầu không khựng.
            HapticController.Init();
            BuildTables();
        }

        #endregion

        #region API

        public void Play(THaptic haptic)
        {
            if (!IsHapticEnabled)
                return;

            if (!_presetsById.TryGetValue(haptic, out HapticPatterns.PresetType preset))
            {
                Debug.LogError($"[HapticService]: Haptic {haptic} is not in the entries.", this);
                return;
            }

            HapticPatterns.PlayPreset(preset);
        }

        #endregion

        #region Class Methods

        private void BuildTables()
        {
            _presetsById.Clear();
            for (int i = 0; i < entries.Length; i++)
                _presetsById.Add(entries[i].Id, entries[i].Preset);
        }

        #endregion
    }
}
```

### Lý do

| Quyết định | Vì |
|---|---|
| Kế thừa `MonoBehaviour` thường, `Awake` | host không cần đối số tiêm — như `AudioService`. Không có bước `Init` của InitArgs để che |
| `_presetsById.Add`, không `TryAdd` | trùng id nổ ở `Awake` thay vì để entry đầu thắng im lặng |
| Id thiếu: `LogError` rồi thoát | sai lúc setup nhưng không nổ — thiếu một entry là thiếu một kiểu rung cả phiên mà không ai biết |
| `IsHapticEnabled` là auto-property mặc định `false` | không rung khi chưa biết Setting của người chơi — rung là thứ người ta tắt đi, rung nhầm một cú lúc boot tệ hơn im. Developer chốt 2026-10-29. Hệ quả: bridge Setting là **bắt buộc**, thiếu là im cả phiên không log. Bridge có thể ghi trước `Awake` của host; `Awake` không chạm cổng |
| `PlayPreset` không bọc thêm gì | lib tự kiểm `hapticsEnabled`, `PresetType.None` và thiết bị không hỗ trợ |

**Commit:** `feat(haptics): add preset haptic service` trong `Assets/Horcrux`. Compile sạch là đủ.

---

## Task 2 — Nối dự án category-jam (agent)

Bắt đầu sau khi Task 1 compile sạch. Agent đọc code thật của Task 1 trước khi viết; tên dưới đây theo Plan, lệch thì theo code.

Hai file developer đã tạo sẵn: `Gameplay/Definition/GameHaptic.cs` (giữ nguyên) và `Gameplay/Feedback/GameHapticService.cs`
(sửa ở 2.2). Thư mục `Gameplay/Audio` đã đổi thành `Gameplay/Feedback` (cùng `GameAudioService`, `GameAudioCatalog`).

### Files

| # | File | Đổi |
|---|---|---|
| 2.1 | `Assets/_CategoryJam/Gameplay/Definition/GameHaptic.cs` | đã có: `enum GameHaptic { TapTile = 1, BoxLidClosed = 2 }` — không đổi |
| 2.2 | `Assets/_CategoryJam/Gameplay/Feedback/GameHapticService.cs` | đã có: thân rỗng, sửa `using` |
| 2.3 | `Assets/_CategoryJam/Bridge/GameplayHapticToggle.cs` (mới) | bridge Setting › Vibration → `IHapticSetting` |
| 2.4 | `Assets/_CategoryJam/Gameplay/Entities/Tile/Tile.cs` | nhận thêm `IHapticService<GameHaptic>`, rung trong `PlayPressFeedback` |
| 2.5 | `Assets/_CategoryJam/Gameplay/Entities/Tile/TileInitializer.cs` | hai đối số |
| 2.6 | `Assets/_CategoryJam/Gameplay/Entities/Box/Box.cs` | nhận thêm `IHapticService<GameHaptic>`, rung trong `HandleLidClosed` |
| 2.7 | `Assets/_CategoryJam/Gameplay/Entities/Box/CategoryBoxInitializer.cs` | hai đối số |
| 2.8 | `Assets/_CategoryJam/Bridge/Bridge.md` | thêm dòng `GameplayHapticToggle` vào bảng file |

Không ai khác gọi `Tile.Init` / `Box.Init` bằng tay (grep test và Editor tool ngày 2026-10-29: 0 chỗ) — chỉ InitArgs tiêm qua Initializer.

### 2.2 `GameHapticService.cs`

```csharp
using CategoryJam.Gameplay.Definition;
using Horcrux.Runtime.Abstractions.Haptics;
using Horcrux.Runtime.Implementations.Haptics;
using Sisus.Init;

namespace CategoryJam.Gameplay.Feedback
{
    [Service(typeof(IHapticService<GameHaptic>), typeof(IHapticSetting), FindFromScene = true)]
    public class GameHapticService : HapticService<GameHaptic>
    {
    }
}
```

Thân rỗng như `GameAudioService`: cổng `IsHapticEnabled` nằm ở base, mặc định `false` — chỉ bridge 2.3 bật nó.

### 2.3 `GameplayHapticToggle.cs` (mới)

Chép khuôn `GameplayAudioToggle`, một cờ thay vì hai.

```csharp
using Horcrux.Runtime.Abstractions.Haptics;
using Kelsey;
using Sisus.Init;

namespace CategoryJam.Bridge
{
    /// <summary>
    /// Cầu nối Setting: đưa Vibration từ save vào cổng rung của gameplay, và giữ nó khớp khi người chơi đổi.
    /// </summary>
    public class GameplayHapticToggle : MonoBehaviour<IVibrationPersistentData, IHapticSetting>
    {
        private IVibrationPersistentData _persistentData;
        private IHapticSetting _hapticSetting;

        protected override void Init(IVibrationPersistentData persistentData, IHapticSetting hapticSetting)
        {
            _persistentData = persistentData;
            _hapticSetting = hapticSetting;

            _hapticSetting.IsHapticEnabled = _persistentData.Vibration;

            _persistentData.RegisterOnVibrationChange(OnVibrationChanged);
        }

        private void OnDestroy() => _persistentData.UnregisterOnVibrationChange(OnVibrationChanged);

        private void OnVibrationChanged(bool isOn) => _hapticSetting.IsHapticEnabled = isOn;
    }
}
```

| Chỗ | Vì |
|---|---|
| Ở `Bridge/`, không ở `Gameplay/` | `IVibrationPersistentData` là của Kelsey; `CategoryJam.Gameplay.asmdef` không tham chiếu Kelsey. `Bridge/` compile vào Assembly-CSharp, thấy cả hai |
| Không ghi `HapticController.hapticsEnabled` | cổng của lib do `VibrationController` (Kelsey) ghi cho nút UI; hai bridge đọc cùng `PrefsBool("vibration")` nên luôn khớp |
| `OnDestroy` không guard null | `Init` luôn chạy trước khi object sống; GameObject nằm trong `Service.unity`, sống cả phiên |

### 2.4 `Tile.cs`

Ba chỗ. `using` thêm:

```csharp
using Horcrux.Runtime.Abstractions.Haptics;
```

Khai báo class (dòng 27):

```csharp
    public partial class Tile : PooledEntity,
        IInitializable<IAudioService<GameSfx, GameMusic>, IHapticService<GameHaptic>>,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IAnimate
```

`PlayPressFeedback` — rung ngay sau dòng SFX, trước cú nhún:

```csharp
        public void PlayPressFeedback()
        {
            _audio.PlaySfx(GameSfx.TapTile);
            _haptic.Play(GameHaptic.TapTile);
            _view.PlayPress(press);
        }
```

Region DI cuối file:

```csharp
        #region DI

        private IAudioService<GameSfx, GameMusic> _audio;
        private IHapticService<GameHaptic> _haptic;

        public void Init(IAudioService<GameSfx, GameMusic> audio, IHapticService<GameHaptic> haptic)
        {
            _audio = audio;
            _haptic = haptic;
        }

        #endregion DI
```

`PlayRejectFeedback` **không** đổi — tap bị từ chối không rung thêm (developer chốt).

### 2.5 `TileInitializer.cs`

```csharp
using Sisus.Init;
using Horcrux.Runtime.Abstractions.Audio;
using Horcrux.Runtime.Abstractions.Haptics;
using CategoryJam.Gameplay.Definition;

namespace CategoryJam.Gameplay.Entities
{
	/// <summary>
	/// Initializer for the <see cref="Tile"/> component.
	/// </summary>
	internal sealed class TileInitializer : Initializer<Tile, IAudioService<GameSfx, GameMusic>, IHapticService<GameHaptic>>
	{
		#if UNITY_EDITOR
		/// <summary>
		/// This section can be used to customize how the Init arguments will be drawn in the Inspector.
		/// <para>
		/// The Init argument names shown in the Inspector will match the names of members defined inside this section.
		/// </para>
		/// <para>
		/// Any PropertyAttributes attached to these members will also affect the Init arguments in the Inspector.
		/// </para>
		/// </summary>
		private sealed class Init
		{
			public IAudioService<GameSfx, GameMusic> audio = default;
			public IHapticService<GameHaptic> haptic = default;
		}
		#endif
	}
}
```

### 2.6 `Box.cs`

`using` thêm `Horcrux.Runtime.Abstractions.Haptics;`. Khai báo class (dòng 13):

```csharp
    public class Box : PooledEntity, IAnimate,
        IInitializable<IAudioService<GameSfx, GameMusic>, IHapticService<GameHaptic>>
```

`HandleLidClosed` — rung ngay sau dòng SFX đóng hộp, trước `OnLidClosed` và event:

```csharp
        private void HandleLidClosed()
        {
            _audio.PlaySfx(GameSfx.BoxComplete);
            _haptic.Play(GameHaptic.BoxLidClosed);
            OnLidClosed();
            EventBus<BoxLidClosed>.Publish(new BoxLidClosed(this));
            ...
```

Region DI: như `Tile` (2.4) — thêm field `_haptic`, `Init` hai đối số.

Không rung ở nhánh combo (`PlaySfx(ComboSfxs[...])`) hay `BoxReceive` — đóng nắp **một** kiểu, combo đã có âm leo thang.

### 2.7 `CategoryBoxInitializer.cs`

Như 2.5, client là `CategoryBox`. Tên ô cũ `argument` đổi thành `audio` cho khớp `TileInitializer`:

```csharp
	internal sealed class CategoryBoxInitializer : Initializer<CategoryBox, IAudioService<GameSfx, GameMusic>, IHapticService<GameHaptic>>
	{
		#if UNITY_EDITOR
		...
		private sealed class Init
		{
			public IAudioService<GameSfx, GameMusic> audio = default;
			public IHapticService<GameHaptic> haptic = default;
		}
		#endif
	}
```

**Dữ liệu prefab.** `Initializer<,>` serialize ô `argument`; `Initializer<,,>` serialize `firstArgument` và `secondArgument`. Giá trị
cũ rơi mất khi đổi kiểu — vô hại: cả ba prefab đang để `argument` trống (`reference: {fileID: 0}`, `rid: -2`), tức nhận từ
service, và ô mới trống cũng nhận từ service. Unity giữ khối `argument` mồ côi trong YAML tới lần lưu prefab kế tiếp.

### 2.8 `Bridge.md`

Thêm một dòng ngay dưới `GameplayAudioToggle` trong bảng file:

```markdown
| `GameplayHapticToggle` | đồng bộ `IHapticSetting` của Horcrux với `IVibrationPersistentData` của Kelsey |
```

### Editor setup (developer)

1. Mở `Assets/_CategoryJam/Scenes/Service.unity`. Tạo GameObject `haptic` cạnh GameObject chứa `GameAudioService`.
   *Thiếu:* không ai resolve được `IHapticService<GameHaptic>` — `Tile`/`Box` báo thiếu đối số lúc spawn.
2. Gắn `GameHapticService` và `GameplayHapticToggle` lên `haptic`.
   *Thiếu toggle:* rung luôn bật, Setting tắt rung không có tác dụng với gameplay.
3. Ô **Entries** của `GameHapticService`: thêm 2 dòng — `TapTile` → `LightImpact`, `BoxLidClosed` → `SoftImpact`.
   *Thiếu dòng:* console `[HapticService]: Haptic … is not in the entries.` ở lần chạm hay đóng nắp đầu.
   *Quên đổi preset:* dòng mới mặc định `Selection` — rung vẫn chạy nhưng sai kiểu.
4. Mở `Tile.prefab`, `GoldenTile.prefab` (`Addressables/Tile/`) và `CategoryBox.prefab` (`Addressables/Box/`): khối Init
   của Initializer hiện hai ô `audio` và `haptic`, cả hai ghi "Service". Lưu prefab (xoá khối `argument` mồ côi).
   *Ô `haptic` không hiện Service:* `GameHapticService` thiếu `[Service]` hoặc sai kiểu trong `typeof(...)`.

### Kiểm

| Kiểm | Cách | Đạt khi |
|---|---|---|
| Compile | MSBuild của Rider trên `CategoryJam.Gameplay.csproj` và `Assembly-CSharp.csproj` | 0 lỗi, 0 cảnh báo mới |
| `CategoryJam.Gameplay.asmdef` không cần tham chiếu Nice Vibrations | cùng lần compile trên | không có CS0012 nhắc `Lofelt.NiceVibrations` — `PresetType` chỉ nằm trong field private của host. Có thì thêm tham chiếu vào asmdef, ghi lý do vào Plan |
| Mọi chỗ gọi đã đủ đối số | grep `IInitializable<IAudioService<GameSfx, GameMusic>>` trong `Tile.cs`, `Box.cs` | 0 kết quả |
| Rung cạnh SFX | grep `_haptic.Play` | đúng 2 chỗ, mỗi chỗ ngay dòng dưới `PlaySfx(GameSfx.TapTile)` / `PlaySfx(GameSfx.BoxComplete)` |
| Đồ thị | `graphify update .` | chạy xong |
| Chơi | kịch bản chơi thử cuối Plan, developer trên máy thật | — |

**Commit:** `feat(gameplay): haptic khi chạm tile và đóng nắp box` ở repo dự án, kèm con trỏ `Assets/Horcrux`. Commit
bằng pathspec — developer đang có thay đổi stage sẵn (đổi `Audio` → `Feedback`); hỏi trước khi gộp chúng vào commit này.

---

## Task 3 — Test (agent)

PlayMode trong `Horcrux.PlayModeTests`, enum test riêng. Không có lớp gián tiếp nên test **không đếm được** lời phát —
chỉ kiểm cổng và bảng tra qua log; cú rung thật kiểm ở kịch bản chơi thử. Trong Editor `PlayPreset` không rung, không ném.

| Case | Dựng | Kỳ vọng |
|---|---|---|
| Phát id có entry | cổng bật, entries `A → LightImpact`; `Play(A)` | không ném, không log |
| Id thiếu | cổng bật, entries `A`; `Play(B)` | `LogError` chứa `[HapticService]` |
| Cổng tắt chặn trước bảng tra | entries rỗng, cổng tắt; `Play(A)` | không log |
| Bật lại | cổng tắt → `Play(A)` → bật → `Play(A)` | lần đầu không log, lần sau `LogError` |
| Ghi cổng trước `Awake` | cổng bật **trước** khi host `Awake`, entries rỗng; `Play(A)` | `LogError` — `Awake` không ghi đè cổng |
| Trùng id | entries `A → Light`, `A → Soft` | `ArgumentException` lúc `Awake` |

Kết quả 2026-10-29: 6/6 pass (PlayMode, Editor 6000.0.73f1).

**Commit:** `test(haptics): HapticService` trong `Assets/Horcrux`.

---

## Task 4 — Tài liệu module (agent)

`Runtime/Abstractions/Foundations/Haptics/HapticSystem.md` (cạnh code thật) viết từ code thật, theo khuôn `AudioSystem.md`: hai phía SDK –
dự án · luồng một lần rung · cách dùng (khai enum, class con, host, nối Setting) · **Trước khi chạy** (Nice Vibrations đã cài,
host, entries) · bảng preset · bẫy và quyết định · chữ ký · cấu trúc file. `.html` đồng bộ khi developer yêu cầu.

---

## Kịch bản chơi thử (developer, máy thật)

Cần: một Android có chỉnh biên độ (đời mới), **một Android rẻ**, iOS nếu có. Setting › Vibration bật.

| Làm | Nhìn / cảm | Dấu hiệu hỏng |
|---|---|---|
| Vào level 1, chạm một tile rồi nhả | một cú rất nhẹ, ngắn, **đúng lúc ngón chạm** — cùng tiếng tap và cú nhún; nhả tay tile bay, không thêm cú nào | rung lúc nhả · rung lệch tiếng rõ · cú đầu tiên trễ hơn các cú sau |
| Làm đầy cache, chạm thêm một tile | chạm có cú nhẹ; nhả: tile lắc, tiếng đục, **không** rung thêm | có cú thứ hai lúc nhả |
| Hoàn thành một box | lúc nắp chạm thân (cùng tiếng đóng hộp): một cú "thụp" mềm, dài hơn chạm, không gắt | không rung · rung lúc tile cuối vào, trước nắp |
| Dùng Magnet đóng nhiều box liền | các cú tách nhau theo từng nắp, không thành một vệt rung dài khó chịu | rung dính thành một hồi dài |
| Bật AutoPlay (nếu có cheat) | không rung khi bot chọn tile; nắp đóng vẫn rung | rung theo mỗi lần bot chọn |
| Setting tắt Vibration | chạm và đóng nắp đều im; nút UI cũng im | còn rung ở một trong hai |
| Tắt rung, kill app, mở lại | vẫn im | rung lại sau khi mở |
| Trên Android rẻ: hoàn thành box | cú đóng nắp chấp nhận được | gắt hoặc dài ⇒ đổi `BoxLidClosed` sang `MediumImpact` trong Inspector |
