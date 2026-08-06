# Audio (SFX) System Implementation Plan

> **Loại tài liệu:** Plan (`DOCS_SKILL` Phần C). `.md` thiết kế (Phần A) + `.html` (Phần B) viết **sau** khi có source.
>
> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development hoặc superpowers:executing-plans. Steps dùng checkbox (`- [ ]`).

**Goal:** `IAudioService` — phát SFX **nhiều tiếng đồng thời**, **điều khiển được cao độ** (điều kiện bắt buộc của pitch ramp), **throttle theo clip** chống chói khi cascade dồn, **zero alloc** mỗi lần phát.

**Architecture:** **6 file** (3 contract + 2 dữ liệu + 1 service).

```
Abstractions/Foundations/Audio/     AudioId · IAudioCatalog (+IAudioEntry) · IAudioService
Implementations/Foundations/Audio/
├── AudioEntry.cs + AudioCatalogSO  dữ liệu do GAME điền — SDK không biết clip nào tồn tại
└── AudioService.cs                 voice AUTHORED · throttle · gộp pitch
```

**Tech Stack:** C#, `AudioSource`, `Unity.Mathematics`, `Sisus.Init`, `AudioPitchHelper` (**đã có**). Không UniTask/Addressables trong đường phát.

## ⚠️ Phân loại lại: `Foundation`, không `Composite`

`PendingSystems.md` §8 xếp Audio là Composite vì bản đầy đủ dùng Object Pooling + hệ save. Bản thu hẹp này dùng **cả hai đều không**:

| §8 bản đầy đủ | Bản này | Vì sao |
|---|---|---|
| Pool `AudioSource` qua `IPoolManager` | **Mảng `AudioSource` gán sẵn trong Inspector** | `IPoolManager.Get<T>` **throw** khi pool chưa cấu hình, và nó là pool *prefab Addressables* — dựng prefab + pool config + addressable group cho một `AudioSource` trần là chi phí lớn hơn lợi ích. Đây **không** phải "pool thứ hai": không `Get/Return`, không prefab, chỉ một mảng cố định (giống cách engine quản voice) |
| Đọc setting từ §2 | `IsSfxOn` là **field**; game set từ setting của nó | Không coupling hệ save, và không cần thêm interface nào |
| Music + crossfade | ngoài phạm vi | Combo không cần |

⇒ Chạy độc lập, port sang dự án khác không cần hệ nào trong SDK = đúng định nghĩa `Foundation`.

## Global Constraints

| Ràng buộc | Giá trị |
|---|---|
| Namespace | `Horcrux.Runtime.Abstractions.Audio` · `…Implementations.Audio` |
| Zero-GC | `AudioId : IEquatable<>` (dictionary key không boxing); không alloc trong `PlaySfx` |
| SOLID | SDK **không biết clip nào** — catalog là SO của game (D). Chọn clip / throttle / cấp voice là 3 vùng tách rời |
| Khoá logic | `AudioId` wrap `int`, **không** `string` |
| Editor-first (§C.1) | Voice `AudioSource` **gán trong Inspector**, không `AddComponent` lúc runtime. Mọi số cảm giác ở asset catalog (§0.4) |
| Không state trong SO | Con trỏ chọn clip nằm ở **service**, không trong asset (SO mutable = dirty asset + rò state giữa lần chơi) |
| Thời gian | `Time.unscaledTime` — throttle phải đúng khi `timeScale = 0` (hitstop) |

## Ngữ cảnh đã chốt

| Nhóm | Chốt |
|---|---|
| **Ai gọi** | `FeedbackSystem.md` → `AudioPitchRampChannel`: mỗi nhịp combo gọi `PlaySfx(id, volumeScale, pitchScale)` với `pitchScale` từ `AudioPitchHelper.GetRampedPitch`. **Caller duy nhất**, và là lý do hệ này tồn tại sớm hơn music. |
| **Mục tiêu** | Chuỗi combo phải **nghe thấy** đang leo rồi bão hoà. Nghiệm thu: combo 1→12 với `+1 semitone/bậc` cho **đúng một quãng tám** ở bậc 12; 20 lần phát cùng frame không chói, không giật. |
| **Ngân sách** | Cao điểm ~10–20 lần/giây, có thể **cùng frame** ⇒ **hot path đã xác nhận**. Mỗi lần phát phải **0 B alloc** — nếu không, khoảnh khắc "sướng nhất" của game chính là khoảnh khắc GC spike. |
| **Ranh giới** | Service **nhận** hệ số cao độ, **không tính** ramp. Danh mục clip thuộc **game**. Service chỉ lo: chọn clip · throttle · cấp voice · áp tham số. |
| **Hướng mở rộng thật** | Chắc chắn cần: music + crossfade → **thêm method** ⇒ rẻ, để lại. |
| **Cố ý KHÔNG làm + lý do** (NT 6: *xóa đi thì hỏng ở đâu*) | ① **`PlaySfxAt` + `spatialBlend`** (SFX 3D) — tiếng combo là UI 2D, không caller nào hỏng; cắt nó làm voice không cần GameObject riêng ⇒ xoá được cả một class. ② **`EAudioSelectMode` + mode `Sequential`** — không caller; cắt xong enum còn 1 giá trị nên cắt luôn enum, cùng với mảng `_cursors` và `NextSequential`. Chọn clip giờ **luôn** là random-không-trùng. ③ **`IAudioSettings`** — 0 implementation; `IsSfxOn` vẫn còn (field), game set trực tiếp. ④ Ngẫu nhiên **âm lượng** — cạnh ngẫu nhiên cao độ thì gần như không cảm được. ⑤ Addressables cho **từng clip** — SFX phải phát **đúng frame** sự kiện; `await` ở đó là trễ thấy được. Game muốn lazy thì nạp **cả catalog SO** như một khối. ⑥ Music/crossfade, `PauseAll/ResumeAll`, mixer group. |
| **Chỗ duy nhất phòng xa** | `PlaySfx` có `pitchScale` **ngay từ đầu** — thêm sau là đổi chữ ký ở **mọi** call-site (khác các mục bị cắt, đều chỉ là thêm method mới). |

