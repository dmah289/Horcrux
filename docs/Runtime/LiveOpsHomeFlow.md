# LiveOpsHomeFlow — điều phối trình diễn của live-ops lúc về Home

Mỗi lần về Home, trình diễn của mọi live-ops chạy theo **bốn giai đoạn cố định**: Thay đổi tiến trình → Tutorial → Ép
xem đầy đủ tiến trình → Quảng bá. Mỗi module giữ luồng của mình trong một `ALiveOpsHomeFlow`; một runner duy nhất gọi
các luồng theo giai đoạn, sắp theo `Priority` của module.

Code: `Runtime/Abstractions/Composites/LiveOps/` (`ALiveOpsHomeFlow.cs`, `ILiveOpsModule.cs`, `ALiveOpsModule.cs`,
`LiveOpsModulePriority.cs`) · `Runtime/Implementations/Composites/LiveOps/` (`LiveOpsHomeFlowRunner.cs`,
`LiveOpsHomeFlowHost.cs`) · test: `Tests/EditMode/LiveOpsHomeFlowRunnerTests.cs`. Nhịp tick và cửa sổ thời gian của
module ở `LiveOpsHost.md`.

## Ngữ cảnh đã chốt

| Mục | Nội dung |
|---|---|
| Người dùng | developer của game và của các live-ops thêm sau |
| Mục tiêu | trình diễn lúc về Home không chồng nhau, đúng thứ tự giai đoạn; live-ops mới chỉ override giai đoạn nó có |
| Ranh giới | Horcrux giữ giai đoạn, thứ tự, huỷ, chạy bù, cô lập lỗi. Dự án giữ luồng của từng module và cầu nối báo vào/ra Home |
| Cố ý KHÔNG làm | giai đoạn và phase thành dữ liệu cấu hình (runner viết thẳng đọc một mạch) · trần số popup quảng bá mỗi lần về Home · timeout cho luồng · lease hay hàng đợi trên tài nguyên độc quyền (luật ở §1 làm chồng chéo không xảy ra) · luồng ngoài live-ops · màn hình khác ngoài Home |
| Mở rộng sau | trần quảng bá: một điều kiện ở giai đoạn 4 của runner · màn hình thứ hai: host thứ hai, runner dùng lại nguyên |

## §1 Bốn giai đoạn

```
GĐ1  Thay đổi tiến trình        [First: đồng thời] ──► [Second: đồng thời]  ∥  [Parallel: lần lượt theo Priority]
GĐ2  Tutorial                   lần lượt theo Priority
GĐ3  Ép xem đầy đủ tiến trình   lần lượt theo Priority
GĐ4  Quảng bá                   lần lượt theo Priority
```

| Giai đoạn | Method của flow | Nghĩa | Dùng tài nguyên độc quyền |
|---|---|---|---|
| 1 · `First`, `Second` | `PlayProgressChangeAsync` | tiến trình tăng hoặc giảm, diễn trên icon/widget của chính module; `Second` cho live-ops cần ít chú ý hơn | **không** |
| 1 · `Parallel` | `PlayProgressChangeAsync` | chuỗi dài chạy lần lượt mà không giữ chân `Second` (ceremony nhận thưởng) | có |
| 2 | `PlayTutorialAsync` | tutorial lần đầu | có |
| 3 | `PlayForcedReviewAsync` | mở giao diện cho người chơi xem tiến trình đầy đủ: mốc vừa đạt, cú tụt | có |
| 4 | `PlayPromotionAsync` | popup gói bán, khi mọi live-ops đã ổn định — giai đoạn 3 đã chạy xong | có |

**Tài nguyên độc quyền** là spotlight (lớp tối, tay chỉ) và màn hình mở chồng lên Home. Chúng chỉ có một bản, nên chỉ
làn lần lượt được dùng; hai flow cùng `First` mà cùng `Focus` là đè nhau. Hệ quả đã chấp nhận: flow `Parallel` làm
tối màn thì animation của `Second` chạy dưới lớp tối.

