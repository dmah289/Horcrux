# Plan — `SpineOneShotEffect`: hiệu ứng Spine một lượt từ pool, lên Horcrux

Bản tạm: category-jam `Assets/_CategoryJam/Scripts/Entities/PooledSpineEffect.cs` (lớp con `Tile/LinkBreakEffect.cs` ở lại dự án).
**Phụ thuộc:** không — gõ lúc nào cũng được.
Kiểm: `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.spine.csproj,<csproj gameplay của dự án> --tests <test EditMode của dự án>`.

## Kịch bản

| Tình huống | Hệ làm gì | Caller/người chơi thấy |
|---|---|---|
| caller lấy từ pool, `Play(vị trí, cỡ, layer, order)` | xoá track, về setup pose, đặt anim từ 0, `Update(0)`, phát hạt | frame đầu đã là pose frame 0 của anim, không loé setup pose |
| `Play(…, startTime)` | `TrackTime = startTime`, hạt `Simulate(startTime)` rồi `Play` | anim và hạt cùng bắt đầu giữa chừng (ví dụ từ mốc vỡ) |
| anim hết (`TrackEntry.IsComplete`) | `OnFinished` một lần | lớp con dự án trả về pool |
| `Follow(điểm neo)` | mỗi `LateUpdate` đặt vị trí = neo + khoảng cách lúc gọi | hiệu ứng đi theo vật đang trượt mà không làm con của nó |
| điểm neo tắt (chủ của neo về pool, scene dọn) | `OnFinished` ngay | hiệu ứng không kẹt trong pool của chủ neo |
| hạt có start delay | không đổi delay | hạt phát đúng mốc hoạ sĩ đặt trong anim |
| về pool giữa lượt | xoá track, bỏ neo, dừng và xoá hạt | lần lấy sau không mang pose, hạt cũ |
| lớp con cần đọc xương | `Skeleton` (SkeletonAnimation), `Duration` | lớp con lái vật theo xương, chờ đúng thời lượng anim |

## Flow

```
caller (dự án)
  ├─ pool.Get<LớpCon>() ─► LớpCon : SpineOneShotEffect          Vendors/Spine/SpineOneShotEffect.cs
  ├─ Play(pos, scale, layer, order, startTime) ──► SortingGroup · SkeletonAnimation.AnimationState · ParticleSystem[]
  ├─ Follow(anchor) (tuỳ chọn)
  │                                  LateUpdate: neo tắt | IsComplete ──► Finish ──► OnFinished() (abstract)
  └◄────────────────────────────────────────────────────────────────── LớpCon: pool.Return(this)
```

## GĐ1 — assembly `com.horcrux.spine` và `SpineOneShotEffect`: dự án thay bản tạm bằng lớp con một dòng

```
Vendors/Spine/
  + com.horcrux.spine.asmdef     (refs com.horcrux.runtime, spine-unity, spine-csharp; define HORCRUX_SPINE)
  + SpineOneShotEffect.cs
```

`Vendors/Spine/com.horcrux.spine.asmdef` — spine-unity đặt trong `Assets/` (không qua Package Manager) nên `versionDefines` không thấy
nó; dự án dùng Spine tự thêm define:

```json
{
    "name": "com.horcrux.spine",
    "rootNamespace": "",
    "references": [
        "com.horcrux.runtime",
        "spine-unity",
        "spine-csharp"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [
        "HORCRUX_SPINE"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

`Vendors/Spine/SpineOneShotEffect.cs`:

```csharp
using Horcrux.Runtime.Abstractions.Pooling;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Rendering;

namespace Horcrux.Vendors.Spine
{
    /// <summary>
    /// Một lượt Spine không lặp cùng hạt của nó, lấy từ pool: hết anim hoặc điểm neo tắt thì gọi <see cref="OnFinished"/>.
    /// Root prefab mang SortingGroup; skeleton và hạt là con, hạt tắt Loop và Play On Awake.
    /// </summary>
    public abstract class SpineOneShotEffect : PoolableBehaviour
    {
        #region Properties

        [SerializeField] private SkeletonAnimation _skeleton;
        [Tooltip("Gom skeleton và hạt thành một bậc vẽ; layer và order đặt mỗi lần phát.")]
        [SerializeField] private SortingGroup _sortingGroup;
        [SerializeField, SpineAnimation(dataField: nameof(_skeleton))] private string _animationName;
        [Tooltip("Phát cùng lúc với anim; start delay của hạt là mốc trong anim do hoạ sĩ đặt.")]
        [SerializeField] private ParticleSystem[] _particles = System.Array.Empty<ParticleSystem>();

        // global:: vì namespace này cũng tên Spine: "Spine.TrackEntry" sẽ tìm trong Horcrux.Vendors.Spine.
        private global::Spine.TrackEntry _track;
        private Transform _anchor;
        private Vector3 _offsetFromAnchor;

