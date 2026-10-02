# LiveOpsHomeFlow — điều phối các luồng trình diễn lúc về Home (Thiết kế)

Nhiều live-ops cùng trình diễn lúc về Home (ceremony, bay thưởng, tutorial, mở màn con) mà mỗi module tự khởi động thì chồng nhau. Thiết kế này cho các luồng đó chạy **tuần tự dưới một điều phối viên**: mỗi module giữ luồng của mình, thứ tự là thứ tự `await` trong một danh sách kéo thả. Code và test: `Assets/LiveOps/_Shared/LiveOpsHomeFlow_Plan.md`. Tài liệu này thành `LiveOpsHomeFlow.md` khi code xong.

## Ngữ cảnh đã chốt

| Mục | Nội dung |
|---|---|
| Người dùng | Developer của game (Collection, Rocket Rush, Endless Sale) và các live-ops thêm sau |
| Mục tiêu | Mọi trình diễn lúc về Home của live-ops chạy **lần lượt, không chồng nhau**; thêm live-ops mới là thêm một phần tử vào danh sách |
| Phạm vi | Mọi trình diễn lúc về Home của ba module hiện có |
| Cố ý KHÔNG làm | Số ưu tiên · lease/hàng đợi trên spotlight · tự đăng ký (thứ tự sẽ phụ thuộc thứ tự `Start`) · timeout cho flow · luồng ngoài live-ops · màn hình khác ngoài Home · code canh module quên kéo vào danh sách (ghi vào "Trước khi chạy") |
| Hướng phát triển | Luồng ngoài live-ops: đổi kiểu phần tử của mảng host sang interface rộng hơn. Màn hình thứ hai: host thứ hai và property thứ hai; `HomeFlowRunner` không biết "Home" nên dùng lại nguyên |

## Quyết định đã chốt

| Câu hỏi | Quyết định |
|---|---|
| Luồng về Home gồm gì | Mọi trình diễn lúc về Home |
| Thứ tự | Danh sách có thứ tự kéo thả trên host; thứ tự trong mảng là thứ tự `await` |
| Yêu cầu chạy lại giữa lúc đang chạy | Chạy xong lượt hiện tại rồi chạy bù **một** lượt (trần 5 lượt mỗi lần yêu cầu) |
| Vị trí | Component riêng cạnh `LiveOpsHost` (nhịp tick và cửa sổ thời gian khác với trình tự Home) |
| Ai bắn yêu cầu chạy lại khi đổi trạng thái | `ALiveOpsModule.SetState` tự bắn |
| Mở tháp khi quit làm rơi điểm | Bridge đặt cờ runtime, flow Rocket Rush tiêu thụ |

## 1. Thành phần

| Thành phần | Tầng | Việc | Không làm |
|---|---|---|---|
| `ILiveOpsHomeFlow` | Horcrux | `UniTask RunAsync(CancellationToken ct)`: làm phần còn thiếu rồi trả về | không biết module nào |
| `LiveOpsHomeFlowRequested` | Horcrux | sự kiện EventBus "module có thứ mới để diễn" | |
| `ALiveOpsModule.HomeFlow` | Horcrux | property virtual, mặc định `null`; module trả luồng của mình. `RequestHomeFlow()` (protected) bắn sự kiện; `SetState` gọi nó sau `OnStateChanged` | |
| `HomeFlowRunner` | Horcrux, class thuần | toàn bộ cơ chế: tuần tự, cô lập lỗi, huỷ, chạy bù, một vòng bơm tại một thời điểm | không có Unity object, nên test được |
| `LiveOpsHomeFlowHost` | Horcrux, component | giữ `ALiveOpsModule[]` có thứ tự, dựng danh sách flow lười lúc vào Home đầu tiên, nghe sự kiện, huỷ ở `OnDestroy` | không chứa logic |
| `HomeFlowBridge` | game, `Assets/LiveOps/_Shared/Bridge` | `HomeVisibilityChangedEvent` → `host.OnHomeEnter/OnHomeExit` | thay các nhánh nghe Home trong bridge từng module |
| Flow của module | game | trình diễn của chính module | không `Start/Cancel`, không `CancellationTokenSource` riêng |

`ICanvasSpotlight` không đổi: các luồng chạy tuần tự nên không bao giờ `Focus` chồng nhau.

## 2. Luồng chạy

```
ShowHome ─► HomeVisibilityChangedEvent ─► HomeFlowBridge ─► host.OnHomeEnter ─► Runner.OnHomeEnter ─► Request
module.SetState / icon·widget gắn / cờ trình diễn mới ─► LiveOpsHomeFlowRequested ─► Request (bỏ qua nếu không ở Home)

Request:  dirty = true;  nếu chưa có vòng bơm thì mở PumpAsync
PumpAsync: while (ở Home && dirty && chưa quá 5 lượt)
              dirty = false;  CTS mới;  foreach flow theo thứ tự:  try { await flow.RunAsync(ct) }
                                                                    catch (huỷ do host) { thoát lượt }
                                                                    catch (lỗi khác)   { báo, flow sau vẫn chạy }
           nếu vẫn còn dirty sau 5 lượt: báo một lỗi, bỏ dirty

HideHome ─► host.OnHomeExit ─► Runner.OnHomeExit: xoá dirty, huỷ CTS ─► flow thoát qua finally
```

## 3. Hợp đồng của một flow

