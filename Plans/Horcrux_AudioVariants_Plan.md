# Plan — AudioSystem `variants`, `startMusic`: một tiếng nhiều clip rút theo túi xáo, nhạc phát khi host dựng xong

Ngày 2026-11-23. Nguồn: category-jam `Assets/_Game/Scripts/Feature/CoinSystem/ButtonCoin.cs` (`CoinSpawnSfxs`, `ShuffledSpawnSfxs`:
6 id cho một tiếng, xáo lại mỗi lượt coin bay để một lượt không trùng tiếng), `Assets/_CategoryJam/Scripts/Feedback/GameAudioService.cs`
(`Start` phát nhạc nền suốt phiên), đang chạy trong dự án. Hai cơ chế độc lập trong `docs/Runtime/AudioSystem`: **A** một entry nhiều
clip, mỗi lần phát rút một clip theo túi xáo, caller mở loạt mới bằng `RestartSfxVariants` · **B** bài nhạc phát khi host dựng xong.

**Phân công:** GĐ1–3 developer gõ, agent viết test theo bảng case rồi compile · GĐ4 (Editor của catalog, tài liệu) · GĐ5 (dự án chuyển
sang bản Horcrux, xoá cơ chế xáo của dự án, xoá Plan) agent làm.
**Chạy được, thấy được:** GĐ1–3 compile sạch + bảng case xanh trong `Tests/EditMode/ShuffleBagTests.cs` và
`Tests/PlayMode/AudioServiceTests.cs`; nghe trong game ở GĐ5.
**Lệnh kiểm** (chạy từ gốc dự án): `bash "Assets/Horcrux/Editor/CompileCheck/Tools~/verify.sh" --csproj
com.horcrux.runtime.csproj,CategoryJam.Gameplay.csproj,Assembly-CSharp.csproj --tests Horcrux.EditModeTests,CategoryJam.EditModeTests`.
Lệnh này chỉ chạy EditMode; `AudioServiceTests` là PlayMode: Editor đang mở thì developer chạy ở **Window › General › Test Runner ›
PlayMode › Horcrux.PlayModeTests › AudioServiceTests › Run Selected**; không Editor nào mở project thì chạy nền
`Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testFilter AudioServiceTests -testResults <xml> -logFile <log>`
và đọc `test-run` total/passed/failed trong XML.
**Commit:** GĐ1–4 trong `Assets/Horcrux`; GĐ5 ở dự án kèm con trỏ submodule.
**Phụ thuộc:** `Horcrux_SlotShuffle_Plan.md` › GĐ1 — `FisherYates.Shuffle<T>(Span<T>, Func<int, int>)` trong
`Runtime/Utilities/Permutation/FisherYates.cs`, viết như đã gõ.
**Đã kiểm trước khi ghi:** csc (Roslyn của Rider, `-langversion:9`, `netstandard.dll` 2.1 và module `UnityEngine` của Unity
6000.0.73f1, `com.horcrux.runtime.dll` trong `Library/ScriptAssemblies`) compile sạch cả thư mục `Foundations/Audio` sau khi áp GĐ1–3,
cùng `ShuffleBag.cs` và `FisherYates.cs`, hai lượt: không define và `UNITY_EDITOR` (cả hai file `.Editor.cs` của thư mục).
Bản mẫu C# của `ShuffleBag` chạy trên mono của Unity, nguồn `System.Random(seed).Next`: `amount` 6, seed 1–200, 60 lần rút — mỗi khối
6 đủ mặt, không hai lần liền trùng, 0 sai; đối chứng bỏ khối đổi chỗ ở ranh: trùng liền ở 152/200 seed (seed đầu tiên: 1). `Restart`
giữa lượt, `amount` 1 và 2, số lần gọi nguồn mỗi lượt (5 hoặc 6) đúng ở 200 seed. `§0`: không — xáo là `FisherYates` của SlotShuffle,
phần riêng chỉ là một phép đổi chỗ ở ranh lượt, ghi ở GĐ1.

## Ngữ cảnh đã chốt

- **Người dùng:** tiếng lặp dày cần đổi clip để tai không mỏi (vật hiện ra, bước chân, nhặt đồ, va chạm) · game có nhạc nền suốt phiên
  (host audio ở scene sống suốt phiên).
- **Mục tiêu:** một id mang nhiều clip; mỗi lần phát rút một clip theo **túi xáo** — hết túi mới lặp, lượt mới không mở bằng clip vừa
  phát. Caller mở **một loạt** bằng `RestartSfxVariants(id)`: các lần phát sau rút từ túi vừa xáo, nên một loạt không dài hơn số clip
  không trùng tiếng nào. Nhạc nền: một ô Inspector trên host, phát khi host dựng xong.