`ProgressChangePhase` mặc định `Second`; `First` dành cho số ít live-ops được ưu tiên nên phải khai rõ.

## §2 Priority

`ILiveOpsModule.Priority` là độ ưu tiên dùng chung giữa các live-ops — Home flow và `LiveOpsLossHelper` cùng sắp bằng
`LiveOpsModulePriority.ByPriority`: số lớn trước · bằng nhau theo `ModuleId` (so ordinal). Module trả giá trị từ remote
config của nó; `ALiveOpsModule` trả `0`.

Runner sắp lại **mỗi lượt**. Priority đọc từ remote config và module đăng ký muộn, nên danh sách sắp sẵn cần một bất
biến "ai làm mới, khi nào"; sắp lại chừng chục phần tử vào buffer dùng lại, ở nhịp mỗi lần về Home, rẻ hơn giữ bất biến đó.

## §3 Thành phần

| Thành phần | Tầng | Việc |
|---|---|---|
| `LiveOpsProgressChangePhase` | Abstractions | enum `First`, `Second`, `Parallel` |
| `ALiveOpsHomeFlow` | Abstractions, class thuần | `ProgressChangePhase` và bốn method giai đoạn, đều `virtual`, mặc định trả về ngay; module override giai đoạn nó có |
| `ILiveOpsModule` | Abstractions | `int Priority` · `ALiveOpsHomeFlow HomeFlow` |
| `ALiveOpsModule` | Abstractions | `Priority => 0` · `HomeFlow => null` · `protected RequestHomeFlow()` bắn `LiveOpsHomeFlowRequested` · `SetState` gọi nó sau `OnStateChanged` |
| `ILiveOpsHost.Modules` | Abstractions | danh sách module runner đọc mỗi lượt |
| `LiveOpsHomeFlowRequested` | Abstractions | sự kiện EventBus "có thứ mới để diễn", không payload |
| `LiveOpsHomeFlowRunner` | Implementations, class thuần | lượt bốn giai đoạn, sắp xếp, cô lập lỗi, huỷ, chạy bù; không chạm Unity object nên test không cần engine |
| `LiveOpsHomeFlowHost` | Implementations, component | ô kéo `LiveOpsHost` · nghe `LiveOpsHomeFlowRequested` khi bật · `OnHomeEnter`/`OnHomeExit` cho cầu nối của dự án · lỗi thành `Debug.LogException` kèm `this` |
| Cầu nối Home | dự án | sự kiện hiện/ẩn Home của game → `host.OnHomeEnter()` / `host.OnHomeExit()` |
| Flow của module | dự án | class kế thừa `ALiveOpsHomeFlow`, module trả qua `HomeFlow` |

## §4 Một lượt

```
Home hiện ─► cầu nối dự án ─► host.OnHomeEnter ─► runner.OnHomeEnter ─► Request
SetState · module gọi RequestHomeFlow() ─► LiveOpsHomeFlowRequested ─► host ─► runner.Request (bỏ qua nếu không ở Home)

Request:        _hasPendingPassRequest = true;  chưa có vòng (!_isRunningPasses) thì mở RunPassesAsync
RunPassesAsync: while (ở Home && _hasPendingPassRequest && passAmount < 3)  xoá cờ;  CTS mới;  RunPassAsync(ct)
                vẫn còn request sau 3 lượt: báo một lỗi, bỏ request
RunPassAsync:
  gom module có HomeFlow != null, sắp theo ByPriority, chia theo ProgressChangePhase
  GĐ1  WhenAll(First)  ─► (đã huỷ thì thoát) ─►  WhenAll( WhenAll(Second), lần lượt(Parallel) )
  GĐ2  lần lượt(mọi flow).PlayTutorialAsync
  GĐ3  lần lượt(mọi flow).PlayForcedReviewAsync
  GĐ4  lần lượt(mọi flow).PlayPromotionAsync
  mỗi lời gọi bọc riêng: huỷ bằng token của runner → im lặng · lỗi khác → báo kèm ModuleId và tên giai đoạn, flow khác vẫn chạy

Home ẩn ─► host.OnHomeExit ─► runner.OnHomeExit: xoá _hasPendingPassRequest, huỷ CTS ─► flow thoát qua finally
```

