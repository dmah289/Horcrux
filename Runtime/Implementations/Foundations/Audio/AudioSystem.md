# Audio System — Plan

> **Loại tài liệu:** Plan — developer tự gõ từng dòng vào Horcrux. Agent viết test sau khi có code thật,
> rồi viết `.md` tài liệu module và `.html` theo chuỗi `code → .md → .html`.
> Steps dùng checkbox `- [ ]`. Sau mỗi task: compile sạch, agent so code với plan và phân loại chỗ lệch.

**Goal:** `IAudioService` — phát **SFX 2D** và **Music** theo **id trong catalog**. SFX: nhiều tiếng cùng
lúc, mỗi tiếng một cao độ riêng, chống chói khi một clip bị gọi dồn trong cùng frame, **0 B cấp phát**
mỗi lần phát. Music: một bài loop, đổi bài là cắt rồi phát bài mới, bật/tắt độc lập với SFX.

**Architecture:** 7 file. Dự án chỉ gọi tên 2 file ở `Abstractions/`.

```
Abstractions/Foundations/Audio/
├── AudioId.cs              struct wrap int — khoá tra cứu có kiểu
└── IAudioService.cs        IsSfxOn · PlaySfx(AudioId) · IsMusicOn · PlayMusic(AudioId) · StopMusic

Implementations/Foundations/Audio/
├── AudioEntry.cs           một tiếng SFX: id · clip · volume · minInterval  (class [Serializable])
├── MusicTrack.cs           một bài nhạc: id · clip · volume  (class [Serializable])
├── AudioCatalog.cs         ScriptableObject chứa AudioEntry[] + MusicTrack[] — game điền, Horcrux không biết clip nào
├── AudioCatalog.Editor.cs  nút Validate (Odin [Button])
├── AudioService.cs         host MonoBehaviour: SFX (tra entry → throttle → cao độ → cấp voice) + Music (một source riêng)
└── AudioService.Editor.cs  OnValidate ép cờ voice và cờ music source · context menu thêm 12 voice
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
| **Tên** | `Giả định (cần xác nhận):` `AudioCatalog` · `AudioEntry` · `AudioService` · `IsSfxOn` · `PlaySfx`. Đã chốt: `MusicTrack` · `IsMusicOn` · `PlayMusic` · `StopMusic`. Đổi tên trước khi gõ, không đổi sau. |
| **Cố ý KHÔNG làm + lý do** | ① **`pitchScale` / `volumeScale` trên `PlaySfx`** — không caller. Thêm sau là **một tham số tuỳ chọn**, call site cũ không đổi; pitch đã là một biến cục bộ ở đường phát nên thêm không phải thiết kế lại. ② **Interface cho catalog và entry** — chỉ có một implementation; tách khi có nguồn dữ liệu thứ hai (remote, procedural). ③ **SFX 3D** (`PlaySfxAt`, `spatialBlend`) — mọi call site hiện tại là 2D. ④ **Crossfade, fade, pause/resume nhạc, `PlayDynamic` (nhạc đổi theo trạng thái), mixer group** — chưa chốt cần; mỗi cái là thêm method hoặc field. Music bản tối thiểu **có** trong plan. ⑤ **Nhiều clip biến thể cho một tiếng** (chọn ngẫu nhiên, tuần tự, theo trọng số) — mỗi entry đúng **một** clip; phát triển sau. Khi thêm, đổi `Clip` thành mảng và throttle sang khoá theo clip (§0.3). ⑥ **Lưu bền `IsSfxOn`** — game đã có save riêng. |
| **Quyết định trái trực giác** | Voice là **mảng `AudioSource` kéo tay** thay vì pool tạo lúc chạy — số voice là hằng cấu hình trong một asset, thiếu thì lộ ô trống. Throttle khoá theo **clip** chứ không theo id — xem §0.3. Hết voice thì **cướp voice sắp xong** thay vì bỏ tiếng mới — tiếng mới là phản hồi người chơi vừa gây ra. **Không object pool runtime**: mảng voice đã là pool cố định (tạo sẵn lúc authoring, cho mượn–cướp–trả, không `Instantiate`/`Destroy` ở đường phát); `ObjectPool<AudioSource>` thêm cấp phát khi lớn lên và giấu số voice khỏi Inspector. **Music có `AudioSource` riêng, ngoài mảng voice** — SFX cướp voice không bao giờ cắt nhạc. Tắt Music rồi bật lại thì bài đang chọn phát lại từ đầu: `PlayMusic` lúc tắt chỉ ghi nhớ bài, không phát. |
| **Mở rộng sau** (đều additive) | `PlaySfx(AudioId, float pitchScale)` cho pitch ramp · `PlaySfxAt` · crossfade (PrimeTween) · `PauseMusic`/`ResumeMusic` · `AudioMixerGroup` trên service, `OnValidate` gán cho mọi voice · `IAudioCatalog` khi có nguồn thứ hai · nạp catalog qua `AssetReference`. |

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
  └─> IAudioService.Service.PlaySfx((int)GameSfx.BoxReceive)
        │
        ├─ !IsSfxOn ──────────────────────────────────────────> return (im lặng, hợp lệ)
        ├─ _entryIndexById[id] ─ thiếu ───────────────────────> LogError, return
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
  └─> IAudioService.Service.PlayMusic((int)GameMusic.Level)
        ├─ _musicIndexById[id] ─ thiếu ─────────────> LogError, return
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
| 1 | `Abstractions/Foundations/Audio/AudioId.cs` · `IAudioService.cs` | developer |
| 2 | `Implementations/Foundations/Audio/AudioEntry.cs` · `MusicTrack.cs` · `AudioCatalog.cs` · `AudioCatalog.Editor.cs` | developer |
| 3 | `Implementations/Foundations/Audio/AudioService.cs` · `AudioService.Editor.cs` | developer |
| 4 | Lớp nối Category Jam + gỡ `AudioController` | agent, sau khi Task 3 compile |
| 5 | Test · `SystemPlan.md` · tài liệu module | agent |

Thứ tự: **1 → 2 → 3 → 4 → 5**. Mỗi task xong là một mốc compile sạch.

---

### Task 1: 2 contract

**Files:** `Assets/Horcrux/Runtime/Abstractions/Foundations/Audio/AudioId.cs` · `IAudioService.cs`

**Interfaces:**
- Consumes: `Horcrux.Runtime.Abstractions.IService<T>`.
- Produces: `readonly struct AudioId` · `IAudioService : IService<IAudioService>` (5 member).

**Quyết định thiết kế:**

| Quyết định | Vì sao |
|---|---|
| `AudioId` wrap `int`, không `string` | Chuỗi gõ tay lệch một ký tự thì tiếng im lặng không phát; `int` từ enum của game có kiểm tra biên dịch, refactor-rename được, tra không hash chuỗi |
| `AudioId` là struct riêng, không truyền `int` trần | `PlaySfx(int)` nhận nhầm một số bất kỳ mà không báo; struct nói rõ tham số này là khoá tra cứu |
| `IEquatable<AudioId>` + `GetHashCode => Value` | Không có nó, `Dictionary<AudioId,_>` rơi về comparer object ⇒ boxing mỗi lần tra ⇒ rác ở nhịp 20 lần/giây |
| `0` = chưa gán | Giá trị mặc định của `int` là giá trị hợp lệ nếu không dành riêng; dành `0` để ô Inspector quên điền lộ ra ở validate |
| `IAudioService` 5 member | `IsSfxOn`, `IsMusicOn` cho hai toggle của game; `PlaySfx(id)`, `PlayMusic(id)`, `StopMusic()` cho gameplay. Không có `StopAll` public — tắt cờ đã dừng mọi voice; caller khác chưa có |
| `IsSfxOn`, `IsMusicOn` là property, setter có hệ quả | Tắt phải im **ngay** ở mọi đường ghi, rẻ, không ném ⇒ hệ quả nằm trong setter, không là method cạnh field |
| `PlayMusic` dùng lại `AudioId`, không có kiểu id riêng | Id nhạc và id SFX tra ở hai bảng khác nhau nên được trùng số; game khai hai enum (`GameSfx`, `GameMusic`) |
| Không có `bool loop` trên `PlayMusic` | Nhạc nền luôn loop; ép ở source lúc authoring. Jingle một lần là SFX |

- [ ] **Step 1: `AudioId.cs`**

```csharp
using System;

