# DOCS_SKILL — Viết docs hệ thống: .md → .html (+ Plan)

3 loại tài liệu, cùng bộ nguyên tắc dưới đây:
- **`.md`** = nguồn sự thật (agent đọc hiểu + phát triển).
- **`.html`** = tài liệu **chính** developer đọc, visualize `.md`.
- **Plan** (tùy chọn) = để developer tự code lại.

## Quy trình

1. Đọc **tất cả** source → hiểu 100% data flow, lifecycle, lý do mỗi quyết định.
2. Viết `.md` cùng thư mục hệ thống (Phần A) — chính xác 100%.
3. Sinh `.html` từ `.md` (Phần B) — 100% nội dung `.md`.
4. (Khi user yêu cầu Plan) — Phần C.

## Nguyên tắc xuyên suốt (áp cho cả 3 loại)

Đây là nơi **duy nhất** định nghĩa các nguyên tắc chung; các phần sau chỉ bổ sung điểm riêng.

1. **Ít văn** — bảng · công thức · diagram · bullet thay đoạn văn. Mỗi ý 1–2 câu "X → Y", không kể lể.
2. **Trình tự hợp lý** — dẫn theo mạch **dễ→khó, tổng quan→chi tiết, vấn đề→giải pháp, trực giác→hình thức hóa**. Mỗi bước chỉ dùng khái niệm đã nêu trước; ý phụ thuộc nhau đặt liền kề; đánh số khi là quy trình.
3. **Giải thích bản chất** — mỗi khái niệm: cơ chế + "tại sao" + trade-off, không chỉ "dùng X".
4. **Không lặp** — 1 khái niệm giải thích 1 nơi (lần đầu xuất hiện), sau đó `xem §x`. Rà cả 3 loại trước khi chốt. **Liên hệ thống:** khái niệm đã dẫn giải ở doc hệ khác → trỏ `xem §… của <hệ>`, không giải lại (vd Euler's formula giải 1 lần ở SpringDamper, DampedOscillator trỏ tới). Ngoại lệ: bảng tra cứu tổng kết cuối (metrics/API).
5. **Hệ toán/vật lý → dẫn giải sâu** theo mạch riêng ở **Phần A**.

---

# Phần A — File .md

**Mục đích:** agent đọc hiểu hệ thống nhanh nhất + nguồn nội dung duy nhất cho `.html`.

**Cấu trúc:** sections theo **data flow** (input→processing→output), KHÔNG "lý thuyết→thiết kế→code". Chọn mục hợp hệ thống: Data structures · Core algorithm · Lifecycle · Implementation details · Framework integration · Design decisions · Safety/error · Platform issues · Architecture (file tree + roles) · Testing (checklist + debug) · Extension · **Performance (bảng metrics — luôn cuối)**.

**Riêng của `.md`** (ngoài nguyên tắc xuyên suốt): so sánh ≥2 lựa chọn → bảng có ✓; data flow → ASCII diagram; code trích nguyên văn, không viết lại; đủ chi tiết để `.html` dựng 100% mà không cần đọc source.

**KaTeX trong `.md`** (lỗi hay tái diễn): bất kỳ lệnh có `\` (`\frac`, `\sqrt`, `\cos`, `\tfrac`…) **bắt buộc** nằm trong `$…$` (inline) hoặc `$$…$$` (block) — viết trong backtick sẽ hiện **raw text**. Backtick chỉ dùng cho ký hiệu Unicode thuần (`ω₀`, `ζ`, `e^{rt}`, `k/m`). Mỗi block `$$…$$` **một dòng** (block trải nhiều dòng vỡ ở một số renderer). Chốt xong quét lại: strip hết `$…$`/backtick còn sót `\[a-zA-Z]` nào là lọt.

## Hệ toán học / vật lý — mạch dẫn giải

Hệ có lõi toán/vật lý (solver, interpolation, dao động, đạn đạo, hình học…) phải cho developer **hiểu sâu**, không chỉ liệt kê công thức cuối. Trình bày lần lượt:

| Bước | Nội dung | Trình bày |
|---|---|---|
| **Bản chất** | hiện tượng/mô hình thực tế đằng sau, map sang mục đích | đoạn ngắn + bảng "thành phần → vai trò" |
| **Nguyên lý** | định luật/định lý gốc; vì sao mô hình đúng | bảng + diagram |
| **Công thức** | phương trình chi phối + ý nghĩa ký hiệu | `$$…$$` + bảng ký hiệu |
| **Lý do dùng** | vì sao công thức/tham số này, không cái khác | bảng so sánh ✓ |
| **Biến đổi/giải nghiệm** | pt gốc → nghiệm dùng trong code, **không nhảy bước** | đánh số ①②③, mỗi bước 1 câu "vì sao" |
| **Kiểm mốc** | giá trị biên (t=0, t→∞…) xác nhận nghiệm đúng | bảng "mốc → kỳ vọng → ✓" |

Bắt buộc:
- **Mọi công thức chốt phải kiểm mốc**; **trực giác trước, ký hiệu sau** (nêu ý niệm "càng xa đi càng nhanh" rồi mới ra phương trình).
- **Suy ra, không áp đặt** — công thức chốt phải *dẫn ra* từ nguyên lý gốc, tuyệt đối không "xuất hiện từ hư không" rồi mới giải thích ngược.
- **Mỗi "tại sao" kèm phép kiểm tái lập được** khi có thể — reader tự kiểm, không phải tin lời (vd "thử `y=cos(ωt)` → `ÿ=−ω²y`, khớp khi `ω²=k/m`").
- **Nêu rõ lựa chọn mô hình khi code cố ý lệch/đơn giản hóa so với vật lý chuẩn** — vì sao chọn vậy + khi nào dùng bản đầy đủ (vd DampedOscillator tách rời `f`/`λ` cho game feel, ai cần ràng buộc `ω_d=√(ω₀²−λ²)` thì dùng SpringDamper). Tránh reader tra sách rồi bối rối.

---

# Phần B — File .html

**Mục đích:** `.html` là **tài liệu CHÍNH** developer đọc. Giữ cấu trúc section của `.md`; trực quan, không dài dòng.

## Tư tưởng (điểm riêng của .html)

- **Hạn chế SHOW CODE** — `.html` để *hiểu*, không chép code. Thay bằng KaTeX/bảng/`.arch`/demo. Chữ ký API → bảng. Chỉ giữ code khi bản thân nó *là* thứ cần minh họa (1 dòng lỗi/pattern then chốt); không dán nguyên class/hàm.
- **Trực quan hóa theo loại nội dung**: so sánh → bảng · data flow → `.arch` · công thức → KaTeX (`.eq`, chốt → `.eq.boxed`) · giá trị liên tục → Canvas · nhiều bước → step. Demo chỉ khi bảng/text không đủ; hệ toán/vật lý ưu tiên demo Canvas để "thấy" hành vi.
- **KaTeX**: TeX ở `data-tex`, render **1 lần** lúc load (static). Bỏ KaTeX khi doc gần như không công thức.
- **Thẩm mỹ**: dark theme, `.reveal` fade-in, header tĩnh + TOC, section cuối `.perf-grid`, responsive.
- **Zero idle cost**: không rAF loop chạy mãi; demo event-driven/static; KaTeX render một lần.

## HTML template

```html
<!DOCTYPE html>
<html lang="vi">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>{System} — {Project}</title>
<link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/themes/prism-tomorrow.min.css">
<link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/KaTeX/0.16.9/katex.min.css"><!-- bỏ nếu doc không có công thức -->
<style>
*,*::before,*::after{box-sizing:border-box;margin:0;padding:0}
:root{--bg:#0d1117;--bg2:#161b22;--bg3:#1c2333;--bg4:#242d3a;--tx:#e6edf3;--tx2:#8b949e;--tx3:#484f58;--ac:#58a6ff;--ac2:#bc8cff;--gr:#3fb950;--or:#d29922;--rd:#f85149;--bd:#30363d;--r:10px;--mono:'JetBrains Mono','Fira Code','Cascadia Code','SF Mono',monospace;--sans:-apple-system,BlinkMacSystemFont,'Segoe UI',Helvetica,Arial,sans-serif}
html{scroll-behavior:smooth;background:var(--bg);color:var(--tx);font:17px/1.7 var(--sans)}
body{overflow-x:hidden}
a{color:var(--ac);text-decoration:none}a:hover{text-decoration:underline}
#progress{position:fixed;top:0;left:0;height:3px;background:linear-gradient(90deg,var(--ac),var(--ac2));width:0;z-index:999;transition:width .1s}
#header{max-width:920px;margin:0 auto;padding:4rem 1.5rem 2rem;border-bottom:1px solid var(--bd)}
#header h1{font-size:clamp(2rem,5vw,3rem);font-weight:800;letter-spacing:-.03em;background:linear-gradient(135deg,var(--ac),var(--ac2));-webkit-background-clip:text;-webkit-text-fill-color:transparent}
#header .sub{font-size:1.05rem;color:var(--tx2);margin-top:.4rem}
#header .badge{display:inline-block;margin-top:.8rem;padding:.3em .9em;border:1px solid var(--bd);border-radius:99px;font-family:var(--mono);font-size:.78rem;color:var(--tx2)}
main{max-width:920px;margin:0 auto;padding:0 1.5rem 6rem}
section{padding-top:4rem}
section .reveal{opacity:0;transform:translateY(24px);transition:opacity .6s ease,transform .6s ease}
section .reveal.visible{opacity:1;transform:none}
#toc{max-width:920px;margin:3rem auto;padding:0 1.5rem}
#toc h2{font-size:1.3rem}
#toc ol{columns:2;column-gap:2rem;padding-left:1.5em}
#toc li{font-size:.92rem;margin-bottom:.3rem;break-inside:avoid}
@media(max-width:600px){#toc ol{columns:1}}
h2{font-size:1.9rem;font-weight:700;letter-spacing:-.02em;margin-bottom:1.5rem;padding-bottom:.5rem;border-bottom:1px solid var(--bd)}
h3{font-size:1.3rem;font-weight:600;margin:2.5rem 0 1rem;color:var(--ac)}
p,ul,ol{margin-bottom:1rem}ul{padding-left:1.3em}li{margin-bottom:.3rem}
strong{color:var(--tx);font-weight:600}
em{color:var(--ac2);font-style:normal}
code{font-family:var(--mono);font-size:.88em;background:var(--bg3);padding:.15em .4em;border-radius:4px}
pre{margin:1.2rem 0!important;border-radius:var(--r)!important;border:1px solid var(--bd)!important}
pre code{background:none!important;padding:0!important;font-size:.85rem!important;line-height:1.6!important}
.card{background:var(--bg2);border:1px solid var(--bd);border-radius:var(--r);padding:1.5rem;margin:1.5rem 0}
.card-title{font-weight:600;margin-bottom:.75rem;font-size:1.05rem;color:var(--ac)}
.note{border-left:3px solid var(--or);background:var(--bg2);padding:1rem 1.2rem;border-radius:0 var(--r) var(--r) 0;margin:1.5rem 0;font-size:.92rem;color:var(--tx2)}
.note strong{color:var(--or)}
.note.good{border-left-color:var(--gr)}.note.good strong{color:var(--gr)}
.note.bad{border-left-color:var(--rd)}.note.bad strong{color:var(--rd)}
.eq{background:var(--bg3);border:1px solid var(--bd);border-radius:var(--r);padding:1.1rem 1.3rem;margin:1.3rem 0;overflow-x:auto;text-align:center}
.eq.boxed{border-color:var(--ac);box-shadow:0 0 0 1px var(--ac) inset}
table{width:100%;border-collapse:collapse;margin:1.2rem 0;font-size:.92rem}
th{text-align:left;padding:.6rem .8rem;border-bottom:2px solid var(--bd);color:var(--ac);font-weight:600}
td{padding:.5rem .8rem;border-bottom:1px solid var(--bd)}
tr:last-child td{border:none}
.demo{background:var(--bg2);border:1px solid var(--bd);border-radius:var(--r);padding:1.5rem;margin:2rem 0}
.demo-title{font-family:var(--mono);font-size:.8rem;color:var(--ac2);text-transform:uppercase;letter-spacing:.08em;margin-bottom:1rem}
.demo canvas{display:block;border-radius:8px;cursor:crosshair}
.demo-row{display:flex;gap:1.5rem;align-items:flex-start;flex-wrap:wrap}
.demo-row>*{flex:1;min-width:250px}
.arch{font-family:var(--mono);font-size:.82rem;line-height:1.8;white-space:pre;overflow-x:auto;padding:1rem;background:var(--bg3);border-radius:var(--r);color:var(--tx2)}
.arch em{color:var(--ac);font-style:normal}
.perf-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(200px,1fr));gap:1rem;margin:1.5rem 0}
.perf-card{background:var(--bg3);border-radius:var(--r);padding:1.2rem;text-align:center}
.perf-val{font-size:1.8rem;font-weight:800;background:linear-gradient(135deg,var(--ac),var(--ac2));-webkit-background-clip:text;-webkit-text-fill-color:transparent}
.perf-label{font-size:.82rem;color:var(--tx2);margin-top:.2rem}
footer{text-align:center;padding:3rem 1rem;color:var(--tx3);font-size:.85rem;border-top:1px solid var(--bd)}
</style>
</head>
<body>
<div id="progress"></div>
<header id="header"><h1>{System}</h1><p class="sub">{mô tả}</p><span class="badge">{keywords}</span></header>
<nav id="toc"><h2>Mục lục</h2><ol>...</ol></nav>
<main><!-- sections --></main>
<footer>{Project} · {System} · <code>{Namespace}</code></footer>
<script src="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/prism.min.js"></script>
<script src="https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/components/prism-csharp.min.js"></script>
<script src="https://cdnjs.cloudflare.com/ajax/libs/KaTeX/0.16.9/katex.min.js"></script><!-- bỏ nếu doc không có công thức -->
<script>
const observer=new IntersectionObserver(e=>{e.forEach(e=>{if(e.isIntersecting)e.target.classList.add('visible')})},{threshold:.08});
document.querySelectorAll('.reveal').forEach(el=>observer.observe(el));
const pb=document.getElementById('progress');
window.addEventListener('scroll',()=>{const h=document.documentElement;pb.style.width=(h.scrollTop/(h.scrollHeight-h.clientHeight)*100)+'%'});
// KaTeX: render mỗi .eq[data-tex] một lần lúc load (static). Bỏ block này nếu không dùng KaTeX.
document.querySelectorAll('.eq[data-tex]').forEach(el=>{
  try{katex.render(el.getAttribute('data-tex'),el,{displayMode:true,throwOnError:false});}
  catch(e){el.textContent=el.getAttribute('data-tex');}
});
</script>
</body>
</html>
```

Thêm Prism components theo nhu cầu: `prism-glsl`, `prism-json`, `prism-python`, ...

## Demo patterns

| Pattern | Khi nào | Perf |
|---------|---------|------|
| **A: Canvas + Mouse** | Giá trị thay đổi theo vị trí | Pre-render bg, rAF guard |
| **B: Step/Auto** | Quá trình rời rạc | DOM-only, `clearInterval` on reset, ≥800ms |
| **C: Input → Transform** | Chuyển đổi real-time | Compute rẻ → trực tiếp handler |
| **D: Static Graph** | Hàm toán học | IIFE 1 lần, không listener |

Mỗi demo: IIFE wrap, cache `getElementById` đầu IIFE, `addEventListener` (không `onclick`).

## Hiệu năng .html — blacklist

| Cấm trong draw loop / handler | Tại sao | Thay bằng |
|-------------------------------|---------|-----------|
| `ctx.shadowBlur` | Gaussian blur per draw | Radial gradient |
| `createImageData()` per event | Alloc W×H×4 bytes | Tạo 1 lần, reuse |
| Per-pixel math per mousemove | O(W×H) 60+/giây | Pre-render → cached ImageData |
| `mousemove` → draw trực tiếp | Draw 2–3× giữa 2 frame | rAF guard: scalar coords + dirty flag |
| `ctx.fillStyle = 'var(--x)'` | Canvas không parse CSS vars | Hex: `'#8b949e'` |
| `innerHTML` tight loop | Parser + reflow | `textContent` |
| Quên `cancelAnimationFrame` | rAF chạy sau leave | Cancel trong leave handler |

Pattern A mẫu (pre-render + rAF guard):

```js
(function() {
  const canvas = document.getElementById('demoCanvas');
  const ctx = canvas.getContext('2d');
  const W = canvas.width, H = canvas.height;

  // Pre-render background ONCE
  const bg = ctx.createImageData(W, H);
  for (let y = 0; y < H; y++)
    for (let x = 0; x < W; x++) {
      const i = (y * W + x) * 4;
      bg.data[i]=/*R*/; bg.data[i+1]=/*G*/; bg.data[i+2]=/*B*/; bg.data[i+3]=255;
    }

  function draw(mx, my) { ctx.putImageData(bg, 0, 0); /* cheap overlay */ }
  draw(-1, -1);

  let px=-1, py=-1, dirty=false, raf=0;
  function tick() { raf=0; if(dirty){draw(px,py); dirty=false;} }
  canvas.addEventListener('mousemove', e => {
    const r=canvas.getBoundingClientRect();
    px=(e.clientX-r.left)*W/r.width; py=(e.clientY-r.top)*H/r.height;
    dirty=true; if(!raf) raf=requestAnimationFrame(tick);
  });
  canvas.addEventListener('mouseleave', () => {
    dirty=false; if(raf){cancelAnimationFrame(raf);raf=0;} draw(-1,-1);
  });
})();
```

---

## Checklist

Ngầm định: mọi item chịu **Nguyên tắc xuyên suốt** (ít văn · trình tự hợp lý · giải thích bản chất · không lặp · mạch toán). Dưới đây chỉ liệt kê điểm kiểm riêng.

**`.md`:** data-flow structure · so sánh→bảng✓ · pipeline→ASCII · metrics bảng cuối · đủ để `.html` dựng 100% không cần source.

**`.html`:** 100% nội dung `.md` · single file, TOC khớp, responsive · hạn chế code (API→bảng) · mọi công thức→`.eq` KaTeX (chốt→`.eq.boxed`) · zero idle cost (Canvas: pre-render, rAF guard, hex màu, cancel; không allocate/shadowBlur trong loop).

---

# Phần C — Plan tự triển khai (khi user yêu cầu)

Developer tự code lại để học. Plan **tự chứa**: `§0` dẫn giải toán (mạch Phần A, không nhảy sang doc khác) → các task xếp theo thứ tự phụ thuộc (nền trước, dùng lại sau), mỗi task chỉ cần thứ đã có.

**Mỗi task gồm:** Files (path chính xác) · Interfaces (consumes/produces, chữ ký đầy đủ) · bảng "toán→code" (trỏ §0) · bảng "self-doc & tối ưu" (lý do mỗi quyết định) · **code hoàn chỉnh dán-được** (comment trỏ công thức) · kiểm chứng bảng input→kỳ vọng (nêu rõ nếu không kèm code test).

**Code — 4 đảm bảo bắt buộc:**

| Đảm bảo | Cụ thể |
|---|---|
| **Đúng đắn tuyệt đối** | khớp 100% công thức §0; mỗi nghiệm đã kiểm mốc trước khi vào code; comment trỏ công thức nguồn (`// B = (v₀+ζω₀y₀)/ω_d`). |
| **Tối ưu CPU** | precompute hằng nặng (exp/sqrt/sincos, chia) 1 lần ngoài hot path; chia→nhân; guard thoát sớm; cache trung gian; `AggressiveInlining` wrapper mỏng. |
| **Giảm GC** | `struct` thay `class`; `ref`/`in` thay copy; không `new` ref-type/LINQ/closure/string trong hot path; reuse buffer. |
| **Self-document** | tên nói rõ mục đích (`SolveAnalytic`≠`Process`); boolean là câu hỏi (`IsActive`); XML doc + công thức + "tại sao" ở API public; comment chỉ nói *tại sao*. |

**Kiểm riêng:** code đủ 4 đảm bảo. (Ít văn · trình tự · không lặp: theo Nguyên tắc xuyên suốt.)

**Verify công thức ↔ code trước khi chốt** (không khẳng định suông, phải có bằng chứng):
- **Đối chiếu từng số hạng** — mỗi hộp `$$boxed$$` map thẳng 1 dòng code, kiểm từng hệ số/dấu (vd `ẋ_cos=A·e^(−λt)[−λcos−ωsin]` ↔ `env*(-lambda*c - omega*s)`).
- **Kiểm mốc chéo** — giá trị biên trong §0 phải khớp bảng kiểm chứng của task (vd `ẋ(0)=−λA` ↔ `GetVelocity(Cos,·,2,0)=−2`).
- **Đạo hàm số** khi có hàm đạo hàm — `f'(t) ≈ (f(t+h)−f(t−h))/2h`, `h=1e-4`, phải khớp công thức giải tích.
- **Round-trip** khi có cặp converter/overload — `A→B→A` phải về gần chính nó (vd `*HalfLife` ↔ `decay`).
