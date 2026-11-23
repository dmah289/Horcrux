# Plan — `SpriteFill` (tên chốt khi gõ): ruột thanh tiến độ vẽ bằng `SpriteRenderer`

Ngày 2026-11-13. Nguồn: category-jam `Assets/_CategoryJam/Scripts/Utilities/` — `SlicedFill.cs` (thanh tiến độ trên box),
`SpriteHorizontalFill.cs`, `SpriteFillDividers.cs`, `sprite_horizontal_fill.shader` (thanh đếm lớp có vạch chia trên kệ), test
`Tests/EditMode/SlicedFillTests.cs`, đã chơi thử. Hai cơ chế cho cùng một việc — ruột thanh theo tỉ lệ ở world — ứng hai chế độ
`SetWidth`/`SetFill` của `ProgressBar` (UI): **A** `SlicedFill` đổi bề rộng `size` của Sliced, hai đầu bo luôn còn; **B**
`SpriteHorizontalFill` cắt texel bằng shader, đầu cắt phẳng, art trong lòng không co, kèm `SpriteFillDividers` đặt vạch chia.

**Phân công:** GĐ1–3 developer gõ, agent viết test theo bảng case rồi compile · GĐ4 (tài liệu) · GĐ5 (dự án chuyển sang bản Horcrux,
xoá bản tạm, xoá Plan) agent làm.
**Chạy được, thấy được:** GĐ1–3 compile sạch + bảng case xanh trong `Tests/EditMode/SlicedFillTests.cs`,
`Tests/EditMode/SpriteHorizontalFillTests.cs` và hai case PlayMode trong `Tests/PlayMode/SpriteHorizontalFillPlayTests.cs`; thấy trong
game ở GĐ5.
**Lệnh kiểm** (chạy từ gốc dự án): `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,Horcrux.EditModeTests.csproj,Horcrux.PlayModeTests.csproj --tests Horcrux.EditModeTests`;
case PlayMode developer chạy trong Window › General › Test Runner › PlayMode.
**Commit:** GĐ1–4 trong `Assets/Horcrux`; GĐ5 ở dự án kèm con trỏ submodule.
**Phụ thuộc:** không. **Chung file:** `Runtime/AssemblyInfo.cs` — `Horcrux_ConveyorLoop_Plan.md` GĐ1 cũng cần dòng
`InternalsVisibleTo("Horcrux.EditModeTests")`; Plan gõ trước tạo file, Plan sau thấy dòng đã có thì bỏ qua · category-jam
`CategoryBox.cs` — GĐ chuyển dự án của `Horcrux_UniTextSetFraction_Plan.md` cũng sửa dòng `using` của file này; gõ cả hai thì gộp
hai GĐ chuyển dự án vào một commit dự án.
**Đã kiểm trước khi ghi:** ba file C# ở namespace mới, theo thứ tự GĐ1 → GĐ1+2 → GĐ1+2+3, cùng `AssemblyInfo.cs`, qua `csc -langversion:9`
trên `UnityEngine.CoreModule` của Editor 6000.0.73f1 và `com.horcrux.runtime.dll`; assembly tên `Horcrux.EditModeTests` gọi được
`SlicedFill.VisibleWidth` (`internal`). Thân hàm trùng nguyên văn ba bản tạm đang chạy trong game; shader trùng từng dòng bản tạm
(diff rỗng), không qua compiler ngoài Unity. Bảng GĐ1 phủ đủ 11 test của `SlicedFillTests`. `§0`: không — mỗi cơ chế một phép toán một
dòng, nằm ở GĐ của nó.

## Ngữ cảnh đã chốt

- **Người dùng:** thanh tiến độ, thanh máu, thanh đếm vẽ bằng `SpriteRenderer` trong world: thanh máu trên đầu quái, thanh tiến độ trên
  vật thể, thanh chia vạch dưới vật thể (số lớp, số lượt), thanh art nhiều lớp đổ bóng trong lòng không được co.