namespace Horcrux.Runtime.Abstractions.Audio
{
    /// <summary>Key of one catalog entry. Wraps <c>int</c> so a call site cannot pass an arbitrary number where an id belongs.</summary>
    /// <remarks>
    /// The game declares <c>enum GameSfx { TapItem = 1, … }</c> with explicit values from 1 and converts with
    /// <c>(int)</c>. <c>0</c> is reserved for "unassigned" so an Inspector field left empty is caught by validation.
    /// <see cref="IEquatable{T}"/> keeps <c>Dictionary</c> lookups free of boxing.
    /// </remarks>
    public readonly struct AudioId : IEquatable<AudioId>
    {
        public readonly int Value;

        public AudioId(int value) => Value = value;

        public static implicit operator AudioId(int value) => new(value);

        /// <summary><c>0</c> means unassigned.</summary>
        public bool IsValid => Value != 0;

        public bool Equals(AudioId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is AudioId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();
    }
}
```

- [ ] **Step 2: `IAudioService.cs`**

```csharp
namespace Horcrux.Runtime.Abstractions.Audio
{
    /// <summary>Plays 2D sound effects and one looping music track from the game's catalog.</summary>
    /// <remarks>SFX: many at once, each with its own pitch, bursts of one clip throttled. Music: one track at a time.</remarks>
    public interface IAudioService : IService<IAudioService>
    {
        /// <summary>Player setting. Turning it off also silences voices already playing. The game writes it from its own save.</summary>
        bool IsSfxOn { get; set; }

