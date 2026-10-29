# Plan — Haptic System: rung theo preset lên Horcrux

Ngày 2026-10-29. Người dùng đầu tiên: category-jam — rung khi chạm tile và khi nắp box đóng. Thay plan cũ (rung theo
biên độ, backend JNI tự viết) đã bỏ: developer chốt chỉ dùng preset dựng sẵn của nền tảng.

**Phân công:**
- Task 1 (Horcrux runtime), Task 2 (backend Nice Vibrations): developer gõ.
- Task 3 (nối dự án), Task 4 (test), Task 5 (tài liệu module): agent làm.

Horcrux là submodule: Task 1, 2, 4, 5 commit **trong `Assets/Horcrux`**; Task 3 commit ở repo dự án kèm con trỏ submodule.

---

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | category-jam: `Tile.PlayPressFeedback` và `Box.HandleLidClosed` · dự án sau cần rung theo khoảnh khắc gameplay |
| Mục tiêu | gameplay gọi `_haptic.Play(GameHaptic.TapTile)` như gọi SFX; preset chọn trong Inspector, đổi không cần compile; Setting tắt rung thì im |
| Nhịp | theo sự kiện — một lần chạm, một lần đóng nắp. Không hot path; đường phát không cấp phát (bảng tra dựng ở `Awake`) |
| Chỗ đặt | cơ chế: `com.horcrux.runtime` (`Abstractions/Foundations/Haptics`, `Implementations/Foundations/Haptics`) · gọi lib: `Vendors/NiceVibrations`, asmdef riêng có define · từ vựng (`GameHaptic`) và lớp nối Setting: dự án |
| Ranh giới | runtime **không biết** Nice Vibrations; backend **không biết** cổng bật/tắt hay id của dự án; dự án **không biết** Nice Vibrations |
| Cảm giác | hướng chill: chạm = `LightImpact`, đóng nắp = `SoftImpact` (cùng độ mạnh, dài gấp 4). Rung lúc **chạm xuống**, cùng nhịp SFX `TapTile` và cú nhún · tap bị từ chối **không** rung thêm · đóng nắp **một** kiểu, không đổi theo combo — developer chốt 2026-10-29 |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Rung theo biên độ / thời lượng tự chỉnh (`PlayEmphasis`, `PlayConstant`, clip `.haptic`) | developer chốt chỉ dùng preset. Thêm sau là method mới trên `IHapticBackend` và `IHapticService`, không đổi `Play` |
| Throttle theo entry (`minInterval` như Audio) | một ngón tay không chạm đủ dày để cần; nắp đóng dồn (Magnet) cách nhau theo trình diễn. Cần thì thêm field vào `HapticEntry`, không đổi chữ ký |
| Catalog `ScriptableObject` như Audio | Audio cần catalog vì clip là asset lớn dùng chung; ở đây mỗi entry là hai enum, mảng trên host là đủ — bớt một class con và một asset |
| `IsSupported` trên backend | lib tự bỏ qua thiết bị không hỗ trợ; chưa caller nào cần hỏi |
| Chuyển rung nút UI (`KButton` → `VibrationController` của Kelsey) sang hệ này | ngoài phạm vi; ghi ở "Mở rộng sau" |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| Có `IHapticBackend` dù chỉ một implementation | `com.horcrux.runtime` không được tham chiếu thư viện vendor — dự án khác chưa chắc có Nice Vibrations. Cùng đường với `IRemoteConfigProvider` / `Vendors/Firebase`. Lợi kèm: test Horcrux chạy với backend giả |
| `HapticPreset` là enum của Horcrux, không dùng `HapticPatterns.PresetType` | dữ liệu preset trong Inspector không phụ thuộc lib; đổi lib là viết lại một file backend, asset không đổi |
| Dự án có **hai** cổng rung: `HapticController.hapticsEnabled` (lib, Kelsey ghi cho nút UI) và `IHapticSetting.IsOn` (gameplay) | Horcrux không được ghi vào trạng thái static mà Kelsey cũng ghi (hai cửa ghi một trạng thái). Hai cổng đọc **cùng một** save qua hai bridge nên luôn khớp. Gộp khi nút UI chuyển sang `IHapticService` |
| Rung gọi **ngay sau dòng SFX** trong `Tile`/`Box`, không qua event cho Bridge nghe | tiếng và rung là hai phản hồi của một khoảnh khắc — suy từ một chỗ. `TilePressed` bắn cả khi `HasAllowLevelInteraction` tắt, còn `PlayPressFeedback` thì không; nghe event là có lúc rung mà không có tiếng |
| Backend ném ở nhánh `_` của `switch` | `0` là ô Inspector chưa gán preset: lỗi authoring, nổ ở lần rung đầu thay vì im lặng cả phiên |
| `HapticController.Init()` gọi trong constructor backend | lib nạp lười ở lần phát đầu ⇒ cú rung đầu khựng. Lib cấm gọi từ **static** constructor (khác luồng); constructor instance do InitArgs gọi lúc boot. *Giả định (cần xác nhận): InitArgs dựng service `[Service]` trên main thread* — kiểm ở Task 3 |

