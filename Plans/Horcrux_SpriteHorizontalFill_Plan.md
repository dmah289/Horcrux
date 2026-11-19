# Plan — `SpriteHorizontalFill` lên Horcrux

Nguồn: bản tạm của một dự án game, cắt ruột thanh đếm lớp trên kệ (`SpriteRenderer` world, có vạch chia). Một shader sprite cắt theo
trục ngang và một component ghi tỉ lệ cắt cho từng renderer — `Image.fillAmount` cho `SpriteRenderer`.

**Phân công:** GĐ1 developer gõ, agent viết test theo bảng case rồi compile · GĐ2 (dự án chuyển sang bản Horcrux) và GĐ3 (tài liệu hệ,
xoá Plan) agent làm. GĐ1, GĐ3 commit trong `Assets/Horcrux`, GĐ2 commit ở dự án; mỗi lần đều kèm con trỏ submodule. Không phụ thuộc
Plan nào, không có `§0`: phép toán một dòng nằm ở GĐ1.

## Ngữ cảnh đã chốt

- **Người dùng:** thanh vẽ bằng `SpriteRenderer` mà đầu ruột phải **phẳng** (thanh chia vạch, đầu cắt chạm đúng tâm vạch), ruột giữ
  nguyên hình art — không co, không kéo.
- **Chỗ đặt, tên:** `Runtime/Utilities/Sprites/SpriteHorizontalFill.cs`, namespace `Horcrux.Runtime.Utilities`; shader
  `Shader/sprite_horizontal_fill.shader`, tên `Horcrux/sprite_horizontal_fill`. Không phụ thuộc hệ nào.
- **Khác `SlicedFill`:** `SlicedFill` đổi `size` của Sliced nên hai đầu bo luôn còn; ở đây đầu phải cắt phẳng, nên không đổi hình học
  mà bỏ texel qua `clip` trong shader. Hai utility cùng sống, mỗi cái một kiểu thanh.
- **Vì sao shader, không `SpriteMask`:** mask ghi stencil chung; renderer `VisibleInsideMask` hiện ở mọi vùng mask khác cùng dải, và mất
  `VisibleOutsideMask` của bàn chơi. Shader cắt riêng từng renderer, mask interaction của renderer giữ nguyên (đã kiểm: mask không dải
  che ruột như che sprite Sprites-Default).
- **Vì sao UV, không toạ độ object:** sprite batching gộp đỉnh sang world nên object space không tin được; UV luôn đúng. Giá: atlas chứa
  sprite phải **tắt xoay** (`enableRotation: 0`) để trục u là trục ngang; `spriteMeshType` Full Rect.
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
Awake     : uMin, uWidth ← min/max x của sprite.uv     (UV trong texture đang vẽ: trang atlas khi đã pack)
            Apply()                                    (giá trị gán trước Awake được áp ở đây)
FillAmount = t :
            t ← Clamp01(t)
            t = 0 → renderer.enabled = false
            t > 0 → enabled = true ; MPB._FillU = uMin + uWidth * t
