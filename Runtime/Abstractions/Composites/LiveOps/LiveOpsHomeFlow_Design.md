# LiveOpsHomeFlow — điều phối trình diễn của live-ops lúc về Home (Thiết kế)

Mỗi lần về Home, trình diễn của mọi live-ops chạy theo **bốn giai đoạn cố định**: Thay đổi tiến trình → Tutorial → Ép xem đầy đủ tiến trình → Quảng bá. Thứ tự giữa các module trong một giai đoạn theo `Priority` lấy từ remote config của module. Mỗi module giữ luồng của mình; một điều phối viên duy nhất gọi các luồng theo giai đoạn. Code và test: `Assets/LiveOps/_Shared/LiveOpsHomeFlow_Plan.md`. Tài liệu này thành `LiveOpsHomeFlow.md` khi code xong.

## Ngữ cảnh đã chốt

| Mục | Nội dung |
|---|---|
| Người dùng | Developer của game và của các live-ops thêm sau; Horcrux mang sang dự án khác |
| Mục tiêu | Trình diễn lúc về Home không chồng nhau, theo đúng thứ tự giai đoạn; live-ops mới chỉ override giai đoạn nó có |
| Phạm vi Plan | Horcrux, Rocket Rush, Endless Sale |
| Cố ý KHÔNG làm | Giai đoạn và phase thành dữ liệu cấu hình được (đã chốt cứng, runner viết thẳng đọc một mạch hơn) · trần số popup quảng bá mỗi lần về Home (mới có một module quảng bá) · timeout cho flow · lease hoặc hàng đợi trên spotlight (luật tài nguyên độc quyền ở §1 làm chồng chéo không xảy ra) · **xoá hoặc đổi tên API Horcrux đang có** (chỉ thêm, để phần Horcrux compile sạch trước khi áp vào module) · luồng ngoài live-ops · màn hình khác ngoài Home |
| Hướng phát triển | Collection: toàn bộ chuỗi nhận token thành flow phase `Parallel` (§6). Trần quảng bá: một điều kiện ở bước 4 của runner. Màn hình thứ hai: host thứ hai, runner dùng lại nguyên |

## Quyết định đã chốt

| Câu hỏi | Quyết định |
|---|---|
| Thứ tự giai đoạn | Thay đổi tiến trình → Tutorial → Ép xem đầy đủ tiến trình → Quảng bá; cố định trong code |
| Phase của giai đoạn 1 | `First` (đồng thời) xong hết mới tới `Second` (đồng thời) và `Parallel` (lần lượt) bắt đầu cùng lúc; giai đoạn 1 xong khi cả hai xong |
| Module thuộc phase nào | hằng trong code flow của module |
| Priority | remote config của từng module · số lớn chạy trước · thiếu key là `0`, đứng cuối · bằng nhau thì theo `ModuleId` (so ordinal) |
| Cờ force | remote config của từng module; chỉ module đọc, khung không biết |
| Ép xem khi nào | force bật **và** còn mốc mới đạt chưa xem **hoặc** còn cú tụt chưa xem; cộng điểm không qua mốc thì không ép. Force chỉ mở giao diện xem; nhận thưởng hay không do module |
| Vượt nhiều mốc trong một lần về Home | nằm trong một lần gọi của module: dừng ở từng mốc, nhận nếu module muốn, rồi chạy tiếp |
| Quảng bá | sau giai đoạn 3; mọi promo đến hạn chạy lần lượt |
| Yêu cầu chạy lại giữa lúc đang chạy | chạy xong lượt hiện tại rồi chạy bù **một** lượt trọn bốn giai đoạn |
| API Horcrux | chỉ thêm member và type mới |

## 1. Bốn giai đoạn

```
GĐ1  Thay đổi tiến trình        [First: đồng thời] ──► [Second: đồng thời]  ∥  [Parallel: lần lượt theo Priority]
GĐ2  Tutorial                   lần lượt theo Priority
GĐ3  Ép xem đầy đủ tiến trình   lần lượt theo Priority
GĐ4  Quảng bá                   lần lượt theo Priority
```

