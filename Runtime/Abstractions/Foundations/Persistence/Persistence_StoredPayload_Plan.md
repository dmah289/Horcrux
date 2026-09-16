# Persistence — bỏ cờ dirty, flush so payload — Plan sửa

> **Loại tài liệu:** Plan sửa — **developer sửa code lõi; agent viết test, chạy và báo kết quả, rồi đồng bộ
> `Persistence.md`.** §2 là **chuỗi chỗ đổi theo thứ tự**, mỗi bước một chỗ: file, dòng hiện tại, bản cũ → bản mới.
> Dòng không nhắc tới là dòng giữ nguyên. Phần sẽ test chỉ có bảng case, không có code test.

## Ngữ cảnh đã chốt

| Nhóm | Chốt |
|---|---|
| **Vấn đề** | `T` là class thì sửa field tại chỗ qua `entry.Value.x = …` không đi qua setter, entry không dirty, `FlushInternal` bỏ qua → thay đổi **không bao giờ** xuống đĩa, không log. Kỷ luật "nhớ gọi `MarkDirty()`" vỡ ở người thứ hai |
| **Cách giải** | Phương án D: mỗi entry nhớ **payload mà kho đang giữ** (`storedPayload`). Flush serialize mọi entry, so chuỗi với `storedPayload`, chỉ `SetString` entry khác, một `Save()`, rồi mới cập nhật `storedPayload`. Không còn cờ dirty, không còn gì để game code quên |
| **Vì sao không rẻ hơn được** | biết một object bị sửa tại chỗ có khác trước không thì phải duyệt hết object. Serialize là lần duyệt đó và sản phẩm của nó là chính chuỗi cần ghi. So tham chiếu chỉ bắt được đường qua setter, tức là đúng ca hôm nay đã đúng |
| **Ngân sách** | nhịp flush: 10 s một lần và lúc vào background. Đo trên desktop CoreCLR với `CollectionProgressData` thật: 4–13 µs và 2–16 KB rác cho một lần serialize + so chuỗi. `PlayerPrefs.Save()` ở cùng tick ghi cả file đồng bộ, đắt hơn vài bậc. Không phải hot path |
| **Hợp đồng payload mới** | `T` phải serialize **ổn định**: cùng dữ liệu → cùng chuỗi. Vi phạm thì flush thấy "đổi" mỗi tick và `Save()` cả kho mỗi 10 s, không log. Kiểu dễ vỡ: `HashSet`, `Dictionary` không giữ thứ tự chèn, `DateTime` với `Kind` khác nhau. Developer tự đảm bảo; nút kiểm để "Mở rộng sau" |
| **Ranh giới cứng đã đồng ý đổi** | `IsDirty` · `MarkDirty()` · `ClearDirty()` rời khỏi `IPersistenceDataEntry` và `PersistenceDataEntry<T>`. Bảy call site `MarkDirty()` trong `CollectionModule` xoá |
| **Không đổi** | `OnValueChanged` vẫn chỉ bắn khi đi qua setter hoặc `ReadPayload`; sửa tại chỗ không bắn. `FlushNow()` · `Flush(entry)` · `FlushAll()` · `RunAutosaveAsync` · seed lần đầu · giữ nguyên payload hỏng · hai host · `ValidateKeys` · `ImportPayload` · khoá và wire format |
| **Phân công** | Developer làm §2 và §3. Agent đọc code thật rồi viết lại `PersistenceSeedingTests` theo §4, chạy, và sửa `Persistence.md` theo §5 |

**Hành vi quan sát được thay đổi** — ba dòng, cả ba là chủ ý:

| Ca | Hôm nay | Sau D |
|---|---|---|
| Gán lại đúng giá trị cũ, hoặc `MarkDirty()` rồi không đổi gì | ghi và `Save()` cả kho | không ghi, không `Save()` |
| Sửa field tại chỗ, không gọi gì thêm | mất | ghi ở flush kế |
| Kho giữ đúng chuỗi `"null"` | rơi về mặc định, **không** ghi lại | rơi về mặc định, ghi mặc định ở flush kế — kho và RAM khớp nhau |

## §1 Thiết kế

