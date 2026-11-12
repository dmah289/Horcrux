# Spine — khi dự án dùng spine-unity 4.3

Ghi chú dùng runtime Spine trong Unity: nâng bản, dựng skeleton trên canvas, gọi anim từ code. Đã kiểm trên spine-unity 4.3
(gói `spine-unity-4.3-2026-11-10`), Unity 6, canvas Overlay. Bản khác thì mở mã nguồn đối chiếu trước khi tin.

## Nâng runtime

1. **Dữ liệu phải xuất từ Spine Editor cùng dòng.** `Assets/Spine/version.txt` ghi bản Editor tối thiểu (4.3 cần 4.3.00 trở lên).
   Json, skel của bản cũ hơn không nạp được. Dữ liệu cũ không ai dùng thì xoá cả bộ (json, atlas, png, material, SkeletonData)
   cùng mọi object trỏ tới nó, trước khi nâng.
2. **Xoá thư mục `Assets/Spine` cũ rồi import gói mới.** Import đè lên bản cũ để lại file đã bị đổi tên hay xoá ở bản mới.
3. **Edit › Preferences › Spine › Upgrade Scenes & Prefabs › Upgrade All** nâng component trong scene, prefab. Chạy một lần
   sau khi compile sạch, rồi đọc diff.
4. Sửa lỗi compile theo mục API đổi dưới đây.

Gói asset của hoạ sĩ hay đóng kèm cả runtime Spine, thường là bản khác bản đang cài. Soát gói bằng `docs/Editor/UnityPackageAudit.md`
và chỉ tick phần hiệu ứng cần.

## API đổi ở 4.3

| Triệu chứng | Nguyên nhân · cách sửa |
|---|---|
| `SkeletonGraphic` không có `AnimationState`, `startingAnimation` (CS1061) | 4.3 tách anim khỏi phần vẽ. Anim nằm ở component `SkeletonAnimation` cùng object; `SkeletonGraphic.Animation` trả `ISkeletonAnimation`, ép sang `SkeletonAnimation` để lấy `AnimationState`, `AnimationName` |
| `Sequence` mơ hồ giữa `Spine.Sequence` và `PrimeTween.Sequence` (CS0104) | 4.3 thêm `Spine.Sequence`. File dùng PrimeTween không `using Spine;`, viết đủ `Spine.TrackEntry` ở chỗ cần |
| Code cũ đọc, ghi `startingAnimation` | đổi sang `AnimationName` của `SkeletonAnimation`. Gán tên là bắt đầu anim, nên có thể bắn sự kiện start, end thêm một lần; code chỉ cần đặt anim thì gọi `AnimationState.SetAnimation` |

## Dựng skeleton trên canvas

- **Một object mang cả `SkeletonGraphic` và `SkeletonAnimation`.** Dựng bằng code Editor thì
  `SkeletonGraphic.AddSkeletonGraphicAnimationComponents(gameObject, skeletonDataAsset, material)`; component trả về đã nối với nhau.
  Ô tham chiếu trong script kiểu `SkeletonAnimation`: đó là thứ gọi anim.
- **Material theo atlas.** Bản 4.3 mặc định atlas không nhân sẵn alpha (straight). Atlas straight dùng
  `Spine/Runtime/spine-unity/Materials/UI-StraightAlphaTex/SkeletonGraphicDefault-Straight.mat`, material của atlas có
  `_StraightAlphaInput: 1`. Atlas nhân sẵn alpha (PMA) dùng bộ `UI-PMATexture`. Lệch hai bên thì viền ảnh tối hoặc sáng quầng.
  Đầu file `.atlas.txt` có dòng `pma:true` khi nhân sẵn alpha; không có dòng đó là straight.
- Khung dữ liệu xuất bằng cỡ màn thiết kế (ví dụ 1080×2340) thì RectTransform của skeleton đặt đúng cỡ đó, gốc skeleton ở tâm,
  để vị trí trong Spine Editor khớp màn.

## Gọi anim từ code

**Sổ tay** — hiện ra rồi lặp:

```csharp
var state = skeletonAnimation.AnimationState;
state.SetAnimation(0, appear, false);
state.AddAnimation(0, idle, true, 0f);
skeletonAnimation.Update(0f);   // đặt pose frame 0 ngay; thiếu thì frame đầu vẽ setup pose (đủ mọi phần) rồi mới về anim
```

- Thời lượng lấy từ dữ liệu: `skeleton.Data.FindAnimation(name).Duration`. Spine và `UniTask.Delay` cùng chạy theo
  `Time.deltaTime`, nên chờ đúng số giây đó là khớp nhịp anim, không cần nghe sự kiện complete.
- Ô tên anim trong Inspector: `[SpineAnimation(dataField: nameof(<ô SkeletonAnimation>))]` cho dropdown tên có thật.
- Đo nhịp anim theo video demo: `docs/Editor/VideoDemo.md`; quay bản Unity bằng `FrameRecorder` (`docs/Editor/Probe.md`).
