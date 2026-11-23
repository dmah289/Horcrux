# Plan — `SpriteFill` (tên chốt khi gõ): ruột thanh tiến độ vẽ bằng `SpriteRenderer`

Bản tạm: category-jam `Assets/_CategoryJam/Scripts/Utilities/` — A `SlicedFill.cs` (thanh tiến độ trên box; test `Tests/EditMode/SlicedFillTests.cs`) · B `SpriteHorizontalFill.cs`, `SpriteFillDividers.cs`, `sprite_horizontal_fill.shader` (thanh đếm lớp có vạch trên kệ). Hai cơ chế độc lập cho cùng việc, ứng `SetWidth` / `SetFill` của `ProgressBar` (UI)
**Phụ thuộc:** không — gõ lúc nào cũng được. Chung `Runtime/AssemblyInfo.cs` với [ConveyorLoop](Horcrux_ConveyorLoop_Plan.md) GĐ1: Plan gõ trước tạo file, Plan sau thấy dòng đã có thì bỏ qua.
Kiểm: `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,Horcrux.EditModeTests.csproj,Horcrux.PlayModeTests.csproj --tests Horcrux.EditModeTests`; PlayMode chạy ở Window › General › Test Runner.

## Cơ chế A — `SlicedFill`: ruột Sliced đổi bề rộng, hai đầu bo luôn còn

### Kịch bản

| Tình huống | Hệ làm gì | Caller thấy |
|---|---|---|
| số đổi `(curr, total)`, caller `Apply(fill, FillWidth(curr, total, full), min)` | ghi `size.x`, mép trái đứng yên | ruột dài ra, co lại; hai đầu bo nguyên cỡ, phần giữa giãn |
| bề rộng dương nhưng hẹp hơn hai đầu bo | nâng lên `MinWidth` | đoạn ngắn đủ hai đầu bo, không chồng |
| `curr` = 0 | tắt renderer, giữ `size` | ruột ẩn; lần hiện lại chỉ ghi `x` |
| `curr` > `total` · `curr` < 0 | `Clamp01` | đầy · ẩn |
| `total` ≤ 0 | `FillWidth` trả 0 | ẩn, không chia 0 |
| tween | caller `Tween.Custom(from, to, …, w => SlicedFill.Apply(fill, w, min))` | ruột trượt mỗi frame |
| caller đọc lại `fill.size.x` làm bề rộng đầy sau lần `Apply` đầu | `Apply` đã ghi đè `size.x` | thanh co dần — chụp `full` một lần ở `Awake` |
| sprite ruột đổi lúc chạy | — | caller gọi lại `MinWidth` |
| nhiều thanh cùng sprite | không material riêng | batch như sprite thường |

### Flow

```
caller (thanh tiến độ)
  │ Awake, một lần: full = fill.size.x ; min = SlicedFill.MinWidth(fill.sprite)
  │ dữ liệu đổi (curr, total), hoặc mỗi frame Tween.Custom
  ├─ SlicedFill.FillWidth(curr, total, full) ──► width               Runtime/Utilities/Sprites/SlicedFill.cs
  └─ SlicedFill.Apply(fill, width, min)
        └─ VisibleWidth (internal): 0 → ẩn ; (0, min) → min ; ≥ min → giữ
              ──size.x, enabled──► SpriteRenderer ruột (Sliced, pivot trái)
test ──VisibleWidth──► SlicedFill                                     Runtime/AssemblyInfo.cs (InternalsVisibleTo)
```

---

### GĐ1 — xong thì thanh Sliced vẽ đúng ở mọi tỉ lệ, kể cả 0 và rất nhỏ

```
caller ──FillWidth+ · MinWidth+ · Apply+──► SlicedFill+ ──size.x, enabled──► SpriteRenderer (Sliced)
test ──VisibleWidth+ (internal)──► SlicedFill
```