```
boot     Setup ─► storedPayload = null · value = CloneDefault()
         LoadEntry:
           không có khoá / payload rỗng ─► Seeded   (storedPayload vẫn null → flush kế sẽ ghi mặc định)
           đọc được                     ─► Loaded   (ReadPayload: value = loaded · storedPayload = payload)
           đọc hỏng                     ─► Failed   (MarkStored(WritePayload()) → coi mặc định là đã lưu,
                                                     kho giữ nguyên chuỗi hỏng cho tới khi người chơi đổi gì)

flush    mọi entry:  payload = WritePayload()
                     payload == StoredPayload ?  bỏ qua  :  PlayerPrefs.SetString · nhớ (entry, payload)
         có gì vừa ghi ─► PlayerPrefs.Save() MỘT lần ─► MarkStored(payload) từng entry vừa ghi
```

| Quyết định | Vì |
|---|---|
| `storedPayload` là field của entry, `MarkStored` là explicit interface implementation | cùng vị trí và cùng lá chắn của `ClearDirty` cũ: chỉ collection với tay tới, và chỉ sau `Save()`. Game code gọi được là có cửa nói dối "đã lưu" |
| So chuỗi ở collection, không ở entry | `FlushInternal` là **cửa ghi duy nhất**; luật "ghi khi nào" nằm cạnh lệnh ghi thì đọc một chỗ ra hết |
| Failed → `MarkStored(WritePayload())` ở collection, không ở entry | quyết định "không ghi đè bản hỏng" đã nằm ở khối `catch` của `LoadEntry` kèm lý do; entry không cần biết |
| `ReadPayload` gán `storedPayload = payload` kể cả khi `loaded == null` | một luật: `storedPayload` = thứ kho đang giữ. Ca `"null"` đổi hành vi như bảng trên, chấp nhận |
| Tên `storedPayload`, không `lastWrittenPayload` | ở ca Failed nó không phải thứ "vừa ghi"; "stored" nói đúng vai: thứ flush coi là kho đã có |
| `List<(IPersistenceDataEntry entry, string payload)>` tái dùng bằng `Clear()` | phase hai cần đúng chuỗi đã `SetString`; serialize lại là hai phép tính buộc khớp nhau từ hai nguồn |
| Không giữ cờ dirty làm đường tắt song song | hai cơ chế nói "có gì cần lưu" là hai bất biến phải giữ khớp; so payload đã phủ mọi ca nên cờ chỉ còn là thứ phải nhớ |
| Không có `HasUnstoredChanges` cho Inspector | tính nó là serialize mỗi lần vẽ lại; `storedPayload` hiện read-only đã đủ để debug |

## §2 Chuỗi chỗ đổi

Gõ hết bước 1–15 rồi mới để Unity biên dịch: dừng giữa chừng thì interface và class lệch nhau. Số dòng là của
bản hiện tại, sửa xong bước trên thì dòng dưới trôi — tìm theo nội dung.

### File 1 — `IPersistenceDataEntry.cs`

**Bước 1** · dòng 6–9 · thay `IsDirty` bằng `StoredPayload`

```csharp
// cũ
        /// <summary>
        /// Has changes not yet stored.
        /// </summary>
        bool IsDirty { get; }
// mới
        /// <summary>
        /// Payload the collection last handed to storage. Flush writes only when the current payload differs.
        /// </summary>
        string StoredPayload { get; }
```

**Bước 2** · dòng 13–21 · thay cặp `MarkDirty` / `ClearDirty` bằng `MarkStored`

```csharp
// cũ
        /// <summary>
        /// Flags unsaved changes so the next flush writes this entry.
        /// </summary>
        void MarkDirty();
        /// <summary>
        /// Clearing it from game code drops unsaved progress
        /// with nothing to show it happened.
        /// </summary>
        void ClearDirty();
// mới
        /// <summary>
        /// Called by the collection once storage holds <paramref name="payload"/>. Calling it early hides unsaved progress.
        /// </summary>
        void MarkStored(string payload);
```

### File 2 — `PersistenceDataEntry.cs`

`PersistenceDataEntry.Editor.cs` **không đổi**: `ImportPayload` đi qua setter `Value`.

**Bước 3** · dòng 12 · thêm hợp đồng ổn định vào `<remarks>`

```csharp
// cũ
    /// <typeparamref name="T"/> must be plain data — no UnityEngine type. See Persistence.md.
// mới
    /// <typeparamref name="T"/> must be plain data with stable serialization — no UnityEngine type. See Persistence.md.
```

**Bước 4** · dòng 24 · field `isDirty` → `storedPayload`

```csharp
// cũ
        [ShowInInspector, ReadOnly] private bool isDirty;
// mới
        // What storage holds, as far as this entry knows. Null until the first load or write lands.
        [ShowInInspector, ReadOnly] private string storedPayload;
```

