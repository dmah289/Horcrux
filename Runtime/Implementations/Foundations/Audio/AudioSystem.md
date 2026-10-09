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
└── IAudioService.cs        IAudioService<TSfx,TMusic> : IAudioSetting — PlaySfx(TSfx) · PlaySfx(TSfx, float pitchScale) · PlayMusic(TMusic) · StopMusic

Implementations/Foundations/Audio/
├── AudioEntry.cs           AudioEntry<TSfx>: một tiếng SFX: id · clip · volume · minInterval  (class [Serializable])
├── MusicTrack.cs           MusicTrack<TMusic>: một bài nhạc: id · clip · volume  (class [Serializable])
├── AudioCatalog.cs         AudioCatalog<TSfx,TMusic> abstract ScriptableObject chứa entry[] + track[] — game điền, Horcrux không biết clip nào
├── AudioCatalog.Editor.cs  nút Validate (Odin [Button])
├── AudioService.cs         AudioService<TSfx,TMusic> abstract host MonoBehaviour: SFX (tra entry → throttle → cao độ → cấp voice) + Music (một source riêng)
└── AudioService.Editor.cs  nút Setup Audio Sources (dựng voice + music source) · nút Configure All Sources ép cờ · nút Validate References

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
| **Ai gọi** | Gameplay của dự án, qua `IAudioService<GameSfx, GameMusic>` tiêm thẳng vào từng nơi gọi, không static, không lớp nối (Task 4). Ở Category Jam: 20 call site hiện gọi `AudioController.Instance.PlaySoundEffect(string)` không kèm vị trí, 16 tiếng, trong đó 5 tiếng combo `sfx_box_combo2..6` là 5 clip riêng — gộp thành **một** entry `BoxCombo`, cao độ tăng dần do call site truyền `pitchScale` (§0.5), catalog còn 12 entry. Music: `MusicController` của `_Gameplay` không còn caller nào ngoài chính nó — bỏ hẳn; nhạc khởi bằng `PlayMusic` ở nơi vào level, và toggle Music trong Setting (`IAudioPersistentData.RegisterOnMusicChange`). |
| **Mục tiêu** | Tiếng gameplay phát đúng frame sự kiện. Nghiệm thu bằng chơi thử: tap item, nhận box, hoàn thành box 3 lần liên tiếp, dùng 3 booster — mỗi hành động có đúng một tiếng, không chói khi nhiều box hoàn thành cùng lúc, tắt Sound trong Setting thì im ngay. Vào level có nhạc nền loop; tắt Music thì nhạc dừng, bật lại thì nhạc phát lại; tắt Music không ảnh hưởng SFX và ngược lại. |
| **Ngân sách** | Nhịp *mỗi tương tác*; cao điểm ~10–20 lần/giây có thể cùng frame khi cascade. **Hot path đã xác nhận**: mọi cấp phát dồn về `Awake`, đường `PlaySfx` không `new`, không LINQ, không boxing. |
| **Ranh giới** | Service chỉ lo: tra entry · throttle · cấp voice · áp tham số · giữ một bài nhạc đang phát. Danh mục clip và danh sách bài nhạc là **asset của game**. Hai cờ `IsSfxOn` / `IsMusicOn` là **property** game set từ save của nó — service không biết hệ save. **Foundation**: không gọi hệ Horcrux nào khác. |
| **Chỗ đặt** | Horcrux. Không type nào mang domain dự án. Lớp nối (enum id, asset catalog, cầu nối setting) nằm trong dự án. |
| **Tên** | `Giả định (cần xác nhận):` `AudioCatalog` · `AudioEntry` · `AudioService` · `IAudioSetting` · `IsSfxOn` · `PlaySfx`. Đã chốt: `MusicTrack` · `IsMusicOn` · `PlayMusic` · `StopMusic` · id là enum của dự án, hệ generic theo hai enum, dự án kế thừa. Đổi tên trước khi gõ, không đổi sau. |
| **Cố ý KHÔNG làm + lý do** | ① **`volumeScale` trên `PlaySfx`** — không caller. Thêm sau là một overload. Pitch có cửa riêng `PlaySfx(sfx, pitchScale)` vì combo là caller thật (§0.5); công thức và mức kẹp nằm ở call site, entry không biết. ② **Interface cho catalog và entry** — chỉ có một implementation; tách khi có nguồn dữ liệu thứ hai (remote, procedural). ③ **SFX 3D** (`PlaySfxAt`, `spatialBlend`) — mọi call site hiện tại là 2D. ④ **Crossfade, fade, pause/resume nhạc, `PlayDynamic` (nhạc đổi theo trạng thái), mixer group** — chưa chốt cần; mỗi cái là thêm method hoặc field. Music bản tối thiểu **có** trong plan. ⑤ **Nhiều clip biến thể cho một tiếng** (chọn ngẫu nhiên, tuần tự, theo trọng số) — mỗi entry đúng **một** clip; phát triển sau. Khi thêm, đổi `Clip` thành mảng và throttle sang khoá theo clip (§0.3). ⑥ **Lưu bền `IsSfxOn`** — game đã có save riêng. ⑦ **Kiểu id riêng của Horcrux bọc `int`** — framework không sở hữu từ vựng của dự án: struct bọc `int` không chặn số bừa và không cho Inspector biết tên; enum của dự án làm được cả hai. ⑧ **Gộp `IAudioSetting` vào interface generic** — consumer chỉ cần hai cờ (cầu nối Setting, composite Horcrux) sẽ phải gọi tên enum của dự án. |
| **Quyết định trái trực giác** | Voice là **mảng `AudioSource` kéo tay** thay vì pool tạo lúc chạy — số voice là hằng cấu hình trong một asset, thiếu thì lộ ô trống. Throttle khoá theo **clip** chứ không theo id — xem §0.3. Hết voice thì **cướp voice sắp xong** thay vì bỏ tiếng mới — tiếng mới là phản hồi người chơi vừa gây ra. **Không object pool runtime**: mảng voice đã là pool cố định (tạo sẵn lúc authoring, cho mượn–cướp–trả, không `Instantiate`/`Destroy` ở đường phát); `ObjectPool<AudioSource>` thêm cấp phát khi lớn lên và giấu số voice khỏi Inspector. **Hệ generic theo hai enum, `abstract` + class con rỗng của dự án** thay cho một service không generic nhận số: hai enum cho compile-time phân biệt SFX và music, Inspector vẽ dropdown tên, và dự án không cast hay gõ số — đổi lại mỗi dự án khai hai class con và gắn `[Service]` lên class con (`Inherited = false`). **Music có `AudioSource` riêng, ngoài mảng voice** — SFX cướp voice không bao giờ cắt nhạc. Tắt Music rồi bật lại thì bài đang chọn phát lại từ đầu: `PlayMusic` lúc tắt chỉ ghi nhớ bài, không phát. |
| **Mở rộng sau** (đều additive) | `PlaySfxAt` · crossfade (PrimeTween) · `PauseMusic`/`ResumeMusic` · `AudioMixerGroup` trên service, `ConfigureAllSources` gán cho mọi voice · `IAudioCatalog` khi có nguồn thứ hai · nạp catalog qua `AssetReference` · hàm đổi enum → `int` ở `Utilities/` nếu Profiler cho thấy `Dictionary` khoá enum cấp phát (Task 3, case #19). |

### Đã khảo sát trước khi viết

| Nguồn | Lấy | Không lấy, vì |
|---|---|---|
| `Kelsey.IAudioService` + `SoundController` (contract ScrewDom, giữ nguyên cho LiveOps) | Cầu nối setting: `IAudioPersistentData.RegisterOnSoundChange` → cờ của service (Task 4) | 30 method, mỗi tiếng một method, 25 method rỗng — catalog + id thay cho việc đó. Phụ thuộc Feel. |
| `MusicController` + `MusicContainer` của `_Gameplay` | Hành vi cần giữ: phát nhạc nền, tắt theo Setting (không còn `Stop`/`Play(name)` vì 0 caller) | Kế thừa `BaseAudioController`, khoá chuỗi, `PlayDynamic` + filter + mixer — chưa có nhu cầu chốt; gỡ ở Task 4. |
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
Cần tắt hoặc chỉnh cho từng tiếng khi có ca thật ⇒ thêm field vào `AudioEntry` lúc đó. Cửa `pitchScale` (§0.5) là
ca thật đầu tiên, và nó **không** dùng jitter.

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
| `comboSemitonesPerStep` | 2 | `[SerializeField]` trên `Box` (call site) |
| `comboMaxStep` | 4 | `[SerializeField]` trên `Box` (call site) |
| Số voice | 8 | mảng `Voices` trên host |

Cách tune: sửa số trong asset, nhấn Play. Không sửa code (trừ `PitchSpread`, hằng một dòng).

### 0.5. Cao độ do call site truyền

`PlaySfx(sfx, pitchScale)` phát với `pitch = pitchScale` **đúng nguyên giá trị**: không jitter (±5% ≈ 0,85 nửa
cung, xoá mất bậc 1 nửa cung), không kẹp. Hợp đồng: `pitchScale > 0`; `0` là giá trị cửa không-pitch dùng để
chọn jitter nên không dùng được làm pitch. `1` là tiếng gốc.

Combo là caller duy nhất: bậc `step = comboNumber − 2 ≥ 0` (combo 2 là tiếng gốc), mỗi bậc cao thêm một số nửa cung
— một nửa cung là nhân tần số với cùng hệ số $2^{1/12}$, nên các bậc nghe cách đều:

$$\text{pitchScale} = 2^{\,\min(\text{step},\ \text{comboMaxStep}) \cdot \text{comboSemitonesPerStep} / 12}$$

Mốc kiểm (`2`, `4`): bậc 0 → 1,0000 · bậc 1 → 1,1225 · bậc 2 → 1,2599 · bậc 4 → 1,5874 · bậc 9 → 1,5874 (kẹp).
Pitch cao nhất 1,5874 làm clip ngắn còn 63%. Hai số chọn bằng tai, không dẫn từ đâu.

Throttle vẫn khoá theo entry: hai lần combo trong `minInterval` bị bỏ tiếng sau — chấp nhận, vì hai nốt khác cao
độ phát cùng lúc nghe lệch tông.

---

## Luồng dữ liệu

```
game: _audio.PlaySfx(GameSfx.BoxReceive)         (_audio: IAudioService<GameSfx, GameMusic>, tiêm vào nơi gọi — Task 4)
  └─> GameAudioService.PlaySfx(GameSfx.BoxReceive)
        │
        ├─ !IsSfxOn ──────────────────────────────────────────> return (im lặng, hợp lệ)
        ├─ catalog.TryGetEntryById(sfx) ─ thiếu ──────────────────────> LogError, return
        ├─ now − _lastPlayTimeById[sfx] < minInterval ────────> return (throttle, hợp lệ)
        ├─ pitch = Random(1 − s, 1 + s)             cửa PlaySfx(sfx, pitchScale): pitch = pitchScale, không jitter
        ├─ voice = RentVoice(now)                              rảnh đầu tiên, hết thì cướp sắp xong
        └─ voice.clip/volume/pitch ← entry ; Play()
           _voicesBusyUntil[voice] = now + clip.length / pitch
           _lastPlayTimeById[sfx] = now
```

`now = Time.unscaledTime`: popup pause đặt `timeScale = 0`, throttle và mốc rảnh phải tiếp tục đúng.

```
game: GameAudio.PlayMusic(GameMusic.Level)
  └─> IAudioService<GameSfx, GameMusic>.Service.PlayMusic(GameMusic.Level)
        ├─ catalog.TryGetMusicById(music) ─ thiếu ────────────> LogError, return
        ├─ _currMusicTrack = track                    nhớ bài đã chọn, kể cả khi Music đang tắt
        └─ ApplyMusicState()
              ├─ !IsMusicOn hoặc chưa chọn bài ─────> musicSource.Stop()
              ├─ đang phát đúng clip đó ────────────> không làm gì (gọi lại cùng bài không restart)
              └─ else ──────────────────────────────> volume/clip ← track ; Play()   (loop do Setup / Configure All Sources ép)

IsMusicOn setter, StopMusic ─> cùng ApplyMusicState (StopMusic đặt _currMusicTrack = null trước)
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

- [ ] **Step 2: thay toàn bộ `IAudioService.cs`** (chép nguyên file; có overload `PlaySfx(TSfx, float pitchScale)`, §0.5)

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

        /// <summary>Plays <paramref name="sfx"/> at exactly <paramref name="pitchScale"/>. No random pitch.</summary>
        /// <param name="pitchScale">Playback speed ratio, must be above 0. 1 is the original pitch.</param>
        void PlaySfx(TSfx sfx, float pitchScale);

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
- Produces: `AudioEntry<TSfx>` (5 field authoring) · `MusicTrack<TMusic>` (4 field) · `AudioCatalog<TSfx, TMusic> : ScriptableObject` abstract (`Entries`, `Tracks`, `BuildTables`, `TryGetEntryById`, `TryGetMusicById`).

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
| Catalog giữ dữ liệu **và bảng tra id → entry / track** (`BuildTables`, `TryGetEntryById`, `TryGetMusicById`) | Bảng tra là hàm thuần của hai mảng serialize, cùng chủ với dữ liệu nguồn nên không ai khác dựng lại. Field không serialize trong SO sống qua các lần Play khi tắt domain reload ⇒ `BuildTables` luôn `Clear` rồi dựng lại, service gọi ở `Awake`. **State đổi lúc chạy** (mốc throttle, voice, bài đang chọn) vẫn ở service: không con trỏ chọn clip, không mốc thời gian trong SO |
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

Thêm hai field bảng tra, `BuildTables` và hai hàm tra. `using System.Collections.Generic;` đã có ở dòng 2. Theo thứ tự region: field cạnh hai field serialize, `Properties` giữ `Entries`/`Tracks`, `API` giữ ba hàm:

```csharp
private readonly Dictionary<TSfx, AudioEntry<TSfx>> _entryById = new();
private readonly Dictionary<TMusic, MusicTrack<TMusic>> _musicById = new();

#region API

public bool TryGetEntryById(TSfx id, out AudioEntry<TSfx> entry)
    => _entryById.TryGetValue(id, out entry);

public bool TryGetMusicById(TMusic id, out MusicTrack<TMusic> music)
    => _musicById.TryGetValue(id, out music);

public void BuildTables()
{
    _entryById.Clear();
    _musicById.Clear();

    for (int i = 0; i < entries.Length; i++)
        _entryById.Add(entries[i].Id, entries[i]);

    for (int i = 0; i < tracks.Length; i++)
        _musicById.Add(tracks[i].Id, tracks[i]);
}

#endregion
```

| Quyết định | Vì sao |
|---|---|
| Bảng tra id → entry / track nằm ở catalog, không ở service | Trả thẳng đối tượng, không trả chỉ số: không caller nào cần chỉ số (throttle khoá theo id, bài đang chọn giữ tham chiếu). Bảng là hàm thuần của hai mảng serialize, cùng chủ với dữ liệu nguồn. State đổi lúc chạy (mốc throttle, voice, bài đang chọn) vẫn ở service |
| `BuildTables` luôn `Clear` rồi dựng lại, service gọi ở `Awake` | Field không serialize trong SO sống qua các lần Play khi tắt domain reload; không `Clear` thì bảng cũ còn sót |
| Trùng id: `Add`, ném `ArgumentException` ở `BuildTables` | Trùng là lỗi authoring nổ ngay lần Play đầu: để nó nổ thay vì cái đầu thắng im lặng. Nút Validate báo sớm hơn từ lúc authoring |

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
| `BuildTables()` với entry id `1, 2` rồi `TryGetEntryById(2)` | `true`, đúng đối tượng entry thứ hai |
| `TryGetEntryById` / `TryGetMusicById` với id không có trong catalog | `false` |
| Hai entry cùng id, `BuildTables()` | ném `ArgumentException` |
| `BuildTables()` hai lần, giữa hai lần đổi mảng entries | bảng khớp mảng mới, không còn id cũ |
| Entry SFX id `1` và track id `1` | hai bảng độc lập, mỗi bên tra ra đối tượng riêng |

- [ ] **Step 8: Commit** — `feat(sdk): audio catalog generic over the game's id enums`

---

### Task 3: `AudioService<TSfx, TMusic>`

**Files:** `Assets/Horcrux/Runtime/Implementations/Foundations/Audio/AudioService.cs` · `AudioService.Editor.cs`

**Interfaces:**
- Consumes: `AudioCatalog<TSfx, TMusic>`, `AudioEntry<TSfx>`, `MusicTrack<TMusic>` (cùng hệ, nhận class cụ thể qua `[SerializeField]`).
- Produces: `AudioService<TSfx, TMusic> : MonoBehaviour, IAudioService<TSfx, TMusic>` — abstract; dự án khai class con và gắn `[Service]` lên đó.

**Bản đồ §0 → code:**

| §0 | Code |
|---|---|
| §0.1 mốc rảnh | `_voicesBusyUntil[voice] = now + clip.length / pitch` |
| §0.2 pitch ngẫu nhiên | `Random.Range(1f - PitchSpread, 1f + PitchSpread)` — hằng, không clamp |
| §0.5 pitch do call site | `pitchScale` dùng nguyên, thay dòng rút pitch ngẫu nhiên, không jitter |
| §0.3 throttle biên đóng | `now - last < minInterval → return`; `last` khởi tạo `float.NegativeInfinity` để lần đầu luôn qua |

**Quyết định thiết kế:**

| Quyết định | Vì sao |
|---|---|
| `abstract` + dự án khai class con rỗng, `[Service]` gắn ở class con | Generic `MonoBehaviour` không add vào GameObject được; class con chốt hai enum. `[Service]` khai `Inherited = false` nên khai ở base **không** tới class con — đây là bước setup, ghi ở "Trước khi chạy" |
| `Awake` và `ConfigureAllSources` là `protected virtual` | Class con của dự án đặt trùng tên sẽ che hẳn bản của base: bảng tra không được dựng, mốc throttle không được điền, cờ voice không được ép — im lặng |
| Bảng tra `Dictionary<TSfx, AudioEntry<TSfx>>` / `Dictionary<TMusic, MusicTrack<TMusic>>` (trong catalog) khoá thẳng bằng enum | Không đổi sang `int`, không phụ thuộc underlying type. Chỉ không boxing khi comparer mặc định của enum là bản generic (đúng ở Unity 6) — case #19 là phép kiểm; không đạt thì thêm hàm đổi enum → `int` ở `Utilities/` và đổi khoá bảng |
| Entry chưa gán id (`default`) vẫn vào bảng tra như một khoá | Chỉ lộ khi caller truyền `default`; nút Validate (Task 2) báo từ lúc authoring. Một nhánh canh ở runtime là code cho ca đã có chỗ bắt |
| Voice = mảng `AudioSource` kéo tay, mỗi source một GameObject con (dựng bằng `SetupAudioSources`), dùng thẳng, không dựng bản sao lọc null | Số voice là hằng cấu hình trong một asset ⇒ kéo thả. Ô trống là lỗi setup, **nổ trễ**: `RentVoice` chỉ chạm ô đó khi các voice trước đã bận, tức đúng lúc combo dồn — nên bắt ở authoring bằng nút `ValidateReferences` (chạy khi được hỏi, không `OnValidate`, vì lúc đang kéo thả mảng luôn chưa đủ). Không `PlayOneShot` trên một source vì mọi tiếng sẽ chung một `pitch` |
| Hai cửa `PlaySfx(sfx)` và `PlaySfx(sfx, pitchScale)` cùng đi vào một thân `Play`; `RandomPitch = 0f` chọn nguồn pitch | Throttle, rent voice, mốc rảnh chỉ có **một bản**; hai cửa chỉ khác nguồn pitch (một dòng). `0` không phải pitch hợp lệ (`clip.length / 0` làm voice kẹt vĩnh viễn) nên dùng làm dấu "không truyền" mà không thêm cờ `bool` hay nhân thân hàm ra hai bản |
| Mọi cấp phát dồn về `Awake` | `catalog.BuildTables()` · `_lastPlayTimeById` · `_voicesBusyUntil`. Sau `Awake`, `PlaySfx` không cấp phát |
| Throttle lưu `Dictionary<TSfx, float>` khoá theo id, dựng đủ khoá ở `Awake` | Cùng khoá với bảng tra nên không cần chỉ số. Ghi đè giá trị của khoá có sẵn không cấp phát; **phải điền đủ mọi id ở `Awake`** (giá trị đầu `NegativeInfinity`) vì thêm khoá mới lúc chạy mới cấp phát. Mỗi tiếng tốn 3 lần băm (tra entry, đọc mốc, ghi mốc) thay vì 1 (tra chỉ số rồi truy cập mảng) — nhịp mỗi tương tác, ~20 lần/giây, không đáng. Cùng một clip ở hai entry thì throttle riêng — chấp nhận, không có ca thật |
| Rảnh đầu tiên, hết thì cướp voice có `busyUntil` nhỏ nhất | Bỏ tiếng mới là bỏ phản hồi người chơi vừa gây ra; voice sắp xong bị cắt ít gây chú ý nhất |
| Kiểm cả `busyUntil` và `isPlaying` | Mốc là dự tính (§0.1); `isPlaying` là sự thật của engine |
| `Time.unscaledTime` | Popup pause đặt `timeScale = 0`; `Time.time` đứng yên ⇒ throttle đóng băng, mọi tiếng sau bị bỏ |
| Giá trị enum lạ ⇒ `LogError` mỗi lần, không throw | Id không có trong catalog là lỗi setup **không nổ** (tra thiếu chỉ trả `false`) ⇒ phải báo, kể cả lặp. Chuỗi nội suy chứa enum boxing, nhưng chỉ ở đường lỗi |
| Catalog, `musicSource`, mảng voice rỗng, `clip` null **không có guard runtime** | Mỗi cái nổ thật ở lần chạm đầu (NRE hoặc `IndexOutOfRange`) và nút Validate báo từ lúc authoring (`ValidateReferences` cho service, `ValidateEntries` cho catalog). Guard ở đây là phí vĩnh viễn trong build cho một lần đọc log dễ hơn |
| Bị throttle, `IsSfxOn = false` ⇒ im lặng | Ca hợp lệ, không phải lỗi |
| `StopAllVoices` thoát khi `_voicesBusyUntil` còn rỗng | Setter `IsSfxOn` có thể chạy trước `Awake` (cầu nối Setting ghi `false` từ save sớm hơn host). Trước `Awake` mảng mốc chưa cấp mà `voices` đã có từ Inspector; chưa có voice nào phát nên thoát là đúng. Case #34 |
| `IsSfxOn = false` dừng voice, **không** xoá mốc throttle | Mốc là `unscaledTime` tăng đều; chỉ chặn trong `minInterval` ≈ 0.05s sau khi bật lại — không có ca "bỏ oan" |
| Service **không** gọi validate của catalog | Validate là nút Editor (Task 2). Dữ liệu sai lộ ra ở đường phát, không bằng một lượt quét ở boot |
| Music: một `musicSource` riêng, một hàm `ApplyMusicState` | `IsMusicOn`, `PlayMusic`, `StopMusic` chỉ đổi trạng thái (cờ, bài đang chọn) rồi gọi một hàm làm source khớp trạng thái ⇒ một cửa ghi lên source, không ba nơi tự `Play`/`Stop` lệch nhau (§3.4) |
| `PlayMusic` lúc `IsMusicOn = false` chỉ nhớ bài | Bật lại thì đúng bài game đã chọn phát; game không phải gọi lại `PlayMusic` sau mỗi lần toggle |
| Gọi lại đúng bài đang phát không restart; `volume` chỉ gán khi đổi bài | So `clip`; level mới gọi `PlayMusic` cùng bài không bị giật nhạc. Volume là hằng của track nên không cần gán lại |
| Giá trị music lạ ⇒ `LogError`, **giữ bài hiện tại** | Sai lúc setup không nổ ⇒ phải báo; cắt nhạc đang chạy vì một giá trị sai là phá thêm |
| Không `DontDestroyOnLoad` | Host là component kéo vào scene; đời sống do scene quyết. Game đặt nó ở scene persistent của nó |

**Editor setup — bước thật, trong dự án và scene của game:**

1. Khai class con trong dự án (Category Jam: `Assets/_Gameplay/Scripts/Common/Audio/GameAudioService.cs`). Thiếu `[Service]`: không ai resolve được `IAudioService<GameSfx, GameMusic>`.

```csharp
[Service(typeof(IAudioService<GameSfx, GameMusic>), typeof(IAudioSetting), FindFromScene = true)]
public sealed class GameAudioService : AudioService<GameSfx, GameMusic> { }
```

   `Giả định (cần xác nhận):` `[Service]` của InitArgs nhận nhiều defining type trong một attribute — kiểm lúc compile.
2. Mở scene dịch vụ persistent của game (Category Jam: `Assets/_Game/Scenes/Service.unity`). Tạo GameObject `audio` → Add Component `GameAudioService`.
3. Kéo asset catalog (Task 2) vào ô **Catalog**. Thiếu: `Awake` ném `NullReferenceException`.
4. Trên component `GameAudioService` bấm nút **Setup Audio Sources**: tạo 8 GameObject con `sfx_voice_0..7`, mỗi cái một `AudioSource`, điền vào **Voices**; tạo thêm GameObject con `music_source` với một `AudioSource`, điền vào ô **Music Source**. Bấm lại chỉ bù ô còn trống, không tạo trùng. Muốn số voice khác thì thêm ô vào mảng rồi bấm lại. Mọi source được đặt `Play On Awake` tắt, `Spatial Blend = 0`, `Loop` tắt (riêng music `Loop` bật). Thiếu voice: mỗi `PlaySfx` ném `IndexOutOfRangeException`; ô `None` chỉ nổ khi các voice trước đã bận (combo dồn). Thiếu `Loop` ở music: nhạc phát một lần rồi im; `PlayMusic` ném `NullReferenceException` khi thiếu ô **Music Source**.
5. Sau khi tự kéo hoặc đổi cờ một source bằng tay, bấm **Configure All Sources** để ép lại bốn cờ trên; nút không tạo gì mới.
6. Bấm nút **Validate References** trên component. Console phải có **một dòng log** xác nhận, không error — thiếu catalog, music source, voice hoặc có ô `None` đều báo ở đây. Save scene.

- [ ] **Step 1: `AudioService.cs`**

```csharp
using System;
using System.Collections.Generic;
using Horcrux.Runtime.Abstractions.Audio;
using Horcrux.Runtime.Utilities;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Horcrux.Runtime.Implementations.Audio
{
    public abstract partial class AudioService<TSfx, TMusic> : MonoBehaviour, IAudioService<TSfx, TMusic>
        where TSfx : struct, Enum
        where TMusic : struct, Enum
    {
        private const float PitchSpread = 0.05f;
        private const float RandomPitch = 0f;
        
        [Splitter("References")]
        [SerializeField] private AudioSource[] voices = Array.Empty<AudioSource>();
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioCatalog<TSfx, TMusic> catalog;
        
        private bool _isMusicOn = true;
        private bool _isSfxOn = true;
        private MusicTrack<TMusic> _currMusicTrack;
        
        private readonly Dictionary<TSfx, float> _lastPlayTimeById = new();
        private float[] _voicesBusyUntil = Array.Empty<float>();
        
        
        #region Properties

        public bool IsSfxOn
        {
            get => _isSfxOn;
            set
            {
                _isSfxOn = value;

                if (!_isSfxOn)
                    StopAllVoices();
            }
        }

        public bool IsMusicOn
        {
            get =>  _isMusicOn;
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
            catalog.BuildTables();
            
            _lastPlayTimeById.Clear();
            for (int i = 0; i < catalog.Entries.Count; i++)
                _lastPlayTimeById[catalog.Entries[i].Id] = float.NegativeInfinity;
            
            _voicesBusyUntil = new float[voices.Length];
        }

        #endregion

        #region API
        
        public void PlaySfx(TSfx sfx) => Play(sfx, RandomPitch);

        public void PlaySfx(TSfx sfx, float pitchScale) => Play(sfx, pitchScale);

        public void PlayMusic(TMusic music)
        {
            if(!catalog.TryGetMusicById(music, out MusicTrack<TMusic> track))
            {
                Debug.LogError($"[AudioService]: Music {music} is not in the catalog.", this);
                return;
            }
            
            _currMusicTrack = track;
            ApplyMusicState();
        }

        public void StopMusic()
        {
            _currMusicTrack = null;
            ApplyMusicState();
        }
        
        #endregion

        #region Class Methods

        private void Play(TSfx sfx, float pitchScale)
        {
            if (!_isSfxOn)
                return;

            if (!catalog.TryGetEntryById(sfx, out AudioEntry<TSfx> entry))
            {
                Debug.LogError($"[AudioService]: Sfx {sfx} is not in the catalog.", this);
                return;
            }
            
            float now = UnityEngine.Time.unscaledTime;
            if (now - _lastPlayTimeById[sfx] < entry.MinIntervalSeconds)
                return;
            
            AudioClip clip = entry.Clip;
            float pitch = pitchScale == RandomPitch
                ? Random.Range(1f - PitchSpread, 1 + PitchSpread)
                : pitchScale;
            int voiceIndex = RentVoice(now);
            AudioSource voice = voices[voiceIndex];
            
            voice.clip = clip;
            voice.pitch = pitch;
            voice.volume = entry.Volume;
            voice.Play();

            _voicesBusyUntil[voiceIndex] = now + clip.length / pitch;
            _lastPlayTimeById[sfx] = now;
        }

        private void StopAllVoices()
        {
            if (voices == null || voices.Length == 0 || _voicesBusyUntil == null || _voicesBusyUntil.Length == 0)
                return;
            
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i].Stop();
                _voicesBusyUntil[i] = 0f;
            }
        }

        private void ApplyMusicState()
        {
            if (!_isMusicOn || _currMusicTrack == null)
            {
                musicSource.Stop();
                return;
            }

            if (musicSource.isPlaying && musicSource.clip == _currMusicTrack.Clip)
                return;
            
            musicSource.volume = _currMusicTrack.Volume;
            musicSource.clip = _currMusicTrack.Clip;
            musicSource.Play();
        }

        private int RentVoice(float now)
        {
            int soonestIndex = 0;
            float soonestBusyUntil = float.MaxValue;

            for (int i = 0; i < voices.Length; i++)
            {
                if (now >= _voicesBusyUntil[i] && !voices[i].isPlaying)
                    return i;

                if (_voicesBusyUntil[i] < soonestBusyUntil)
                {
                    soonestBusyUntil = _voicesBusyUntil[i];
                    soonestIndex = i;
                }
            }

            return soonestIndex;
        }

        #endregion
    }
}
```

- [ ] **Step 2: `AudioService.Editor.cs`**

```csharp
using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Audio
{
    public abstract partial class AudioService<TSfx, TMusic>
        where TSfx : struct, Enum
        where TMusic : struct, Enum
    {
        #if UNITY_EDITOR

        private const int DefaultVoiceAmount = 8;

        [Button]
        protected virtual void ConfigureAllSources()
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

        [Button]
        private void ValidateReferences()
        {
            int invalidAmount = 0;

            if (catalog == null)
            {
                Debug.LogError("[AudioService]: Catalog is not assigned.", this);
                invalidAmount++;
            }

            if (musicSource == null)
            {
                Debug.LogError("[AudioService]: Music Source is not assigned.", this);
                invalidAmount++;
            }

            if (voices.Length == 0)
            {
                Debug.LogError("[AudioService]: No Sfx voice assigned — use 'Setup Audio Sources' on the component.", this);
                invalidAmount++;
            }

            for (int i = 0; i < voices.Length; i++)
            {
                if (voices[i] == null)
                {
                    Debug.LogError($"[AudioService]: Sfx Voices[{i}] is empty.", this);
                    invalidAmount++;
                }
            }

            if (invalidAmount == 0)
                Debug.Log($"[AudioService]: Catalog, Music Source and {voices.Length} Sfx voices assigned.", this);
        }

        [Button]
        private void SetupAudioSources()
        {
            UnityEditor.Undo.RecordObject(this, "Setup Audio Sources");

            if (voices.Length < DefaultVoiceAmount)
                Array.Resize(ref voices, DefaultVoiceAmount);

            for (int i = 0; i < voices.Length; i++)
            {
                if (voices[i] == null)
                    voices[i] = CreateAudioSource($"sfx_voice_{i}");
            }

            if (musicSource == null)
                musicSource = CreateAudioSource("music_source");

            ConfigureAllSources();
            UnityEditor.EditorUtility.SetDirty(this);
        }

        private AudioSource CreateAudioSource(string objectName)
        {
            var child = new GameObject(objectName);
            UnityEditor.Undo.RegisterCreatedObjectUndo(child, "Setup Audio Sources");
            child.transform.SetParent(transform, false);

            return child.AddComponent<AudioSource>();
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
| 3 | Chưa gán catalog | `Awake` ném `NullReferenceException`; `ValidateReferences` báo 1 error |
| 4 | Mảng `Voices` rỗng | `PlaySfx` ném `IndexOutOfRangeException`; `ValidateReferences` báo 1 error |
| 5 | `Voices` có 1 ô `None` ở chỉ số 3 trong 5 | `ValidateReferences` báo 1 error nêu `Voices[3]`; 3 lần phát đồng thời đầu bình thường, lần thứ 4 ném `NullReferenceException` |
| 6 | `PlaySfx(sfx hợp lệ)` | voice được chọn có `clip == entry.Clip`, `volume == entry.Volume` |
| 7 | Entry có `clip = null` | `PlaySfx` ném `NullReferenceException`; nút Validate của catalog báo 1 error |
| 8 | Hai entry khác id, cùng frame, `minInterval = 0.05` | cả hai phát (throttle theo entry) |
| 9 | 100 lần phát, `minInterval = 0`, mỗi lần cách nhau 1 giây | luôn dùng đúng clip đó, không treo |
| 10 | 20 lần cùng id cùng frame, `minInterval = 0.05` | phát **1** lần |
| 11 | 20 lần cùng id cùng frame, `minInterval = 0` | phát 8 lần rồi cướp voice; `busyUntil` của voice bị cướp là nhỏ nhất trước đó |
| 12 | 2 lần cách đúng `minInterval` | cả hai phát (biên đóng) |
| 13 | `timeScale = 0`, 2 lần cách 0.1s thật | lần hai phát |
| 14 | 1000 lần phát | mọi `voice.pitch` trong `[0.95, 1.05]`, không phải lúc nào cũng bằng 1 |
| 15 | Clip 0.4s (đọc lại `voice.pitch` sau khi phát) | `busyUntil − now == 0.4 / voice.pitch` |
| 16 | 20 lần phát cùng entry (`minInterval = 0`) | không phải cả 20 `voice.pitch` bằng nhau |
| 17 | `IsSfxOn = false` khi đang phát | mọi voice `isPlaying == false` ngay |
| 18 | Lần phát đầu tiên sau `Awake` | không bị throttle (`NegativeInfinity`) |
| 19 | Profiler: 20 lần phát/giây, 10 giây, bản build IL2CPP | **0 B** GC Alloc trên đường `PlaySfx` (khoá `TestSfx : int`), và trên `PlayMusic` lặp cùng nhịp (khoá `TestMusic : byte`) — cũng là phép kiểm cho việc khoá bảng tra bằng enum. Tiền kiểm chỉ riêng `Dictionary` khoá enum (chưa cần `AudioService`): `Tests/PlayMode/EnumKeyedLookupAllocationTests.cs`, chạy trên player IL2CPP |
| 20 | Bật `Play On Awake` trên một voice rồi bấm `ConfigureAllSources` | `playOnAwake == false` |
| 36 | Bấm `SetupAudioSources` hai lần liên tiếp | 8 voice + 1 music source, không tạo thêm GameObject con ở lần hai; `musicSource.loop == true`, mọi voice `loop == false` |
| 21 | `PlayMusic(music)` khi `IsMusicOn = true` | `musicSource.isPlaying`, `clip` và `volume` đúng track, `loop == true` |
| 22 | `PlayMusic((TestMusic)99)` khi đang phát bài A | 1 error `[AudioService]:`, bài A vẫn phát |
| 23 | `PlayMusic(A)` rồi `PlayMusic(B)` | `clip == B` |
| 24 | `PlayMusic(A)` hai lần liên tiếp | lần hai không restart: `time` không về 0 |
| 25 | `IsMusicOn = false` khi đang phát | `isPlaying == false` ngay; `IsSfxOn` và voice SFX không đổi |
| 26 | `IsMusicOn = false`, `PlayMusic(A)`, rồi `IsMusicOn = true` | sau `PlayMusic` chưa phát; sau bật phát bài A |
| 27 | `PlayMusic(A)`, `StopMusic()`, bật tắt `IsMusicOn` | không phát (bài đã bị bỏ chọn) |
| 28 | `IsSfxOn = false` khi nhạc đang phát | nhạc vẫn phát |
| 29 | 20 lần `PlaySfx` liên tiếp hết voice, nhạc đang phát | nhạc không bị cắt (`musicSource` không nằm trong `voices`) |
| 30 | Chưa gán `musicSource`, gọi `PlayMusic` | ném `NullReferenceException`; `ValidateReferences` báo 1 error |
| 31 | `ValidateReferences` với đủ catalog, music source, 8 voice | đúng 1 log, 0 error |
| 32 | `PlaySfx` rồi đọc qua `IAudioSetting` | `IsSfxOn` và `IsMusicOn` đọc qua interface thấy đúng giá trị vừa set qua `IAudioService<,>` (cùng một object) |
| 33 | Hai entry trùng id | `Awake` ném `ArgumentException` (từ `BuildTables`) |
| 34 | `IsSfxOn = false` trước `Awake` (host vừa dựng, voice đã gán) | không throw |
| 35 | Một track trùng clip với bài đang phát nhưng khác `volume`, `PlayMusic` hai lần | lần hai không restart; `volume` giữ theo bài đầu |
| 37 | `PlaySfx(sfx, 1f)` | `voice.pitch == 1f` đúng (không jitter) |
| 38 | `PlaySfx(sfx, 1.2599f)` | `voice.pitch == 1.2599f` |
| 39 | 10 lần `PlaySfx(sfx, 1.5f)` (`minInterval = 0`) | mọi `voice.pitch == 1.5f`; `busyUntil − now == clip.length / 1.5f` |

**Test đã viết** (chưa chạy trên Unity): `Tests/PlayMode/AudioServiceTests.cs` (hành vi runtime, kể cả thời gian; host dựng không active, nối field bằng reflection, rồi bật để `Awake` chạy) · `Tests/PlayMode/AudioServiceEditorTests.cs` (ba nút `SetupAudioSources`, `ConfigureAllSources`, `ValidateReferences`, gọi qua reflection, bọc `#if UNITY_EDITOR`). Cả hai nằm ở assembly PlayMode vì một `MonoBehaviour` khai trong assembly chỉ-Editor của EditMode không add vào GameObject được (*Can't add script behaviour … it is an editor script*); `TestSfx` / `TestMusic` / `TestAudioCatalog` / `TestAudioService` cũng ở đó. Test cần source đang phát dùng `Assume.That(AudioCanPlay())`: máy không có thiết bị audio thì ra *inconclusive*, `Control_AudioSourceReportsPlaying` báo lý do.

**Chưa phủ, vì sao:** #12 biên đóng chính xác (cần đồng hồ giả hoặc tiêm thời gian; có `PlaySfx_AfterIntervalElapsed_PlaysAgain` kiểm phía "đã qua khoảng") · #19 0 B GC (Profiler trên IL2CPP, developer chạy) · #9 đổi từ "cách nhau 1 giây" thành 100 lần cùng frame với `minInterval = 0` · #16 gộp vào test pitch (#14) · #11 kiểm voice bị cướp bằng clip của entry thứ hai, không đọc `busyUntil` trước–sau.

- [ ] **Step 4: Commit** — `feat(sdk): add AudioService (authored voices, per-sound throttle, random pitch, pitchScale, music source)`

---

### Task 4: Lớp nối Category Jam (agent viết, sau khi Task 3 compile)

Hai hệ cùng tên `IAudioService` cùng tồn tại có chủ ý: `Kelsey.IAudioService` + `SoundController` là lớp
contract ScrewDom, đóng băng để kéo LiveOps; `IAudioService<GameSfx, GameMusic>` (Horcrux) phục vụ gameplay.
Hai tên khác số tham số kiểu nên cùng `using` không xung đột, không cần alias.

Không có `static class GameAudio`: mỗi nơi phát tiếng **nhận** `IAudioService<GameSfx, GameMusic>` qua InitArgs
(`GameAudioService` đã đăng ký service) và lưu vào field `_audio`.

**Bước 1 — Đổi từng nơi phát tiếng (11 file, 20 call site).**
Làm lần lượt từng file: `Box`, `BoxAnimation`, `BoxesContainer`, `CacheHoles`, `ShoppingCart`, `CacheHole`,
`GoodsItem`, `NormalMovingShelf`, `NormalShelf`, `SingleMovingShelf`, `GameDirector.Events`. Với mỗi file:
- Thêm field `private IAudioService<GameSfx, GameMusic> _audio;`.
- Cách nhận phụ thuộc, theo base của class:
  - Base là `MonoBehaviour` thuần (`BoxAnimation`, `ShoppingCart`): kế thừa `MonoBehaviour<IAudioService<GameSfx, GameMusic>>`, viết `protected override void Init(IAudioService<GameSfx, GameMusic> audio) => _audio = audio;`.
  - Đã có base riêng (`BaseManager`, `DisposableEntity`, `Hole`, `Shelf`): thêm `IInitializable<IAudioService<GameSfx, GameMusic>>`, viết `public void Init(IAudioService<GameSfx, GameMusic> audio) => _audio = audio;`. `Shelf` đã có `Init()` không tham số; chữ ký khác nên không đụng.
- Thay `AudioController.Instance.PlaySoundEffect(Constant.AUDIO_X)` bằng `_audio.PlaySfx(GameSfx.X)`.
- Xoá `using` thừa. Compile sạch rồi sang file kế.

Riêng hai chỗ:
- **Combo** (`Box.cs:170`): một entry `GameSfx.BoxCombo`, cao độ tăng theo bậc combo, tính tại call site (§0.5). Hai số tinh chỉnh là field `[SerializeField]` trên `Box`, mặc định ngay trong khai báo (không cần kéo gì): `[SerializeField, Min(0f)] private float comboSemitonesPerStep = 2f;` và `[SerializeField, Min(0)] private int comboMaxStep = 4;`. Dòng cũ `AudioController.Instance.PlaySoundEffect(Constant.GetComboAudioID(comboNumber > 6 ? 6 : comboNumber))` đổi thành:

  ```csharp
  float pitchScale = Mathf.Pow(2f, Mathf.Min(comboNumber - 2, comboMaxStep) * comboSemitonesPerStep / 12f);
  _audio.PlaySfx(GameSfx.BoxCombo, pitchScale);
  ```

  Vẫn trong `if (comboNumber > 1)`, nên combo 2 là bậc 0, `pitchScale = 1`. Clip gốc của entry lấy từ `sfx_box_combo2`. `Giả định (cần xác nhận):` pitch cao nhất 1,5874 làm clip ngắn còn 63% và mỏng hơn clip combo6 cũ — đổi cảm giác chơi, nghiệm thu bằng chơi thử; không hợp thì chỉnh hai số trên `Box` trong Inspector, không sửa code.
- **Tiếng thua** (`GameDirector.Events:211`): `GameDirector` là class thuần nên không nhận được `_audio`. Xoá 2 dòng phát tiếng ở `OnGameEnded` và phát ở nơi đã nhận `_audio`: `CategoryGameController.OnOutOfMoves`, `_audio.PlaySfx(GameSfx.LoseGame)` ngay dòng đầu. Đây đúng thời điểm tiếng cũ phát (lúc hết nước đi, trước popup hồi sinh). `Giả định (cần xác nhận):` nếu muốn tiếng chỉ kêu khi thua hẳn thì chuyển vào `Lose()`, nhưng đó là đổi hành vi.

**Bước 2 — Khởi nhạc nền, bỏ `MusicController`.**
Grep `MusicController` trong `Assets` không có caller nào ngoài thư mục `Audio/`, nên không có call site nào để đổi.
- `CategoryGameController` đổi sang `MonoBehaviour<IAudioService<GameSfx, GameMusic>>`, gọi `_audio.PlayMusic(GameMusic.Level)` ngay sau `Subscribe()` trong `Start`.
- Bỏ khối `AudioController.Initialize(sfxOn)` (4 dòng, `Service.TryGet`) ở cùng hàm.
- `Stop`/`Pause`/`Continue`/`PlayDynamic` không ai gọi: bỏ theo, không thay thế.

**Bước 3 — Cầu nối Setting.**
Tạo `Assets/_Game/Scripts/Common/GameplayAudioToggle.cs`, `MonoBehaviour<IAudioPersistentData, IAudioSetting>` (không cần biết `GameSfx`):
`Init` gán `IsSfxOn = Sound`, `IsMusicOn = Music`, rồi `RegisterOnSoundChange` + `RegisterOnMusicChange`;
`OnDestroy` unregister cả hai. Đặt component trên GameObject `audio`.

**Bước 4 — Gỡ hệ cũ** (chỉ sau khi mọi call site đã đổi và chơi thử xong).
Xoá: `Assets/_Gameplay/Scripts/Common/Audio/Scripts/` (gồm `MusicController` + `MusicContainer`), `Addressables/Prefabs/Sounds/` (16 prefab, gồm 5 prefab `sfx_box_combo2..6`) và nhóm Addressables SFX, `Constant.AudioID.cs` (gồm `GetComboAudioID`), `GameAudioIds.cs` bỏ `BoxCombo3..6`, `GameplaySession.SfxOn`, instance `AudioController` trong `Service.unity`.
Grep `AudioController`, `MusicController`, `AUDIO_`, `SfxOn` phải trả 0.

**Bước 5 — Ghi contract doc.** `docs/ScrewDom_Contract.md` mục 2, một dòng: `SoundController` đóng băng cho LiveOps; audio gameplay đi qua Horcrux; không gộp hai bên.

**Editor setup (agent liệt kê lại đầy đủ khi tới task này):** tạo `AudioCatalog_Gameplay` (từ `GameAudioCatalog`) với **12** entry,
id chọn từ dropdown `GameSfx`, clip lấy từ 12 prefab `sfx_*` hiện tại (không tính `sfx_box_combo3..6`; entry `BoxCombo` lấy clip của `sfx_box_combo2`) · `Box` prefab: **Combo Semitones Per Step** = 2, **Combo Max Step** = 4 (đã là mặc định, chỉ chỉnh khi tune); thêm **Tracks** id chọn từ dropdown `GameMusic`, clip lấy từ
`MusicContainer` hiện tại · `audio` trong `Service.unity` với `GameAudioService` theo Task 3 (gồm ô **Music Source**) ·
add `GameplayAudioToggle` lên `audio` · xoá instance prefab `AudioController` và `MusicController` khỏi `Service.unity`.

**Kịch bản chơi thử (developer):** vào level 1 → tap 3 item cùng loại → nghe tiếng tap mỗi lần, tiếng nhận box
khi item vào box, hoàn thành 6 box liên tiếp (combo 2 → 6): tiếng combo **cao dần đều** theo từng bậc, dừng tăng từ combo 6, không chói, không lệch tông · mở Setting, tắt Sound giữa lúc đang có tiếng →
im ngay, bật lại → tiếng kế tiếp phát · dùng booster Tray, Magnet, Cart → mỗi cái một tiếng · để thua → tiếng thua · nhạc nền loop khi vào level; mở Setting tắt Music → nhạc dừng ngay, SFX vẫn kêu; bật lại → nhạc phát lại; tắt Sound → nhạc vẫn phát · đổi sang level/màn khác gọi cùng bài → nhạc không giật về đầu.
Dấu hiệu hỏng: nhạc bị cắt khi nhiều SFX dồn, nhạc không phát lại sau khi bật Music, console đỏ `[AudioService]:` hoặc `[AudioCatalog]:`, tiếng phát trễ hơn hiệu ứng hình, tiếng chói khi nhiều box hoàn thành cùng lúc, bậc combo 5–6 chói hoặc bị cắt cụt (hạ **Combo Semitones Per Step** / **Combo Max Step** trên `Box`), hai bậc đầu khó phân biệt (tăng **Combo Semitones Per Step**), hai box hoàn thành trong 0,05s mất tiếng của box sau (hạ `Min Interval Seconds` của `BoxCombo` nếu muốn nghe cả hai).

---

### Task 5: Test · SystemPlan · tài liệu module (agent)

- [ ] Test EditMode trong `Assets/Horcrux/Tests/EditMode/AudioServiceTests.cs` và `AudioCatalogTests.cs` theo hai bảng case ở Task 2 và 3; dựng host bằng `new GameObject` + `AddComponent<TestAudioService>` (hai enum và `TestAudioCatalog` của Task 2, Step 6; `TestAudioService` của Task 3), bắt log bằng `LogAssert.Expect` với regex `^\[AudioService\]: `.
- [ ] Cập nhật dòng §8 Audio trong `Assets/Horcrux/SystemPlan.md`: `Foundation`, 8 file, generic theo hai enum của dự án, API `IAudioSetting` 2 member + `IAudioService<,>` 4 member (SFX + SFX có `pitchScale` + Music tối thiểu); cột "ngoài plan" giữ `PlaySfxAt`, crossfade, pause/resume nhạc, `PlayDynamic`, mixer group, `volumeScale`.
- [ ] Viết `AudioSystem.md` tài liệu module (thay file plan này sau khi code xong) với mục **Trước khi chạy** lấy từ Editor setup của Task 2 và 3; rồi `.html`.

---

## Cheat

Không có. Mọi tiếng và nhạc đều tới được bằng đường người chơi trong vài giây chơi; bật tắt Sound và Music đã có trong Setting.
