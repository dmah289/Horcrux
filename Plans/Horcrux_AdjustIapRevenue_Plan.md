# Plan — `AdjustIapRevenue`: doanh thu IAP lên Adjust, vào Horcrux `Vendors/Adjust`

Ngày 2026-11-23. Nguồn: category-jam `Assets/_Modules/Sdk/AdjustIapTracking.cs` (`AdjustIapTracking.Track`), đang gọi ở
`IAPControl` trước bước confirm; cùng thân với `AdjustService.TrackEventIap` của ScrewDom. Lõi dựng `AdjustEvent` từ `PendingOrder`
của Unity IAP 5: doanh thu sau phí store, `ProductId`, khoá chống trùng theo store (iOS `TransactionId`, Google `PurchaseToken`).

**Phân công:** GĐ1 developer gõ, agent viết test theo bảng case rồi compile · GĐ2 (tài liệu) và GĐ3 (dự án chuyển sang bản Horcrux,
xoá bản tạm, xoá Plan) agent làm.
**Chạy được, thấy được:** GĐ1 compile sạch với cả `UNITY_ANDROID` lẫn `UNITY_IOS` + bảng case xanh trong
`Tests/EditMode/Vendors/AdjustPurchasing/`; thấy event trên Adjust sandbox ở GĐ3.
**Lệnh kiểm** (chạy từ gốc dự án): `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj
com.horcrux.adjust.purchasing.csproj --tests Horcrux.Vendors.Adjust.Purchasing.Tests`.
**Commit:** GĐ1, GĐ2 trong `Assets/Horcrux`; GĐ3 ở dự án kèm con trỏ submodule.
**Phụ thuộc:** không.
**Đã kiểm trước khi ghi:** file GĐ1 compile bằng csc (C# 9, `-warn:4`, 0 lỗi 0 cảnh báo) trên DLL của dự án (Adjust SDK 5.5.1,
Unity IAP 5.4.0, `UnityEngine.CoreModule` 6000.0.73f1) với từng define `UNITY_ANDROID`, `UNITY_IOS`, `UNITY_STANDALONE_WIN`. Phần đọc
receipt chạy so với phần đọc của bản dự án trên 36 receipt sinh vét cạn ba tầng (mỗi tầng: thiếu, `null`, rỗng, không phải JSON,
JSON hỏng, mảng, số, khoá `null`, khoá là số, khoá là mảng, tầng trong hợp lệ): 72 lượt, 0 lệch; "ném" của bản dự án ứng với `false`.
Đối chứng: đọc khoá JSON `null` thành `""` thay vì `null` cho 4 lệch. `§0`: không, hệ không có toán.

---

## Ngữ cảnh đã chốt

- **Người dùng:** lớp IAP của dự án (chủ vòng mua Unity IAP 5) gọi một lần cho mỗi `PendingOrder` mới, trước khi confirm. Môi trường
  Adjust (production, sandbox) là cái SDK đã khởi tạo, nên cheat đổi sandbox của SDK áp luôn cho event này.
- **Mục tiêu:** một lời gọi `AdjustIapRevenue.Track(order, token)` gửi đúng event bản dự án đang gửi.
- **Ranh giới:** Horcrux dựng và gửi event. Dự án giữ token event (lấy ở đâu), cổng anti-cheat, chỗ gọi trong vòng mua và `try/catch`
  quanh nó.
- **Chỗ đặt, tên:** thư mục `Vendors/Adjust/Purchasing/`, assembly riêng `com.horcrux.adjust.purchasing`, namespace
  `Horcrux.Vendors.Adjust` (cùng `AdjustCampaignBootStep`). Assembly riêng vì dự án có Adjust mà không có IAP vẫn compile
  `com.horcrux.adjust` như cũ. Tài liệu `docs/Runtime/AdjustIapRevenue/`.
- **Dữ liệu ghi ra giữ nguyên từng byte:** doanh thu = `double` giá × `float` tỉ lệ (0.7f Android, 0.65f iOS), hai callback parameter
  `tax_included = "TRUE"`, `tax_amount = "0.7"` hay `"0.65"`, purchase token đọc như bản dự án — kể cả khoá JSON `null` gửi `null`.
  Dashboard và báo cáo đang đọc đúng những số này.