| Giai đoạn | Nghĩa | Được dùng tài nguyên độc quyền |
|---|---|---|
| 1 · `First`, `Second` | tiến trình tăng hoặc giảm, diễn trên icon/widget của chính module; phase sau dành cho live-ops cần ít chú ý hơn | **không** |
| 1 · `Parallel` | chuỗi dài chạy lần lượt mà không giữ chân phase `Second` (ceremony nhận thưởng) | có |
| 2 | tutorial lần đầu | có |
| 3 | mở giao diện cho người chơi xem tiến trình đầy đủ: mốc vừa đạt hoặc cú tụt | có |
| 4 | popup gói bán, khi mọi live-ops đã ổn định — tức giai đoạn 3 đã chạy xong, không đòi người chơi đã nhận hết | có |

**Tài nguyên độc quyền** là spotlight (lớp tối, tay chỉ) và màn hình mở chồng lên Home. Chúng chỉ có một bản, nên chỉ làn chạy lần lượt được dùng; hai flow cùng `First` mà cùng `Focus` là đè nhau. Hệ quả đã chấp nhận: flow `Parallel` làm tối màn thì animation của phase `Second` chạy dưới lớp tối.

## 2. Priority

`ILiveOpsModule.Priority` là độ ưu tiên dùng chung giữa các live-ops, cho Home flow và mọi tác vụ sau cần thứ tự giữa module. Module trả giá trị từ remote config của nó; base trả `0`.

Home flow dùng Priority ở `Parallel` và giai đoạn 2–4. `First` và `Second` chạy đồng thời nên không cần thứ tự.

Runner sắp xếp lại **mỗi lượt**. Priority đọc từ remote config và module đăng ký ở `Start`, nên một danh sách sắp sẵn sẽ cần một bất biến "ai làm mới, khi nào". Sắp lại chừng chục phần tử vào buffer dùng lại, ở nhịp mỗi lần về Home, rẻ hơn giữ bất biến đó.

## 3. Thành phần

| Thành phần | Tầng | Việc |
|---|---|---|
| `LiveOpsProgressPhase` | Horcrux Abstractions | enum `First, Second, Parallel` |
| `ALiveOpsHomeFlow` | Horcrux Abstractions, class thuần | `ProgressPhase` (virtual, mặc định `First`) · `PlayProgressChangeAsync` · `PlayTutorialAsync` · `PlayForcedReviewAsync` · `PlayPromotionAsync` — mỗi method `virtual`, mặc định trả về ngay; module override giai đoạn nó có |
| `ILiveOpsModule` | thêm | `int Priority { get; }` · `ALiveOpsHomeFlow HomeFlow { get; }` |
| `ALiveOpsModule` | thêm | `Priority => 0` và `HomeFlow => null` (virtual) · `protected RequestHomeFlow()` bắn `LiveOpsHomeFlowRequested` · `SetState` gọi nó sau `OnStateChanged` |
| `ILiveOpsHost` | thêm | `IReadOnlyList<ILiveOpsModule> Modules { get; }` |
| `LiveOpsHomeFlowRequested` | Horcrux Abstractions | sự kiện EventBus "có thứ mới để diễn", không payload |
| `LiveOpsHomeFlowRunner` | Horcrux Implementations, class thuần | lượt bốn giai đoạn, sắp xếp, cô lập lỗi, huỷ, chạy bù, một vòng bơm tại một thời điểm; không có Unity object nên test được |
| `LiveOpsHomeFlowHost` | Horcrux Implementations, component | ô kéo `LiveOpsHost`, đọc `Modules` · nghe sự kiện · `OnHomeEnter/OnHomeExit` · huỷ ở `OnDestroy` |
| `HomeFlowBridge` | game, `Assets/LiveOps/_Shared/Bridge` | `HomeVisibilityChangedEvent` → `host.OnHomeEnter/OnHomeExit` |
| Flow của module | game | `RocketRushHomeFlow`, `EndlessSaleHomeFlow` kế thừa `ALiveOpsHomeFlow` |

