# Plan — `SpriteHorizontalFill` lên Horcrux

Nguồn: bản tạm của một dự án game, cắt ruột thanh đếm lớp trên kệ (`SpriteRenderer` world, có vạch chia). Một shader sprite cắt theo
trục ngang và một component ghi tỉ lệ cắt cho từng renderer — `Image.fillAmount` cho `SpriteRenderer`.

**Phân công:** GĐ1, GĐ2 developer gõ, agent viết test theo bảng case rồi compile · GĐ3 (dự án chuyển sang bản Horcrux) và GĐ4 (tài liệu
hệ, xoá Plan) agent làm. GĐ1, GĐ2, GĐ4 commit trong `Assets/Horcrux`, GĐ3 commit ở dự án; mỗi lần đều kèm con trỏ submodule. Không phụ thuộc
Plan nào, không có `§0`: phép toán một dòng nằm ở GĐ1, GĐ2.

## Ngữ cảnh đã chốt

- **Người dùng:** thanh vẽ bằng `SpriteRenderer` mà đầu ruột phải **phẳng** (thanh chia vạch, đầu cắt chạm đúng tâm vạch), ruột giữ
  nguyên hình art — không co, không kéo.
- **Chỗ đặt, tên:** `Runtime/Utilities/Sprites/SpriteHorizontalFill.cs` và `SpriteFillDividers.cs` (vạch chia), namespace `Horcrux.Runtime.Utilities`; shader
  `Shader/sprite_horizontal_fill.shader`, tên `Horcrux/sprite_horizontal_fill`. Không phụ thuộc hệ nào.
- **Khác `SlicedFill`:** `SlicedFill` đổi `size` của Sliced nên hai đầu bo luôn còn; ở đây đầu phải cắt phẳng, nên không đổi hình học
  mà bỏ texel qua `clip` trong shader. Hai utility cùng sống, mỗi cái một kiểu thanh.
- **Vì sao shader, không `SpriteMask`:** mask ghi stencil chung; renderer `VisibleInsideMask` hiện ở mọi vùng mask khác cùng dải, và mất
  `VisibleOutsideMask` của bàn chơi. Shader cắt riêng từng renderer, mask interaction của renderer giữ nguyên (đã kiểm: mask không dải
  che ruột như che sprite Sprites-Default).
- **Vì sao UV, không toạ độ object:** sprite batching gộp đỉnh sang world nên object space không tin được; UV luôn đúng. Giá: atlas chứa
  sprite phải **tắt xoay** (`enableRotation: 0`) để trục u là trục ngang.
- **Vị trí và UV lấy từ cùng hai đỉnh của mesh:** đỉnh trái nhất, phải nhất cho cả `Left`/`Width` lẫn khoảng u, nên tỉ lệ `t` cùng một
  nghĩa ở đầu cắt và ở `Left + t * Width` dù mesh Tight hay Full Rect. Lấy UV từ đỉnh còn vị trí từ `sprite.bounds` thì hai nguồn chỉ
  trùng khi mesh phủ trọn rect; mesh Tight trên art có lề trong suốt làm vạch lệch khỏi đầu cắt.
- **Giá hiệu năng:** mỗi renderer một `MaterialPropertyBlock` riêng nên không batch với renderer khác; nhận vì thanh ít và đứng riêng.
- **Cố ý không làm:** trục dọc, cắt từ phải, tâm ra hai phía — chưa có người dùng · tween — caller tự `Tween.Custom` rồi ghi
  `FillAmount` · xem trước tỉ lệ ngoài Play — tỉ lệ do code ghi lúc chạy.

## Đã khảo sát

| Nơi | Kết luận |
|---|---|
| `Runtime/Utilities/UI/ProgressBar` | `Image.fillAmount` của UGUI, không cho `SpriteRenderer` |
| `Plans/Horcrux_SlicedFill_Plan.md` | đổi `size` Sliced: đầu bo giữ lại, không cắt phẳng được |
| `Shader/` của Horcrux | shader UI và nền, không có shader sprite cắt |

## Luồng mô tả

```
Awake     : iL, iR ← đỉnh x nhỏ nhất, lớn nhất của sprite.vertices
            Left, Width ← vertices[iL].x, vertices[iR].x − Left
            uMin, uWidth ← uv[iL].x, uv[iR].x − uMin    (UV trong texture đang vẽ: trang atlas khi đã pack)
            Apply()                                    (giá trị gán trước Awake được áp ở đây)
FillAmount = t :
            t ← Clamp01(t)
            t = 0 → renderer.enabled = false
            t > 0 → enabled = true ; MPB._FillU = uMin + uWidth * t
Shader    : clip(_FillU − uv.x)  → texel bên phải đầu cắt bị bỏ, đầu cắt phẳng
Caller    : vị trí đầu cắt theo local renderer = Left + t * Width (đặt vạch, hiệu ứng ở đầu cắt)
Dividers  : SetSegments(n) đặt vạch k ở Left + Width·k/n ; Show(t) ghi FillAmount và bật vạch đã nằm trọn trên ruột
```