`Runtime/AssemblyInfo.cs` — file mới (bỏ qua nếu đã có dòng này):

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Horcrux.EditModeTests")]
```

`Runtime/Utilities/Sprites/SlicedFill.cs` — file mới:

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Sprites
{
    /// <summary>
    /// Ruột thanh SpriteRenderer Sliced, sprite pivot trái: đổi size.x thì mép trái đứng yên, hai đầu bo không méo.
    /// </summary>
    public static class SlicedFill
    {
        #region API

        /// <summary>Bề rộng ruột cho curr trên total; total ≤ 0 thì rỗng.</summary>
        public static float FillWidth(int curr, int total, float fullWidth)
            => total <= 0 ? 0f : fullWidth * Mathf.Clamp01((float)curr / total);

        /// <summary>Bề rộng vẽ được: 0 thì ẩn; dương thì không hẹp hơn hai đầu bo của Sliced.</summary>
        internal static float VisibleWidth(float width, float minWidth)
            => width <= 0f ? 0f : Mathf.Max(width, minWidth);

        /// <summary>Hai đầu bo của sprite, theo unit: hẹp hơn thì Sliced chồng hai đầu lên nhau.</summary>
        public static float MinWidth(Sprite sprite)
        {
            var border = sprite.border;
            return (border.x + border.z) / sprite.pixelsPerUnit;
        }

        /// <summary>Ghi bề rộng vẽ được lên ruột; 0 thì tắt renderer, giữ nguyên size.</summary>
        public static void Apply(SpriteRenderer fill, float width, float minWidth)
        {
            var visible = VisibleWidth(width, minWidth);
            fill.enabled = visible > 0f;
            // Ghi size, không localScale: Sliced giữ hai đầu bo nguyên cỡ, scale làm bo méo.
            if (visible > 0f)
                fill.size = new Vector2(visible, fill.size.y);
        }

        #endregion API
    }
}
```

Editor setup phía caller:

| # | Bước | Thiếu thì |
|---|---|---|
| 1 | `SpriteRenderer` ruột › **Draw Mode** = Sliced | `size` bị bỏ qua, thanh không đổi |
| 2 | sprite có border trái, phải > 0 và pivot **trái** | thiếu pivot trái: ruột co về tâm |
| 3 | `size.x` lúc dựng là bề rộng đầy | caller chụp sai `full` |

`Tests/EditMode/SlicedFillTests.cs` (agent):

| Input | Kỳ vọng |
|---|---|
| `FillWidth(0, 10, 2)` · `FillWidth(5, 10, 2)` · `FillWidth(10, 10, 2)` | 0 · 1 · 2 |
| `FillWidth(15, 10, 2)` · `FillWidth(-3, 10, 2)` | 2 · 0 (chặn biên) |
| `FillWidth(3, 0, 2)` · `FillWidth(3, -1, 2)` | 0 · 0 (không chia 0) |
| `VisibleWidth(0, 0.4)` · `VisibleWidth(-1, 0.4)` | 0 · 0 |
| `VisibleWidth(0.1, 0.4)` · `VisibleWidth(0.4, 0.4)` · `VisibleWidth(1, 0.4)` | 0.4 · 0.4 · 1 |
| `MinWidth` sprite border 18/0/18/0, 100 ppu | 0.36 |
| `Apply` width 0 | `enabled` false, `size` không đổi |
| `Apply` width 1, minWidth 0.4 | `enabled` true, `size.x` 1, `size.y` giữ |
| `Apply` width 0.1, minWidth 0.4 | `size.x` 0.4 |

---

## Cơ chế B — `SpriteHorizontalFill`, `SpriteFillDividers`: cắt phẳng bằng shader, vạch chia

### Kịch bản

