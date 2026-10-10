# Audio System

Phát **SFX 2D** và **một bài nhạc nền** theo id enum của dự án. Game gọi
`_audio.PlaySfx(GameSfx.Click)`; dữ liệu clip nằm trong một asset catalog do game điền, SDK không biết clip nào.

- SFX: nhiều tiếng cùng lúc, mỗi lần phát lệch cao độ ±5%, một tiếng bị gọi dồn trong cùng frame chỉ phát một lần, **0 B cấp phát** ở đường phát.
- Music: một bài loop, đổi bài là cắt rồi phát bài mới, bật/tắt độc lập với SFX.
- Không UniTask, không Addressables, không phụ thuộc hệ Horcrux nào khác (**Foundation**).

## Hai phía

Phụ thuộc một chiều: dự án tham chiếu SDK, SDK không biết gì về dự án.

```
SDK — com.horcrux.runtime                    Dự án — assembly của game
────────────────────────────────             ───────────────────────────────────────────
IAudioSetting                                enum GameSfx · GameMusic   (số tường minh, từ 1)
  IsSfxOn · IsMusicOn  (không generic)
                                             GameAudioCatalog : AudioCatalog<GameSfx,GameMusic>
IAudioService<TSfx,TMusic>                     [CreateAssetMenu]  — class rỗng
  : IAudioSetting
  PlaySfx · PlaySfx(pitch) · PlayMusic       GameAudioService : AudioService<GameSfx,GameMusic>
  · StopMusic                                  [Service(typeof(IAudioService<…>), typeof(IAudioSetting),
                                                        FindFromScene = true)]  — class rỗng
AudioCatalog<TSfx,TMusic>  (abstract SO)
AudioEntry<TSfx> · MusicTrack<TMusic>        Asset catalog  ·  GameObject `audio` trong scene
AudioService<TSfx,TMusic>  (abstract host)
```

Hai class con rỗng là bắt buộc: generic `MonoBehaviour`/`ScriptableObject` không add/create được, và `[Service]` / `[CreateAssetMenu]` không dùng được trên generic.

## Luồng phát một tiếng

```
_audio.PlaySfx(GameSfx.Click)
   ├─ IsSfxOn = false ─────────────────────────> im lặng (hợp lệ)
   ├─ id không có trong catalog ───────────────> LogError, bỏ
   ├─ now − lần phát trước của entry < minInterval ─> bỏ (throttle, hợp lệ)
   ├─ pitch = random [0.95, 1.05]   (hoặc pitchScale nếu dùng PlaySfx(sfx, pitchScale))
   ├─ voice = rảnh đầu tiên, hết thì cướp voice sắp xong nhất
   └─ voice.clip/volume/pitch ← entry ; Play()
      busyUntil[voice] = now + clip.length / pitch
```

`now = Time.unscaledTime`: popup pause đặt `timeScale = 0` thì throttle vẫn chạy đúng.

## Cách dùng

### 1. Khai id và hai class con (một lần cho dự án)

```csharp
// enum: gán số TƯỜNG MINH, bắt đầu từ 1 — 0 là "chưa gán"
public enum GameSfx   { Click = 1, Win = 2 /* … */ }
public enum GameMusic { Level = 1 }

[CreateAssetMenu(fileName = "AudioCatalog", menuName = "Game/Audio Catalog")]
public sealed class GameAudioCatalog : AudioCatalog<GameSfx, GameMusic> { }

[Service(typeof(IAudioService<GameSfx, GameMusic>), typeof(IAudioSetting), FindFromScene = true)]
public class GameAudioService : AudioService<GameSfx, GameMusic> { }
```

Số trong enum là wire format của asset: **không đánh số lại**, chỉ thêm số mới.

### 2. Dựng catalog

1. `Create → Game → Audio Catalog` (tên menu do `[CreateAssetMenu]` của bạn khai).
2. **Entries**: mỗi tiếng một phần tử — chọn `Id` từ dropdown `GameSfx`, kéo `Clip`, chỉnh `Volume` (mặc định 1) và `Min Interval Seconds` (mặc định 0.03).
3. **Tracks**: mỗi bài nhạc một phần tử — `Id` từ dropdown `GameMusic`, `Clip`, `Volume`.
4. Bấm **Validate Entries**: Console phải có **một dòng log**, không error.

### 3. Dựng host trong scene

