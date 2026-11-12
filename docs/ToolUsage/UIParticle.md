# UIParticle — khi dự án dùng com.coffee.ui-particle (ParticleEffectForUGUI)

Ghi chú đặt hiệu ứng hạt lên canvas uGUI. Đã kiểm trên UIParticle 4.11.2, Unity 6, Built-in RP, canvas Overlay. Bản khác thì
mở `Runtime/UIParticle.cs` đối chiếu trước khi tin.

## Khi nào cần

Canvas **Overlay** vẽ sau mọi camera: ParticleSystem thường không hiện lên trên UI dù sorting order cao. UIParticle bake hạt
thành mesh của canvas, nên hạt xếp lớp theo thứ tự con như mọi Graphic. Canvas Camera hay World thì cân nhắc sorting order
trước khi thêm UIParticle.

## Dựng

```
<node UI> [RectTransform, UIParticle]          scale, Position Mode, Auto Scaling Mode
└── offset [Transform]                         dời gốc hiệu ứng về chỗ cần trên màn
    └── <prefab VFX gốc>                       không sửa; khác biệt đặt bằng override trên instance
```

| Ô | Chọn | Vì |
|---|---|---|
| `scale` | số pixel mỗi unit của camera mà hiệu ứng được dựng cho (thường 100) | hiệu ứng dựng bằng unit thế giới; ra pixel canvas phải nhân lại đúng tỉ lệ đó |
| `Position Mode` | `Relative` | hạt bám theo node như con của UI; `Absolute` dùng toạ độ thế giới |
| `Auto Scaling Mode` | xem bảng dưới | quyết hạt co theo màn thế nào |
| offset | vị trí tâm hiệu ứng đo trên demo, quy về unit của hiệu ứng | hiệu ứng tác giả đặt quanh tâm camera của họ, không quanh gốc prefab |

**Auto Scaling Mode** (theo mã nguồn):

| Chế độ | Làm gì | Hệ quả khi màn khác cỡ thiết kế |
|---|---|---|
| `Transform` (mặc định) | ép `lossyScale` của node về (1, 1, 1) | `scale` tính theo pixel màn thật: màn thấp hơn thiết kế thì hạt to hơn so với bố cục, màn cao hơn thì nhỏ hơn |
| `UIParticle` | chỉnh `scale` theo hệ số của canvas | hạt co cùng bố cục canvas |
| `None` | không làm gì | scale theo cây node như thường |

Hiệu ứng phải giữ tỉ lệ với bố cục trên mọi máy thì kiểm chế độ này ở vài cỡ màn (`demo-compare`), không chỉ ở cỡ thiết kế.

## Điều khiển

| Muốn | Gọi | Đừng gọi |
|---|---|---|
| Bắt đầu, phát lại từ đầu | `StartEmission()` rồi `Play()` (mô phỏng lại từ giây 0) | |
| Thôi phát, hạt đang bay rơi nốt | `StopEmission()` | `Stop()`: dừng phát **và** đặt `isPaused`, hạt đang bay đứng hình giữa màn |
| Đóng băng tạm rồi chạy tiếp | `Pause()`, rồi `Resume()` | `Play()` để chạy tiếp: nó phát lại từ đầu |

Hạt bay nốt rồi mới tắt object: chờ hết `startLifetime` lớn nhất sau `StopEmission()`, hoặc tắt sau khi lớp phủ khác đã che.

## Bẫy

| Triệu chứng | Nguyên nhân · cách tránh |
|---|---|
| Hạt hiện thành ô vuông đặc (đỏ, hồng) qua UIParticle, trong Scene view vẫn đúng | emitter dùng material mặc định (`Particles/Standard Unlit`) hay shader cần dữ liệu camera · gán material hạt có texture và shader hợp canvas; prefab VFX gốc giữ nguyên, đổi bằng override trên instance |
| Hạt lệch chỗ hay lệch cỡ so với demo dù khung tĩnh đã khớp | thiếu `scale` hoặc offset theo khung của tác giả · đo tâm, cỡ hiệu ứng trên demo, quy về unit rồi đặt |
| Kiểm bằng ảnh chụp canvas ngoài Play ra đúng mà trong game sai | ảnh chụp đổi canvas sang chế độ render khác · kiểm trong Play ở chế độ render của game (`workflow` › Nghiệm thu) |
| Shape Box phủ rộng mà hạt như đứng yên | đã ghi ở `ui-reskin` › Bẫy |