| Tình huống | Hệ làm gì | Caller thấy |
|---|---|---|
| `FillAmount = t`, 0 < t ≤ 1 | ghi `_FillU` qua `MaterialPropertyBlock`; shader bỏ texel bên phải | đầu cắt phẳng như `Image.fillAmount`; art trong lòng không co, không kéo |
| `t` = 0 hay âm | kẹp 0, tắt renderer | ruột ẩn; `FillAmount` đọc lại 0 |
| `t` > 1 | kẹp 1 | cả sprite |
| chưa ai gán `FillAmount` | `Awake` áp tỉ lệ mặc định 1 | cả sprite |
| caller gán `FillAmount` hay đọc `Left`, `Width` trước `Awake` của component, hoặc ngoài Play | đo mesh ở lần dùng đầu | số đúng ngay, không cần `Awake` |
| mesh sprite Tight hay Full Rect; sprite là mảnh của atlas | vị trí và UV lấy từ cùng hai đỉnh mép | đầu cắt và vạch khớp nhau |
| thanh đếm: `SetSegments(tổng)` khi tổng đổi, `Show(còn / tổng)` mỗi lần số đổi, mỗi frame tween | đặt n − 1 vạch ở k/n; bật vạch đầu cắt đã qua hết bề rộng | đầu cắt dừng đúng tâm vạch `còn`; vạch ở đầu cắt và trên phần rỗng ẩn |
| lúc co, đầu cắt chạm mép phải vạch | vạch tắt ngay | vạch không bao giờ nửa trên ruột nửa trên phần rỗng |
| số đoạn giảm | vạch thừa chỉ tắt | pool lấy lại thanh với tổng khác không sinh rác |
| lúc chạy, thiếu vạch | chép thêm từ vạch đầu | đủ vạch |
| ngoài Play, thiếu vạch | `LogError`, đặt số vạch đang có | Console đỏ; scene không thêm object |
| `SetSegments(1)` | không vạch | chỉ ruột |
| scale vạch đổi lúc chạy | — | caller gọi lại `SetSegments` |
| tool Editor vẽ thanh ngoài Play mỗi lần mở scene | ghi `enabled`, vị trí vạch chỉ khi khác | scene không bẩn; bản xem trước giống game |
| renderer B | material riêng, mỗi renderer một MPB | không batch với renderer khác |
| atlas xoay sprite · `flipX` bật | — | đầu cắt chạy theo trục dọc của art · hiện ở phía phải (setup sai) |

### Flow

```
caller thanh đếm (còn / tổng) · tool Editor vẽ thanh ngoài Play (Awake không chạy)
  │ SetSegments(tổng) khi tổng đổi · Show(còn / tổng) mỗi lần số đổi, mỗi frame tween
  ▼
SpriteFillDividers                                         Runtime/Utilities/Sprites/SpriteFillDividers.cs
  ├─ localPosition.x, enabled ──► vạch (SpriteRenderer con của ruột)
  │ FillAmount · Left · Width
  ▼
SpriteHorizontalFill ◄──FillAmount── caller thanh không vạch   Runtime/Utilities/Sprites/SpriteHorizontalFill.cs
  ├─ EnsureMeasured (lần dùng đầu) ◄── sprite.vertices, sprite.uv
  └─ Apply ──enabled, MPB _FillU──► SpriteRenderer ruột
                                       └─► clip(_FillU − uv.x)    Shader/sprite_horizontal_fill.shader
```

### Thuật toán

```
Đo (lần dùng đầu: FillAmount, Left, Width hay Awake — cái nào tới trước):
    iL, iR        ← chỉ số đỉnh x nhỏ nhất, lớn nhất của sprite.vertices
    Left, Width   ← vertices[iL].x, vertices[iR].x − Left
    uMin, uWidth  ← uv[iL].x, uv[iR].x − uMin        (UV trên texture đang vẽ: trang atlas khi đã pack)
Cắt:   t ← Clamp01(t) ; t = 0 → tắt renderer ; t > 0 → _FillU = uMin + uWidth · t
Shader: clip(_FillU − uv.x)
Vạch:  vạch k (1 … n−1) ở x = Left + Width · k / n
       h = nửa bề rộng vạch / Width
       vạch k bật ⇔ t ≥ k / n + h
```

