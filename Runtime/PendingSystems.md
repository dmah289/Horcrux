# Horcrux SDK — Pending Systems (tài liệu tư duy trước khi phát triển)

> **Đây KHÔNG phải plan code.** Đây là tài liệu **tư duy thiết kế**: mục đích tồn tại, use case thực tế, tư tưởng cốt lõi, và mức độ tái sử dụng của từng hệ thống — để có **định hướng đúng** trước khi viết spec/plan chi tiết cho mỗi hệ.
>
> **Nguồn:** khảo sát 4 dự án puzzle iKame (`color-loop`, `foods_jam`, `Goods-Jam`, `water-flow`). Mỗi hệ nêu rõ nó xuất hiện ở đâu, bản nào sạch nhất để tham chiếu khi làm.
>
> **Đã loại khỏi tài liệu này (theo yêu cầu):**
> - **Object Pooling, EventBus, Remote Config** — đã có sẵn trong SDK/dự án.
> - **Toàn bộ Editor tooling** (level painter, quick-access, JSON→SO...) — mỗi game một schema khác, không nên SDK-hóa phần editor.
>
> **Nguyên tắc chung khi hiện thực bất kỳ hệ nào dưới đây** (bám `SKILL.md` + `CLAUDE.md`):
> - SOLID tuyệt đối; mở rộng qua abstraction, không sửa lõi (Open/Closed).
> - Zero-GC ở hot path; pool + cache; không LINQ/closure/boxing trong vòng lặp.
> - DI qua `Sisus.Init` (`[Service]` + `MonoBehaviour<TDep>`); phụ thuộc **interface**, không phụ thuộc impl.
> - Async bằng UniTask + `CancellationToken`; asset qua `AssetReference`/Addressables.
> - Self-documenting; tách **abstraction** (contract) khỏi **implementation** (vendor/game-specific).
> - **Ranh giới SDK:** SDK sở hữu *khung + contract*; game điền *nội dung + wiring vendor*.

---

## Bảng tổng quan — độ phổ quát & ưu tiên

Cột "4 dự án" = số dự án có hệ này (tín hiệu phổ quát). Cột "Tầng" = thứ tự đề xuất phát triển (phụ thuộc lẫn nhau).

| # | Hệ thống | 4 dự án | Tầng | Ghi chú nhanh |
|---|---|:--:|:--:|---|
| 1 | Manager Lifecycle & Bootstrap | 4/4 | 1 | Nền: mọi hệ khác init qua đây |
| 2 | Save / Persistence (typed + cloud) | 4/4 | 1 | Nền: rất nhiều hệ cần lưu state |
| 3 | Scene Flow / Loading | 4/4 | 1 | Nền: chuyển màn + chặn input |
| 4 | Time Service (countdown + server time) | 4/4 | 1 | Nền: live-ops/lives/daily đều cần |
| 5 | Stack State Machine | 3/4 | 1 | Tiện ích lõi, nhỏ, generic |
| 6 | Safe-Area / Responsive Canvas | 4/4 | 1 | UI nền tảng mobile |
| 7 | UI Navigator (Page/Popup) | 4/4 | 2 | Xương sống UI; phụ thuộc pool + addressables |
| 8 | Audio (music/SFX + pooled) | 4/4 | 2 | Phụ thuộc pool + save (settings) |
| 9 | Haptics / Vibration | 3/4 | 2 | Thin wrapper + preset |
| 10 | Toast + Notification Seed (red-dot) | 4/4 | 2 | Phụ thuộc UI Navigator/pool |
| 11 | Analytics / Tracking (abstraction) | 4/4 | 2 | Chỉ extract contract + taxonomy |
| 12 | Monetization Boundary (Ads/IAP interface) | 4/4 | 2 | Chỉ interface; impl per-game |
| 13 | Economy: Currency / Lives / Reward | 4/4 | 3 | Retention staple; phụ thuộc save + UI |
| 14 | Level System (library/distribution/difficulty) | 4/4 | 3 | Runtime, KHÔNG phải editor |
| 15 | Tutorial / FTUE | 4/4 | 3 | Step + handler framework |
| 16 | Tab Navigation / Scroll-Snap Home | 2/4 | 3 | Meta-map/home UX |
| 17 | In-Game Rating / Review flow | 3/4 | 3 | Multi-step popup |
| 18 | 💎 Dynamic Difficulty (Glicko-2) | 1/4 | 4 | IP khác biệt nhất; adaptive difficulty |
| 19 | 💎 LiveOps Module Host | 4/4 | 4 | Khung event/battlepass cắm-rút |
| 20 | 💎 DayActive / Monetization Scenario | 1/4 | 4 | Segment + scenario, remote-driven |

💎 = "viên ngọc" — hệ có giá trị IP cao, đáng nhân rộng dù chỉ xuất hiện ít nơi.

---

# TẦNG 1 — Foundation (làm trước; nhỏ, sạch, mọi thứ phụ thuộc)

## 1. Manager Lifecycle & Bootstrap

**Mục đích:** Chuẩn hoá cách **khởi tạo có thứ tự** các hệ thống lớn của game (audio, data, level, ads...) — ai init trước, ai sau, chờ async xong mới qua bước tiếp, huỷ sạch khi thoát.

**Use case thực tế:**
- Khởi động game: load save → fetch remote config → init audio/ads → vào màn hình đầu. Thứ tự SAI = crash (vd ads init trước khi có config).
- Chuyển scene gameplay: reinit các manager theo level mới.
- Thoát/pause app: gọi hook dọn dẹp theo đúng thứ tự ngược.