- **Mục tiêu:** caller đưa tỉ lệ (hay `curr`/`total`), ruột vẽ đúng ở mọi tỉ lệ kể cả 0, 1 và rất nhỏ. Chọn cơ chế theo bảng dưới.
- **Ranh giới:** chỉ vẽ ruột theo tỉ lệ; tween, chữ số, khung là của caller.
- **Chỗ đặt, tên:** `Runtime/Utilities/Sprites/` (`SlicedFill.cs`, `SpriteHorizontalFill.cs`, `SpriteFillDividers.cs`), namespace
  `Horcrux.Runtime.Utilities.Sprites`; shader `Shader/sprite_horizontal_fill.shader`, tên `Horcrux/sprite_horizontal_fill`. Không phụ
  thuộc hệ nào. Tên đổi tuỳ ý trước khi gõ.
- **Bảng chọn:**

  | | A `SlicedFill` | B `SpriteHorizontalFill` (+ `SpriteFillDividers`) |
  |---|---|---|
  | ứng chế độ `ProgressBar` (UI) | `SetWidth` | `SetFill` (`Image.fillAmount`) |
  | đầu ruột | hai đầu bo luôn còn | cắt phẳng |
  | art trong lòng | phần giữa Sliced giãn theo bề rộng | giữ nguyên, không co, không kéo |
  | vạch chia | không | có, đầu cắt dừng đúng tâm vạch |
  | giá | không material riêng, batch như sprite thường | material shader riêng, mỗi renderer một `MaterialPropertyBlock`, không batch với renderer khác |
  | setup | Draw Mode Sliced, sprite có border trái/phải, pivot trái | Draw Mode Simple, material shader B, atlas tắt xoay, `flipX` tắt |

- **Cố ý không làm:** tween — caller tự `Tween.Custom` rồi gọi `Apply` / ghi `FillAmount` mỗi frame · trục dọc, cắt từ phải, tâm ra hai
  phía — chưa có người dùng · `flipX` cho B — `_Flip` của Sprites-Default lật đỉnh còn UV giữ nguyên, đầu cắt sang phía phải nhìn thấy và
  `Left`/`Width` đọc đỉnh chưa lật làm vạch lệch; chưa có người dùng · bản URP của shader B — chưa có người dùng (shader cho built-in).
- **Quyết định trái trực giác:**
  - A đổi `size.x`, không đổi `localScale.x`: Sliced giữ hai đầu bo nguyên cỡ khi đổi `size`; đổi scale thì bo méo. Sprite ruột pivot
    trái để mép trái đứng yên.
  - B cắt bằng shader, không `SpriteMask`: mask ghi stencil chung; renderer `VisibleInsideMask` hiện ở mọi vùng mask khác cùng dải, và
    mất `VisibleOutsideMask` của bàn chơi. Shader cắt riêng từng renderer, mask interaction của renderer giữ nguyên.
  - B cắt theo UV, không toạ độ object: sprite batching gộp đỉnh sang world nên object space không tin được. Giá: atlas chứa sprite tắt
    xoay để trục u là trục ngang.
  - B lấy vị trí và UV từ **cùng hai đỉnh** trái nhất, phải nhất của mesh: tỉ lệ `t` cùng một nghĩa ở đầu cắt và ở `Left + t * Width` dù
    mesh Tight hay Full Rect. Lấy vị trí từ `sprite.bounds` thì hai nguồn chỉ trùng khi mesh phủ trọn rect; mesh Tight trên art có lề
    trong suốt làm vạch lệch khỏi đầu cắt.
  - B chạy cả ngoài Play: tool Editor vẽ thanh bằng chính hai component này, để bản xem trước giống game mà không chép công thức. Đo mesh
    ở lần dùng đầu thay vì `Awake` · ghi `enabled` và vị trí vạch chỉ khi khác, vì ghi trùng ngoài Play làm scene bẩn · ngoài Play không
    chép vạch (bản chép lưu vào scene đang mở): prefab dùng ngoài Play đặt sẵn đủ vạch.
- **Mở rộng sau:** `SlicedFill.VisibleWidth` thành `public` khi caller cần số mà không ghi (giờ `internal`, `Apply` và test gọi) · trục
  dọc, cắt từ phải, `flipX`, bản URP khi có người dùng · B đọc lại mesh khi sprite đổi lúc chạy.

## Đã khảo sát