| Bất biến | Nhờ |
|---|---|
| `t` cùng một nghĩa ở đầu cắt (`u`) và ở `x = Left + t · Width` | vị trí và UV lấy từ cùng hai đỉnh; atlas không xoay nên `u` tuyến tính theo `x` |
| `Show(còn / tổng)` dừng đầu cắt ở tâm vạch `còn` | `còn / tổng` là bội của `1/n` |
| ở `t = k/n` đúng, vạch k tắt | ngưỡng có `+ h`; đầu cắt phẳng là ranh giới, vạch ở đó thừa |

---

### GĐ2 — xong thì thanh không vạch có đầu cắt phẳng đúng ở mọi tỉ lệ, kể cả 0 và 1

```
caller ──FillAmount+──► SpriteHorizontalFill+ ──MPB _FillU+, enabled──► SpriteRenderer ──► sprite_horizontal_fill.shader+ (clip)
caller ──Left+ · Width+──► SpriteHorizontalFill+ (đo lười EnsureMeasured+)
```

`Shader/sprite_horizontal_fill.shader` — file mới:

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

`Runtime/Utilities/Sprites/SpriteHorizontalFill.cs` — file mới:

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Sprites
{
    /// <summary>
    /// Cắt SpriteRenderer theo chiều ngang như Image.fillAmount: giữ phần từ mép trái tới tỉ lệ, đầu cắt phẳng, sprite không co.
    /// Chạy cả ngoài Play: đo mesh ở lần dùng đầu, không chờ Awake.
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

        /// <summary>Bề rộng mesh sprite theo đơn vị local của renderer: tỉ lệ t nằm ở x = Left + t * Width.</summary>
        public float Width
        {
            get
            {
                EnsureMeasured();
                return _width;
            }
        }

        /// <summary>Mép trái mesh sprite theo đơn vị local của renderer.</summary>
        public float Left
        {
            get
            {
                EnsureMeasured();
                return _left;
            }
        }

        #endregion Properties

        #region Unity Callbacks

        private void Awake() => Apply();

        #endregion Unity Callbacks

        #region Class Methods

        /// <summary>
        /// Đo một lần ở lần dùng đầu, không ở Awake: ngoài Play Awake không chạy, và caller gán FillAmount trước Awake của
        /// component này (hai component cùng prefab) vẫn nhận số đúng.
        /// </summary>
        private void EnsureMeasured()
        {
            // _block là cờ đã đo: mesh sprite không đổi suốt đời renderer; vertices, uv cấp phát mảng mỗi lần đọc.
            if (_block != null) return;

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
        }

        private void Apply()
        {
            EnsureMeasured();

            // Chỉ ghi khi khác: ngoài Play tool vẽ lại theo dữ liệu mỗi lần mở scene, ghi trùng làm scene bẩn.
            var visible = _fillAmount > 0f;
            if (fillRenderer.enabled != visible) fillRenderer.enabled = visible;
            if (!visible) return;

            // Đọc trước khi ghi: giữ giá trị khác caller đã đặt trên cùng renderer.
            fillRenderer.GetPropertyBlock(_block);
            _block.SetFloat(s_fillUId, _uMin + _uWidth * _fillAmount);
            fillRenderer.SetPropertyBlock(_block);
        }

        #endregion Class Methods
    }
}
```

Editor setup phía caller:

| # | Bước | Thiếu thì |
|---|---|---|
| 1 | material mới dùng shader `Horcrux/sprite_horizontal_fill`, gán vào renderer ruột | không cắt, ruột luôn đầy |
| 2 | renderer ruột › **Draw Mode** Simple, **Flip X** tắt | `flipX` bật: đầu cắt hiện ở phía phải |
| 3 | thêm `SpriteHorizontalFill`, kéo renderer ruột vào ô **Fill Renderer** | `NullReferenceException` ở lần dùng đầu |
| 4 | atlas chứa sprite tắt **Allow Rotation** | đầu cắt chạy theo trục dọc của art |

`Tests/EditMode/SpriteHorizontalFillTests.cs` (agent) — sprite `Sprite.Create` trên texture 100×10, renderer có material shader trên, đọc `_FillU` qua `GetPropertyBlock`; hàng PlayMode ở `Tests/PlayMode/SpriteHorizontalFillPlayTests.cs` (agent):

| Input | Kỳ vọng |
|---|---|
| PlayMode: chưa gán `FillAmount`, bật object để `Awake` chạy | `_FillU` của MPB = uMax; renderer bật |
| `FillAmount = 0.5` | `_FillU` = uMin + 0.5 × (uMax − uMin) |
| `FillAmount = 0` · `-1` | renderer tắt · `FillAmount` đọc lại 0 |
| `FillAmount = 2` | đọc lại 1, `_FillU` = uMax |
| gán 0.3 ngay sau `AddComponent`, ngoài Play | `_FillU` theo 0.3, không cần `Awake` |
| đọc `Width` trước mọi lần gán | đã đo: số đúng, không 0 |
| sprite là mảnh con của texture (rect lệch trái) | uMin > 0; `_FillU` nội suy trong khoảng UV của mảnh |
| `Left`, `Width` sprite 100 px, 100 ppu, pivot giữa | −0.5, 1 |
| sprite pivot trái (`Sprite.Create` pivot (0, 0.5)), `FillAmount = 0.25` | `Left` 0; `_FillU` = uMin + 0.25 × uWidth |

---

### GĐ3 — xong thì thanh chia n đoạn có vạch chạy được, cả trong tool Editor ngoài Play

```
caller ──SetSegments+(n) · Show+(t)──► SpriteFillDividers+ ──FillAmount · Left · Width──► SpriteHorizontalFill
                                                └─ localPosition.x, enabled+──► vạch (SpriteRenderer con của ruột)