1. Scene sống suốt phiên chơi (scene persistent / bootstrap của game): tạo GameObject `audio`, Add Component `GameAudioService`.
2. Kéo catalog vào ô **Catalog**.
3. Bấm **Setup Audio Sources**: tạo 8 GameObject con `sfx_voice_0..7` + `music_source`, điền vào **Voices** / **Music Source**, ép cờ (`Play On Awake` tắt, `Spatial Blend = 0`, `Loop` tắt; riêng music `Loop` bật). Bấm lại chỉ bù ô thiếu. Muốn nhiều voice hơn: thêm ô vào mảng rồi bấm lại.
4. Bấm **Validate References**: một dòng log, không error. Lưu scene.

### 4. Phát tiếng, phát nhạc

Nhận service qua InitArgs, không static:

```csharp
// class đã có base riêng: IInitializable + gắn Initializer (component ẩn) trên prefab
public class Button : BaseWidget, IInitializable<IAudioService<GameSfx, GameMusic>>
{
    private IAudioService<GameSfx, GameMusic> _audio;
    public void Init(IAudioService<GameSfx, GameMusic> audio) => _audio = audio;
}

// class MonoBehaviour thuần: MonoBehaviour<T>, tự nhận, không cần Initializer
public class Player : MonoBehaviour<IAudioService<GameSfx, GameMusic>>
{
    private IAudioService<GameSfx, GameMusic> _audio;
    protected override void Init(IAudioService<GameSfx, GameMusic> audio) => _audio = audio;
}
```

```csharp
_audio.PlaySfx(GameSfx.Click);            // cao độ ngẫu nhiên ±5%
_audio.PlaySfx(GameSfx.Jump, 1.26f);      // cao độ chỉ định, không jitter
_audio.PlayMusic(GameMusic.Level);        // vào màn; gọi lại cùng bài thì không restart
_audio.StopMusic();
```

`Initializer` thêm trên prefab; để ô argument trống thì lấy từ service.

### 5. Nối Setting (bật/tắt Sound, Music)

Service không biết hệ lưu của game: game tự ghi hai cờ từ save của nó. Consumer chỉ cần hai cờ nên lấy
`IAudioSetting` (không generic, không phải gọi tên enum của dự án):

```csharp
public class AudioSettingBridge : MonoBehaviour<IMySaveData, IAudioSetting>
{
    private IMySaveData _save;
    private IAudioSetting _audio;

    protected override void Init(IMySaveData save, IAudioSetting audio)
    {
        _save = save;
        _audio = audio;

        _audio.IsSfxOn = _save.SoundOn;                  // áp giá trị đã lưu ngay lúc khởi động
        _audio.IsMusicOn = _save.MusicOn;
        _save.OnSoundChanged += SetSfx;                  // rồi bám theo mỗi lần người chơi đổi
        _save.OnMusicChanged += SetMusic;
    }

    private void OnDestroy()
    {
        _save.OnSoundChanged -= SetSfx;
        _save.OnMusicChanged -= SetMusic;
    }

    private void SetSfx(bool on) => _audio.IsSfxOn = on;
    private void SetMusic(bool on) => _audio.IsMusicOn = on;
}
```

Đặt component trên GameObject `audio`. Cờ có thể được ghi trước `Awake` của host: an toàn.

### 6. Một tiếng nhiều bậc (combo, chuỗi)

Cách chính: **mỗi bậc một entry** với clip thu sẵn, rồi chọn entry theo bậc và kẹp ở bậc cuối. Clip thu sẵn nghe hay hơn một clip bị đẩy pitch cao (ngắn lại, mỏng đi).

```csharp
private static readonly GameSfx[] ComboSfxs = { GameSfx.Combo2, GameSfx.Combo3, GameSfx.Combo4 };

_audio.PlaySfx(ComboSfxs[Mathf.Min(combo, ComboSfxs.Length + 1) - 2]);   // combo ≥ 2; từ bậc cuối trở đi lặp tiếng cuối
```

Khi chỉ có một clip và muốn tăng dần bằng cao độ, dùng `PlaySfx(sfx, pitchScale)`; công thức do call site giữ,
ví dụ nửa cung mỗi bậc:

```csharp
float pitchScale = Mathf.Pow(2f, Mathf.Min(step, maxStep) * semitonesPerStep / 12f);
_audio.PlaySfx(GameSfx.Combo, pitchScale);
```

### 7. Thêm một tiếng mới

1. Thêm giá trị vào `GameSfx` với **số mới** (không dùng lại số đã xoá).
2. Thêm entry vào catalog, **Validate Entries**.
3. Gọi `_audio.PlaySfx(GameSfx.X)`.

Thiếu bước 2: `PlaySfx` log `[AudioService]: Sfx X is not in the catalog` và không phát.

## Hành vi cần biết