| Nguồn | Lấy | Không lấy, vì sao |
|---|---|---|
| category-jam `Scripts/Utilities/SlicedFill.cs` (bản tạm) | cả bốn hàm nguyên văn | namespace dự án; `VisibleWidth` public — caller thật (`CategoryBox`) không gọi |
| category-jam `Tests/EditMode/SlicedFillTests.cs` | 11 test, đã có trong bảng GĐ1 | — |
| category-jam `Scripts/Utilities/SpriteHorizontalFill.cs`, `SpriteFillDividers.cs`, `sprite_horizontal_fill.shader` (bản tạm) | cả cơ chế nguyên văn | namespace dự án |
| category-jam `CategoryBox.cs`, `Scripts/Entities/Shelf/SingleShelfAmountBar.cs`, `_GameDesign/Script/LevelEditor/Design/DesignShelfView.cs` | cách gọi: chụp bề rộng đầy và `MinWidth` ở `Awake` (A) · `SetSegments(tổng)` rồi `Show(còn/tổng)` mỗi lần số đổi và mỗi frame tween (B) · tool level vẽ thanh ngoài Play (B) | — |
| Horcrux `Runtime/Utilities/UI/ProgressBar.cs` | hai chế độ `SetWidth`/`SetFill` làm trục cho bảng chọn | chỉ cho `RectTransform`/`Image` UI |
| Horcrux `Shader/` | chỗ đặt shader | shader UI và nền, không có shader sprite cắt |

---

## Cơ chế A — `SlicedFill`

### Luồng A

```
Awake của caller : full = fill.size.x ; min = SlicedFill.MinWidth(fill.sprite)        (chụp một lần)
Dữ liệu đổi (curr, total):
         width = SlicedFill.FillWidth(curr, total, full)
         SlicedFill.Apply(fill, width, min)
                 └─ VisibleWidth: 0 → ẩn ; (0, min) → nâng lên min ; ≥ min → giữ
Có tween : Tween.Custom(from, to, ..., w => SlicedFill.Apply(fill, w, min))
```

Chụp `full` ở `Awake`, không đọc lại `fill.size.x` về sau: `Apply` ghi đè `size.x`.

### Bản đồ A

| Thành phần | Ai gọi | Việc |
|---|---|---|
| `SlicedFill.FillWidth` | caller mỗi lần dữ liệu đổi | tỉ lệ curr/total → bề rộng |
| `SlicedFill.MinWidth` | caller một lần ở `Awake` | hai đầu bo của sprite, theo unit |
| `SlicedFill.VisibleWidth` (`internal`) | `Apply`, test | 0 → 0 · dương → không hẹp hơn `MinWidth` |
| `SlicedFill.Apply` | caller (trực tiếp hoặc trong tween) | ghi `size.x`, bật/tắt renderer |

## Giai đoạn 1 — `SlicedFill`

**Mục tiêu:** thanh Sliced chạy đúng ở mọi tỉ lệ, kể cả 0 và rất nhỏ. Xong là thanh tiến độ trên vật thể vẽ được bằng bản Horcrux.

**Vì sao bây giờ:** cơ chế đơn giản nhất, không phụ thuộc gì; là trọn cơ chế A.

**Sơ đồ sau GĐ1** (`+` là phần mới)

```
caller ──FillWidth+ · MinWidth+ · Apply+──► SlicedFill+ ──size.x, enabled──► SpriteRenderer (Sliced)
test ──VisibleWidth+ (internal)──► SlicedFill
```

### `Runtime/AssemblyInfo.cs` — file mới (bỏ qua nếu đã có dòng này)

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Horcrux.EditModeTests")]
```

### `Runtime/Utilities/Sprites/SlicedFill.cs` — file mới

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
            if (visible > 0f)
                fill.size = new Vector2(visible, fill.size.y);
        }

        #endregion API
    }
}
```

**Consumes / Produces:** consumes `SpriteRenderer.size`, `enabled`, `Sprite.border`, `pixelsPerUnit` · produces `FillWidth`, `MinWidth`,
`Apply`; `internal VisibleWidth`.

**Quyết định:**
- `FillWidth`: `Clamp01` chặn curr vượt total và âm; `total ≤ 0` trả 0 thay vì chia cho 0.
- `MinWidth`: `border.x + border.z` là trái + phải theo pixel, chia `pixelsPerUnit` ra unit. Bề rộng dưới số này làm Sliced chồng hai
  đầu bo.
