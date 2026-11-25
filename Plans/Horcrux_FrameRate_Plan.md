# Plan — `FrameRateBootStep`: một chủ giữ FPS mục tiêu, xin lại khi app về foreground

Bản tạm: category-jam `Assets/_Game/Scripts/Common/FrameRateBootStep.cs`, đã wire dưới `bootstrap_service` trong `Launcher.unity`.
**Phụ thuộc:** Bootstrap (đã có). Gõ **sau** khi bản tạm qua chơi thử trên máy Android 90/120Hz — máy thật bác cách xin lại thì Plan đổi trước.
Kiểm: `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,Horcrux.PlayModeTests.csproj --tests Horcrux.EditModeTests`; PlayMode chạy ở Window › General › Test Runner.

## Kịch bản

| Tình huống | Hệ làm gì | Người chơi thấy |
|---|---|---|
| scene chứa bootstrap nạp | `Awake` đặt `targetFrameRate` | màn loading, trước chuỗi boot, đã ở FPS mục tiêu |
| app vào nền | không làm gì | — |
| app về foreground, chuỗi boot đã xong | frame đó trả `-1`, frame sau xin lại số mục tiêu | một khựng ngắn rồi về FPS mục tiêu; máy 90/120Hz không còn kẹt 30–45 FPS |
| app về foreground khi chuỗi boot chưa xong | Bootstrap chưa phát hook, step không chạy | FPS giữ như trước — biên đã biết, ở Mở rộng sau |
| step bị huỷ giữa lượt chờ (thoát app, unload) | `finally` trả số mục tiêu | không kẹt ở `-1` |
| hai lần về foreground sát nhau | hai lượt chạy song song, cả hai kết ở số mục tiêu | như một lần |

## Flow

```
scene nạp ──► FrameRateBootStep.Awake ──► Application.targetFrameRate = targetFrameRate       FrameRateBootStep.cs

Unity OnApplicationPause / OnApplicationFocus
  └─► BootstrapService.HandleGoToBackground  (gộp hai tín hiệu, một lượt mỗi chuyến, chỉ sau khi khởi tạo xong)   BootstrapService.cs
        └─► ABootStep.OnGoToBackground(false) ──► FrameRateBootStep.OnGoToBackground                            FrameRateBootStep.cs
              └─► ReapplyFrameRateAsync: targetFrameRate = -1 ──NextFrame──► finally: targetFrameRate = mục tiêu
```

---

## GĐ1 — xong thì game đặt và giữ FPS mục tiêu qua mỗi lần app quay lại

```
BootstrapService ──OnGoToBackground──► FrameRateBootStep+ ──► Application.targetFrameRate
```

