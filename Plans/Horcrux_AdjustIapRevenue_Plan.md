# Plan — `AdjustIapRevenue`: doanh thu IAP lên Adjust, vào Horcrux `Vendors/Adjust`

Bản tạm: category-jam `Assets/_Modules/Sdk/AdjustIapTracking.cs` › `Track`, gọi ở `Assets/_Modules/Sdk/IAP/IAPControl.cs` trước bước confirm.
**Phụ thuộc:** không — gõ lúc nào cũng được.
Kiểm: `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.adjust.purchasing.csproj --tests Horcrux.Vendors.Adjust.Purchasing.Tests`, compile cả define `UNITY_ANDROID` lẫn `UNITY_IOS` (đổi build target sang iOS, hoặc csc với `UNITY_IOS`).

## Kịch bản

| Tình huống | Hệ làm gì | Caller thấy |
|---|---|---|
| lớp IAP nhận `PendingOrder` mới, qua cổng anti-cheat, gọi `Track(order, token)` trước confirm | dựng `AdjustEvent`: revenue = giá × tỉ lệ sau phí store (0.7f Android, 0.65f iOS), currency, `ProductId`, khoá chống trùng (iOS `TransactionId`, Android `PurchaseToken`), `tax_included = "TRUE"`, `tax_amount = "0.7"` hay `"0.65"` → `AdjustSdk.Adjust.TrackEvent` | event ở môi trường SDK đã khởi tạo (production, sandbox); cheat đổi sandbox của SDK áp luôn |
| token event rỗng | `LogError`, không gửi | lỗi setup lặp mỗi lần mua tới khi điền |
| đơn không có sản phẩm | `LogError`, không gửi | không event |
| receipt có tầng sai kiểu JSON (mảng, số) | `LogError`, không gửi | không event |
| tầng receipt thiếu, rỗng, không phải JSON | token `""` | event vẫn đi |
| khoá `purchaseToken` là JSON `null` | token `null` | event vẫn đi |
| iOS, payload base64 | chỉ kiểm tầng `Payload` | event đi, khoá chống trùng là `TransactionId` |
| SDK native ném trong `TrackEvent` | không bắt | chỗ gọi bọc `try/catch`, đơn vẫn tới bước confirm |

## Flow

```
lớp IAP dự án (PendingOrder mới) ── cổng anti-cheat ──► AdjustIapRevenue.Track(order, token)        AdjustIapRevenue.cs
   token: setting của dự án             │ token rỗng · không Product ──► LogError, dừng
                                        │ Android: TryReadGooglePurchaseToken ─► TryReadString ×3 ─► MiniJson
                                        │ nơi khác: TryReadString(receipt, "Payload")
                                        │ tầng sai kiểu JSON ──► LogError, dừng (không event)
                                        └ AdjustEvent ──► AdjustSdk.Adjust.TrackEvent ──► môi trường SDK đã khởi tạo
lớp IAP dự án confirm đơn
Horcrux.Vendors.Adjust.Purchasing.Tests ──► TryReadGooglePurchaseToken, TryReadString (internal, InternalsVisibleTo)
```

## Thuật toán

Receipt Google: `receipt ─"Payload"─► payload ─"json"─► json ─"purchaseToken"─► token`, mỗi tầng qua `TryReadString`:

| Tầng | Kết quả |
|---|---|
| rỗng, `null`, không phải JSON, thiếu khoá | `true`, `""` |
| JSON không phải object · khoá không phải chuỗi và không `null` | `false` → không event |
| khoá JSON `null` | `true`, `null` |
| khoá là chuỗi | `true`, chuỗi đó |

Dữ liệu ghi ra giữ từng byte như bản tạm: revenue = `double` giá × `float` tỉ lệ; `tax_amount` = `RevenueShare.ToString(InvariantCulture)`.
Bản mẫu: 36 receipt vét cạn ba tầng, so phần đọc của bản tạm: 72 lượt, 0 lệch ("ném" ứng với `false`) · đối chứng đọc JSON `null` thành `""` cho 4 lệch.

---

## GĐ1 — xong thì lớp IAP của dự án gửi doanh thu lên Adjust bằng một lời gọi

```
com.horcrux.adjust.purchasing+: AdjustIapRevenue.Track+ ─► TryReadGooglePurchaseToken+ ─► TryReadString+ ─► MiniJson
Horcrux.Vendors.Adjust.Purchasing.Tests+ ──────────────────► (internal)
```

`Vendors/Adjust/Purchasing/com.horcrux.adjust.purchasing.asmdef` — file mới:

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

`Vendors/Adjust/Purchasing/AdjustIapRevenue.cs` — file mới, `Track` (caller) trước, hai hàm đọc receipt sau:

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
            // LogError, not silent: a missing token is a setup error, repeated every purchase until filled in.
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

            // purchaseToken declared in the Android branch only: declared above it, the iOS branch warns it is unused.
#if UNITY_ANDROID
            bool readable = TryReadGooglePurchaseToken(order.Info.Receipt, out string purchaseToken);
#else
            // Payload level only: Apple's payload is base64, which MiniJson may decode as a number.
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
            // Built from the share constant itself ("0.7", "0.65"): no hand-written copy to keep in sync.
            adjustEvent.AddCallbackParameter("tax_amount", RevenueShare.ToString(CultureInfo.InvariantCulture));

            // Inside Horcrux.Vendors.Adjust the bare name Adjust is this namespace.
            AdjustSdk.Adjust.TrackEvent(adjustEvent);
        }

        #endregion

        #region Class Methods

        // bool, not a throw: same outcome (no event) without relying on the caller's try/catch.
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

            // A JSON null stays null, not "": reading it as "" changes the token sent.
            if (raw != null && raw is not string)
                return false;

            value = (string)raw;
            return true;
        }

        #endregion
    }
}
```

`Tests/EditMode/Vendors/AdjustPurchasing/` (agent): asmdef `Horcrux.Vendors.Adjust.Purchasing.Tests` — references `com.horcrux.adjust.purchasing`, `Unity.Purchasing.Utilities`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`; `nunit.framework.dll`, `includePlatforms: Editor`; `defineConstraints` `UNITY_INCLUDE_TESTS`, `HORCRUX_ADJUST`, `HORCRUX_PURCHASING_5` kèm hai `versionDefines` như trên. Receipt dựng bằng `MiniJson.JsonEncode`.