## Bản đồ hệ cuối

| Thành phần | Ai gọi | Việc |
|---|---|---|
| `sprite_horizontal_fill.shader` | material trên renderer | Sprites-Default + `clip` theo `_FillU` |
| `SpriteHorizontalFill.FillAmount` | caller mỗi lần dữ liệu đổi, mỗi frame tween | ghi `_FillU`, bật/tắt renderer |
| `SpriteHorizontalFill.Left`, `Width` | `SpriteFillDividers`, caller khác bám đầu cắt, sau `Awake` | mép trái và bề rộng mesh sprite theo local renderer |
| `SpriteFillDividers.SetSegments` | caller khi số đoạn đổi | đặt n − 1 vạch ở k/n |
| `SpriteFillDividers.Show` | caller thay cho `FillAmount` khi thanh có vạch | ghi tỉ lệ, bật vạch trên phần ruột |

## Giai đoạn 1 — shader và `SpriteHorizontalFill`

**Mục tiêu:** một renderer cắt phẳng đúng ở mọi tỉ lệ, kể cả 0 và 1. **Vì sao trước:** là toàn bộ hệ.

File mới `Shader/sprite_horizontal_fill.shader`:

```hlsl
// Sprites/Default plus a horizontal cut: keeps texels with u <= _FillU, so the cut edge is straight like Image.fillAmount.
// _FillU is written per renderer by SpriteHorizontalFill; the default 1 keeps the whole sprite.
Shader "Horcrux/sprite_horizontal_fill"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        [PerRendererData] _FillU ("Fill U (atlas space)", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment FillFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            float _FillU;

            fixed4 FillFrag(v2f IN) : SV_Target
            {
                clip(_FillU - IN.texcoord.x);
                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;
                c.rgb *= c.a;
                return c;
            }
        ENDCG
        }
    }
}
```

- Thân giống Sprites-Default (`UnitySprites.cginc` lo đỉnh, màu renderer, flip, alpha ngoài), chỉ thêm dòng `clip`.
- `_FillU` mặc định 1: renderer chưa có MPB (ngoài Play, chưa `Awake`) vẽ cả sprite, vì UV không vượt 1.
- Shader này cho built-in render pipeline; dự án URP cần bản Shader Graph hay HLSL URP — chưa có người dùng.

