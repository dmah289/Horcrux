# Plan — AudioSystem: một tiếng nhiều clip (`ShuffleBag`, `RestartSfxVariants`) và nhạc phát lúc khởi động

Ngày 2026-11-23. Nguồn: category-jam `Assets/_Game/Scripts/Feature/CoinSystem/ButtonCoin.cs` (6 id `CoinSpawn1..6` và hàm xáo
`ShuffledSpawnSfxs` để một lượt coin bay không trùng tiếng), `Assets/_CategoryJam/Scripts/Feedback/GameAudioService.cs` (`Start`
phát nhạc nền suốt phiên). Hai cơ chế độc lập trong `docs/Runtime/AudioSystem`: **A** nhiều clip cho một entry · **B** bài nhạc phát
khi host dựng xong.

**Phân công:** GĐ1–GĐ3 developer gõ, agent viết test theo bảng case rồi compile · GĐ4 (Editor của catalog, tài liệu) và GĐ5
(category-jam dùng bản Horcrux) agent làm. **Chạy được, thấy được** ở GĐ1–GĐ3 = compile sạch + bảng case xanh
(`Tests/EditMode/` cho `ShuffleBag`, `Tests/PlayMode/AudioServiceTests.cs` cho service); nghe trong game ở GĐ5. GĐ1–GĐ4 commit trong
`Assets/Horcrux`, GĐ5 commit ở dự án kèm con trỏ submodule. Không phụ thuộc Plan nào; `Runtime/Utilities/Permutation/` cũng là thư
mục của `Horcrux_SlotShuffle_Plan.md` — Plan nào gõ trước thì tạo thư mục. Không có `§0`: xáo Fisher–Yates và một phép đổi chỗ ở ranh
lượt, ghi ở GĐ1.

## Ngữ cảnh đã chốt

- **Người dùng:** tiếng lặp dày cần đổi clip để tai không mỏi (coin hiện ra, bước chân, nhặt đồ, va chạm) · game có nhạc nền suốt
  phiên (host audio ở scene sống suốt phiên).
- **Mục tiêu:** một id mang nhiều clip; mỗi lần phát rút một clip theo **túi xáo** — hết túi mới lặp, lượt mới không mở bằng clip vừa
  phát. Caller mở **một loạt** bằng `RestartSfxVariants(id)`: các lần phát sau rút từ túi vừa xáo, nên một loạt không dài hơn số clip
  không trùng tiếng nào. Developer chốt cơ chế này ngày 2026-11-23 (phương án bỏ: túi chạy liên tục không có lệnh mở loạt — loạt bị
  huỷ giữa chừng làm loạt sau trùng tiếng). Nhạc nền: một ô Inspector trên host, phát khi host dựng xong.
- **Ranh giới cứng giữ nguyên:** entry không có variant phát y như cũ (không cấp phát thêm, không rút ngẫu nhiên); catalog cũ không
  phải sửa — `variants` là **field mới** cạnh `clip`, không đổi `clip` thành mảng (đổi là đổi wire format của mọi catalog đã author);
  `startMusic` mặc định `0` = không phát gì, host cũ chạy như cũ.
- **Chỗ đặt:** `ShuffleBag` ở `Runtime/Utilities/Permutation/ShuffleBag.cs`, namespace `Horcrux.Runtime.Utilities.Permutation` — túi
  xáo chỉ số không mang gì của audio, dùng được cho chọn level, chọn câu thoại · phần còn lại sửa file có sẵn của AudioSystem.
- **Tên:** `variants`, `ClipAmount`, `ClipAt`, `ShuffleBag`, `RestartSfxVariants`, `startMusic` đổi tuỳ ý trước khi gõ.
- **Không làm:** throttle khoá theo clip (lý do GĐ1) · chọn tuần tự, theo trọng số (chưa người dùng) · volume, pitch riêng từng variant
  (clip nào lệch thì chuẩn hoá file — nút `Audit Loudness` chỉ ra) · `maxInstances` · crossfade nhạc khởi động.