- `Apply` giữ `size.y` và không đụng `size` khi ẩn: lần hiện lại chỉ ghi `x`.
- Caller đọc `sprite.border` qua `MinWidth` một lần; sprite đổi lúc chạy thì gọi lại.

**Editor setup:** không trong Horcrux. Phía caller: `SpriteRenderer.drawMode` = **Sliced**, sprite có border trái/phải > 0 và pivot
**trái**, `size.x` lúc dựng là bề rộng đầy. Thiếu pivot trái: ruột co về tâm. Thiếu Sliced: `size` bị bỏ qua, thanh không đổi.

**Kiểm:** lệnh kiểm ở đầu Plan. Agent viết `Tests/EditMode/SlicedFillTests.cs`:

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

**Mốc commit:** `feat(sprites): SlicedFill — ruột Sliced theo tỉ lệ` (Horcrux).

---

## Cơ chế B — `SpriteHorizontalFill`, `SpriteFillDividers`

### Luồng B

```
Lần dùng đầu (FillAmount, Left, Width hay Awake — cái nào tới trước; ngoài Play Awake không chạy):
            iL, iR ← đỉnh x nhỏ nhất, lớn nhất của sprite.vertices
            Left, Width ← vertices[iL].x, vertices[iR].x − Left
            uMin, uWidth ← uv[iL].x, uv[iR].x − uMin    (UV trong texture đang vẽ: trang atlas khi đã pack)
FillAmount = t :
            t ← Clamp01(t)
            t = 0 → renderer.enabled = false
            t > 0 → enabled = true ; MPB._FillU = uMin + uWidth * t
Shader    : clip(_FillU − uv.x)  → texel bên phải đầu cắt bị bỏ, đầu cắt phẳng
Dividers  : SetSegments(n) đặt vạch k ở Left + Width·k/n ; Show(t) ghi FillAmount và bật vạch đã nằm trọn trên ruột
```

### Bản đồ B

| Thành phần | Ai gọi | Việc |
|---|---|---|
| `sprite_horizontal_fill.shader` | material trên renderer | Sprites-Default + `clip` theo `_FillU` |
| `SpriteHorizontalFill.FillAmount` | `SpriteFillDividers.Show`; caller thanh không vạch | ghi `_FillU`, bật/tắt renderer |
| `SpriteHorizontalFill.Left`, `Width` | `SpriteFillDividers`, caller khác bám đầu cắt | mép trái và bề rộng mesh sprite theo local renderer |
| `SpriteFillDividers.SetSegments` | caller khi số đoạn đổi | đặt n − 1 vạch ở k/n |
| `SpriteFillDividers.Show` | caller mỗi lần số đổi, mỗi frame tween | ghi tỉ lệ, bật vạch trên phần ruột |

## Giai đoạn 2 — shader và `SpriteHorizontalFill`

**Mục tiêu:** một renderer cắt phẳng đúng ở mọi tỉ lệ, kể cả 0 và 1. Xong là thanh không vạch có đầu phẳng vẽ được.

**Vì sao bây giờ:** là lõi của B; `SpriteFillDividers` (GĐ3) đọc `Left`, `Width` và ghi `FillAmount` của nó.

**Sơ đồ sau GĐ2**

```
caller ──FillAmount+──► SpriteHorizontalFill+ ──MPB _FillU+, enabled──► SpriteRenderer ──► sprite_horizontal_fill.shader+ (clip)
caller ──Left+ · Width+──► SpriteHorizontalFill+ (đo lười EnsureMeasured+)
```

### `Shader/sprite_horizontal_fill.shader` — file mới

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
- `_FillU` mặc định 1: renderer chưa có MPB (chưa ai gán tỉ lệ) vẽ cả sprite, vì UV không vượt 1.
- Từ GĐ2 tới GĐ5, dự án có hai shader cùng tên `Horcrux/sprite_horizontal_fill` (bản tạm ở `_CategoryJam/Scripts/Utilities/`), thân
  giống từng dòng: material trỏ shader theo GUID nên không đổi gì; `Shader.Find` trong test lấy bản nào cũng như nhau. GĐ5 xoá bản tạm.

