# horcrux_skill.md — bảng chỉ dẫn và tư tưởng thiết kế

Bộ skill phát triển hệ thống game Unity, mang nguyên sang mọi dự án, không mang domain dự án nào; áp cho mọi thứ agent viết
ra (runtime, Editor, tooling, script tạm, tài liệu). **Horcrux** là framework dùng chung đi theo mọi dự án; đường dẫn tính
từ gốc Horcrux: skill `Skills/`, Plan `Plans/`, tài liệu hệ `docs/Editor/` và `docs/Runtime/`.

## Luật đọc

1. Vào session: đọc **trọn** file này rồi **trọn** bảy skill nền theo thứ tự bảng, trước mọi việc; không đọc lướt.
2. Skill công cụ chỉ đọc khi bảng chỉ dẫn gọi. Thiếu công cụ thì mở rộng công cụ trong Horcrux rồi cập nhật skill, không
   viết script dùng một lần (NT7).
3. Mỗi tác vụ tra bảng chỉ dẫn: cột *Bắt buộc* tuân thủ từng luật, *Thêm khi chạm* áp khi chạm tới; nhiều hàng thì gộp.

## Bộ skill

`Skills/<tên>/SKILL.md`. Mỗi luật định nghĩa ở **một** skill; nơi khác trỏ sang, không chép lại.

| Skill nền | Nói về |
|---|---|
| `workflow` | phỏng vấn, đối chiếu, văn phong, ngữ cảnh đã chốt, chưng cất, subagent, nghiệm thu, đề xuất công cụ |
| `reuse-gate` | có sẵn chưa, có cần không, đặt ở dự án hay Horcrux |
| `code-design` | SOLID, bậc cấu trúc, module, hiệu năng, bất biến và guard, naming |
| `async-presentation` | UniTask, token, vòng đời, pool, PrimeTween, Addressables, trình diễn |
| `editor-first` | việc của Editor thay vì code, tool Editor, import/export |
| `math-physics` | khi nào cần toán, dẫn giải, đối chiếu công thức với code |
| `docs` | `.md`, `.html`, Plan, Manual, skill, kỷ luật viết |

| Skill công cụ | Nói về |
|---|---|
| `feel-tune` | chỉnh, refactor, dò lỗi feeling; đo GC, batches lúc trình diễn |
| `build-size` | build nặng vì sao, giảm, so trước–sau |
| `compile-check` | kiểm sau mỗi task sửa code bằng một lệnh |

## Bảng chỉ dẫn

| Loại việc | Bắt buộc | Thêm khi chạm |
|---|---|---|
| Mọi tác vụ — đầu và cuối chat | `workflow` | |
| Sinh code mới, hệ mới, tính năng mới | `reuse-gate` → `code-design` | `async-presentation` khi có async, tween, vòng đời · `editor-first` khi có cấu hình, wire, tool |
| Sửa lỗi, refactor code runtime | `code-design` | `async-presentation` |
| Tool Editor, import/export, cấu hình nội dung, setup scene và prefab | `editor-first` | `code-design` |
| Hệ có công thức toán, vật lý | `math-physics`, `code-design` | `docs` khi phải dẫn giải |
| Viết hoặc cập nhật tài liệu, Plan, Manual, skill | `docs` | `math-physics` khi có công thức |
| Chỉnh feeling, đo GC và batches lúc trình diễn | `feel-tune` | `async-presentation` |
| Build nặng, giảm size | `build-size` | |
| Kết thúc task sửa code C#, trước khi báo xong hay commit | `compile-check` | |
| Orchestrator bơm ngữ cảnh cho subagent | hàng của việc được giao (`workflow` › Subagent) | |

## Ba tầng ràng buộc

| Tầng | Nhận biết | Nghĩa | Agent được phép |
|---|---|---|---|
| **Luật** | mặc định | tiêu chí nghiệm thu: **cần đạt gì** | không bỏ; tự chọn cách đạt |
| **Nền tảng** | khối `> **Nền tảng**` | giới hạn thật của Unity, C#, renderer, hoặc stack Horcrux | không đi đường khác; dự án thiếu món nào thì quay về Luật |
| **Sổ tay** | `**Sổ tay** —` | một cách đã chạy được | thay bằng cách hay hơn, kèm lý do và phép kiểm (NT6) |

**Stack Horcrux:** InitArgs, UniTask, PrimeTween, Addressables. Công cụ ngoài stack (SRDebugger, Odin…) chỉ ghi dạng
*"khi dự án dùng X"*. **Ký hiệu:** `NT<n>` ở file này · `` `skill` › Mục `` trỏ sang skill khác · `mục X` trong cùng file.
Số hiệu và con trỏ chỉ sống trong bộ skill; code comment, tài liệu, Plan viết lý do bằng nội dung. *Đã sai một lần* là bẫy
đã trả giá — ghi **triệu chứng**, không tường thuật.

---