- **Cố ý không làm:**
  - Cổng anti-cheat trong hàm: mỗi dự án một hệ anti-cheat, chỗ gọi giữ cổng.
  - Đọc token từ asset cấu hình: dự án truyền vào, mỗi dự án để token một nơi.
  - Contract `IAttributionService`: `SystemPlan` §13 chưa dựng, hàm tĩnh này chưa cần nó.
  - Giỏ nhiều món: game mua một món mỗi đơn.
  - Đường Unity IAP 4 (`PurchaseEventArgs`): chưa dự án IAP 4 nào dùng.
  - `try/catch` trong `Track`: chỗ gọi đã bọc riêng để đơn vẫn tới bước confirm khi SDK native ném.
- **Quyết định trái trực giác:**
  - **Receipt sai hình thì không gửi event**, kèm `LogError`. Bản dự án ép kiểu `(Dictionary<…>)`, `(string)` ở mỗi tầng; tầng mang sai
    kiểu JSON thì ném, chỗ gọi nuốt, không event nào đi. Bản này giữ đúng kết cục đó bằng `false` thay cho ném. Tầng thiếu, rỗng
    hay không phải JSON thì bản dự án gửi token `""`; bản này cũng vậy. Mọi nền tảng đọc tầng `Payload` (bản dự án đọc nó ở mọi nền
    tảng); chỉ Android đọc tiếp hai tầng trong — payload của Apple là base64, MiniJson có thể đọc nó ra số.
  - Tỉ lệ để kiểu `float`, không phải `0.7` kiểu `double`. Đổi sang `double` làm doanh thu 0.99 USD gửi đi thành `0.693` thay vì
    `0.69299998819828035` (đo bằng eval) — đổi dữ liệu ghi ra.
  - Chuỗi `tax_amount` sinh từ chính hằng tỉ lệ (`ToString(InvariantCulture)` ra `"0.7"`, `"0.65"`, đã đo), không có hai bản viết
    tay phải khớp nhau.
  - Đơn không có sản phẩm thì `LogError` rồi thoát (bản dự án thoát im lặng). Chỉ log khác, dữ liệu gửi đi không đổi.
- **Mở rộng sau:**
  - Tỉ lệ `double`: chỉ đổi kiểu một hằng, kèm ghi chú cho đội đọc số liệu.
  - Nhánh `UNITY_STANDALONE_WIN` đọc purchase token như Android (bản dự án có): thêm vào `#if` khi có dự án build Windows dùng Google
    Play.

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| category-jam `Assets/_Modules/Sdk/AdjustIapTracking.cs` (bản tạm) | toàn bộ hành vi: tỉ lệ, ba lớp JSON của receipt Google, callback parameter, guard token rỗng, kết cục khi ép kiểu ném | LINQ `FirstOrDefault`; ép kiểu ném làm luồng điều khiển — thay bằng `bool` cùng kết cục |
| category-jam `Assets/_Modules/Sdk/IAP/IAPControl.cs` | chỗ gọi: trong `try/catch` riêng trước confirm, cổng `IsCheater` | ở lại dự án |
| Unity IAP 5.4.0 | `Order.CartOrdered.Items()` (`IReadOnlyList<CartItem>`), `Order.Info.{TransactionID, Receipt}`, `UnityEngine.Purchasing.MiniJson` (assembly `Unity.Purchasing.Utilities`; trả `null` khi chuỗi rỗng hay không phải JSON) | — |
| Horcrux `Vendors/Adjust` | khuôn asmdef `versionDefines` + `defineConstraints`; gọi `AdjustSdk.Adjust` đầy đủ vì trong namespace `Horcrux.Vendors.Adjust` chữ `Adjust` là namespace | — |
| Zego `ZegoAdjust.TrackEventIap` | xác nhận cùng cách gửi | chỉ chạy khi có class cài `Zego.IIapService` |

## Luồng

```
Unity IAP báo PendingOrder mới ──► lớp IAP dự án trao thưởng
        ▼
cổng anti-cheat của dự án ──► AdjustIapRevenue.Track(order, token)
        ▼
token rỗng? ──có──► LogError, dừng
        ▼
món đầu của giỏ ──► Product? ──không──► LogError, dừng
        ▼
đọc receipt: Android TryReadGooglePurchaseToken · nơi khác TryReadString(receipt, "Payload")
        │      receipt ─"Payload"─► payload ─"json"─► json ─"purchaseToken"─► token
        └─ tầng sai kiểu JSON ──► LogError, dừng (không event)
        ▼
AdjustEvent(token): revenue = giá × tỉ lệ, currency, ProductId, iOS TransactionId · Android PurchaseToken
        ▼
tax_included, tax_amount ──► AdjustSdk.Adjust.TrackEvent ──► môi trường SDK đã khởi tạo
        ▼
lớp IAP dự án confirm đơn
```