- **Ranh giới:** entry không có variant phát y như cũ (không cấp phát thêm, không rút ngẫu nhiên); catalog cũ không phải sửa —
  `variants` là **field mới** cạnh `clip`, không đổi `clip` thành mảng (đổi là đổi wire format của mọi catalog đã author); `startMusic`
  mặc định `0` = không phát gì, host cũ chạy như cũ. API công khai mới: `IAudioService.RestartSfxVariants`, `ShuffleBag`.
- **Chỗ đặt, tên:** `ShuffleBag` ở `Runtime/Utilities/Permutation/ShuffleBag.cs`, namespace `Horcrux.Runtime.Utilities.Permutation` —
  túi xáo chỉ số không mang gì của audio, dùng được cho chọn level, chọn câu thoại · phần còn lại sửa file có sẵn của AudioSystem.
  `AudioEntry.ClipAmount`, `ClipAt` là `internal`: người gọi là `AudioService` và `AudioCatalog.Editor.cs`, cùng assembly
  `com.horcrux.runtime`; Horcrux không có `InternalsVisibleTo` nào và test không cần hai member này. `variants`, `ClipAmount`, `ClipAt`,
  `ShuffleBag`, `RestartSfxVariants`, `startMusic` — tên chốt khi gõ.
- **Cố ý không làm:** throttle khoá theo clip (lý do GĐ1) · chọn tuần tự, theo trọng số (chưa người dùng) · volume, pitch riêng từng
  variant (clip nào lệch thì chuẩn hoá file — nút **Audit Loudness** chỉ ra) · `maxInstances` · crossfade nhạc khởi động · đối tượng
  "loạt" riêng cho caller giữ (lý do GĐ2).
- **Quyết định trái trực giác:** service không tự biết lúc nào một loạt bắt đầu — caller gọi `RestartSfxVariants`. Túi chạy liên tục
  không lệnh mở loạt thì loạt bị huỷ giữa chừng để lại túi dở, loạt sau trùng tiếng · nhạc phát ở `Awake` của base, không ở `Start`:
  class con của dự án hay khai `private void Start` riêng, che magic method cùng tên của base mà không cảnh báo.
- **Mở rộng sau:** chế độ chọn khác thì thêm `enum` chọn trên entry, mặc định là túi xáo · `ClipAmount`, `ClipAt` lên `public` khi có
  người gọi ngoài assembly.

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| category-jam `ButtonCoin.ShuffledSpawnSfxs` | nhu cầu: một lượt bay không trùng tiếng, lượt sau thứ tự khác | mảng clone mỗi lượt (cấp phát mỗi tương tác); 6 id cho một tiếng |
| category-jam `GameAudioService.Start` | phát bài ở cấu hình host | `private void Start` ở class con che magic method cùng tên của base nếu base có |
| category-jam `Bridge/GameplayAudioToggle.cs` | cờ Music đọc từ save đặt qua `IAudioSetting.IsMusicOn` lúc `Init`, sau hoặc trước `Awake` của host | — |
| `AudioService.Play` | điểm rút clip ngay sau throttle, `busyUntil` theo clip đã rút | — |
| `AudioEntry`, `AudioCatalog.Editor.cs` (`IsEntryValid`, `AuditLoudness`) | chỗ thêm field, chỗ kiểm variant | — |
| `docs/Runtime/AudioSystem/AudioSystem.md` › Chưa làm, Bẫy | "nhiều clip cho một tiếng" đã tính sẵn là additive | dòng "đổi throttle sang khoá theo clip" — xem GĐ1 |
| `Horcrux_SlotShuffle_Plan.md` › GĐ1 `FisherYates.Shuffle` | hàm xáo, thư mục `Permutation/`, khuôn `Func<int, int> nextIndex` | `SlotShuffle.Assign` là bài toán khác (xếp vào slot theo nhóm) |
| `Horcrux_ProgressDifficulty_Plan.md` › `RandomPick` (`Utilities/Picking/`) | cùng khuôn `nextIndex` | chọn một phần tử, không nhớ đã rút gì — túi cần nhớ |
| `FeelingPlan.md` › `RandomHelper` (lộ trình) | — | chưa có Plan, chưa API; phần "shuffle" của nó là `FisherYates.Shuffle` |

## Luồng