`ALiveOpsModule` là class duy nhất implement `ILiveOpsModule`, `LiveOpsHost` là class duy nhất implement `ILiveOpsHost`: thêm member vào hai interface không làm đỏ module nào.

## 4. Một lượt

```
ShowHome ─► HomeVisibilityChangedEvent ─► HomeFlowBridge ─► host.OnHomeEnter ─► Runner.OnHomeEnter ─► Request
SetState · icon gắn · tiến trình đổi · chu kỳ mới · cheat ─► LiveOpsHomeFlowRequested ─► Runner.Request (bỏ qua nếu không ở Home)

Request:   dirty = true;  chưa có vòng bơm thì mở PumpAsync
PumpAsync: while (ở Home && dirty && chưa quá 5 lượt liên tiếp)  dirty = false;  CTS mới;  RunPassAsync(ct)
           vẫn còn dirty sau 5 lượt: báo một lỗi, bỏ dirty
RunPassAsync:
  gom flow từ host.Modules (HomeFlow != null) vào buffer, sắp theo Priority
  GĐ1  WhenAll(First)  ─►  WhenAll( WhenAll(Second), lần lượt(Parallel) )
  GĐ2  lần lượt(mọi flow).PlayTutorialAsync
  GĐ3  lần lượt(mọi flow).PlayForcedReviewAsync
  GĐ4  lần lượt(mọi flow).PlayPromotionAsync
  mỗi lời gọi bọc riêng:  huỷ do host → thoát lượt  ·  lỗi khác → log kèm tên flow và giai đoạn, flow khác vẫn chạy

HideHome ─► host.OnHomeExit ─► Runner.OnHomeExit: xoá dirty, huỷ CTS ─► flow thoát qua finally
```

- **Bọc từng lời gọi, kể cả trong `WhenAll`.** `UniTask.WhenAll` kết thúc ngay ở lỗi đầu tiên trong khi các task khác vẫn chạy; không bọc thì một flow lỗi cắt cả phase và mọi giai đoạn sau.
- **Hai hàm duyệt cho mọi giai đoạn**: một "đồng thời", một "lần lượt"; giai đoạn chỉ khác thao tác gọi, truyền vào bằng delegate static. Cô lập lỗi và điểm thoát khi huỷ có đúng một bản.
- **Một vòng bơm tại một thời điểm.** Thoát rồi vào lại Home khi lượt cũ chưa dọn xong: lượt mới chỉ bắt đầu sau khi lượt cũ đã thoát hết qua `finally`.
- **Trần 5 lượt liên tiếp mỗi vòng bơm**: một flow tự yêu cầu chạy lại ở mọi lượt sẽ không có `await` nào chặn và treo Editor; trần biến nó thành một dòng lỗi.
- **Nguồn yêu cầu là sự kiện EventBus**: module không giữ tham chiếu host, `Init` các module không đổi.

## 5. Hợp đồng của flow

| Luật | Vì |
|---|---|
| **Idempotent**: đọc trạng thái đã lưu, chỉ làm phần chưa làm; không có gì thì trả về ngay | runner gọi mọi flow ở mọi giai đoạn, mọi lượt — tự trả về là cách "chỉ module đến hạn tham gia" |
| **Điểm tiêu thụ trạng thái chọn theo "mất gì nếu diễn lại"**: tiến độ cần thấy tiêu thụ **sau** khi diễn xong; lời chào một lần ghi **trước** | huỷ giữa chừng thì tiến độ diễn lại, lời chào không lặp |
| Kết thúc khi mọi thứ nó mở đã đóng | flow kế không bao giờ thấy màn hình chồng |
| Tài nguyên dùng chung trả trong `finally` | huỷ khi rời Home không để lại lớp tối |
| Icon/widget chưa gắn thì trả về ngay; gắn thì module gọi `RequestHomeFlow()` | runner chạy độc lập với việc view có mặt |
| **Trước `await` đầu tiên không đổi state module** | `Request` chạy đồng bộ bên trong `SetState`, giữa vòng tick của `LiveOpsHost`; đổi state lúc đó là `Evaluate` lồng trong `Evaluate` |
| `First`/`Second` không dùng tài nguyên độc quyền | §1 |

