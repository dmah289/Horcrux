# Plan — AudioSystem `variants`, `startMusic`: một tiếng nhiều clip rút theo túi xáo, nhạc phát khi host dựng xong

Bản tạm: category-jam `Assets/_Game/Scripts/Feature/CoinSystem/ButtonCoin.cs` (`CoinSpawnSfxs`, `ShuffledSpawnSfxs`), `Assets/_CategoryJam/Scripts/Feedback/GameAudioService.cs` (`Start`). Phụ thuộc `Horcrux_SlotShuffle_Plan.md` › GĐ1 — `FisherYates.Shuffle<T>(Span<T>, Func<int, int>)` ở `Runtime/Utilities/Permutation/FisherYates.cs`, gõ trước Plan này; `ShuffleBag` vào cùng thư mục.
Kiểm (từ gốc dự án): `bash "Assets/Horcrux/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,CategoryJam.Gameplay.csproj,Assembly-CSharp.csproj --tests Horcrux.EditModeTests,CategoryJam.EditModeTests` (chỉ EditMode).
PlayMode `AudioServiceTests`: Editor mở thì Window › General › Test Runner › PlayMode › Horcrux.PlayModeTests › AudioServiceTests › Run Selected; không Editor nào mở thì `Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testFilter AudioServiceTests -testResults <xml> -logFile <log>`, đọc `test-run` total/passed/failed.

## A — Một tiếng nhiều clip: `variants`, `ShuffleBag`, `RestartSfxVariants`

### Kịch bản

| Tình huống | Hệ làm gì | Caller/người chơi thấy |
|---|---|---|
| entry không có variant | phát `Clip`, không tra túi, không cấp phát | y như trước |
| entry n clip, phát liên tục | mỗi lần qua throttle rút một clip theo túi xáo | mỗi khối n lần đủ n clip; thứ tự khác giữa các lượt |
| ranh hai lượt | clip vừa phát không mở lượt mới | không hai lần liền một tiếng |
| `PlaySfx` bị throttle | không rút | lần bị bỏ không tiêu clip của túi |
| variant dài khác `Clip` | `busyUntil` theo clip vừa rút | voice không bị chiếm sớm hay muộn |
| caller mở loạt: `RestartSfxVariants(id)` | túi của id coi như rỗng | n lần phát sau đủ n clip; loạt trước bị huỷ giữa chừng không để túi dở |
| hai loạt gối nhau | chung một túi, loạt sau mở túi mới giữa loạt trước | dòng tiếng không lặp trong n lần kể từ lần mở gần nhất; riêng loạt trước có thể gặp lại clip của nó |
| `RestartSfxVariants` tiếng một clip | không làm gì | phát `Clip`, không lỗi |
| `RestartSfxVariants` id ngoài catalog | không log | `PlaySfx` theo sau log đúng một lỗi |
| túi `amount` 1 | luôn trả 0, không gọi nguồn ngẫu nhiên | — |
| túi `amount` 2 | ranh không lặp | xen kẽ tuyệt đối 0, 1 |
| catalog có variant `null` | **Validate Entries** `LogError` `"{label} variant #{k} has no clip."` | entry báo không hợp lệ |
| **Audit Loudness** | một dòng mỗi clip (`Sfx X`, `Sfx X ~1`…), trung vị trên mọi clip | clip lệch độ lớn lộ ra để chuẩn hoá file |

### Flow

```
  authoring: AudioEntry (Clip, variants[]) ─────────────────────────────────────────────── Implementations/Foundations/Audio/AudioEntry.cs
  host Awake: vòng catalog ──ClipAmount > 1──► _bagById[id] = new ShuffleBag(ClipAmount) ── Implementations/Foundations/Audio/AudioService.cs
  caller ──PlaySfx(id)──► AudioService.Play ──throttle theo entry (như cũ)──►
        _bagById có id? entry.ClipAt(bag.Draw(NextIndex)) : entry.Clip ──► voice (busyUntil theo clip đó)
                              │  NextIndex = Random.Range(0, n), static
                              └─ ShuffleBag.Draw ──hết lượt──► StartRound ──► FisherYates.Shuffle   Utilities/Permutation/ShuffleBag.cs, FisherYates.cs
  caller ──RestartSfxVariants(id)──► IAudioService ──► AudioService ──► ShuffleBag.Restart        Abstractions/Foundations/Audio/IAudioService.cs
  Editor: AudioCatalog.Editor.cs — Validate Entries (variant null), Audit Loudness (ClipAmount/ClipAt, một dòng mỗi clip)
```

