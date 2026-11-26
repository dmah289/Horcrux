# Spine — khi dự án dùng spine-unity 4.3

Ghi chú dùng runtime Spine trong Unity: nâng bản, dựng skeleton trên canvas, gọi anim từ code. Đã kiểm trên spine-unity 4.3
(gói `spine-unity-4.3-2026-11-10`), Unity 6, canvas Overlay và skeleton world. Bản khác thì mở mã nguồn đối chiếu trước khi tin.

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
| `AnimationState` mơ hồ giữa `Spine.AnimationState` và `UnityEngine.AnimationState` (CS0104) | file có cả `using Spine;` lẫn `using UnityEngine;`: thêm `using AnimationState = Spine.AnimationState;` |
| Code cũ đọc, ghi `startingAnimation` | đổi sang `AnimationName` của `SkeletonAnimation`. Gán tên là bắt đầu anim, nên có thể bắn sự kiện start, end thêm một lần; code chỉ cần đặt anim thì gọi `AnimationState.SetAnimation` |

## Dựng skeleton trên canvas

- **Một object mang cả `SkeletonGraphic` và `SkeletonAnimation`.** Dựng bằng code Editor thì
  `SkeletonGraphic.AddSkeletonGraphicAnimationComponents(gameObject, skeletonDataAsset, material)`; component trả về đã nối với nhau.
  Ô tham chiếu trong script kiểu `SkeletonAnimation`: đó là thứ gọi anim.
- **Dựng tay:** bản này không có menu `GameObject › Spine`. Kéo asset `SkeletonData` từ Project thả lên một object nằm trong
  Canvas ở Hierarchy, hoặc Create Empty dưới Canvas rồi Add Component `Spine › SkeletonGraphic (Unity UI Canvas)` và
  `Spine › SkeletonAnimation`, gán `SkeletonData` vào ô của `SkeletonGraphic`. Tên anim, Loop, Unscaled Time nằm ở
  `SkeletonAnimation`, không ở `SkeletonGraphic`; UI chạy khi `timeScale` = 0 thì bật Unscaled Time.
- **Material theo atlas.** Bản 4.3 mặc định atlas không nhân sẵn alpha (straight). Atlas straight dùng
  `Spine/Runtime/spine-unity/Materials/UI-StraightAlphaTex/SkeletonGraphicDefault-Straight.mat`, material của atlas có
  `_StraightAlphaInput: 1`. Atlas nhân sẵn alpha (PMA) dùng bộ `UI-PMATexture`. Lệch hai bên thì viền ảnh tối hoặc sáng quầng.
  Đầu file `.atlas.txt` có dòng `pma:true` khi nhân sẵn alpha; không có dòng đó là straight.
  Skeleton mờ dần theo `CanvasGroup` cha thì dùng bản trong thư mục con `CanvasGroup/` (ví dụ
  `UI-StraightAlphaTex/CanvasGroup/SkeletonGraphicDefault-CanvasGroupStraight.mat`). `CanvasGroup` chỉ hạ alpha của màu
  đỉnh mà không nhân vào RGB; material thường coi màu đỉnh đã nhân sẵn nên lúc mờ, skeleton sáng loá thay vì nhạt dần.
  Bản `CanvasGroup` bật `_CANVAS_GROUP_COMPATIBLE` để tự nhân.
- **Texture straight trong project Linear phải bật sRGB.** Spine nhập texture theo preset ở ô `textureSettingsReference` của
  `SpineSettings.asset`; ô trống là preset PMA, tắt sRGB. Texture màu bị đọc như số linear thì màu nhạt đi rõ (kênh 99 hiện thành
  khoảng 166). Đặt ô đó trỏ `StraightAlphaPreset` trước khi nhập dữ liệu straight; texture đã nhập thì bật lại sRGB và Alpha Is
  Transparency. *Đã sai một lần:* logo nhập bằng preset mặc định, màu nhạt hơn demo; số đo trên texture gốc vẫn đúng màu demo.
- Khung dữ liệu xuất bằng cỡ màn thiết kế (ví dụ 1080×2340) thì RectTransform của skeleton đặt đúng cỡ đó, gốc skeleton ở tâm,
  để vị trí trong Spine Editor khớp màn.

## Skeleton trong world (ngoài canvas)

- **4.3 tách vẽ khỏi anim cả ở world:** một object mang `SkeletonRenderer`, `SkeletonAnimation`, `MeshRenderer`. Dựng bằng code
  Editor: `SkeletonRenderer.AddSpineComponents<SkeletonRenderer, SkeletonAnimation>(gameObject, skeletonDataAsset, false)`.
  Lớp vẽ đặt ở `MeshRenderer` (Sorting Layer, Order in Layer).