**Bước 5** · dòng 35 · property

```csharp
// cũ
        public bool IsDirty => isDirty;
// mới
        public string StoredPayload => storedPayload;
```

**Bước 6** · dòng 43 · setter `Value` không còn mark

```csharp
// cũ
                MarkDirty();
// mới
                RaiseChanged();
```

**Bước 7** · dòng 49–53 · xoá cả method

```csharp
// xoá
        public void MarkDirty()
        {
            isDirty = true;
            RaiseChanged();
        }
```

**Bước 8** · dòng 59 · trong `Setup`

```csharp
// cũ
            isDirty = false;
// mới
            storedPayload = null;
```

**Bước 9** · dòng 67 · trong `ReadPayload`

```csharp
// cũ
            isDirty = false;
// mới
            storedPayload = payload;
```

**Bước 10** · dòng 74–75 · explicit implementation

```csharp
// cũ
        void IPersistenceDataEntry.ClearDirty()
            => isDirty = false;
// mới
        void IPersistenceDataEntry.MarkStored(string payload)
            => storedPayload = payload;
```

### File 3 — `BasePersistenceDataCollection.cs`

`BasePersistenceDataCollection.Editor.cs` và `.TimeService.cs` **không đổi**.

**Bước 11** · dòng 31 · danh sách chờ phase hai mang cả payload

```csharp
// cũ
        private readonly List<IPersistenceDataEntry> pendingClearDirtyEntries = new();
// mới
        private readonly List<(IPersistenceDataEntry entry, string payload)> writtenThisFlush = new();
```

**Bước 12** · dòng 106–111 · nhánh Seeded thứ nhất trong `LoadEntry`

```csharp
// cũ
            // first use of this entry, keep the default.
            if (!PlayerPrefs.HasKey(storageKey))
            {
                entry.MarkDirty();
                return EntryLoadResult.Seeded;
            }
// mới
            // First use of this entry: StoredPayload stays null, so the next flush writes the default.
            if (!PlayerPrefs.HasKey(storageKey))
                return EntryLoadResult.Seeded;
```

**Bước 13** · dòng 115–120 · nhánh Seeded thứ hai

```csharp
// cũ
            // Key exists but carries nothing readable — same as first use, and no data to lose.
            if (string.IsNullOrEmpty(payload))
            {
                entry.MarkDirty();
                return EntryLoadResult.Seeded;
            }
// mới
            // Key exists but carries nothing readable — same as first use, and no data to lose.
            if (string.IsNullOrEmpty(payload))
                return EntryLoadResult.Seeded;
```

**Bước 14** · dòng 129 · khối `catch` của `LoadEntry`, thêm hai dòng ngay dưới comment có sẵn

```csharp
// cũ
                // Not seeded on purpose: overwriting a broken payload destroys the only copy of it.
                Debug.LogError($"[PersistenceDataCollection]: Reading entry '{entry.Key}' failed — using its default", this);
// mới
                // Not seeded on purpose: overwriting a broken payload destroys the only copy of it.
                // Treating the default as stored keeps flush away until the player actually changes something.
                entry.MarkStored(entry.WritePayload());
                Debug.LogError($"[PersistenceDataCollection]: Reading entry '{entry.Key}' failed — using its default", this);
```

**Bước 15** · dòng 162–199 · thay cả thân `FlushInternal` — gần mọi dòng đổi nên chép nguyên khối

```csharp
        /// <summary>The one flush body. A null <paramref name="only"/> flushes every changed entry, otherwise just that one.</summary>
        private void FlushInternal(IPersistenceDataEntry only)
        {
            writtenThisFlush.Clear();

            // Phase one: hand changed payloads to PlayerPrefs. Storage is not reached yet — this is still memory.
            for (int i = 0; i < entries.Count; i++)
            {
                IPersistenceDataEntry entry = entries[i];

                if (only != null && !ReferenceEquals(entry, only))
                    continue;

                try
                {
                    string payload = entry.WritePayload();

                    // In-place edits never announce themselves; comparing payloads is the one check that sees them.
                    if (string.Equals(payload, entry.StoredPayload, StringComparison.Ordinal))
                        continue;

                    PlayerPrefs.SetString(GetFinalKey(entry.Key), payload);
                    writtenThisFlush.Add((entry, payload));
                }
                catch (Exception e)
                {
                    Debug.LogError($"[PersistenceDataCollection]: Writing entry '{entry.Key}' failed" +
                                   " — it retries next flush.", this);
                    Debug.LogException(e, this);
                }
            }

            if (writtenThisFlush.Count == 0)
                return;

            // Phase two: the only call that reaches storage. StoredPayload updates after it lands, never before.
            PlayerPrefs.Save();

            for (int i = 0; i < writtenThisFlush.Count; i++)
                writtenThisFlush[i].entry.MarkStored(writtenThisFlush[i].payload);
        }
```