## Bản đồ hệ cuối

```
  dự án: lớp IAP ── IsCheater? ──► AdjustIapRevenue.Track ──► AdjustSdk.Adjust.TrackEvent
         token: setting của dự án          │
                                           └─► TryReadGooglePurchaseToken ─► TryReadString ─► MiniJson
  test: Horcrux.Vendors.Adjust.Purchasing.Tests ──► TryReadGooglePurchaseToken, TryReadString (internal, InternalsVisibleTo)
```

---

## GĐ1 — Assembly và `AdjustIapRevenue`

**Mục tiêu:** `AdjustIapRevenue.Track(order, token)` dựng và gửi event; phần đọc receipt có bảng case xanh. Xong thì dự án gọi được
bản Horcrux.

**Vì sao bây giờ:** cả hệ là một class tĩnh một màn hình; `Track` là caller của hàm đọc receipt, test là caller thứ hai.

**Sơ đồ sau GĐ** (mới: cả hai khối):

```
[mới] com.horcrux.adjust.purchasing: AdjustIapRevenue.Track ─► TryReadGooglePurchaseToken ─► TryReadString ─► MiniJson
[mới] Horcrux.Vendors.Adjust.Purchasing.Tests ──────────────────► (internal)
```

**Consumes:** `PendingOrder` (Unity IAP 5), `AdjustEvent`, `AdjustSdk.Adjust` (Adjust SDK 5), `MiniJson`.
**Produces:** `AdjustIapRevenue.Track(PendingOrder, string)`.

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

`versionDefines` chỉ đặt define trong chính assembly khai nó, nên assembly này tự khai lại `HORCRUX_ADJUST`, không mượn được của
`com.horcrux.adjust`. Assembly test ở dưới cũng tự khai lại cả hai.

### `Vendors/Adjust/Purchasing/AdjustIapRevenue.cs` — file mới

Gõ theo thứ tự đọc: `Track` (caller) trước, hai hàm đọc receipt sau.

```csharp
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using AdjustSdk;
using UnityEngine;
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
        #region Properties

        private const string LogPrefix = "[AdjustIapRevenue]: ";

        // Share kept after the store fee. Float on purpose: revenue is double * float, and changing the type changes the
        // revenue figures the dashboard already holds.
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

#if UNITY_ANDROID
            bool readable = TryReadGooglePurchaseToken(order.Info.Receipt, out string purchaseToken);
#else
            bool readable = TryReadString(order.Info.Receipt, "Payload", out _);
#endif

            if (!readable)
            {
                Debug.LogError(LogPrefix + $"order {order.Info.TransactionID} has a malformed receipt; revenue not sent");
                return;
            }

            var adjustEvent = new AdjustEvent(eventToken);
            adjustEvent.SetRevenue(decimal.ToDouble(product.metadata.localizedPrice) * RevenueShare,
                product.metadata.isoCurrencyCode);
            adjustEvent.ProductId = product.definition.id;
#if UNITY_IOS
            adjustEvent.TransactionId = order.Info.TransactionID; // Adjust dedupes iOS purchases on it.
#elif UNITY_ANDROID
            adjustEvent.PurchaseToken = purchaseToken; // Adjust dedupes Google Play purchases on it.
#endif
            adjustEvent.AddCallbackParameter("tax_included", "TRUE");
            adjustEvent.AddCallbackParameter("tax_amount", RevenueShare.ToString(CultureInfo.InvariantCulture));

            // Inside Horcrux.Vendors.Adjust the bare name Adjust is this namespace.
            AdjustSdk.Adjust.TrackEvent(adjustEvent);
        }

        #endregion

        #region Class Methods

        /// <summary>
        /// Google Play purchase token nested three JSON levels deep in a Unity IAP receipt. False when a level holds the wrong
        /// JSON type; a missing or non-JSON level reads as empty.
        /// </summary>
        internal static bool TryReadGooglePurchaseToken(string receipt, out string token)
        {
            token = string.Empty;

            return TryReadString(receipt, "Payload", out string payload)
                && TryReadString(payload, "json", out string json)
                && TryReadString(json, "purchaseToken", out token);
        }

        /// <summary>
        /// String field of a JSON object. False when the text is JSON but not an object, or the field is neither a string nor
        /// null; empty, non-JSON text and a missing field give "", a JSON null gives null.
        /// </summary>
        internal static bool TryReadString(string json, string key, out string value)
        {
            value = string.Empty;

            if (string.IsNullOrEmpty(json))
                return true;

            object decoded = MiniJson.JsonDecode(json);

            // MiniJson returns null for text that is not JSON.
            if (decoded == null)
                return true;

            if (decoded is not Dictionary<string, object> fields)
                return false;

            if (!fields.TryGetValue(key, out object raw))
                return true;

            if (raw != null && raw is not string)
                return false;

            value = (string)raw;
            return true;
        }

        #endregion
    }
}
```