- **Mở rộng sau:** chế độ chọn khác thì thêm `enum` chọn trên entry, mặc định là túi xáo.
- **Đã khảo sát:**

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| `ButtonCoin.ShuffledSpawnSfxs` | Fisher–Yates bằng `UnityEngine.Random.Range(0, i + 1)` | mảng clone mỗi lượt (cấp phát mỗi tương tác); 6 id cho một tiếng |
| `AudioService.Play` | điểm rút clip ngay sau throttle, `busyUntil` theo clip đã rút | — |
| `AudioEntry`, `AudioCatalog.Editor.cs` (`IsEntryValid`, `AuditLoudness`) | chỗ thêm field, chỗ kiểm variant | — |
| `docs/Runtime/AudioSystem/AudioSystem.md` › Chưa làm, Bẫy | "nhiều clip cho một tiếng" đã tính sẵn là additive | dòng "đổi throttle sang khoá theo clip" — xem GĐ1 |
| `GameAudioService.Start` | phát bài ở cấu hình host | `private void Start` ở class con che magic method cùng tên của base nếu base có |
| `Horcrux_SlotShuffle_Plan.md` | thư mục `Permutation/`, khuôn nhận `Func<int, int> nextIndex` | `SlotShuffle.Assign` là bài toán khác (xếp vào slot theo nhóm) |
| Horcrux `Utilities/` | — | chưa có túi xáo, chưa có hàm xáo chung |

## Luồng mô tả

```
authoring: entry có Clip + variants[] ───────────────────────────────────────────────────────────── GĐ1
host Awake: BuildTables → mỗi entry ClipAmount > 1 dựng một ShuffleBag(ClipAmount) ─────────────── GĐ1
caller mở loạt: RestartSfxVariants(id) → túi của id coi như rỗng ──────────────────────────────── GĐ2
caller PlaySfx(id) ── throttle ──► bag.Draw(NextIndex) → entry.ClipAt(i) ──► voice (busyUntil theo clip đó)   GĐ1
host Awake (cuối): startMusic ≠ 0 → PlayMusic(startMusic) ────────────────────────────────────── GĐ3
Editor: Validate báo variant trống, Audit đo từng variant · tài liệu ────────────────────────────── GĐ4 (agent)
category-jam: CoinSpawn một id 6 clip, ButtonCoin mở loạt mỗi lượt bay; nhạc nền qua startMusic ─ GĐ5 (agent)
```

## Bản đồ hệ cuối

```
  caller ──PlaySfx / RestartSfxVariants──► AudioService ──TryGetEntryById──► AudioCatalog ──► AudioEntry (Clip, variants)
                                              ├─ _bagById: TSfx → ShuffleBag (chỉ entry nhiều clip)
                                              │     ShuffleBag.Draw(NextIndex) — NextIndex = Random.Range(0, n), static
                                              ├─ throttle theo entry (như cũ) · voice (như cũ)
                                              └─ startMusic ──Awake──► PlayMusic
  Editor: AudioCatalog.Editor.cs — Validate Entries (variant null), Audit Loudness (một dòng mỗi clip)
```

---

## Giai đoạn 1 — Một entry nhiều clip, rút theo túi xáo

**Mục tiêu:** entry có `variants` thì mỗi lần phát (không bị throttle) rút một clip theo túi: hết túi mới xáo lại, lượt mới không mở
bằng clip vừa phát. Xong: catalog nhiều clip nghe đổi tiếng; catalog cũ nghe y như cũ.

**Vì sao bây giờ:** là cơ chế cả Plan dựa vào; `RestartSfxVariants` (GĐ2) chỉ là một lệnh lên túi này.