---

## §0. Ba điều cần biết + số nào là số cảm giác

### 0.1. Gộp cao độ = **một phép nhân**

`AudioSource.pitch` là **tốc độ phát** ⇒ tác động lên tần số theo **tỉ lệ**, không theo hiệu. Thang semitone (`ratio = 2^(n/12)`) đã hiện thực và kiểm mốc trong `AudioPitchHelper` — plan này **không** làm lại (NT 4).

Điều duy nhất cần chốt: một tiếng chịu **hai** nguồn lệch cao độ (jitter của catalog, ramp của caller). Vì `2^(a/12) × 2^(b/12) = 2^((a+b)/12)`, **nhân hai ratio tương đương cộng hai lượng semitone** ⇒ gộp bằng một phép nhân là đúng thang nhạc.

```
pitch = catalogJitterRatio * pitchScale      rồi clamp về [0.05, 3]
```

**Phép kiểm tái lập:** `jitter = 0` (ratio 1) + `pitchScale = GetRampedPitch(12, 1, 12)` = `2^(12/12)` = `2` ⇒ `pitch == 2`, đúng một quãng tám. Nếu ai viết phép **cộng**: `1 + 2 = 3` ⇒ vọt hơn một quãng tám rưỡi — sai lộ ra ngay ở mốc này.

Kẹp sàn `0.05`: Unity nhận `[-3,3]` nhưng `pitch ≤ 0` làm clip không phát/phát ngược, **và** làm mốc rảnh của voice (§0.2) vô nghĩa.

### 0.2. Thời lượng phát phụ thuộc pitch

`pitch` là tốc độ ⇒ `t_play = clip.length / pitch`. Voice cần biết khi nào rảnh; dùng `clip.length` trần là sai theo đúng tỉ lệ pitch:

| `pitch` | `t_play` thật (clip 0.4s) | Nếu dùng `clip.length` |
|---|---|---|
| 2.0 (ramp cao) | 0.20s | giữ voice thừa 0.2s ⇒ hết voice sớm, tiếng sau bị cướp |
| 0.5 | 0.80s | trả voice sớm ⇒ **cắt tiếng giữa câu** |

Vẫn kiểm `!isPlaying` như lưới thứ hai (clip có tail/reverb dài hơn `length`).

### 0.3. Throttle theo **clip**, không theo `AudioId`

20 mảnh nổ cùng frame phát cùng một clip 20 lần lệch ~0ms: biên độ cộng dồn (to + méo) và pha gần trùng gây tiếng "xẹt". Bỏ qua lần phát nếu clip đó vừa phát trong `minInterval`, biên **đóng** (`≥ minInterval` thì phát — biên mở làm nhịp đều đặn bị bỏ ngẫu nhiên).

Khoá theo **clip** vì một `AudioId` chọn ra clip khác nhau mỗi lần, và **hai clip khác nhau phát cùng lúc thì không chói** — đó là điều ta muốn.

### 0.4. Số cảm giác — chọn bằng tai

Theo NT 7: những số dưới đây **chọn bằng tai**, không dẫn ra từ đâu. Chúng là điểm khởi đầu.

| Số | Khởi đầu | Tune ở đâu |
|---|---|---|
| `PitchJitterSemitones` | ±0.5 | asset `AudioCatalog`, per-entry |
| `MinIntervalSeconds` (throttle) | 0.05s | asset `AudioCatalog`, per-entry |
| `Volume` | 1.0 | asset `AudioCatalog`, per-entry |
| Số voice | 12 | mảng `Voices` trong Inspector |
| `SemitonesPerStep` / `MaxSemitones` của ramp | +1 / 12 | `AudioPitchRampChannel` (`FeedbackSystem.md`) |

Cách tune: sửa số trong asset, nhấn Play. **Không** sửa code (§C.1).

---

## Bản đồ triển khai

| Task | File | Nội dung |
|---|---|---|
| 1 | `Abstractions/Foundations/Audio/` — `AudioId.cs` · `IAudioCatalog.cs` · `IAudioService.cs` | contract |
| 2 | `Implementations/Foundations/Audio/` — `AudioEntry.cs` · `AudioCatalogSO.cs` | dữ liệu game điền + **asset** |
| 3 | `Implementations/Foundations/Audio/AudioService.cs` | voice authored + throttle + pitch |