File mới `Runtime/Utilities/Sprites/SpriteHorizontalFill.cs`:

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Utilities
{
    /// <summary>
    /// Cắt SpriteRenderer theo chiều ngang như Image.fillAmount: giữ phần từ mép trái tới tỉ lệ, đầu cắt phẳng, sprite không co.
    /// Material của renderer dùng shader Horcrux/sprite_horizontal_fill.
    /// </summary>
    public class SpriteHorizontalFill : MonoBehaviour
    {
        #region Properties

        private static readonly int s_fillUId = Shader.PropertyToID("_FillU");

        [Tooltip("Renderer dùng material shader Horcrux/sprite_horizontal_fill. Component này giữ ô enabled của nó: tỉ lệ 0 thì tắt.")]
        [SerializeField] private SpriteRenderer fillRenderer;

        private MaterialPropertyBlock _block;
        private float _left;
        private float _width;
        private float _uMin;
        private float _uWidth;
        private float _fillAmount = 1f;

        /// <summary>Tỉ lệ giữ lại từ mép trái, kẹp [0, 1]; 0 tắt renderer. Mặc định 1: cả sprite.</summary>
        public float FillAmount
        {
            get => _fillAmount;
            set
            {
                _fillAmount = Mathf.Clamp01(value);
                Apply();
            }
        }

        /// <summary>Bề rộng mesh sprite theo đơn vị local của renderer: tỉ lệ t nằm ở x = Left + t * Width. Đọc được sau Awake.</summary>
        public float Width => _width;

        /// <summary>Mép trái mesh sprite theo đơn vị local của renderer. Đọc được sau Awake.</summary>
        public float Left => _left;

        #endregion Properties

        #region Unity Callbacks

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            // Đỉnh trái nhất và phải nhất của mesh cho cả vị trí lẫn UV: mesh Tight hay Full Rect thì t vẫn cùng một nghĩa ở
            // đầu cắt và ở Left + t * Width. sprite.uv là UV trên trang atlas khi đã pack; atlas phải tắt xoay để u chạy theo x.
            var sprite = fillRenderer.sprite;
            var vertices = sprite.vertices;
            var uv = sprite.uv;
            var leftIndex = 0;
            var rightIndex = 0;
            for (var i = 1; i < vertices.Length; i++)
            {
                if (vertices[i].x < vertices[leftIndex].x) leftIndex = i;
                if (vertices[i].x > vertices[rightIndex].x) rightIndex = i;
            }
            _left = vertices[leftIndex].x;
            _width = vertices[rightIndex].x - _left;
            _uMin = uv[leftIndex].x;
            _uWidth = uv[rightIndex].x - _uMin;
            Apply();
        }

        #endregion Unity Callbacks

        #region Class Methods

        private void Apply()
        {
            if (_block == null) return;

            fillRenderer.enabled = _fillAmount > 0f;
            if (!fillRenderer.enabled) return;

            fillRenderer.GetPropertyBlock(_block);
            _block.SetFloat(s_fillUId, _uMin + _uWidth * _fillAmount);
            fillRenderer.SetPropertyBlock(_block);
        }

        #endregion Class Methods
    }
}
```

- `Awake` đọc `sprite.vertices` và `sprite.uv` một lần (mỗi cái cấp phát một mảng): mesh của sprite không đổi suốt đời renderer. Sprite
  đổi lúc chạy thì phải đọc lại — chưa có người dùng, nên không có API.
- Hai mảng cùng chỉ số: `uv[i]` là UV của `vertices[i]`. Atlas không xoay thì u tuyến tính theo x, nên hai đỉnh ở hai mép là đủ.
- `Left`, `Width` là số chụp ở `Awake`: caller đọc trước `Awake` (component trên object chưa bật lần nào) nhận 0. Caller đặt vạch lúc
  khởi tạo dữ liệu, sau khi prefab đã `Instantiate` và bật, nên không cần đọc lười.
- `if (_block == null) return;` là ca hợp lệ chưa tới lượt: caller gán `FillAmount` trước `Awake` của component (hai component cùng prefab,
  `Awake` không định thứ tự); giá trị được giữ và `Awake` áp.
- `Left`, `Width` theo local renderer (đỉnh của sprite), không theo `bounds` của renderer (world, gồm scale cha): caller đặt con của
  renderer theo tỉ lệ thì đúng ở mọi scale.
- `GetPropertyBlock` trước `SetPropertyBlock`: giữ giá trị khác caller có thể đã đặt trên cùng renderer.

**Editor setup** (phía caller): material mới dùng shader `Horcrux/sprite_horizontal_fill`, gán vào renderer ruột · `drawMode` Simple ·
thêm component, kéo renderer vào ô **Fill Renderer** · atlas chứa sprite tắt **Allow Rotation**. Thiếu material: không cắt, ruột luôn đầy.
Thiếu ô: `NullReferenceException` ở `Awake` lần Play đầu. Atlas xoay sprite: đầu cắt chạy theo trục dọc của art.

**Kiểm:** compile sạch; test PlayMode `Horcrux.PlayModeTests` (cần `Awake`; agent viết sau khi đọc code thật), sprite tạo bằng
`Sprite.Create` trên texture 100×10, renderer có material shader trên:

| Input | Kỳ vọng |
|---|---|
| chưa gán `FillAmount` | `_FillU` của MPB = uMax; renderer bật |
| `FillAmount = 0.5` | `_FillU` = uMin + 0.5 × (uMax − uMin) |
| `FillAmount = 0` · `-1` | renderer tắt · `FillAmount` đọc lại 0 |
| `FillAmount = 2` | đọc lại 1, `_FillU` = uMax |
| gán 0.3 trước `Awake` (component tắt rồi bật) | sau `Awake`, `_FillU` theo 0.3 |
| sprite là mảnh con của texture (rect lệch trái) | uMin > 0; `_FillU` nội suy trong khoảng UV của mảnh |
| `Left`, `Width` sprite 100 px, 100 ppu, pivot giữa | −0.5, 1 |
| sprite pivot trái (`Sprite.Create` pivot (0, 0.5)), `FillAmount = 0.25` | `Left` 0; `_FillU` = uMin + 0.25 × uWidth |

**Mốc commit:** compile sạch + bảng case xanh → commit trong `Assets/Horcrux`.

## Giai đoạn 2 — `SpriteFillDividers`

**Mục tiêu:** thanh chia n đoạn, đầu cắt dừng đúng tâm vạch, vạch chỉ hiện trên phần ruột. **Vì sao sau GĐ1:** đọc `Left`, `Width` và
ghi `FillAmount` của ruột đã cắt được.

```
SetSegments(n) : n − 1 vạch (chép thêm từ vạch mẫu khi thiếu)
                 vạch k (1…n−1) ở x = Left + Width · k / n
                 halfWidthAmount = nửa bề rộng vạch / Width