**Tư tưởng cốt lõi:**
- `abstract BaseManager` với `int Priority` + `UniTask Initialize(CancellationToken)`. Một bộ điều phối sort theo priority, `await` từng cái.
- Truy cập chéo qua `GetManager<T>()` / `GetManagerAs<TInterface>()` — nhưng ưu tiên DI hơn service-locator.
- **Mỗi manager 1 trách nhiệm**; bootstrap chỉ lo *thứ tự*, không lo *nội dung*.

**Tái sử dụng:** Rất cao (4/4). Mọi game level-based có cùng bài toán "init có thứ tự". Hoàn toàn genre-independent.

**Khảo sát:** `color-loop` bản sạch nhất — `BaseManager.cs` + `ServiceInit.cs` + `GameManager.cs` (~160 LOC, UniTask + CancellationToken). `foods_jam` có `GameBoostrap.cs` (state-machine init) là biến thể tốt.

**Định hướng cho Horcrux:** Ghép với DI sẵn có (`Sisus.Init`). `BaseManager` phát ra event lifecycle (Initialized/Reinitialized) để hệ khác bám. Cân nhắc: bootstrap nên là **data-driven** (danh sách manager + priority khai báo trong asset) thay vì hardcode — dễ thêm/bớt không sửa code.

**Cạm bẫy cần tránh:** thứ tự init ngầm định qua `Awake()` Unity (không kiểm soát được) → phải chủ động điều phối. Không để bootstrap biết chi tiết từng manager (chỉ biết `Priority` + `Initialize`).

---

## 2. Save / Persistence (typed + cloud sync)

**Mục đích:** Lưu/đọc dữ liệu người chơi **an toàn, có kiểu, chống sửa**, tự động lưu định kỳ, đồng bộ cloud khi cần.

**Use case thực tế:**
- Lưu tiến độ level, coin, boosters, settings, lives, daily-streak.
- Đồng bộ giữa thiết bị (Firebase/GameServer).
- Chống cheat: mã hoá giá trị nhạy cảm (coin, level) để không sửa bằng tool.
- Autosave: không mất dữ liệu khi app bị kill đột ngột.

**Tư tưởng cốt lõi:**
- `interface ISaveable` + `PlayerSaveLoadService<T>` generic — service không biết *nội dung* data, chỉ biết serialize/deserialize + dirty-tracking.
- Serializer thay được (MemoryPack / JSON / Newtonsoft) qua abstraction — không khoá vào 1 lib.
- Dirty flag + autosave loop (vd 100ms) thay vì lưu mỗi lần đổi.
- Lớp cloud-sync **tách rời** lớp local (local hoạt động offline, cloud là tuỳ chọn cắm thêm).
- Mã hoá là **decorator** quanh serializer, không nhồi vào logic game.

**Tái sử dụng:** Rất cao (4/4). Mọi F2P cần save. Phần "typed + dirty + autosave + crypto" hoàn toàn generic.

**Khảo sát:**
- `color-loop`: `PlayerSaveLoadService<T>` + `ISaveable` + `DataManager` (MemoryPack, autosave 100ms) — pattern generic sạch.
- `water-flow`: `KPrefs<T>` (typed PlayerPrefs + Newtonsoft + cache + server-save hook) + `AESEncryption` — bản typed-prefs tốt.
- `Goods-Jam`: crypto (Rijndael/TripleDES) + facade tách Config/Local/Transient data — pattern facade đáng học.
- `foods_jam`: dựa `RCore.JObjectDBManagerV2` (external) — cho thấy nhu cầu cloud-sync là chuẩn.

**Định hướng cho Horcrux:** Abstraction 3 lớp: (1) `IStorage` (đọc/ghi bytes: device/cloud), (2) `ISerializer` (object↔bytes: có/không crypto), (3) `ISaveRegistry` (tập `ISaveable` + dirty + autosave). Game chỉ khai báo model + implement `ISaveable`.

**Cạm bẫy:** "god save blob" (color-loop `GameData` gom hết vào 1 object) → nhiều hệ thò tay vào cùng blob, coupling cao. Thiết kế cho **nhiều save-unit độc lập** đăng ký vào registry.

---

## 3. Scene Flow / Loading

**Mục đích:** Chuyển scene mượt với màn hình loading (thật hoặc giả), progress, và **chặn input** trong lúc chuyển.

**Use case thực tế:**
- Splash → Home → Gameplay → Home, có loading che asset đang load.
- "Fake loading" để UX mượt kể cả khi load nhanh (tránh nháy).
- Chặn double-tap/spam trong lúc transition.

**Tư tưởng cốt lõi:**
- Service async (`UniTask LoadScene(...)`) phát progress qua event/callback.
- Loading screen **tách** khỏi logic load — chỉ subscribe progress.
- `ScreenInteractionBlocker` là tiện ích độc lập (dùng được cả ngoài scene-load).
- Factory async cho context game (`GameLoadContext`) — chuẩn bị dữ liệu trước khi scene active.

**Tái sử dụng:** Cao (4/4). Genre-independent.

**Khảo sát:** `Goods-Jam` bản đầy đủ nhất — `SceneManager.cs` + `SceneLoaderScreen`/`SceneFakeLoaderScreen` + `ScreenInteractionBlocker` (~657 LOC). `color-loop` `LoadingManager` event-driven cũng gọn.