| Case | Hàm, input | Kỳ vọng |
|---|---|---|
| Google đủ ba tầng | `TryReadGooglePurchaseToken`, `{"Store":"GooglePlay","Payload":"{\"json\":\"{\\\"purchaseToken\\\":\\\"tok-abc\\\"}\"}"}` | `true`, `"tok-abc"` — đỏ nếu bỏ tầng `"json"` |
| Apple | `TryReadString(…, "Payload")`, `Payload` là chuỗi base64 | `true`, chuỗi base64 |
| `null` · `""` | `TryReadGooglePurchaseToken` | `true`, `""` |
| không phải JSON | `{not json` | `true`, `""` |
| receipt là mảng | `[1,2]` | `false` |
| `Payload` là số | `{"Payload":5}` | `false` |
| `Payload` JSON `null` | `{"Payload":null}` | `true`, `""` |
| payload là mảng | `Payload` = `"[1]"` | `false` |
| thiếu `purchaseToken` | `json` chỉ có `orderId` | `true`, `""` |
| `purchaseToken` JSON `null` | `json` = `{"purchaseToken":null}` | `true`, `null` — đỏ nếu `raw != null && raw is not string` thành `raw is not string` |
| `purchaseToken` là số | `json` = `{"purchaseToken":5}` | `false` |

---

## Chuyển dự án (agent)

| Chỗ | Đổi |
|---|---|
| `docs/Runtime/AdjustIapRevenue/AdjustIapRevenue.md`, `.html` | tạo, `Code: Vendors/Adjust/Purchasing/`; Trước khi chạy: token event IAP lấy từ dashboard Adjust, gọi trước confirm, bọc `try/catch` ở chỗ gọi; lưu ý: cổng anti-cheat ở chỗ gọi (và cổng riêng của SDK nếu có), receipt sai hình thì không event, tỉ lệ là `float` |
| `Assets/_Modules/Sdk/Core.Sdk.asmdef` | thêm reference `com.horcrux.adjust.purchasing` (GUID từ `.meta` của asmdef mới) |
| `Assets/_Modules/Sdk/IAP/IAPControl.cs` › khối `try` trước confirm | gọi `AdjustIapRevenue.Track(order, _adjustSetting.GetAdjustIapToken())`, thêm `using Horcrux.Vendors.Adjust;`; giữ cổng `IsCheater` và `try/catch` quanh; comment đầu class đổi tên `AdjustIapTracking` |
| `Assets/_Modules/Sdk/AdjustIapTracking.cs` + `.meta` | xoá (class tĩnh, không asset nào giữ GUID) |
| Plan này | xoá cùng `.meta` |
| chơi thử | QA, APK Android: cài ngoài Store thì bấm "Bypass Anti-Cheat" trước · SDK Debugger › Adjust Debugger › Switch Sandbox Mode (game tự đóng) · mở lại, mua một gói → Adjust Testing Console có event revenue = giá × 0.7, `tax_amount = 0.7`, có purchase token, giống event bản cũ; hỏng: không event (token rỗng, receipt sai hình — xem Console), revenue bằng đúng giá |

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | gộp vào `com.horcrux.adjust` | dự án có Adjust mà không có IAP phải compile như cũ |
| bỏ | mượn define `HORCRUX_ADJUST` của `com.horcrux.adjust` | `versionDefines` chỉ đặt define trong chính assembly khai nó; asmdef này và asmdef test tự khai lại |
| bỏ | ép kiểu ném như bản tạm, chỗ gọi nuốt | `bool` + `out` cho cùng kết cục bỏ event mà không dựa vào `try/catch` của chỗ gọi |
| bỏ | một hàm đọc ba tầng ở mọi nền tảng | payload Apple là base64, MiniJson đọc chuỗi mở đầu bằng chữ số ra số: iOS bỏ event mà bản tạm vẫn gửi |
| bỏ | tỉ lệ `double` (`0.7`) | 0.99 USD gửi thành `0.693` thay vì `0.69299998819828035`: đổi số liệu dashboard đang giữ |
| bỏ | cổng anti-cheat trong hàm | mỗi dự án một hệ anti-cheat, chỗ gọi giữ cổng |
| bỏ | đọc token từ asset cấu hình | mỗi dự án để token một nơi, dự án truyền vào |
| bỏ | contract `IAttributionService` | `SystemPlan` §13 chưa dựng, hàm tĩnh này chưa cần |
| bỏ | giỏ nhiều món | game mua một món mỗi đơn |
| bỏ | đường Unity IAP 4 (`PurchaseEventArgs`) | chưa dự án IAP 4 nào dùng |
| bỏ | `try/catch` trong `Track` | chỗ gọi đã bọc riêng để đơn vẫn tới bước confirm khi SDK native ném |
| bỏ | gọi `ZegoAdjust.TrackEventIap` | chỉ chạy khi có class cài `Zego.IIapService` |
| sau | tỉ lệ `double` | chỉ đổi kiểu một hằng, kèm ghi chú cho đội đọc số liệu |
| sau | nhánh `UNITY_STANDALONE_WIN` đọc purchase token như Android | thêm vào `#if` khi có dự án build Windows dùng Google Play |