Thứ tự: **1 → 2 → 3**.

---

### Task 1: 3 contract

**Files:** 3 file trong `Assets/Horcrux/Runtime/Abstractions/Foundations/Audio/`

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| `AudioId` wrap `int`, **không** `string` | Gõ tay `"sfx_combo"` mỗi call-site: typo nổ lúc runtime, không refactor-rename được, và hash chuỗi mỗi lần tra |
| `AudioId : IEquatable<AudioId>` | Không có nó, `Dictionary<AudioId,_>` rơi về comparer của object ⇒ **boxing mỗi lần tra**. Ở 20 lần/giây (hot path đã xác nhận) đó là 20 rác/giây |
| `GetHashCode() => Value` | `int` **là** hash tốt nhất của chính nó |
| `IAudioEntry` là **interface** | SDK không được biết game lưu dữ liệu này bằng gì — SO, payload remote, procedural đều cắm được (D) |
| Catalog tra **2 bước** (`TryGetEntryIndex` → `GetEntry`) | Service cần **chỉ số ổn định** để giữ state chọn clip trong mảng song song — state đó không được nằm trong asset |
| `PitchJitterSemitones` là **một** float (±) | Lệch quanh gốc là ca duy nhất thực dùng |
| `MinIntervalSeconds` **per-entry** | Tiếng "tick" UI cần chặn dày; tiếng vỡ cần cho chồng. Một số toàn cục sai cho cả hai |
| Tên `pitchScale`, không `pitch` | Nói rõ đây là **hệ số nhân** gộp với jitter, không phải giá trị cuối |

- [ ] **Step 1: `AudioId.cs`**

```csharp
using System;

namespace Horcrux.Runtime.Abstractions.Audio
{
    /// <summary>Định danh một mục âm thanh. Wrap <c>int</c> để khoá logic ở call-site có kiểu.</summary>
    /// <remarks>
    /// Vì sao KHÔNG <c>string</c>: gõ tay <c>"sfx_combo_hit"</c> ở mỗi chỗ dùng khiến typo chỉ nổ lúc
    /// runtime, không refactor-rename được, và hash chuỗi mỗi lần tra là chi phí thuần.
    ///
    /// Vì sao PHẢI implement <see cref="IEquatable{T}"/>: không có nó, <c>Dictionary&lt;AudioId,_&gt;</c>
    /// rơi về comparer mặc định của object ⇒ BOXING mỗi lần tra. Ở 20 lần/giây đó là 20 rác/giây sinh
    /// ra đúng lúc cần mượt nhất.
    ///
    /// Game khai <c>enum GameSfx { ComboHit = 1, … }</c> rồi truyền <c>(int)GameSfx.ComboHit</c>.
    /// </remarks>
    public readonly struct AudioId : IEquatable<AudioId>
    {
        public readonly int Value;

        /// <param name="value">Giá trị định danh; <c>0</c> dành riêng cho "chưa gán".</param>
        public AudioId(int value) => Value = value;

        public static implicit operator AudioId(int value) => new(value);

        /// <summary><c>0</c> = chưa gán — dùng để bắt field quên điền trong Inspector.</summary>
        public bool IsValid => Value != 0;

        public bool Equals(AudioId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is AudioId other && Equals(other);
        public override int GetHashCode() => Value;              // int LÀ hash tốt nhất của chính nó
        public override string ToString() => Value.ToString();   // chỉ dùng khi log lỗi (cold path)
    }
}
```

- [ ] **Step 2: `IAudioCatalog.cs` + `IAudioService.cs`**