### `Runtime/Utilities/Sprites/SpriteHorizontalFill.cs` — file mới

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

            fillRenderer.GetPropertyBlock(_block);
            _block.SetFloat(s_fillUId, _uMin + _uWidth * _fillAmount);
            fillRenderer.SetPropertyBlock(_block);
        }

        #endregion Class Methods
    }
}
```

**Consumes / Produces:** consumes `Sprite.vertices`, `Sprite.uv`, `MaterialPropertyBlock` · produces shader
`Horcrux/sprite_horizontal_fill`, `SpriteHorizontalFill.FillAmount`, `Left`, `Width`.

**Quyết định:**
- `EnsureMeasured` đọc `sprite.vertices` và `sprite.uv` một lần (mỗi cái cấp phát một mảng): mesh của sprite không đổi suốt đời
  renderer. `_block` khác null là cờ "đã đo".
- Đo lười, không ở `Awake`: tool Editor gọi `Show`, `Left` ngoài Play, nơi `Awake` không chạy · caller gán `FillAmount` trước `Awake`
  của component (hai component cùng prefab, `Awake` không định thứ tự) vẫn được áp ngay. `Awake` vẫn gọi `Apply` để renderer có MPB
  khi chưa ai gán tỉ lệ.
- Hai mảng cùng chỉ số: `uv[i]` là UV của `vertices[i]`. Atlas không xoay thì u tuyến tính theo x, nên hai đỉnh ở hai mép là đủ.
- So `enabled` trước khi ghi: ngoài Play, ghi một giá trị trùng cũng đánh dấu scene bẩn; lúc chạy một phép so không đáng kể. MPB không
  serialize nên không làm scene bẩn.
- `Left`, `Width` theo local renderer (đỉnh của sprite), không theo `bounds` của renderer (world, gồm scale cha): caller đặt con của
  renderer theo tỉ lệ thì đúng ở mọi scale.
- `GetPropertyBlock` trước `SetPropertyBlock`: giữ giá trị khác caller có thể đã đặt trên cùng renderer.

**Editor setup** (phía caller): material mới dùng shader `Horcrux/sprite_horizontal_fill`, gán vào renderer ruột · `drawMode` Simple ·
`flipX` tắt · thêm component, kéo renderer vào ô **Fill Renderer** · atlas chứa sprite tắt **Allow Rotation**. Thiếu material: không
cắt, ruột luôn đầy. Thiếu ô: `NullReferenceException` ở lần dùng đầu. Atlas xoay sprite: đầu cắt chạy theo trục dọc của art. Bật
`flipX`: đầu cắt hiện ở phía phải.

**Kiểm:** lệnh kiểm ở đầu Plan. Agent viết `Tests/EditMode/SpriteHorizontalFillTests.cs` (đo lười nên không cần `Awake`; sprite tạo
bằng `Sprite.Create` trên texture 100×10, renderer có material shader trên, đọc `_FillU` qua `GetPropertyBlock`), riêng hàng `Awake`
ở `Tests/PlayMode/SpriteHorizontalFillPlayTests.cs` vì ở EditMode `Awake` không chạy:

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

**Mốc commit:** `feat(sprites): SpriteHorizontalFill — cắt phẳng SpriteRenderer bằng shader` (Horcrux).

---

## Giai đoạn 3 — `SpriteFillDividers`

**Mục tiêu:** thanh chia n đoạn, đầu cắt dừng đúng tâm vạch, vạch chỉ hiện trên phần ruột. Xong là thanh đếm có vạch vẽ được, cả trong
tool Editor ngoài Play.

**Vì sao bây giờ:** đọc `Left`, `Width` và ghi `FillAmount` của ruột đã cắt được ở GĐ2.

**Sơ đồ sau GĐ3**

```
caller ──SetSegments+(n) · Show+(t)──► SpriteFillDividers+ ──FillAmount · Left · Width──► SpriteHorizontalFill
                                                └─ localPosition.x, enabled+──► vạch (SpriteRenderer con của ruột)
```

```
SetSegments(n) : n − 1 vạch (lúc chạy chép thêm từ vạch đầu khi thiếu; ngoài Play không chép, LogError)
                 vạch k (1…n−1) ở x = Left + Width · k / n
                 halfWidthAmount = nửa bề rộng vạch / Width