Chỗ dễ gõ sai:
- `AdjustSdk.Adjust.TrackEvent` phải gõ đủ namespace. Viết `Adjust.TrackEvent` thì báo CS0234, vì `Adjust` lúc đó là namespace
  `Horcrux.Vendors.Adjust`.
- `decimal.ToDouble(…) * RevenueShare` nhân `double` với `float`, đúng phép tính bản dự án. Viết literal `0.7` (double) thì ra số khác.
- `TryReadString` giữ đúng ba kết cục của phép ép kiểu ở bản dự án: `false` khi `(Dictionary<…>)` hay `(string)` sẽ ném; `""` khi
  bản dự án rơi vào nhánh `: ""`; `null` khi khoá là JSON `null` (`(string)null`). Rút gọn `raw == null` thành `""` là đổi token gửi
  đi (đối chứng ở đầu Plan).
- `purchaseToken` chỉ khai ở nhánh `UNITY_ANDROID`; khai chung thì nhánh iOS báo biến không dùng.
- Guard token rỗng là `LogError` rồi thoát: thiếu token là lỗi setup, lặp mỗi lần mua cho tới khi điền.

**Quyết định:** `bool` + `out` thay cho ném — kết cục giống bản dự án mà không dựa vào `try/catch` của chỗ gọi để bỏ event. *Bỏ:*
một hàm đọc ba tầng dùng ở mọi nền tảng — payload Apple là base64, MiniJson đọc chuỗi mở đầu bằng chữ số ra số, iOS sẽ bỏ event
mà bản dự án vẫn gửi.

**Editor setup:** không.

### Assembly test — agent viết