```csharp
// ── IAudioCatalog.cs ──────────────────────────────────────────────────────
using System.Collections.Generic;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Audio
{
    /// <summary>Dữ liệu authoring của MỘT mục âm thanh.</summary>
    /// <remarks>
    /// Là interface để SDK không phụ thuộc việc game lưu dữ liệu này bằng gì — ScriptableObject,
    /// payload remote, hay sinh procedural đều cắm được (D trong SOLID).
    /// </remarks>
    public interface IAudioEntry
    {
        AudioId Id { get; }

        /// <summary>
        /// Các biến thể clip; service chọn ngẫu nhiên không trùng clip trước.
        /// <c>IReadOnlyList</c> chứ không <c>IEnumerable</c>: truy cập theo chỉ số và KHÔNG cấp phát enumerator.
        /// </summary>
        IReadOnlyList<AudioClip> Clips { get; }

        /// <summary>Âm lượng gốc, miền [0,1]. Số cảm giác — tune ở asset (§0.4).</summary>
        float Volume { get; }

        /// <summary>
        /// Lệch cao độ ngẫu nhiên ± bao nhiêu SEMITONE (đơn vị người thiết kế âm nghĩ bằng).
        /// <c>0</c> = tắt jitter. Số cảm giác (§0.4).
        /// </summary>
        float PitchJitterSemitones { get; }

        /// <summary>
        /// Cửa sổ tối thiểu giữa 2 lần phát CÙNG một clip, đơn vị GIÂY (§0.3).
        /// <c>0</c> = tắt throttle. Số cảm giác (§0.4).
        /// </summary>
        float MinIntervalSeconds { get; }
    }

    /// <summary>Danh mục âm thanh — do GAME điền. SDK không biết clip nào tồn tại.</summary>
    /// <remarks>
    /// Tra cứu chia 2 bước có chủ ý: service cần **chỉ số ổn định** để lưu state chọn clip trong một
    /// mảng song song — state đó KHÔNG được nằm trong asset (SO mutable làm dirty asset trong editor
    /// và rò state giữa các lần chơi).
    /// </remarks>
    public interface IAudioCatalog
    {
        int EntryCount { get; }

        /// <param name="index">Chỉ số ổn định trong catalog; dùng làm khoá cho state chọn clip ở service.</param>
        bool TryGetEntryIndex(AudioId id, out int index);

        /// <param name="index">Phải là chỉ số vừa lấy từ <see cref="TryGetEntryIndex"/>.</param>
        IAudioEntry GetEntry(int index);
    }
}

// ── IAudioService.cs ──────────────────────────────────────────────────────
namespace Horcrux.Runtime.Abstractions.Audio
{
    /// <summary>Facade phát SFX 2D. Nhiều tiếng đồng thời, cao độ điều khiển được, throttle theo clip.</summary>
    /// <remarks>
    /// <c>pitchScale</c> có mặt NGAY TỪ ĐẦU là quyết định phòng xa duy nhất của hệ này: thêm nó sau
    /// nghĩa là đổi chữ ký ở mọi call-site. Nó là HỆ SỐ NHÂN, gộp với jitter của catalog bằng một phép
    /// nhân (§0.1). Tính giá trị này bằng <c>AudioPitchHelper.GetRampedPitch</c> ở phía caller —
    /// service KHÔNG biết khái niệm "bậc combo".
    /// </remarks>
    public interface IAudioService : IService<IAudioService>
    {
        /// <summary>Cờ người chơi. Mặc định <c>true</c>; game set lại từ setting của nó lúc bootstrap.</summary>
        bool IsSfxOn { get; set; }

        /// <param name="id">Định danh mục trong catalog. Id lạ ⇒ no-op im lặng.</param>
        /// <param name="volumeScale">Hệ số nhân âm lượng, gộp với <c>Volume</c> của entry. Ai cấp: caller.</param>
        /// <param name="pitchScale">
        /// Hệ số nhân cao độ (không phải giá trị cuối). <c>1</c> = không đổi. Kết quả gộp bị kẹp về
        /// [0.05, 3] ở service.
        /// </param>
        void PlaySfx(AudioId id, float volumeScale = 1f, float pitchScale = 1f);

        /// <summary>Tắt mọi voice đang phát ngay. Được gọi khi <see cref="IsSfxOn"/> chuyển sang false.</summary>
        void StopAllSfx();
    }
}
```

- [ ] **Step 3: Kiểm chứng** — `((AudioId)7).Equals((AudioId)7) == true` · `((AudioId)0).IsValid == false` · tra `Dictionary<AudioId,int>` 1000 lần → GC Alloc **0 B** trong Profiler.

- [ ] **Step 4: Commit** — `feat(sdk): add audio contracts (AudioId + pitch-aware PlaySfx)`

---

### Task 2: `AudioEntry` + `AudioCatalogSO`

**Files:** `Assets/Horcrux/Runtime/Implementations/Foundations/Audio/AudioEntry.cs` · `AudioCatalogSO.cs`

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| `AudioEntry` là **class** (không struct) | Dữ liệu authoring do Unity serialize, sống suốt đời SO ⇒ không alloc theo tần số phát. Struct sẽ bị **copy** mỗi lần trả qua interface |
| `[SerializeField] private` + property get-only | Config do tác giả điền, runtime chỉ **đọc** |
| Dictionary dựng ở `OnEnable` | Chạy khi asset được nạp — trước mọi lần tra. Lazy-init thêm một phép kiểm null vào hot path |
| Log **error** khi `Id = 0` hoặc trùng | 2 lỗi author phổ biến nhất; im lặng thì tiếng không phát mà không ai biết vì sao (§C.1: lỗi phải lộ ra lúc authoring) |
| `Clips` trả `AudioClip[]` trực tiếp | Array implement `IReadOnlyList<T>` ⇒ không wrap, không alloc |

**Editor setup (§C.1):**

1. `Create → Horcrux → Audio Catalog` → đặt tên `AudioCatalog_Combo`, lưu ở thư mục asset của **game** (không trong `Assets/Horcrux/`).
2. Thêm 1 entry: `Id = 1` · kéo 2–3 clip "tick" ngắn (≤0.3s) vào `Clips`.
3. Console phải **không** có error. Nếu có, sửa `Id` tại chỗ.

- [ ] **Step 1: `AudioEntry.cs`**