## Bốn ưu tiên khi cân đo

Khi phải đánh đổi, bốn thứ này thắng; hy sinh cái nào thì nói tại chỗ kèm giá.

**NT1 · Vừa đủ — lõi chạy được trước, mở để thêm, tối ưu khi có số đo.** Mỗi lớp phức tạp trả bằng **một nhu cầu đang có
thật**; "phòng khi", "cho đầy đủ", "chuẩn hơn" không phải nhu cầu. Phép kiểm: *xoá đi thì hỏng ở đâu?* Giá một dòng code nằm
ở mọi lần đọc sau — *số file phải mở để lần hết một luồng*, asset kéo thả cũng tính; agent sinh code gần miễn phí nên hay
quên vế này. Hai bờ vực: **xé vụn** (tính năng rải nhiều mảnh, không mảnh nào nói được hành vi) và **gom bừa** (class ôm
nhiều lý do thay đổi). **Thứ tự:** ① lõi chạy được, kiểm được từ input tới kết quả quan sát được (tối ưu miễn phí của NT2
làm luôn) → ② ca biên khi có input thật, cấu hình khi có call site → ③ tối ưu làm khó đọc khi có số đo. Lõi trước cắt
chiều sâu, không cắt mục đích. **Mở để thêm** là hình dạng: bước kế tiếp là *thêm vào*, không đập ra — chỉ phòng xa thứ sửa
sau rất đắt (chữ ký bắt buộc, ranh giới trách nhiệm, chiều phụ thuộc, chỗ đặt file). Nghiệm thu: gọi được tên bước kế
tiếp và nó là "thêm".

**NT2 · Hiệu năng runtime — bộ nhớ và thời gian, mỗi phép tính khai được nhịp:** *mỗi frame* · *mỗi tương tác* · *mỗi lần
dữ liệu đổi*. Đặt ở nhịp nhanh hơn cần thì không gì báo sai, chỉ có hệ chậm dần. **Phân xử với NT1: bản nhanh có khó đọc
hơn không?** Không (đúng nhịp, không rác ở chỗ lặp, không tính lại thứ không đổi — kết quả chỉ phụ thuộc cấu hình đọc một
lần là hằng của phiên, dựng một lần giữ trong field) → **làm luôn, không cần đo**. Có (cache, bẻ cấu trúc dữ liệu, vòng lặp
viết tay) → cần **chỗ đo và số trước–sau**, chỉ ở hot path đã xác nhận. Rẻ nhất là làm phép tính **biến mất**.

**NT3 · Module hoá — hệ độc lập là mặc định, mang đi được.** *Bê sang dự án sau phải sửa gì?* Mặc định **không gì**: không
gọi tên type dự án, phụ thuộc một chiều **dự án → Horcrux**. Hệ **kết hợp** là ngoại lệ phải nêu lý do và đường tách.
**Phân xử với NT1: thứ phải thêm là gì?** Chiều phụ thuộc, chỗ đặt file → làm ngay (sửa sau đập cả cây). Interface,
adapter, tham số cho người dùng *chưa có* → bỏ. Horcrux tích luỹ qua các dự án: chức năng mang đi được thuộc về Horcrux,
bản trong dự án là bản tạm. Hai câu tách bạch: **chỗ đặt** xét theo *mang đi được không*; **trừu tượng hoá** xét theo
*người dùng thứ hai đang có thật*.

**NT4 · Editor-first — code lo hành vi, Editor lo cấu hình và kết nối.** Thứ **làm chắc chắn được lúc authoring** thì không
sinh code; phép kiểm là *chắc chắn*, không phải *tiện*. Code chỉ để tìm, nối, gán thứ đã có lúc authoring là đặt sai chỗ.
Việc bắt buộc trước khi build là **một bước trong tài liệu**, không phải code canh; **tự tạo tệ hơn tự canh** vì giấu setup
thiếu. Ngoại lệ ở *thời điểm biết được* (spawn runtime, số lượng động, dữ liệu server). **Phân xử với "wiring trong asset
không grep được": số chỗ nối là hằng nhỏ hay tăng theo instance?** Hằng nhỏ → kéo thả. Tăng theo instance → đăng ký bằng
code, gom n ca hỏng-im-lặng thành một dòng đỏ lúc boot.

---

## Nguyên tắc làm việc

**NT5 · Hỏi tới khi rõ, không đoán rồi làm.** Yêu cầu chưa rõ hay trình bày thiếu thì **hỏi — nhiều câu, nhiều vòng** — tới
khi viết lại được **kịch bản** developer xác nhận (ai làm gì, thấy gì, ca biên, nghiệm thu bằng gì). Kịch bản đã chốt là
**hợp đồng khi triển khai**; cần lệch thì hỏi trước. Giả định chỉ vá khe hở **nhỏ, đảo ngược được**, ghi
`Giả định (cần xác nhận): …` tại chỗ dùng; thao tác khó đảo ngược luôn hỏi. Quyết định **thuộc developer** thì thu hẹp và
nêu giá, không chốt hộ: phạm vi · tên và thuật ngữ · ba ranh giới cứng của NT6 · chỗ đặt dự án hay Horcrux · thứ developer
đã quyết.