| Giai đoạn | Flow làm | Không làm |
|---|---|---|
| Thay đổi tiến trình | diễn từ mốc đã thấy tới sự thật, tăng hay giảm; vượt nhiều mốc thì dừng ở từng mốc | — |
| Tutorial | diễn khi chưa diễn | — |
| Ép xem đầy đủ tiến trình | force bật và còn mốc mới hoặc cú tụt chưa xem: mở giao diện, chờ đóng | ép nhận thưởng |
| Quảng bá | popup gói bán khi đến hạn, chờ đóng | — |

## 6. Từng module

**Rocket Rush** — phase `First`.

| GĐ | Làm | Điều kiện |
|---|---|---|
| 1 | sao bay vào icon từ `iconAnimatedPoints` tới `points`; tụt thì icon đặt thẳng về `points` (cú rơi diễn ở GĐ3); ghi `iconAnimatedPoints` khi bay xong | icon gắn · `IsReady` · `IsUnlocked` |
| 2 | tay chỉ icon → tháp → nút info → chờ intro đóng → **chờ tháp đóng** | `RocketRushRules.ShouldRunTutorial` |
| 3 | mở tháp, chờ đóng; tháp tự diễn từ `animatedPoints` tới `points` | `IsReady` · `forceReview` · `State != Inactive` (giữa hai cửa sổ không mở tháp của sự kiện đã đóng) · `IsUnlocked` · `points < animatedPoints` hoặc `ReachedStepsAmount(points) > ReachedStepsAmount(animatedPoints)` |

- Sao bay rút khỏi `RocketRushHomeIcon.Refresh`; `Refresh` chỉ vẽ trạng thái tĩnh: thanh nghỉ ở chỗ chuyến bay kế tiếp bắt đầu. "Còn nợ sao bay" là một hàm `RocketRushRules.IsStarFlightOwed` mà icon và flow cùng gọi, vì chỗ nghỉ và chỗ bắt đầu bay phải khớp.
- `iconAnimatedPoints` (save, mặc định `-1` = chưa có mốc, nên lần đầu đặt thẳng chứ không bay; `RollTo` đặt `0`) thay `displayedPoints` trên view: mốc "đã thấy" phải sống trong save, view bị tắt và dựng lại.
- Điều kiện GĐ3 suy hoàn toàn từ save: không có cờ runtime, bridge bỏ `module.OpenScreen()` khi quit, thứ tự event quit/về Home không còn quan trọng. Tutorial mở tháp thì tháp đã diễn tới `points`, GĐ3 tự không còn gì.
- Module bỏ `OnHomeEnter/OnHomeExit`, `_isHomeVisible`, `Start/Cancel` của flow; bridge bỏ nhánh nghe Home. `SetPoints`, `AttachHomeIcon`, `Debug_ResetTutorial` gọi `RequestHomeFlow()`.

**Endless Sale** — chỉ có GĐ4.

| GĐ | Làm | Điều kiện |
|---|---|---|
| 4 | ghi `playedOpenIntro` → sao bay vào icon → mở màn sale → chờ đóng | `Running` · `!PlayedOpenIntro` · icon gắn |

- Intro rút khỏi `EndlessSaleHomeIcon.Refresh`. `AttachHomeIcon` và `RollIntoNextCycle` gọi `RequestHomeFlow()`: chu kỳ mới nợ lời chào mới, mà state có thể vẫn `Running` qua lần roll.

**Collection** (ngoài Plan) — phase `Parallel`: chờ main screen đóng → `ConsumeEndedSummary` → ceremony có làm tối → bay token → bar dừng ở từng mốc và nhận → multiplier, tất cả trong `PlayProgressChangeAsync`; tutorial sang GĐ2. Widget đang gắn ở `Start`, trễ một frame sau sự kiện Home: chuyển sang `OnEnable` khi áp dụng, như hai icon kia.