| Tình huống | Kết quả |
|---|---|
| `IsSfxOn = false` | dừng mọi voice ngay, tiếng sau im; **không** ảnh hưởng nhạc |
| `IsMusicOn = false` | nhạc dừng ngay; `PlayMusic` lúc tắt chỉ **nhớ** bài; bật lại thì bài đó phát lại **từ đầu** |
| `PlayMusic` đúng bài đang phát | không restart, `volume` giữ theo lần phát đầu |
| `PlayMusic` id không có trong catalog | `LogError`, **giữ** bài hiện tại |
| `StopMusic()` | bỏ chọn bài: bật/tắt `IsMusicOn` sau đó không phát lại |
| 20 lần cùng id cùng frame | phát **1** lần (throttle theo entry); `≥ minInterval` thì phát (biên đóng) |
| Hai tiếng khác id cùng frame | cả hai phát |
| Hết voice | cướp voice có `busyUntil` nhỏ nhất — tiếng mới là phản hồi người chơi vừa gây ra |
| SFX hết voice | không bao giờ cắt nhạc (`musicSource` nằm ngoài mảng voice) |
| `PlaySfx(sfx, pitchScale)` | pitch dùng nguyên, không jitter, không kẹp; hợp đồng `pitchScale > 0` do caller giữ |
| `IsSfxOn` set trước `Awake` | an toàn (cầu nối Setting có thể chạy sớm hơn host) |
| `Awake` | `catalog.BuildTables()`, điền mốc throttle, cấp mảng `busyUntil`; sau đó `PlaySfx` không cấp phát |

## Số chỉnh bằng tai

| Số | Mặc định | Chỉnh ở đâu |
|---|---|---|
| `PitchSpread` | 0.05 | hằng `const` trong `AudioService`, không có ô Inspector |
| `minIntervalSeconds` | 0.03 | catalog, từng entry |
| `volume` | 1 | catalog, từng entry / track |
| Số voice | 8 | mảng **Voices** trên host |

Sửa số trong asset rồi nhấn Play, không sửa code.

## Nút Editor

| Nút | Ở đâu | Làm gì |
|---|---|---|
| `Validate Entries` | catalog | id chưa gán / không thuộc enum / trùng, clip null — kiểm **riêng** từng bảng (SFX và music được trùng số với nhau) |
| `Setup Audio Sources` | host | dựng voice + music source còn thiếu, rồi ép cờ |
| `Configure All Sources` | host | ép lại cờ cho mọi source (sau khi tự kéo hoặc đổi tay); không tạo mới |
| `Validate References` | host | thiếu catalog, music source, voice, hoặc ô `None` |

Validate chỉ chạy khi bấm: không `OnValidate` (đang kéo thả danh sách thì dữ liệu luôn chưa đủ), không chạy ở `Awake`.

## Bẫy và quyết định thiết kế

Đọc mục này trước khi "sửa cho gọn".

| Chỗ | Sự thật |
|---|---|
| **`[Service]` không di truyền** | `Inherited = false`: khai ở `AudioService` base không tới class con. Thiếu `[Service]` trên `GameAudioService` thì không ai resolve được `IAudioService<…>` |
| **`Awake` và `ConfigureAllSources` là `protected virtual`** | class con đặt trùng tên sẽ che hẳn bản base: bảng tra không dựng, mốc throttle không điền, cờ voice không ép — im lặng. Override thì phải gọi `base` |
| **Enum phải gán số tường minh từ 1** | `0` là `default`: ô Inspector quên điền đọc ra "chưa gán" mà không cần hàm riêng. Enum `long` không hỗ trợ (Unity lưu enum thành `int`) |
| **Id là enum của dự án, không phải `int` hay struct của SDK** | compiler chặn `GameMusic.Level` truyền vào `PlaySfx`; Inspector vẽ dropdown tên; hai bảng độc lập nên được trùng số |
| **Voice là mảng kéo tay, không pool runtime** | mảng đã là pool cố định; số voice thấy ngay trong Inspector. Ô `None` **nổ trễ**: chỉ chạm khi các voice trước đã bận (đúng lúc combo dồn) ⇒ bắt bằng `Validate References` từ lúc authoring |
| **Không guard runtime cho catalog / music source / voice / clip null** | mỗi cái nổ thật ở lần chạm đầu (NRE hoặc `IndexOutOfRange`) và nút Validate báo sớm. Guard là phí vĩnh viễn trong build cho một lần đọc log dễ hơn |
| **Mốc rảnh dùng `clip.length / pitch`** | `pitch` là tốc độ phát; dùng `clip.length` trần thì pitch 1.5 giữ voice thừa, pitch 0.7 cắt tiếng giữa chừng. Vẫn kiểm `!isPlaying` làm lưới thứ hai |
| **Throttle khoá theo entry, biên đóng** | hai tiếng khác nhau không chói nhau; biên mở làm nhịp đều bị bỏ ngẫu nhiên. Cùng một clip ở hai entry thì throttle riêng. Khi một entry có nhiều clip, phải đổi sang khoá theo clip |
| **`RandomPitch = -1f` là dấu "không truyền pitch"** | so bằng `Mathf.Approximately`. `pitchScale = 0` **không** bị đổi thành ngẫu nhiên mà làm `clip.length / 0` vô hạn ⇒ voice không bao giờ rảnh. Đừng truyền 0 |
| **Dictionary khoá enum** | không boxing khi comparer mặc định của enum là bản generic (đúng ở Unity 6). Nếu Profiler IL2CPP cho thấy cấp phát: đổi khoá sang `int` ở `Utilities/` |
| **Mọi cấp phát dồn về `Awake`** | throttle là `Dictionary` điền đủ mọi id ở `Awake`; thêm khoá lúc chạy mới cấp phát |
| **Không `DontDestroyOnLoad`** | host là component trong scene; đời sống do scene quyết. Đặt nó ở scene sống suốt phiên chơi |
| **Trùng id trong catalog** | `BuildTables` ném `ArgumentException` ở `Awake` (cái đầu không thắng im lặng); Validate báo sớm hơn |