`Runtime/Implementations/Foundations/FrameRate/FrameRateBootStep.cs` — file mới:

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Bootstrap;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.FrameRate
{
    /// <summary>
    /// Chủ duy nhất của <see cref="Application.targetFrameRate"/>: đặt khi nạp scene chứa bootstrap, xin lại mỗi lần app về foreground.
    /// </summary>
    public class FrameRateBootStep : ABootStep
    {
        [SerializeField, Min(1), Tooltip("FPS mục tiêu của cả game.")]
        private int targetFrameRate = 60;

        // Awake chứ không InitializeAsync: màn chạy trước chuỗi boot (loading) cũng phải ở đúng FPS.
        private void Awake() => Application.targetFrameRate = targetFrameRate;

        public override UniTask InitializeAsync(CancellationToken ct) => UniTask.CompletedTask;

        public override void OnGoToBackground(bool inBackground)
        {
            if (!inBackground)
                ReapplyFrameRateAsync(destroyCancellationToken).Forget();
        }

        // Android màn 90/120Hz: nằm nền vài giây rồi quay lại, vsync kẹt ở nhịp thấp dù giá trị vẫn là mục tiêu.
        // Gán lại đúng số cũ có thể bị bỏ qua, nên trả về mặc định một frame rồi xin lại.
        private async UniTaskVoid ReapplyFrameRateAsync(CancellationToken ct)
        {
            Application.targetFrameRate = -1;
            try
            {
                await UniTask.NextFrame(ct);
            }
            finally
            {
                Application.targetFrameRate = targetFrameRate;
            }
        }
    }
}
```

**Editor setup** (dự án chưa có step này):

1. Trong scene chứa `BootstrapService`, chuột phải object của service › Create Empty, đặt tên `frame_rate_bootstep`.
2. Add Component › `FrameRateBootStep`; ô **Target Frame Rate** giữ 60 hoặc đổi theo game. Thiếu step: FPS là mặc định của nền tảng (30 trên Android).
3. Kéo object vào danh sách **Steps** của `BootstrapService`. Thiếu bước này: `Awake` vẫn đặt FPS, nhưng không bao giờ xin lại khi app quay về.

`Tests/PlayMode/FrameRateBootStepTests.cs` (agent) — `SetUp` ghi lại `Application.targetFrameRate`, `TearDown` trả lại:

| Case | Input | Kỳ vọng |
|---|---|---|
| Awake | `AddComponent<FrameRateBootStep>()` lên GameObject mới | `targetFrameRate` = 60 |
| vào nền | đặt 30, `OnGoToBackground(true)`, chờ 2 frame | vẫn 30 |
| về foreground, cùng frame | `OnGoToBackground(false)` | `-1` — đỏ khi bỏ bước trả mặc định |
| về foreground, frame sau | như trên, chờ 2 frame | 60 |
| huỷ giữa lượt chờ | `OnGoToBackground(false)`, `Object.Destroy(step.gameObject)`, chờ 2 frame | 60 — đỏ khi bỏ `finally` |

---

## Chuyển dự án (agent)

| Chỗ | Đổi |
|---|---|
| category-jam `Assets/_Game/Scripts/Common/FrameRateBootStep.cs` + `.meta` | xoá trước, rồi mới import file Horcrux |
| `Runtime/Implementations/Foundations/FrameRate/FrameRateBootStep.cs.meta` | GUID `bedabaa801b620848b16169dca3f5b6c` của bản tạm, Refresh — `frame_rate_bootstep` trong `Launcher.unity` giữ reference và ô `order`, `targetFrameRate` |
| `docs/Runtime/FrameRate/FrameRate.md`, `.html` | tạo: Trước khi chạy (ba bước Editor setup) · vì sao trả `-1` một frame · biên khi chuỗi boot chưa xong · Optimized Frame Pacing là lựa chọn của từng dự án |
| `Agent/Memory/inbox.md` › "FPS tụt hẳn sau khi app Android quay lại từ nền" | trỏ về hệ FrameRate |
| Plan này | xoá cùng `.meta` |
| grep | không còn `ScrewCraze3D.FrameRateBootStep`; compile sạch |
| chơi thử | máy Android 90/120Hz, bộ đếm FPS: vào một level → chuyển app khác hơn 5 giây → quay lại → FPS về mục tiêu sau một khựng; hỏng: kẹt 30–45 tới khi tắt app |

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | gán lại `targetFrameRate` đúng số đang có | engine có thể bỏ qua vì giá trị không đổi |
| bỏ | step tự nghe `OnApplicationFocus` / `OnApplicationPause` | `BootstrapService` đã gộp hai tín hiệu thành một lượt; hai chủ cho một tín hiệu thì thứ tự không ai giữ |
| bỏ | `[RuntimeInitializeOnLoadMethod]` tự dựng object giữ FPS | setup chạy ngầm, không thấy trong scene, thiếu cũng không ai biết |
| bỏ | tắt Optimized Frame Pacing bằng code | không đổi được lúc chạy; là Player Settings, mỗi dự án tự quyết |
| sau | phủ lượt về foreground trước khi chuỗi boot xong | khi đo được máy kẹt FPS ở màn loading; cần `BootstrapService` phát hook trước khi khởi tạo xong |
| sau | setter đổi FPS giữa phiên | khi game có tuỳ chọn 30/60/120 cho người chơi |
| sau | chờ nhiều frame trước khi xin lại | khi máy thật cho thấy một frame chưa đủ |