**Định hướng cho Horcrux:** Addressables-first (`AssetReference` cho scene). Progress = tổng hợp nhiều async task (scene + preload asset + warm pool). Tách `ITransition` (fade/slide) để reskin không sửa lõi.

**Cạm bẫy:** loading screen tự load asset của chính nó cũng cần thời gian → phải có loading tối thiểu (bootstrap scene nhẹ).

---

## 4. Time Service (countdown + server time)

**Mục đích:** Nguồn thời gian **tập trung, chống chỉnh giờ máy**, cho mọi tính năng theo thời gian.

**Use case thực tế:**
- Lives refill sau X phút; daily reward reset lúc nửa đêm; event live-ops đếm ngược; cooldown booster.
- Chống cheat "tua giờ máy" để nhận thưởng sớm → cần server-time offset.
- Nhiều UI cùng hiển thị "còn 02:31" — 1 nguồn tick, nhiều listener.

**Tư tưởng cốt lõi:**
- `TimeManager` giữ offset = serverTime − deviceTime; mọi query "now" qua đây.
- `TimeCounter` (đếm ngược) tái sử dụng, `TimeRemainElement` (UI binding) tách khỏi logic.
- 1 nguồn tick trung tâm thay vì mỗi UI tự `Update` (zero-GC, gom tick).

**Tái sử dụng:** Cao (4/4). Bất kỳ game có live-ops/lives/daily.

**Khảo sát:** `Goods-Jam` `TimeManager` + `TimeCounter` + `TimeRemainElement` + `TimeExtension` (~327 LOC) — bản trọn vẹn nhất.

**Định hướng cho Horcrux:** Server-time lấy qua abstraction (`IServerTimeProvider`) — Firebase/GameServer cắm sau. Countdown phát event khi về 0. Xử lý cả trường hợp offline (fallback device time + cảnh báo).

**Cạm bẫy:** trôi giờ khi app background → resync khi `OnApplicationPause(false)`. Không tin device clock cho phần thưởng.

---

## 5. Stack State Machine

**Mục đích:** Quản lý trạng thái theo **ngăn xếp** (push/pop) — cho flow có thể "quay lại trạng thái trước".

**Use case thực tế:**
- Gameplay states: Playing → Paused (push) → Resume (pop về Playing).
- UI flow: mở popup chồng popup, đóng thì về cái dưới.
- Tutorial tạm chiếm control rồi trả lại.

**Tư tưởng cốt lõi:**
- `interface IStackState { Enter/Exit/Update }` + `StackStateMachine` push/pop.
- State là object độc lập, không biết nhau — machine điều phối.
- Nhỏ, generic, zero dependency.

**Tái sử dụng:** Cao (3/4). Utility lõi. Rất dễ extract.

**Khảo sát:** `color-loop` `_AlienCode/.../StackStateMachine/` (~165 LOC) — generic sạch.

**Định hướng cho Horcrux:** Bản generic thuần C# (không MonoBehaviour bắt buộc). Cân nhắc thêm biến thể FSM phẳng (không stack) cho case đơn giản — cùng interface.

**Cạm bẫy:** đừng over-engineer thành visual-scripting. Giữ tối giản.

---

## 6. Safe-Area / Responsive Canvas

**Mục đích:** UI hiển thị đúng trên **mọi tỉ lệ màn hình + tai thỏ (notch)** — không bị che, không méo.

**Use case thực tế:**
- Nút không lọt dưới notch/home-bar iPhone.
- Layout co giãn theo aspect ratio (tablet vs phone dài).
- Grid board tự canh giữa vùng an toàn.

**Tư tưởng cốt lõi:**
- Component `[ExecuteAlways]` cập nhật safe-area realtime, hỗ trợ simulator trong editor.
- Tách `ISafeAreaUpdatable` — nhiều thứ có thể phản ứng với thay đổi vùng an toàn.
- Grid/layout helper (canvas bounder, scale-with-bounder) bám vùng an toàn.

**Tái sử dụng:** Rất cao (4/4). Nhu cầu phổ quát mobile.

**Khảo sát:** `foods_jam` `Squirrel.UGUI` (đã tách mini-lib: `SafeAreaBase/Component`, `RuntimeSafeAreaUpdater`, simulator proxy) — bản sạch nhất. `water-flow` `Kelsey/UGUI/SafeArea~` cũng đầy đủ (có prefab post-processor).

**Định hướng cho Horcrux:** Có thể adopt `NotchSolution` (cả 4 dự án đều có) làm nền + wrapper mỏng. Hoặc bản tự viết nếu muốn zero-dependency.

**Cạm bẫy:** safe-area đổi khi xoay máy / đổi orientation → phải nghe sự kiện, không tính 1 lần.

---

# TẦNG 2 — Services (phổ quát, cần genericize nhẹ)

## 7. UI Navigator (Page / Popup framework)

**Mục đích:** Xương sống điều hướng UI: **ngăn xếp màn hình/popup** với animation chuyển cảnh, load async, truyền dữ liệu có kiểu, backdrop, pooling.

**Use case thực tế:**
- `ShowPopup<SettingsPopup>()`, `ShowPopup<RewardPopup, RewardData>(data)` — mở popup có dữ liệu.
- Chồng popup, đóng về đúng cái dưới; back button xử lý stack.
- Screen/Modal/Sheet: home screen, popup giữa màn, bottom sheet.
- Load UI qua Addressables (không nhồi hết vào scene).