### Thuật toán

| Bước | Công thức, bất biến |
|---|---|
| lượt | `FisherYates.Shuffle(_order)`, `_cursor = 0`; rút `_order[_cursor++]`; `_cursor == n` thì mở lượt mới |
| ranh | `n > 1 ∧ _order[0] == _lastDrawn` → đổi chỗ `0` với `j = 1 + nextIndex(n − 1)` |
| số lần gọi nguồn mỗi lượt | `n − 1` (xáo) hoặc `n` (xáo + đổi chỗ) — có biên, tất định theo seed |
| `Restart` | `_cursor = n`: lần rút sau mở lượt mới, ranh vẫn áp |
| bất biến | mỗi lượt là một hoán vị của `0..n−1`; `n > 1` thì không hai lần rút liền nhau trùng |

Bản mẫu: C# trên mono của Unity, nguồn `System.Random(seed).Next`, `amount` 6, seed 1–200, 60 lần rút — 0 sai; `Restart` giữa lượt, `amount` 1 và 2, số lần gọi nguồn (5 hoặc 6) đúng ở 200 seed; bỏ khối đổi chỗ ở ranh: trùng liền ở 152/200 seed (seed đầu: 1).

---

### GĐ1 — xong thì catalog nào cũng cho một tiếng nhiều clip, nghe đổi tiếng mà catalog cũ y như cũ

```
  caller ──PlaySfx──► AudioService ──throttle──► _bagById+ ? entry.ClipAt+(bag.Draw+(NextIndex+)) : entry.Clip ──► voice
                                                               └──► FisherYates.Shuffle (đã có, SlotShuffle GĐ1)
  Awake ──ClipAmount+ > 1──► new ShuffleBag+
```

**Editor setup:** tạo `ShuffleBag.cs` trong Project window, trong thư mục `Permutation` SlotShuffle GĐ1 đã tạo (để có `.meta`).

`Runtime/Implementations/Foundations/Audio/AudioService.cs` › `using` — thêm `using Horcrux.Runtime.Utilities.Permutation;` sau `using Horcrux.Runtime.Utilities;`.

`AudioService.cs` › field, sau `private float[] _voicesBusyUntil = Array.Empty<float>();` (`Random` là alias `UnityEngine.Random` có sẵn):

```csharp
        private readonly Dictionary<TSfx, float> _lastPlayTimeById = new();
        private float[] _voicesBusyUntil = Array.Empty<float>();
        // Túi chỉ cho entry nhiều clip: entry một clip không tra, không rút.
        private readonly Dictionary<TSfx, ShuffleBag> _bagById = new();
        // Static: không bắt biến nên không cấp phát mỗi lần rút.
        private static readonly Func<int, int> NextIndex = static amount => Random.Range(0, amount);
```

`AudioService.cs` › `Unity Callbacks` › `Awake` — vòng điền `_lastPlayTimeById`, cũ:

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
                // Dựng túi ở đây, không lúc phát: đường phát vẫn 0 B.
                if (entry.ClipAmount > 1)
                    _bagById[entry.Id] = new ShuffleBag(entry.ClipAmount);
            }
```

`AudioService.cs` › `Class Methods` › `Play` — cũ:

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

`Runtime/Implementations/Foundations/Audio/AudioEntry.cs` › field, sau `minIntervalSeconds`:

```csharp
        [SerializeField, Min(0f)] private float minIntervalSeconds = 0.03f;
        [Tooltip("Clip thêm cho cùng tiếng, rút xen với Clip theo túi xáo: hết một lượt mới lặp. Trống: luôn phát Clip.")]
        [SerializeField] private AudioClip[] variants = Array.Empty<AudioClip>();