So với thân cũ: bỏ `if (!entry.IsDirty) continue;` · `WritePayload()` gọi trước rồi so `StoredPayload` thay vì gọi
ngay trong `SetString` · `pendingClearDirtyEntries.Add(entry)` → `writtenThisFlush.Add((entry, payload))` ·
`ClearDirty()` → `MarkStored(payload)` · log ghi hỏng bỏ chữ "stays dirty".

### File 4 — `CollectionModule.cs`

`Assets/LiveOps/Collection/Module/CollectionModule.cs` — **bước 16** · xoá **đúng dòng** `progressEntry.MarkDirty();`
ở bảy chỗ, giữ nguyên mọi dòng khác, kể cả hai `progressEntry.FlushNow();` ngay sau nó.

| Dòng | Trong method |
|---|---|
| 161 | `GrantTokens(int amount, string sourceId)` |
| 222 | `ClaimStep(int stepIndex)` — giữ `FlushNow()` dòng 223 |
| 231 | `SetAnimatedTokens(int token)` |
| 259 | `ConsumeEndedSummary(List<CollectionRewardSnapshot> into)` — giữ `FlushNow()` dòng 260 |
| 266 | `MarkTutorialPlayed()` |
| 295 | `SetMultiplierIndex(int newVal)` |
| 337 | `RollCycle()` |

Compiler là phép kiểm: còn sót dòng nào thì `CS1061` tại đúng dòng đó. `IPlayerPrefsProvider.MarkDirty` trong
`Assets/Horcrux/Editor/PlayerPrefsEditor/` là hệ khác, **không đụng**.

### File 5 — `PersistenceSeedingTests.cs`

`Assets/Horcrux/Tests/EditMode/PersistenceSeedingTests.cs` — **bước 17** · xoá hai dòng để assembly test biên dịch;
agent viết lại bộ test ngay sau theo §4.

```csharp
// dòng 61
            Assert.IsFalse(collection.Probe.IsDirty, "Dirty must clear once the write lands.");
// dòng 86
            Assert.IsFalse(collection.Probe.IsDirty);
```

## §3 Kiểm sau khi biên dịch

1. Console không còn lỗi biên dịch ở ba assembly: `com.horcrux.runtime`, `LiveOps.Collection`, `Horcrux.Tests`.
2. `grep -rn "MarkDirty\|IsDirty\|ClearDirty" Assets/Horcrux/Runtime Assets/LiveOps` chỉ còn hit trong `PlayerPrefsEditor`.
3. Play một lần với asset `Assets/_TheGame/Runtime/Game/Resources/Config/PersistenceDataCollection.asset` đang có:
   máy đã có save thì console **không** có `LogWarning` "entries had no stored key"; Inspector của asset hiện
   `storedPayload` của `collection_progress_data` bằng đúng JSON đang nằm trong PlayerPrefs.

Không có bước Editor nào khác: không đổi scene, prefab, asset, khoá hay wire format.

## §4 Bảng case test — agent viết sau khi có code thật

File `Assets/Horcrux/Tests/EditMode/PersistenceSeedingTests.cs`, giữ fixture `ProbeCollection` và cách authoring
qua `SerializedObject`. `T` của `probe` là `int` cho bốn case cũ; thêm một fixture với `T` là class có `List<int>`
cho hai case sửa tại chỗ.