- **Skeleton đi cùng hạt của nó thì gom dưới một cha có `SortingGroup`**: cả cụm xếp với phần còn lại của màn bằng một bậc, thứ tự
  skeleton với hạt chỉnh bằng order bên trong nhóm, không giãn bậc của lớp khác.
- **Hạt hoạ sĩ đóng kèm để Loop và Play On Awake** cho bản preview. Game phát theo lượt thì tắt cả hai, phát hạt cùng lúc với anim;
  start delay hoạ sĩ đặt là mốc trong anim, giữ nguyên.
- Cỡ skeleton world quy theo vật world đứng cạnh nó trên demo (`Agent/Skills/demo-compare/SKILL.md`), không theo bề ngang màn.
- **Skeleton thay một sprite tĩnh cùng art** (vật lật, vỡ từ hình đang nằm yên): gốc skeleton thường lệch pivot sprite vài phần
  trăm cỡ vật, đủ thấy giật lúc đổi. Chụp sprite và frame 0 skeleton cùng khung (`UICapture.World` hai ô, `docs/Editor/UIReskin.md`),
  so khung bằng `DemoCompare.BBox`, đặt phần lệch vào object con mang skeleton; root giữ đúng vị trí code đặt.
- **Vẽ ngoài Play** (chụp, so, dựng prefab): không vòng frame nào chạy, nên gọi tay `SkeletonRenderer.Initialize(true)`,
  `SkeletonAnimation.Initialize(true)`, `SetAnimation`, đặt `TrackTime`, `Update(0f)`, rồi `SkeletonRenderer.LateUpdate()` để dựng mesh.

## Gọi anim từ code

**Sổ tay** — hiện ra rồi lặp:

```csharp
var state = skeletonAnimation.AnimationState;
state.SetAnimation(0, appear, false);
state.AddAnimation(0, idle, true, 0f);
skeletonAnimation.Update(0f);   // đặt pose frame 0 ngay; thiếu thì frame đầu vẽ setup pose (đủ mọi phần) rồi mới về anim
```

- **Nối nhiều clip không trộn** (hoạ sĩ đã khớp pose cuối clip trước với pose đầu clip sau): `AddAnimation(0, tên, false, 0f)`
  rồi `entry.SetMixDuration(0f, 0f)`. `AddAnimation` tính `Delay` theo mix mặc định ngay lúc gọi; gán riêng `MixDuration = 0` không
  tính lại `Delay`, clip sau vẫn bắt đầu sớm hơn clip trước hết một khoảng mix.
- **Chạy lại từ đầu** một vật đã diễn: `AnimationState.ClearTracks()` rồi `Skeleton.SetupPose()` trước khi xếp clip, rồi
  `Update(0f)` như trên. Thiếu `SetupPose` thì slot, attachment clip cũ để lại còn trên frame đầu lượt mới.
- **Chặng code chạy song song với anim** (vật bay tới đích trong lúc clip bay chạy) lấy thời lượng bằng tổng `Animation.Duration` của
  các clip đã xếp trước clip tác động: vật tới đích đúng frame clip tác động bắt đầu, hoạ sĩ đổi clip thì chặng code theo.
- Thời lượng lấy từ dữ liệu: `skeleton.Data.FindAnimation(name).Duration`. Spine và `UniTask.Delay` cùng chạy theo
  `Time.deltaTime`, nên chờ đúng số giây đó là khớp nhịp anim, không cần nghe sự kiện complete.
- Track đang chạy: `AnimationState.GetTrack(0)` (4.3 không có `GetCurrent`); thời điểm trong anim là
  `TrackEntry.AnimationTime`.
- **Phát từ giữa anim** (chỉ đoạn sau một mốc): `entry.TrackTime = t` sau `SetAnimation`, rồi `Update(0f)`. Hạt đi cùng tua tới cùng
  mốc: `Simulate(t, true, true)` rồi `Play(true)`; thiếu thì hạt có start delay bắn trễ đúng `t`.
- **Lượt không lặp đã hết:** `TrackEntry.IsComplete`, đọc ở `LateUpdate` (sau khi Spine tiến frame). Hiệu ứng một lượt lấy từ pool
  dùng nó để tự trả về.