```
authoring: entry có Clip + variants[] ───────────────────────────────────────────────────────────── GĐ1
host Awake: BuildTables → mỗi entry ClipAmount > 1 dựng một ShuffleBag(ClipAmount) ─────────────── GĐ1
caller PlaySfx(id) ── throttle ──► bag.Draw(NextIndex) → entry.ClipAt(i) ──► voice (busyUntil theo clip đó)   GĐ1
caller mở loạt: RestartSfxVariants(id) → túi của id coi như rỗng ──────────────────────────────── GĐ2
host Awake (cuối): startMusic ≠ 0 → PlayMusic(startMusic) ────────────────────────────────────── GĐ3
Editor: Validate báo variant trống, Audit đo từng variant · tài liệu ────────────────────────────── GĐ4 (agent)
dự án: tiếng coin một id 6 clip, mở loạt mỗi lượt bay; nhạc nền qua startMusic ─────────────────── GĐ5 (agent)
```

## Bản đồ hệ cuối

```
  caller ──PlaySfx / RestartSfxVariants──► AudioService ──TryGetEntryById──► AudioCatalog ──► AudioEntry (Clip, variants)
                                              ├─ _bagById: TSfx → ShuffleBag (chỉ entry nhiều clip)
                                              │     ShuffleBag.Draw(NextIndex) ──► FisherYates.Shuffle (SlotShuffle Plan)
                                              │     NextIndex = Random.Range(0, n), static
                                              ├─ throttle theo entry (như cũ) · voice (như cũ)
                                              └─ startMusic ──Awake──► PlayMusic
  Editor: AudioCatalog.Editor.cs — Validate Entries (variant null), Audit Loudness (một dòng mỗi clip)
```

---

## GĐ1 — Một entry nhiều clip, rút theo túi xáo

**Mục tiêu:** entry có `variants` thì mỗi lần phát (không bị throttle) rút một clip theo túi: hết túi mới xáo lại, lượt mới không mở
bằng clip vừa phát. Xong: catalog nhiều clip nghe đổi tiếng; catalog cũ nghe y như cũ.

**Vì sao bây giờ:** là cơ chế cả Plan dựa vào; `RestartSfxVariants` (GĐ2) chỉ là một lệnh lên túi này.

**Sơ đồ sau GĐ1** (`+` là phần mới)

```
  caller ──PlaySfx──► AudioService ──throttle──► _bagById+ ? entry.ClipAt+(bag.Draw+(NextIndex+)) : entry.Clip ──► voice
                                                               └──► FisherYates.Shuffle (đã có, SlotShuffle GĐ1)
  Awake ──ClipAmount+ > 1──► new ShuffleBag+
```

### `Runtime/Implementations/Foundations/Audio/AudioService.cs` › `using`

Thêm `using Horcrux.Runtime.Utilities.Permutation;` sau `using Horcrux.Runtime.Utilities;`.

### `AudioService.cs` › field, sau `private float[] _voicesBusyUntil = Array.Empty<float>();`

```csharp
        private readonly Dictionary<TSfx, float> _lastPlayTimeById = new();
        private float[] _voicesBusyUntil = Array.Empty<float>();
        // Túi chỉ cho entry nhiều clip: entry một clip không tra, không rút.
        private readonly Dictionary<TSfx, ShuffleBag> _bagById = new();
        // Static: không bắt biến nên không cấp phát mỗi lần rút.
        private static readonly Func<int, int> NextIndex = static amount => Random.Range(0, amount);
```

`Random` trong file là `UnityEngine.Random` (alias `using Random = UnityEngine.Random;` có sẵn).

### `AudioService.cs` › `Unity Callbacks` › `Awake` — vòng điền `_lastPlayTimeById` cũ → mới

Cũ:

```csharp
            _lastPlayTimeById.Clear();
            for (int i = 0; i < catalog.Entries.Count; i++)
                _lastPlayTimeById[catalog.Entries[i].Id] = float.NegativeInfinity;
```

Mới:

```csharp
            _lastPlayTimeById.Clear();
            _bagById.Clear();
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                AudioEntry<TSfx> entry = catalog.Entries[i];
                _lastPlayTimeById[entry.Id] = float.NegativeInfinity;
                if (entry.ClipAmount > 1)
                    _bagById[entry.Id] = new ShuffleBag(entry.ClipAmount);
            }
```

### `AudioService.cs` › `Class Methods` › `Play` — `AudioClip clip = entry.Clip;` cũ → mới

Cũ:

```csharp
            float now = UnityEngine.Time.unscaledTime;
            if (now - _lastPlayTimeById[sfx] < entry.MinIntervalSeconds)
                return;
            
            AudioClip clip = entry.Clip;
```

Mới (chỉ dòng cuối đổi):

