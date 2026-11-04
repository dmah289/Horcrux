# UserSegmentationSimulator — thử camp, xem pool đã kế thừa, kiểm dữ liệu phân tập

Code: `Editor/UserSegmentation/`.

Một cửa sổ đọc dữ liệu qua chính host phân tập đang chạy trong Play: gõ camp thấy tập nào khớp bằng keyword nào, lưới pool hiện
hàng `purchased` đã kế thừa từ một hàng chọn làm prev, nút **Kiểm hợp lệ** chạy luật boot của lõi cộng ba kiểm chỉ tool làm được.
Ngoài Play có một nút xoá save phân tập để lần Play sau chạy như cài mới. Hệ: `docs/Runtime/UserSegmentation/UserSegmentation.md`.

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | developer khi soạn pool và bảng keyword, khi thử camp mới của UA; agent khi nghiệm thu dữ liệu phân tập |
| Đầu vào | host `AUserSegmentationHost` trong scene đang Play (tự tìm, đổi được ở ô **Host**); ô **Camp**; ô **Save collection** |
| Ranh giới | chỉ đọc host: không gọi `ReportCampaign`, `ReportPurchase`, không sửa remote config; chỉ ghi là xoá một key PlayerPrefs |
| Chỗ đặt | `Editor/UserSegmentation/`, menu `Horcrux > User Segmentation > Simulator`, assembly `com.horcrux.editor` |

**Quyết định trái trực giác**

| Quyết định | Vì |
|---|---|
| Chỉ có dữ liệu trong Play | host nhận nguồn pool và bảng keyword qua `Init` của InitArgs; ngoài Play chưa ai tiêm, getter `Configs` ném `NullReferenceException`. Đọc qua host thay vì tự dò biến remote config theo kiểu: tool thấy đúng thứ host thấy, không có bản thứ hai của getter |
| Thấy giá trị host đang giữ, không tự fetch | remote chưa setup thì đó là giá trị trên asset; fetch đã chạy trong phiên thì là giá trị console |
| Ba property `protected` của host (`KeywordRules`, `DefaultSegmentId`, `PurchasedSegmentId`) và `Configs` đọc bằng reflection | tool không gọi tên type dự án; `Configs` khai ở lớp generic nên tool lần chuỗi `BaseType` tới `AUserSegmentationHost<TConfig>` (type con không thấy member không public của lớp cha) |
| Hàng `purchased` gọi `InheritFrom` của DTO, kiểm chạy `UserSegmentConfigTable.Validate` | cùng bản host chạy lúc boot; tool không viết lại luật nào |
| Cột lưới quét field `public` kiểu `int` và `string` của `TConfig` | reflection trên DTO chỉ sống ở Editor; lõi runtime không biết cột |
| Key save lấy bằng hàm private `GetFinalKey` của collection | xoá đúng key runtime ghi, không chép tiền tố `persistence_` sang tool |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Mô phỏng `UserSegmentDecider` (khoá camp, số phiên, đã mua) | ô Camp chỉ trả lời "camp này khớp keyword nào"; luật chuyển tập đã có test EditMode và log `[UserSegmentation]` lúc chạy |
| Ghi vào host | thử chuyển tập thật thuộc cheat của dự án (`ReportCampaign`, `ReportPurchase`) |
| Kiểm nền mỗi lần dữ liệu đổi | kiểm chạy khi bấm |

## Luồng dữ liệu

```
Play ──► host (ô Host, tự tìm) ──► IsReady? ──► reflection: KeywordRules · DefaultSegmentId · PurchasedSegmentId · Configs · TConfig
                                                     │  (dựng lại khi host trả list khác: fetch thay list)
         ┌───────────────────────────────────────────┼───────────────────────────────────────────┐
         ▼                                           ▼                                           ▼
 Camp ──► UserSegmentKeywordMatcher.TryMatch   Pool: mỗi hàng một dòng, cột = field       Kiểm hợp lệ ──► UserSegmentationSimulatorCore.Check
   tập, keyword, cờ nhiều tập khớp;            hàng purchased = InheritFrom(prev chọn)       Validate của lõi + (a) (b) (c) dưới
   hàng thắng tô nền, keyword thắng in đậm     ô -1 gốc tô xanh, tooltip "lấy từ tập N"       danh sách lỗi hoặc "Hợp lệ"

Ngoài Play: Save collection ──► GetFinalKey(UserSegmentState.Key) ──► hộp xác nhận ──► PlayerPrefs.DeleteKey
```

## Ba kiểm chỉ tool làm

Lõi cố ý chỉ kiểm hình dạng dữ liệu và vai `default` (`UserSegmentation.md` › Guard dữ liệu); ba kiểm dưới cần nhìn cột hoặc vai
`purchased` nên sống ở đây.

| Kiểm | Báo khi | Nếu lọt |
|---|---|---|
| (a) `-1` sai hàng | hàng không phải `purchased` có ô `int` (trừ cột id) bằng `-1` | tập đó chạy với giá trị `-1`: quảng cáo từ level −1, giá âm |
| (b) `InheritFrom` bỏ sót cột | hàng toàn `-1` kế thừa từ hàng `default` mà một cột còn `-1` trong khi hàng `default` có số | cột mới quên dòng `Pick`: user `purchased` chạy với `-1` ở cột đó |
| (c) keyword trỏ `purchased` | hàng keyword có `SegmentId` bằng tập `purchased` | camp khớp hàng đó rơi về `default` không một dòng log, hàng dưới không được xét |

Cột id tìm bằng thử: gán một số dò vào từng field `int` của một hàng mới, field nào làm `Id` đổi theo là cột id.

## Trước khi chạy

1. Scene boot có host phân tập đã wire theo `UserSegmentation.md` › Trước khi chạy. Thiếu: cửa sổ báo "không có AUserSegmentationHost".
2. **Horcrux → User Segmentation → Simulator**, bấm Play, chờ bootstrap xong. Chưa boot: cửa sổ báo "Host chưa boot".
3. Xoá save: dừng Play, ô **Save collection** (tự điền khi dự án có đúng một asset `BasePersistenceDataCollection`), bấm **Xoá save
   phân tập**, xác nhận. Entry chưa có `Key`: cửa sổ báo, không xoá gì.

## Nghiệm thu

`Tests/EditMode/UserSegmentationSimulatorTests.cs`:

| Ca | Kỳ vọng |
|---|---|
| cột của DTO | chỉ field `public int`, theo thứ tự khai |
| cột id | field đứng sau `Id` |
| `Inherit` | gọi `InheritFrom` của DTO: ô `-1` lấy từ prev, ô có số giữ |
| dữ liệu hợp lệ | 0 dòng |
| `-1` ở hàng không phải `purchased` | một dòng nêu tập và field |
| DTO quên `Pick` một cột | một dòng nêu cột |
| keyword trỏ `purchased` | một dòng nêu chỉ số hàng |
| bảng keyword rỗng · thiếu hàng `default` | dòng của `Validate`; kiểm (b) bỏ qua khi không có hàng `default` |