```

`Runtime/Utilities/Sprites/SpriteFillDividers.cs` — file mới:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Sprites
{
    /// <summary>
    /// Vạch chia trên ruột SpriteHorizontalFill: chia ruột thành n đoạn bằng nhau, vạch k ở k/n bề rộng. Tỉ lệ dừng ở bội của 1/n thì
    /// đầu cắt nằm đúng tâm một vạch; vạch chỉ hiện khi đầu cắt đã qua hết bề rộng nó, nên vạch ở đầu cắt và trên phần rỗng ẩn.
    /// Chạy cả ngoài Play nhưng không chép vạch: prefab dùng ngoài Play đặt sẵn đủ vạch.
    /// </summary>
    public class SpriteFillDividers : MonoBehaviour
    {
        #region Properties

        [SerializeField] private SpriteHorizontalFill fill;
        [Tooltip("Vạch đặt sẵn, con của renderer ruột; vạch đầu là mẫu để chép thêm lúc chạy. x do code đặt, y giữ theo prefab.")]
        [SerializeField] private List<SpriteRenderer> dividers = new();

        private float _halfWidthAmount;
        private int _segments;

        #endregion Properties

        #region API

        /// <summary>Chia ruột thành segments đoạn: segments − 1 vạch, lúc chạy chép thêm từ vạch đầu khi thiếu. Gọi lại khi số đoạn đổi, rồi gọi Show.</summary>
        public void SetSegments(int segments)
        {
            _segments = Mathf.Max(1, segments);
            var dividerCount = _segments - 1;
            var template = dividers[0];
            while (dividers.Count < dividerCount)
            {
                // Ngoài Play vạch chép được lưu vào scene đang mở.
                if (!Application.isPlaying)
                {
                    Debug.LogError($"[SpriteFillDividers]: cần {dividerCount} vạch, prefab đặt sẵn {dividers.Count}; ngoài Play không chép thêm.", this);
                    break;
                }
                dividers.Add(Instantiate(template, template.transform.parent));
            }

            var left = fill.Left;
            var width = fill.Width;
            // Nửa bề rộng vạch theo tỉ lệ ruột: đầu cắt phải qua thêm chừng này mới phủ hết vạch.
            _halfWidthAmount = template.sprite.bounds.extents.x * template.transform.localScale.x / width;
            var placedCount = Mathf.Min(dividerCount, dividers.Count);
            for (var i = 0; i < placedCount; i++)
            {
                var divider = dividers[i].transform;
                var position = divider.localPosition;
                position.x = left + width * (i + 1) / _segments;
                // Chỉ ghi khi khác: ngoài Play ghi trùng làm scene bẩn.
                if (divider.localPosition != position) divider.localPosition = position;
            }
        }

        /// <summary>Ghi tỉ lệ lên ruột và bật vạch nằm trọn trên phần ruột. Gọi mỗi lần tỉ lệ đổi, kể cả mỗi frame tween.</summary>
        public void Show(float amount)
        {
            // Đầu cắt và vạch là một trạng thái: ghi chung thì caller không quên được một bên.
            fill.FillAmount = amount;
            var dividerCount = _segments - 1;
            for (var i = 0; i < dividers.Count; i++)
            {
                // Vạch thừa khi số đoạn giảm chỉ tắt, không huỷ: pool lấy lại thanh với tổng khác không sinh rác.
                var shown = i < dividerCount && amount >= (float)(i + 1) / _segments + _halfWidthAmount;
                if (dividers[i].enabled != shown) dividers[i].enabled = shown;
            }
        }

        #endregion API
    }
}
```