```csharp
            // Rút sau throttle: lần bị bỏ không tiêu clip của túi.
            AudioClip clip = _bagById.TryGetValue(sfx, out ShuffleBag bag) ? entry.ClipAt(bag.Draw(NextIndex)) : entry.Clip;
```

`busyUntil` phía dưới đã dùng `clip.length` của biến `clip`, nên tự theo clip vừa rút.

### `Runtime/Implementations/Foundations/Audio/AudioEntry.cs` › field, sau `minIntervalSeconds`

```csharp
        [SerializeField, Min(0f)] private float minIntervalSeconds = 0.03f;
        [Tooltip("Clip thêm cho cùng tiếng, rút xen với Clip theo túi xáo: hết một lượt mới lặp. Trống: luôn phát Clip.")]
        [SerializeField] private AudioClip[] variants = Array.Empty<AudioClip>();
```

### `AudioEntry.cs` › sau `public float MinIntervalSeconds => minIntervalSeconds;`

```csharp
        public float MinIntervalSeconds => minIntervalSeconds;

        /// <summary>Số clip của tiếng: Clip cộng các variant.</summary>
        internal int ClipAmount => 1 + variants.Length;

        /// <summary>Clip thứ index: 0 là Clip, từ 1 là variant thứ index − 1.</summary>
        internal AudioClip ClipAt(int index) => index == 0 ? clip : variants[index - 1];
```

### `Runtime/Utilities/Permutation/ShuffleBag.cs` — file mới

```csharp
using System;

namespace Horcrux.Runtime.Utilities.Permutation
{
    /// <summary>
    /// Túi xáo chỉ số 0..amount-1: rút hết một lượt mới xáo lại, lượt mới không mở bằng chỉ số vừa rút. Không cấp phát sau constructor.
    /// Caller đưa nguồn ngẫu nhiên: nextIndex(n) trả số trong [0, n).
    /// </summary>
    public sealed class ShuffleBag
    {
        private readonly int[] _order;
        private int _cursor;
        private int _lastDrawn = -1;

        public ShuffleBag(int amount)
        {
            _order = new int[amount];
            for (int i = 0; i < amount; i++)
                _order[i] = i;

            // Túi coi như rỗng: lần rút đầu tiên xáo.
            _cursor = amount;
        }

        public int Amount => _order.Length;

        /// <summary>Lần rút sau mở lượt mới dù lượt này còn chỉ số; lượt mới vẫn không mở bằng chỉ số vừa rút.</summary>
        public void Restart() => _cursor = _order.Length;

        public int Draw(Func<int, int> nextIndex)
        {
            if (_cursor == _order.Length)
                StartRound(nextIndex);

            _lastDrawn = _order[_cursor++];
            return _lastDrawn;
        }

        private void StartRound(Func<int, int> nextIndex)
        {
            FisherYates.Shuffle(_order.AsSpan(), nextIndex);

            // Ranh hai lượt: chỉ số vừa rút đổi chỗ với một chỗ khác ngẫu nhiên, tai không nghe hai lần liền một tiếng.
            if (_order.Length > 1 && _order[0] == _lastDrawn)
            {
                int j = 1 + nextIndex(_order.Length - 1);
                (_order[0], _order[j]) = (_order[j], _order[0]);
            }

            _cursor = 0;
        }
    }
}
```

| Consumes | Produces |
|---|---|
| `FisherYates.Shuffle` (SlotShuffle GĐ1) · `UnityEngine.Random.Range` | field `AudioEntry.variants` · `AudioEntry.ClipAmount`, `ClipAt` (internal) · `ShuffleBag` (public) · `AudioService._bagById`, `NextIndex` |

**Quyết định** — throttle vẫn khoá theo **entry**, không theo clip (dòng "phải đổi sang khoá theo clip" trong `AudioSystem.md` sửa ở
GĐ4): `minInterval` chặn dồn tiếng trong cùng vài frame; nhiều clip của một entry vẫn là một tiếng, hai lượt gối nhau cùng frame chỉ nên
ra một. Người dùng đầu phát cách nhau ≈ 0.117 s, xa hơn `minInterval` 0.03 s. — Túi dựng ở `Awake` trong cùng vòng điền throttle:
đường phát 0 B như cũ (`Dictionary` khoá enum, delegate static). — `Clip` giữ nguyên là clip thứ 0, không đổi thành mảng: key YAML `clip`
của mọi catalog đã author không đổi, entry một clip không phải sửa. Giá: variant thứ k nằm ở `ClipAt(k + 1)`, `ClipAt` là chỗ duy nhất
biết điều đó.