        /// <summary>Player setting. Off stops the music; on again restarts the chosen track. The game writes it from its own save.</summary>
        bool IsMusicOn { get; set; }

        /// <param name="id">Catalog entry. An id missing from the catalog logs an error and plays nothing.</param>
        void PlaySfx(AudioId id);

        /// <summary>Replaces the current track. Same track again keeps playing. While <see cref="IsMusicOn"/> is off, only remembers the choice.</summary>
        /// <param name="id">Music track in the catalog. An id missing from it logs an error and keeps the current track.</param>
        void PlayMusic(AudioId id);

        void StopMusic();
    }
}
```

- [ ] **Step 3: Compile sạch.** Commit — `feat(sdk): add audio contracts (AudioId, IAudioService)`

---

### Task 2: `AudioEntry` + `MusicTrack` + `AudioCatalog`

**Files:** `Assets/Horcrux/Runtime/Implementations/Foundations/Audio/AudioEntry.cs` · `MusicTrack.cs` · `AudioCatalog.cs` · `AudioCatalog.Editor.cs`

**Interfaces:**
- Consumes: `AudioId`.
- Produces: `AudioEntry` (5 field authoring, property get-only) · `MusicTrack` (4 field) · `AudioCatalog : ScriptableObject` (`Entries`, `MusicTracks`, `LogInvalidEntries()`).

**Quyết định thiết kế:**

| Quyết định | Vì sao |
|---|---|
| `MusicTrack` là class riêng, không dùng lại `AudioEntry` | Nhạc không có throttle, nhiều clip; hai kiểu đổi vì hai lý do khác nhau, và dùng chung thì ô vô nghĩa nằm đầy Inspector (§3.1 `S`) |
| `AudioEntry` là `class`, không `struct` | Unity serialize, sống suốt đời asset ⇒ không cấp phát theo tần số phát; struct bị copy mỗi lần đọc qua property |
| `displayName` chỉ để đọc | Danh sách 16 phần tử toàn `int` không đọc được trong Inspector; tên cũng đi vào log lỗi |
| `[Min(0)]` cho interval | Kẹp dải **lúc authoring** thay cho clamp runtime; ô quên điền không thể mang giá trị âm |
| Catalog **chỉ là dữ liệu** | Không dictionary, không con trỏ chọn clip trong SO: field đổi lúc Play không quay lại khi dừng Play, asset không có thứ tự khởi tạo. Mọi bảng tra và state ở service |
| `LogInvalidEntries()` ở file runtime, một thân duy nhất | Service gọi ở `Awake`, nút Editor gọi cùng method ⇒ hai nơi kiểm cùng một luật, không lệch |
| Validate **khi được hỏi** (nút) + **một lần ở `Awake`**, không `OnValidate` | Đang dựng danh sách thì dữ liệu luôn chưa hợp lệ, `OnValidate` sẽ đỏ console sau mỗi lần thêm phần tử |
| Trùng id kiểm O(n²) không `HashSet` | n ≈ 16, chạy một lần ở `Awake`; `HashSet` là một cấp phát để tiết kiệm vài trăm phép so |
| Id SFX và id nhạc kiểm trùng **riêng từng bảng** | Hai bảng tra độc lập; trùng giữa hai bảng không gây lỗi |

- [ ] **Step 1: `AudioEntry.cs`**

```csharp
using System;
using System.Collections.Generic;
using Horcrux.Runtime.Abstractions.Audio;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Audio
{
    /// <summary>Authoring data of one sound. Filled in the Inspector of <see cref="AudioCatalog"/>.</summary>
    /// <remarks>A class, not a struct: Unity serializes it once per asset, so reading it per play copies nothing.</remarks>
    [Serializable]
    public sealed class AudioEntry
    {
        [SerializeField, Tooltip("Shown in this list and in error logs only.")]
        private string displayName;

        [SerializeField, Tooltip("Must equal the game's enum value. 0 = unassigned.")]
        private int id;

        [SerializeField]
        private AudioClip clip;

        [SerializeField, Range(0f, 1f)]
        private float volume = 1f;

        [SerializeField, Min(0f), Tooltip("Seconds this sound must wait before it may play again. 0 = off.")]
        private float minIntervalSeconds = 0.05f;

        public string DisplayName => displayName;
        public AudioId Id => id;
        public AudioClip Clip => clip;
        public float Volume => volume;
        public float MinIntervalSeconds => minIntervalSeconds;
    }
}
```

- [ ] **Step 2: `MusicTrack.cs`**

```csharp
using System;
using Horcrux.Runtime.Abstractions.Audio;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Audio
{
    /// <summary>Authoring data of one music track. Filled in the Inspector of <see cref="AudioCatalog"/>.</summary>
    [Serializable]
    public sealed class MusicTrack
    {
        [SerializeField, Tooltip("Shown in this list and in error logs only.")]
        private string displayName;

        [SerializeField, Tooltip("Must equal the game's music enum value. 0 = unassigned.")]
        private int id;

        [SerializeField]
        private AudioClip clip;

        [SerializeField, Range(0f, 1f)]
        private float volume = 1f;

        public string DisplayName => displayName;
        public AudioId Id => id;
        public AudioClip Clip => clip;
        public float Volume => volume;
    }
}
```

- [ ] **Step 3: `AudioCatalog.cs`**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Audio
{
    /// <summary>The game's list of sounds and music. Data only: every lookup table and play state lives in <see cref="AudioService"/>.</summary>
    [CreateAssetMenu(fileName = "AudioCatalog", menuName = "Horcrux/Audio Catalog")]
    public sealed partial class AudioCatalog : ScriptableObject
    {
        [SerializeField] private AudioEntry[] entries = Array.Empty<AudioEntry>();
        [SerializeField] private MusicTrack[] musicTracks = Array.Empty<MusicTrack>();

        public IReadOnlyList<AudioEntry> Entries => entries;
        public IReadOnlyList<MusicTrack> MusicTracks => musicTracks;

        /// <summary>Logs every authoring mistake that would otherwise drop a sound silently. Returns how many entries and tracks are unusable.</summary>
        public int LogInvalidEntries()
        {
            int invalidAmount = 0;

            for (int i = 0; i < entries.Length; i++)
            {
                if (!IsEntryValid(i))
                    invalidAmount++;
            }

            for (int i = 0; i < musicTracks.Length; i++)
            {
                if (!IsMusicTrackValid(i))
                    invalidAmount++;
            }

            return invalidAmount;
        }

        private bool IsMusicTrackValid(int index)
        {
            MusicTrack track = musicTracks[index];
            string label = $"music track #{index} '{track.DisplayName}'";

            if (!track.Id.IsValid)
            {
                Debug.LogError($"[AudioCatalog]: {label} has Id 0 (unassigned).", this);
                return false;
            }

            for (int j = 0; j < index; j++)
            {
                if (musicTracks[j].Id.Equals(track.Id))
                {
                    Debug.LogError($"[AudioCatalog]: {label} repeats Id {track.Id} of music track #{j}. Only the first one plays.", this);
                    return false;
                }
            }

            if (track.Clip == null)
            {
                Debug.LogError($"[AudioCatalog]: {label} has no clip.", this);
                return false;
            }

            return true;
        }

        private bool IsEntryValid(int index)
        {
            AudioEntry entry = entries[index];
            string label = $"entry #{index} '{entry.DisplayName}'";

            if (!entry.Id.IsValid)
            {
                Debug.LogError($"[AudioCatalog]: {label} has Id 0 (unassigned).", this);
                return false;
            }

            for (int j = 0; j < index; j++)
            {
                if (entries[j].Id.Equals(entry.Id))
                {
                    Debug.LogError($"[AudioCatalog]: {label} repeats Id {entry.Id} of entry #{j}. Only the first one plays.", this);
                    return false;
                }
            }

            if (entry.Clip == null)
            {
                Debug.LogError($"[AudioCatalog]: {label} has no clip.", this);
                return false;
            }

            return true;
        }
    }
}
```

- [ ] **Step 4: `AudioCatalog.Editor.cs`**

```csharp
using Sirenix.OdinInspector;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Audio
{
    public sealed partial class AudioCatalog
    {
#if UNITY_EDITOR
        [Button]
        private void ValidateEntries()
        {
            if (LogInvalidEntries() == 0)
                Debug.Log($"[AudioCatalog]: {entries.Length} entries, {musicTracks.Length} music tracks — ids unique and assigned, every slot has a clip.", this);
        }
#endif
    }
}
```

**Editor setup (asset của game, không trong `Assets/Horcrux/`):**

1. Project window → `Create → Horcrux → Audio Catalog`, đặt tên theo game (Category Jam: `AudioCatalog_Gameplay`, thư mục `Assets/_Gameplay/Audio/`).
2. Mỗi tiếng một entry trong **Entries**: `Display Name` · `Id` khớp **đúng số** trong enum `GameSfx` · kéo một clip vào `Clip`.
3. Mỗi bài nhạc một phần tử trong **Music Tracks**: `Display Name` · `Id` khớp **đúng số** trong enum `GameMusic` · kéo clip vào `Clip`. Thiếu: `PlayMusic` log error "id not in catalog".
4. Bấm nút **Validate Entries**. Console phải có **một dòng log** xác nhận, không error. Thiếu bước này: id trùng hoặc 0 chỉ lộ ra khi Play, dưới dạng tiếng không phát.

- [ ] **Step 5: Kiểm chứng — case agent sẽ test**

| Input | Kỳ vọng |
|---|---|
| 3 entry id `1, 2, 3`, mỗi entry 1 clip | `LogInvalidEntries() == 0`, không log |
| Music track `id = 0` | 1 error có `displayName`, trả `1` |
| Hai music track cùng `id = 2` | 1 error trỏ track **sau**, trả `1` |
| Music track `clip = null` | 1 error, trả `1` |
| Entry SFX id `1` và music track id `1` | `LogInvalidEntries() == 0` (hai bảng độc lập) |
| Entry có `id = 0` | 1 error có `displayName`, trả `1` |
| Hai entry cùng `id = 5` | 1 error trỏ entry **sau**, trả `1` |
| Entry `clip = null` | 1 error, trả `1` |
| Catalog rỗng | trả `0`, không log |

- [ ] **Step 6: Commit** — `feat(sdk): add AudioCatalog + AudioEntry + MusicTrack`

---

### Task 3: `AudioService`

**Files:** `Assets/Horcrux/Runtime/Implementations/Foundations/Audio/AudioService.cs` · `AudioService.Editor.cs`

**Interfaces:**
- Consumes: `AudioCatalog`, `AudioEntry` (cùng hệ, nhận class cụ thể qua `[SerializeField]`), `Sisus.Init.ServiceAttribute`.
- Produces: `AudioService : MonoBehaviour, IAudioService`, đăng ký `[Service(typeof(IAudioService), FindFromScene = true)]`.

**Bản đồ §0 → code:**

| §0 | Code |
|---|---|
| §0.1 mốc rảnh | `_voiceBusyUntil[voice] = now + clip.length / pitch` |
| §0.2 pitch ngẫu nhiên | `Random.Range(1f - PitchSpread, 1f + PitchSpread)` — hằng, không clamp |
| §0.3 throttle biên đóng | `now - last < minInterval → return`; `last` khởi tạo `float.NegativeInfinity` để lần đầu luôn qua |

**Quyết định thiết kế:**

| Quyết định | Vì sao |
|---|---|
| Voice = mảng `AudioSource` kéo tay trên cùng GameObject | Số voice là hằng cấu hình trong một asset ⇒ kéo thả, ô trống có người nhìn. Một GameObject đủ vì chỉ phát 2D. Không `PlayOneShot` trên một source vì mọi tiếng sẽ chung một `pitch` |
| Compact mảng voice ở `Awake` thành `_voices` không null | Ô trống log một lần ở boot; đường phát không kiểm null 12 lần mỗi tiếng |
| Mọi bảng tra dựng ở `Awake` | `_entryIndexById` · `_lastPlayTimeByEntry` · `_voiceBusyUntil`. Sau `Awake`, `PlaySfx` không cấp phát |
| Throttle lưu theo chỉ số entry (`float[]`) chứ không `Dictionary<AudioClip,_>` | Chỉ số đã có trong tay; mảng không hash, không cấp phát. Cùng một clip ở hai entry thì throttle riêng — chấp nhận, không có ca thật |
| Rảnh đầu tiên, hết thì cướp voice có `busyUntil` nhỏ nhất | Bỏ tiếng mới là bỏ phản hồi người chơi vừa gây ra; voice sắp xong bị cắt ít gây chú ý nhất |
| Kiểm cả `busyUntil` và `isPlaying` | Mốc là dự tính (§0.1); `isPlaying` là sự thật của engine |
| `Time.unscaledTime` | Popup pause đặt `timeScale = 0`; `Time.time` đứng yên ⇒ throttle đóng băng, mọi tiếng sau bị bỏ |
| Id lạ, entry không clip ⇒ `LogError` mỗi lần, không throw | Sai lúc setup mà không nổ ⇒ phải báo, kể cả lặp. Throw giữa gameplay là trả giá cho một lỗi cấu hình |
| Bị throttle, `IsSfxOn = false` ⇒ im lặng | Ca hợp lệ, không phải lỗi |
| `IsSfxOn = false` dừng voice, **không** xoá mốc throttle | Mốc là `unscaledTime` tăng đều; chỉ chặn trong `minInterval` ≈ 0.05s sau khi bật lại — không có ca "bỏ oan" |
| Music: một `musicSource` riêng, một hàm `ApplyMusicState` | `IsMusicOn`, `PlayMusic`, `StopMusic` chỉ đổi trạng thái (cờ, bài đang chọn) rồi gọi một hàm làm source khớp trạng thái ⇒ một cửa ghi lên source, không ba nơi tự `Play`/`Stop` lệch nhau (§3.4) |
| `PlayMusic` lúc `IsMusicOn = false` chỉ nhớ bài | Bật lại thì đúng bài game đã chọn phát; game không phải gọi lại `PlayMusic` sau mỗi lần toggle |
| Gọi lại đúng bài đang phát không restart | So `clip`; level mới gọi `PlayMusic` cùng bài không bị giật nhạc |
| Id nhạc lạ ⇒ `LogError`, **giữ bài hiện tại** | Sai lúc setup không nổ ⇒ phải báo; cắt nhạc đang chạy vì một id sai là phá thêm |
| Không `DontDestroyOnLoad` | Host là component kéo vào scene; đời sống do scene quyết. Game đặt nó ở scene persistent của nó |

**Editor setup — bước thật, trong scene của game:**

1. Mở scene dịch vụ persistent của game (Category Jam: `Assets/_Game/Scenes/Service.unity`). Tạo GameObject `[Audio]` → Add Component `AudioService`.
2. Kéo asset catalog (Task 2) vào ô **Catalog**. Thiếu: mỗi `PlaySfx` log một error "id not in catalog".
3. Chuột phải tiêu đề component `AudioService` → **Add 12 Voices**: 12 `AudioSource` được thêm vào chính GameObject và điền vào **Voices**. Muốn số khác thì add tay rồi kéo vào mảng; `OnValidate` tự tắt `Play On Awake`, `Loop`, đặt `Spatial Blend = 0` cho mọi voice trong mảng. Thiếu voice: mỗi `PlaySfx` log một error.
4. Add Component `AudioSource` thứ 13 lên cùng GameObject, kéo vào ô **Music Source** (không kéo vào **Voices**). `OnValidate` tự đặt `Loop = true`, tắt `Play On Awake`, `Spatial Blend = 0`. Thiếu: mỗi lần gọi `PlayMusic`, `StopMusic`, hoặc set `IsMusicOn` log một error.
5. Save scene. Kiểm: mảng **Voices** không có ô `None`, ô **Music Source** có giá trị, Console không error.

- [ ] **Step 1: `AudioService.cs`**

```csharp
using System;
using System.Collections.Generic;
using Horcrux.Runtime.Abstractions.Audio;
using Horcrux.Runtime.Utilities;
using Sisus.Init;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Audio
{
    /// <summary>Plays 2D SFX: find entry → throttle per sound → random pitch → rent a voice. No allocation per play. Also keeps one looping music track.</summary>
    /// <remarks>
    /// Voices are <see cref="AudioSource"/> components assigned in the Inspector, not created at runtime: a missing one shows
    /// as an empty slot while authoring, and the voice count is tuned without recompiling.
    /// Music has its own source outside the voices, so a stolen voice never cuts the music.
    /// All lookup tables and play state live here, never in <see cref="AudioCatalog"/>.
    /// </remarks>
    [Service(typeof(IAudioService), FindFromScene = true)]
    public sealed partial class AudioService : MonoBehaviour, IAudioService
    {
        [Splitter("References")]
        [SerializeField] private AudioCatalog catalog;

        [SerializeField, Tooltip("One AudioSource per simultaneous sound. When all are busy, the one closest to finishing is taken over.")]
        private AudioSource[] voices = Array.Empty<AudioSource>();

        [SerializeField, Tooltip("Plays the music track. Not one of the voices, so SFX never take it over.")]
        private AudioSource musicSource;

        private bool _isSfxOn = true;
        private bool _isMusicOn = true;
        private int _currMusicIndex = -1;
        private readonly Dictionary<AudioId, int> _musicIndexById = new();
        private AudioSource[] _voices = Array.Empty<AudioSource>();
        private float[] _voiceBusyUntil = Array.Empty<float>();
        private readonly Dictionary<AudioId, int> _entryIndexById = new();
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

        private void Awake()
        {
            CollectVoices();
            BuildEntryTables();
        }

        #endregion

        #region API

        public void PlaySfx(AudioId id)
        {
            if (!_isSfxOn)
                return;

            if (!_entryIndexById.TryGetValue(id, out int entryIndex))
            {
                Debug.LogError($"[AudioService]: Id {id} is not in the catalog.", this);
                return;
            }

            AudioEntry entry = catalog.Entries[entryIndex];
            AudioClip clip = entry.Clip;

            if (clip == null)
            {
                Debug.LogError($"[AudioService]: '{entry.DisplayName}' has no clip.", this);
                return;
            }

            if (_voices.Length == 0)
            {
                Debug.LogError("[AudioService]: No voice assigned — use 'Add 12 Voices' on the component.", this);
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

        public void PlayMusic(AudioId id)
        {
            if (!_musicIndexById.TryGetValue(id, out int musicIndex))
            {
                Debug.LogError($"[AudioService]: Music id {id} is not in the catalog.", this);
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
                    Debug.LogError($"[AudioService]: Voices[{i}] is empty.", this);
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
                Debug.LogError("[AudioService]: Catalog is not assigned — every PlaySfx will be dropped.", this);
                return;
            }

            catalog.LogInvalidEntries();

            IReadOnlyList<AudioEntry> entries = catalog.Entries;
            _lastPlayTimeByEntry = new float[entries.Count];
            Array.Fill(_lastPlayTimeByEntry, float.NegativeInfinity);

            for (int i = 0; i < entries.Count; i++)
            {
                AudioEntry entry = entries[i];

                if (entry.Id.IsValid)
                    _entryIndexById.TryAdd(entry.Id, i);   // duplicates were already reported by the catalog
            }

            IReadOnlyList<MusicTrack> tracks = catalog.MusicTracks;

            for (int i = 0; i < tracks.Count; i++)
            {
                if (tracks[i].Id.IsValid)
                    _musicIndexById.TryAdd(tracks[i].Id, i);
            }
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

            MusicTrack track = catalog.MusicTracks[_currMusicIndex];
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
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Audio
{
    public sealed partial class AudioService
    {
#if UNITY_EDITOR
        private const int DefaultVoiceAmount = 12;

        /// <summary>Each play draws pitch uniformly in [1 − spread, 1 + spread]. Fixed: not tuned per sound.</summary>
        private const float PitchSpread = 0.05f;

        /// <summary>Three flags each cause a different silent bug: a bang on load, a sound that never ends, a 2D effect heard from afar.</summary>
        private void OnValidate()
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

- [ ] **Step 3: Kiểm chứng — case agent sẽ test** (biên theo: rỗng · một phần tử · chạm giới hạn · trùng · hai sự kiện cùng lúc · frame đầu)

| # | Input | Kỳ vọng |
|---|---|---|
| 1 | `PlaySfx(id)` khi `IsSfxOn = false` | không voice nào `isPlaying` |
| 2 | `PlaySfx(id lạ)` | 1 error `[AudioService]:`, không throw |
| 3 | Chưa gán catalog | 1 error ở `Awake`; mỗi `PlaySfx` thêm 1 error, không throw |
| 4 | Mảng `Voices` rỗng | mỗi `PlaySfx` 1 error, không throw |
| 5 | `Voices` có 1 ô `None` giữa 12 | 1 error ở `Awake`; 11 voice vẫn phát |
| 6 | `PlaySfx(id hợp lệ)` | voice được chọn có `clip == entry.Clip`, `volume == entry.Volume` |
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
| 19 | Profiler: 20 lần phát/giây, 10 giây | **0 B** GC Alloc trên đường `PlaySfx` |
| 20 | Bật `Play On Awake` trên một voice rồi rời Inspector | `OnValidate` tắt lại |
| 21 | `PlayMusic(id)` khi `IsMusicOn = true` | `musicSource.isPlaying`, `clip` và `volume` đúng track, `loop == true` |
| 22 | `PlayMusic(id lạ)` khi đang phát bài A | 1 error `[AudioService]:`, bài A vẫn phát |
| 23 | `PlayMusic(A)` rồi `PlayMusic(B)` | `clip == B` |
| 24 | `PlayMusic(A)` hai lần liên tiếp | lần hai không restart: `time` không về 0 |
| 25 | `IsMusicOn = false` khi đang phát | `isPlaying == false` ngay; `IsSfxOn` và voice SFX không đổi |
| 26 | `IsMusicOn = false`, `PlayMusic(A)`, rồi `IsMusicOn = true` | sau `PlayMusic` chưa phát; sau bật phát bài A |
| 27 | `PlayMusic(A)`, `StopMusic()`, bật tắt `IsMusicOn` | không phát (bài đã bị bỏ chọn) |
| 28 | `IsSfxOn = false` khi nhạc đang phát | nhạc vẫn phát |
| 29 | 20 lần `PlaySfx` liên tiếp hết voice, nhạc đang phát | nhạc không bị cắt (`musicSource` không nằm trong `_voices`) |
| 30 | Chưa gán `musicSource`, gọi `PlayMusic`, `StopMusic`, set `IsMusicOn` | mỗi lệnh 1 error, không throw |
| 31 | `PlayMusic` khi chưa gán catalog | 1 error "not in catalog", không throw |

- [ ] **Step 4: Commit** — `feat(sdk): add AudioService (authored voices, per-sound throttle, random pitch, music source)`

---

### Task 4: Lớp nối Category Jam (agent viết, sau khi Task 3 compile)

Hai hệ cùng tên `IAudioService` cùng tồn tại có chủ ý: `Kelsey.IAudioService` + `SoundController` là
lớp contract ScrewDom, đóng băng để kéo LiveOps; `Horcrux.Runtime.Abstractions.Audio.IAudioService` phục
vụ gameplay. Chúng ở hai assembly khác nhau; file nào cần cả hai thì dùng alias
`using HxAudio = Horcrux.Runtime.Abstractions.Audio;`.

| Việc | Ở đâu | Ghi chú |
|---|---|---|
| Thêm tham chiếu `com.horcrux.runtime` | `Assets/_Gameplay/CategoryJam.Gameplay.asmdef` | Chiều phụ thuộc: gameplay → Horcrux |
| `enum GameSfx` gán số tường minh từ `= 1` | `Assets/_Gameplay/Scripts/Definition/GameSfx.cs` | 16 giá trị thay 11 hằng chuỗi + `GetComboAudioID`. Số đã vào asset là wire format: phần tử mới thêm ở cuối |
| `enum GameMusic` gán số tường minh từ `= 1` | `Assets/_Gameplay/Scripts/Definition/GameMusic.cs` | Liệt kê bài từ `MusicContainer` hiện tại; `Giả định (cần xác nhận):` số bài đọc từ asset khi tới task này |
| `static class GameAudio { Play(GameSfx) · PlayMusic(GameMusic) · StopMusic() }` | `Assets/_Gameplay/Scripts/Common/GameAudio.cs` | Mỗi hàm một dòng: `IAudioService.Service.PlaySfx((int)sfx)`, `PlayMusic((int)music)`, `StopMusic()`. Mã hoá phép đổi kiểu để call site không lặp `(int)` |
| Đổi call site của `MusicController` | Grep `MusicController` trong `_Gameplay`, `_Game` | `Play(name)` → `GameAudio.PlayMusic(GameMusic.X)`; `Stop()` → `GameAudio.StopMusic()`. `Pause`/`Continue`/`PlayDynamic` còn caller thì **dừng, hỏi developer** — ngoài bản tối thiểu |
| Đổi 20 call site | `Box`, `BoxAnimation`, `BoxesContainer`, `CacheHoles`, `ShoppingCart`, `CacheHole`, `GoodsItem`, `NormalMovingShelf`, `NormalShelf`, `SingleMovingShelf`, `GameDirector.Events` | `GameAudio.Play(GameSfx.X)`. Combo: `GameSfx.BoxCombo2..6` theo `comboNumber`, giữ nguyên 5 clip — `Giả định (cần xác nhận):` chưa đổi sang pitch ramp vì đó là đổi cảm giác chơi |
| Cầu nối setting | `Assets/_Game/Scripts/Common/GameplayAudioToggle.cs`, component trên `[Audio]` | `MonoBehaviour<IAudioPersistentData, HxAudio.IAudioService>`: `Init` set `IsSfxOn = Sound`, `IsMusicOn = Music` rồi `RegisterOnSoundChange` + `RegisterOnMusicChange`; `OnDestroy` unregister cả hai. Thay cho 4 dòng ở `CategoryGameController.Start` |
| Gỡ hệ cũ | `Assets/_Gameplay/Scripts/Common/Audio/` (15 file, gồm `MusicController` + `MusicContainer`) · `Addressables/Prefabs/Sounds/` (16 prefab) · nhóm Addressables SFX · `Constant.AudioID.cs` · `GameplaySession.SfxOn` · instance `AudioController` trong `Service.unity` | Xoá sau khi mọi call site đã chuyển và chơi thử xong. Grep `AudioController`, `MusicController`, `AUDIO_`, `SfxOn` phải trả 0 |
| Ghi vào contract doc | `docs/ScrewDom_Contract.md` mục 2 | Một dòng: `SoundController` đóng băng cho LiveOps; audio gameplay đi qua Horcrux; không gộp hai bên |

**Editor setup (agent liệt kê lại đầy đủ khi tới task này):** tạo `AudioCatalog_Gameplay` với 16 entry,
id khớp `GameSfx`, clip lấy từ 16 prefab `sfx_*` hiện tại; thêm **Music Tracks** id khớp `GameMusic`, clip lấy từ
`MusicContainer` hiện tại · `[Audio]` trong `Service.unity` theo Task 3 (gồm ô **Music Source**) ·
add `GameplayAudioToggle` lên `[Audio]` · xoá instance prefab `AudioController` và `MusicController` khỏi `Service.unity`.

**Kịch bản chơi thử (developer):** vào level 1 → tap 3 item cùng loại → nghe tiếng tap mỗi lần, tiếng nhận box
khi item vào box, tiếng combo khác nhau ở box thứ 2–6 liên tiếp · mở Setting, tắt Sound giữa lúc đang có tiếng →
im ngay, bật lại → tiếng kế tiếp phát · dùng booster Tray, Magnet, Cart → mỗi cái một tiếng · để thua → tiếng thua · nhạc nền loop khi vào level; mở Setting tắt Music → nhạc dừng ngay, SFX vẫn kêu; bật lại → nhạc phát lại; tắt Sound → nhạc vẫn phát · đổi sang level/màn khác gọi cùng bài → nhạc không giật về đầu.
Dấu hiệu hỏng: nhạc bị cắt khi nhiều SFX dồn, nhạc không phát lại sau khi bật Music, console đỏ `[AudioService]:` hoặc `[AudioCatalog]:`, tiếng phát trễ hơn hiệu ứng hình, tiếng chói khi nhiều box hoàn thành cùng lúc.

---

### Task 5: Test · SystemPlan · tài liệu module (agent)

- [ ] Test EditMode trong `Assets/Horcrux/Tests/EditMode/AudioServiceTests.cs` và `AudioCatalogTests.cs` theo hai bảng case ở Task 2 và 3; dựng host bằng `new GameObject` + `AddComponent`, bắt log bằng `LogAssert.Expect` với regex `^\[AudioService\]: `.
- [ ] Cập nhật dòng §8 Audio trong `Assets/Horcrux/SystemPlan.md`: `Foundation`, 7 file, API 5 member (SFX + Music tối thiểu), không `pitchScale`; cột "ngoài plan" giữ `PlaySfxAt`, crossfade, pause/resume nhạc, `PlayDynamic`, mixer group, thêm `pitchScale`.
- [ ] Viết `AudioSystem.md` tài liệu module (thay file plan này sau khi code xong) với mục **Trước khi chạy** lấy từ Editor setup của Task 2 và 3; rồi `.html`.

---

## Cheat

Không có. Mọi tiếng và nhạc đều tới được bằng đường người chơi trong vài giây chơi; bật tắt Sound và Music đã có trong Setting.