- **Bọc từng lời gọi, kể cả trong `WhenAll`.** `UniTask.WhenAll` kết thúc ở lỗi đầu tiên trong khi task khác vẫn
  chạy; không bọc thì một flow lỗi cắt cả phase và mọi giai đoạn sau.
- **Hai hàm duyệt cho mọi giai đoạn**: `RunSimultaneousAsync` và `RunSequentialAsync`; giai đoạn chỉ khác thao tác gọi,
  truyền vào bằng delegate static. Cô lập lỗi và điểm thoát khi huỷ có đúng một bản (`RunIsolatedAsync`). Làn lần lượt
  kiểm huỷ trước mỗi flow.
- **Huỷ chỉ im lặng khi là token của runner** (`catch ... when (ct.IsCancellationRequested)`). Flow ném
  `OperationCanceledException` từ token khác vẫn được báo là lỗi.
- **Một lượt** là một lần chạy trọn `RunPassAsync`. Request đến giữa lượt bật lại cờ nên có đúng một lượt sau; nhiều
  request gộp một.
- **Mỗi lúc chỉ một vòng chạy lượt** (`_isRunningPasses`). Ra rồi vào lại Home khi lượt cũ chưa dọn xong: lượt mới chỉ
  bắt đầu sau khi lượt cũ đã thoát hết qua `finally`.
- **Trần 3 lượt liên tiếp mỗi vòng** (`MaxPassesPerBurst`; chơi bình thường một vòng chỉ 1–2 lượt): một flow tự yêu cầu
  chạy lại ở mọi lượt sẽ không có `await` nào chặn và treo Editor; trần biến nó thành một dòng lỗi.
- **Nguồn yêu cầu là sự kiện EventBus**: module không giữ tham chiếu host. Host chỉ nghe khi bật (`OnEnable`/`OnDisable`);
  `OnDestroy` huỷ lượt đang chạy.

## §5 Hợp đồng của flow

| Luật | Vì |
|---|---|
| **Idempotent**: đọc trạng thái đã lưu, chỉ làm phần chưa làm; không có gì thì trả về ngay | runner gọi mọi flow ở mọi giai đoạn, mọi lượt — tự trả về là cách "chỉ module đến hạn tham gia" |
| **Điểm tiêu thụ trạng thái chọn theo "mất gì nếu diễn lại"**: tiến độ cần thấy tiêu thụ **sau** khi diễn xong; lời chào một lần ghi **trước** | huỷ giữa chừng thì tiến độ diễn lại, lời chào không lặp |
| Mốc "đã thấy" nằm trong save, không trên view | view bị tắt và dựng lại; điều kiện của mọi giai đoạn suy được từ save |
| Kết thúc khi mọi thứ nó mở đã đóng | flow kế không bao giờ thấy màn hình chồng |
| Tài nguyên dùng chung trả trong `finally` | huỷ khi rời Home không để lại lớp tối |
| Icon/widget chưa gắn thì trả về ngay; gắn thì module gọi `RequestHomeFlow()` | runner chạy độc lập với việc view có mặt |
| Icon/widget tự đăng ký với module ở `OnAwake`, gỡ ở `OnDestroy` | `Start` chạy sau sự kiện Home lúc trang dựng lần đầu nên lượt đầu bỏ qua module; `OnEnable`/`OnDisable` gỡ view khỏi module ngay lần nó tự ẩn |
| **Trước `await` đầu tiên không đổi state module** | `Request` chạy đồng bộ bên trong `SetState`, giữa vòng tick của `LiveOpsHost`; đổi state lúc đó là `Evaluate` lồng trong `Evaluate` |
| `First`/`Second` không dùng tài nguyên độc quyền | §1 |
| Vượt nhiều mốc trong một lần về Home nằm trong **một** lời gọi: dừng ở từng mốc, nhận nếu module muốn, rồi chạy tiếp | runner không biết mốc; tách ra nhiều lượt là chạy lại cả bốn giai đoạn giữa hai mốc |
| Cờ "ép xem" (nếu có) là remote config của module, chỉ module đọc | khung không biết lý do ép; force chỉ mở giao diện xem, nhận thưởng hay không do module |