**Quyết định** — nguồn ngẫu nhiên là tham số của `Draw`: service truyền `UnityEngine.Random` (như jitter cao độ), test truyền
`System.Random(seed)`. — Xáo bằng `FisherYates.Shuffle` có sẵn, không viết vòng xáo riêng: một bản cho cả `SlotShuffle` và túi. — Đổi
chỗ ở ranh, không xáo lại tới khi khác: số lần gọi `nextIndex` cố định mỗi lượt (n − 1 hoặc n), test theo seed tất định. Phương án bỏ:
lặp xáo tới khi `_order[0] != _lastDrawn` — số lần gọi không biên trên. — `Amount = 0` không chặn: không caller nào dựng túi rỗng
(service chỉ dựng khi `ClipAmount > 1`), `Draw` trên túi rỗng ném `IndexOutOfRangeException` ngay lần đầu.

**Editor setup:** tạo `ShuffleBag.cs` trong Project window, trong thư mục `Permutation` SlotShuffle GĐ1 đã tạo (để có `.meta`).

**Kiểm:** compile sạch. Agent viết:

`Tests/EditMode/ShuffleBagTests.cs` (nguồn `new System.Random(seed).Next`):

| Case | Input | Kỳ vọng |
|---|---|---|
| một lượt đủ mặt | amount 6, rút 6 | đủ 0..5, không trùng |
| nhiều lượt | amount 6, seed 1…200, rút 60 | mỗi khối 6 liên tiếp đủ 0..5; không hai lần liền nhau trùng ở mọi ranh |
| `Restart` giữa lượt | amount 6, seed 1…200, rút 2, `Restart`, rút 6 | 6 lần sau đủ 0..5; lần đầu sau `Restart` ≠ lần rút ngay trước |
| amount 1 | rút 5 | luôn 0, `nextIndex` không bị gọi |
| amount 2 | seed 1…200, rút 40 | xen kẽ tuyệt đối 0, 1 (ranh không lặp) |
| số lần gọi nguồn | amount 6, seed 1…200, đếm lời gọi `nextIndex` mỗi lượt qua 10 lượt | 5 hoặc 6 |
| đối chứng | túi giống hệt nhưng bỏ khối đổi chỗ ở ranh (bản sao trong test) | case "nhiều lượt" đỏ ở seed 1 (bản mẫu: 152/200 seed) |

`Tests/PlayMode/AudioServiceTests.cs` (helper `Entry` thêm tham số `params AudioClip[] variants`, đặt field `variants` bằng reflection
như các field khác):

| Case | Input | Kỳ vọng |
|---|---|---|
| entry một clip | 20 lần `PlaySfx`, minInterval 0 | mọi voice nhận `entry.Clip`; `_bagById` không có khoá này |
| entry 3 clip | 30 lần, minInterval 0 | mỗi khối 3 liên tiếp đủ 3 clip, không hai lần liền trùng |
| throttle không tiêu túi | minInterval 1, gọi 2 lần cùng frame rồi chờ qua interval gọi 2 lần | 2 lần phát, hai clip khác nhau |
| `busyUntil` theo clip rút | variant dài gấp đôi clip | `busyUntil` = `now + length(clip vừa phát) / pitch` |
| không cấp phát | `PlaySfx` entry 3 clip trong khung `AllocationMeter` | 0 B (đối chứng: cùng khung bắt được một closure) |

**Mốc commit:** `feat(audio): một tiếng nhiều clip rút theo túi xáo`.

---

## GĐ2 — Caller mở một loạt: `RestartSfxVariants`

**Mục tiêu:** caller có loạt tiếng không được trùng (một lượt vật bay) gọi `RestartSfxVariants(id)` trước loạt; các lần phát sau rút từ
túi mới. Loạt trước bị huỷ giữa chừng không để lại túi dở cho loạt sau.

**Vì sao bây giờ:** túi GĐ1 chạy liên tục; chỉ thiếu mốc "bắt đầu loạt" mà caller biết còn service không biết.

**Sơ đồ sau GĐ2**

```
  caller ──RestartSfxVariants+(id)──► AudioService ──► ShuffleBag.Restart
         ──PlaySfx(id) × n──► (GĐ1) rút từ túi mới
```

### `Runtime/Abstractions/Foundations/Audio/IAudioService.cs` › sau `void PlaySfx(TSfx sfx, float pitchScale);`

```csharp
        void PlaySfx(TSfx sfx, float pitchScale);

        /// <summary>Starts a run for a sound with several clips: the next plays draw from a fresh shuffle, no clip repeats
        /// until every clip played. One-clip sound: no-op.</summary>
        void RestartSfxVariants(TSfx sfx);
```