Show(t)        : fill.FillAmount = t
                 vạch k bật ⇔ t ≥ k / n + halfWidthAmount
```

Caller (thanh đếm còn/tổng) gọi `SetSegments(tổng)` khi tổng đổi, `Show(còn / tổng)` mỗi lần số đổi và mỗi frame tween. `còn/tổng` là
bội của `1/n` nên đầu cắt dừng ở `Left + Width · còn / n` — tâm vạch `còn`.

### `Runtime/Utilities/Sprites/SpriteFillDividers.cs` — file mới

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
            fill.FillAmount = amount;
            var dividerCount = _segments - 1;
            for (var i = 0; i < dividers.Count; i++)
            {
                var shown = i < dividerCount && amount >= (float)(i + 1) / _segments + _halfWidthAmount;
                if (dividers[i].enabled != shown) dividers[i].enabled = shown;
            }
        }

        #endregion API
    }
}
```

**Consumes / Produces:** consumes GĐ2 `FillAmount`, `Left`, `Width` · produces `SpriteFillDividers.SetSegments`, `Show`.

**Quyết định:**
- `Show` ghi luôn `FillAmount`: đầu cắt và vạch là một trạng thái, ghi hai chỗ thì caller có thể quên một.
- Ngưỡng `k/n + halfWidthAmount`, không `k/n`: lúc ruột co, vạch tắt ngay khi đầu cắt chạm mép phải vạch, không đợi tới tâm — vạch
  không bao giờ đứng nửa trên ruột nửa trên phần rỗng. Ở `t = k/n` đúng, vạch k tắt: đầu cắt phẳng là ranh giới, vạch ở đó thừa.
- `halfWidthAmount` tính một lần ở `SetSegments` theo scale local của vạch mẫu; scale vạch đổi lúc chạy thì gọi lại `SetSegments`.
- Vạch thừa khi n giảm chỉ tắt, không huỷ: pool lấy lại thanh với tổng khác không sinh rác.
- Danh sách vạch là ô serialize, vạch đầu là mẫu: prefab chạy ngoài Play (tool Editor) đặt sẵn đủ vạch vào ô, vì ngoài Play
  `Instantiate` sinh object con lưu vào scene đang mở. Thiếu vạch ngoài Play thì `LogError` rồi đặt số vạch đang có — lỗi setup không
  nổ, nên báo đỏ.
- So vị trí và `enabled` trước khi ghi: cùng lý do với `SpriteHorizontalFill.Apply`.

**Editor setup** (phía caller): một vạch con của renderer ruột, `sortingOrder` trên ruột · thêm component lên object của ruột, kéo
`SpriteHorizontalFill` vào ô **Fill**, vạch vào phần tử đầu của ô **Dividers** · prefab vẽ ngoài Play: thêm sẵn vạch tới số đoạn lớn
nhất trừ một. Thiếu ô: `NullReferenceException` ở lần `SetSegments` đầu.

**Kiểm:** lệnh kiểm ở đầu Plan. Case thêm vào `SpriteHorizontalFillTests.cs`, riêng hàng chép vạch ở file PlayMode (chỉ lúc chạy mới
chép); ruột sprite 100 px (Width 1, Left −0.5), vạch 2 px:

| Input | Kỳ vọng |
|---|---|
| `SetSegments(3)` | 2 vạch, x = −0.1667, 0.1667 |
| `SetSegments(3)`, `Show(2/3f)` | vạch 1 bật, vạch 2 tắt |
| `SetSegments(3)`, `Show(1/3f + 0.005f)` | vạch 1 tắt (chưa qua nửa bề rộng 0.01) — đỏ khi ngưỡng bỏ `halfWidthAmount` |
| PlayMode, ô một vạch: `SetSegments(5)` rồi `SetSegments(2)`, `Show(1)` | vạch 1 bật, vạch 2…4 tắt; tổng 4 renderer |
| ngoài Play, ô một vạch: `SetSegments(3)` | `LogError` một lần, không thêm renderer, vạch 1 ở x = −0.1667 |
| `SetSegments(1)`, `Show(1)` | không vạch nào bật |