- Ô tên anim trong Inspector: `[SpineAnimation(dataField: nameof(<ô SkeletonAnimation>))]` cho dropdown tên có thật.
- Đo nhịp anim theo video demo: `docs/Editor/VideoDemo.md`; quay bản Unity bằng `FrameRecorder` (`docs/Editor/Probe.md`).

## Xương lái vật ngoài skeleton

Anim chỉ có xương (hoạ sĩ dựng chuyển động, vật thật do game vẽ): mỗi frame đặt vật theo xương. Xem tên xương và khoá của nó bằng
`docs/Editor/SpineJson.md`.

- **Đọc xương trong `SkeletonAnimation.UpdateComplete`:** chạy ngay sau khi Spine tính world transform của frame, nên vật và phần
  skeleton vẽ ra cùng frame; đọc ở `Update` của vật thì trễ một frame. Handler là event của component, không bị xoá như handler
  của `TrackEntry`: đăng ký một lần.
- **Vị trí** `bone.GetWorldPosition(skeletonTransform)`; **góc** `bone.AppliedPose.WorldRotationX` (4.3 để pose đã áp ở
  `AppliedPose`).
- **Vật nhận phần xương đã đi, không nhận vị trí xương:** vị trí vật = vị trí vật lúc bắt đầu + (xương bây giờ − xương lúc bắt
  đầu), góc cùng cách. Vật giữ chỗ đứng game đặt; skeleton không cần trùng khít chỗ vật.
- **Anim dựng cho một khoảng cách giữa hai vật:** scale skeleton = khoảng cách thật của hai vật ÷ khoảng cách hai xương ở frame
  đầu, đo ở scale 1 sau `Update(0f)`. Chuyển động co theo, đúng ở mọi khoảng cách.
- **`FindBone` lại mỗi lượt:** skeleton dựng lại (nạp lại dữ liệu) thay object `Bone`, tham chiếu cũ không còn được cập nhật.
- **Vật chỉ nhận tư thế khi nó đang ở trạng thái chờ theo xương** (cờ do chính vật giữ): hết chờ, bị huỷ, về pool thì code của vật
  chiếm lại transform mà hiệu ứng không cần biết vòng đời vật.

## Event trong anim — neo hiệu ứng vào khoảnh khắc của anim

Hiệu ứng ăn theo một khoảnh khắc trong anim (tay chạm xuống, chân chạm đất) neo vào event Spine đặt ở khoảnh khắc đó, không vào
delay đo tay.

- **Đặt event:** đúng chỗ là trong project Spine của hoạ sĩ. Sửa tay json được để thử: khối `"events": { "<tên>": {} }` ở gốc
  (trước `"animations"`), và trong từng anim một mảng `"events": [ { "time": <giây>, "name": "<tên>" } ]`. Xuất lại từ Spine
  Editor là mất phần sửa tay, nên bàn giao tên và thời điểm cho hoạ sĩ.
- **Nghe event:** `AnimationState.Event += (TrackEntry entry, Spine.Event e) => …`, lọc theo `e.Data.Name`. `AnimationState` lấy
  ở `Start`, chưa chắc có ở `Awake`. Skeleton dựng lại (đổi `SkeletonData` trong Inspector, reload) thay `AnimationState` mới:
  đăng ký lại trong `SkeletonAnimation.OnAnimationRebuild`, gỡ ở bản cũ.
- **Một lượt diễn chờ event của một clip:** gắn handler lên `TrackEntry` của chính clip đó (`entry.Event`, `entry.Complete`), không
  lên `AnimationState`: chỉ nghe đúng clip, và Spine xoá handler khi trả entry về pool (`TrackEntry.Reset`), nên gắn mỗi lượt không
  rò. Handler chỉ bật cờ; chủ lượt diễn `await UniTask.WaitUntil(cờ, …, ct)` với delegate đọc cờ dựng một lần, cờ đặt lại ở đầu
  mỗi lượt. Huỷ giữa chừng thì `finally` của chủ `ClearTracks` và tắt vật.
- **Anim chưa có event:** đọc `TrackEntry.AnimationTime` mỗi `LateUpdate` (sau khi Spine đã tiến frame) và bắn khi mốc nằm trong
  khoảng (frame trước, frame này]. Anim lặp thì thời điểm quay về đầu: khoảng bị cắt làm hai. Có event thật thì bỏ đường đọc
  thời điểm, không bắn hai lần. Biết anim có event chưa: duyệt `animation.Timelines` tìm `EventTimeline` có event cùng tên.
- File có cả `using Spine;` lẫn `using UnityEngine;` thì `Animation` mơ hồ (CS0104): viết đủ `Spine.Animation`, `Spine.Event`.