**Mở rộng sau** (đều là thêm, không sửa cũ)
- Nút UI (`KButton`) gọi `IHapticService` thay `IVibrationService` của Kelsey ⇒ bỏ cổng thứ hai.
- Throttle theo entry · rung theo biên độ · preset đổi theo bậc (combo) bằng nhiều id, như Audio mục "một tiếng nhiều bậc".

---

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| Kelsey `IVibrationService` + `VibrationController` (dự án) | lib Nice Vibrations v4 đã cài, đã chạy cho nút UI | Kelsey là di sản, `CategoryJam.Gameplay.asmdef` không tham chiếu; tên hàm theo cường độ (`PlayVibrateHeavy`) chứ không theo khoảnh khắc; namespace dự án cũ |
| Horcrux Audio (`IAudioService<TSfx,TMusic>`, `AudioService`, `GameplayAudioToggle`) | khuôn: enum của dự án làm id · host generic abstract + class con rỗng mang `[Service]` · `IAudioSetting` không generic cho bridge Setting · `LogError` khi id thiếu · trùng id nổ ở `Awake` | catalog SO · throttle · voice (lý do ở bảng "Cố ý KHÔNG làm") |
| Horcrux `Vendors/Firebase` | interface ở runtime, bản cài đặt trong asmdef riêng có `defineConstraints`, đăng ký bằng `[Service(typeof(I…))]` | `versionDefines` — Nice Vibrations nằm trong `Assets/`, không phải gói UPM, nên không tự sinh define |
| Nice Vibrations v4 `HapticPatterns.PresetType` | 9 preset · `PlayPreset` · `HapticController.Init()` (gọi nhiều lần an toàn) | `PlayEmphasis` / `PlayConstant` / `HapticClip` (developer chốt chỉ preset) · cổng `hapticsEnabled` (Kelsey đang giữ) |
| Define `MOREMOUNTAINS_NICEVIBRATIONS_INSTALLED` | — | do script Feedbacks thêm cho build target **đang chọn**, không phải hợp đồng của lib; define riêng của Horcrux nói rõ ý định bật vendor |
| Plan cũ (biên độ, `AndroidJavaObject`, `HapticRampChannel`) | ý tách lib sau `IHapticBackend` | rung theo biên độ và backend JNI tự viết — developer bỏ hướng đó |

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

Sáu file, không ai dùng ⇒ compile sạch ngay. Namespace theo Audio.

### Files

| File | Vai trò |
|---|---|
| `Runtime/Abstractions/Foundations/Haptics/HapticPreset.cs` | từ vựng preset của Horcrux |
| `Runtime/Abstractions/Foundations/Haptics/IHapticSetting.cs` | cổng bật/tắt, không generic — bridge Setting không phải gọi tên enum của dự án |
| `Runtime/Abstractions/Foundations/Haptics/IHapticService.cs` | thứ gameplay gọi |
| `Runtime/Abstractions/Foundations/Haptics/IHapticBackend.cs` | cầu sang lib |
| `Runtime/Implementations/Foundations/Haptics/HapticEntry.cs` | một dòng id → preset |
| `Runtime/Implementations/Foundations/Haptics/HapticService.cs` | host: bảng tra, cổng, giao backend |

### Code

```csharp
namespace Horcrux.Runtime.Abstractions.Haptics
{
    /// <summary>Kiểu rung dựng sẵn của nền tảng. Số tường minh từ 1: <c>0</c> là ô Inspector chưa gán.</summary>
    public enum HapticPreset
    {
        Selection = 1,
        Success = 2,
        Warning = 3,
        Failure = 4,
        LightImpact = 5,
        MediumImpact = 6,
        HeavyImpact = 7,
        RigidImpact = 8,
        SoftImpact = 9,
    }
}
```

```csharp
namespace Horcrux.Runtime.Abstractions.Haptics
{
    public interface IHapticSetting
    {
        bool IsOn { get; set; }
    }
}
```