## Trước khi chạy

| # | Thao tác | Thiếu thì hỏng ở đâu |
|---|---|---|
| 1 | Trong scene sống suốt phiên chứa `LiveOpsHost`: tạo GameObject `live_ops_home_flow`, *Add Component* `LiveOpsHomeFlowHost`, kéo object mang `LiveOpsHost` vào ô `Live Ops Host` | `NullReferenceException` ở `Awake` lần Play đầu |
| 2 | Dự án viết cầu nối: Home hiện thì gọi `host.OnHomeEnter()`, Home ẩn thì gọi `host.OnHomeExit()`; kéo `live_ops_home_flow` vào ô của cầu nối | thiếu cầu nối: không lượt nào chạy, không log · thiếu ô: NRE ở lần về Home đầu |
| 3 | Mỗi module có trình diễn: override `HomeFlow` trả **một** instance flow giữ trong field, override `Priority` đọc từ remote config của module | thiếu `HomeFlow`: module bị bỏ qua, không log · thiếu `Priority`: mọi module bằng `0`, xếp theo `ModuleId` |
| 4 | Cầu nối bật trước lần hiện Home đầu tiên | Home hiện trước khi cầu nối nghe thì chưa có lượt nào tới lần về Home sau |

## Kiểm

EditMode `LiveOpsHomeFlowRunnerTests` (agent chạy qua compile-check), mỗi ca một test:

| Nhóm | Ca |
|---|---|
| Thứ tự | bốn giai đoạn đúng thứ tự · `First` xong hết mới bắt đầu `Second` và `Parallel` · `Second` và `Parallel` bắt đầu cùng lúc, GĐ2 chờ cả hai · nhiều flow `First` chạy đồng thời · `Parallel` và GĐ2–4 theo Priority rồi `ModuleId` · flow không khai phase là `Second` |
| Danh sách | module `HomeFlow` null bị bỏ qua · danh sách rỗng · module thêm sau lượt đầu có mặt ở lượt sau · Priority đổi giữa hai lượt thì lượt sau theo thứ tự mới |
| Lỗi | lỗi ở làn đồng thời và làn lần lượt đều được báo, phần còn lại vẫn chạy · flow đòi chạy lại mọi lượt dừng ở 3 lượt với một lỗi |
| Huỷ, chạy bù | rời Home giữa GĐ2 thì không giai đoạn nào sau đó chạy, không tính là lỗi · ba request trong một lượt thêm đúng một lượt · request rồi rời Home thì bỏ lượt chờ · request khi không ở Home không chạy gì · vào lại khi lượt cũ đang dọn thì lượt mới chạy sau · rời Home khi không có lượt nào không ném, vào lại chạy trọn lượt |

Trình diễn của từng module là cảm giác chơi: developer kiểm bằng chơi thử.

## Rủi ro đã chấp nhận

- Flow treo vì lỗi logic chặn mọi giai đoạn sau tới khi rời Home. Tutorial chờ tap treo là chủ ý; không có timeout.
- Rời Home giữa một lời chào đã ghi "đã chào" trước khi diễn thì lời chào mất trong chu kỳ đó.
- Mua hàng ở GĐ4 làm đổi tiến trình module khác: lượt bù diễn GĐ1 sau khi màn quảng bá đóng, không diễn ngay.