**Mốc commit:** `feat(sprites): SpriteFillDividers — vạch chia trên ruột cắt phẳng` (Horcrux).

---

## Giai đoạn 4 — Tài liệu `docs/Runtime/SpriteFill/SpriteFill.md` và `SpriteFill.html` (agent)

Một tài liệu cho hai cơ chế. `.md` mở đầu `Code: Runtime/Utilities/Sprites/, Shader/sprite_horizontal_fill.shader`. Mục: **bảng chọn**
(Ngữ cảnh đã chốt, kèm cột ứng chế độ `ProgressBar`) · luồng A, luồng B ASCII · bảng API (`FillWidth`, `MinWidth`, `Apply`;
`FillAmount`, `Left`, `Width`, `SetSegments`, `Show`) · "Design decisions" gom Quyết định trái trực giác và Quyết định từng GĐ · "Trước
khi chạy" = Editor setup phía caller của GĐ1, GĐ2, GĐ3 · "Dùng trong dự án": A chụp `full` và `min` một lần ở `Awake`, gọi `Apply` mỗi
lần dữ liệu đổi hoặc mỗi frame tween; B gọi `SetSegments` khi tổng đổi, `Show` mỗi lần số đổi. `.html` chọn từ `.md`: Kịch bản (đầy,
nửa, rất nhỏ, rỗng; thanh chia vạch) · Cách dùng · Flow · Lưu ý (thiếu pivot trái, thiếu Sliced, atlas xoay, `flipX`, thiếu material,
mất batch, sprite đổi lúc chạy). Tên trung tính, không tên dự án.

**Mốc commit:** `docs(sprites): SpriteFill.md, SpriteFill.html` (Horcrux).

---

## Giai đoạn 5 — category-jam dùng bản Horcrux, xoá bản tạm, xoá Plan (agent)

**Mục tiêu:** thanh tiến độ box, thanh đếm lớp của kệ Single và view kệ trong tool level chạy trên bản Horcrux; bản tạm và test của nó
biến mất; prefab, material không "Missing script", không hồng.

| File | Việc |
|---|---|
| `Assets/_CategoryJam/Scripts/Utilities/SlicedFill.cs` + `.meta` | xoá (class static, không asset nào trỏ GUID) |
| `Assets/_CategoryJam/Tests/EditMode/SlicedFillTests.cs` + `.meta` | xoá — 11 test đã ở bảng GĐ1 |
| `Assets/Horcrux/Runtime/Utilities/Sprites/SpriteHorizontalFill.cs.meta` | `guid:` ← GUID trong `.meta` bản tạm `SpriteHorizontalFill.cs` (hiện `988795041c6f7d64aaa65b34ef9b7ed6`; đọc lại từ file) |
| `Assets/Horcrux/Runtime/Utilities/Sprites/SpriteFillDividers.cs.meta` | `guid:` ← bản tạm (hiện `86ce7e486a3758e439a70d8c44ec2472`) |
| `Assets/Horcrux/Shader/sprite_horizontal_fill.shader.meta` | `guid:` ← bản tạm (hiện `cc94eb9ad979a9d45912ac378507d3c2`) |
| bản tạm `SpriteHorizontalFill.cs`, `SpriteFillDividers.cs`, `sprite_horizontal_fill.shader` + `.meta` | xoá |
| `Scripts/Entities/Box/CategoryBox.cs` | thêm `using Horcrux.Runtime.Utilities.Sprites;`; giữ `using CategoryJam.Gameplay.Utilities;` nếu file còn type khác của namespace đó, không thì bỏ |
| `Scripts/Entities/Shelf/SingleShelfAmountBar.cs`, `_GameDesign/Script/LevelEditor/Design/DesignShelfView.cs` | như trên (`CategoryJam.GameDesign.asmdef` đã tham chiếu `com.horcrux.runtime`) |
| `Scripts/Entities/Entities.md` (cây `AmountDisplay`, đoạn `_FillU`, `progress_fill`), `Structure.md` (cây `Utilities`, bảng bản tạm chờ Horcrux), `Scripts/Feel.md` (Máy kiểm `SlicedFillTests`), `_GameDesign/Script/LevelEditor/LevelEditor.md` (thanh `amount_bar`) | trỏ bản Horcrux và tài liệu `docs/Runtime/SpriteFill/`, bỏ dòng bản tạm và đường dẫn Plan cũ |
| `Assets/Horcrux/Plans/Horcrux_SpriteFill_Plan.md` + `.meta` | xoá |

