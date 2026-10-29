# Haptic System

Rung theo **preset** dựng sẵn của Nice Vibrations v4, gọi theo id enum của dự án. Game gọi
`_haptic.Play(GameHaptic.TapTile)`; preset của từng id chọn trong Inspector của host, SDK không biết khoảnh khắc nào rung kiểu gì.

- Chỉ preset (`LightImpact`, `SoftImpact`, …): không biên độ tự chỉnh, không rung liên tục, không clip `.haptic`.
- Một cổng bật/tắt (`IsHapticEnabled`) do game đẩy vào từ Setting của nó.
- Đường phát không cấp phát: bảng tra id → preset dựng một lần ở `Awake`.
- Phụ thuộc: Nice Vibrations v4 (`Lofelt.NiceVibrations`), tham chiếu thẳng từ `com.horcrux.runtime` — cùng hàng PrimeTween, UniTask.
  Không phụ thuộc hệ Horcrux nào khác (**Foundation**).

## Hai phía

Phụ thuộc một chiều: dự án tham chiếu SDK, SDK không biết gì về dự án.

```
SDK — com.horcrux.runtime                    Dự án — assembly của game
────────────────────────────────             ───────────────────────────────────────────
IHapticSetting                               enum GameHaptic   (số tường minh, từ 1)
  IsHapticEnabled  (không generic)
                                             GameHapticService : HapticService<GameHaptic>
IHapticService<THaptic> : IHapticSetting       [Service(typeof(IHapticService<GameHaptic>),
  Play(id)                                              typeof(IHapticSetting), FindFromScene = true)]
                                               — class rỗng
HapticEntry<THaptic>   id → PresetType
HapticService<THaptic> (abstract host)       Bridge Setting → IHapticSetting  ·  GameObject trong scene
```

Class con rỗng là bắt buộc: generic `MonoBehaviour` không add được, và `[Service]` không dùng được trên generic.

Interface (`IHapticService`, `IHapticSetting`) không lộ kiểu nào của Nice Vibrations; chỉ host và `HapticEntry` biết lib.
Call site của dự án không chạm `HapticPatterns` — chỉ Inspector của host hiện dropdown `PresetType`.

## Luồng một lần rung

```
_haptic.Play(GameHaptic.TapTile)
   ├─ IsHapticEnabled = false ──────────────────> im (hợp lệ), không tra bảng
   ├─ id không có trong entries ────────────────> LogError, bỏ
   └─ HapticPatterns.PlayPreset(preset)
        ├─ HapticController.hapticsEnabled = false ─> im (cổng của lib)
        ├─ preset = None ───────────────────────────> im
        └─ thiết bị không hỗ trợ ───────────────────> lib tự bỏ qua, không ném
```

`Awake` của host gọi `HapticController.Init()` (lib nạp lười ở lần phát đầu ⇒ cú rung đầu khựng; nạp lúc boot thì không) rồi dựng bảng tra.

## Cách dùng

### 1. Khai id và class con (một lần cho dự án)

```csharp
// enum: gán số TƯỜNG MINH, bắt đầu từ 1 — mỗi id là một KHOẢNH KHẮC gameplay, không phải một kiểu rung
public enum GameHaptic { TapTile = 1, BoxLidClosed = 2 }

[Service(typeof(IHapticService<GameHaptic>), typeof(IHapticSetting), FindFromScene = true)]
public class GameHapticService : HapticService<GameHaptic> { }
```

Số trong enum là wire format của scene: **không đánh số lại**, chỉ thêm số mới.

Assembly của game **không** cần tham chiếu `Lofelt.NiceVibrations`: `PresetType` chỉ nằm trong field private của host
(category-jam 2026-10-29: `CategoryJam.Gameplay.asmdef` compile sạch không có tham chiếu đó).

### 2. Dựng host trong scene

1. Scene sống suốt phiên chơi (scene service / bootstrap của game): tạo GameObject, Add Component `GameHapticService`.
2. Ô **Entries**: mỗi khoảnh khắc một dòng — `Id` từ dropdown `GameHaptic`, `Preset` từ dropdown `PresetType`.
   Dòng mới mặc định `Selection` (`PresetType.Selection = 0`): **đổi preset** sau khi thêm dòng.
3. Lưu scene.

### 3. Nối Setting (bật/tắt Vibration)

Service không biết hệ lưu của game: game tự ghi cổng từ save của nó. Consumer chỉ cần cổng nên lấy `IHapticSetting`
(không generic, không phải gọi tên enum của dự án):

