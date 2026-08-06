# DOCS_SKILL — Viết docs hệ thống: .md → .html (+ Plan)

3 loại đầu ra, cùng một bộ tư tưởng:
- **`.md`** = nguồn sự thật (agent đọc hiểu + phát triển).
- **`.html`** = tài liệu **chính** developer đọc, visualize `.md`.
- **Plan** (tùy chọn) = để developer tự code lại.

> **Đọc theo thứ tự.** §Nguyên tắc xuyên suốt + §Phỏng vấn ngữ cảnh là **tư tưởng chung — bắt buộc nắm, áp cho mọi việc**. Phần A/B/C chỉ là cơ chế riêng của từng loại đầu ra, đều dựa trên tư tưởng đó; nắm tư tưởng rồi thì A/B/C đọc lướt là đủ.

## Quy trình

0. **Phỏng vấn ngữ cảnh** (§ ngay dưới) — bắt buộc, trước khi viết bất kỳ loại nào.
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
5. **Dẫn giải sâu đúng chỗ** — hệ toán/vật lý phải cho hiểu bản chất, nhưng **độ sâu cân theo độ khó thật của công thức**, không mặc định tối đa: suy ra trong 1–2 bước → công thức + kiểm mốc là đủ; nghiệm vi phân / biến đổi nhiều bước / có chọn lựa mô hình đáng bàn → dẫn giải đầy đủ (mạch Phần A). Dẫn định luật nền để biện minh cho một phép nhân là over-engineering ở tầng tài liệu.
6. **Vừa đủ, nhưng mở được** — đơn giản nhất mà vẫn phục vụ đủ mục đích ban đầu, đồng thời **không chặn hướng mở rộng về sau**. Đơn giản là **mặc định**; mỗi lớp phức tạp thêm vào phải trả giá bằng **một nhu cầu đang có thật** — "phòng khi cần", "cho đầy đủ", "chuẩn hơn" không phải nhu cầu. Kiểm nhanh: *xóa nó đi thì hỏng ở đâu* — không gọi được tên chỗ hỏng thì bỏ. Hai bờ vực đều sai: **thừa** (đúng, nhưng không ai cần) và **thiếu** (cắt vào mục đích ban đầu — mục đích là **sàn**, không phải chỗ gọt cho ngắn).
7. **Game feel là tiêu chí nghiệm thu** — hệ này phục vụ **cảm giác chơi**, không phục vụ độ chính xác vật lý. Công thức "sai sách" mà chơi đã tay thì **đúng**; công thức chuẩn sách mà chơi vô hồn thì **sai**. Toán ở đây là *công cụ để đạt cảm giác*, không phải mục tiêu.
8. **Editor-first** — thứ gì quyết được lúc authoring thì đừng đẩy sang runtime: **code lo *hành vi*, Editor lo *cấu hình & kết nối*** (chi tiết Unity: §C.1).
9. **Bằng chứng, không khẳng định suông** — mọi "tại sao" kèm phép kiểm **tái lập được**; mọi công thức chốt phải **kiểm mốc**; code phải đối chiếu với công thức trước khi chốt. Không viết "đã đúng", "đã tối ưu" mà thiếu mốc, số đo, hoặc phép thử reader tự chạy lại được.
10. **Hỏi đúng lúc, không tự bày thêm** — thiếu ngữ cảnh thì **hỏi** (gộp 1–2 lượt, §Phỏng vấn), không tự đoán rồi viết; buộc phải giả định thì ghi rõ `Giả định (cần xác nhận): …`. Ngược lại, **không tự đề xuất** bản phức tạp / chính xác hơn, cũng không hỏi user có muốn không — chỉ khi **user tự nêu** nhu cầu đó mới hỏi lại **một lần** để cân đắt/lợi, rồi theo quyết định của user.

## Phỏng vấn ngữ cảnh & chốt phạm vi (áp cho cả 3 loại)

