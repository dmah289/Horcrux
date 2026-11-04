# UserSegmentation — lý do khoá trong save, cửa đọc và lệnh cho cheat — Plan sửa

> **Loại tài liệu:** Plan sửa — **developer sửa code lõi theo §2; agent sửa test theo §3, chạy, rồi đồng bộ
> `docs/Runtime/UserSegmentation/UserSegmentation.md` theo §4.** §2 là chuỗi chỗ đổi theo thứ tự: file, dòng hiện tại, bản
> cũ → bản mới. Dòng không nhắc tới giữ nguyên. Phần test chỉ có bảng case.

## Ngữ cảnh đã chốt

| Nhóm | Chốt |
|---|---|
| **Người dùng** | một panel cheat của dự án (ví dụ category SRDebugger) cần xem đủ state phân tập và đưa user về chưa quyết để thử lại; lõi không biết panel nào |
| **Vấn đề** | panel không đọc được `SessionCount` (state `private`), không biết **vì sao** camp bị khoá, không có cửa reset an toàn. Suy lý do từ state hiện có sai đúng ca QA hay làm: khoá vì hết cửa sổ, cheat báo camp → `OnCampaign` vẫn ghi `LastCampaign` → trông như Adjust đã trả lời |
| **Cách giải** | lưu `LockReason` vào state, ghi ở đúng chỗ đang khoá, lý do **đầu tiên** thắng; `IsCampaignLocked` thành property suy từ nó. Host phơi `SessionCount`, `LockReason`; partial `AUserSegmentationHost.Debug.cs` cài explicit `IUserSegmentationDebug.ResetState()` |
| **Ranh giới cứng đã đồng ý đổi** | **wire format save**: bỏ khoá `IsCampaignLocked`, thêm `LockReason` (int). Save cũ đã khoá đọc ra `None` → mở lại cửa sổ một lần. Chỉ chấp nhận được khi chưa có user thật trên store dùng hệ này |
| **Không đổi** | API `IsCampaignLocked` (host và state, giờ chỉ đọc) · mọi dòng bảng Luật về tập · `UserSegmentChanged` · bước Adjust · Simulator Editor |
| **Cố ý không làm** | panel, định dạng chuỗi, reset dữ liệu ngoài hệ: việc của dự án · publish event khi reset: reset không phải lần đổi tập của người chơi, tracking không được thấy · `CurrConfig` tính lại sau reset: caller phải thoát app |
| **Quyết định trái trực giác** | lý do đầu thắng: "khoá" là cửa sổ phân lại đã đóng, mua sau khi Adjust trả lời vẫn là `Attribution`, tập purchased đã nói việc mua · reset thay **cả** `_state` lẫn giá trị entry: Commit muộn (poll nền) ghi `_entry.Value = _state`, giữ `_state` cũ là bản cũ đè bản reset · reset là explicit interface: gọi nhầm là mất save im lặng |
| **Phân công** | developer §2. Agent §3, §4, compile, chạy test |

## §1 Thiết kế

```
state      LockReason: None(0) · Attribution(1) · Purchase(2) · SessionWindow(3)     wire format, chỉ thêm cuối
           IsCampaignLocked => LockReason != None                                    [JsonIgnore], không vào save

Decider    OnBoot      hết cửa sổ   ─► Lock(SessionWindow)
           OnCampaign  chưa khoá    ─► Lock(Attribution)
           OnPurchase  luôn         ─► Lock(Purchase)
           Lock(reason): chỉ ghi khi đang None

host       SessionCount · LockReason                                                  đọc, trước Init trả 0 / None
           IUserSegmentationDebug.ResetState()   _state = new; _entry.Value = _state; FlushNow; log
```

## §2 Chỗ đổi

### Bước 1 — `UserSegmentState.cs`: enum mới, field mới

File `Runtime/Abstractions/Composites/UserSegmentation/UserSegmentState.cs`, thay nguyên file:

```csharp
using System;
using Newtonsoft.Json;

namespace Horcrux.Runtime.Abstractions.UserSegmentation
{
    /// <summary>Why the campaign window closed. Ints are the save wire format: append only.</summary>
    public enum UserSegmentLockReason
    {
        None = 0,
        Attribution = 1,
        Purchase = 2,
        SessionWindow = 3
    }

    /// <summary>
    /// Save model. Field names are the wire format; 0 in <see cref="CurrSegmentId"/> means undecided.
    /// </summary>
    [Serializable]
    public sealed class UserSegmentState
    {
        public int CurrSegmentId;
        /// <summary>Segment the user was in right before the purchased segment; only written on purchase.</summary>
        public int PrevSegmentId;
        /// <summary>First reason the campaign window closed; None while it is open.</summary>
        public UserSegmentLockReason LockReason;
        public int SessionCount;
        public string LastCampaign;

        /// <summary>Derived from <see cref="LockReason"/>; not saved.</summary>
        [JsonIgnore] public bool IsCampaignLocked => LockReason != UserSegmentLockReason.None;
    }
}
```

Vì sao `[JsonIgnore]`: Persistence serialize bằng Newtonsoft, Newtonsoft ghi mọi property public đọc được. Thiếu attribute thì
save có lại khoá `IsCampaignLocked`, hai nguồn sự thật.

### Bước 2 — `UserSegmentDecider.cs`: ghi lý do qua một hàm

File `Runtime/Abstractions/Composites/UserSegmentation/UserSegmentDecider.cs`.

Dòng 45–46, cũ:

```csharp
            if (!state.IsCampaignLocked && state.SessionCount >= maxSessions)
                state.IsCampaignLocked = true;
```

mới:

```csharp
            if (!state.IsCampaignLocked && state.SessionCount >= maxSessions)
                Lock(state, UserSegmentLockReason.SessionWindow);
```

Dòng 62, cũ `            state.IsCampaignLocked = true;` → mới:

```csharp
            Lock(state, UserSegmentLockReason.Attribution);
```

Dòng 90, cũ `            state.IsCampaignLocked = true;` → mới:

```csharp
            Lock(state, UserSegmentLockReason.Purchase);
```

Cuối class, sau `OnPurchase`, thêm:

```csharp

        /// <summary>The first reason wins: a lock means the resegment window closed, later causes do not rewrite why.</summary>
        private static void Lock(UserSegmentState state, UserSegmentLockReason reason)
        {
            if (state.LockReason == UserSegmentLockReason.None)
                state.LockReason = reason;
        }
```

Sửa `<summary>` của field trong `UserSegmentState` không cần thêm: lý do đã ghi ở bước 1.

### Bước 3 — `AUserSegmentationHost.cs`: hai cửa đọc, class thành partial

File `Runtime/Abstractions/Composites/UserSegmentation/AUserSegmentationHost.cs`.

Dòng 18, cũ `    public abstract class AUserSegmentationHost : ABootStep, IInitializable<BasePersistenceDataCollection>` → mới:

```csharp
    public abstract partial class AUserSegmentationHost : ABootStep, IInitializable<BasePersistenceDataCollection>
```

Sau dòng 33 (`public string LastCampaign => _state?.LastCampaign;`), thêm:

```csharp
        /// <summary>Cold boots since install, this one included; 0 before <see cref="InitializeAsync"/>.</summary>
        public int SessionCount => _state?.SessionCount ?? 0;
        public UserSegmentLockReason LockReason => _state?.LockReason ?? UserSegmentLockReason.None;
```

Dòng 31 (`IsCampaignLocked`) và 63 (`wasLocked`) giữ nguyên: đọc property mới của state.

### Bước 4 — file mới `AUserSegmentationHost.Debug.cs`