        public SkeletonAnimation Skeleton => _skeleton;
        /// <summary>Thời lượng anim theo dữ liệu; Spine và tween cùng chạy theo deltaTime nên chờ đúng số giây này là khớp nhịp anim.</summary>
        public float Duration => _skeleton.Skeleton.Data.FindAnimation(_animationName).Duration;

        #endregion Properties

        #region Unity Callbacks

        private void LateUpdate()
        {
            if (_track == null) return;

            if (_anchor != null)
            {
                // Chủ của điểm neo về pool: hiệu ứng không còn chỗ đứng.
                if (!_anchor.gameObject.activeInHierarchy)
                {
                    Finish();
                    return;
                }
                transform.position = _anchor.position + _offsetFromAnchor;
            }

            if (_track.IsComplete) Finish();
        }

        #endregion Unity Callbacks

        #region API

        /// <summary>Phát từ startTime (giây trong anim): hạt tua tới cùng mốc nên giữ nhịp với anim.</summary>
        public void Play(Vector3 position, float scale, int sortingLayerId, int sortingOrder, float startTime = 0f)
        {
            transform.SetPositionAndRotation(position, Quaternion.identity);
            transform.localScale = new Vector3(scale, scale, 1f);
            _sortingGroup.sortingLayerID = sortingLayerId;
            _sortingGroup.sortingOrder = sortingOrder;

            var state = _skeleton.AnimationState;
            state.ClearTracks();
            _skeleton.Skeleton.SetupPose();
            _track = state.SetAnimation(0, _animationName, false);
            _track.TrackTime = startTime;
            // Đặt pose ngay frame này; thiếu thì frame đầu vẽ setup pose (đủ mọi phần) rồi mới về anim.
            _skeleton.Update(0f);

            foreach (var particle in _particles)
            {
                if (startTime > 0f) particle.Simulate(startTime, true, true);
                particle.Play(true);
            }
        }

        /// <summary>Đi theo điểm neo, giữ khoảng cách lúc gọi; neo tắt thì kết thúc lượt. Gọi sau Play.</summary>
        public void Follow(Transform anchor)
        {
            _anchor = anchor;
            _offsetFromAnchor = transform.position - anchor.position;
        }

        public override void OnReturnToPool()
        {
            _track = null;
            _anchor = null;
            _skeleton.AnimationState.ClearTracks();
            foreach (var particle in _particles)
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        #endregion API

        #region Class Methods

        /// <summary>Dự án trả về pool của nó: framework không biết khoá pool.</summary>
        protected abstract void OnFinished();

        private void Finish()
        {
            _track = null;
            OnFinished();
        }

        #endregion Class Methods
    }
}
```

**Editor setup**
1. Project Settings › Player › Scripting Define Symbols: thêm `HORCRUX_SPINE` cho mọi platform build. Thiếu thì assembly không
   compile, lớp con dự án báo `CS0246 SpineOneShotEffect`.
2. Asmdef gameplay của dự án: thêm reference `com.horcrux.spine`.

| Input | Kỳ vọng |
|---|---|
| `Play` rồi chờ `Duration` | `OnFinished` đúng một lần ở frame `IsComplete` |
| `Play(…, startTime = Duration − 0.1)` | `OnFinished` sau ~0.1 s |
| `Follow(neo)` rồi tắt neo | `OnFinished` ở `LateUpdate` kế tiếp |
| về pool rồi lấy lại, `Play` | không còn neo cũ, hạt bắt đầu từ 0 |

## Chuyển dự án (agent)

- category-jam `Scripts/Entities/PooledSpineEffect.cs` thay bằng lớp con một dòng (trả về `IPoolService<GameplayPool>`); tên field
  serialize giữ nguyên nên prefab `LinkBreakVfx`, `HiddenRevealVfx` không phải gán lại. `LinkBreakEffect` không đổi.
- Asmdef `CategoryJam.Gameplay` thêm `com.horcrux.spine`; define `HORCRUX_SPINE`.
- `TileMechanics.md` › Hiệu ứng trỏ `docs/Runtime/SpineOneShot/`; xoá Plan này.
- Chơi thử: nhặt cặp link, Magnet hút một tile của cặp, lật tile "?" — hình và nhịp như trước khi chuyển.

## Không làm · Mở rộng sau

- Không tự trả về pool trong Horcrux: khoá pool là từ vựng dự án; `OnFinished` abstract để dự án trả bằng một dòng.
- Không làm con của điểm neo: chủ neo về pool sẽ kéo hiệu ứng vào pool của nó lúc pool hiệu ứng còn coi là đang dùng.
- Không nghe event Spine để bắn hạt: start delay của hạt do hoạ sĩ đặt đã neo vào mốc anim; khi anim có event thì lớp con nghe `Skeleton.AnimationState.Event`.
- Mở rộng sau: lớp con lái vật theo xương (đặt vị trí, góc vật mỗi `UpdateComplete`) lên Horcrux khi có game thứ hai cần — hôm nay chỉ một caller.