Agent **không** được suy đoán ngữ cảnh rồi viết. Đoán sai lệch về hai phía, cả hai đều đắt:
- **Thừa (over-engineering)** — chương không ai đọc, hàm không caller nào gọi, tối ưu chỗ không phải hot path, dẫn giải 100 dòng cho công thức 1 dòng. Trả giá bằng thời gian viết + đọc + bảo trì, vĩnh viễn.
- **Thiếu (đóng cứng)** — doc bỏ mất thứ người đọc cần; chữ ký/cấu trúc chặn hướng dùng thật, phải đập đi làm lại.

Hỏi **5 nhóm dưới**, gộp thành **1–2 lượt** (không hỏi lắt nhắt từng câu một). Đã biết chắc nhóm nào thì nêu giả định của mình để user xác nhận, thay vì hỏi lại.

| Nhóm | Với `.md` / `.html` | Với Plan / code |
|---|---|---|
| **Ai dùng đầu ra** | ai đọc, đọc để làm gì, biết sẵn tới đâu | ai gọi, gọi ở đâu trong game, có caller thật **ngay bây giờ** chưa |
| **Mục tiêu** | đọc xong phải **làm được gì** | phải đạt **cảm giác/hành vi** gì, nghiệm thu bằng gì |
| **Ngân sách** | độ sâu & độ dài nào là đủ | bao nhiêu lần/giây, có phải hot path không, platform nào |
| **Ranh giới** | phần nào giải ở đây, phần nào trỏ sang doc khác | phần nào của class này, phần nào của caller/hệ khác |
| **Hướng mở rộng thật** | hệ sắp đổi gì khiến doc phải sửa | **chắc chắn** sắp cần thêm gì; cái gì *có thể* cần nhưng chưa chắc |

**Quy tắc chốt phạm vi** — một luật, áp cho mọi thứ định đưa vào (chương doc · demo · hàm · tham số · guard · tối ưu):

| Tình huống | Xử lý |
|---|---|
| Có nhu cầu thật, ngay bây giờ | → đưa vào |
| Chỉ "có thể cần sau", **thêm sau rẻ** (thêm mục mới / hàm mới, không sửa cái cũ) | → **để lại**, ghi 1 dòng ở mục "Mở rộng sau" |
| Chỉ "có thể cần sau", nhưng **thêm sau đắt** (sửa chữ ký, đập cấu trúc, viết lại cả doc) | → làm ngay; đây là chỗ **duy nhất** đáng phòng xa |
| Tối ưu (inline, precompute, struct, pooling…) | → chỉ khi tần suất **đã xác nhận** là hot path |
| Guard / nhánh biên | → chỉ khi input thật chạm được biên đó |

> **Tính mở rộng đến từ Open/Closed** — thêm cái mới mà không sửa cái cũ — **không** đến từ việc viết sẵn thứ chưa ai cần. Một hàm 1 dòng thêm sau tốn 2 phút; giữ nó trong API từ đầu tốn mãi mãi. Ngược lại, chữ ký sai thì sửa sau rất đắt → dồn công sức phòng xa vào **chữ ký và ranh giới trách nhiệm**, không vào số lượng.

**Ghi kết quả phỏng vấn vào đầu output** — Plan: mục **"Ngữ cảnh đã chốt"** trước `§0`; `.md`: nêu ở phần mở. Gồm: người dùng · mục tiêu · ranh giới · **những gì cố ý KHÔNG làm + lý do**. Người đọc sau biết vì sao phạm vi dừng ở đó, không "bổ sung cho đủ".

**Chưa có câu trả lời thì chưa viết.** Nếu buộc phải giả định (user bận, câu hỏi phụ), ghi rõ `Giả định (cần xác nhận): …` tại chỗ dùng — không giấu giả định vào output như thể đã chốt.

---

# Phần A — File .md

**Mục đích:** agent đọc hiểu hệ thống nhanh nhất + nguồn nội dung duy nhất cho `.html`.

**Cấu trúc:** sections theo **data flow** (input→processing→output), KHÔNG "lý thuyết→thiết kế→code". Chọn mục hợp hệ thống: Data structures · Core algorithm · Lifecycle · Implementation details · Framework integration · Design decisions · Safety/error · Platform issues · Architecture (file tree + roles) · Testing (checklist + debug) · Extension · **Performance (bảng metrics — luôn cuối)**.