```

`AudioEntry.cs` › sau `public float MinIntervalSeconds => minIntervalSeconds;`:

```csharp
        public float MinIntervalSeconds => minIntervalSeconds;

        /// <summary>Số clip của tiếng: Clip cộng các variant.</summary>
        internal int ClipAmount => 1 + variants.Length;

        /// <summary>Clip thứ index: 0 là Clip, từ 1 là variant thứ index − 1.</summary>
        // Clip giữ là clip 0, không thành mảng: key YAML clip của mọi catalog đã author không đổi.
        internal AudioClip ClipAt(int index) => index == 0 ? clip : variants[index - 1];
```

`Runtime/Utilities/Permutation/ShuffleBag.cs` — file mới:

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

        // amount 0 không chặn: service chỉ dựng khi ClipAmount > 1; Draw trên túi rỗng ném ngay lần đầu.
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

        // Nguồn là tham số: service truyền UnityEngine.Random, test truyền System.Random(seed).
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
            // Đổi chỗ, không xáo lại tới khi khác: số lần gọi nextIndex mỗi lượt có biên, test tất định.
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

`Tests/EditMode/ShuffleBagTests.cs` (agent; nguồn `new System.Random(seed).Next`):

| Case | Input | Kỳ vọng |
|---|---|---|
| một lượt đủ mặt | amount 6, rút 6 | đủ 0..5, không trùng |
| nhiều lượt | amount 6, seed 1…200, rút 60 | mỗi khối 6 liên tiếp đủ 0..5; không hai lần liền nhau trùng ở mọi ranh |
| `Restart` giữa lượt | amount 6, seed 1…200, rút 2, `Restart`, rút 6 | 6 lần sau đủ 0..5; lần đầu sau `Restart` ≠ lần rút ngay trước |
| amount 1 | rút 5 | luôn 0, `nextIndex` không bị gọi |
| amount 2 | seed 1…200, rút 40 | xen kẽ tuyệt đối 0, 1 (ranh không lặp) |
| số lần gọi nguồn | amount 6, seed 1…200, đếm lời gọi `nextIndex` mỗi lượt qua 10 lượt | 5 hoặc 6 |
| đối chứng | túi giống hệt nhưng bỏ khối đổi chỗ ở ranh (bản sao trong test) | case "nhiều lượt" đỏ ở seed 1 (bản mẫu: 152/200 seed) |

`Tests/PlayMode/AudioServiceTests.cs` (agent; helper `Entry` thêm tham số `params AudioClip[] variants`, đặt field `variants` bằng reflection như các field khác):

| Case | Input | Kỳ vọng |
|---|---|---|
| entry một clip | 20 lần `PlaySfx`, minInterval 0 | mọi voice nhận `entry.Clip`; `_bagById` không có khoá này |
| entry 3 clip | 30 lần, minInterval 0 | mỗi khối 3 liên tiếp đủ 3 clip, không hai lần liền trùng |
| throttle không tiêu túi | minInterval 1, gọi 2 lần cùng frame rồi chờ qua interval gọi 2 lần | 2 lần phát, hai clip khác nhau |
| `busyUntil` theo clip rút | variant dài gấp đôi clip | `busyUntil` = `now + length(clip vừa phát) / pitch` |
| không cấp phát | `PlaySfx` entry 3 clip trong khung `AllocationMeter` | 0 B (đối chứng: cùng khung bắt được một closure) |

---

### GĐ2 — xong thì caller mở được một loạt tiếng không trùng clip, loạt dở trước đó không ảnh hưởng

```
  caller ──RestartSfxVariants+(id)──► AudioService ──► ShuffleBag.Restart
         ──PlaySfx(id) × n──► (GĐ1) rút từ túi mới
```

`Runtime/Abstractions/Foundations/Audio/IAudioService.cs` › sau `void PlaySfx(TSfx sfx, float pitchScale);`:

```csharp
        void PlaySfx(TSfx sfx, float pitchScale);

        /// <summary>Starts a run for a sound with several clips: the next plays draw from a fresh shuffle, no clip repeats
        /// until every clip played. One-clip sound: no-op.</summary>
        void RestartSfxVariants(TSfx sfx);