```csharp
public class HapticSettingBridge : MonoBehaviour<IMySaveData, IHapticSetting>
{
    private IMySaveData _save;
    private IHapticSetting _haptic;

    protected override void Init(IMySaveData save, IHapticSetting haptic)
    {
        _save = save;
        _haptic = haptic;

        _haptic.IsHapticEnabled = _save.VibrationOn;     // áp giá trị đã lưu ngay lúc khởi động
        _save.OnVibrationChanged += SetEnabled;          // rồi bám theo mỗi lần người chơi đổi
    }

    private void OnDestroy() => _save.OnVibrationChanged -= SetEnabled;

    private void SetEnabled(bool on) => _haptic.IsHapticEnabled = on;
}
```

Đặt component trên cùng GameObject với host. **Bắt buộc**: cổng mặc định `false`, thiếu bridge là im cả phiên mà không có log.
Bản thật của category-jam: `Assets/_CategoryJam/Bridge/GameplayHapticToggle.cs` (đọc `IVibrationPersistentData` của Kelsey).

### 4. Rung

Nhận service qua InitArgs, không static:

```csharp
// class đã có base riêng: IInitializable + gắn Initializer trên prefab
public class Tile : PooledEntity, IInitializable<IAudioService<GameSfx, GameMusic>, IHapticService<GameHaptic>>
{
    private IAudioService<GameSfx, GameMusic> _audio;
    private IHapticService<GameHaptic> _haptic;

    public void Init(IAudioService<GameSfx, GameMusic> audio, IHapticService<GameHaptic> haptic)
    {
        _audio = audio;
        _haptic = haptic;
    }

    public void PlayPressFeedback()
    {
        _audio.PlaySfx(GameSfx.TapTile);
        _haptic.Play(GameHaptic.TapTile);   // ngay cạnh SFX của cùng khoảnh khắc
        _view.PlayPress(press);
    }
}
```

`Initializer` trên prefab: để ô `haptic` trống thì lấy từ service.

### 5. Thêm một khoảnh khắc rung mới

1. Thêm giá trị vào `GameHaptic` với **số mới**.
2. Thêm dòng vào **Entries** của host, chọn preset.
3. Gọi `_haptic.Play(GameHaptic.X)` cạnh SFX của khoảnh khắc đó.

Thiếu bước 2: `Play` log `[HapticService]: Haptic X is not in the entries.` và không rung.

## Bảng preset

Đọc từ `HapticPatterns.cs`. Chọn theo **độ dài** trước, độ mạnh sau — trên máy rẻ độ mạnh có thể không còn.

| Preset | Android có chỉnh biên độ | Android không chỉnh biên độ | iOS |
|---|---|---|---|
| `LightImpact` | 0.156 × 40 ms | 40 ms mức tối đa | Impact light |
| `SoftImpact` | 0.156 × 160 ms | 160 ms mức tối đa | Impact soft |
| `Selection` | 0.471 × 40 ms | 40 ms mức tối đa | Selection |
| `MediumImpact` | 0.471 × 80 ms | 80 ms mức tối đa | Impact medium |
| `RigidImpact` | 1.0 × 40 ms | 40 ms mức tối đa | Impact rigid |
| `HeavyImpact` | 1.0 × 160 ms | 160 ms mức tối đa | Impact heavy |
| `Success` · `Warning` · `Failure` | chuỗi nhiều nhịp | chuỗi nhiều nhịp, mức tối đa | Notification |

Máy Android không chỉnh biên độ phát **mọi** preset ở mức tối đa trong đúng thời lượng của nó: preset dài (`Soft`, `Heavy`) là
preset mạnh nhất trên máy đó.

category-jam (hướng chill): `TapTile` → `LightImpact`, `BoxLidClosed` → `SoftImpact` — cùng độ mạnh, phân biệt bằng độ dài.

## Hành vi cần biết

| Tình huống | Kết quả |
|---|---|
| `IsHapticEnabled` chưa ai ghi | `false`: im. Bridge Setting phải ghi lúc khởi động |
| `IsHapticEnabled = false` | `Play` thoát trước khi tra bảng: id thiếu cũng không log |
| `IsHapticEnabled` ghi trước `Awake` | giữ nguyên — `Awake` không chạm cổng |
| Id không có trong entries | `LogError`, không rung, không ném |
| Hai dòng cùng id | `ArgumentException` ở `Awake` (dòng đầu không thắng im lặng) |
| Nhiều `Play` dồn cùng frame | mỗi lần đều gọi lib — không throttle; lib phát chồng/cắt theo nền tảng |
| `HapticController.hapticsEnabled = false` | im — cổng thứ hai, của lib |
| Thiết bị không hỗ trợ | lib bỏ qua, không ném |

## Hai cổng rung trong một dự án