**Riêng của `.md`** (ngoài nguyên tắc xuyên suốt): so sánh ≥2 lựa chọn → bảng có ✓; data flow → ASCII diagram; code trích nguyên văn, không viết lại; đủ chi tiết để `.html` dựng 100% mà không cần đọc source.

**KaTeX trong `.md`** (lỗi hay tái diễn): bất kỳ lệnh có `\` (`\frac`, `\sqrt`, `\cos`, `\tfrac`…) **bắt buộc** nằm trong `$…$` (inline) hoặc `$$…$$` (block) — viết trong backtick sẽ hiện **raw text**. Backtick chỉ dùng cho ký hiệu Unicode thuần (`ω₀`, `ζ`, `e^{rt}`, `k/m`). Mỗi block `$$…$$` **một dòng** (block trải nhiều dòng vỡ ở một số renderer). Chốt xong quét lại: strip hết `$…$`/backtick còn sót `\[a-zA-Z]` nào là lọt.

## Hệ toán học / vật lý — mạch dẫn giải

Hệ có lõi toán/vật lý (solver, interpolation, dao động, đạn đạo, hình học…) phải cho developer **hiểu sâu**, không chỉ liệt kê công thức cuối.

> **Cân độ sâu trước (Nguyên tắc 5):** mạch 6 bước dưới đây chỉ dành cho công thức **không hiển nhiên**. Suy ra trong 1–2 bước → công thức + kiểm mốc là đủ, không chạy hết mạch.

Trình bày lần lượt:

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
- **Suy ra, không áp đặt** — công thức chốt phải *dẫn ra* từ nguyên lý gốc, tuyệt đối không "xuất hiện từ hư không" rồi mới giải thích ngược. **Ngoại lệ: thứ chọn bằng cảm giác** (hằng số tinh chỉnh, đường cong tự chế cho đã tay) — nói thẳng "chọn bằng tai/mắt, số này cho cảm giác X", **đừng bịa dẫn giải vật lý** cho một giá trị vốn chọn bằng cảm nhận.
- **Phép kiểm tái lập được** cho mỗi "tại sao" (Nguyên tắc 9) — vd "thử `y=cos(ωt)` → `ÿ=−ω²y`, khớp khi `ω²=k/m`".
- **Lệch vật lý chuẩn là bình thường, không phải lỗi cần bào chữa** (Nguyên tắc 7). Chỉ cần nêu **lệch ở đâu, vì sao, khi nào mới cần bản đầy đủ** (vd DampedOscillator tách rời `f`/`λ` cho game feel, ai cần ràng buộc `ω_d=√(ω₀²−λ²)` thì dùng SpringDamper). Tránh reader tra sách rồi bối rối.

---

# Phần B — File .html

**Mục đích:** `.html` là **tài liệu CHÍNH** developer đọc. Giữ cấu trúc section của `.md`; trực quan, không dài dòng.

## Điểm riêng của `.html`

- **Hạn chế SHOW CODE** — `.html` để *hiểu*, không chép code. Thay bằng KaTeX/bảng/`.arch`/demo. Chữ ký API → bảng. Chỉ giữ code khi bản thân nó *là* thứ cần minh họa (1 dòng lỗi/pattern then chốt); không dán nguyên class/hàm.
- **Trực quan hóa theo loại nội dung**: so sánh → bảng · data flow → `.arch` · công thức → KaTeX (`.eq`, chốt → `.eq.boxed`) · giá trị liên tục → Canvas · nhiều bước → step. Demo chỉ khi bảng/text không đủ; hệ toán/vật lý ưu tiên demo Canvas để "thấy" hành vi.
- **KaTeX**: TeX ở `data-tex`, render **1 lần** lúc load (static). Bỏ KaTeX khi doc gần như không công thức.
- **Thẩm mỹ**: dark theme, `.reveal` fade-in, header tĩnh + TOC, section cuối `.perf-grid`, responsive.
- **Zero idle cost**: không rAF loop chạy mãi; demo event-driven/static; KaTeX render một lần.

## Cơ chế

**Skeleton + CSS + demo mẫu:** `DOCS_TEMPLATE.html` (cùng thư mục) — copy nguyên, thay `{System}`/`{Project}`, bỏ KaTeX/Prism nếu không dùng. Thêm Prism component theo nhu cầu: `prism-glsl`, `prism-json`, `prism-python`, …

**Demo patterns** — mỗi demo: IIFE wrap, cache `getElementById` đầu IIFE, `addEventListener` (không `onclick`). Code mẫu Pattern A ở cuối `DOCS_TEMPLATE.html`.

| Pattern | Khi nào | Perf |
|---|---|---|
| **A: Canvas + Mouse** | giá trị đổi theo vị trí | pre-render bg, rAF guard |
| **B: Step/Auto** | quá trình rời rạc | DOM-only, `clearInterval` on reset, ≥800ms |
| **C: Input → Transform** | chuyển đổi real-time | compute rẻ → trực tiếp handler |
| **D: Static Graph** | hàm toán học | IIFE 1 lần, không listener |

**Blacklist** — cấm trong draw loop / handler:

| Cấm | Tại sao | Thay bằng |
|---|---|---|
| `ctx.shadowBlur` | Gaussian blur mỗi lần vẽ | radial gradient |
| `createImageData()` mỗi event | alloc W×H×4 bytes | tạo 1 lần, reuse |
| Per-pixel math mỗi mousemove | O(W×H) 60+ lần/giây | pre-render → cached ImageData |
| `mousemove` → vẽ thẳng | vẽ 2–3× giữa 2 frame | rAF guard: scalar coords + dirty flag |
| `ctx.fillStyle='var(--x)'` | Canvas không parse CSS vars | hex: `'#8b949e'` |
| `innerHTML` trong vòng lặp | parser + reflow | `textContent` |
| Quên `cancelAnimationFrame` | rAF chạy tiếp sau khi rời chuột | cancel trong leave handler |

---

## Checklist `.md` + `.html`

Ngầm định: mọi item chịu **10 Nguyên tắc xuyên suốt** và **§Phỏng vấn ngữ cảnh**. Dưới đây chỉ liệt kê điểm kiểm riêng của từng loại.

**`.md`:** data-flow structure · so sánh→bảng✓ · pipeline→ASCII · metrics bảng cuối · đủ để `.html` dựng 100% không cần source.

**`.html`:** 100% nội dung `.md` · single file, TOC khớp, responsive · hạn chế code (API→bảng) · mọi công thức→`.eq` KaTeX (chốt→`.eq.boxed`) · zero idle cost (Canvas: pre-render, rAF guard, hex màu, cancel; không allocate/shadowBlur trong loop).

---

# Phần C — Plan tự triển khai (khi user yêu cầu)

Developer tự code lại để học. Plan **tự chứa**: `§0` dẫn giải toán (mạch Phần A, không nhảy sang doc khác) → các task xếp theo thứ tự phụ thuộc (nền trước, dùng lại sau), mỗi task chỉ cần thứ đã có.

## C.1 — Editor-first trong Unity

Áp **Nguyên tắc 8** vào Unity, khi chạm tới MonoBehaviour / prefab / scene / asset.

Thứ gì gán được lúc authoring thì gán ở Editor — reference kéo thả vào `[SerializeField]`, component add trên prefab, số tinh chỉnh phơi ra Inspector, preset thành ScriptableObject. Đang viết code chỉ để **tìm, nối, hoặc gán** thứ vốn đã tồn tại lúc authoring (`GetComponent`, `Find`, `AddComponent`, `Resources.Load`, hằng số feel hardcode) → đặt sai chỗ.

Lý do gốc: dữ liệu serialize sửa được **không cần compile**, ai cũng chỉnh được, và thiếu thì lộ ra ô trống trong Inspector chứ không nổ giữa gameplay.

Ngoại lệ tự nhiên là thứ chưa tồn tại lúc authoring — object spawn runtime, số lượng động, dữ liệu từ server. Kể cả vậy: wire sẵn trong prefab rồi `Instantiate`, đừng dựng hierarchy bằng code.

Plan chạm scene/prefab thì mô tả thao tác Editor **như một bước thật**, không lặng lẽ thay bằng code cho tiện viết.

## C.2 — Vừa đủ, áp vào code

**Nguyên tắc 6** nói *cái gì mới được thêm*; các dạng hay gặp khi viết code — nhu cầu thật của từng loại: interface cần **implementation thứ hai** · tham số cần **call site truyền khác mặc định** · guard cần **input chạm được biên** · tối ưu cần **hot path đã xác nhận** (§Phỏng vấn) · tách lớp cần **trách nhiệm thật sự khác**, không phải "cho gọn mắt".

`§0` của plan cân độ sâu theo **Nguyên tắc 5** — công thức xấp xỉ đủ dùng cho game feel là mặc định, không tự nâng lên mô hình đầy đủ (Nguyên tắc 10).

**Mỗi task gồm:** Files (path chính xác) · Interfaces (consumes/produces, chữ ký đầy đủ) · bảng "toán→code" (trỏ §0) · bảng "self-doc & tối ưu" (lý do mỗi quyết định) · **code hoàn chỉnh dán-được** (comment trỏ công thức) · **Editor setup** (khi chạm scene/prefab — §C.1) · kiểm chứng bảng input→kỳ vọng (nêu rõ nếu không kèm code test).

**Code — 5 đảm bảo bắt buộc:**

| Đảm bảo | Cụ thể |
|---|---|
| **Vừa đủ** (Nguyên tắc 6) | đơn giản nhất trong các cách **cùng đúng**; mỗi hàm · tham số · nhánh · lớp trừu tượng đều gọi được tên chỗ hỏng nếu xóa đi. |
| **Đúng với công thức đã chốt** | code khớp 100% công thức §0; mỗi nghiệm đã kiểm mốc trước khi vào code; comment trỏ công thức nguồn (`// B = (v₀+ζω₀y₀)/ω_d`). Là "code khớp công thức", **không** phải "công thức phải khớp vật lý" — Nguyên tắc 7. |
| **Tối ưu CPU** — *chỉ ở hot path đã xác nhận (§Phỏng vấn)* | precompute hằng nặng (exp/sqrt/sincos, chia) 1 lần ngoài hot path; chia→nhân; guard thoát sớm; cache trung gian; `AggressiveInlining` wrapper mỏng. **Ngoài hot path: chọn bản dễ đọc nhất.** |
| **Giảm GC** — *bắt buộc ở hot path; ngoài đó chỉ khi không đánh đổi độ rõ* | `struct` thay `class`; `ref`/`in` thay copy; không `new` ref-type/LINQ/closure/string trong hot path; reuse buffer. |
| **Self-document** | tên nói rõ mục đích (`SolveAnalytic`≠`Process`); boolean là câu hỏi (`IsActive`); XML doc ở API public — **`<param>` bắt buộc cho mọi tham số có contract không hiển nhiên** (miền giá trị, đơn vị, ai cấp); comment chỉ nói *tại sao*. |