Editor setup phía caller:

| # | Bước | Thiếu thì |
|---|---|---|
| 1 | một vạch con của renderer ruột, `sortingOrder` trên ruột | — |
| 2 | thêm `SpriteFillDividers` lên object của ruột, kéo `SpriteHorizontalFill` vào ô **Fill**, vạch vào phần tử đầu của ô **Dividers** | `NullReferenceException` ở lần `SetSegments` đầu |
| 3 | prefab vẽ ngoài Play: thêm sẵn vạch vào ô **Dividers** tới số đoạn lớn nhất trừ một | `LogError`, thanh thiếu vạch |

Case thêm vào `SpriteHorizontalFillTests.cs` (agent), hàng chép vạch ở file PlayMode; ruột sprite 100 px (`Width` 1, `Left` −0.5), vạch 2 px:

| Input | Kỳ vọng |
|---|---|
| `SetSegments(3)` | 2 vạch, x = −0.1667, 0.1667 |
| `SetSegments(3)`, `Show(2/3f)` | vạch 1 bật, vạch 2 tắt |
| `SetSegments(3)`, `Show(1/3f + 0.005f)` | vạch 1 tắt (chưa qua nửa bề rộng 0.01) — đỏ khi ngưỡng bỏ `halfWidthAmount` |
| PlayMode, ô một vạch: `SetSegments(5)` rồi `SetSegments(2)`, `Show(1)` | vạch 1 bật, vạch 2…4 tắt; tổng 4 renderer |
| ngoài Play, ô một vạch: `SetSegments(3)` | `LogError` một lần, không thêm renderer, vạch 1 ở x = −0.1667 |
| `SetSegments(1)`, `Show(1)` | không vạch nào bật |

---

## Chuyển dự án (agent)