**NT6 · Phạm vi bàn được, cách làm luôn mở.** Tiêu chí đã chốt thì cách đạt là của người triển khai; cách đơn giản, nhanh,
rõ hơn thì dùng, kèm lý do và phép kiểm. **Ranh giới cứng** — tự do chỉ khi cả ba không đổi: **hành vi quan sát được** (kể
cả ngẫu nhiên theo seed) · **dữ liệu ghi ra** (format, giá trị) · **API công khai**; đụng một là đổi phạm vi, phải hỏi.
Biết cách tốt hơn mà im lặng chọn Sổ tay là vi phạm.

**NT7 · Có sẵn thì dùng, không lặp tri thức.** Khảo sát trước khi viết (`reuse-gate`), Horcrux ưu tiên; gần đúng thì mở
rộng không sửa cái cũ; phải bẻ cong bài toán thì viết mới. Tài liệu: một khái niệm giải thích một nơi. Áp cho **tri thức
trùng**, không cho **code trông giống** — *cùng lý do thay đổi?* cùng → gộp · khác → **để lặp** (gộp là trói hai nghiệp vụ)
· chưa chắc → để lặp, lặp rồi gộp rẻ hơn trừu tượng sai · mấp mé → hỏi. Ngoại lệ bắt buộc gộp: **hai bản buộc khớp ở
runtime** (`code-design` › Bất biến).

**NT8 · Bằng chứng, không khẳng định suông.** Mọi "tại sao" kèm phép kiểm tái lập được; không viết "đã đúng", "đã tối ưu"
thiếu mốc hay số đo. **Bản trong trí nhớ, prompt, lượt trước là bản chết:** tên file, chữ ký, hằng số, nhãn UI, ví dụ, phát
hiện review đều **mở file hiện tại đối chiếu** trước khi ghi. **Bằng chứng cùng loại với tiêu chí**; tiêu chí cao nhất là
**cảm giác chơi** — công thức "sai sách" mà chơi đã tay thì đúng. Cảm giác chỉ nghiệm thu bằng chơi thử, và chỉ developer
chơi được (`workflow` › Nghiệm thu).

**NT9 · Config chưa chốt thì không phải mốc · chuẩn hoá thì bỏ điểm neo.** Số trong asset, bảng cấu hình, remote config là
**nháp của developer** tới khi developer nói chốt. Đổi đơn vị, trục, công thức: không tự ánh xạ giá trị cũ, không tự dựng
đối chiếu để chứng minh "cảm giác không đổi"; giữ **hành vi thuật toán** bất biến, chứng minh bằng mốc, rồi hỏi giá trị cũ
là nháp hay mốc. Chuẩn hoá là **bỏ** neo, không chọn neo tốt hơn; **mẫu số là đại lượng gốc**, không phải đại lượng đã bị
núm khác nhân vào.

**NT10 · Độ dài canh theo việc người đọc phải làm · kết luận trước · chỉ một hệ.** **Sàn:** đủ để người đọc tự làm việc của
họ — *đang có*, *sẽ đổi*, *đánh đổi*, kèm số hay đường dẫn để kiểm. **Trần:** *dòng này đổi gì trong việc người đọc?* —
không thì cắt. Bảng cho so sánh, diagram cho luồng, công thức cho định lượng, một câu cho trực giác. **Kết luận trước, lý do
sau; một khẳng định = một câu lý do.** Dễ → khó, tổng quan → chi tiết. **Đổi rồi thì chỉ còn một hệ:** không "trước đây
là", không hai đơn vị song song, không tên cũ làm cầu — trừ khi gọi tên được người dùng thật (payload cũ phải deserialize,
developer yêu cầu). Lịch sử ở commit.

---

## Thứ tự ưu tiên khi va chạm

1. **Yêu cầu của developer** ở mục cuối; các yêu cầu ngang nhau, va nhau thì hỏi.
2. **NT1–NT10**, phân xử theo câu viết sẵn trong nguyên tắc.
3. **Luật trong skill.** Hai skill nói khác nhau là lỗi của bộ skill: làm theo bản hợp NT hơn, nêu để developer phân xử,
   không tự sửa cả hai.

## Yêu cầu của developer

Mục này **developer tự gõ**. Agent **không sửa, không định dạng lại, không chèn, không xoá** ký tự nào giữa hai dấu dưới —
kể cả khi thấy lỗi, dòng trùng, mâu thuẫn: chỉ báo trong chat. Chưng cất không bao giờ ghi vào đây.

<!-- DEVELOPER-ONLY: START — agent không sửa gì từ dòng này tới END -->

- 

<!-- DEVELOPER-ONLY: END -->