```csharp
using System;
using System.Collections.Generic;
using Horcrux.Runtime.Abstractions.Audio;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Audio
{
    /// <summary>Dữ liệu authoring của một mục âm thanh. Điền trong Inspector của <see cref="AudioCatalogSO"/>.</summary>
    /// <remarks>
    /// Là <c>class</c> chứ không <c>struct</c> có chủ ý: dữ liệu do Unity serialize, sống suốt đời
    /// asset ⇒ không alloc theo tần số phát. Struct sẽ bị copy mỗi lần trả qua interface.
    /// </remarks>
    [Serializable]
    public sealed class AudioEntry : IAudioEntry
    {
        [SerializeField] private int id;

        [Tooltip("Nhiều clip ⇒ service chọn ngẫu nhiên, không trùng clip vừa phát.")]
        [SerializeField] private AudioClip[] clips = Array.Empty<AudioClip>();

        [Range(0f, 1f)]
        [SerializeField] private float volume = 1f;

        [Tooltip("Lệch cao độ ngẫu nhiên ± bao nhiêu SEMITONE (12 = một quãng tám). ~0.5 chống lặp âm nhàm.")]
        [SerializeField] private float pitchJitterSemitones = 0.5f;

        [Tooltip("Cửa sổ tối thiểu giữa 2 lần phát cùng clip (giây). 0 = cho chồng tự do.")]
        [SerializeField] private float minIntervalSeconds = 0.05f;

        public AudioId Id => new(id);
        public IReadOnlyList<AudioClip> Clips => clips;          // array implement IReadOnlyList → không wrap
        public float Volume => volume;
        public float PitchJitterSemitones => pitchJitterSemitones;
        public float MinIntervalSeconds => minIntervalSeconds;
    }
}
```

- [ ] **Step 2: `AudioCatalogSO.cs`**

```csharp
using System;
using System.Collections.Generic;
using Horcrux.Runtime.Abstractions.Audio;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Audio
{
    /// <summary>Danh mục SFX dạng asset — GAME điền, SDK không biết clip nào tồn tại.</summary>
    /// <remarks>
    /// Bảng tra dựng MỘT lần ở <c>OnEnable</c> (khi asset được nạp), không lazy: lazy thêm một phép
    /// kiểm null vào hot path phát tiếng.
    /// </remarks>
    [CreateAssetMenu(fileName = "AudioCatalog", menuName = "Horcrux/Audio Catalog")]
    public sealed class AudioCatalogSO : ScriptableObject, IAudioCatalog
    {
        [SerializeField] private AudioEntry[] entries = Array.Empty<AudioEntry>();

        private readonly Dictionary<AudioId, int> _indexById = new();

        public int EntryCount => entries.Length;

        private void OnEnable() => RebuildLookup();

        private void RebuildLookup()
        {
            _indexById.Clear();

            for (int i = 0; i < entries.Length; i++)
            {
                AudioEntry entry = entries[i];
                if (entry == null) continue;

                AudioId id = entry.Id;

                // Field quên điền là lỗi author phổ biến nhất — im lặng thì tiếng không phát mà
                // không ai biết vì sao.
                if (!id.IsValid)
                {
                    Debug.LogError($"[AudioCatalog] {name}: entry #{i} có Id = 0. Bỏ qua.", this);
                    continue;
                }

                // Id trùng: entry sau ghi đè entry trước ⇒ một tiếng "biến mất" bí ẩn.
                if (_indexById.ContainsKey(id))
                {
                    Debug.LogError($"[AudioCatalog] {name}: Id {id} trùng ở entry #{i}. Giữ entry đầu.", this);
                    continue;
                }

                _indexById.Add(id, i);
            }
        }

        public bool TryGetEntryIndex(AudioId id, out int index) => _indexById.TryGetValue(id, out index);

        public IAudioEntry GetEntry(int index) => entries[index];

#if UNITY_EDITOR
        // Đổi Id trong Inspector phải thấy hiệu lực ngay khi Play.
        private void OnValidate() => RebuildLookup();
#endif
    }
}
```

- [ ] **Step 3: Kiểm chứng**

| Input | Kỳ vọng |
|---|---|
| Catalog 3 entry id `1,2,3` | `TryGetEntryIndex(2, out i)` → `true`, `i == 1` |
| Entry có `id = 0` | log error **lúc chỉnh Inspector**, entry bị bỏ khỏi lookup |
| Hai entry cùng `id = 5` | log error, trả index của entry **đầu** |
| `TryGetEntryIndex(999)` | `false`, không throw |

- [ ] **Step 4: Commit** — `feat(sdk): add AudioCatalogSO`

---

### Task 3: `AudioService`

**Files:** Create `Assets/Horcrux/Runtime/Implementations/Foundations/Audio/AudioService.cs`

**Bản đồ toán → code:**