```

`AudioService.cs` › `API`, sau `PlaySfx(TSfx sfx, float pitchScale)`:

```csharp
        public void PlaySfx(TSfx sfx, float pitchScale)
        {
            Play(sfx, pitchScale);
        }

        public void RestartSfxVariants(TSfx sfx)
        {
            // Id ngoài catalog không log: PlaySfx theo sau đã log, log ở đây thành hai dòng đỏ.
            if (_bagById.TryGetValue(sfx, out ShuffleBag bag))
                bag.Restart();
        }
```

`Tests/PlayMode/AudioServiceTests.cs` (agent), thêm:

| Case | Input | Kỳ vọng |
|---|---|---|
| loạt sau loạt dở | entry 6 clip, phát 2, `RestartSfxVariants`, phát 6 | 6 lần sau đủ 6 clip (đỏ nếu `RestartSfxVariants` để trống thân) |
| tiếng một clip | `RestartSfxVariants` rồi `PlaySfx` | phát `Clip`, không lỗi |
| id ngoài catalog | `RestartSfxVariants(C)` khi catalog không có C | không log (`LogAssert.NoUnexpectedReceived`) |
| `IAudioService` | gọi qua biến kiểu interface | như gọi trên service |

---

## B — Nhạc phát khi host dựng xong: `startMusic`

### Kịch bản

| Tình huống | Hệ làm gì | Người chơi thấy |
|---|---|---|
| ô **Start Music** trống (`0`) | không phát, chờ `PlayMusic` | host cũ chạy như cũ |
| đặt bài M | cuối `Awake` gọi `PlayMusic(M)` | nhạc từ màn đầu; host ở scene sống suốt phiên thì chạy tới khi tắt app |
| Music tắt trước `Awake` | `PlayMusic` chỉ nhớ M | im; bật Music → phát M |
| Music tắt sau `Awake` cùng frame (cờ từ save đặt lúc `Init`) | `ApplyMusicState` dừng | dừng trước khi audio kịp ra |
| bật, tắt Music trong Setting | `ApplyMusicState` như cũ | tắt thì im, bật lại thì phát |
| đặt id không có track | `Awake` log `Music N is not in the catalog`, không ném | im |

### Flow

```
  host Awake (AudioService.cs) ──BuildTables──► … ──startMusic ≠ 0──► PlayMusic ──► ApplyMusicState ◄── IAudioSetting.IsMusicOn
                                                                                                        (dự án đặt từ save lúc Init, trước hay sau Awake)
```

---

### GĐ3 — xong thì host nào cũng có nhạc nền chỉ bằng một ô Inspector

```
  Awake ──BuildTables──► … ──startMusic+ ≠ 0──► PlayMusic (luồng nhạc như cũ)
```

`AudioService.cs` › `Unity Callbacks` › cuối `Awake`:

```csharp
            _voicesBusyUntil = new float[voices.Length];

            // Bảng tra đã dựng ở trên: PlayMusic tra được ngay.
            // Ở Awake, không thêm Start: class con hay khai private Start, che magic method của base không cảnh báo.
            if (!EqualityComparer<TMusic>.Default.Equals(startMusic, default))
                PlayMusic(startMusic);
        }
```

`AudioService.cs` › field, sau `[SerializeField] private AudioCatalog<TSfx, TMusic> catalog;`:

```csharp
        [SerializeField] private AudioCatalog<TSfx, TMusic> catalog;

        [Splitter("Configs")]
        [Tooltip("Bài phát ngay khi host dựng xong; host ở scene sống suốt phiên thì bài chạy tới khi tắt app. Trống: chờ PlayMusic.")]
        [SerializeField] private TMusic startMusic;
