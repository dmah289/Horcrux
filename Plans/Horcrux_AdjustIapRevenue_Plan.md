# Plan — `AdjustIapRevenue`: doanh thu IAP lên Adjust, vào Horcrux `Vendors/Adjust`

Ngày 2026-11-23. Nguồn: category-jam `Assets/_Modules/Sdk/AdjustIapTracking.cs`. File này chép nguyên thân
`AdjustService.TrackEventIap` của ScrewDom; hai dự án đã có cùng đoạn code. Lõi dựng `AdjustEvent` từ `PendingOrder` của Unity IAP 5:
doanh thu sau phí store, `ProductId`, khoá chống trùng theo store (iOS `TransactionId`, Google `PurchaseToken`). Phần ở lại dự án:
token event lấy ở đâu, cổng anti-cheat, chỗ gọi trong vòng mua.

**Phân công:**
- Developer gõ GĐ1–2.
- Agent viết test GĐ1 theo bảng case.
- Agent làm GĐ3: dự án chuyển sang bản Horcrux, xoá bản tạm, viết tài liệu.

**Chạy được, thấy được:**
- GĐ1: compile sạch và bảng case xanh.
- GĐ2: compile sạch trên cả hai define `UNITY_ANDROID` và `UNITY_IOS`.
- GĐ3: event lên Adjust sandbox khi QA mua thử.

Lệnh kiểm từ gốc dự án:
`bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.adjust.purchasing.csproj --tests Horcrux.Vendors.Adjust.Purchasing.Tests`.

GĐ1–2 commit trong `Assets/Horcrux`; GĐ3 commit ở dự án kèm con trỏ submodule. Không phụ thuộc Plan nào, không có lõi toán.