| Luật | Vì |
|---|---|
| **Idempotent**: đọc trạng thái đã lưu (token chờ, `playedTutorial`, `playedOpenIntro`, điểm chưa bay…), chỉ làm phần chưa làm; không có gì thì trả về ngay | lượt chạy bù và lần về Home sau đều gọi lại mọi flow |
| **Điểm tiêu thụ trạng thái do flow chọn theo "mất gì nếu diễn lại"**: tiến độ người chơi cần thấy (token, điểm bay) tiêu thụ **sau** khi diễn xong, nên huỷ thì lần sau diễn lại; lời chào một lần (Endless Sale) ghi **trước**, vì lặp lại là phiền | hai loại trình diễn có giá khác nhau khi lặp |
| Kết thúc khi mọi thứ nó mở đã đóng (màn hình con, popup) | flow kế không bao giờ thấy màn hình chồng |
| Tài nguyên dùng chung (spotlight, tay chỉ) trả trong `finally` | huỷ khi rời Home không để lại lớp tối |
| Trả về ngay khi widget hoặc icon của nó chưa gắn; widget gắn thì module gọi `RequestHomeFlow()` | host chạy độc lập với việc widget có mặt |
| Lỗi một flow được bắt và log kèm tên flow | một flow lỗi không chặn flow sau; đây là code của người khác chạy trong vòng lặp của host |

## 4. Chạy lại và rời Home

- Nguồn yêu cầu là sự kiện EventBus; module không giữ tham chiếu host nên `Init` các module không đổi.
- Host chỉ nhận khi đang ở Home. Đang chạy thì đặt dirty, xong thì chạy bù đúng một lượt (nhiều yêu cầu gộp một).
- Nguồn: `ALiveOpsModule.SetState` (mọi chuyển trạng thái đều đi qua đây nên module mới không thể quên) · icon hoặc widget gắn muộn · cờ trình diễn mới do module đặt (cờ mở tháp, điểm đổi do cheat) · cheat đặt lại tutorial.
- **Một vòng bơm tại một thời điểm.** Thoát rồi vào lại Home khi lượt cũ chưa dọn xong: lượt mới chỉ bắt đầu sau khi lượt cũ đã thoát hết qua `finally`, nên phần dọn của flow cũ không bao giờ chạy đè lên flow mới.
- **Trần 5 lượt** mỗi lần yêu cầu: một flow tự yêu cầu chạy lại ở mọi lượt sẽ không có `await` nào chặn và treo Editor; trần biến nó thành một dòng lỗi.

## 5. Từng module

| Module | Flow gồm (theo thứ tự) | Thay đổi chính |
|---|---|---|
| Collection | ceremony nhận token → bay token → multiplier → tutorial (thân `RunAsync` hiện có) | bỏ `Start/Cancel`, `OnHomeEnter/Exit`, `_isHomeVisible`, `spotlightPriority`, `AcquireAsync`. Guard `Widget == null`. Bridge bỏ nhánh nghe Home. `AttachWidget` bắn yêu cầu chạy lại |
| Rocket Rush | bay sao vào icon → mở tháp nếu có cờ (rồi chờ đóng) → tutorial lần đầu | star-fly rút khỏi `Refresh` của icon thành `PlayWonPointsAsync(ct)`; `Refresh` chỉ vẽ trạng thái tĩnh. Cờ `HasPendingTowerReveal` thay cho `module.OpenScreen()` trong bridge. `SetPoints` bắn yêu cầu chạy lại |
| Endless Sale | ghi `playedOpenIntro` → bay sao → mở màn hình → chờ đóng | intro rút khỏi `Refresh` của icon; `OpenScreenAndWaitAsync` và `WaitClosedAsync` |

## 6. Trước khi chạy (sẽ nằm trong tài liệu hệ khi có code)

| # | Thao tác | Thiếu thì hỏng ở đâu |
|---|---|---|
| 1 | `Services.unity`: thêm `LiveOpsHomeFlowHost` (cạnh `live_ops_host`) và `HomeFlowBridge`; kéo host vào ô của bridge | không có luồng Home nào chạy, không log |
| 2 | Kéo `collection`, `rocket_rush`, `endless_sale` vào mảng `Modules` của host **theo thứ tự muốn chạy** (đề xuất: Collection → Rocket Rush → Endless Sale, vì Endless Sale mở cả màn hình) | module không có trong mảng thì không có luồng Home và không báo lỗi; ô trống thì `LogError` ở lần vào Home đầu |

## 7. Test (agent viết sau khi developer code xong lõi)

`HomeFlowRunner`, 11 case: đúng thứ tự · flow kế chỉ chạy sau khi flow trước xong · lỗi một flow được báo và flow sau vẫn chạy · thoát Home huỷ flow đang chạy và bỏ phần còn lại, huỷ không tính là lỗi · yêu cầu trong lúc chạy thì thêm đúng một lượt · nhiều yêu cầu gộp một lượt · thoát Home xoá yêu cầu đang chờ · yêu cầu khi không ở Home bị bỏ qua · thoát rồi vào lại khi lượt cũ chưa dọn xong thì lượt mới chỉ chạy sau khi lượt cũ thoát · trần 5 lượt · danh sách rỗng.

## 8. Rủi ro

- Flow treo vì lỗi logic chặn mọi flow sau tới khi rời Home. Tutorial chờ tap treo là chủ ý. Chưa có timeout (cố ý).
- Home hiện trước khi `HomeFlowBridge` bật thì chưa có lượt nào tới lần về Home sau (hành vi cũ của Collection cũng vậy).
- Endless Sale: rời Home giữa lúc sao bay thì lời chào mất trong chu kỳ đó.
- Tutorial Rocket Rush chặn intro Endless Sale phía sau tới khi intro tutorial đóng; thứ tự trong mảng quyết định điều này.