```csharp
using System;

namespace Horcrux.Runtime.Abstractions.Haptics
{
    /// <summary>
    /// Rung theo id enum của dự án; mỗi id gắn một <see cref="HapticPreset"/> trong Inspector của host.
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
namespace Horcrux.Runtime.Abstractions.Haptics
{
    /// <summary>Cầu sang thư viện rung. Thiết bị không hỗ trợ thì im lặng, không ném.</summary>
    public interface IHapticBackend
    {
        void Play(HapticPreset preset);
    }
}
```

```csharp
using System;
using Horcrux.Runtime.Abstractions.Haptics;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Haptics
{
    [Serializable]
    public class HapticEntry<THaptic> where THaptic : struct, Enum
    {
        [SerializeField] private THaptic id;
        [SerializeField] private HapticPreset preset;

        public THaptic Id => id;
        public HapticPreset Preset => preset;
    }
}
```

```csharp
using System;
using System.Collections.Generic;
using Horcrux.Runtime.Abstractions.Haptics;
using Horcrux.Runtime.Utilities;
using Sisus.Init;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Haptics
{
    /// <summary>Host rung: tra id → preset trong bảng kéo tay, chặn theo <see cref="IsOn"/>, giao backend phát.</summary>
    public abstract class HapticService<THaptic> : MonoBehaviour<IHapticBackend>, IHapticService<THaptic>
        where THaptic : struct, Enum
    {
        #region Properties

        [Splitter("Configs")]
        [SerializeField] private HapticEntry<THaptic>[] entries = Array.Empty<HapticEntry<THaptic>>();

        private readonly Dictionary<THaptic, HapticPreset> _presetById = new();

        public bool IsOn { get; set; } = true;

        #endregion

        #region Unity Callbacks

        protected override void OnAwake() => BuildTable();

        #endregion

        #region API

        public void Play(THaptic haptic)
        {
            if (!IsOn)
                return;

            if (!_presetById.TryGetValue(haptic, out HapticPreset preset))
            {
                Debug.LogError($"[HapticService]: Haptic {haptic} is not in the entries.", this);
                return;
            }

            _backend.Play(preset);
        }

        #endregion

        #region Class Methods

        private void BuildTable()
        {
            _presetById.Clear();
            for (int i = 0; i < entries.Length; i++)
                _presetById.Add(entries[i].Id, entries[i].Preset);
        }

        #endregion

        #region DI

        private IHapticBackend _backend;

        protected override void Init(IHapticBackend backend) => _backend = backend;

        #endregion
    }
}
```

### Lý do

| Quyết định | Vì |
|---|---|
| `MonoBehaviour<IHapticBackend>`, bảng tra dựng ở `OnAwake` | base của InitArgs gọi `Init` trước `OnAwake`; khai `Awake` riêng là che bước tiêm |
| `_presetById.Add`, không `TryAdd` | trùng id nổ ở `Awake` thay vì để entry đầu thắng im lặng |
| Id thiếu: `LogError` rồi thoát | sai lúc setup nhưng không nổ — thiếu một entry là thiếu một kiểu rung cả phiên mà không ai biết |
| Không guard `_backend == null` | thiếu backend (chưa bật define) thì InitArgs báo thiếu đối số ngay lúc host `Awake`, lần Play đầu |
| `IsOn` là auto-property mặc định `true` | bridge Setting có thể ghi trước `Awake` của host; `OnAwake` không chạm `IsOn` |

**Commit:** `feat(haptics): add preset haptic service` trong `Assets/Horcrux`. Compile sạch là đủ.

---

## Task 2 — Backend Nice Vibrations (developer gõ)

### Files

| File | Vai trò |
|---|---|
| `Vendors/NiceVibrations/com.horcrux.nicevibrations.asmdef` | chỉ biên dịch khi có define `HORCRUX_NICE_VIBRATIONS` |
| `Vendors/NiceVibrations/NiceVibrationsHapticBackend.cs` | `HapticPreset` → `HapticPatterns.PresetType` |

### Code

```json
{
    "name": "com.horcrux.nicevibrations",
    "rootNamespace": "",
    "references": [
        "com.horcrux.runtime",
        "InitArgs",
        "InitArgs.Services",
        "Lofelt.NiceVibrations"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [
        "HORCRUX_NICE_VIBRATIONS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

```csharp
using System;
using Horcrux.Runtime.Abstractions.Haptics;
using Lofelt.NiceVibrations;
using Sisus.Init;