```

`Tests/PlayMode/AudioServiceTests.cs` (agent; rig `Build` thêm tham số `TestMusic startMusic = default`, đặt bằng reflection trước `SetActive(true)`), thêm:

| Case | Input | Kỳ vọng |
|---|---|---|
| để trống | catalog có track M | sau `Awake`: `musicSource.isPlaying` false, clip null |
| đặt M | | sau `Awake`: phát clip của M, volume của M |
| đặt M, Music tắt trước `Awake` | `IsMusicOn = false` trên object chưa bật | không phát; bật `IsMusicOn` → phát M |
| đặt M, Music tắt sau `Awake` | | dừng ngay |
| đặt id không có track | N, catalog chỉ có M | `Awake` log `Music N is not in the catalog`, không ném |

---

## Chuyển dự án (agent)

| Chỗ | Đổi |
|---|---|
| `AudioCatalog.Editor.cs` › `IsEntryValid` | sau kiểm `Clip == null`: mỗi variant `null` → `LogError` `"{label} variant #{k} has no clip."`, entry không hợp lệ; test `Entry_WithNullVariant_IsReported` trong `Tests/EditMode/AudioCatalogTests.cs` |
| `AudioCatalog.Editor.cs` › `AuditLoudness` | mỗi entry một dòng **mỗi clip** qua `ClipAmount`/`ClipAt` (`Sfx X`, `Sfx X ~1`…); trung vị tính trên mọi clip |
| `docs/Runtime/AudioSystem/AudioSystem.md` | mục mới "Một tiếng nhiều clip" (túi xáo, ranh lượt, `RestartSfxVariants`, loạt gối nhau) cạnh "Một tiếng nhiều bậc" · luồng phát thêm bước rút clip · "Luồng nhạc" thêm `startMusic` · "Trước khi chạy" bước host: ô **Start Music** · Bẫy: dòng "đổi throttle sang khoá theo clip" thành throttle theo entry (dòng `bỏ` dưới) · "Chưa làm": bỏ "nhiều clip cho một tiếng" · bảng chữ ký: `RestartSfxVariants`, `ShuffleBag` · cấu trúc file |
| `docs/Runtime/AudioSystem/AudioSystem.html` | đồng bộ các mục trên, soát lại cả trang |
| `docs/Runtime/ShuffleBag/ShuffleBag.md`, `.html` | tạo, `Code: Runtime/Utilities/Permutation`: kịch bản (rút không lặp, mở lượt), cách dùng, lưu ý nguồn ngẫu nhiên; trỏ `FisherYates` ở tài liệu SlotShuffle |
| `Assets/_CategoryJam/Resources/AudioCatalog.asset` — trước khi sửa | đọc lại: bản 2026-11-23 entry 21–26 volume 0.9, minInterval 0.03, clip `sfx_coin_1..6` theo thứ tự; volume entry 22–26 khác entry 21 thì hỏi developer trước khi gộp — variant dùng volume của entry |
| `Assets/_CategoryJam/Scripts/Definition/GameAudioIds.cs` | `CoinSpawn1 = 21` → `CoinSpawn = 21`; xoá `CoinSpawn2..6`; comment `// 22–26 đã dùng cho tiếng coin cũ, không cấp lại.` |
| `Assets/_CategoryJam/Resources/AudioCatalog.asset` | entry 21 thêm khối `variants:` năm clip của entry 22–26 theo thứ tự (guid `161b5cb8…`, `cf82ba3b…`, `937137eb…`, `e47da200…`, `03369096…`, `fileID: 8300000, type: 3`); xoá entry 22–26 |
| `Assets/_Game/Scripts/Feature/CoinSystem/ButtonCoin.cs` | xoá `CoinSpawnSfxs`, `ShuffledSpawnSfxs()`; trước vòng `for` trong `CoinCollector`: `_audio.RestartSfxVariants(GameSfx.CoinSpawn);` thay `var spawnSfxs = ShuffledSpawnSfxs();`; trong vòng: `_audio.PlaySfx(GameSfx.CoinSpawn);` thay `_audio.PlaySfx(spawnSfxs[i % spawnSfxs.Length]);`; comment trên lời gọi mở loạt: một lượt bay không trùng tiếng |
| `Assets/_CategoryJam/Scripts/Feedback/GameAudioService.cs` | xoá `backgroundMusic` và `Start`; class rỗng lại; summary: nhạc nền đặt ở ô Start Music của host |
| `Assets/_CategoryJam/Scenes/Service.unity` | `audio` (`GameAudioService`): dòng `backgroundMusic: 1` (ngay sau `catalog:`) thành `startMusic: 1` = ô **Start Music** `Background` — đổi tên field mà không sửa YAML thì ô trống, Unity bỏ key cũ khi lưu, nhạc nền im; `git diff` scene đúng một dòng |
| kiểm | grep `CoinSpawn[1-6]`, `ShuffledSpawnSfxs`, `backgroundMusic` trong `Assets/` ra 0 · **Validate Entries** không error, warning id thiếu không có `CoinSpawn` · **Audit Loudness** in sáu dòng `Sfx CoinSpawn`, `~1` … `~5` với clip `sfx_coin_1` … `sfx_coin_6` · lệnh kiểm ở dòng mở xanh |
| Plan này | xoá cùng `.meta` sau chơi thử |
| chơi thử | nhận coin vài lượt: 6 tiếng mỗi lượt không trùng, thứ tự đổi giữa lượt; lượt dài hơn 6 coin lặp đúng thứ tự 6 tiếng, lượt mới không lặp ở chỗ nối · mở app, Music bật: nhạc từ màn đầu · Setting tắt/bật Music · Music tắt, tắt hẳn app rồi mở: im từ đầu; hỏng: hai tiếng giống liền kề, chỉ còn một tiếng coin, Console `Sfx CoinSpawn… is not in the catalog`, không nhạc nền, lọt một đoạn nhạc lúc boot |

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | throttle khoá theo clip | `minInterval` chặn dồn tiếng trong vài frame; nhiều clip một entry vẫn là một tiếng — người dùng đầu phát cách ≈ 0.117 s, xa hơn 0.03 s |
| bỏ | đổi `clip` thành mảng | đổi wire format của mọi catalog đã author; giá của field mới: variant thứ k ở `ClipAt(k + 1)`, chỉ `ClipAt` biết |
| bỏ | `ClipAmount`, `ClipAt` `public` | người gọi là `AudioService`, `AudioCatalog.Editor.cs` cùng assembly; Horcrux không có `InternalsVisibleTo`, test không cần |
| bỏ | mảng id clone mỗi lượt (bản tạm `ShuffledSpawnSfxs`) | cấp phát mỗi tương tác; 6 id cho một tiếng |
| bỏ | `RandomPick` (`Horcrux_ProgressDifficulty_Plan.md`) | chọn một phần tử, không nhớ đã rút gì — túi cần nhớ |
| bỏ | vòng xáo riêng trong `ShuffleBag` | một bản `FisherYates` cho cả SlotShuffle và túi |
| bỏ | lặp xáo tới khi `_order[0] != _lastDrawn` | số lần gọi nguồn không có biên trên |
| bỏ | service tự đoán lúc bắt đầu loạt, túi chạy liên tục | loạt bị huỷ giữa chừng để túi dở, loạt sau trùng tiếng; chỉ caller biết mốc loạt |
| bỏ | `BeginSfxRound(id)` trả handle loạt | thêm type công khai và cấp phát mỗi loạt cho khác biệt tai khó phân biệt (hai loạt xen nhau nghe thành một dòng) |
| bỏ | interface riêng cho `RestartSfxVariants` để không chạm `IAudioService` | mọi assembly đã quét chỉ `AudioService` implement `IAudioService<,>` |
| bỏ | volume, pitch riêng từng variant | clip lệch thì chuẩn hoá file — **Audit Loudness** chỉ ra |
| bỏ | `maxInstances` · crossfade nhạc khởi động | chưa người dùng |
| bỏ | thêm `bool` bật nhạc khởi động | `0` là "không phát" như mọi id chưa gán của AudioSystem |
| sau | `enum` chọn trên entry (tuần tự, theo trọng số), mặc định túi xáo | khi có người dùng chế độ chọn khác |
| sau | `ClipAmount`, `ClipAt` lên `public` | khi có người gọi ngoài assembly |