Doc tiếng Anh theo phần còn lại của file.

### `AudioService.cs` › `API`, sau `PlaySfx(TSfx sfx, float pitchScale)`

```csharp
        public void PlaySfx(TSfx sfx, float pitchScale)
        {
            Play(sfx, pitchScale);
        }

        public void RestartSfxVariants(TSfx sfx)
        {
            if (_bagById.TryGetValue(sfx, out ShuffleBag bag))
                bag.Restart();
        }
```

| Consumes | Produces |
|---|---|
| `_bagById`, `ShuffleBag.Restart` (GĐ1) | `IAudioService.RestartSfxVariants`, `AudioService.RestartSfxVariants` |

**Quyết định** — id không có trong catalog không log: lệnh luôn đi trước `PlaySfx` cùng id, và `PlaySfx` đã log đúng lỗi đó; log ở đây là
một sự kiện hai dòng đỏ. — Không có đối tượng "loạt" riêng cho caller giữ: hai loạt gối nhau dùng chung một túi, loạt sau mở túi mới giữa
loạt trước. Dòng tiếng nghe được vẫn không lặp trong n lần rút liên tiếp kể từ lần mở gần nhất, nhưng riêng loạt trước có thể gặp lại
clip nó phát trước lúc loạt sau mở. Phương án bỏ: `BeginSfxRound(id)` trả handle — thêm type công khai và cấp phát mỗi loạt cho khác
biệt tai khó phân biệt (hai loạt xen nhau nghe thành một dòng). — Thêm member vào interface: trong mọi assembly đã quét, chỉ
`AudioService` implement `IAudioService<,>`.

**Editor setup:** không.

**Kiểm:** compile sạch; thêm vào `AudioServiceTests`:

| Case | Input | Kỳ vọng |
|---|---|---|
| loạt sau loạt dở | entry 6 clip, phát 2, `RestartSfxVariants`, phát 6 | 6 lần sau đủ 6 clip (đỏ nếu `RestartSfxVariants` để trống thân) |
| tiếng một clip | `RestartSfxVariants` rồi `PlaySfx` | phát `Clip`, không lỗi |
| id ngoài catalog | `RestartSfxVariants(C)` khi catalog không có C | không log (`LogAssert.NoUnexpectedReceived`) |
| `IAudioService` | gọi qua biến kiểu interface | như gọi trên service |

**Mốc commit:** `feat(audio): RestartSfxVariants mở một loạt không trùng clip`.

---

## GĐ3 — Nhạc phát khi host dựng xong: `startMusic`

**Mục tiêu:** host đặt bài ở ô **Start Music**; host dựng xong là bài đó phát (hay được nhớ nếu Music đang tắt). Để trống: như cũ.

**Vì sao bây giờ:** độc lập với GĐ1–2; đặt cuối vì chỉ chạm `Awake` và một field.

**Sơ đồ sau GĐ3**

```
  Awake ──BuildTables──► … ──startMusic+ ≠ 0──► PlayMusic (luồng nhạc như cũ)
```

### `AudioService.cs` › `Unity Callbacks` › cuối `Awake`

```csharp
            _voicesBusyUntil = new float[voices.Length];

            // Bảng tra đã dựng ở trên: PlayMusic tra được ngay.
            if (!EqualityComparer<TMusic>.Default.Equals(startMusic, default))
                PlayMusic(startMusic);
        }
```

### `AudioService.cs` › field, sau `[SerializeField] private AudioCatalog<TSfx, TMusic> catalog;`

```csharp
        [SerializeField] private AudioCatalog<TSfx, TMusic> catalog;

        [Splitter("Configs")]
        [Tooltip("Bài phát ngay khi host dựng xong; host ở scene sống suốt phiên thì bài chạy tới khi tắt app. Trống: chờ PlayMusic.")]
        [SerializeField] private TMusic startMusic;
```

| Consumes | Produces |
|---|---|
| `PlayMusic`, `ApplyMusicState` (có sẵn) | field `startMusic` (ô **Start Music**) |

**Quyết định** — phát ở `Awake`, không thêm `Start` vào base: class con của dự án hay khai `private void Start` riêng, che magic method
cùng tên của base không cảnh báo; `Awake` của base đã là `protected virtual` và con override phải gọi `base`. — Cờ `IsMusicOn` ghi trước
hay sau `Awake` đều cho đúng trạng thái: `PlayMusic` chỉ nhớ bài khi nhạc tắt, `ApplyMusicState` áp lại khi bật. Cờ từ save đặt sau
`Awake` cùng frame thì `Stop` tới trước khi audio kịp ra; GĐ5 kiểm bằng tai ca Music lưu tắt. — `0` là "không phát" như mọi id chưa gán
của AudioSystem, không thêm `bool`.

