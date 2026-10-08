# Audio System — Plan

> **Loại tài liệu:** Plan — developer tự gõ từng dòng vào Horcrux. Agent viết test sau khi có code thật,
> rồi viết `.md` tài liệu module và `.html` theo chuỗi `code → .md → .html`.
> Steps dùng checkbox `- [ ]`. Sau mỗi task: compile sạch, agent so code với plan và phân loại chỗ lệch.

**Goal:** `IAudioService<TSfx, TMusic>` — phát **SFX 2D** và **Music** theo **id trong catalog**. SFX: nhiều tiếng cùng
lúc, mỗi tiếng một cao độ riêng, chống chói khi một clip bị gọi dồn trong cùng frame, **0 B cấp phát**
mỗi lần phát. Music: một bài loop, đổi bài là cắt rồi phát bài mới, bật/tắt độc lập với SFX.

**Architecture:** 8 file, id là enum của dự án (type parameter `TSfx`, `TMusic`). Dự án chỉ gọi tên 2 file ở `Abstractions/`, và khai 2 class con rỗng để chốt enum.

```
Abstractions/Foundations/Audio/
├── IAudioSetting.cs       IsSfxOn · IsMusicOn  (không generic)
└── IAudioService.cs        IAudioService<TSfx,TMusic> : IAudioSetting — PlaySfx(TSfx) · PlayMusic(TMusic) · StopMusic

Implementations/Foundations/Audio/
├── AudioEntry.cs           AudioEntry<TSfx>: một tiếng SFX: id · clip · volume · minInterval  (class [Serializable])
├── MusicTrack.cs           MusicTrack<TMusic>: một bài nhạc: id · clip · volume  (class [Serializable])
├── AudioCatalog.cs         AudioCatalog<TSfx,TMusic> abstract ScriptableObject chứa entry[] + track[] — game điền, Horcrux không biết clip nào
├── AudioCatalog.Editor.cs  nút Validate (Odin [Button])
├── AudioService.cs         AudioService<TSfx,TMusic> abstract host MonoBehaviour: SFX (tra entry → throttle → cao độ → cấp voice) + Music (một source riêng)
└── AudioService.Editor.cs  OnValidate ép cờ voice và cờ music source · context menu thêm 12 voice

Dự án (Category Jam):
GameSfx · GameMusic (enum, số tường minh từ 1) · GameAudioCatalog : AudioCatalog<GameSfx,GameMusic> · GameAudioService : AudioService<GameSfx,GameMusic> + [Service]
```

**Tech stack:** C#, `AudioSource`, `Sisus.Init`, `Sirenix.OdinInspector` (chỉ ở
file Editor). Không UniTask, không Addressables, không Feel trong đường phát. Không object pool
runtime: voice SFX là mảng `AudioSource` cố định kéo tay (đã là pool, xem "Quyết định trái trực giác"),
music là một source riêng.

---

## Ngữ cảnh đã chốt