`HapticController.hapticsEnabled` là trạng thái static của lib, `IsHapticEnabled` là cổng của host. Horcrux **không** ghi cổng
của lib: ở category-jam `VibrationController` của Kelsey (nút UI) đang ghi nó — hai cửa ghi một trạng thái là hai nguồn sự thật.
Hai bridge đọc **cùng một** save nên hai cổng luôn khớp. Gộp khi nút UI chuyển sang gọi `IHapticService`.

## Bẫy và quyết định thiết kế

Đọc mục này trước khi "sửa cho gọn".

| Chỗ | Sự thật |
|---|---|
| **Cổng mặc định `false`** | cố ý: không rung khi chưa biết Setting của người chơi — rung nhầm một cú lúc boot tệ hơn im. Giá: bridge Setting là bắt buộc, thiếu là im cả phiên **không log**. Đừng đổi sang `true` để "chạy được ngay" |
| **`[Service]` không di truyền** | thiếu `[Service]` trên class con thì không ai resolve được `IHapticService<…>`; client báo thiếu đối số lúc spawn |
| **Gọi rung ngay cạnh dòng SFX, không qua event** | tiếng và rung là hai phản hồi của một khoảnh khắc — suy từ một chỗ. Ở category-jam `TilePressed` bắn cả khi tương tác bị khoá, còn hàm phản hồi thì không: nghe event là có lúc rung mà không có tiếng |
| **Không gọi `HapticPatterns` thẳng từ gameplay** | bỏ qua cổng Setting và bảng tra; kiểu rung lại rải trong code, đổi phải compile |
| **Id là khoảnh khắc, preset là kiểu rung** | đừng đặt id theo cường độ (`Heavy`): đổi cảm giác là đổi preset trong Inspector, không đổi tên id hay call site |
| **Không có enum preset riêng của Horcrux** | entry lưu thẳng `PresetType`; bỏ được một enum chép lại và một `switch` dịch. Giá trị: không bắt được dòng "chưa gán" vì `Selection = 0` |
| **Không lớp backend** | một lib, không kế hoạch đổi; lớp gián tiếp chỉ "phòng khi đổi lib" là dư. Hệ quả: test không đếm được lời phát |
| **`HapticController.Init()` ở `Awake`** | lib cấm gọi từ static constructor hay luồng khác; `Awake` chạy trên main thread lúc scene nạp |
| **Không rung trong `Update`** | rung là **sự kiện**, không phải trạng thái: rung mỗi frame nóng máy, hao pin, cảm giác nhoè |
| **Không `DontDestroyOnLoad`** | host là component trong scene; đặt nó ở scene sống suốt phiên chơi |

## Chưa làm (thêm sau đều additive)

Rung theo biên độ (`HapticPatterns.PlayEmphasis` / `PlayConstant` — điều kiện của haptic ramp theo cường độ) · rung liên tục
(`Begin/End` + ref-count) · throttle theo entry (field mới trên `HapticEntry`) · `IsSupported` · preset đổi theo bậc (nhiều id,
như Audio mục "một tiếng nhiều bậc") · nút UI gọi `IHapticService` thay `IVibrationService` của Kelsey.

## Chữ ký

**`Horcrux.Runtime.Abstractions.Haptics`**

| Type | Thành viên |
|---|---|
| `IHapticSetting` | `bool IsHapticEnabled { get; set; }` |
| `IHapticService<THaptic> : IHapticSetting, IService<IHapticService<THaptic>>` | `void Play(THaptic)` — `where THaptic : struct, Enum` |
| `HapticEntry<THaptic>` | `[Serializable]` · `Id` · `Preset` (`HapticPatterns.PresetType`) |
| `HapticService<THaptic> : MonoBehaviour, IHapticService<THaptic>` | `abstract` · `entries` (Inspector) · `IsHapticEnabled` mặc định `false` · `Play` |

## Cấu trúc file

```
Runtime/Abstractions/Foundations/Haptics/
├── IHapticSetting.cs
├── IHapticService.cs
├── HapticEntry.cs       một dòng id → preset
├── HapticService.cs     host: nạp lib, bảng tra, cổng, phát
└── HapticSystem.md      tài liệu này

Tests/PlayMode/   HapticServiceTests · TestHapticService
```

Test kiểm cổng và bảng tra qua log (6 case). Không phủ: lời phát thật (host gọi lib trực tiếp, không có chỗ đếm) — kiểm trên
máy thật theo kịch bản dưới.

## Kịch bản chơi thử (máy thật)

Bản category-jam. Cần: một Android có chỉnh biên độ (đời mới), **một Android rẻ**, iOS nếu có. Setting › Vibration bật.

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