**Editor setup:** không trong Horcrux; host của dự án chọn bài ở ô **Start Music** (GĐ5).

**Kiểm:** compile sạch; thêm vào `AudioServiceTests` (rig `Build` thêm tham số `TestMusic startMusic = default`, đặt bằng reflection trước
`SetActive(true)`):

| Case | Input | Kỳ vọng |
|---|---|---|
| để trống | catalog có track M | sau `Awake`: `musicSource.isPlaying` false, clip null |
| đặt M | | sau `Awake`: phát clip của M, volume của M |
| đặt M, Music tắt trước `Awake` | `IsMusicOn = false` trên object chưa bật | không phát; bật `IsMusicOn` → phát M |
| đặt M, Music tắt sau `Awake` | | dừng ngay |
| đặt id không có track | N, catalog chỉ có M | `Awake` log `Music N is not in the catalog`, không ném |

**Mốc commit:** `feat(audio): startMusic phát khi host dựng xong`.

---

## GĐ4 — Editor của catalog và tài liệu (agent)

| File | Đổi |
|---|---|
| `AudioCatalog.Editor.cs` › `IsEntryValid` | sau kiểm `Clip == null`: mỗi variant `null` → `LogError` `"{label} variant #{k} has no clip."`, entry không hợp lệ. Test `Entry_WithNullVariant_IsReported` trong `AudioCatalogTests` |
| `AudioCatalog.Editor.cs` › `AuditLoudness` | mỗi entry một dòng **mỗi clip** qua `ClipAmount`/`ClipAt` (`Sfx X`, `Sfx X ~1`…); trung vị tính trên mọi clip |
| `docs/Runtime/AudioSystem/AudioSystem.md` | mục mới "Một tiếng nhiều clip" (túi xáo, ranh lượt, `RestartSfxVariants`, loạt gối nhau) cạnh "Một tiếng nhiều bậc" · luồng phát thêm bước rút clip · "Luồng nhạc" thêm `startMusic` · "Trước khi chạy" bước host: ô **Start Music** · Bẫy: sửa dòng throttle theo entry (lý do GĐ1) · "Chưa làm": bỏ "nhiều clip cho một tiếng" · bảng chữ ký: `RestartSfxVariants`, `ShuffleBag` · cấu trúc file |
| `docs/Runtime/AudioSystem/AudioSystem.html` | đồng bộ các mục trên, soát lại cả trang |
| `docs/Runtime/ShuffleBag/ShuffleBag.md`, `.html` | hệ Runtime thuần, `Code: Runtime/Utilities/Permutation`: kịch bản (rút không lặp, mở lượt), cách dùng, lưu ý nguồn ngẫu nhiên; trỏ `FisherYates` ở tài liệu SlotShuffle |

**Mốc commit:** `docs(audio): nhiều clip, startMusic; ShuffleBag`.

---

## GĐ5 — category-jam dùng bản Horcrux, xoá cơ chế xáo của dự án, xoá Plan (agent)

**Trước khi sửa:** đọc lại `Assets/_CategoryJam/Resources/AudioCatalog.asset`. Bản 2026-11-23: entry 21–26 volume 0.9, minInterval 0.03,
clip `sfx_coin_1..6` theo thứ tự — sáu entry cùng volume, gộp không đổi độ lớn. Volume các entry 22–26 khác entry 21 lúc làm thì hỏi
developer trước khi gộp: variant dùng volume của entry.

| File | Đổi |
|---|---|
| `Assets/_CategoryJam/Scripts/Definition/GameAudioIds.cs` | `CoinSpawn1 = 21` → `CoinSpawn = 21`; xoá `CoinSpawn2..6`; comment `// 22–26 đã dùng cho tiếng coin cũ, không cấp lại.` |
| `Assets/_CategoryJam/Resources/AudioCatalog.asset` | entry 21 thêm khối `variants:` năm clip của entry 22–26 theo thứ tự (guid `161b5cb8…`, `cf82ba3b…`, `937137eb…`, `e47da200…`, `03369096…`, `fileID: 8300000, type: 3`); xoá entry 22–26 |
| `Assets/_Game/Scripts/Feature/CoinSystem/ButtonCoin.cs` | xoá `CoinSpawnSfxs` và `ShuffledSpawnSfxs()`; trước vòng `for` trong `CoinCollector`: `_audio.RestartSfxVariants(GameSfx.CoinSpawn);` thay `var spawnSfxs = ShuffledSpawnSfxs();`; trong vòng: `_audio.PlaySfx(GameSfx.CoinSpawn);` thay `_audio.PlaySfx(spawnSfxs[i % spawnSfxs.Length]);`; comment trên lời gọi mở loạt: một lượt bay không trùng tiếng |
| `Assets/_CategoryJam/Scripts/Feedback/GameAudioService.cs` | xoá `backgroundMusic` và `Start`; class rỗng lại; summary: nhạc nền đặt ở ô Start Music của host |
| `Assets/_CategoryJam/Scenes/Service.unity` | component `GameAudioService`: dòng `backgroundMusic: 1` (ngay sau `catalog:`) thành `startMusic: 1` |