**Tư tưởng cốt lõi:**
- Tách **Container/Service** (quản stack) khỏi **View** (nội dung).
- `Page` (toàn màn) vs `Popup/Modal` (đè) vs `Sheet` (trượt) — cùng vòng đời.
- Typed-data popup: `PopupT<TData>` — truyền dữ liệu type-safe, không cast lung tung.
- Vòng đời rõ: `WillEnter/DidEnter/WillExit/DidExit` (async) — để chạy tween vào/ra.
- Recycle/pool view thay vì Instantiate/Destroy.

**Tái sử dụng:** Rất cao (4/4). Mọi game cần. **Đây là hệ UI quan trọng nhất.**

**Khảo sát:**
- `water-flow`: `Kelsey/UGUI/Navigator` (in-house, typed popup, DI, ~1225 LOC) — **bản in-house tốt nhất, nên tham chiếu chính**.
- `foods_jam` & `Goods-Jam`: dùng fork OSS **UnityScreenNavigator** (Haruma-K) — mature nhưng là OSS.

**Định hướng cho Horcrux:** **Quyết định lớn:** tự viết (theo bản water-flow) hay adopt UnityScreenNavigator upstream? Tự viết = kiểm soát hoàn toàn, khớp DI/UniTask/zero-GC của SDK, nhưng tốn công. Nếu tự viết: bám vòng đời async + typed data + Addressables + pool. Tích hợp hệ **Tween Horcrux** (đang làm) cho transition.

**Cạm bẫy:** double-open (spam tap mở 2 popup) → phải khoá input lúc transition. Rò rỉ handle Addressables → track + Release khi pop.

---

## 8. Audio (music / SFX + pooled sources)

**Mục đích:** Phát nhạc nền + hiệu ứng âm thanh, **pool AudioSource**, tách music/SFX, tôn trọng setting bật/tắt.

**Use case thực tế:**
- Nhạc nền loop theo scene; SFX click/match/win.
- Nhiều SFX cùng lúc (pool source, không giật).
- Throttle: cùng 1 clip bắn liên tục → giới hạn tần suất (tránh chói).
- Setting music/sfx on/off lưu vào save.

**Tư tưởng cốt lõi:**
- Tách `MusicController` (1-2 source, crossfade) vs `SoundEffectController` (pool nhiều source).
- Clip định danh bằng **id/enum** + container SO (không hardcode `Resources.Load` rải rác).
- Đọc setting qua abstraction (`IAudioPersistentData`) — không coupling save.
- Throttle same-clip (cache last-play time).

**Tái sử dụng:** Rất cao (4/4). Genre-independent.

**Khảo sát:** `Goods-Jam` bản tách music/SFX/pool/trigger rõ nhất (~1831 LOC, 14 file). `color-loop` `AudioManager` (~350 LOC, interface-driven, GC-conscious) gọn hơn. `water-flow` `SoundController` + `.Hotpot` partial (tách clip game-specific ra partial — pattern hay).