| Case | Dàn cảnh | Khẳng định |
|---|---|---|
| Chưa có khoá | kho không có khoá | `Initialize()` xong: kho có `"7"`, `Value == 7`, `StoredPayload == "7"` |
| Khoá có, payload rỗng | `SetString(key, "")` | như chưa có khoá |
| Khoá đọc được | `SetString(key, "42")` | `Value == 42`, kho vẫn `"42"`, `StoredPayload == "42"` |
| Payload hỏng | `SetString(key, "{not json")` | `Value == 7`, kho vẫn `"{not json"`, đúng một `LogError` nêu khoá; **rồi** `FlushAll()` → kho **vẫn** `"{not json"` |
| Payload hỏng rồi người chơi đổi | như trên, rồi `Value = 9`, `FlushAll()` | kho là `"9"` |
| Kho giữ `"null"` (fixture class) | `SetString(key, "null")` | `Initialize()` xong `Value` bằng mặc định; `FlushAll()` → kho là JSON của mặc định |
| Sửa tại chỗ, không gọi gì (fixture class) | `Initialize()`, `Value.items.Add(1)`, `FlushAll()` | kho chứa `1`, `StoredPayload` bằng chuỗi vừa ghi |
| Không đổi thì không chạm kho | `Initialize()`, sửa tay `SetString(key, "sentinel")`, `FlushAll()` không đổi `Value` | kho vẫn `"sentinel"` — flush không `SetString` entry không đổi |
| Gán lại đúng giá trị cũ | `Initialize()` với kho `"42"`, sửa tay kho thành `"sentinel"`, `Value = 42`, `FlushAll()` | kho vẫn `"sentinel"` |
| `FlushNow()` chỉ ghi entry đó | fixture hai entry, đổi cả hai, `FlushNow()` một entry | kho có entry đó, entry kia chưa; `StoredPayload` entry kia vẫn cũ |
| Ghi hỏng thử lại | `T` mang `Vector3` trong `List` rỗng lúc mặc định, `Initialize()` sạch, `Value.Add(Vector3.one)`, `FlushAll()` hai lần | mỗi lần một `LogError` nêu khoá, `StoredPayload` không đổi |

Biên đã liệt kê: rỗng · một phần tử · payload hỏng · payload `"null"` · hai entry cùng lượt · flush lặp lại.

## §5 Đồng bộ `Persistence.md` — agent làm cùng lượt với test

Sửa **dòng cũ**, không thêm dòng nói ngược. Các mục phải chạm:

| Mục | Sửa gì |
|---|---|
| Mở đầu, "Ba nhịp", kịch bản 3 | bỏ mọi chữ "dirty" và `MarkDirty`; nhịp đọc/ghi chỉ còn `Value = x → OnValueChanged`; flush mô tả serialize → so `StoredPayload` → `SetString` → `Save()` → `MarkStored` |
| Kịch bản 4 | đổi từ "đường duy nhất phải tự mark" thành "sửa tại chỗ được ghi ở flush kế; `OnValueChanged` không bắn" |
| Kịch bản 6, 7 | Seeded không còn `MarkDirty`; Failed thêm `MarkStored(WritePayload())`; ghi hỏng "thử lại ở lượt sau" giữ nguyên |
| Ca biên | dòng `"null"` đổi kết cục theo bảng "Hành vi quan sát được thay đổi" ở đầu plan này |
| Hợp đồng payload | thêm luật **serialize ổn định**, kèm ba kiểu dễ vỡ và hậu quả "ghi cả kho mỗi tick, không log" |
| Inspector | hàng `isDirty` → `storedPayload`, không serialize vào asset |
| Kiểm thử | bảng case theo §4 |
| Bất biến | "Dirty chỉ tắt sau khi storage nhận" → "`StoredPayload` chỉ cập nhật sau khi storage nhận"; "`ClearDirty` không gọi được từ game code" → `MarkStored` |
| Bẫy | bỏ hàng "Mutate model tại chỗ mà không `MarkDirty()`"; thêm bẫy mới: payload không ổn định → `Save()` mỗi tick. Hàng "Reset dirty trước khi ghi" giữ làm bài học lịch sử |
| Chữ ký | `IPersistenceDataEntry` và `PersistenceDataEntry<T>` theo bước 1–10 |
| Ba chỗ cố ý khác Remote Config | thêm chỗ thứ tư: Persistence so payload, Remote Config dùng cờ `fetched` — kèm lý do |
| `Collection_Plan.md` mục 2.1 nếu còn nhắc `MarkDirty` | grep rồi sửa cùng lượt |

## Mở rộng sau — rẻ, thêm không sửa cũ

- Nút `ValidateKeys` kiểm serialize ổn định: serialize `CloneDefault()` hai lần rồi so chuỗi cho từng entry. Bắt được
  `HashSet`/`Dictionary` có phần tử trong mặc định; không bắt được container rỗng lúc mặc định.
- Nếu tổng payload của cả kho vượt vài chục KB thì chuyển kho sang file trên đĩa, sửa nội bộ `FlushInternal` và
  `LoadEntry`; `StoredPayload` và entry không đổi.