File `Runtime/Abstractions/Composites/UserSegmentation/AUserSegmentationHost.Debug.cs`:

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.UserSegmentation
{
    /// <summary>Debug commands that touch private state. Implemented explicitly: a stray call wipes the save silently.</summary>
    public interface IUserSegmentationDebug
    {
        /// <summary>Back to undecided and flushed. CurrConfig keeps the old row: the caller must quit the app.</summary>
        void ResetState();
    }

    public abstract partial class AUserSegmentationHost : IUserSegmentationDebug
    {
        void IUserSegmentationDebug.ResetState()
        {
            if (!IsReady)
            {
                Debug.LogError(LogPrefix + "ResetState before InitializeAsync", this);
                return;
            }

            // Both, not only the entry: a late Commit writes _state back, and the old object would undo the reset.
            _state = new UserSegmentState();
            _entry.Value = _state;
            _entry.FlushNow();
            Debug.Log(LogPrefix + "state reset to undecided", this);
        }
    }
}
```

Không publish `UserSegmentChanged`: reset không phải lần đổi tập của người chơi.

### Bước 5 — asset collection của mỗi dự án đang dùng hệ

Mở asset collection, mục **User Segment** › **Default Value**: ô **Is Campaign Locked** biến mất, ô **Lock Reason** hiện
`None`. Lưu asset (Ctrl+S) để YAML bỏ khoá `IsCampaignLocked` và có `LockReason: 0`. Không lưu thì YAML giữ khoá cũ chết,
không lỗi.

Sau bước 4: compile sạch phần runtime; `UserSegmentDeciderTests` lỗi biên dịch vì gán `IsCampaignLocked` — agent sửa ở §3
**trước** khi commit, để commit là một mốc compile sạch.

## §3 Test — agent viết

`Tests/EditMode/UserSegmentDeciderTests.cs`: mọi chỗ gán `IsCampaignLocked = true` đổi thành gán `LockReason` cụ thể; mọi
assert `IsCampaignLocked` giữ và thêm assert `LockReason`.

| Ca | Kỳ vọng |
|---|---|
| state mới | `LockReason None`, `IsCampaignLocked false` |
| boot phiên N+1, chưa camp | `SessionWindow` |
| boot khi đã khoá vì `Attribution` | giữ `Attribution` |
| camp khớp ở phiên đầu · camp rỗng · camp không khớp · camp khớp purchased | `Attribution` |
| camp về khi tập purchased, chưa khoá | `Attribution` |
| mua khi chưa khoá | `Purchase` |
| camp rồi mua | giữ `Attribution`, tập purchased |
| hết cửa sổ rồi camp về | giữ `SessionWindow`, `LastCampaign` ghi, tập giữ |
| hết cửa sổ rồi mua | giữ `SessionWindow`, tập purchased |
| wire format: `JsonConvert.SerializeObject(state)` | không chứa `IsCampaignLocked`, có `"LockReason":` |
| wire format: `{"CurrSegmentId":2,"LockReason":2}` | đọc ra `IsCampaignLocked true`, `LockReason Purchase` |
| wire format: JSON cũ `{"IsCampaignLocked":true}` | đọc không ném, `LockReason None` (save cũ mở lại một lần) |

Host và `ResetState` không có test tự động (MonoBehaviour gắn Persistence); kiểm bằng compile và chơi thử của dự án.

## §4 Tài liệu — agent

`docs/Runtime/UserSegmentation/UserSegmentation.md`:

- Mở đầu đoạn đầu: không đổi.
- Bảng Luật: cột Kết quả các dòng khoá ghi lý do (`IsCampaignLocked = true` → `khoá: SessionWindow` · `Attribution` · `Purchase`),
  thêm một dòng "khoá chỉ ghi lý do khi đang `None`".
- Quyết định trái trực giác: hai dòng của Ngữ cảnh đã chốt (lý do đầu thắng, reset thay cả `_state`).
- Consumer: bảng đọc thêm `SessionCount`, `LockReason`.
- Mục mới **Debug** (sau Consumer): `IUserSegmentationDebug.ResetState()` explicit, caller thoát app, không publish; panel do
  dự án dựng.
- Chữ ký: `UserSegmentState` có `LockReason`, `IsCampaignLocked` (property, `[JsonIgnore]`); thêm `UserSegmentLockReason`,
  `IUserSegmentationDebug`; host thêm `SessionCount`, `LockReason`, `partial`.
- Cấu trúc file: thêm `AUserSegmentationHost.Debug.cs`.
- Mở rộng sau: bỏ dòng "Cheat khi dự án dùng SRDebugger…".
- Kiểm thử: dòng `UserSegmentDeciderTests` thêm "lý do khoá, lý do đầu thắng, wire format `LockReason`".

`.html` đồng bộ theo mốc, khi developer yêu cầu. Hệ dựng xong và `.md` đã cập nhật thì xoá Plan này.

## §5 Nghiệm thu

| Tiêu chí | Bằng chứng |
|---|---|
| compile sạch runtime, Editor, test | `compile-check` |
| mọi ca §3 | test EditMode xanh |
| không còn chỗ gán `IsCampaignLocked` | grep `IsCampaignLocked\s*=` trong `Runtime/` và `Tests/` = 0 |
| tài liệu khớp code | grep `LockReason`, `SessionCount`, `IUserSegmentationDebug` trong `UserSegmentation.md` |