## 7. Remote config và save

| Chỗ | Thêm | Mặc định | Ai đọc |
|---|---|---|---|
| `rocket_rush_operation` → `RocketRushOperationConfig` | `priority` (int) · `forceReview` (bool) | nháp của developer | `RocketRushModule` |
| `endless_sale_operation` → `EndlessSaleOperationConfig` | `priority` (int) | nháp của developer | `EndlessSaleModule` |
| `RocketRushProgressData` | `iconAnimatedPoints` | `-1`; `RollTo` đặt `0` | `RocketRushHomeFlow` qua module |

Save cũ không có `iconAnimatedPoints`: `JsonConvert` giữ giá trị khởi tạo `-1`, nên lần về Home đầu sau khi cập nhật đặt thẳng, không bay lại cả chu kỳ.

## 8. Trước khi chạy (sẽ nằm trong tài liệu hệ khi có code)

| # | Thao tác | Thiếu thì hỏng ở đâu |
|---|---|---|
| 1 | `Services.unity`: GameObject `live_ops_home_flow` ở gốc scene, *Add Component* `LiveOpsHomeFlowHost`, kéo `live_ops_host` (con của prefab instance `bootstrap_runner`) vào ô `Live Ops Host` | `NullReferenceException` ở `Awake` lần Play đầu |
| 2 | Cùng object, *Add Component* `HomeFlowBridge`, kéo chính `live_ops_home_flow` vào ô `Host` | thiếu ô: NRE ở lần Home đầu · thiếu component: không luồng Home nào chạy, không log |
| 3 | `RemoteConfigCollection.asset`: `rocket_rush_operation` → `value` đặt `Priority`, tick `Force Review` (key này `allowFetching: 0`, asset là giá trị chạy); `endless_sale_operation` → thêm `"priority"` vào JSON, cả trên Firebase | thiếu `priority`: mọi module bằng `0`, xếp theo `ModuleId` · thiếu `forceReview`: không bao giờ ép mở tháp, cú tụt chỉ thấy khi người chơi tự mở |

## 9. Test (agent viết sau khi developer code xong lõi)

`LiveOpsHomeFlowRunner`: bốn giai đoạn đúng thứ tự · `First` xong hết mới bắt đầu `Second` và `Parallel` · `Second` và `Parallel` bắt đầu cùng lúc, GĐ2 chỉ bắt đầu khi cả hai xong · flow trong `First`/`Second` chạy đồng thời · `Parallel` và GĐ2–4 chạy lần lượt, Priority lớn trước, bằng nhau theo `ModuleId` · module có `HomeFlow` null bị bỏ qua · lỗi một flow (trong làn đồng thời và trong làn lần lượt) được báo, flow khác và giai đoạn sau vẫn chạy · thoát Home huỷ lượt, không giai đoạn nào bắt đầu sau đó, huỷ không tính là lỗi · yêu cầu trong lúc chạy thì thêm đúng một lượt · nhiều yêu cầu gộp một · thoát Home xoá yêu cầu đang chờ · yêu cầu khi không ở Home bị bỏ qua · thoát rồi vào lại khi lượt cũ chưa dọn xong thì lượt mới chỉ chạy sau khi lượt cũ thoát · trần 5 lượt · danh sách rỗng · module đăng ký sau lượt đầu có mặt ở lượt sau · Priority đổi giữa hai lượt thì lượt sau theo thứ tự mới.

## 10. Rủi ro

- Flow treo vì lỗi logic chặn mọi giai đoạn sau tới khi rời Home. Tutorial chờ tap treo là chủ ý. Không có timeout (cố ý).
- Home hiện trước khi `HomeFlowBridge` bật thì chưa có lượt nào tới lần về Home sau.
- Endless Sale: rời Home giữa lúc sao bay thì lời chào mất trong chu kỳ đó.
- Mua hàng ở GĐ4 làm đổi tiến trình module khác: lượt bù diễn GĐ1 sau khi màn sale đóng, không diễn ngay.