| Nhóm | Chốt |
|---|---|
| **Ai gọi** | Gameplay của dự án, qua một lớp nối mỏng do agent viết (Task 4). Ở Category Jam: 20 call site hiện gọi `AudioController.Instance.PlaySoundEffect(string)` không kèm vị trí, 16 tiếng, trong đó 5 tiếng combo `sfx_box_combo2..6` là 5 clip riêng. Music: `MusicController` của `_Gameplay` (`Play(name)`, `Stop`) và toggle Music trong Setting (`IAudioPersistentData.RegisterOnMusicChange`). |
| **Mục tiêu** | Tiếng gameplay phát đúng frame sự kiện. Nghiệm thu bằng chơi thử: tap item, nhận box, hoàn thành box 3 lần liên tiếp, dùng 3 booster — mỗi hành động có đúng một tiếng, không chói khi nhiều box hoàn thành cùng lúc, tắt Sound trong Setting thì im ngay. Vào level có nhạc nền loop; tắt Music thì nhạc dừng, bật lại thì nhạc phát lại; tắt Music không ảnh hưởng SFX và ngược lại. |
| **Ngân sách** | Nhịp *mỗi tương tác*; cao điểm ~10–20 lần/giây có thể cùng frame khi cascade. **Hot path đã xác nhận**: mọi cấp phát dồn về `Awake`, đường `PlaySfx` không `new`, không LINQ, không boxing. |
| **Ranh giới** | Service chỉ lo: tra entry · throttle · cấp voice · áp tham số · giữ một bài nhạc đang phát. Danh mục clip và danh sách bài nhạc là **asset của game**. Hai cờ `IsSfxOn` / `IsMusicOn` là **property** game set từ save của nó — service không biết hệ save. **Foundation**: không gọi hệ Horcrux nào khác. |
| **Chỗ đặt** | Horcrux. Không type nào mang domain dự án. Lớp nối (enum id, asset catalog, cầu nối setting) nằm trong dự án. |
| **Tên** | `Giả định (cần xác nhận):` `AudioCatalog` · `AudioEntry` · `AudioService` · `IAudioSetting` · `IsSfxOn` · `PlaySfx`. Đã chốt: `MusicTrack` · `IsMusicOn` · `PlayMusic` · `StopMusic` · id là enum của dự án, hệ generic theo hai enum, dự án kế thừa. Đổi tên trước khi gõ, không đổi sau. |
| **Cố ý KHÔNG làm + lý do** | ① **`pitchScale` / `volumeScale` trên `PlaySfx`** — không caller. Thêm sau là **một tham số tuỳ chọn**, call site cũ không đổi; pitch đã là một biến cục bộ ở đường phát nên thêm không phải thiết kế lại. ② **Interface cho catalog và entry** — chỉ có một implementation; tách khi có nguồn dữ liệu thứ hai (remote, procedural). ③ **SFX 3D** (`PlaySfxAt`, `spatialBlend`) — mọi call site hiện tại là 2D. ④ **Crossfade, fade, pause/resume nhạc, `PlayDynamic` (nhạc đổi theo trạng thái), mixer group** — chưa chốt cần; mỗi cái là thêm method hoặc field. Music bản tối thiểu **có** trong plan. ⑤ **Nhiều clip biến thể cho một tiếng** (chọn ngẫu nhiên, tuần tự, theo trọng số) — mỗi entry đúng **một** clip; phát triển sau. Khi thêm, đổi `Clip` thành mảng và throttle sang khoá theo clip (§0.3). ⑥ **Lưu bền `IsSfxOn`** — game đã có save riêng. ⑦ **Kiểu id riêng của Horcrux bọc `int`** — framework không sở hữu từ vựng của dự án: struct bọc `int` không chặn số bừa và không cho Inspector biết tên; enum của dự án làm được cả hai. ⑧ **Gộp `IAudioSetting` vào interface generic** — consumer chỉ cần hai cờ (cầu nối Setting, composite Horcrux) sẽ phải gọi tên enum của dự án. |
| **Quyết định trái trực giác** | Voice là **mảng `AudioSource` kéo tay** thay vì pool tạo lúc chạy — số voice là hằng cấu hình trong một asset, thiếu thì lộ ô trống. Throttle khoá theo **clip** chứ không theo id — xem §0.3. Hết voice thì **cướp voice sắp xong** thay vì bỏ tiếng mới — tiếng mới là phản hồi người chơi vừa gây ra. **Không object pool runtime**: mảng voice đã là pool cố định (tạo sẵn lúc authoring, cho mượn–cướp–trả, không `Instantiate`/`Destroy` ở đường phát); `ObjectPool<AudioSource>` thêm cấp phát khi lớn lên và giấu số voice khỏi Inspector. **Hệ generic theo hai enum, `abstract` + class con rỗng của dự án** thay cho một service không generic nhận số: hai enum cho compile-time phân biệt SFX và music, Inspector vẽ dropdown tên, và dự án không cast hay gõ số — đổi lại mỗi dự án khai hai class con và gắn `[Service]` lên class con (`Inherited = false`). **Music có `AudioSource` riêng, ngoài mảng voice** — SFX cướp voice không bao giờ cắt nhạc. Tắt Music rồi bật lại thì bài đang chọn phát lại từ đầu: `PlayMusic` lúc tắt chỉ ghi nhớ bài, không phát. |
| **Mở rộng sau** (đều additive) | `PlaySfx(TSfx, float pitchScale)` cho pitch ramp · `PlaySfxAt` · crossfade (PrimeTween) · `PauseMusic`/`ResumeMusic` · `AudioMixerGroup` trên service, `OnValidate` gán cho mọi voice · `IAudioCatalog` khi có nguồn thứ hai · nạp catalog qua `AssetReference` · hàm đổi enum → `int` ở `Utilities/` nếu Profiler cho thấy `Dictionary` khoá enum cấp phát (Task 3, case #19). |

### Đã khảo sát trước khi viết

| Nguồn | Lấy | Không lấy, vì |
|---|---|---|
| `Kelsey.IAudioService` + `SoundController` (contract ScrewDom, giữ nguyên cho LiveOps) | Cầu nối setting: `IAudioPersistentData.RegisterOnSoundChange` → cờ của service (Task 4) | 30 method, mỗi tiếng một method, 25 method rỗng — catalog + id thay cho việc đó. Phụ thuộc Feel. |
| `MusicController` + `MusicContainer` của `_Gameplay` | Hành vi cần giữ: phát theo tên, `Stop`, tắt theo Setting | Kế thừa `BaseAudioController`, khoá chuỗi, `PlayDynamic` + filter + mixer — chưa có nhu cầu chốt; gỡ ở Task 4. |
| `AudioController` + `SoundEffectController` của `_Gameplay` | Chưa lấy gì (chọn clip không trùng `RandomButPickOnce` để dành khi có nhiều clip) | 15 file, 4 lớp (controller → effect → container → pool) để phát một clip; khoá chuỗi; coroutine mỗi lần phát; `maxInstances` per entry chồng vai với throttle. |
| `MMSoundManager` (Feel) | Xác nhận công thức voice rảnh `clip.length / |pitch|` (§0.1) | `PlayOptions` hơn 40 field — tham số authoring thuộc catalog, không thuộc call site. Coroutine `AutoDisableAudioSource` mỗi lần phát là rác GC đúng lúc combo dồn. Track qua mixer là "Mở rộng sau". |
| Horcrux `Utilities/` | — | Không có helper audio nào trên đĩa. Pitch ngẫu nhiên là một dòng `Random.Range`, viết tại chỗ. |

---

## §0. Ba điều cần biết + số nào là số cảm giác

### 0.1. Thời lượng phát phụ thuộc cao độ

`AudioSource.pitch` là **tốc độ phát** ⇒ `t_play = clip.length / pitch`. Voice cần biết lúc nào rảnh; dùng
`clip.length` trần là sai đúng theo tỉ lệ pitch:

| `pitch` | `t_play` thật (clip 0.4s) | Nếu dùng `clip.length` |
|---|---|---|
| 1.5 | 0.27s | giữ voice thừa 0.13s ⇒ hết voice sớm, tiếng sau bị cướp |
| 0.7 | 0.57s | trả voice sớm ⇒ **cắt tiếng giữa chừng** |

Vẫn kiểm `!isPlaying` làm lưới thứ hai: mốc thời gian là dự tính, `isPlaying` là sự thật của engine.

### 0.2. Pitch ngẫu nhiên

Mỗi lần phát rút `pitch` đều trong `[1 − s, 1 + s]` với `s` là **hằng cố định** của service
(`PitchSpread = 0.05`), không khai theo entry. Không đổi đơn vị: pitch đã là tỉ lệ tần số, `1` là không đổi.
Cùng một tiếng lặp 20 lần nghe không còn là cái máy; 5% lệch nghe được mà chưa thành "nốt khác".

`s` là hằng nhỏ nên `pitch` luôn trong `[0.95, 1.05]`: dương, hữu hạn, nên `t_play` ở §0.1 luôn hữu hạn.
Cần tắt hoặc chỉnh cho từng tiếng khi có ca thật ⇒ thêm field vào `AudioEntry` lúc đó.

### 0.3. Throttle theo tiếng, biên đóng

20 mảnh vỡ cùng frame phát cùng một clip 20 lần: biên độ cộng dồn (to, méo), pha gần trùng gây tiếng
"xẹt". Bỏ qua lần phát nếu **entry đó** vừa phát trong `minInterval`; **≥ `minInterval` thì phát** (biên
đóng — biên mở làm nhịp đều đặn bị bỏ ngẫu nhiên).

Khoá theo **entry** (mỗi entry đúng một clip), nên **hai tiếng khác nhau phát cùng lúc thì không chói**.
Khi sau này một entry có nhiều clip, throttle phải đổi sang khoá theo clip.

### 0.4. Số cảm giác — chọn bằng tai

Những số dưới chọn bằng tai, không dẫn ra từ đâu; là điểm khởi đầu, chưa phải mốc.

| Số | Khởi đầu | Tune ở đâu |
|---|---|---|
| `PitchSpread` | 0.05 | hằng `const` trong `AudioService`, không có ô Inspector |
| `minIntervalSeconds` | 0.05 | asset `AudioCatalog`, per-entry |
| `volume` | 1.0 | asset `AudioCatalog`, per-entry |
| Số voice | 12 | mảng `Voices` trên host |

Cách tune: sửa số trong asset, nhấn Play. Không sửa code (trừ `PitchSpread`, hằng một dòng).

---

## Luồng dữ liệu

```
game: GameAudio.Play(GameSfx.BoxReceive)         (lớp nối trong dự án, Task 4)
  └─> IAudioService<GameSfx, GameMusic>.Service.PlaySfx(GameSfx.BoxReceive)
        │
        ├─ !IsSfxOn ──────────────────────────────────────────> return (im lặng, hợp lệ)
        ├─ _entryIndexById[sfx] ─ thiếu ──────────────────────> LogError, return
        ├─ entry.Clip null ───────────────────────────────────> LogError, return
        ├─ now − _lastPlayTime[entry] < minInterval ─────────> return (throttle, hợp lệ)
        ├─ pitch = Random(1 − s, 1 + s)
        ├─ voice = RentVoice(now)                              rảnh đầu tiên, hết thì cướp sắp xong
        └─ voice.clip/volume/pitch ← entry ; Play()
           _voiceBusyUntil[voice] = now + clip.length / pitch
           _lastPlayTime[entry] = now
```

`now = Time.unscaledTime`: popup pause đặt `timeScale = 0`, throttle và mốc rảnh phải tiếp tục đúng.

```
game: GameAudio.PlayMusic(GameMusic.Level)
  └─> IAudioService<GameSfx, GameMusic>.Service.PlayMusic(GameMusic.Level)
        ├─ _musicIndexById[music] ─ thiếu ────────────> LogError, return
        ├─ _currMusicIndex = index                    nhớ bài đã chọn, kể cả khi Music đang tắt
        └─ ApplyMusicState()
              ├─ !IsMusicOn hoặc chưa chọn bài ─────> musicSource.Stop()
              ├─ đang phát đúng clip đó ────────────> chỉ cập nhật volume (gọi lại cùng bài không restart)
              └─ else ──────────────────────────────> clip/volume ← track ; Play()   (loop do OnValidate ép)

IsMusicOn setter, StopMusic ─> cùng ApplyMusicState (StopMusic đặt _currMusicIndex = −1 trước)
```

---

## Bản đồ triển khai

| Task | File | Ai |
|---|---|---|
| 1 | `Abstractions/Foundations/Audio/IAudioSetting.cs` · `IAudioService.cs` | developer |
| 2 | `Implementations/Foundations/Audio/AudioEntry.cs` · `MusicTrack.cs` · `AudioCatalog.cs` · `AudioCatalog.Editor.cs` · xoá `AudioId.cs` · spike serialize | developer |
| 3 | `Implementations/Foundations/Audio/AudioService.cs` · `AudioService.Editor.cs` | developer |
| 4 | Lớp nối Category Jam + gỡ `AudioController` | agent, sau khi Task 3 compile |
| 5 | Test · `SystemPlan.md` · tài liệu module | agent |

Thứ tự: **1 → 2 → 3 → 4 → 5**. Mỗi task xong là một mốc compile sạch.

---

### Task 1: 2 contract

**Files:** `Assets/Horcrux/Runtime/Abstractions/Foundations/Audio/IAudioSetting.cs` (mới) · `IAudioService.cs` (đổi)

**Interfaces:**
- Consumes: `Horcrux.Runtime.Abstractions.IService<T>`.
- Produces: `IAudioSetting` (2 member) · `IAudioService<TSfx, TMusic> : IAudioSetting, IService<IAudioService<TSfx, TMusic>>` (3 member + 2 kế thừa).

**Quyết định thiết kế:**

| Quyết định | Vì sao |
|---|---|
| Id là **type parameter** `TSfx` / `TMusic`, Horcrux không có kiểu id riêng bọc `int` | Tập id do dự án đặt tên. Kiểu bọc của framework chỉ nói "đây là một khoá" và `implicit` từ `int` mở lại cửa cho số bừa; enum của dự án cho compiler và Inspector biết **khoá nào** hợp lệ. Call site không cast, asset không gõ số |
| `where TSfx : struct, Enum` | Chỉ nhận enum thật. Không `unmanaged`, không đổi sang `int`: id đi thẳng vào bảng tra bằng chính kiểu enum, nên không phụ thuộc underlying type |
| SFX và music là **hai** type parameter | `GameMusic.Level` truyền vào `PlaySfx` là lỗi biên dịch, không phải tiếng phát nhầm khi hai enum trùng số. Hai bảng tra độc lập nên id được trùng số |
| `IAudioSetting` tách khỏi interface generic | Consumer chỉ cần hai cờ (cầu nối Setting, một composite Horcrux) không đóng được generic của dự án — Horcrux không gọi tên enum của dự án (NT3). Tách theo consumer là `I` |
| `IAudioService<,>` kế thừa `IAudioSetting` | Một object, hai mặt. Dự án đăng ký cả hai làm defining type (Task 3) |
| `IsSfxOn`, `IsMusicOn` là property, setter có hệ quả | Tắt phải im **ngay** ở mọi đường ghi, rẻ, không ném ⇒ hệ quả nằm trong setter, không là method cạnh field |
| Không có `StopAll` public, không có `bool loop` trên `PlayMusic` | Tắt cờ đã dừng mọi voice; nhạc nền luôn loop, ép ở source lúc authoring. Jingle một lần là SFX |
| Không `in`/`out` trên type parameter | Enum là value type, variance không áp |

**Chỗ đổi (theo thứ tự):**

- [ ] **Step 1: tạo `IAudioSetting.cs`**

```csharp
namespace Horcrux.Runtime.Abstractions.Audio
{
    /// <summary>Player toggles of the audio system. Not generic: a consumer that only reads settings never names the game's id enums.</summary>
    public interface IAudioSetting
    {
        /// <summary>Turning it off also silences voices already playing. The game writes it from its own save.</summary>
        bool IsSfxOn { get; set; }

        /// <summary>Off stops the music; on again restarts the chosen track. The game writes it from its own save.</summary>
        bool IsMusicOn { get; set; }
    }
}
```

- [ ] **Step 2: thay toàn bộ `IAudioService.cs`** (cả 12 dòng đổi, chép nguyên file)

```csharp
using System;

namespace Horcrux.Runtime.Abstractions.Audio
{
    /// <summary>Plays 2D sound effects and one looping music track from the game's catalog, keyed by the game's own enums.</summary>
    /// <remarks>SFX: many at once, each with its own pitch, bursts of one sound throttled. Music: one track at a time.</remarks>
    /// <typeparam name="TSfx">The game's sound-effect enum. Starts at 1: <c>0</c> reads as unassigned. Underlying type must be <c>int</c>.</typeparam>
    /// <typeparam name="TMusic">The game's music enum. Same rules; may share numbers with <typeparamref name="TSfx"/>.</typeparam>
    public interface IAudioService<TSfx, TMusic> : IAudioSetting, IService<IAudioService<TSfx, TMusic>>
        where TSfx : struct, Enum
        where TMusic : struct, Enum
    {
        /// <param name="sfx">Catalog entry. A value missing from the catalog logs an error and plays nothing.</param>
        void PlaySfx(TSfx sfx);

        /// <summary>Replaces the current track. Same track again keeps playing. While <see cref="IAudioSetting.IsMusicOn"/> is off, only remembers the choice.</summary>
        /// <param name="music">Music track in the catalog. A value missing from it logs an error and keeps the current track.</param>
        void PlayMusic(TMusic music);

        void StopMusic();
    }
}
```

- [ ] **Step 3: Compile sạch.** `AudioId.cs` vẫn còn trên đĩa, chưa ai gọi — xoá ở Task 2 khi `AudioEntry` và `MusicTrack` đã bỏ nó. Commit — `feat(sdk): audio contracts keyed by the game's enums (IAudioSetting, IAudioService<TSfx,TMusic>)`

---

### Task 2: `AudioEntry<TSfx>` + `MusicTrack<TMusic>` + `AudioCatalog<TSfx,TMusic>`

**Files:** `Assets/Horcrux/Runtime/Implementations/Foundations/Audio/` — `AudioEntry.cs` · `MusicTrack.cs` · `AudioCatalog.cs` · `AudioCatalog.Editor.cs` (đổi) · `Assets/Horcrux/Runtime/Abstractions/Foundations/Audio/AudioId.cs` (xoá)

**Interfaces:**
- Consumes: `AudioClip`; không còn phụ thuộc `Abstractions/`.
- Produces: `AudioEntry<TSfx>` (5 field authoring) · `MusicTrack<TMusic>` (4 field) · `AudioCatalog<TSfx, TMusic> : ScriptableObject` abstract (`Entries`, `Tracks`).

**Quyết định thiết kế:**

| Quyết định | Vì sao |
|---|---|
| `AudioEntry<TSfx>` giữ field `TSfx id`, không `int id` | Unity serialize enum thành số, Inspector vẽ dropdown tên. Designer chọn tên, không gõ số; số trong asset vẫn là số của enum (wire format, không đánh số lại) |
| Id là enum, không đổi sang `int` ở đâu cả; thoả thuận duy nhất là **enum gán số tường minh từ 1** | `0` là `default` của enum nên ô Inspector quên điền đọc ra "chưa gán" mà không cần hàm riêng. Catalog so id bằng `EqualityComparer<T>.Default`, service khoá bảng tra bằng chính enum. Không phụ thuộc underlying type |
| `AudioCatalog<,>` là `abstract` không có member abstract | Dự án khai class con rỗng để chốt hai enum, và để gắn `[CreateAssetMenu]` — attribute đó không dùng được trên generic. Tự chạy một mình là việc của class con, không phải của Horcrux (không mang domain) |
| `MusicTrack` là class riêng, không dùng lại `AudioEntry` | Nhạc không có throttle, nhiều clip; hai kiểu đổi vì hai lý do khác nhau, và dùng chung thì ô vô nghĩa nằm đầy Inspector |
| `AudioEntry` là `class`, không `struct` | Unity serialize, sống suốt đời asset ⇒ không cấp phát theo tần số phát; struct bị copy mỗi lần đọc qua property |
| `volume` mặc định `1f` | `float` mặc định `0` là giá trị hợp lệ: entry mới thêm vào danh sách **im lặng** mà không một dòng log. Mặc định đặt ngay trong khai báo |
| `[Min(0f)]` cho interval | Kẹp dải **lúc authoring** thay cho clamp runtime; ô quên điền không thể mang giá trị âm |
| Catalog **chỉ là dữ liệu** | Không dictionary, không con trỏ chọn clip trong SO: field đổi lúc Play không quay lại khi dừng Play, asset không có thứ tự khởi tạo. Mọi bảng tra và state ở service |
| Validate **chỉ ở Editor, khi được hỏi** (nút), không `OnValidate`, không chạy ở `Awake` | Đang dựng danh sách thì dữ liệu luôn chưa hợp lệ, `OnValidate` sẽ đỏ console sau mỗi lần thêm phần tử. Runtime tự bảo vệ bằng log "không có trong catalog" ở đường phát |
| Validate kiểm thêm `Enum.IsDefined` | Xoá một giá trị khỏi enum thì asset vẫn giữ số cũ; Inspector hiện số trần. Chỉ lộ ra khi được hỏi |
| Trùng id kiểm O(n²) không `HashSet`; kiểm **riêng từng bảng** | n ≈ 16, chạy khi bấm nút. Hai bảng tra độc lập; trùng giữa hai bảng không gây lỗi |

**Chỗ đổi (theo thứ tự):**

- [ ] **Step 1: `AudioEntry.cs`**

| Dòng | Cũ | Mới | Vì sao |
|---|---|---|---|
| 2 | `using Horcrux.Runtime.Abstractions.Audio;` | xoá | không còn `AudioId` |
| 8 | `public class AudioEntry` | `public class AudioEntry<TSfx> where TSfx : struct, Enum` | id là enum của dự án |
| 10 | `[SerializeField] private string displayName;` | xoá | log nêu `Sfx entry #index` và tên enum, không cần tên gõ tay |
| 11 | `[SerializeField] private int id;` | `[SerializeField] private TSfx id;` | dropdown tên |
| 13 | `[SerializeField, Range(0, 1)] private float volume;` | `[SerializeField, Range(0, 1)] private float volume = 1f;` | mặc định `0` = entry mới im lặng |
| 14 | `[SerializeField] private float minIntervalSeconds = 0.05f;` | `[SerializeField, Min(0f)] private float minIntervalSeconds = 0.05f;` | kẹp lúc authoring |
| 16 | `public string DisplayName => displayName;` | xoá | |
| 17 | `public AudioId Id => id;` | `public TSfx Id => id;` | |

- [ ] **Step 2: `MusicTrack.cs`**

| Dòng | Cũ | Mới |
|---|---|---|
| 2 | `using Horcrux.Runtime.Abstractions.Audio;` | xoá |
| 8 | `public class MusicTrack` | `public class MusicTrack<TMusic> where TMusic : struct, Enum` |
| 10 | `[SerializeField] private string displayName;` | xoá |
| 11 | `[SerializeField, Tooltip("Mapping AudioId Value")] private int id;` | `[SerializeField] private TMusic id;` |
| 13 | `[SerializeField, Range(0, 1)] private float volume;` | `[SerializeField, Range(0, 1)] private float volume = 1f;` |
| 15 | `public string DisplayName => displayName;` | xoá |
| 16 | `public AudioId Id => id;` | `public TMusic Id => id;` |

- [ ] **Step 3: `AudioCatalog.cs`**

| Dòng | Cũ | Mới | Vì sao |
|---|---|---|---|
| 7 | `[CreateAssetMenu(fileName = "AudioCatalog", menuName = "Horcrux/Audio Catalog")]` | xoá | dự án khai trên class con của nó |
| 8 | `public partial class AudioCatalog : ScriptableObject` | `public abstract partial class AudioCatalog<TSfx, TMusic> : ScriptableObject` + hai dòng `where TSfx : struct, Enum` / `where TMusic : struct, Enum` | |
| 10 | `AudioEntry[] entries = Array.Empty<AudioEntry>()` | `AudioEntry<TSfx>[] entries = Array.Empty<AudioEntry<TSfx>>()` | |
| 11 | `MusicTrack[] tracks = Array.Empty<MusicTrack>()` | `MusicTrack<TMusic>[] tracks = Array.Empty<MusicTrack<TMusic>>()` | |
| 13 | `IReadOnlyList<AudioEntry> Entries` | `IReadOnlyList<AudioEntry<TSfx>> Entries` | |
| 14 | `IReadOnlyList<MusicTrack> Tracks` | `IReadOnlyList<MusicTrack<TMusic>> Tracks` | |

- [ ] **Step 4: `AudioCatalog.Editor.cs`**

| Dòng | Cũ | Mới | Vì sao |
|---|---|---|---|
| 1 | — | thêm `using System;` và `using System.Collections.Generic;` | `Enum.IsDefined`, `EqualityComparer` |
| 6 | `public partial class AudioCatalog` | `public abstract partial class AudioCatalog<TSfx, TMusic>` + hai dòng `where` như Step 3 | cùng class |
| 31 | `MusicTrack track = tracks[index];` | `MusicTrack<TMusic> track = tracks[index];` | |
| 32 | `string label = $"music track #{index} '{track.DisplayName}'";` | `string label = $"Music track #{index} ({track.Id})";` | nêu rõ **Music** và giá trị enum; id chưa gán hoặc không thuộc enum in ra dạng số |
| 34–38 | `if (!track.Id.IsValid)` … `has Id 0 (unassigned)` | khối `default` + khối `IsDefined` dưới | |
| 42 | `if (tracks[j].Id.Equals(track.Id))` | `if (EqualityComparer<TMusic>.Default.Equals(tracks[j].Id, track.Id))` | so bằng comparer của enum, không boxing |
| 44 | `… of music track #{j}. …` | `… of Music track #{j}. …` | |
| 60 | `AudioEntry entry = entries[index];` | `AudioEntry<TSfx> entry = entries[index];` | |
| 61 | `string label = $"entry #{index} '{entry.DisplayName}'";` | `string label = $"Sfx entry #{index} ({entry.Id})";` | |
| 63–67 | `if (!entry.Id.IsValid)` … | như dòng 34–38, `TSfx` và `entry` | |
| 71 | `if (entries[j].Id.Equals(entry.Id))` | `if (EqualityComparer<TSfx>.Default.Equals(entries[j].Id, entry.Id))` | |
| 73 | `… of entry #{j}. …` | `… of Sfx entry #{j}. …` | |
| 93 | `{entries.Length} entries, {tracks.Length} music tracks — …` | `{entries.Length} Sfx entries, {tracks.Length} Music tracks — …` | |

Khối thay cho dòng 34–38 (music; bản entry đổi `track` → `entry`, `TMusic` → `TSfx`). Id không thuộc enum hoặc chưa gán đã hiện dạng số trong `label`:

```csharp
if (EqualityComparer<TMusic>.Default.Equals(track.Id, default))
{
    Debug.LogError($"[AudioCatalog]: {label} has no Id (unassigned).", this);
    return false;
}

if (!Enum.IsDefined(typeof(TMusic), track.Id))
{
    Debug.LogError($"[AudioCatalog]: {label} is no member of {typeof(TMusic).Name}.", this);
    return false;
}
```

- [ ] **Step 5: xoá `AudioId.cs`** trong Project window (giữ `.meta` đồng bộ). Compile sạch; `grep AudioId` trong `*.cs` toàn project trả **0**.

**Editor setup (asset của game, không trong `Assets/Horcrux/`):**

1. Khai hai enum và hai class con trong dự án (Category Jam: `Assets/_Gameplay/Scripts/Definition/` và `Assets/_Gameplay/Scripts/Common/Audio/`). Enum gán số tường minh **từ 1**; thiếu thì giá trị đầu là `0` và bị coi là chưa gán:

```csharp
public enum GameSfx { TapItem = 1, BoxReceive = 2 /* … */ }
public enum GameMusic { Level = 1 }

[CreateAssetMenu(fileName = "AudioCatalog", menuName = "Game/Audio Catalog")]
public sealed class GameAudioCatalog : AudioCatalog<GameSfx, GameMusic> { }
```

2. Project window → `Create → Game → Audio Catalog`, đặt tên theo game (Category Jam: `AudioCatalog_Gameplay`, thư mục `Assets/_Gameplay/Audio/`).
3. Mỗi tiếng một entry trong **Entries**: **chọn** `Id` từ dropdown `GameSfx` · kéo một clip vào `Clip`. Mỗi bài nhạc một phần tử trong **Tracks**, `Id` chọn từ dropdown `GameMusic`. Thiếu: `PlaySfx`/`PlayMusic` log error "not in the catalog".
4. Bấm nút **Validate Entries**. Console phải có **một dòng log** xác nhận, không error. Thiếu bước này: id trùng, chưa gán hoặc không còn trong enum chỉ lộ ra khi Play, dưới dạng tiếng không phát.

- [ ] **Step 6: Spike serialize — làm ngay sau khi compile, trước Task 3.** Phương án này dựa vào việc Unity serialize `AudioEntry<TSfx>[]` trong một `ScriptableObject` generic và vẽ enum thành dropdown; plan chưa kiểm trên Unity 6000.0.73f1. Trong `Assets/Horcrux/Tests/EditMode/` khai `TestSfx { A = 1 }` (`int`), `TestMusic : byte { M = 1 }` và `TestAudioCatalog : AudioCatalog<TestSfx, TestMusic>` có `[CreateAssetMenu]` (Task 5 dùng lại) — hai enum khác underlying type để cùng một spike kiểm cả hai. Tạo asset, thêm một entry và một track, chọn id bằng dropdown, kéo clip, Save, đóng–mở lại Inspector. Kỳ vọng: dropdown hiện tên enum ở cả hai bảng, giá trị còn nguyên, nút Validate báo sạch. Enum `long` không nằm trong phạm vi hỗ trợ: Unity lưu enum thành `int` 32 bit. **Không đạt thì dừng và báo developer** — các Task sau không đổi hướng được tại chỗ (NT5).

- [ ] **Step 7: Kiểm chứng — case agent sẽ test**

| Input | Kỳ vọng |
|---|---|
| 3 entry id `1, 2, 3`, mỗi entry 1 clip | `LogInvalidEntries() == 0`, không log |
| Music track chưa gán id (`0`) | 1 error mở bằng `Music track #index`, trả `1` |
| Entry id `0` | 1 error mở bằng `Sfx entry #index`, trả `1` |
| Entry id `99` (số không thuộc enum) | 1 error nêu số và tên enum, trả `1` |
| Hai music track cùng `id = 2` | 1 error trỏ track **sau**, trả `1` |
| Hai entry cùng `id = 5` | 1 error trỏ entry **sau**, trả `1` |
| Music track `clip = null` | 1 error, trả `1` |
| Entry `clip = null` | 1 error, trả `1` |
| Entry SFX id `1` và music track id `1` | `LogInvalidEntries() == 0` (hai bảng độc lập) |
| Catalog rỗng | trả `0`, không log |
| Entry mới thêm trong Inspector | `volume == 1`, `minIntervalSeconds == 0.05` |

- [ ] **Step 8: Commit** — `feat(sdk): audio catalog generic over the game's id enums`

---

### Task 3: `AudioService<TSfx, TMusic>`

**Files:** `Assets/Horcrux/Runtime/Implementations/Foundations/Audio/AudioService.cs` · `AudioService.Editor.cs` (cả hai chưa có trên đĩa, chép nguyên file)

**Interfaces:**
- Consumes: `AudioCatalog<TSfx, TMusic>`, `AudioEntry<TSfx>`, `MusicTrack<TMusic>` (cùng hệ, nhận class cụ thể qua `[SerializeField]`).
- Produces: `AudioService<TSfx, TMusic> : MonoBehaviour, IAudioService<TSfx, TMusic>` — abstract; dự án khai class con và gắn `[Service]` lên đó.

**Bản đồ §0 → code:**

| §0 | Code |
|---|---|
| §0.1 mốc rảnh | `_voiceBusyUntil[voice] = now + clip.length / pitch` |
| §0.2 pitch ngẫu nhiên | `Random.Range(1f - PitchSpread, 1f + PitchSpread)` — hằng, không clamp |
| §0.3 throttle biên đóng | `now - last < minInterval → return`; `last` khởi tạo `float.NegativeInfinity` để lần đầu luôn qua |

**Quyết định thiết kế:**

| Quyết định | Vì sao |
|---|---|
| `abstract` + dự án khai class con rỗng, `[Service]` gắn ở class con | Generic `MonoBehaviour` không add vào GameObject được; class con chốt hai enum. `[Service]` khai `Inherited = false` nên khai ở base **không** tới class con — đây là bước setup, ghi ở "Trước khi chạy" |
| `Awake` và `OnValidate` là `protected virtual` | Class con của dự án đặt trùng tên sẽ che hẳn bản của base: voice không được gom, bảng tra không được dựng, im lặng |
| Bảng tra `Dictionary<TSfx,int>` / `Dictionary<TMusic,int>` khoá thẳng bằng enum | Không đổi sang `int`, không phụ thuộc underlying type. Chỉ không boxing khi comparer mặc định của enum là bản generic (đúng ở Unity 6) — case #19 là phép kiểm; không đạt thì thêm hàm đổi enum → `int` ở `Utilities/` và đổi khoá bảng |
| Entry chưa gán id (`default`) vẫn vào bảng tra như một khoá | Chỉ lộ khi caller truyền `default`; nút Validate (Task 2) báo từ lúc authoring. Một nhánh canh ở runtime là code cho ca đã có chỗ bắt |
| Voice = mảng `AudioSource` kéo tay trên cùng GameObject | Số voice là hằng cấu hình trong một asset ⇒ kéo thả, ô trống có người nhìn. Một GameObject đủ vì chỉ phát 2D. Không `PlayOneShot` trên một source vì mọi tiếng sẽ chung một `pitch` |
| Compact mảng voice ở `Awake` thành `_voices` không null | Ô trống log một lần ở boot; đường phát không kiểm null 12 lần mỗi tiếng |
| Mọi bảng tra dựng ở `Awake` | `_entryIndexById` · `_musicIndexById` · `_lastPlayTimeByEntry` · `_voiceBusyUntil`. Sau `Awake`, `PlaySfx` không cấp phát |
| Throttle lưu theo chỉ số entry (`float[]`) chứ không `Dictionary<AudioClip,_>` | Chỉ số đã có trong tay; mảng không hash, không cấp phát. Cùng một clip ở hai entry thì throttle riêng — chấp nhận, không có ca thật |
| Rảnh đầu tiên, hết thì cướp voice có `busyUntil` nhỏ nhất | Bỏ tiếng mới là bỏ phản hồi người chơi vừa gây ra; voice sắp xong bị cắt ít gây chú ý nhất |
| Kiểm cả `busyUntil` và `isPlaying` | Mốc là dự tính (§0.1); `isPlaying` là sự thật của engine |
| `Time.unscaledTime` | Popup pause đặt `timeScale = 0`; `Time.time` đứng yên ⇒ throttle đóng băng, mọi tiếng sau bị bỏ |
| Giá trị enum lạ, entry không clip ⇒ `LogError` mỗi lần, không throw | Sai lúc setup mà không nổ ⇒ phải báo, kể cả lặp. Throw giữa gameplay là trả giá cho một lỗi cấu hình. Chuỗi nội suy chứa enum boxing, nhưng chỉ ở đường lỗi |
| Bị throttle, `IsSfxOn = false` ⇒ im lặng | Ca hợp lệ, không phải lỗi |
| `IsSfxOn = false` dừng voice, **không** xoá mốc throttle | Mốc là `unscaledTime` tăng đều; chỉ chặn trong `minInterval` ≈ 0.05s sau khi bật lại — không có ca "bỏ oan" |
| Service **không** gọi validate của catalog | Validate là nút Editor (Task 2). Dữ liệu sai lộ ra ở đường phát bằng log, không bằng một lượt quét ở boot |
| Music: một `musicSource` riêng, một hàm `ApplyMusicState` | `IsMusicOn`, `PlayMusic`, `StopMusic` chỉ đổi trạng thái (cờ, bài đang chọn) rồi gọi một hàm làm source khớp trạng thái ⇒ một cửa ghi lên source, không ba nơi tự `Play`/`Stop` lệch nhau (§3.4) |
| `PlayMusic` lúc `IsMusicOn = false` chỉ nhớ bài | Bật lại thì đúng bài game đã chọn phát; game không phải gọi lại `PlayMusic` sau mỗi lần toggle |
| Gọi lại đúng bài đang phát không restart | So `clip`; level mới gọi `PlayMusic` cùng bài không bị giật nhạc |
| Giá trị music lạ ⇒ `LogError`, **giữ bài hiện tại** | Sai lúc setup không nổ ⇒ phải báo; cắt nhạc đang chạy vì một giá trị sai là phá thêm |
| Không `DontDestroyOnLoad` | Host là component kéo vào scene; đời sống do scene quyết. Game đặt nó ở scene persistent của nó |

**Editor setup — bước thật, trong dự án và scene của game:**

1. Khai class con trong dự án (Category Jam: `Assets/_Gameplay/Scripts/Common/Audio/GameAudioService.cs`). Thiếu `[Service]`: không ai resolve được `IAudioService<GameSfx, GameMusic>`.

```csharp
[Service(typeof(IAudioService<GameSfx, GameMusic>), typeof(IAudioSetting), FindFromScene = true)]
public sealed class GameAudioService : AudioService<GameSfx, GameMusic> { }
```

   `Giả định (cần xác nhận):` `[Service]` của InitArgs nhận nhiều defining type trong một attribute — kiểm lúc compile.
2. Mở scene dịch vụ persistent của game (Category Jam: `Assets/_Game/Scenes/Service.unity`). Tạo GameObject `[Audio]` → Add Component `GameAudioService`.
3. Kéo asset catalog (Task 2) vào ô **Catalog**. Thiếu: mỗi `PlaySfx` log một error "not in the catalog".
4. Chuột phải tiêu đề component `GameAudioService` → **Add 12 Voices**: 12 `AudioSource` được thêm vào chính GameObject và điền vào **Voices**. Muốn số khác thì add tay rồi kéo vào mảng; `OnValidate` tự tắt `Play On Awake`, `Loop`, đặt `Spatial Blend = 0` cho mọi voice trong mảng. Thiếu voice: mỗi `PlaySfx` log một error.
5. Add Component `AudioSource` thứ 13 lên cùng GameObject, kéo vào ô **Music Source** (không kéo vào **Voices**). `OnValidate` tự đặt `Loop = true`, tắt `Play On Awake`, `Spatial Blend = 0`. Thiếu: mỗi lần gọi `PlayMusic`, `StopMusic`, hoặc set `IsMusicOn` log một error.
6. Save scene. Kiểm: mảng **Voices** không có ô `None`, ô **Music Source** và **Catalog** có giá trị, Console không error.

- [ ] **Step 1: `AudioService.cs`**

```csharp
using System;
using System.Collections.Generic;
using Horcrux.Runtime.Abstractions.Audio;
using Horcrux.Runtime.Utilities;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Audio
{
    /// <summary>Plays 2D SFX: find entry → throttle per sound → random pitch → rent a voice. No allocation per play. Also keeps one looping music track.</summary>
    /// <remarks>
    /// Abstract: a game derives one sealed class, closes <typeparamref name="TSfx"/> and <typeparamref name="TMusic"/> with its enums,
    /// and puts <c>[Service]</c> on it — the attribute is not inherited from here.
    /// Voices are <see cref="AudioSource"/> components assigned in the Inspector, not created at runtime: a missing one shows
    /// as an empty slot while authoring, and the voice count is tuned without recompiling.
    /// Music has its own source outside the voices, so a stolen voice never cuts the music.
    /// All lookup tables and play state live here, never in <see cref="AudioCatalog{TSfx,TMusic}"/>.
    /// </remarks>
    public abstract partial class AudioService<TSfx, TMusic> : MonoBehaviour, IAudioService<TSfx, TMusic>
        where TSfx : struct, Enum
        where TMusic : struct, Enum
    {
        /// <summary>Each play draws pitch uniformly in [1 − spread, 1 + spread]. Fixed: not tuned per sound.</summary>
        private const float PitchSpread = 0.05f;

        [Splitter("References")]
        [SerializeField] private AudioCatalog<TSfx, TMusic> catalog;

        [SerializeField, Tooltip("One AudioSource per simultaneous sound. When all are busy, the one closest to finishing is taken over.")]
        private AudioSource[] voices = Array.Empty<AudioSource>();

        [SerializeField, Tooltip("Plays the music track. Not one of the voices, so SFX never take it over.")]
        private AudioSource musicSource;

        private bool _isSfxOn = true;
        private bool _isMusicOn = true;
        private int _currMusicIndex = -1;
        private readonly Dictionary<TMusic, int> _musicIndexById = new();
        private AudioSource[] _voices = Array.Empty<AudioSource>();
        private float[] _voiceBusyUntil = Array.Empty<float>();
        private readonly Dictionary<TSfx, int> _entryIndexById = new();
        private float[] _lastPlayTimeByEntry = Array.Empty<float>();


        #region Properties

        public bool IsSfxOn
        {
            get => _isSfxOn;
            set
            {
                _isSfxOn = value;

                if (!value)
                    StopAllVoices();
            }
        }

        public bool IsMusicOn
        {
            get => _isMusicOn;
            set
            {
                _isMusicOn = value;
                ApplyMusicState();
            }
        }

        #endregion

        #region Unity Callbacks

        protected virtual void Awake()
        {
            CollectVoices();
            BuildEntryTables();
        }

        #endregion

        #region API

        public void PlaySfx(TSfx sfx)
        {
            if (!_isSfxOn)
                return;

            if (!_entryIndexById.TryGetValue(sfx, out int entryIndex))
            {
                Debug.LogError($"[AudioService]: Sfx {sfx} is not in the catalog.", this);
                return;
            }

            AudioEntry<TSfx> entry = catalog.Entries[entryIndex];
            AudioClip clip = entry.Clip;

            if (clip == null)
            {
                Debug.LogError($"[AudioService]: Sfx {sfx} has no clip.", this);
                return;
            }

            if (_voices.Length == 0)
            {
                Debug.LogError("[AudioService]: No Sfx voice assigned — use 'Add 12 Voices' on the component.", this);
                return;
            }

            float now = Time.unscaledTime;

            if (now - _lastPlayTimeByEntry[entryIndex] < entry.MinIntervalSeconds)
                return;

            float pitch = UnityEngine.Random.Range(1f - PitchSpread, 1f + PitchSpread);
            int voiceIndex = RentVoice(now);
            AudioSource voice = _voices[voiceIndex];

            voice.clip = clip;
            voice.volume = entry.Volume;
            voice.pitch = pitch;
            voice.Play();

            _voiceBusyUntil[voiceIndex] = now + clip.length / pitch;
            _lastPlayTimeByEntry[entryIndex] = now;
        }

        public void PlayMusic(TMusic music)
        {
            if (!_musicIndexById.TryGetValue(music, out int musicIndex))
            {
                Debug.LogError($"[AudioService]: Music {music} is not in the catalog.", this);
                return;
            }

            _currMusicIndex = musicIndex;
            ApplyMusicState();
        }

        public void StopMusic()
        {
            _currMusicIndex = -1;
            ApplyMusicState();
        }

        #endregion

        #region Class Methods

        private void CollectVoices()
        {
            int assignedAmount = 0;

            for (int i = 0; i < voices.Length; i++)
            {
                if (voices[i] != null)
                    assignedAmount++;
                else
                    Debug.LogError($"[AudioService]: Sfx Voices[{i}] is empty.", this);
            }

            _voices = new AudioSource[assignedAmount];
            _voiceBusyUntil = new float[assignedAmount];

            for (int i = 0, v = 0; i < voices.Length; i++)
            {
                if (voices[i] != null)
                    _voices[v++] = voices[i];
            }
        }

        private void BuildEntryTables()
        {
            if (catalog == null)
            {
                Debug.LogError("[AudioService]: Catalog is not assigned — every Sfx and Music will be dropped.", this);
                return;
            }

            IReadOnlyList<AudioEntry<TSfx>> entries = catalog.Entries;
            _lastPlayTimeByEntry = new float[entries.Count];
            Array.Fill(_lastPlayTimeByEntry, float.NegativeInfinity);

            for (int i = 0; i < entries.Count; i++)
                _entryIndexById.TryAdd(entries[i].Id, i);   // first one wins; duplicates are reported by the catalog's Validate button

            IReadOnlyList<MusicTrack<TMusic>> tracks = catalog.Tracks;

            for (int i = 0; i < tracks.Count; i++)
                _musicIndexById.TryAdd(tracks[i].Id, i);
        }

        /// <summary>Single place that makes the music source match <see cref="IsMusicOn"/> and the chosen track.</summary>
        private void ApplyMusicState()
        {
            if (musicSource == null)
            {
                Debug.LogError("[AudioService]: Music Source is not assigned — music is dropped.", this);
                return;
            }

            if (!_isMusicOn || _currMusicIndex < 0)
            {
                musicSource.Stop();
                return;
            }

            MusicTrack<TMusic> track = catalog.Tracks[_currMusicIndex];
            musicSource.volume = track.Volume;

            if (musicSource.isPlaying && musicSource.clip == track.Clip)
                return;

            musicSource.clip = track.Clip;
            musicSource.Play();
        }

        /// <summary>First idle voice; when none, the one that finishes soonest is taken over.</summary>
        private int RentVoice(float now)
        {
            int soonestIndex = 0;
            float soonestBusyUntil = float.MaxValue;

            for (int i = 0; i < _voices.Length; i++)
            {
                if (now >= _voiceBusyUntil[i] && !_voices[i].isPlaying)
                    return i;

                if (_voiceBusyUntil[i] < soonestBusyUntil)
                {
                    soonestBusyUntil = _voiceBusyUntil[i];
                    soonestIndex = i;
                }
            }

            return soonestIndex;
        }

        private void StopAllVoices()
        {
            for (int i = 0; i < _voices.Length; i++)
            {
                _voices[i].Stop();
                _voiceBusyUntil[i] = 0f;
            }
        }

        #endregion
    }
}
```

- [ ] **Step 2: `AudioService.Editor.cs`**

```csharp
using System;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Audio
{
    public abstract partial class AudioService<TSfx, TMusic>
        where TSfx : struct, Enum
        where TMusic : struct, Enum
    {
#if UNITY_EDITOR
        private const int DefaultVoiceAmount = 12;

        /// <summary>Three flags each cause a different silent bug: a bang on load, a sound that never ends, a 2D effect heard from afar.</summary>
        protected virtual void OnValidate()
        {
            for (int i = 0; i < voices.Length; i++)
            {
                if (voices[i] != null)
                    ConfigureVoice(voices[i]);
            }

            if (musicSource != null)
            {
                ConfigureVoice(musicSource);
                musicSource.loop = true;
            }
        }

        [ContextMenu("Add 12 Voices")]
        private void AddDefaultVoices()
        {
            var added = new AudioSource[DefaultVoiceAmount];

            for (int i = 0; i < DefaultVoiceAmount; i++)
            {
                added[i] = UnityEditor.Undo.AddComponent<AudioSource>(gameObject);
                ConfigureVoice(added[i]);
            }

            voices = added;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        private static void ConfigureVoice(AudioSource voice)
        {
            voice.playOnAwake = false;
            voice.loop = false;
            voice.spatialBlend = 0f;
        }
#endif
    }
}
```

- [ ] **Step 3: Kiểm chứng — case agent sẽ test** (biên theo: rỗng · một phần tử · chạm giới hạn · trùng · hai sự kiện cùng lúc · frame đầu). Dựng host bằng `TestAudioService : AudioService<TestSfx, TestMusic>`, khai cạnh `TestAudioCatalog` trong `Assets/Horcrux/Tests/EditMode/`.

| # | Input | Kỳ vọng |
|---|---|---|
| 1 | `PlaySfx(sfx)` khi `IsSfxOn = false` | không voice nào `isPlaying` |
| 2 | `PlaySfx((TestSfx)99)` (giá trị không có trong catalog) | 1 error `[AudioService]:`, không throw |
| 3 | Chưa gán catalog | 1 error ở `Awake`; mỗi `PlaySfx` thêm 1 error, không throw |
| 4 | Mảng `Voices` rỗng | mỗi `PlaySfx` 1 error, không throw |
| 5 | `Voices` có 1 ô `None` giữa 12 | 1 error ở `Awake`; 11 voice vẫn phát |
| 6 | `PlaySfx(sfx hợp lệ)` | voice được chọn có `clip == entry.Clip`, `volume == entry.Volume` |
| 7 | Entry có `clip = null` | mỗi `PlaySfx` 1 error `[AudioService]:`, không throw |
| 8 | Hai entry khác id, cùng frame, `minInterval = 0.05` | cả hai phát (throttle theo entry) |
| 9 | 100 lần phát, `minInterval = 0`, mỗi lần cách nhau 1 giây | luôn dùng đúng clip đó, không treo |
| 10 | 20 lần cùng id cùng frame, `minInterval = 0.05` | phát **1** lần |
| 11 | 20 lần cùng id cùng frame, `minInterval = 0` | phát 12 lần rồi cướp voice; `busyUntil` của voice bị cướp là nhỏ nhất trước đó |
| 12 | 2 lần cách đúng `minInterval` | cả hai phát (biên đóng) |
| 13 | `timeScale = 0`, 2 lần cách 0.1s thật | lần hai phát |
| 14 | 1000 lần phát | mọi `voice.pitch` trong `[0.95, 1.05]`, không phải lúc nào cũng bằng 1 |
| 15 | Clip 0.4s (đọc lại `voice.pitch` sau khi phát) | `busyUntil − now == 0.4 / voice.pitch` |
| 16 | 20 lần phát cùng entry (`minInterval = 0`) | không phải cả 20 `voice.pitch` bằng nhau |
| 17 | `IsSfxOn = false` khi đang phát | mọi voice `isPlaying == false` ngay |
| 18 | Lần phát đầu tiên sau `Awake` | không bị throttle (`NegativeInfinity`) |
| 19 | Profiler: 20 lần phát/giây, 10 giây, bản build IL2CPP | **0 B** GC Alloc trên đường `PlaySfx` (khoá `TestSfx : int`), và trên `PlayMusic` lặp cùng nhịp (khoá `TestMusic : byte`) — cũng là phép kiểm cho việc khoá bảng tra bằng enum. Tiền kiểm chỉ riêng `Dictionary` khoá enum (chưa cần `AudioService`): `Tests/PlayMode/EnumKeyedLookupAllocationTests.cs`, chạy trên player IL2CPP |
| 20 | Bật `Play On Awake` trên một voice rồi rời Inspector | `OnValidate` tắt lại |
| 21 | `PlayMusic(music)` khi `IsMusicOn = true` | `musicSource.isPlaying`, `clip` và `volume` đúng track, `loop == true` |
| 22 | `PlayMusic((TestMusic)99)` khi đang phát bài A | 1 error `[AudioService]:`, bài A vẫn phát |
| 23 | `PlayMusic(A)` rồi `PlayMusic(B)` | `clip == B` |
| 24 | `PlayMusic(A)` hai lần liên tiếp | lần hai không restart: `time` không về 0 |
| 25 | `IsMusicOn = false` khi đang phát | `isPlaying == false` ngay; `IsSfxOn` và voice SFX không đổi |
| 26 | `IsMusicOn = false`, `PlayMusic(A)`, rồi `IsMusicOn = true` | sau `PlayMusic` chưa phát; sau bật phát bài A |
| 27 | `PlayMusic(A)`, `StopMusic()`, bật tắt `IsMusicOn` | không phát (bài đã bị bỏ chọn) |
| 28 | `IsSfxOn = false` khi nhạc đang phát | nhạc vẫn phát |
| 29 | 20 lần `PlaySfx` liên tiếp hết voice, nhạc đang phát | nhạc không bị cắt (`musicSource` không nằm trong `_voices`) |
| 30 | Chưa gán `musicSource`, gọi `PlayMusic`, `StopMusic`, set `IsMusicOn` | mỗi lệnh 1 error, không throw |
| 31 | `PlayMusic` khi chưa gán catalog | 1 error "not in the catalog", không throw |
| 32 | `PlaySfx` rồi đọc qua `IAudioSetting` | `IsSfxOn` và `IsMusicOn` đọc qua interface thấy đúng giá trị vừa set qua `IAudioService<,>` (cùng một object) |
| 33 | Hai entry trùng id | phát entry **đầu**, không throw |

- [ ] **Step 4: Commit** — `feat(sdk): add AudioService (authored voices, per-sound throttle, random pitch, music source)`

---

### Task 4: Lớp nối Category Jam (agent viết, sau khi Task 3 compile)

Hai hệ cùng tên `IAudioService` cùng tồn tại có chủ ý: `Kelsey.IAudioService` + `SoundController` là
lớp contract ScrewDom, đóng băng để kéo LiveOps; `Horcrux.Runtime.Abstractions.Audio.IAudioService<,>` phục
vụ gameplay. Chúng ở hai assembly khác nhau; file nào cần cả hai thì dùng alias
`using HxAudio = Horcrux.Runtime.Abstractions.Audio;`.

| Việc | Ở đâu | Ghi chú |
|---|---|---|
| Thêm tham chiếu `com.horcrux.runtime` | `Assets/_Gameplay/CategoryJam.Gameplay.asmdef` | Chiều phụ thuộc: gameplay → Horcrux |
| `enum GameSfx` gán số tường minh từ `= 1` | `Assets/_Gameplay/Scripts/Definition/GameSfx.cs` | 16 giá trị thay 11 hằng chuỗi + `GetComboAudioID`. Số đã vào asset là wire format: phần tử mới thêm ở cuối |
| `enum GameMusic` gán số tường minh từ `= 1` | `Assets/_Gameplay/Scripts/Definition/GameMusic.cs` | Liệt kê bài từ `MusicContainer` hiện tại; `Giả định (cần xác nhận):` số bài đọc từ asset khi tới task này |
| `GameAudioCatalog` · `GameAudioService` | `Assets/_Gameplay/Scripts/Common/Audio/` | Hai class con rỗng như Editor setup của Task 2 và 3. `[Service]` gắn ở `GameAudioService`, không ở base |
| `static class GameAudio { Play(GameSfx) · PlayMusic(GameMusic) · StopMusic() }` | `Assets/_Gameplay/Scripts/Common/GameAudio.cs` | Mỗi hàm một dòng: `IAudioService<GameSfx, GameMusic>.Service.PlaySfx(sfx)`, `PlayMusic(music)`, `StopMusic()`. Chỉ rút ngắn tên interface đã đóng generic cho 20 call site; không còn phép đổi kiểu |
| Đổi call site của `MusicController` | Grep `MusicController` trong `_Gameplay`, `_Game` | `Play(name)` → `GameAudio.PlayMusic(GameMusic.X)`; `Stop()` → `GameAudio.StopMusic()`. `Pause`/`Continue`/`PlayDynamic` còn caller thì **dừng, hỏi developer** — ngoài bản tối thiểu |
| Đổi 20 call site | `Box`, `BoxAnimation`, `BoxesContainer`, `CacheHoles`, `ShoppingCart`, `CacheHole`, `GoodsItem`, `NormalMovingShelf`, `NormalShelf`, `SingleMovingShelf`, `GameDirector.Events` | `GameAudio.Play(GameSfx.X)`. Combo: `GameSfx.BoxCombo2..6` theo `comboNumber`, giữ nguyên 5 clip — `Giả định (cần xác nhận):` chưa đổi sang pitch ramp vì đó là đổi cảm giác chơi |
| Cầu nối setting | `Assets/_Game/Scripts/Common/GameplayAudioToggle.cs`, component trên `[Audio]` | `MonoBehaviour<IAudioPersistentData, IAudioSetting>` (không cần biết `GameSfx`): `Init` set `IsSfxOn = Sound`, `IsMusicOn = Music` rồi `RegisterOnSoundChange` + `RegisterOnMusicChange`; `OnDestroy` unregister cả hai. Thay cho 4 dòng ở `CategoryGameController.Start` |
| Gỡ hệ cũ | `Assets/_Gameplay/Scripts/Common/Audio/` (15 file, gồm `MusicController` + `MusicContainer`) · `Addressables/Prefabs/Sounds/` (16 prefab) · nhóm Addressables SFX · `Constant.AudioID.cs` · `GameplaySession.SfxOn` · instance `AudioController` trong `Service.unity` | Xoá sau khi mọi call site đã chuyển và chơi thử xong. Grep `AudioController`, `MusicController`, `AUDIO_`, `SfxOn` phải trả 0 |
| Ghi vào contract doc | `docs/ScrewDom_Contract.md` mục 2 | Một dòng: `SoundController` đóng băng cho LiveOps; audio gameplay đi qua Horcrux; không gộp hai bên |

**Editor setup (agent liệt kê lại đầy đủ khi tới task này):** tạo `AudioCatalog_Gameplay` (từ `GameAudioCatalog`) với 16 entry,
id chọn từ dropdown `GameSfx`, clip lấy từ 16 prefab `sfx_*` hiện tại; thêm **Tracks** id chọn từ dropdown `GameMusic`, clip lấy từ
`MusicContainer` hiện tại · `[Audio]` trong `Service.unity` với `GameAudioService` theo Task 3 (gồm ô **Music Source**) ·
add `GameplayAudioToggle` lên `[Audio]` · xoá instance prefab `AudioController` và `MusicController` khỏi `Service.unity`.

**Kịch bản chơi thử (developer):** vào level 1 → tap 3 item cùng loại → nghe tiếng tap mỗi lần, tiếng nhận box
khi item vào box, tiếng combo khác nhau ở box thứ 2–6 liên tiếp · mở Setting, tắt Sound giữa lúc đang có tiếng →
im ngay, bật lại → tiếng kế tiếp phát · dùng booster Tray, Magnet, Cart → mỗi cái một tiếng · để thua → tiếng thua · nhạc nền loop khi vào level; mở Setting tắt Music → nhạc dừng ngay, SFX vẫn kêu; bật lại → nhạc phát lại; tắt Sound → nhạc vẫn phát · đổi sang level/màn khác gọi cùng bài → nhạc không giật về đầu.
Dấu hiệu hỏng: nhạc bị cắt khi nhiều SFX dồn, nhạc không phát lại sau khi bật Music, console đỏ `[AudioService]:` hoặc `[AudioCatalog]:`, tiếng phát trễ hơn hiệu ứng hình, tiếng chói khi nhiều box hoàn thành cùng lúc.

---

### Task 5: Test · SystemPlan · tài liệu module (agent)

- [ ] Test EditMode trong `Assets/Horcrux/Tests/EditMode/AudioServiceTests.cs` và `AudioCatalogTests.cs` theo hai bảng case ở Task 2 và 3; dựng host bằng `new GameObject` + `AddComponent<TestAudioService>` (hai enum và `TestAudioCatalog` của Task 2, Step 6; `TestAudioService` của Task 3), bắt log bằng `LogAssert.Expect` với regex `^\[AudioService\]: `.
- [ ] Cập nhật dòng §8 Audio trong `Assets/Horcrux/SystemPlan.md`: `Foundation`, 8 file, generic theo hai enum của dự án, API `IAudioSetting` 2 member + `IAudioService<,>` 3 member (SFX + Music tối thiểu), không `pitchScale`; cột "ngoài plan" giữ `PlaySfxAt`, crossfade, pause/resume nhạc, `PlayDynamic`, mixer group, thêm `pitchScale`.
- [ ] Viết `AudioSystem.md` tài liệu module (thay file plan này sau khi code xong) với mục **Trước khi chạy** lấy từ Editor setup của Task 2 và 3; rồi `.html`.

---

## Cheat

Không có. Mọi tiếng và nhạc đều tới được bằng đường người chơi trong vài giây chơi; bật tắt Sound và Music đã có trong Setting.