**Định hướng cho Horcrux:** Dùng ObjectPool sẵn có cho AudioSource. Clip catalog = SO (game điền), controller generic. Bám `IAudioService` contract. Tích hợp `IAudioPersistentData` với Save system (#2).

**Cạm bẫy:** clip catalog nhồi vào controller = coupling. Tách data (SO) khỏi logic (controller) như bản `.Hotpot` partial của water-flow.

---

## 9. Haptics / Vibration

**Mục đích:** Rung phản hồi theo **preset ngữ nghĩa** (nhẹ/vừa/mạnh, success/fail), tôn trọng setting.

**Use case thực tế:**
- Rung nhẹ khi tap, rung "success" khi hoàn thành, "warning" khi sai.
- Setting vibration on/off.
- Đa nền tảng (iOS Taptic vs Android).

**Tư tưởng cốt lõi:**
- Wrapper mỏng quanh vendor (NiceVibrations/Lofelt).
- **Preset ngữ nghĩa** (`HapticFeature` enum) thay vì gọi API vendor trực tiếp → đổi vendor không sửa call-site.
- Setting qua abstraction.

**Tái sử dụng:** Cao (3/4). Genre-independent. Nhỏ.

**Khảo sát:** `color-loop` `VibrationHandler` (static, `VibrationFeature` enum, `VibrationConfig`). `water-flow` `VibrationController` (DI service, `IVibrationService`).

**Định hướng cho Horcrux:** ⚠️ **Genericize cái enum** — `color-loop` có `VibrationFeature.Grind/PickBox/OrderCompleted` (game-specific). SDK nên dùng enum **ngữ nghĩa chung** (`Light/Medium/Heavy/Success/Warning/Failure/Selection`), game map ngữ cảnh của nó vào đó. Bám `IVibrationService` + `IVibrationPersistentData`.

**Cạm bẫy:** enum game-specific = không port được. Giữ vocabulary trung tính.

---

## 10. Toast + Notification Seed (red-dot)

**Mục đích:** (a) Thông báo tức thời dạng "toast" trượt qua; (b) chấm đỏ "có gì mới" (notification seed).

**Use case thực tế:**
- Toast: "Không đủ coin", "Đã lưu", "+100 coin".
- Red-dot: chấm đỏ trên nút Shop khi có offer mới, trên Daily khi chưa nhận.

**Tư tưởng cốt lõi:**
- Toast: event-driven (`Publish(ToastEvent)`), queue, pool, animation vào/ra.
- Red-dot: cây điều kiện — mỗi node có `INotificationCondition`, tự cập nhật khi state đổi. Cha đỏ nếu con đỏ (bubble up).

**Tái sử dụng:** Cao (4/4). Genre-independent.

**Khảo sát:** Toast: cả 4 đều có (`color-loop` ToastManager+ToastUI, `Goods-Jam`/`foods_jam` ToastManager, `water-flow` ToastManager+pool). Red-dot: `water-flow` `NotificationSeed` + `INotificationCondition` — bản có cấu trúc.

**Định hướng cho Horcrux:** Toast dùng UI Navigator (#7) + pool + Tween. Red-dot là cây điều kiện tách rời, đăng ký condition qua DI. Bubble-up state qua EventBus sẵn có.

**Cạm bẫy:** red-dot tính lại toàn cây mỗi frame = phí. Event-driven + dirty flag.

---

## 11. Analytics / Tracking (chỉ abstraction + taxonomy)

**Mục đích:** Ghi nhận sự kiện phân tích (level start/win/fail, IAP, ad, UI) qua **contract thống nhất**, nhiều backend (Firebase/Adjust).

**Use case thực tế:**
- Track level_start, level_complete (kèm difficulty, moves, time), ad_watched, iap_purchased.
- Nhiều tracker cùng lúc (Firebase + Adjust + custom).
- Anti-cheat hook, revenue tracking.

**Tư tưởng cốt lõi:**
- **CHỈ extract phần abstraction + taxonomy sự kiện**, KHÔNG extract impl (impl coupling vendor + key game-specific).
- `interface ITrackingService` + event struct có kiểu (`Event_LEVEL_TRACK`...) thay vì string bừa bãi.
- Nhiều backend qua composite (1 event → nhiều tracker).
- Filter (event nào track 1 lần, event nào mỗi lần).

**Tái sử dụng:** Cao (4/4) — nhưng chỉ ở tầng contract. Impl mỗi game khác key.

**Khảo sát:** `water-flow` `_Core/Tracking/Event/` (base event pattern) + `IEventTrackingFilter` — taxonomy sạch. `Goods-Jam` `Dessentials/Features/Tracking` (Bamboo/Taichi/Incremental, interface-driven) — multi-backend tốt.

**Định hướng cho Horcrux:** SDK sở hữu: `ITrackingService`, `ITracker` (backend), base event struct, filter/dedup. Game định nghĩa event cụ thể + wiring vendor. **Không** nhồi Firebase vào SDK.

**Cạm bẫy:** string event name rải rác = typo + không refactor được. Dùng struct/typed event + code-gen constant.

---

## 12. Monetization Boundary (Ads / IAP — chỉ interface)

**Mục đích:** Cô lập SDK ads/iap của bên thứ 3 sau **contract của mình** — để game gọi interface ổn định, đổi vendor không sửa game.

**Use case thực tế:**
- `ShowInterstitial(placement)`, `ShowRewarded(onReward)`, banner, remove-ads, IAP purchase/restore.
- Đổi MAX↔AdMob, thêm mediation → chỉ sửa impl.
- Placement + pacing (giới hạn tần suất inter).

**Tư tưởng cốt lõi:**
- **CHỈ extract interface** (`IAdsService`, `IIapService`, `IAdjustService`, `IAntiCheatService`). Impl vendor = per-game (quá coupling để SDK-hoá).
- Placement qua enum/id; kết quả qua callback/UniTask.
- Interface segregation: banner/inter/rewarded/remove-ads tách nhỏ (`IBannerAds`, `IIntervalAds`...).

**Tái sử dụng:** Cao (4/4) ở tầng contract.

**Khảo sát:** `water-flow` tách interface sạch nhất — `Ads/IAdsService.cs` (+Banner/Interval/BreakLv/RemoveAds), `Iap/IIapService.cs`, impl ở `_Modules/Sdk` (per-game). `Goods-Jam` `_LiveOpsModules` có `IAdsService` sạch.

**Định hướng cho Horcrux:** SDK = interface + pacing/placement abstraction. Game implement wiring MAX/Adjust. Xem thêm hệ #20 (scenario) xây TRÊN boundary này.

**Cạm bẫy:** `Goods-Jam`/`foods_jam` `AdsManager` tangled (thò tay vào screen/toast/liveops) → **đừng** extract impl đó. Chỉ lấy hình dạng interface.

---

# TẦNG 3 — Puzzle / F2P features (đáng nhân rộng, coupling trung bình)

## 13. Economy: Currency / Lives / Reward

**Mục đích:** Bộ ba retention/monetization: **tiền tệ mềm** (coin), **lives/energy** (giới hạn lượt chơi + refill), **reward** (định nghĩa + trao + animation gom).

**Use case thực tế:**
- Coin: kiếm/tiêu, UI bar + animation coin bay vào ví.
- Lives: mất 1 khi thua, refill sau X phút, "infinite lives" window, popup hết lives.
- Reward: định nghĩa thưởng (SO), factory tạo, fly-up collect, popup claim, chest UI.

**Tư tưởng cốt lõi:**
- Currency = balance + event OnChanged + animation binding (tách logic khỏi UI).
- Lives = balance + timer refill (dùng Time Service #4) + config max.
- Reward = `ERewardType` + `RewardDataSO` + factory (tạo reward theo type) + collector (animation gom).
- Tất cả lưu qua Save (#2), hiển thị qua UI Navigator (#7).

**Tái sử dụng:** Rất cao (4/4). Staple F2P puzzle.

**Khảo sát:**
- `water-flow`: bản module-hoá đẹp nhất — `CoinSystem`, `LiveSystem` (heart+refill+infinite window), `GameReward` (factory + fly-up + chest). Contract tách ở `_Core`.
- `color-loop`: `LivesRefillFlow` + `LivesRefillConfig` event-decoupled sạch.

**Định hướng cho Horcrux:** 3 module riêng nhưng cùng pattern: `Balance + Config + PersistentData + UI binding`. Reward factory Open/Closed (thêm type = thêm handler). Lives timer bám Time Service (#4). **Tách state khỏi UI** (color-loop `GameData` blob là phản ví dụ).

**Cạm bẫy:** economy state gom vào god-blob (color-loop). Mỗi module 1 save-unit độc lập.

---

## 14. Level System (library / distribution / difficulty — runtime)

**Mục đích:** Quản lý **catalog level** lúc runtime: nạp level, override từ remote, phân phối nội dung theo trọng số, gắn nhãn độ khó. **KHÔNG phải editor** (editor loại theo yêu cầu).

**Use case thực tế:**
- Load level N; loop level sau khi hết (vd từ level 20 quay vòng).
- Remote override: A/B test thứ tự/nội dung level không cần update app.
- Phân phối: level nào dùng mechanic/màu nào theo phân bố.
- Difficulty tag để hệ DDA (#18) hoặc reskin dùng.

**Tư tưởng cốt lõi:**
- `ILevelLibrary` + `LevelDatabase` (SO) + remote override layer.
- **Tách format** (game-specific, mỗi game khác) khỏi **management** (generic: load/loop/override/distribute).
- Distribution/difficulty là strategy cắm được.

**Tái sử dụng:** Trung bình-cao. **Phần management generic**, phần data format thì per-game. Nên SDK-hoá cái khung, để trống schema.

**Khảo sát:** `water-flow` `_Modules/LevelSystem` (`LevelLibrary`, `LevelDatabase`, `Distribution`, `Difficult`, remote override, ~1955 LOC) — bản đầy đủ. `color-loop` `LevelManager` (~927 LOC) có loop-scheme + compression nhưng tangled với mechanic.

**Định hướng cho Horcrux:** Generic `ILevelLibrary<TLevelConfig>` (game khai `TLevelConfig`). Remote override + distribution strategy + difficulty tag là generic. Format serialize để game tự lo (chỉ cần implement `ILevelConfig`).

**Cạm bẫy:** trộn format vào manager (color-loop) → không port. Giữ manager không biết chi tiết ô/mechanic.

---

## 15. Tutorial / FTUE

**Mục đích:** Hướng dẫn người chơi mới bằng **chuỗi bước** có tay chỉ/mask/highlight, chờ hành động.

**Use case thực tế:**
- Bước 1: tay chỉ vào ô → chờ tap; bước 2: highlight nút → chờ action gameplay.
- Overlay che phần khác, chỉ cho tương tác vùng đang dạy.
- Tutorial gated bởi remote config (bật/tắt, thứ tự).

**Tư tưởng cốt lõi:**
- **Step + Handler pattern**: `BaseTutorialStep` (AutoClick/ManualClick/GameAction) + `TutorialHandler` (Canvas/Gameplay). Queue tuần tự.
- Step config-driven (SO), không hardcode toạ độ tay trong code.
- Chờ điều kiện (tap/action) qua callback/UniTask.

**Tái sử dụng:** Cao (4/4). Khung generic, chỉ step cụ thể per-game.

**Khảo sát:** `foods_jam` & `Goods-Jam` gần **giống hệt nhau** — `TutorialManager` + `TutorialHandler/{Base,Canvas,Gameplay}` + `TutorialSteps/{Base,AutoClick,ManualClick,GameActionType}` (~944 LOC). Đây là tín hiệu mạnh: pattern đã hội tụ, sẵn sàng SDK-hoá.

**Định hướng cho Horcrux:** Extract khung step/handler/queue. ⚠️ Genericize `GameActionType` (game định nghĩa action của nó, SDK chỉ biết "chờ 1 action id"). Highlight/mask dùng UI Navigator + Tween. Gating qua Remote Config sẵn có.

**Cạm bẫy:** 2 dự án có **2 impl song song** (gameplay vs liveops tutorial) — hợp nhất khi làm. Toạ độ tay hardcode = bể khi đổi layout; dùng anchor/target reference.

---

## 16. Tab Navigation / Scroll-Snap Home

**Mục đích:** Điều hướng **bottom-tab** cho màn home meta (Shop/Home/Events...) với scroll-snap giữa các trang.

**Use case thực tế:**
- Home có 3-5 tab dưới, vuốt hoặc tap tab để chuyển, snap vào trang.
- Feedback khi đổi tab (scale/màu), auto-switch tab khi mở từ nơi khác.

**Tư tưởng cốt lõi:**
- Base/derived: core scroll-snap generic + skin game.
- Tab bar ↔ scroll rect đồng bộ 2 chiều.
- Responsive theo số tab + kích thước màn.

**Tái sử dụng:** Trung bình (2/4). Game có meta-map/home nhiều tab thì cần; game gameplay-only thì không.

**Khảo sát:** `water-flow` `_Modules/TabNavigation` (base/derived split, ~1611 LOC) + `Kelsey/UGUI/Tab` (`KTabManager`, `KButtonTab`).

**Định hướng cho Horcrux:** Ưu tiên thấp hơn (không 4/4). Nếu làm: core scroll-snap + tab-sync generic, để reskin. Dùng chung EnhancedScroller nếu đã có.

**Cạm bẫy:** đừng ép mọi home vào pattern này — game đơn giản chỉ cần vài nút.

---

## 17. In-Game Rating / Review flow

**Mục đích:** Xin đánh giá app **đúng thời điểm** qua flow nhiều bước (chọn sao → native review).

**Use case thực tế:**
- Sau khi win level cao/đạt mốc vui → hỏi "thích game không?" → 4-5 sao mở native review, thấp thì mở feedback.
- Giới hạn tần suất hỏi (không spam).

**Tư tưởng cốt lõi:**
- Multi-step popup (step1 chọn sao → step2/3 theo nhánh).
- Trigger theo điều kiện (level, session) + nhớ đã hỏi.
- Native review qua abstraction (iOS/Android).

**Tái sử dụng:** Cao (3/4). Genre-independent.

**Khảo sát:** `water-flow` `_Modules/InGameRating` (`AppRating`, `RateGame`, `PopupRateStep1/2/3`, `IRateGameService`). `color-loop` `RateUsController` + `InAppReviewHelper` + prefs.

**Định hướng cho Horcrux:** Flow generic + trigger config. Native review qua interface. Dùng UI Navigator cho popup steps.

**Cạm bẫy:** hỏi sai thời điểm (sau khi thua) = rating thấp. Trigger cẩn thận + tôn trọng "đã hỏi".

---

# TẦNG 4 — 💎 Viên ngọc (IP giá trị cao, đáng nhân rộng)

## 18. 💎 Dynamic Difficulty (Glicko-2 skill rating)

**Mục đích:** **Điều chỉnh độ khó thích ứng** theo kỹ năng thực của người chơi — dùng thuật toán rating **Glicko-2** (như cờ vua/matchmaking) để ước lượng skill và chọn level phù hợp.

**Use case thực tế:**
- Người chơi giỏi → level khó hơn; người chơi vật lộn → dễ hơn (giữ chân, giảm churn).
- Mỗi level có "rating"; mỗi người chơi có rating (+ độ lệch + volatility). So khớp để chọn độ khó.
- Điều chỉnh số moves, số booster gợi ý, phân bố item theo deficit skill.

**Tư tưởng cốt lõi:**
- **Glicko-2**: mỗi thực thể (player, level) có `rating`, `RD` (rating deviation), `volatility`. Sau mỗi lần chơi, cập nhật như một "trận đấu" player vs level.
- Tách **phần toán rating** (thuần, generic) khỏi **phần áp dụng** (moves/booster — game-specific).
- Difficulty curve + user segment + A/B template (remote-driven).

**Tái sử dụng:** Chỉ 1/4 dự án có (foods_jam) — **nhưng đây là IP khác biệt nhất**, áp dụng cho **mọi game puzzle theo level**. Adaptive difficulty là thứ nâng retention rõ rệt và ít studio làm bài bản.

**Khảo sát:** `foods_jam` `Gameplay/Manager/DynamicDifficulty/` + `GameDifficultyManager.*` (4 partials) + config `ABTests/` (~2000+ LOC). Phần vàng: `Glicko2Helper`, `DynamicDifficultyRatingConfig`. Phần còn lại tangled với booster/item.

**Định hướng cho Horcrux:** **Extract phẫu thuật** — chỉ lấy engine Glicko-2 (thuần toán, `IDifficultyRatingEngine`), để game tự map "rating → tham số độ khó của nó". SDK sở hữu: rating math + update rule + curve/segment abstraction. Game sở hữu: áp rating vào mechanic.

**Cạm bẫy:** đừng extract cả cụm (dính booster/item foods_jam). Chỉ lấy hạt nhân toán học + interface áp dụng. Cần hiểu Glicko-2 kỹ trước khi làm (đọc paper gốc).

**Ưu tiên:** cao về giá trị, nhưng làm SAU khi Level System (#14) sẵn (cần catalog level + difficulty tag để vận hành).

---

## 19. 💎 LiveOps Module Host

**Mục đích:** **Khung cắm-rút** cho các sự kiện live-ops (battle pass, tournament, daily event, chest...) — game implement 1 bộ **service interface**, rồi thả module event vào là chạy.

**Use case thực tế:**
- Thêm event "Magic Cauldron" mùa này, "Battle Pass" mùa sau — không sửa lõi game.
- Mỗi module tự chứa: UI, reward, localization, tracking, schedule.
- Bật/tắt/lên lịch event qua remote config.

**Tư tưởng cốt lõi:**
- **Interface segregation triệt để**: module phụ thuộc `IAdsService`, `IGameRewardService`, `IVibrationService`, `ISpriteProviderService`, `ILevelPersistentDataService`... — game cung cấp impl.
- **Optional-service pattern**: module khai báo service tuỳ chọn (thiếu thì degrade gracefully).
- Module **tự chứa** (self-contained): có localization/tracking/popup riêng, drop-in.
- Schedule qua abstraction (local/remote/instant source).

**Tái sử dụng:** Cao (4/4 đều có live-ops, nhưng chất lượng khung khác nhau). Đây là **hệ được thiết kế SDK-hoá có chủ đích nhất** trong 4 dự án.

**Khảo sát:**
- `Goods-Jam` `_LiveOpsModules/_Shared` — **bản mẫu tốt nhất**: interface core services, optional pattern, module tự chứa (MagicCauldron), localization/tracking riêng (~13.5k LOC framework + 1 sample).
- `color-loop` `UniflareCoreEvent` — module riêng (own asmdef), event lifecycle + schedule source + reward + chest (~3150 LOC), gần SDK-ready.
- `water-flow` `BattlePass`/`WinStreak`/`Background` — cùng pattern `Core(Manager+Config+DataHandle+Remote)+UI`.

**Định hướng cho Horcrux:** Đây là **dự án lớn** — làm cuối, sau khi các service tầng 2-3 (reward/ads/tracking/save/time) đã sẵn (vì module host **tiêu thụ** chúng qua interface). Extract: service contract core + module lifecycle + schedule abstraction + optional-service. Mẫu tham chiếu chính: `Goods-Jam LiveOpsModules`.

**Cạm bẫy:** nội dung event là game-specific (đừng extract MagicCauldron). Chỉ extract **khung host + contract**. Cần các hệ khác xong trước (dependency nặng).

---

## 20. 💎 DayActive / Monetization Scenario

**Mục đích:** Điều phối **hiển thị ads/IAP theo kịch bản + phân khúc người dùng** — pacing interstitial theo ngày-hoạt-động, kịch bản theo segment, remote-driven.

**Use case thực tế:**
- Người mới: ít ads; người chơi lâu: nhiều hơn (theo "day active").
- Segment theo hành vi (spender/non-spender) → kịch bản ads/offer khác nhau.
- Toàn bộ điều khiển từ remote JSON — chỉnh không cần update app.

**Tư tưởng cốt lõi:**
- **Scenario/segment-driven**: rule engine đọc remote config + PlayerPrefs cache, quyết định khi nào show gì.
- Xây **TRÊN** Monetization Boundary (#12) — dùng `IAdsService`, không gọi vendor trực tiếp.
- `DayActiveTracker` đếm ngày hoạt động → pacing.

**Tái sử dụng:** Chỉ 1/4 (color-loop) — **nhưng tác giả tự ghi chú "không phụ thuộc type nào của game → port dễ"**. Genre-independent hoàn toàn. Mọi game hyper-casual/puzzle cần pacing ads thông minh.

**Khảo sát:** `color-loop` `_TheGame/Runtime/Monet/` (`MonetizationRuntime`, `MonetScenarioFlow`, `MonetizationRules`, remote bridge) + `_TheGame/Runtime/DayActiveAds/` (`DayActiveInterModule` — tự nhận portable, `DayActiveTracker`, `DayActiveInterConfig`). ~1350 LOC, sạch, deliberately game-agnostic.

**Định hướng cho Horcrux:** Extract gần như nguyên (đã game-agnostic). Đặt trên Monetization Boundary (#12) + Remote Config (đã có) + Time Service (#4, cho day-active). Rule engine đọc config → quyết định pacing.

**Cạm bẫy:** cần Monetization Boundary (#12) xong trước. Giữ rule remote-driven (đừng hardcode ngưỡng).

---

# Phụ lục — quan sát xuyên suốt 4 dự án (bài học cho SDK)

**Nợ kỹ thuật lặp lại (SDK sinh ra để xoá):**
- **Trùng lặp:** hầu hết dự án có **2 bản Object Pool**, **2 bản EventBus**, util trùng nhau → phân mảnh. SDK = 1 nguồn sự thật (bạn đã có sẵn 3 hệ này).
- **God-blob state:** `color-loop` `GameData` gom hết economy → coupling cao. SDK: nhiều save-unit độc lập.
- **Impl tangled:** `AdsManager` ở foods_jam/Goods-Jam thò tay vào screen/toast/liveops → không port. Bài học: **luôn tách contract khỏi impl vendor**.
- **Enum game-specific trong hệ generic:** `VibrationFeature.Grind` → không tái dùng. Vocabulary trung tính.
- **2 impl song song** (2 tutorial, 2 event bus) → hợp nhất khi SDK-hoá.

**Điểm chung tích cực (khớp Horcrux):**
- Cả 4 đều dùng **Sisus.Init (DI) + UniTask + Odin** — convention khớp sẵn SDK.
- `water-flow` (Kelsey) & `Goods-Jam` (Dessentials/LiveOpsModules) đã có **tầng interface tách sạch** — tham chiếu kiến trúc tốt nhất.
- Pattern hội tụ (tutorial step/handler, module `Core+UI`, typed remote config) → dấu hiệu đã chín để chuẩn hoá.

**Thứ tự phụ thuộc (đọc để không làm ngược):**
```
Tầng 1 (Manager, Save, Scene, Time, StateMachine, SafeArea)
   └─> Tầng 2 (UI Navigator, Audio, Haptics, Toast, Tracking, Monet Boundary)
          └─> Tầng 3 (Economy, Level System, Tutorial, TabNav, Rating)
                 └─> Tầng 4 (💎 DDA, LiveOps Host, Monet Scenario)
```
Viên ngọc (#18-20) tiêu thụ nhiều hệ tầng dưới → làm cuối. Tầng 1 nhỏ + zero-coupling → làm trước, ROI cao nhất.

---

*Tài liệu tư duy — cập nhật khi khảo sát thêm hoặc khi bắt đầu spec chi tiết một hệ. Mỗi hệ khi triển khai sẽ có file plan riêng (như `Tweening.md`) theo DOCS_SKILL.*