**Ai gọi:** `AudioService.Play` (gọi `ShuffleBag.Draw`, `AudioEntry.ClipAt`), `AudioService.Awake` (dựng túi theo `ClipAmount`); test.

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
                Shuffle(nextIndex);

            _lastDrawn = _order[_cursor++];
            return _lastDrawn;
        }

        private void Shuffle(Func<int, int> nextIndex)
        {
            for (int i = _order.Length - 1; i > 0; i--)
            {
                int j = nextIndex(i + 1);
                (_order[i], _order[j]) = (_order[j], _order[i]);
            }

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

**Quyết định** — nguồn ngẫu nhiên là tham số: framework không chọn nguồn; service truyền `UnityEngine.Random` (như jitter cao độ), test
truyền `System.Random(seed)`. — `Amount = 0` không chặn: không caller nào dựng túi rỗng (service chỉ dựng khi `ClipAmount > 1`), `Draw` trên
túi rỗng ném `IndexOutOfRangeException` ngay lần đầu. — Đổi chỗ ở ranh, không xáo lại tới khi khác: số lần gọi `nextIndex` cố định mỗi lượt
(n − 1 hoặc n), test theo seed tất định. Phương án bỏ: lặp xáo tới khi `_order[0] != _lastDrawn` — số lần gọi không biên trên.

### `Runtime/Implementations/Foundations/Audio/AudioEntry.cs` › sau `minIntervalSeconds`

```csharp
        [SerializeField, Min(0f)] private float minIntervalSeconds = 0.03f;
        [Tooltip("Clip thêm cho cùng tiếng, rút xen với Clip theo túi xáo: hết một lượt mới lặp. Trống: luôn phát Clip.")]
        [SerializeField] private AudioClip[] variants = Array.Empty<AudioClip>();
```

### `AudioEntry.cs` › sau `public float MinIntervalSeconds => minIntervalSeconds;`

```csharp
        public float MinIntervalSeconds => minIntervalSeconds;

        /// <summary>Số clip của tiếng: Clip cộng các variant.</summary>
        public int ClipAmount => 1 + variants.Length;

        /// <summary>Clip thứ index: 0 là Clip, từ 1 là variant thứ index − 1.</summary>
        public AudioClip ClipAt(int index) => index == 0 ? clip : variants[index - 1];
```

**Quyết định** — `Clip` giữ nguyên là clip thứ 0, không đổi thành mảng: key YAML `clip` của mọi catalog đã author không đổi, entry
một clip không phải sửa. Giá: variant thứ k nằm ở `ClipAt(k + 1)`, `ClipAt` là chỗ duy nhất biết điều đó.

### `AudioService.cs` › `using` và field, sau `private float[] _voicesBusyUntil = Array.Empty<float>();`

Thêm `using Horcrux.Runtime.Utilities.Permutation;` cạnh `using Horcrux.Runtime.Utilities;`.

```csharp
        private float[] _voicesBusyUntil = Array.Empty<float>();
        // Túi chỉ cho entry nhiều clip: entry một clip không tra, không rút.
        private readonly Dictionary<TSfx, ShuffleBag> _bagById = new();
        // Static: không bắt biến nên không cấp phát mỗi lần rút.
        private static readonly Func<int, int> NextIndex = static amount => Random.Range(0, amount);
```

### `AudioService.cs` › `Awake`, sau vòng điền `_lastPlayTimeById`

```csharp
            for (int i = 0; i < catalog.Entries.Count; i++)
                _lastPlayTimeById[catalog.Entries[i].Id] = float.NegativeInfinity;

            _bagById.Clear();
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                AudioEntry<TSfx> entry = catalog.Entries[i];
                if (entry.ClipAmount > 1)
                    _bagById[entry.Id] = new ShuffleBag(entry.ClipAmount);
            }
```

### `AudioService.cs` › `Play`, thay `AudioClip clip = entry.Clip;`

Cũ:

```csharp
            AudioClip clip = entry.Clip;
```

Mới:

```csharp
            // Rút sau throttle: lần bị bỏ không tiêu clip của túi.
            AudioClip clip = _bagById.TryGetValue(sfx, out ShuffleBag bag) ? entry.ClipAt(bag.Draw(NextIndex)) : entry.Clip;
```

`busyUntil` phía dưới đã dùng `clip.length` của biến `clip`, nên tự theo clip vừa rút.

**Quyết định** — throttle vẫn khoá theo **entry**, không theo clip (dòng "phải đổi sang khoá theo clip" trong `AudioSystem.md` sửa ở
GĐ4): `minInterval` chặn dồn tiếng trong cùng vài frame; nhiều clip của một entry vẫn là một tiếng, hai lượt gối nhau cùng frame chỉ
nên ra một. Người dùng đầu (coin) phát cách nhau 0.7 / 6 ≈ 0.117 s, xa hơn 0.03 s. — Túi dựng ở `Awake` cùng các bảng khác: đường phát
0 B như cũ (`Dictionary` khoá enum không boxing, delegate static).

**Sơ đồ tương tác sau GĐ1** (`+` là phần mới)

```
  caller ──PlaySfx──► AudioService ──throttle──► _bagById+ ? entry.ClipAt+(bag.Draw+(NextIndex+)) : entry.Clip ──► voice
  Awake ──ClipAmount+ > 1──► new ShuffleBag+
```

**Editor setup:** tạo `ShuffleBag.cs` trong Project window (để có `.meta`); thư mục `Permutation` nếu chưa có.

**Kiểm:** compile sạch. Agent viết:

`Tests/EditMode/ShuffleBagTests.cs` (nguồn `new System.Random(seed).Next`):

| Case | Input | Kỳ vọng |
|---|---|---|
| một lượt đủ mặt | amount 6, rút 6 | đủ 0..5, không trùng |
| nhiều lượt | amount 6, seed 1…200, rút 60 | mỗi khối 6 liên tiếp đủ 0..5; không hai lần liền nhau trùng ở mọi ranh |
| `Restart` giữa lượt | amount 6, rút 2, `Restart`, rút 6 | 6 lần sau đủ 0..5; lần đầu sau `Restart` ≠ lần rút ngay trước |
| amount 1 | rút 5 | luôn 0, `nextIndex` không bị gọi |
| amount 2 | seed 1…200, rút 40 | xen kẽ tuyệt đối 0, 1 (ranh không lặp) |
| số lần gọi nguồn | amount 6, đếm lời gọi `nextIndex` qua một lượt | 5 hoặc 6 |
| phép kiểm đỏ được | bỏ khối đổi chỗ ở ranh (bản sao trong test) | case "nhiều lượt" đỏ ở ít nhất một seed — ghi seed cạnh case |

`Tests/PlayMode/AudioServiceTests.cs` (helper `Entry` thêm tham số `params AudioClip[] variants`, đặt field `variants` bằng `SetField`):

| Case | Input | Kỳ vọng |
|---|---|---|
| entry một clip | 20 lần `PlaySfx`, minInterval 0 | mọi voice nhận `entry.Clip`; `_bagById` không có khoá này |
| entry 3 clip | 30 lần, minInterval 0 | mỗi khối 3 liên tiếp đủ 3 clip, không hai lần liền trùng |
| throttle không tiêu túi | minInterval 1, gọi 2 lần cùng frame rồi chờ qua interval gọi 2 lần | 2 lần phát, hai clip khác nhau |
| `busyUntil` theo clip rút | variant dài gấp đôi clip | `busyUntil` = `now + length(clip vừa phát) / pitch` |
| không cấp phát | `PlaySfx` entry 3 clip trong khung `AllocationMeter` | 0 B (đối chứng: cùng khung bắt được một closure) |

**Mốc commit:** `feat(audio): một tiếng nhiều clip rút theo túi xáo`.

---

## Giai đoạn 2 — Caller mở một loạt: `RestartSfxVariants`

**Mục tiêu:** caller có loạt tiếng không được trùng (một lượt coin bay) gọi `RestartSfxVariants(id)` trước loạt; các lần phát sau rút từ
túi mới. Loạt trước bị huỷ giữa chừng không để lại túi dở cho loạt sau.

**Vì sao bây giờ:** túi GĐ1 chạy liên tục; chỉ thiếu mốc "bắt đầu loạt" mà caller biết còn service không biết.

**Ai gọi:** category-jam `ButtonCoin.CoinCollector` trước vòng sinh coin (GĐ5); test.

### `Runtime/Abstractions/Foundations/Audio/IAudioService.cs` › sau `void PlaySfx(TSfx sfx, float pitchScale);`

```csharp
        void PlaySfx(TSfx sfx, float pitchScale);

        /// <summary>Mở một loạt cho tiếng nhiều clip: các lần phát sau rút từ túi xáo mới, không clip nào lặp tới khi hết túi.
        /// Tiếng một clip: không làm gì.</summary>
        void RestartSfxVariants(TSfx sfx);
```

### `AudioService.cs` › `API`, sau `PlaySfx(TSfx sfx, float pitchScale)`

```csharp
        public void RestartSfxVariants(TSfx sfx)
        {
            if (_bagById.TryGetValue(sfx, out ShuffleBag bag))
                bag.Restart();
        }
```

**Quyết định** — id không có trong catalog không log: lệnh luôn đi trước `PlaySfx` cùng id, và `PlaySfx` đã log đúng lỗi đó; log ở đây là
một sự kiện hai dòng đỏ. — Không có đối tượng "loạt" riêng cho caller giữ: hai loạt gối nhau dùng chung một túi, loạt sau mở túi mới
giữa loạt trước. Dòng tiếng nghe được vẫn không lặp trong n lần rút liên tiếp kể từ lần mở gần nhất, nhưng riêng loạt trước có thể gặp
lại clip nó phát trước lúc loạt sau mở. Phương án bỏ: `BeginSfxRound(id)` trả handle — thêm type công khai và cấp phát mỗi loạt cho
khác biệt tai khó phân biệt (hai loạt xen nhau nghe thành một dòng).

**Sơ đồ tương tác sau GĐ2**

```
  caller ──RestartSfxVariants+(id)──► AudioService ──► ShuffleBag.Restart
         ──PlaySfx(id) × n──► (GĐ1) rút từ túi mới
```

**Editor setup:** không.

**Kiểm:** compile sạch; thêm vào `AudioServiceTests`:

| Case | Input | Kỳ vọng |
|---|---|---|
| loạt sau loạt dở | entry 6 clip, phát 2, `RestartSfxVariants`, phát 6 | 6 lần sau đủ 6 clip |
| tiếng một clip | `RestartSfxVariants` rồi `PlaySfx` | phát `Clip`, không lỗi |
| id ngoài catalog | `RestartSfxVariants(C)` khi catalog không có C | không log (`LogAssert.NoUnexpectedReceived`) |
| `IAudioService` | gọi qua biến kiểu interface | như gọi trên service |

**Mốc commit:** `feat(audio): RestartSfxVariants mở một loạt không trùng clip`.

---

## Giai đoạn 3 — Nhạc phát khi host dựng xong: `startMusic`

**Mục tiêu:** host đặt bài ở ô **Start Music**; host dựng xong là bài đó phát (hay được nhớ nếu Music đang tắt). Để trống: như cũ.

**Vì sao bây giờ:** độc lập với GĐ1–GĐ2; đặt cuối vì chỉ chạm `Awake` và một field.

**Ai gọi:** `AudioService.Awake`; category-jam bỏ `GameAudioService.Start` (GĐ5).

### `AudioService.cs` › field, sau `[SerializeField] private AudioCatalog<TSfx, TMusic> catalog;`

```csharp
        [SerializeField] private AudioCatalog<TSfx, TMusic> catalog;

        [Splitter("Configs")]
        [Tooltip("Bài phát ngay khi host dựng xong; host ở scene sống suốt phiên thì bài chạy tới khi tắt app. Trống: chờ PlayMusic.")]
        [SerializeField] private TMusic startMusic;
```

### `AudioService.cs` › cuối `Awake`

```csharp
            _voicesBusyUntil = new float[voices.Length];

            // Bảng tra đã dựng ở trên: PlayMusic tra được ngay.
            if (!EqualityComparer<TMusic>.Default.Equals(startMusic, default))
                PlayMusic(startMusic);
```

**Quyết định** — phát ở `Awake`, không thêm `Start` vào base: class con của dự án hay khai `private void Start` riêng, che magic method
cùng tên của base không cảnh báo; `Awake` của base đã là `protected virtual` và con override phải gọi `base`. — Cờ `IsMusicOn` ghi trước
hay sau `Awake` đều đúng: `PlayMusic` chỉ nhớ bài khi nhạc tắt, `ApplyMusicState` áp lại khi bật. — `0` là "không phát" như mọi id chưa
gán của AudioSystem, không thêm `bool`.

**Sơ đồ tương tác sau GĐ3**

```
  Awake ──BuildTables──► … ──startMusic+ ≠ 0──► PlayMusic (luồng nhạc như cũ)
```

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

## Giai đoạn 4 — Editor của catalog và tài liệu (agent)

| File | Đổi |
|---|---|
| `AudioCatalog.Editor.cs` › `IsEntryValid` | sau kiểm `Clip == null`: mỗi variant `null` → `LogError` `"{label} variant #{k} has no clip."`, entry không hợp lệ. Test `Entry_WithNullVariant_IsReported` trong `AudioCatalogTests` |
| `AudioCatalog.Editor.cs` › `AuditLoudness` | mỗi entry một dòng **mỗi clip** (`Sfx X`, `Sfx X ~1`…); trung vị tính trên mọi clip |
| `docs/Runtime/AudioSystem/AudioSystem.md` | mục mới "Một tiếng nhiều clip" (túi xáo, ranh lượt, `RestartSfxVariants`, loạt gối nhau) cạnh "Một tiếng nhiều bậc" · luồng phát thêm bước rút clip · "Luồng nhạc" thêm `startMusic` · "Trước khi chạy" bước host: ô **Start Music** · Bẫy: sửa dòng throttle theo entry (lý do GĐ1) · "Chưa làm": bỏ "nhiều clip cho một tiếng" · bảng chữ ký: `RestartSfxVariants`, `ClipAmount`, `ClipAt`, `ShuffleBag` · cấu trúc file |
| `docs/Runtime/AudioSystem/AudioSystem.html` | đồng bộ các mục trên theo `docs` › `.html` |
| `docs/Runtime/ShuffleBag/ShuffleBag.md`, `.html` | hệ Runtime thuần: kịch bản (rút không lặp, mở lượt), cách dùng, lưu ý nguồn ngẫu nhiên |

**Mốc commit:** `docs(audio): nhiều clip, startMusic; ShuffleBag`.

---

## Giai đoạn 5 — category-jam dùng bản Horcrux (agent)

**Trước khi sửa:** đọc lại `AudioCatalog.asset` lúc làm (số ở đây là bản 2026-11-23: entry 21–26 volume 1, minInterval 0.03 — nháp của
developer). Volume các entry 22–26 khác entry 21 thì hỏi developer trước khi gộp: variant dùng volume của entry.

| File | Đổi |
|---|---|
| `Scripts/Definition/GameAudioIds.cs` | `CoinSpawn1 = 21` → `CoinSpawn = 21`; xoá `CoinSpawn2..6`; comment `// 22–26 đã dùng cho tiếng coin cũ, không cấp lại.` |
| `Resources/AudioCatalog.asset` | entry 21 thêm khối `variants:` năm clip của entry 22–26 theo thứ tự (guid `161b5cb8…`, `cf82ba3b…`, `937137eb…`, `e47da200…`, `03369096…`, `fileID: 8300000, type: 3`); xoá entry 22–26 |
| `_Game/Scripts/Feature/CoinSystem/ButtonCoin.cs` | xoá `CoinSpawnSfxs` và `ShuffledSpawnSfxs()`; trước vòng `for`: `_audio.RestartSfxVariants(GameSfx.CoinSpawn);` thay `var spawnSfxs = ShuffledSpawnSfxs();`; trong vòng: `_audio.PlaySfx(GameSfx.CoinSpawn);` thay `_audio.PlaySfx(spawnSfxs[i % spawnSfxs.Length]);`; comment trên lời gọi mở loạt: một lượt bay không trùng tiếng |
| `Scripts/Feedback/GameAudioService.cs` | xoá `backgroundMusic` và `Start`; class rỗng lại; summary: nhạc nền đặt ở ô Start Music của host |

**Editor setup:** `Service.unity` › `audio` (`GameAudioService`) › ô **Start Music** = `Background`. Scene hiện không có key
`backgroundMusic` (field cũ chạy theo mặc định C#), nên không có key cũ để đổi tên: đặt qua Inspector hoặc eval rồi lưu scene, soát
`git diff` chỉ thêm dòng `startMusic: 1`.

**Kiểm:** grep `CoinSpawn[1-6]`, `ShuffledSpawnSfxs`, `backgroundMusic` trong `Assets/` = 0 · catalog **Validate Entries** không error,
warning id thiếu không có `CoinSpawn` · eval: entry 21 `ClipAmount == 6`, `ClipAt(1..5)` đúng tên `sfx_coin_2..6` · compile dự án sạch,
EditMode dự án và Horcrux xanh · **cần chơi thử** (developer): nhận thưởng coin hoặc thắng level. Nghe: 6 tiếng coin hiện ra mỗi lượt
không trùng, khác thứ tự giữa các lượt; nhạc nền chạy từ màn đầu, tắt Music trong Setting thì im, bật lại thì phát. Dấu hiệu hỏng: một
lượt nghe hai tiếng giống nhau liền kề; chỉ còn một tiếng coin; Console `Sfx CoinSpawn… is not in the catalog`; không có nhạc nền.

**Mốc commit:** repo dự án `refactor(audio): tiếng coin một id nhiều clip, nhạc nền qua startMusic của Horcrux` kèm con trỏ submodule.

---

## File → giai đoạn

| File | GĐ1 | GĐ2 | GĐ3 | GĐ4 | GĐ5 |
|---|---|---|---|---|---|
| `Runtime/Utilities/Permutation/ShuffleBag.cs` | tạo | | | | |
| `Runtime/Implementations/Foundations/Audio/AudioEntry.cs` | `variants`, `ClipAmount`, `ClipAt` | | | | |
| `Runtime/Implementations/Foundations/Audio/AudioService.cs` | túi, rút | `RestartSfxVariants` | `startMusic` | | |
| `Runtime/Abstractions/Foundations/Audio/IAudioService.cs` | | `RestartSfxVariants` | | | |
| `Tests/EditMode/ShuffleBagTests.cs`, `Tests/PlayMode/AudioServiceTests.cs` (agent) | tạo, thêm case | thêm case | thêm case | | |
| `AudioCatalog.Editor.cs`, `Tests/EditMode/AudioCatalogTests.cs` (agent) | | | | variant | |
| `docs/Runtime/AudioSystem/*`, `docs/Runtime/ShuffleBag/*` (agent) | | | | sửa, tạo | |
| dự án: `GameAudioIds.cs`, `AudioCatalog.asset`, `ButtonCoin.cs`, `GameAudioService.cs`, `Service.unity` | | | | | đổi |