| §0 | Code |
|---|---|
| §0.1 gộp pitch | `pitch = jitterRatio * pitchScale`, rồi `clamp(MinPitch, MaxPitch)` |
| §0.1 semitone→ratio | `AudioPitchHelper.GetDetunedPitch(...)` — **không** viết lại `2^(n/12)` |
| §0.2 mốc rảnh | `_releaseTime[i] = now + clip.length / pitch` (pitch đã kẹp > 0) |
| §0.3 throttle | `now - last < entry.MinIntervalSeconds` → `return`, với `now = Time.unscaledTime` |

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| Voice là **mảng `AudioSource` gán trong Inspector** | §C.1: `AddComponent` cho thứ vốn tồn tại lúc authoring là đặt sai chỗ. Gán sẵn thì thiếu là **ô trống trong Inspector**, không phải bug giữa gameplay; và đổi số voice không cần compile |
| Helper `#if UNITY_EDITOR` tạo voice | Xóa nó thì author phải add 12 component bằng tay — tedious và dễ sai. Đây là **thao tác authoring** |
| `OnValidate` cưỡng chế `playOnAwake=false`, `loop=false`, `spatialBlend=0` | Ba cờ này sai là 3 lỗi im lặng khác nhau (tiếng nổ lúc load / tiếng kêu mãi / nghe như xa xăm). Sửa lúc authoring, không kiểm lại ở runtime |
| 2 mảng song song (`voices`, `_releaseTime`) | `_releaseTime` là state của service về voice, không phải thuộc tính của `AudioSource` ⇒ tách ra khỏi phải bọc class |
| Chọn voice: rảnh trước, **cướp cũ nhất** nếu hết | Bỏ tiếng mới là bỏ đúng cái người chơi vừa gây ra (ở combo cao, tiếng mới nhất quan trọng nhất). Cướp cái sắp xong ít gây chú ý nhất |
| Kiểm **cả** `_releaseTime` **và** `!isPlaying` | Mốc thời gian là dự tính (§0.2); `isPlaying` là sự thật của engine |
| `Time.unscaledTime` | Hitstop đặt `timeScale = 0`; dùng `Time.time` thì throttle đóng băng và mọi tiếng sau bị bỏ |
| `Dictionary<AudioClip, float>` cho throttle | Khoá theo **clip**, không `AudioId` (§0.3) |
| `int[] _lastClipIndices` song song với entry | State chọn clip ở **service**, không trong SO |
| Không log khi bị throttle | Bị throttle là hành vi **bình thường**; log ở đây là hàng chục dòng/giây |

**Editor setup (§C.1):**

1. Tạo GameObject `[Audio]` ở scene bootstrap → add `AudioService`.
2. Kéo asset `AudioCatalog_Combo` (Task 2) vào field `Catalog`.
3. Chuột phải component → **`Create SFX voices (12)`** → 12 `AudioSource` được add lên chính GameObject này và điền vào mảng `Voices`. (Hoặc add tay rồi kéo vào mảng.)
4. Kiểm: mảng `Voices` không còn ô `None`, Console không có error.

- [ ] **Step 1: `AudioService.cs`**