`CategoryBox.cs` cũng đổi trong GĐ chuyển dự án của `Horcrux_UniTextSetFraction_Plan.md`: gõ cả hai thì làm chung một commit.

**Editor setup** — `SingleShelf.prefab`, `_GameDesign/Prefabs/design_shelf_single.prefab` trỏ hai script, `Arts/Material/sprite_horizontal_fill.mat`
trỏ shader, đều theo GUID; bản Horcrux **nhận lại GUID của bản tạm**:

1. Đóng Prefab Mode nếu đang mở hai prefab trên (Prefab Mode tự lưu đè file).
2. Đóng Unity (sửa `.meta` khi Editor mở thì Unity ghi đè). Đọc ba `guid:` từ `.meta` bản tạm, xoá bản tạm `.cs`/`.shader` + `.meta`, ghi
   ba GUID vào `.meta` bản Horcrux — cùng một lần.
3. Mở Unity, chờ import. Mở `SingleShelf.prefab` › `AmountDisplay`: `Sprite Horizontal Fill`, `Sprite Fill Dividers` còn đủ ô; material
   ruột không hồng.

**Kiểm:** `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj CategoryJam.Gameplay.csproj,CategoryJam.GameDesign.csproj,CategoryJam.EditModeTests.csproj --tests CategoryJam.EditModeTests,Horcrux.EditModeTests`
xanh · `grep -rln "SlicedFill\|SpriteHorizontalFill\|SpriteFillDividers" Assets/_CategoryJam Assets/_GameDesign --include=*.cs` chỉ
ra ba caller `CategoryBox.cs`, `SingleShelfAmountBar.cs`, `DesignShelfView.cs` · `grep -rl 988795041c6f7d64aaa65b34ef9b7ed6 Assets` chỉ
còn `.meta` Horcrux và hai prefab · Console không "Missing script".

**Chơi thử (developer):** vào màn có box và kệ Single · đặt tile vào box — thanh tiến độ trên box dài ra theo số tile, hai đầu bo, lúc rất
ít tile vẫn thấy một đoạn ngắn đủ hai đầu, rỗng thì ẩn · nhặt tile khỏi kệ Single — thanh đếm lớp co lại, đầu cắt phẳng dừng đúng tâm
vạch, vạch ở phần rỗng ẩn · mở tool thiết kế level, xem kệ Single — thanh vẽ như trong game · mọi thứ như trước khi chuyển. Dấu hiệu hỏng:
ruột hồng (mất shader), ruột luôn đầy (mất material), thanh không đổi, vạch lệch khỏi đầu cắt, "Missing script" trên prefab.

**Mốc commit:** `refactor(sprites): thanh tiến độ, thanh đếm dùng SpriteFill của Horcrux` (dự án, kèm con trỏ submodule).

---

## File → GĐ

| File | GĐ |
|---|---|
| `Runtime/AssemblyInfo.cs` (nếu chưa có), `Runtime/Utilities/Sprites/SlicedFill.cs`, `Tests/EditMode/SlicedFillTests.cs` (agent) | 1 |
| `Shader/sprite_horizontal_fill.shader`, `Runtime/Utilities/Sprites/SpriteHorizontalFill.cs`, `Tests/EditMode/SpriteHorizontalFillTests.cs`, `Tests/PlayMode/SpriteHorizontalFillPlayTests.cs` (agent) | 2 |
| `Runtime/Utilities/Sprites/SpriteFillDividers.cs`; hai file test trên thêm case | 3 |
| `docs/Runtime/SpriteFill/SpriteFill.md`, `.html` (agent) | 4 |
| category-jam bản tạm (xoá), `SlicedFillTests.cs` (xoá), ba `.meta` Horcrux nhận GUID, `CategoryBox.cs`, `SingleShelfAmountBar.cs`, `DesignShelfView.cs`, `Entities.md`, `Structure.md`, `Feel.md`, `LevelEditor.md`; Plan này (xoá) | 5 |