Code GĐ1–2 đã compile thử bằng csc trên DLL của dự án (Adjust SDK 5.5.1, Unity IAP 5.4.0, C# 9) với cả `UNITY_ANDROID` lẫn
`UNITY_IOS`. Hàm đọc receipt đã chạy qua eval trong Editor với đủ các case ở GĐ1.

---

## Ngữ cảnh đã chốt

- **Người dùng:** lớp IAP của dự án (chủ vòng mua Unity IAP 5) gọi một lần cho mỗi `PendingOrder` mới, trước khi confirm. Môi
  trường Adjust (production, sandbox) là cái SDK đã khởi tạo. Cheat sandbox của SDK (Zego `SwitchSandboxMode`) vì thế áp luôn cho
  event này.
- **Chỗ đặt:**
  - Thư mục `Vendors/Adjust/Purchasing/`, assembly riêng `com.horcrux.adjust.purchasing`.
  - Namespace `Horcrux.Vendors.Adjust`, cùng namespace với `AdjustCampaignBootStep`.
  - Lý do tách assembly: dự án có Adjust mà không có IAP vẫn compile `com.horcrux.adjust` như cũ.
- **Hành vi giữ nguyên từng byte gửi đi:** doanh thu = `double` giá × `float` tỉ lệ (0.7f Android, 0.65f iOS), cùng hai callback
  parameter `tax_included = "TRUE"`, `tax_amount = "0.7"` hay `"0.65"`. Đây là **dữ liệu ghi ra**: dashboard và báo cáo đang đọc
  đúng những số này.
- **Không làm:**
  - Cổng anti-cheat trong hàm: mỗi dự án một hệ anti-cheat, chỗ gọi giữ cổng.
  - Đọc token từ asset cấu hình: dự án truyền vào, mỗi dự án để token một nơi.
  - Contract `IAttributionService`: `SystemPlan` §13 còn chưa dựng, và hàm tĩnh này chưa cần nó.
  - Giỏ nhiều món: game mua một món mỗi đơn.
  - Đường Unity IAP 4 (`PurchaseEventArgs`): dự án IAP 4 không phải người dùng hiện có.
- **Quyết định trái trực giác:**
  - Tỉ lệ để kiểu `float`, không phải `0.7` kiểu `double`. Đổi sang `double` làm doanh thu 0.99 USD gửi đi thành `0.693` thay vì
    `0.69299998819828035` (đo bằng eval). Đó là đổi dữ liệu ghi ra, chờ developer quyết; xem "Mở rộng sau".
  - Chuỗi `tax_amount` sinh từ chính hằng tỉ lệ (`ToString(InvariantCulture)` ra `"0.7"`, `"0.65"`, đã đo), không còn hai bản
    viết tay phải khớp nhau.
- **Mở rộng sau:**
  - Tỉ lệ `double`: chỉ đổi kiểu một hằng, kèm ghi chú cho đội đọc số liệu.
  - Đơn không có sản phẩm: bản dự án bỏ qua im lặng, bản này `LogError`. Chỉ log đổi, dữ liệu gửi đi không đổi.

### Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| `AdjustIapTracking.cs` dự án | toàn bộ hành vi: tỉ lệ, ba lớp JSON của receipt Google, callback parameter, guard token rỗng | LINQ `FirstOrDefault`; ép kiểu `(Dictionary<…>)` và `(string)` ném khi receipt sai hình |
| `IAPControl.cs` dự án | chỗ gọi: trong `try/catch` riêng trước confirm, cổng `IsCheater` | — ở lại dự án |
| Unity IAP 5.4.0 | `Order.CartOrdered.Items()` (`IReadOnlyList<CartItem>`), `Order.Info.{TransactionID, Receipt}`, `UnityEngine.Purchasing.MiniJson` (assembly `Unity.Purchasing.Utilities`) | — |
| Horcrux `Vendors/Adjust` | khuôn asmdef `versionDefines` + `defineConstraints`; gọi `AdjustSdk.Adjust` đầy đủ vì trong namespace `Horcrux.Vendors.Adjust` chữ `Adjust` là namespace | — |
| Zego `ZegoAdjust.TrackEventIap` | xác nhận cùng cách gửi | chỉ chạy khi có class cài `Zego.IIapService`; nhánh `#if` đi theo define của dự án |

## Luồng mô tả — một lần mua tới event trên Adjust

```
Unity IAP báo PendingOrder mới ──► lớp IAP dự án trao thưởng
        ▼
cổng anti-cheat của dự án ──► AdjustIapRevenue.Track(order, token)
        ▼
token rỗng? ──có──► LogError, dừng                                                     GĐ2
        ▼
món đầu của giỏ ──► Product? ──không──► LogError, dừng
        ▼
AdjustEvent(token): revenue = giá × tỉ lệ, currency, ProductId
        ├─ iOS: TransactionId
        └─ Android: PurchaseToken = ReadGooglePurchaseToken(receipt)                    GĐ1
              receipt ─"Payload"─► payload ─"json"─► json ─"purchaseToken"─► token ("" nếu thiếu một tầng)
        ▼
tax_included, tax_amount ──► AdjustSdk.Adjust.TrackEvent ──► môi trường SDK đã khởi tạo
        ▼
lớp IAP dự án confirm đơn                                                               GĐ3
```

## Bản đồ hệ cuối

```
  dự án: lớp IAP (Core.Sdk) ── IsCheater? ──► AdjustIapRevenue.Track ──► AdjustSdk.Adjust.TrackEvent
         token: IAdjustSetting                     │
                                                   └─► ReadGooglePurchaseToken ──► MiniJson (Unity.Purchasing.Utilities)
  test: Horcrux.Vendors.Adjust.Purchasing.Tests ──► ReadGooglePurchaseToken (internal, InternalsVisibleTo)
```

---

## Giai đoạn 1 — Assembly và hàm đọc purchase token của Google

**Mục tiêu:** từ chuỗi receipt Unity IAP lấy ra purchase token của Google Play; mọi receipt khác hoặc hỏng trả `""`, không ném.

**Vì sao bây giờ:** đây là phần duy nhất có logic nhận dữ liệu ngoài, kiểm được không cần store. Đặt trước `Track` để có bảng case
xanh trước khi nối SDK.

### `Vendors/Adjust/Purchasing/com.horcrux.adjust.purchasing.asmdef` — file mới

```json
{
    "name": "com.horcrux.adjust.purchasing",
    "rootNamespace": "",
    "references": [
        "AdjustSdk.Scripts",
        "Unity.Purchasing",
        "Unity.Purchasing.Utilities"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [
        "HORCRUX_ADJUST",
        "HORCRUX_PURCHASING_5"
    ],
    "versionDefines": [
        {
            "name": "com.adjust.sdk",
            "expression": "5.0.0",
            "define": "HORCRUX_ADJUST"
        },
        {
            "name": "com.unity.purchasing",
            "expression": "5.0.0",
            "define": "HORCRUX_PURCHASING_5"
        }
    ],
    "noEngineReferences": false
}
```

`versionDefines` chỉ đặt define trong chính assembly khai nó. Vì vậy assembly này tự khai lại `HORCRUX_ADJUST`, không mượn được
của `com.horcrux.adjust`. Assembly test ở dưới cũng phải tự khai lại cả hai.

### `Vendors/Adjust/Purchasing/AdjustIapRevenue.cs` — file mới, phần GĐ1

```csharp
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine.Purchasing;

[assembly: InternalsVisibleTo("Horcrux.Vendors.Adjust.Purchasing.Tests")]

namespace Horcrux.Vendors.Adjust
{
    /// <summary>
    /// Sends one Unity IAP order to Adjust as a revenue event, net of the store fee. Call once per new order, before
    /// confirming it; the environment (production, sandbox) is the one the Adjust SDK was initialised with.
    /// </summary>
    public static class AdjustIapRevenue
    {
        #region Class Methods

        /// <summary>Google Play purchase token nested in a Unity IAP receipt; empty when any level is missing.</summary>
        internal static string ReadGooglePurchaseToken(string receipt)
        {
            string payload = ReadString(receipt, "Payload");
            string json = ReadString(payload, "json");
            return ReadString(json, "purchaseToken");
        }

        private static string ReadString(string json, string key) =>
            !string.IsNullOrEmpty(json)
            && MiniJson.JsonDecode(json) is Dictionary<string, object> fields
            && fields.TryGetValue(key, out object value)
            && value is string text
                ? text
                : string.Empty;

        #endregion
    }
}
```

- Receipt Google lồng ba tầng JSON, mỗi tầng là **chuỗi** chứa JSON của tầng sau, nên cùng một `ReadString` gọi ba lần.
- `MiniJson.JsonDecode` trả `null` khi chuỗi không phải JSON. Pattern `is Dictionary<…>` gộp cả ca `null` lẫn ca JSON là mảng.
- `internal` + `InternalsVisibleTo`: test là người gọi thứ hai; dự án không cần hàm này.

### Assembly test — agent viết

Thư mục `Tests/EditMode/Vendors/AdjustPurchasing/`, asmdef `Horcrux.Vendors.Adjust.Purchasing.Tests`:
- references: `com.horcrux.adjust.purchasing`, `Unity.Purchasing.Utilities`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`;
- `nunit.framework.dll`, `includePlatforms: Editor`;
- `defineConstraints`: `UNITY_INCLUDE_TESTS`, `HORCRUX_ADJUST`, `HORCRUX_PURCHASING_5`, kèm hai `versionDefines` như trên.

| Case | Receipt | Kỳ vọng |
|---|---|---|
| Google đủ ba tầng | `{"Store":"GooglePlay","Payload":"{\"json\":\"{\\\"purchaseToken\\\":\\\"tok-abc\\\"}\"}"}` (dựng bằng `MiniJson.JsonEncode`) | `"tok-abc"` |
| Apple | `Payload` là chuỗi base64, không phải JSON | `""` |
| `null` · `""` | — | `""` |
| JSON hỏng | `{not json` | `""`, không ném |
| JSON là mảng | `[1,2]` | `""`, không ném |
| `Payload` không phải chuỗi | `{"Payload":5}` | `""` |
| thiếu `purchaseToken` | `json` chỉ có `orderId` | `""` |

Đối chứng: bỏ tầng `"json"` (đọc thẳng `purchaseToken` từ payload) thì case Google đỏ. Agent chạy đối chứng một lần, ghi kết quả
cạnh case.

**Kiểm:** compile sạch, bảng case xanh. Mốc commit: `feat(adjust): assembly purchasing, đọc purchase token Google từ receipt`.

---

## Giai đoạn 2 — `Track`: dựng và gửi event doanh thu

**Mục tiêu:** một lời gọi `AdjustIapRevenue.Track(order, token)` gửi đúng event bản dự án đang gửi.

**Vì sao bây giờ:** đã có hàm đọc token; giai đoạn này nối nó với SDK, và caller thật xuất hiện ngay ở GĐ3.

### `Vendors/Adjust/Purchasing/AdjustIapRevenue.cs` — thêm

Thêm `using` ở đầu file, cạnh các dòng đã có:

```csharp
using System.Globalization;
using AdjustSdk;
using UnityEngine;
```

Trong class, thêm region `Properties` **trước** region `Class Methods` (đoạn `ReadGooglePurchaseToken` đã gõ giữ nguyên), rồi
region `API` ngay sau nó:

```csharp
        #region Properties

        private const string LogPrefix = "[AdjustIapRevenue]: ";

        // Share kept after the store fee. Float on purpose: revenue already sent was computed as double * float.
#if UNITY_IOS
        private const float RevenueShare = 0.65f;
#else
        private const float RevenueShare = 0.7f;
#endif

        #endregion

        #region API

        public static void Track(PendingOrder order, string eventToken)
        {
            if (string.IsNullOrEmpty(eventToken))
            {
                Debug.LogError(LogPrefix + "event token is empty; revenue not sent");
                return;
            }

            // A game purchase carries one cart item.
            IReadOnlyList<CartItem> items = order.CartOrdered.Items();
            Product product = items.Count > 0 ? items[0].Product : null;

            if (product == null)
            {
                Debug.LogError(LogPrefix + $"order {order.Info.TransactionID} has no product; revenue not sent");
                return;
            }

            var adjustEvent = new AdjustEvent(eventToken);
            adjustEvent.SetRevenue(decimal.ToDouble(product.metadata.localizedPrice) * RevenueShare,
                product.metadata.isoCurrencyCode);
            adjustEvent.ProductId = product.definition.id;
#if UNITY_IOS
            adjustEvent.TransactionId = order.Info.TransactionID; // Adjust dedupes iOS purchases on it.
#elif UNITY_ANDROID || UNITY_STANDALONE_WIN
            adjustEvent.PurchaseToken = ReadGooglePurchaseToken(order.Info.Receipt);
#endif
            adjustEvent.AddCallbackParameter("tax_included", "TRUE");
            adjustEvent.AddCallbackParameter("tax_amount", RevenueShare.ToString(CultureInfo.InvariantCulture));

            // Inside Horcrux.Vendors.Adjust the bare name Adjust is this namespace.
            AdjustSdk.Adjust.TrackEvent(adjustEvent);
        }

        #endregion
```

Chỗ dễ gõ sai:
- `AdjustSdk.Adjust.TrackEvent` phải gõ đủ namespace. Viết `Adjust.TrackEvent` thì báo CS0234, vì `Adjust` lúc đó là namespace
  `Horcrux.Vendors.Adjust`.
- `decimal.ToDouble(…) * RevenueShare` nhân `double` với `float`, đúng phép tính bản dự án đang gửi. Ép tỉ lệ về `double` trước khi
  nhân cũng ra cùng số; viết literal `0.7` (double) thì ra số khác.
- Guard token rỗng là **`LogError` rồi thoát**: thiếu token là lỗi setup, không nổ, lặp mỗi lần mua cho tới khi điền.
- Guard thiếu `Product` là dữ liệu ngoài từ store: guard đầy đủ kèm log, vì không lượt kiểm nào lúc authoring bắt được.
- Không `try/catch` ở đây: chỗ gọi đã bọc riêng để đơn vẫn tới bước confirm khi SDK native ném.

**Editor setup:** không có.

**Kiểm:** compile sạch với define Android, rồi đổi build target hay chạy csc với `UNITY_IOS` để thấy nhánh iOS compile. Bảng case
GĐ1 vẫn xanh. Mốc commit: `feat(adjust): AdjustIapRevenue.Track gửi doanh thu IAP`.

---

## Giai đoạn 3 — Dự án dùng bản Horcrux (agent)

**Mục tiêu:** `IAPControl` gọi bản Horcrux; bản tạm `AdjustIapTracking.cs` bị xoá cùng lần.

- `Core.Sdk.asmdef`: thêm reference tới `com.horcrux.adjust.purchasing` (GUID lấy từ `.meta` của asmdef mới).
- `IAPControl.cs`: `AdjustIapTracking.Track(order, …)` → `AdjustIapRevenue.Track(order, _adjustSetting.GetAdjustIapToken())`.
  Giữ nguyên cổng `IsCheater` và khối `try/catch` quanh. Comment đầu class đổi tên bản tạm sang tên mới.
- Xoá `AdjustIapTracking.cs` và `.meta`. Đây là class tĩnh, không asset nào giữ GUID của nó.
- Tài liệu: `docs/Runtime/AdjustIapRevenue/AdjustIapRevenue.md` và `.html`. Cần mục "Trước khi chạy" (token event IAP lấy từ
  dashboard Adjust, chỗ gọi trước confirm), và mục lưu ý về cổng anti-cheat của SDK lẫn của dự án.
- Xoá Plan này.

**Kiểm:**
- grep dự án không còn `AdjustIapTracking`;
- `verify.sh` xanh với `Core.Sdk.csproj` và assembly test.

**QA chơi qua kịch bản** trên APK:
1. Cài ngoài Store thì bấm "Bypass Anti-Cheat" trước.
2. Chuyển Adjust sang Sandbox: SDK Debugger › Adjust Debugger › Switch Sandbox Mode, game tự đóng.
3. Mở lại game, mua một gói.
4. Adjust Testing Console thấy event IAP với revenue = giá × 0.7, `tax_amount = 0.7`, có purchase token.

Mốc commit dự án: `refactor(iap): doanh thu IAP lên Adjust qua AdjustIapRevenue của Horcrux`, kèm con trỏ submodule.

---

## File → giai đoạn

| File | GĐ |
|---|---|
| `Vendors/Adjust/Purchasing/com.horcrux.adjust.purchasing.asmdef` | 1 |
| `Vendors/Adjust/Purchasing/AdjustIapRevenue.cs` | 1, 2 |
| `Tests/EditMode/Vendors/AdjustPurchasing/` (asmdef, test) | 1 |
| dự án `Assets/_Modules/Sdk/Core.Sdk.asmdef`, `IAP/IAPControl.cs`, xoá `AdjustIapTracking.cs` | 3 |
| `docs/Runtime/AdjustIapRevenue/AdjustIapRevenue.{md,html}` | 3 |