Shader    : clip(_FillU − uv.x)  → texel bên phải đầu cắt bị bỏ, đầu cắt phẳng
Caller    : vị trí đầu cắt theo local renderer = Left + t * Width (đặt vạch, hiệu ứng ở đầu cắt)
```

## Bản đồ hệ cuối

| Thành phần | Ai gọi | Việc |
|---|---|---|
| `sprite_horizontal_fill.shader` | material trên renderer | Sprites-Default + `clip` theo `_FillU` |
| `SpriteHorizontalFill.FillAmount` | caller mỗi lần dữ liệu đổi, mỗi frame tween | ghi `_FillU`, bật/tắt renderer |
| `SpriteHorizontalFill.Left`, `Width` | caller khi đặt thứ bám đầu cắt | mép trái và bề rộng sprite theo local renderer |

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

        /// <summary>Bề rộng sprite theo đơn vị local của renderer: tỉ lệ t nằm ở x = Left + t * Width.</summary>
        public float Width => fillRenderer.sprite.bounds.size.x;

        /// <summary>Mép trái sprite theo đơn vị local của renderer.</summary>
        public float Left => fillRenderer.sprite.bounds.min.x;

        #endregion Properties

        #region Unity Callbacks

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            // sprite.uv là UV trong texture đang vẽ (trang atlas khi đã pack); atlas phải tắt xoay để trục u là trục ngang.
            var uv = fillRenderer.sprite.uv;
            var min = float.MaxValue;
            var max = float.MinValue;
            for (var i = 0; i < uv.Length; i++)
            {
                min = Mathf.Min(min, uv[i].x);
                max = Mathf.Max(max, uv[i].x);
            }
            _uMin = min;
            _uWidth = max - min;
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

- `Awake` đọc `sprite.uv` một lần (cấp phát một mảng): UV của sprite không đổi suốt đời renderer. Sprite đổi lúc chạy thì phải đọc lại —
  chưa có người dùng, nên không có API.
- `if (_block == null) return;` là ca hợp lệ chưa tới lượt: caller gán `FillAmount` trước `Awake` của component (hai component cùng prefab,
  `Awake` không định thứ tự); giá trị được giữ và `Awake` áp.
- `Left`, `Width` đọc `sprite.bounds` theo local renderer, không theo `bounds` của renderer (world, gồm scale cha): caller đặt con của renderer
  theo tỉ lệ thì đúng ở mọi scale.
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

**Mốc commit:** compile sạch + bảng case xanh → commit trong `Assets/Horcrux`.

## Giai đoạn 2 — dự án chuyển sang bản Horcrux (agent)

**Mục tiêu:** dự án dùng bản Horcrux, bản tạm biến mất. **Vì sao sau GĐ1:** cần bản Horcrux compile được và test xanh trước.

- `.meta` của `SpriteHorizontalFill.cs` và shader bản Horcrux **nhận GUID của bản tạm** (prefab trỏ script, material trỏ shader theo GUID):
  xoá bản tạm cùng `.meta` rồi ghi GUID cũ vào `.meta` bản Horcrux, cùng một lần.
- Đổi `using` sang `Horcrux.Runtime.Utilities` ở caller (thanh đếm lớp của kệ).
- Cập nhật tài liệu dự án nhắc bản tạm (bảng "chờ Horcrux", chỗ ghi đường dẫn bản tạm).

**Kiểm:** grep tên type kèm namespace cũ ra 0; compile sạch; prefab dùng nó mở ra không có script missing, material không hồng.
**Mốc commit:** một commit ở dự án kèm con trỏ submodule.

## Giai đoạn 3 — Tài liệu hệ `docs/Runtime/SpriteHorizontalFill/SpriteHorizontalFill.md` và `.html` (agent)

`.md` mở đầu `Code: Runtime/Utilities/Sprites/, Shader/`. Mục: luồng ASCII (luồng mô tả ở trên) · bảng API (`FillAmount`, `Left`, `Width`) ·
"Design decisions" gom Ngữ cảnh đã chốt (shader không mask, UV không object space, MPB không batch, không tween) · "Trước khi chạy" = Editor
setup GĐ1. `.html` theo `docs` › `.html`: Kịch bản (đầy, nửa, rỗng, thanh chia vạch) · Cách dùng · Flow · Lưu ý (atlas xoay, thiếu
material, mất batch).

Xong thì **xoá Plan này** (`Horcrux_SpriteHorizontalFill_Plan.md` cùng `.meta`).

**Mốc commit:** `docs(sprite-fill): SpriteHorizontalFill.md, SpriteHorizontalFill.html`, xoá Plan.

## Use case

Thanh chia vạch dưới vật thể (số lớp, số lượt) · thanh tiến độ world có art đổ bóng trong lòng không được co · thanh máu art nhiều lớp cắt
theo phần trăm.

## File → giai đoạn

| File | GĐ |
|---|---|
| `Shader/sprite_horizontal_fill.shader`, `Runtime/Utilities/Sprites/SpriteHorizontalFill.cs` | 1 |
| Test PlayMode `SpriteHorizontalFillTests` | 1 |
| Caller, bản tạm, tài liệu của dự án | 2 |
| `docs/Runtime/SpriteHorizontalFill/SpriteHorizontalFill.md`, `.html` (agent) | 3 |