**Editor setup:** `Service.unity` › `audio` (`GameAudioService`) › ô **Start Music** = `Background`. Scene đang giữ key
`backgroundMusic: 1` của field cũ; đổi tên field mà không sửa YAML thì ô mới để trống, Unity bỏ key cũ khi lưu, nhạc nền im. Sửa YAML
như bảng trên hoặc chọn ô qua Inspector rồi lưu scene; soát `git diff` của scene chỉ còn đúng một dòng đổi.

**Kiểm:** grep `CoinSpawn[1-6]`, `ShuffledSpawnSfxs`, `backgroundMusic` trong `Assets/` ra 0 · catalog **Validate Entries** không error,
warning id thiếu không có `CoinSpawn` · catalog **Audit Loudness** in sáu dòng `Sfx CoinSpawn`, `Sfx CoinSpawn ~1` … `~5` với clip
`sfx_coin_1` … `sfx_coin_6` · lệnh kiểm ở đầu Plan xanh.

**Kịch bản chơi thử** (developer):

| Vào đâu | Làm gì | Nghe gì | Khác trước | Dấu hiệu hỏng |
|---|---|---|---|---|
| màn nhận thưởng coin hoặc thắng level | nhận coin vài lượt | 6 tiếng coin hiện ra mỗi lượt không trùng, thứ tự khác giữa các lượt | lượt dài hơn 6 coin: lượt cũ lặp đúng thứ tự 6 tiếng, lượt mới xáo lại và không lặp tiếng ở chỗ nối | hai tiếng giống nhau liền kề; chỉ còn một tiếng coin; Console `Sfx CoinSpawn… is not in the catalog` |
| mở app, Music đang bật | vào màn đầu | nhạc nền chạy từ màn đầu | không | không có nhạc nền (ô Start Music trống) |
| Setting | tắt Music, bật lại | tắt thì im, bật lại thì phát | không | tắt mà vẫn phát; bật mà im |
| Music đang tắt, tắt hẳn app rồi mở lại | lắng nghe lúc boot | im từ đầu tới khi bật Music | không | lọt một đoạn nhạc ngắn lúc boot rồi tắt |

Xong kịch bản: xoá Plan này (và `.meta`).

**Mốc commit:** repo dự án `refactor(audio): tiếng coin một id nhiều clip, nhạc nền qua startMusic của Horcrux` kèm con trỏ submodule.

---

## File → GĐ

| File | GĐ1 | GĐ2 | GĐ3 | GĐ4 | GĐ5 |
|---|---|---|---|---|---|
| `Runtime/Utilities/Permutation/ShuffleBag.cs` | tạo | | | | |
| `Runtime/Implementations/Foundations/Audio/AudioEntry.cs` | `variants`, `ClipAmount`, `ClipAt` | | | | |
| `Runtime/Implementations/Foundations/Audio/AudioService.cs` | `using`, túi, rút | `RestartSfxVariants` | `startMusic` | | |
| `Runtime/Abstractions/Foundations/Audio/IAudioService.cs` | | `RestartSfxVariants` | | | |
| `Tests/EditMode/ShuffleBagTests.cs`, `Tests/PlayMode/AudioServiceTests.cs` (agent) | tạo, thêm case | thêm case | thêm case | | |
| `AudioCatalog.Editor.cs`, `Tests/EditMode/AudioCatalogTests.cs` (agent) | | | | variant | |
| `docs/Runtime/AudioSystem/*`, `docs/Runtime/ShuffleBag/*` (agent) | | | | sửa, tạo | |
| dự án: `GameAudioIds.cs`, `AudioCatalog.asset`, `ButtonCoin.cs`, `GameAudioService.cs`, `Service.unity`; Plan này | | | | | đổi; xoá Plan |