namespace Horcrux.Vendors.NiceVibrations
{
    /// <summary>Phát <see cref="HapticPreset"/> qua Nice Vibrations v4. Thiết bị không hỗ trợ thì lib tự bỏ qua.</summary>
    [Service(typeof(IHapticBackend))]
    public class NiceVibrationsHapticBackend : IHapticBackend
    {
        // Lib nạp lười ở lần phát đầu; nạp lúc boot để cú rung đầu không khựng.
        public NiceVibrationsHapticBackend() => HapticController.Init();

        public void Play(HapticPreset preset) => HapticPatterns.PlayPreset(ToPresetType(preset));

        private static HapticPatterns.PresetType ToPresetType(HapticPreset preset) => preset switch
        {
            HapticPreset.Selection => HapticPatterns.PresetType.Selection,
            HapticPreset.Success => HapticPatterns.PresetType.Success,
            HapticPreset.Warning => HapticPatterns.PresetType.Warning,
            HapticPreset.Failure => HapticPatterns.PresetType.Failure,
            HapticPreset.LightImpact => HapticPatterns.PresetType.LightImpact,
            HapticPreset.MediumImpact => HapticPatterns.PresetType.MediumImpact,
            HapticPreset.HeavyImpact => HapticPatterns.PresetType.HeavyImpact,
            HapticPreset.RigidImpact => HapticPatterns.PresetType.RigidImpact,
            HapticPreset.SoftImpact => HapticPatterns.PresetType.SoftImpact,
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "[NiceVibrationsHapticBackend]: preset is unassigned."),
        };
    }
}
```

### Editor setup

1. **Project Settings › Player › Other Settings › Scripting Define Symbols**, tab **Android** và tab **iOS**: thêm
   `HORCRUX_NICE_VIBRATIONS`. Dự án dùng Build Profile thì thêm vào **Scripting Defines** của profile đang dùng.
   *Thiếu:* assembly backend không biên dịch, không ai đăng ký `IHapticBackend` — host báo thiếu đối số lúc `Awake` (sau Task 3).

**Commit:** `feat(haptics): add Nice Vibrations backend` trong `Assets/Horcrux`.

---

## Task 3 — Nối dự án category-jam (agent)

Bắt đầu sau khi Task 1–2 compile sạch. Agent đọc code thật của Task 1–2 trước khi viết.

### Files

| File | Đổi |
|---|---|
| `Assets/_CategoryJam/Gameplay/Definition/GameHapticIds.cs` (mới) | `enum GameHaptic { TapTile = 1, BoxLidClosed = 2 }` |
| `Assets/_CategoryJam/Gameplay/Haptics/GameHapticService.cs` (mới) | class rỗng `: HapticService<GameHaptic>` mang `[Service(typeof(IHapticService<GameHaptic>), typeof(IHapticSetting), FindFromScene = true)]` — `[Service]` không di truyền nên phải ở class con |
| `Assets/_CategoryJam/Bridge/GameplayHapticToggle.cs` (mới) | chép khuôn `GameplayAudioToggle`: `MonoBehaviour<IVibrationPersistentData, IHapticSetting>`, gán `IsOn = Vibration` lúc `Init`, `RegisterOnVibrationChange` / `Unregister…` ở `OnDestroy` |
| `Tile.cs`, `TileInitializer.cs` | `IInitializable<IAudioService<GameSfx, GameMusic>, IHapticService<GameHaptic>>`; `PlayPressFeedback` thêm `_haptic.Play(GameHaptic.TapTile);` ngay sau `PlaySfx(GameSfx.TapTile)` |
| `Box.cs`, `CategoryBoxInitializer.cs` | như trên; `HandleLidClosed` thêm `_haptic.Play(GameHaptic.BoxLidClosed);` ngay sau `PlaySfx(GameSfx.BoxComplete)` |

Grep mọi lời gọi `Init(` của `Tile`/`Box` trong test và Editor tool, sửa theo chữ ký mới.

### Editor setup (developer)

1. Mở `Assets/_CategoryJam/Scenes/Service.unity`. Tạo GameObject `haptic` cạnh GameObject chứa `GameAudioService`.
   *Thiếu:* không ai resolve được `IHapticService<GameHaptic>` — `Tile`/`Box` báo thiếu đối số lúc spawn.
2. Gắn `GameHapticService` và `GameplayHapticToggle` lên `haptic`.
   *Thiếu toggle:* rung luôn bật, Setting tắt rung không có tác dụng với gameplay.
3. Ô **Entries** của `GameHapticService`: thêm 2 dòng — `TapTile` → `LightImpact`, `BoxLidClosed` → `SoftImpact`.
   *Thiếu dòng:* console `[HapticService]: Haptic … is not in the entries.` ở lần chạm hay đóng nắp đầu.
4. Mở `Tile.prefab`, `GoldenTile.prefab` (`Addressables/Tile/`) và `CategoryBox.prefab` (`Addressables/Box/`): khối Init
   của Initializer hiện hai ô `audio` và `haptic`, cả hai nhận từ service. Lưu prefab.

### Kiểm

- Compile sạch (MSBuild của Rider) · `graphify update .`
- Giả định của backend: Play, chạm tile đầu tiên — không có cảnh báo luồng từ Lofelt trong console.
- Tắt define `HORCRUX_NICE_VIBRATIONS` thử một lần: Play báo thiếu `IHapticBackend` ở host (xác nhận "để nó nổ" là nổ thật), rồi bật lại.

**Commit:** `feat(gameplay): haptic khi chạm tile và đóng nắp box` ở repo dự án, kèm con trỏ `Assets/Horcrux`.

---

## Task 4 — Test (agent)

PlayMode trong `Horcrux.PlayModeTests`, backend giả đếm số lần gọi và preset nhận được, enum test riêng. Không cần Nice Vibrations.

| Case | Dựng | Kỳ vọng |
|---|---|---|
| Phát đúng preset | entries `A → LightImpact`, `B → SoftImpact`; `Play(B)` | backend nhận đúng 1 lần `SoftImpact` |
| Cổng tắt | `IsOn = false`; `Play(A)` | backend 0 lần, không log |
| Bật lại | `IsOn` false → true; `Play(A)` | backend 1 lần |
| Ghi cổng trước `Awake` | `IsOn = false` trước khi host `Awake`; `Play(A)` | backend 0 lần |
| Id thiếu | entries rỗng; `Play(A)` | `LogError` chứa `[HapticService]`, backend 0 lần |
| Trùng id | entries `A → Light`, `A → Soft` | `ArgumentException` lúc `Awake` |
| Phát dồn | `Play(A)` 10 lần cùng frame | backend 10 lần (không throttle — đúng thiết kế) |

Backend Nice Vibrations: đọc là đủ — 9 nhánh `switch` khớp 9 giá trị `HapticPreset`, giá trị `0` rơi vào nhánh ném.

**Commit:** `test(haptics): HapticService` trong `Assets/Horcrux`.

---

## Task 5 — Tài liệu module (agent)

`Runtime/Implementations/Foundations/Haptics/HapticSystem.md` viết từ code thật, theo khuôn `AudioSystem.md`: hai phía SDK –
dự án · luồng một lần rung · cách dùng (khai enum, class con, host, nối Setting) · **Trước khi chạy** (define, host, entries) ·
bảng preset · bẫy và quyết định · chữ ký · cấu trúc file. `.html` đồng bộ khi developer yêu cầu.

---

## Kịch bản chơi thử (developer, máy thật)

Cần: một Android có chỉnh biên độ (đời mới), **một Android rẻ**, iOS nếu có. Setting › Vibration bật.

| Làm | Nhìn / cảm | Dấu hiệu hỏng |
|---|---|---|
| Vào level 1, chạm một tile rồi nhả | một cú rất nhẹ, ngắn, **đúng lúc ngón chạm** — cùng tiếng tap và cú nhún; nhả tay tile bay, không thêm cú nào | rung lúc nhả · rung lệch tiếng rõ |
| Làm đầy cache, chạm thêm một tile | chạm có cú nhẹ; nhả: tile lắc, tiếng đục, **không** rung thêm | có cú thứ hai lúc nhả |
| Hoàn thành một box | lúc nắp chạm thân (cùng tiếng đóng hộp): một cú "thụp" mềm, dài hơn chạm, không gắt | không rung · rung lúc tile cuối vào, trước nắp |
| Dùng Magnet đóng nhiều box liền | các cú tách nhau theo từng nắp, không thành một vệt rung dài khó chịu | rung dính thành một hồi dài |
| Bật AutoPlay (nếu có cheat) | không rung khi bot chọn tile; nắp đóng vẫn rung | rung theo mỗi lần bot chọn |
| Setting tắt Vibration | chạm và đóng nắp đều im; nút UI cũng im | còn rung ở một trong hai |
| Tắt rung, kill app, mở lại | vẫn im | rung lại sau khi mở |
| Trên Android rẻ: hoàn thành box | cú đóng nắp chấp nhận được | gắt hoặc dài ⇒ đổi `BoxLidClosed` sang `MediumImpact` trong Inspector |
