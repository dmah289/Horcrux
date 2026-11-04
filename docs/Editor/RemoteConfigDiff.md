# RemoteConfigDiff — asset remote config lệch Firebase console ở đâu

Code: `Editor/RemoteConfigDiff/`.

Một cửa sổ so từng biến `[MarkedRemoteConfig]` của một `BaseRemoteConfigCollection` với template JSON xuất từ Firebase console. Mỗi
dòng là một key: Khớp, Lệch kèm hai giá trị, chỉ có ở asset, chỉ có ở console, console không parse được, console dùng default trong
app. Chạy sau khi dán giá trị lên console, trước khi publish hay trước khi build: asset là lưới đỡ khi fetch hỏng, nên phải bằng console.

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | developer sau khi CopyJson từ asset và dán lên console; agent khi nghiệm thu remote config |
| Đầu vào | asset collection (ô **Collection**) và template console (ô **Template JSON**) |
| Ranh giới | chỉ báo, không sửa asset, không sửa console; không gọi API Firebase |
| Chỗ đặt | `Editor/RemoteConfigDiff/`, menu `Horcrux > Remote Config > Diff`, assembly `com.horcrux.editor` |

**Quyết định trái trực giác**

| Quyết định | Vì |
|---|---|
| Danh sách biến lấy bằng cách gọi hàm private `GetRemoteConfigFields` của collection qua reflection | đúng tập biến runtime fetch: tool tự quét field thì lệch runtime ở field private của lớp cha (runtime không thấy) |
| Giá trị console parse bằng hàm private `TryParseValueFromString` của chính `RemoteConfig<T>` | cùng luật với máy người chơi: string giữ nguyên chữ, số theo `InvariantCulture`, enum theo tên, còn lại `JsonConvert`. Parse hỏng thì runtime tự `LogError` (Console đỏ một dòng mỗi key hỏng là đúng: máy người chơi cũng hỏng ở đó) |
| So sau khi parse hai bên rồi serialize lại bằng `JsonConvert`, so `JToken.DeepEquals` | thứ tự khoá, khoảng trắng, `1.50` với `1.5` không phải lệch; khác giá trị thật mới là lệch |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Sửa asset theo console hay ngược lại | người nhìn quyết bên nào đúng |
| So giá trị trong `conditionalValues` | điều kiện (nước, platform, phần trăm user) không tính được ở Editor; tool chỉ cảnh báo có điều kiện |
| Gọi API Firebase lấy template | cần credential; dán file xuất tay là đủ cho nhịp vài lần mỗi đợt cấu hình |

## Luồng dữ liệu

```
Collection ──► GetRemoteConfigFields() ──► mỗi field: RemoteConfig<T> ──► FirebaseKey, AllowFetching, Value ──► JsonConvert ─┐
                                                                                                                          ▼
Template JSON ──► "parameters" + "parameterGroups.*.parameters" ──► key → defaultValue.value ──► TryParseValueFromString ──► JsonConvert
                                                                                                                          ▼
                                     RemoteConfigDiffCore.TryCompare: một RemoteConfigDiffRow mỗi field asset, rồi key chỉ console (sắp chữ)
                                                                                                                          ▼
                                     cửa sổ: dòng tổng theo trạng thái; mỗi dòng trạng thái · key · field, hai giá trị khi khác Khớp, cảnh báo
                                     Copy báo cáo: bảng markdown (FormatReport)
```

## Trạng thái và cảnh báo

| Trạng thái | Khi | Nghĩa trên máy người chơi |
|---|---|---|
| `Match` | hai giá trị bằng nhau sau parse | — |
| `Mismatch` | khác giá trị | fetch được thì dùng giá trị console |
| `AssetOnly` | key không có trên console | runtime `LogError` "Can't find remote config value", dùng cache hay asset |
| `ConsoleOnly` | key trên console không biến nào khai | không ai đọc |
| `ConsoleUnparsable` | `TryParseValueFromString` trả false | runtime `LogError`, giữ cache hay asset |
| `ConsoleInAppDefault` | `defaultValue` là `useInAppDefault` | không có giá trị fetch, runtime báo key thiếu hay rỗng, dùng cache hay asset |
| `InvalidField` | field có `[MarkedRemoteConfig]` mà không chứa `RemoteConfig<T>` | runtime `LogError` lúc `Initialize`, bỏ field |

| Cảnh báo | Khi |
|---|---|
| `conditionalValues` | key có giá trị theo điều kiện: user trúng điều kiện nhận giá trị khác `defaultValue` |
| `allowFetching is off` | console có key mà biến tắt fetch: runtime bỏ giá trị console |
| `firebaseKey is empty` · `also used by field …` | khoá rỗng hay hai biến cùng khoá |

## Trước khi chạy

1. Firebase console → **Remote Config** → menu ⋮ → **Download default template** (hay template hiện tại) → mở file `.json`, chép toàn bộ.
   Thiếu: không có gì để so.
2. **Horcrux → Remote Config → Diff**: kéo asset collection vào **Collection**, dán vào **Template JSON**, bấm **So sánh**.
3. Cần gửi kết quả: **Copy báo cáo** rồi dán vào tin nhắn hay PR.

## Nghiệm thu

`Tests/EditMode/RemoteConfigDiffTests.cs` dựng một collection thử bằng `JsonUtility.FromJsonOverwrite`:

| Ca | Kỳ vọng |
|---|---|
| int bằng nhau · string `"123"` · float `1.50` với `1.5` · JSON khác thứ tự khoá và khoảng trắng | `Match` |
| int khác · JSON khác giá trị | `Mismatch`, hai giá trị |
| int nhận `"abc"` | `ConsoleUnparsable`, giữ chữ thô, runtime log một dòng |
| key thiếu ở console · key thiếu ở asset | `AssetOnly` · `ConsoleOnly`, xếp sau dòng asset, theo chữ |
| `useInAppDefault` | `ConsoleInAppDefault` |
| `conditionalValues` · fetch tắt mà console có key · hai biến cùng khoá | cảnh báo tương ứng; fetch tắt mà console không có key thì không cảnh báo |
| key trong `parameterGroups` | đọc như `parameters` |
| field đánh dấu không phải `RemoteConfig<T>` | `InvalidField` |
| JSON hỏng · không có `parameters` lẫn `parameterGroups` | `TryCompare` false kèm lỗi |
| báo cáo | một dòng mỗi row, `|` trong giá trị được escape |