```csharp
using System;
using System.Collections.Generic;
using Horcrux.Runtime.Abstractions.Audio;
using Horcrux.Runtime.Utilities.AudioHelper;
using Sisus.Init;
using Unity.Mathematics;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Audio
{
    /// <summary>Phát SFX 2D: chọn clip → throttle theo clip → gộp cao độ → cấp voice. Zero alloc mỗi lần phát.</summary>
    /// <remarks>
    /// Ba trách nhiệm tách rõ trong ba vùng code: <c>SelectClip</c> · <c>IsThrottled</c> (§0.3)
    /// · <c>RentVoice</c> (§0.2). Chúng không biết nhau nên sửa một cái không ảnh hưởng hai cái kia.
    ///
    /// Voice là mảng <c>AudioSource</c> GÁN SẴN trong Inspector (không <c>AddComponent</c> lúc runtime
    /// — §C.1): thiếu thì lộ ra ô trống lúc authoring, và đổi số voice không cần compile.
    /// Hệ này chỉ phát 2D nên tất cả voice ở chung một GameObject là đủ.
    /// </remarks>
    [Service(typeof(IAudioService), FindFromScene = true)]
    public sealed class AudioService : MonoBehaviour, IAudioService
    {
        // Unity nhận pitch ∈ [-3,3], nhưng ≤ 0 làm clip không phát/phát ngược VÀ làm mốc rảnh
        // (length / pitch) vô nghĩa (§0.1, §0.2). Sàn 0.05 ≈ thấp hơn ~4.3 quãng tám — quá đủ.
        private const float MinPitch = 0.05f;
        private const float MaxPitch = 3f;

        [SerializeField] private AudioCatalogSO catalog;

        [Tooltip("Số tiếng phát đồng thời tối đa = độ dài mảng. Hết voice thì tiếng mới CƯỚP voice cũ nhất.")]
        [SerializeField] private AudioSource[] voices = Array.Empty<AudioSource>();

        private float[] _releaseUnscaledTime;
        private int[] _lastClipIndices;                       // chống trùng clip liên tiếp, theo entry index
        private Dictionary<AudioClip, float> _lastPlayByClip;

        /// <summary>Mặc định bật; game set lại từ setting của nó lúc bootstrap.</summary>
        public bool IsSfxOn
        {
            get => _isSfxOn;
            set
            {
                _isSfxOn = value;
                if (!value) StopAllSfx();     // tắt giữa lúc đang phát ⇒ im NGAY
            }
        }

        private bool _isSfxOn = true;

        private void Awake()
        {
            DontDestroyOnLoad(this);

            // Cấu hình sai lộ ra ở đây một lần, không nổ rải rác giữa gameplay.
            if (voices.Length == 0)
                Debug.LogError("[Audio] Mảng Voices rỗng — dùng context-menu 'Create SFX voices'.", this);

            if (catalog == null)
                Debug.LogError("[Audio] Chưa gán AudioCatalog — mọi lệnh PlaySfx sẽ bị bỏ qua.", this);

            // Toàn bộ cấp phát ở đây — sau Awake, đường phát tiếng là 0 alloc.
            _releaseUnscaledTime = new float[voices.Length];

            int entryCount = catalog != null ? catalog.EntryCount : 0;
            _lastClipIndices = new int[entryCount];
            for (int i = 0; i < entryCount; i++) _lastClipIndices[i] = -1;   // −1 = chưa dùng clip nào

            _lastPlayByClip = new Dictionary<AudioClip, float>(entryCount > 0 ? entryCount : 8);
        }

        public void PlaySfx(AudioId id, float volumeScale = 1f, float pitchScale = 1f)
        {
            if (!_isSfxOn || catalog == null || voices.Length == 0) return;
            if (!catalog.TryGetEntryIndex(id, out int entryIndex)) return;   // id lạ: im lặng, không throw

            IAudioEntry entry = catalog.GetEntry(entryIndex);
            AudioClip clip = SelectClip(entry, entryIndex);
            if (clip == null) return;

            float now = Time.unscaledTime;                                   // hitstop đặt timeScale = 0
            if (IsThrottled(clip, entry.MinIntervalSeconds, now)) return;
            _lastPlayByClip[clip] = now;

            float pitch = ResolvePitch(entry, pitchScale);
            int voiceIndex = RentVoice(now);

            AudioSource source = voices[voiceIndex];
            source.clip = clip;
            source.volume = entry.Volume * volumeScale;
            source.pitch = pitch;
            source.Play();

            // §0.2: pitch là tốc độ phát ⇒ thời lượng thực = length / pitch.
            _releaseUnscaledTime[voiceIndex] = now + clip.length / pitch;
        }

        public void StopAllSfx()
        {
            for (int i = 0; i < voices.Length; i++)
            {
                if (voices[i] == null) continue;

                voices[i].Stop();
                voices[i].clip = null;                  // nhả tham chiếu clip
                _releaseUnscaledTime[i] = 0f;
            }

            // Mốc cũ sẽ làm tiếng đầu sau khi bật lại bị bỏ oan.
            _lastPlayByClip.Clear();
        }

        /// <summary>Gộp cao độ: jitter của catalog × hệ số của caller (§0.1).</summary>
        /// <param name="pitchScale">Hệ số nhân từ caller; kết quả gộp bị kẹp về [MinPitch, MaxPitch].</param>
        private static float ResolvePitch(IAudioEntry entry, float pitchScale)
        {
            float jitter = entry.PitchJitterSemitones;

            // GetDetunedPitch nhận signedUnit ∈ [-1,1] rồi nhân với biên độ semitone → ra ratio.
            float jitterRatio = jitter > 0f
                ? AudioPitchHelper.GetDetunedPitch(UnityEngine.Random.Range(-1f, 1f), jitter)
                : 1f;

            // Nhân 2 ratio ≡ cộng 2 lượng semitone (§0.1) → một phép nhân là đúng thang nhạc.
            return math.clamp(jitterRatio * pitchScale, MinPitch, MaxPitch);
        }

        /// <summary>Bỏ qua nếu clip này vừa phát trong cửa sổ tối thiểu (§0.3).</summary>
        /// <param name="minInterval">Giây; <c>0</c> = tắt throttle.</param>
        /// <param name="now">Phải là <c>Time.unscaledTime</c> — xem bảng quyết định.</param>
        private bool IsThrottled(AudioClip clip, float minInterval, float now)
        {
            if (minInterval <= 0f) return false;                          // 0 = tắt, không cần cờ riêng
            if (!_lastPlayByClip.TryGetValue(clip, out float last)) return false;

            return now - last < minInterval;                               // biên ĐÓNG
        }

        /// <summary>Voice rảnh đầu tiên; hết thì CƯỚP voice có mốc rảnh sớm nhất (§0.2).</summary>
        /// <remarks>
        /// Cướp thay vì bỏ tiếng mới: ở combo cao, tiếng mới nhất là tiếng người chơi vừa gây ra —
        /// bỏ nó là bỏ đúng phản hồi quan trọng nhất. Voice sắp xong thì cắt ít gây chú ý nhất.
        /// </remarks>
        private int RentVoice(float now)
        {
            int oldestIndex = 0;
            float oldestRelease = float.MaxValue;

            for (int i = 0; i < voices.Length; i++)
            {
                if (voices[i] == null) continue;                  // ô trống trong Inspector

                // Hai lưới: mốc thời gian là dự tính, isPlaying là sự thật của engine.
                if (now >= _releaseUnscaledTime[i] && !voices[i].isPlaying) return i;

                if (_releaseUnscaledTime[i] < oldestRelease)
                {
                    oldestRelease = _releaseUnscaledTime[i];
                    oldestIndex = i;
                }
            }

            return oldestIndex;
        }

        /// <summary>Chọn ngẫu nhiên một clip, KHÔNG trùng clip vừa phát của entry đó.</summary>
        /// <remarks>
        /// KHÔNG dùng <c>do { i = rand(); } while (i == last);</c> — lặp VÔ HẠN khi n = 1 và số vòng
        /// không tất định khi n nhỏ. Cách này rút trong (n−1) chỗ rồi "chèn qua" vị trí last: luôn
        /// đúng MỘT lần rút.
        /// </remarks>
        private AudioClip SelectClip(IAudioEntry entry, int entryIndex)
        {
            IReadOnlyList<AudioClip> clips = entry.Clips;
            int n = clips.Count;
            if (n == 0) return null;
            if (n == 1) return clips[0];                      // không có gì để tránh trùng

            int last = _lastClipIndices[entryIndex];
            int index;

            if (last < 0)
            {
                index = UnityEngine.Random.Range(0, n);        // chưa từng phát → tự do
            }
            else
            {
                int r = UnityEngine.Random.Range(0, n - 1);
                index = r >= last ? r + 1 : r;
            }

            _lastClipIndices[entryIndex] = index;
            return clips[index];
        }

#if UNITY_EDITOR
        private const int DefaultVoiceCount = 12;

        /// <summary>Tạo sẵn voice lúc AUTHORING — thay việc add 12 component bằng tay (§C.1).</summary>
        [ContextMenu("Create SFX voices (12)")]
        private void CreateVoices()
        {
            var created = new AudioSource[DefaultVoiceCount];

            for (int i = 0; i < DefaultVoiceCount; i++)
            {
                created[i] = UnityEditor.Undo.AddComponent<AudioSource>(gameObject);
                ConfigureVoice(created[i]);
            }

            voices = created;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// Cưỡng chế 3 cờ lúc authoring. Sai một cờ là một lỗi im lặng khác nhau:
        /// <c>playOnAwake</c> → tiếng nổ lúc load; <c>loop</c> → tiếng kêu mãi;
        /// <c>spatialBlend &gt; 0</c> → SFX 2D nghe như xa xăm.
        /// </summary>
        private void OnValidate()
        {
            for (int i = 0; i < voices.Length; i++)
            {
                if (voices[i] != null) ConfigureVoice(voices[i]);
            }
        }

        private static void ConfigureVoice(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;      // hệ này chỉ phát 2D
        }
#endif
    }
}
```