Thư mục `Tests/EditMode/Vendors/AdjustPurchasing/`, asmdef `Horcrux.Vendors.Adjust.Purchasing.Tests`:
- references: `com.horcrux.adjust.purchasing`, `Unity.Purchasing.Utilities`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`;
- `nunit.framework.dll`, `includePlatforms: Editor`;
- `defineConstraints`: `UNITY_INCLUDE_TESTS`, `HORCRUX_ADJUST`, `HORCRUX_PURCHASING_5`, kèm hai `versionDefines` như trên.

Receipt dựng bằng `MiniJson.JsonEncode`.

| Case | Hàm, input | Kỳ vọng |
|---|---|---|
| Google đủ ba tầng | `TryReadGooglePurchaseToken`, `{"Store":"GooglePlay","Payload":"{\"json\":\"{\\\"purchaseToken\\\":\\\"tok-abc\\\"}\"}"}` | `true`, `"tok-abc"` |
| Apple | `TryReadString(…, "Payload")`, `Payload` là chuỗi base64 | `true`, chuỗi base64 |
| `null` · `""` | `TryReadGooglePurchaseToken` | `true`, `""` |
| không phải JSON | `{not json` | `true`, `""` |
| receipt là mảng | `[1,2]` | `false` |
| `Payload` là số | `{"Payload":5}` | `false` |
| `Payload` JSON `null` | `{"Payload":null}` | `true`, `""` |
| payload là mảng | `Payload` = `"[1]"` | `false` |
| thiếu `purchaseToken` | `json` chỉ có `orderId` | `true`, `""` |
| `purchaseToken` JSON `null` | `json` = `{"purchaseToken":null}` | `true`, `null` |
| `purchaseToken` là số | `json` = `{"purchaseToken":5}` | `false` |

Case đỏ được: bỏ tầng `"json"` thì case Google đỏ; đổi `raw != null && raw is not string` thành `raw is not string` thì case
`purchaseToken` `null` đỏ. Agent chạy hai đối chứng một lần, ghi kết quả cạnh case.

**Kiểm:** compile sạch với define Android, rồi đổi build target sang iOS (hoặc csc với `UNITY_IOS`) để thấy nhánh iOS compile. Bảng
case xanh. Mốc commit: `feat(adjust): AdjustIapRevenue gửi doanh thu IAP lên Adjust`.

---

## GĐ2 — Tài liệu (agent)

**Mục tiêu:** dự án sau dùng hệ mà không mở code.

**Vì sao bây giờ:** API đã chốt ở GĐ1; GĐ3 xoá Plan, nên tài liệu phải có trước.

**Sơ đồ sau GĐ:** không đổi code.

`docs/Runtime/AdjustIapRevenue/AdjustIapRevenue.md` và `.html`, dòng `Code: Vendors/Adjust/Purchasing/`. Mục "Trước khi chạy": token
event IAP lấy từ dashboard Adjust, chỗ gọi trước confirm, bọc `try/catch` ở chỗ gọi. Mục lưu ý: cổng anti-cheat ở chỗ gọi (và cổng
riêng của SDK nếu có), receipt sai hình thì không có event, tỉ lệ là `float`.

**Editor setup:** không.

**Kiểm:** mọi chữ ký trong `.md` khớp file GĐ1; `.html` dựng được từ `.md`. Mốc commit: `docs(adjust): tài liệu AdjustIapRevenue`.

---

## GĐ3 — Dự án dùng bản Horcrux (agent)

**Mục tiêu:** `IAPControl` gọi bản Horcrux; bản tạm `AdjustIapTracking.cs` và Plan này bị xoá cùng lần.

**Sơ đồ sau GĐ:**

```
IAPControl ── IsCheater? ──► [mới] AdjustIapRevenue.Track (Horcrux)      [xoá] AdjustIapTracking.Track (dự án)
```

- `Assets/_Modules/Sdk/Core.Sdk.asmdef`: thêm reference tới `com.horcrux.adjust.purchasing` (GUID lấy từ `.meta` của asmdef mới).
- `Assets/_Modules/Sdk/IAP/IAPControl.cs`, trong khối `try` trước confirm:

  ```csharp
                        if (!_antiCheatService.IsCheater)
                            AdjustIapTracking.Track(order, _adjustSetting.GetAdjustIapToken());
  ```

  → `AdjustIapRevenue.Track(order, _adjustSetting.GetAdjustIapToken())`, thêm `using Horcrux.Vendors.Adjust;`. Giữ cổng `IsCheater`
  và khối `try/catch` quanh. Comment đầu class nhắc `AdjustIapTracking` đổi sang tên mới.
- Xoá `Assets/_Modules/Sdk/AdjustIapTracking.cs` và `.meta` (class tĩnh, không asset nào giữ GUID).
- Xoá `Plans/Horcrux_AdjustIapRevenue_Plan.md` và `.meta`.

**Editor setup:** không.

**Kiểm:** grep dự án không còn `AdjustIapTracking`; `verify.sh` xanh với `Core.Sdk.csproj` và assembly test.

**Kịch bản chơi thử** (QA, APK Android):
1. Cài ngoài Store thì bấm "Bypass Anti-Cheat" trước — không thì cổng anti-cheat chặn, không event nào đi.
2. Chuyển Adjust sang Sandbox: SDK Debugger › Adjust Debugger › Switch Sandbox Mode, game tự đóng.
3. Mở lại game, mua một gói.
4. Nhìn Adjust Testing Console: event IAP với revenue = giá × 0.7, `tax_amount = 0.7`, có purchase token — giống hệt event bản cũ
   gửi. Hỏng: không có event (token rỗng hay receipt sai hình — xem Console), hoặc revenue bằng đúng giá (tỉ lệ không nhân).

Mốc commit dự án: `refactor(iap): doanh thu IAP lên Adjust qua AdjustIapRevenue của Horcrux`, kèm con trỏ submodule.

---

## File → GĐ

| File | GĐ |
|---|---|
| `Vendors/Adjust/Purchasing/com.horcrux.adjust.purchasing.asmdef` | 1 |
| `Vendors/Adjust/Purchasing/AdjustIapRevenue.cs` | 1 |
| `Tests/EditMode/Vendors/AdjustPurchasing/` (asmdef, test) | 1 (agent) |
| `docs/Runtime/AdjustIapRevenue/AdjustIapRevenue.{md,html}` | 2 |
| dự án `Assets/_Modules/Sdk/Core.Sdk.asmdef`, `IAP/IAPControl.cs`, xoá `AdjustIapTracking.cs` | 3 |
| xoá `Plans/Horcrux_AdjustIapRevenue_Plan.md` | 3 |