**Kiểm riêng:** có mục "Ngữ cảnh đã chốt" · **mọi hàm có caller thật hoặc lý do phòng-xa chữ ký** · task chạm scene/prefab có "Editor setup" (§C.1) · code đủ 5 đảm bảo. (Phần còn lại — vừa đủ, độ sâu toán, bằng chứng — theo Nguyên tắc xuyên suốt.)

**Verify công thức ↔ code trước khi chốt** — cách lấy bằng chứng cụ thể cho **Nguyên tắc 9**:
- **Đối chiếu từng số hạng** — mỗi hộp `$$boxed$$` map thẳng 1 dòng code, kiểm từng hệ số/dấu (vd `ẋ_cos=A·e^(−λt)[−λcos−ωsin]` ↔ `env*(-lambda*c - omega*s)`).
- **Kiểm mốc chéo** — giá trị biên trong §0 phải khớp bảng kiểm chứng của task (vd `ẋ(0)=−λA` ↔ `GetVelocity(Cos,·,2,0)=−2`).
- **Đạo hàm số** khi có hàm đạo hàm — `f'(t) ≈ (f(t+h)−f(t−h))/2h`, `h=1e-4`, phải khớp công thức giải tích.
- **Round-trip** khi có cặp converter/overload — `A→B→A` phải về gần chính nó (vd `*HalfLife` ↔ `decay`).