## Chưa làm (thêm sau đều additive)

`PlaySfxAt` / `spatialBlend` (SFX 3D) · `volumeScale` · crossfade (PrimeTween) · `PauseMusic`/`ResumeMusic` · `PlayDynamic` · `AudioMixerGroup` (gán ở `ConfigureAllSources`) · nhiều clip cho một tiếng (chọn ngẫu nhiên / tuần tự; đổi `Clip` thành mảng, throttle sang khoá theo clip) · giới hạn số tiếng đồng thời cho **từng** entry (`maxInstances`) · `IAudioCatalog` khi có nguồn dữ liệu thứ hai.

## Chữ ký

**`Horcrux.Runtime.Abstractions.Audio`**

| Type | Thành viên |
|---|---|
| `IAudioSetting` | `bool IsSfxOn { get; set; }` · `bool IsMusicOn { get; set; }` |
| `IAudioService<TSfx,TMusic> : IAudioSetting, IService<IAudioService<TSfx,TMusic>>` | `void PlaySfx(TSfx)` · `void PlaySfx(TSfx, float pitchScale)` · `void PlayMusic(TMusic)` · `void StopMusic()` — `where TSfx, TMusic : struct, Enum` |

**`Horcrux.Runtime.Implementations.Audio`**

| Type | Thành viên |
|---|---|
| `AudioEntry<TSfx>` | `[Serializable]` · `Id` · `Clip` · `Volume` · `MinIntervalSeconds` |
| `MusicTrack<TMusic>` | `[Serializable]` · `Id` · `Clip` · `Volume` |
| `AudioCatalog<TSfx,TMusic> : ScriptableObject` | `abstract` · `Entries` · `BuildTables()` · `TryGetEntryById` · `TryGetMusicById` · nút `Validate Entries` |
| `AudioService<TSfx,TMusic> : MonoBehaviour, IAudioService<…>` | `abstract` · `protected virtual Awake()` · `protected virtual ConfigureAllSources()` · 3 nút Editor |

## Cấu trúc file

```
Runtime/Abstractions/Foundations/Audio/
├── IAudioSetting.cs
└── IAudioService.cs

Runtime/Implementations/Foundations/Audio/
├── AudioEntry.cs            một tiếng SFX
├── MusicTrack.cs            một bài nhạc
├── AudioCatalog.cs          SO: entry[] + track[] + bảng tra id → entry/track
├── AudioCatalog.Editor.cs   nút Validate Entries
├── AudioService.cs          host: SFX (throttle, pitch, voice) + Music (một source riêng)
├── AudioService.Editor.cs   nút Setup / Configure / Validate References
└── AudioSystem.md           tài liệu này

Tests/EditMode/   AudioCatalogTests · TestAudioCatalog
Tests/PlayMode/   AudioServiceTests · AudioServiceEditorTests · EnumKeyedLookupAllocationTests · TestAudioService · TestAudioCatalog
```

Test cần `AudioSource` đang phát dùng `Assume.That(AudioCanPlay())`: máy không có thiết bị audio thì ra *inconclusive*.
Chưa phủ: biên đóng chính xác của throttle (cần đồng hồ giả) và 0 B GC (Profiler trên IL2CPP, chạy tay).