Show(t)        : fill.FillAmount = t
                 vạch k bật ⇔ t ≥ k / n + halfWidthAmount
```

Caller (thanh đếm còn/tổng) gọi `SetSegments(tổng)` khi tổng đổi, `Show(còn / tổng)` mỗi lần số đổi và mỗi frame tween. `còn/tổng` là bội
của `1/n` nên đầu cắt dừng ở `Left + Width · còn / n` — tâm vạch `còn`.

File mới `Runtime/Utilities/Sprites/SpriteFillDividers.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Horcrux.Runtime.Utilities
{
    /// <summary>
    /// Vạch chia trên ruột SpriteHorizontalFill: chia ruột thành n đoạn bằng nhau, vạch k ở k/n bề rộng. Tỉ lệ dừng ở bội của 1/n thì
    /// đầu cắt nằm đúng tâm một vạch; vạch chỉ hiện khi đầu cắt đã qua hết bề rộng nó, nên vạch ở đầu cắt và trên phần rỗng ẩn.
    /// </summary>
    public class SpriteFillDividers : MonoBehaviour
    {
        #region Properties

        [SerializeField] private SpriteHorizontalFill fill;
        [Tooltip("Vạch đầu tiên, con của renderer ruột. Vạch thứ hai trở đi chép từ nó khi số đoạn cần thêm; x do code đặt, y giữ theo prefab.")]
        [SerializeField] private SpriteRenderer dividerTemplate;

        private readonly List<SpriteRenderer> _dividers = new();
        private float _halfWidthAmount;
        private int _segments;

        #endregion Properties

        #region Unity Callbacks

        private void Awake() => _dividers.Add(dividerTemplate);

        #endregion Unity Callbacks

        #region API

        /// <summary>Chia ruột thành segments đoạn: segments − 1 vạch, chép thêm từ vạch mẫu khi thiếu. Gọi lại khi số đoạn đổi, rồi gọi Show.</summary>
        public void SetSegments(int segments)
        {
            _segments = Mathf.Max(1, segments);
            var dividerCount = _segments - 1;
            while (_dividers.Count < dividerCount)
                _dividers.Add(Instantiate(dividerTemplate, dividerTemplate.transform.parent));

            var left = fill.Left;
            var width = fill.Width;
            // Nửa bề rộng vạch theo tỉ lệ ruột: đầu cắt phải qua thêm chừng này mới phủ hết vạch.
            _halfWidthAmount = dividerTemplate.sprite.bounds.extents.x * dividerTemplate.transform.localScale.x / width;
            for (var i = 0; i < dividerCount; i++)
            {
                var divider = _dividers[i].transform;
                var position = divider.localPosition;
                position.x = left + width * (i + 1) / _segments;
                divider.localPosition = position;
            }
        }

        /// <summary>Ghi tỉ lệ lên ruột và bật vạch nằm trọn trên phần ruột. Gọi mỗi lần tỉ lệ đổi, kể cả mỗi frame tween.</summary>
        public void Show(float amount)
        {
            fill.FillAmount = amount;
            var dividerCount = _segments - 1;
            for (var i = 0; i < _dividers.Count; i++)
                _dividers[i].enabled = i < dividerCount && amount >= (float)(i + 1) / _segments + _halfWidthAmount;
        }

        #endregion API
    }
}
```

- `Show` ghi luôn `FillAmount`: đầu cắt và vạch là một trạng thái, ghi hai chỗ thì caller có thể quên một.
- Ngưỡng `k/n + halfWidthAmount`, không `k/n`: lúc ruột co, vạch tắt ngay khi đầu cắt chạm mép phải vạch, không đợi tới tâm — vạch
  không bao giờ đứng nửa trên ruột nửa trên phần rỗng. Ở `t = k/n` đúng, vạch k tắt: đầu cắt phẳng là ranh giới, vạch ở đó thừa.
- `halfWidthAmount` tính một lần ở `SetSegments` theo scale local của vạch mẫu; scale vạch đổi lúc chạy thì gọi lại `SetSegments`.
- Vạch thừa khi n giảm chỉ tắt, không huỷ: pool lấy lại thanh với tổng khác không sinh rác.
- `Awake` thêm vạch mẫu vào danh sách: vạch mẫu nằm sẵn trong prefab, là vạch 1.

**Editor setup** (phía caller): một vạch con của renderer ruột, `sortingOrder` trên ruột · thêm component lên object của ruột, kéo
`SpriteHorizontalFill` vào ô **Fill**, vạch vào ô **Divider Template**. Thiếu ô: `NullReferenceException` ở `Awake` hay lần
`SetSegments` đầu.

**Kiểm:** compile sạch; test PlayMode cùng file test GĐ1, ruột sprite 100 px (Width 1, Left −0.5), vạch 2 px:

| Input | Kỳ vọng |
|---|---|
| `SetSegments(3)` | 2 vạch, x = −0.1667, 0.1667 |
| `SetSegments(3)`, `Show(2/3f)` | vạch 1 bật, vạch 2 tắt |
| `SetSegments(3)`, `Show(1/3f + 0.005f)` | vạch 1 tắt (chưa qua nửa bề rộng 0.01) |
| `SetSegments(5)` rồi `SetSegments(2)`, `Show(1)` | vạch 1 bật, vạch 2…4 tắt; tổng 4 renderer |
| `SetSegments(1)`, `Show(1)` | không vạch nào bật |

**Mốc commit:** compile sạch + bảng case xanh → commit trong `Assets/Horcrux`.

## Giai đoạn 3 — dự án chuyển sang bản Horcrux (agent)

**Mục tiêu:** dự án dùng bản Horcrux, bản tạm biến mất. **Vì sao sau GĐ2:** cần bản Horcrux compile được và test xanh trước.

- `.meta` của `SpriteHorizontalFill.cs`, `SpriteFillDividers.cs` và shader bản Horcrux **nhận GUID của bản tạm** (prefab trỏ script, material trỏ shader theo GUID):
  xoá bản tạm cùng `.meta` rồi ghi GUID cũ vào `.meta` bản Horcrux, cùng một lần.
- Đổi `using` sang `Horcrux.Runtime.Utilities` ở caller (thanh đếm lớp của kệ).
- Cập nhật tài liệu dự án nhắc bản tạm (bảng "chờ Horcrux", chỗ ghi đường dẫn bản tạm).

**Kiểm:** grep tên type kèm namespace cũ ra 0; compile sạch; prefab dùng nó mở ra không có script missing, material không hồng.
**Mốc commit:** một commit ở dự án kèm con trỏ submodule.

## Giai đoạn 4 — Tài liệu hệ `docs/Runtime/SpriteHorizontalFill/SpriteHorizontalFill.md` và `.html` (agent)

`.md` mở đầu `Code: Runtime/Utilities/Sprites/, Shader/`. Mục: luồng ASCII (luồng mô tả ở trên) · bảng API (`FillAmount`, `Left`, `Width`, `SetSegments`, `Show`) ·
"Design decisions" gom Ngữ cảnh đã chốt (shader không mask, UV không object space, MPB không batch, không tween) · "Trước khi chạy" = Editor
setup GĐ1, GĐ2 · "Chọn với `SlicedFill`" — bảng một dòng mỗi bên: đầu ruột phẳng hay bo, art trong lòng giữ hay co, giá (material riêng +
mất batch, hay không). `.html` theo `docs` › `.html`: Kịch bản (đầy, nửa, rỗng, thanh chia vạch) · Cách dùng · Flow · Lưu ý (atlas xoay,
thiếu material, mất batch, bảng chọn với `SlicedFill`).

Xong thì **xoá Plan này** (`Horcrux_SpriteHorizontalFill_Plan.md` cùng `.meta`).

**Mốc commit:** `docs(sprite-fill): SpriteHorizontalFill.md, SpriteHorizontalFill.html`, xoá Plan.

## Use case

Thanh chia vạch dưới vật thể (số lớp, số lượt) · thanh tiến độ world có art đổ bóng trong lòng không được co · thanh máu art nhiều lớp cắt
theo phần trăm.

## File → giai đoạn

| File | GĐ |
|---|---|
| `Shader/sprite_horizontal_fill.shader`, `Runtime/Utilities/Sprites/SpriteHorizontalFill.cs` | 1 |
| `Runtime/Utilities/Sprites/SpriteFillDividers.cs` | 2 |
| Test PlayMode `SpriteHorizontalFillTests` | 1, 2 |
| Caller, bản tạm, tài liệu của dự án | 3 |
| `docs/Runtime/SpriteHorizontalFill/SpriteHorizontalFill.md`, `.html` (agent) | 4 |