- [ ] **Step 2: Kiểm chứng**

| # | Input | Kỳ vọng |
|---|---|---|
| 1 | `PlaySfx(id)` khi `IsSfxOn = false` | không phát |
| 2 | `PlaySfx(id lạ)` | im lặng, không throw |
| 3 | Chưa gán catalog / mảng Voices rỗng | log error 1 lần ở `Awake`, mọi lệnh no-op |
| 4 | `jitter = 0`, `pitchScale = 1` | `AudioSource.pitch == 1` |
| 5 | `pitchScale = GetRampedPitch(12, 1, 12)` | `pitch == 2` (một quãng tám) — §0.1 |
| 6 | `pitchScale = 0` (caller sai) | `pitch == 0.05`, clip **vẫn** phát, mốc rảnh hữu hạn |
| 7 | Clip 0.4s, `pitch = 2` | voice rảnh sau **0.2s** — §0.2 |
| 8 | 20 lần cùng id cùng frame, `minInterval = 0.05` | phát **1** lần — §0.3 |
| 9 | 20 lần với `minInterval = 0` | phát 12 lần rồi cướp voice, không alloc |
| 10 | Entry 1 clip | luôn trả clip đó, **không treo** |
| 11 | Entry 3 clip, 100 lần | không bao giờ trùng 2 lần liên tiếp |
| 12 | `timeScale = 0`, 2 lần cách 0.1s | throttle hoạt động đúng |
| 13 | Bật `playOnAwake` trên một voice rồi rời Inspector | `OnValidate` tắt lại ngay |
| 14 | Profiler: 20 lần phát/giây | **0 B** GC Alloc |

- [ ] **Step 3: Cập nhật `PendingSystems.md` §8** — trỏ plan này và ghi rõ 3 điểm lệch: `pitchScale` thêm vào chữ ký · không dùng `IPoolManager` · phân loại lại thành `Foundation`.

- [ ] **Step 4: Commit** — `feat(sdk): add AudioService (authored voices + throttle + pitch scale)`

---

## Ghi chú thực thi

- **Hệ dùng tiếp:** `FeedbackSystem.md` → `AudioPitchRampChannel`.
- **Tune:** mọi số cảm giác nằm trong asset `AudioCatalog` + Inspector của `AudioPitchRampChannel` (§0.4). Không sửa code.
- **Mở rộng sau** (đều **additive**): `PlaySfxAt` + `spatialBlend` (SFX 3D — lúc đó voice cần GameObject riêng) · `PlayMusicAsync`/`StopMusicAsync` + crossfade · `PauseAll`/`ResumeAll` (tách hẳn khỏi on/off — trộn hai cái là bug kinh điển "hết ads nhạc không trở lại") · `EAudioSelectMode` (thêm `Sequential`/`RandomWeighted`) · `IAudioSettings` để lưu bền `IsSfxOn` · `AudioMixerGroup` per-entry · nạp catalog qua `AssetReference`.