| Chỗ | Đổi |
|---|---|
| thứ tự với Plan khác | category-jam `CategoryBox.cs` chung với Chuyển dự án của [UniTextSetFraction](Horcrux_UniTextSetFraction_Plan.md) — gõ cả hai thì chuyển chung một lần |
| `docs/Runtime/SpriteFill/SpriteFill.md`, `.html` | tạo, một tài liệu cho hai cơ chế: `Code: Runtime/Utilities/Sprites/, Shader/sprite_horizontal_fill.shader` · bảng chọn A / B (ứng `ProgressBar.SetWidth` / `SetFill`, đầu bo / phẳng, art trong lòng giãn / giữ, vạch, giá batch, setup) · Flow A, B · API · quyết định theo comment và Không làm · "Trước khi chạy" = Editor setup GĐ1–3 · `.html`: Kịch bản (đầy, nửa, rất nhỏ, rỗng; thanh chia vạch), Cách dùng, Flow, Lưu ý (thiếu pivot trái, thiếu Sliced, atlas xoay, `flipX`, thiếu material, mất batch, sprite đổi lúc chạy); tên trung tính |
| `Scripts/Utilities/SlicedFill.cs` + `.meta` | xoá (class static, không asset nào trỏ GUID) |
| `Tests/EditMode/SlicedFillTests.cs` + `.meta` | xoá — 11 test đã ở bảng GĐ1 |
| Unity, trước ba dòng `.meta` | đóng Prefab Mode nếu đang mở `SingleShelf.prefab`, `_GameDesign/Prefabs/design_shelf_single.prefab` (Prefab Mode tự lưu đè file) · đóng Unity (sửa `.meta` khi Editor mở thì Unity ghi đè) |
| `Assets/Horcrux/Runtime/Utilities/Sprites/SpriteHorizontalFill.cs.meta` | `guid:` ← GUID trong `.meta` bản tạm `SpriteHorizontalFill.cs` (hiện `988795041c6f7d64aaa65b34ef9b7ed6`; đọc lại từ file) — hai prefab trên trỏ script theo GUID |
| `Assets/Horcrux/Runtime/Utilities/Sprites/SpriteFillDividers.cs.meta` | `guid:` ← bản tạm (hiện `86ce7e486a3758e439a70d8c44ec2472`) |
| `Assets/Horcrux/Shader/sprite_horizontal_fill.shader.meta` | `guid:` ← bản tạm (hiện `cc94eb9ad979a9d45912ac378507d3c2`) — `Arts/Material/sprite_horizontal_fill.mat` trỏ shader theo GUID |
| bản tạm `SpriteHorizontalFill.cs`, `SpriteFillDividers.cs`, `sprite_horizontal_fill.shader` + `.meta` | xoá, cùng lần với ba dòng `.meta` trên |
| Unity, sau ba dòng `.meta` | mở, chờ import · `SingleShelf.prefab` › `AmountDisplay`: `Sprite Horizontal Fill`, `Sprite Fill Dividers` còn đủ ô; material ruột không hồng |
| `Scripts/Entities/Box/CategoryBox.cs` | thêm `using Horcrux.Runtime.Utilities.Sprites;`; giữ `using CategoryJam.Gameplay.Utilities;` nếu file còn type khác của namespace đó, không thì bỏ |
| `Scripts/Entities/Shelf/SingleShelfAmountBar.cs`, `_GameDesign/Script/LevelEditor/Design/DesignShelfView.cs` | như trên (`CategoryJam.GameDesign.asmdef` đã tham chiếu `com.horcrux.runtime`) |
| `Scripts/Entities/Entities.md` (cây `AmountDisplay`, đoạn `_FillU`, `progress_fill`), `Structure.md` (cây `Utilities`, bảng bản tạm chờ Horcrux), `Scripts/Feel.md` (Máy kiểm `SlicedFillTests`), `_GameDesign/Script/LevelEditor/LevelEditor.md` (thanh `amount_bar`) | trỏ bản Horcrux và `docs/Runtime/SpriteFill/`, bỏ dòng bản tạm và đường dẫn Plan |
| kiểm | `verify.sh --csproj CategoryJam.Gameplay.csproj,CategoryJam.GameDesign.csproj,CategoryJam.EditModeTests.csproj --tests CategoryJam.EditModeTests,Horcrux.EditModeTests` xanh · `grep -rln "SlicedFill\|SpriteHorizontalFill\|SpriteFillDividers" Assets/_CategoryJam Assets/_GameDesign --include=*.cs` chỉ ra `CategoryBox.cs`, `SingleShelfAmountBar.cs`, `DesignShelfView.cs` · `grep -rl 988795041c6f7d64aaa65b34ef9b7ed6 Assets` chỉ còn `.meta` Horcrux và hai prefab · Console không "Missing script" |
| Plan này | xoá cùng `.meta` |
| chơi thử | màn có box và kệ Single theo bảng Kịch bản A, B (đặt tile vào box, nhặt tile khỏi kệ Single); tool thiết kế level xem kệ Single vẽ như trong game; hỏng: ruột hồng (mất shader), ruột luôn đầy (mất material), thanh không đổi, vạch lệch khỏi đầu cắt, "Missing script" trên prefab |

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | tween, chữ số, khung trong hệ | caller tự `Tween.Custom` rồi gọi `Apply` / `Show` mỗi frame; hệ chỉ vẽ ruột theo tỉ lệ |
| bỏ | A đổi `localScale.x` | scale làm méo hai đầu bo; Sliced giữ hai đầu nguyên cỡ chỉ khi đổi `size` |
| bỏ | `VisibleWidth` `public` như bản tạm | caller thật không gọi; `Apply` và test gọi |
| bỏ | B cắt bằng `SpriteMask` | mask ghi stencil chung: renderer `VisibleInsideMask` hiện ở mọi vùng mask khác cùng dải và mất `VisibleOutsideMask` của bàn chơi |
| bỏ | B cắt theo toạ độ object | sprite batching gộp đỉnh sang world nên object space không tin được; giá là atlas tắt xoay |
| bỏ | vị trí lấy từ `sprite.bounds` | chỉ trùng UV khi mesh phủ trọn rect; mesh Tight trên art có lề trong suốt làm vạch lệch khỏi đầu cắt |
| bỏ | `Left`, `Width` theo `bounds` của renderer | world, gồm scale cha; con của renderer đặt theo tỉ lệ sai khi cha scale |
| bỏ | B đo mesh ở `Awake` | ngoài Play `Awake` không chạy; caller cùng prefab gán `FillAmount` trước `Awake` vì thứ tự `Awake` không định |
| bỏ | B chỉ chạy trong Play | tool Editor vẽ thanh bằng chính hai component để bản xem trước giống game mà không chép công thức |
| bỏ | ngưỡng vạch `k/n` | lúc co, vạch đứng nửa trên ruột nửa trên phần rỗng tới khi đầu cắt chạm tâm |
| bỏ | ngoài Play chép vạch thiếu | `Instantiate` ngoài Play lưu vạch vào scene đang mở; prefab dùng ngoài Play đặt sẵn đủ vạch |
| bỏ | đặt tên shader Horcrux khác bản tạm | material trỏ GUID, hai bản thân giống từng dòng nên `Shader.Find` lấy bản nào cũng như nhau tới khi Chuyển dự án xoá bản tạm |
| sau | `SlicedFill.VisibleWidth` thành `public` | khi caller cần số mà không ghi |
| sau | trục dọc, cắt từ phải, tâm ra hai phía | khi có người dùng |
| sau | `flipX` cho B | `_Flip` của Sprites-Default lật đỉnh còn UV giữ, đầu cắt sang phía phải và vạch lệch vì `Left`, `Width` đọc đỉnh chưa lật; khi có người dùng |
| sau | bản URP của shader B | khi có người dùng; shader hiện cho built-in |
| sau | B đọc lại mesh khi sprite đổi lúc chạy | khi có người dùng |
