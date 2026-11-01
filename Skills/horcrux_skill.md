# horcrux_skill.md — Bảng chỉ dẫn skill và tư tưởng thiết kế

Dành cho **game trên Unity**, mang nguyên sang mọi dự án; không mang domain của dự án nào. Áp cho mọi thứ
viết ra: runtime, editor, tooling, script tạm, tài liệu. **Horcrux** là framework dùng chung đi theo mọi dự
án; bộ skill sống trong `Skills/` của nó, cùng `Plans/` ở gốc (`reuse-gate`).

**Ai đọc:** AI agent trước khi làm bất cứ việc gì · developer khi review và khi gõ yêu cầu vào mục cuối.

## Luật đọc khi vào session

1. Đọc **trọn** file này, tới hết mục developer ở cuối.
2. Đọc **trọn** bảy skill nền ở bảng dưới, theo thứ tự bảng, trước khi làm bất cứ việc gì. Không đọc lướt,
   không đọc một phần rồi suy ra phần còn lại.
3. Skill công cụ chỉ đọc khi bảng chỉ dẫn gọi tới. Skill giữ công cụ dựng sẵn và quy trình rẻ token; viết
   script đo mới cho thứ skill đã phủ là lặp tri thức (NT7). Skill thiếu công cụ thì mở rộng công cụ trong
   Horcrux rồi cập nhật skill, không viết bản dùng một lần.
4. Với mỗi tác vụ, tra **bảng chỉ dẫn**: skill ở cột *Bắt buộc* tuân thủ nghiêm ngặt từng luật; cột *Thêm
   khi chạm* áp khi tác vụ chạm tới thứ ghi ở đó. Một tác vụ thuộc nhiều hàng thì gộp các cột.

## Bộ skill

Mỗi skill ở `Skills/<tên>/SKILL.md`. Mỗi luật chỉ định nghĩa ở một skill; skill khác trỏ sang, không chép lại.

| Skill nền | Nói về |
|---|---|
| `workflow` | quy trình một việc: phỏng vấn, đối chiếu với developer, văn phong, ngữ cảnh đã chốt, chưng cất tư tưởng, subagent, nghiệm thu, đề xuất công cụ |
| `reuse-gate` | ba câu trước khi sinh code: có sẵn chưa, có cần không, đặt ở dự án hay Horcrux; bốn bước bản tạm → Plan → developer gõ → chuyển |
| `code-design` | SOLID, bậc cấu trúc, composite, module và phân tầng, hiệu năng bộ nhớ và thời gian, bất biến và guard, naming |
| `async-presentation` | UniTask, token, vòng đời, pool, PrimeTween, Addressables, trình diễn thay đổi cho người chơi |
| `editor-first` | thứ gì làm ở Editor thay vì code, tool Editor IMGUI, import/export |
| `math-physics` | khi nào cần toán, dẫn giải cho hiểu sâu, đối chiếu công thức với code |
| `docs` | `.md`, `.html`, Plan, Manual, kỷ luật viết và bảo trì |

| Skill công cụ | Nói về |
|---|---|
| `feel-tune` | chỉnh, refactor, dò lỗi feeling — tween, trình diễn, nhịp, juice; đo GC, batches lúc diễn |
| `build-size` | build nặng vì sao, giảm, so build trước–sau |

## Bảng chỉ dẫn theo loại việc

| Loại việc | Bắt buộc | Thêm khi chạm |
|---|---|---|
| Mọi tác vụ — đầu và cuối chat | `workflow` | |
| Sinh code mới, hệ mới, tính năng mới | `reuse-gate` → `code-design` | `async-presentation` khi có async, tween, vòng đời · `editor-first` khi có cấu hình, wire, tool |
| Sửa lỗi, refactor code runtime | `code-design` | `async-presentation` |
| Tool Editor, import/export, cấu hình nội dung, setup scene và prefab | `editor-first` | `code-design` cho phần code |
| Hệ có công thức toán, vật lý | `math-physics`, `code-design` | `docs` khi phải dẫn giải |
| Viết hoặc cập nhật `.md`, `.html`, Plan, Manual | `docs` | `math-physics` khi tài liệu có công thức |
| Chỉnh feeling, đo GC, batches lúc trình diễn | `feel-tune` | `async-presentation` |
| Build nặng, giảm size | `build-size` | |
| Orchestrator bơm ngữ cảnh cho subagent | đúng hàng của việc được giao (`workflow` › Subagent) | |

## Ba tầng ràng buộc

| Tầng | Nhận biết | Nghĩa | Agent được phép |
|---|---|---|---|
| **Luật** | mặc định, không đánh dấu | tiêu chí nghiệm thu: nói **cần đạt gì**, không nói làm thế nào | không bỏ; tự chọn cách đạt |
| **Nền tảng** | khối `> **Nền tảng**` | đáp án đã chốt: giới hạn thật của Unity, C#, renderer — hoặc stack mặc định của Horcrux | không đi đường khác; dự án thiếu món nào thì quay về Luật ngay trên khối |
| **Sổ tay** | dòng `**Sổ tay** —` | một cách đã dùng và chạy được, không phải cách duy nhất | thay bằng cách hay hơn, kèm lý do và phép kiểm (NT6) |

**Ký hiệu:** `NT<n>` là nguyên tắc n ở file này. Con trỏ `` `skill` › Mục `` trỏ tới một mục trong skill đó;
`mục X` trỏ trong cùng file. Số hiệu và con trỏ **chỉ sống trong bộ skill**: code comment, tài liệu module,
plan viết lý do bằng nội dung, không bằng con trỏ. NT áp cho mọi việc; skill là cách đạt, tra theo bảng chỉ
dẫn. *Đã sai một lần* là bẫy đã trả giá — ghi **triệu chứng**, không tường thuật.

---

## Bốn ưu tiên đứng trên

Khi phải cân đo, bốn thứ này thắng; hy sinh cái nào thì nói ra tại chỗ kèm giá. Nơi duy nhất định nghĩa
NT1–NT4; câu phân xử giữa chúng nằm ngay trong ô.

| # | Nguyên tắc | Nội dung |
|---|---|---|
| 1 | **Vừa đủ — lõi chạy được trước, hình dạng mở để thêm, tối ưu khi có số đo** | Đơn giản là mặc định; mỗi lớp phức tạp thêm trả bằng **một nhu cầu đang có thật** — "phòng khi", "cho đầy đủ", "chuẩn hơn" không phải nhu cầu. Phép kiểm: *xoá nó đi thì hỏng ở đâu* — không gọi được tên thì bỏ. Giá của một dòng code nằm ở mọi lần đọc sau, đo bằng *số file phải mở để lần hết một luồng* (asset kéo thả cũng tính); agent sinh code gần miễn phí nên hay quên vế này — gốc của over-engineering. Hai bờ vực: **xé vụn** (một tính năng rải nhiều mảnh, không mảnh nào nói được hành vi) và **gom bừa** (một class ôm nhiều lý do thay đổi); tách chỉ theo **lý do thay đổi** (`code-design` › Hình dạng). **Thứ tự làm:** ① lõi chạy được và kiểm được từ input tới kết quả quan sát được → ② ca biên khi có input thật, cấu hình khi có call site → ③ tối ưu *làm code khó đọc hơn* khi có số đo; tối ưu miễn phí theo NT2 làm ngay từ ①. "Lõi trước" cắt theo chiều sâu, không cắt mục đích: mốc không chạy được là thiếu, không phải gọn. **Mở để thêm** là hình dạng, không phải số lượng: bước kế tiếp là *thêm vào*, không đập ra. Chỗ duy nhất đáng phòng xa là thứ **sửa sau rất đắt**: chữ ký bắt buộc, ranh giới trách nhiệm, chiều phụ thuộc, chỗ đặt file; hàm hay tham số tuỳ chọn thêm sau tốn hai phút. Nghiệm thu: gọi được tên bước kế tiếp và chỉ ra nó là "thêm". → `reuse-gate`, `code-design` › Hình dạng |
| 2 | **Hiệu năng runtime — bộ nhớ và thời gian tính, mỗi phép tính khai được nhịp** | Nhịp: *mỗi frame* · *mỗi tương tác* · *mỗi lần dữ liệu đổi*. Hàm chạy lặp phải nói được nó thuộc nhịp nào; đặt ở nhịp nhanh hơn cần thì không gì báo sai, chỉ có hệ chậm dần. **Phân xử với NT1: bản nhanh hơn có khó đọc hơn không?** Không — khai đúng nhịp · không tạo rác ở chỗ chạy lặp · không tính lại thứ không đổi (đầu vào chỉ đổi cùng cấu hình đọc một lần thì kết quả là hằng của phiên: dựng một lần, giữ trong field) — thì **làm luôn, không cần đo**. Có — thêm tầng cache, bẻ cấu trúc dữ liệu, viết tay vòng lặp — thì cần **chỗ đo và số trước–sau**, chỉ ở hot path đã xác nhận. Cách rẻ nhất là làm phép tính **biến mất**, không phải chạy nhanh hơn. → `code-design` › Hiệu năng |
| 3 | **Module hoá — hệ độc lập là mặc định, mang đi được** | Mỗi hệ trả lời: *bê sang dự án sau phải sửa gì?* Mặc định **không sửa gì**: hệ không gọi tên type của dự án, phụ thuộc một chiều **dự án → Horcrux**. Hệ **kết hợp** (dựng trên nhiều hệ, không rời đi một mình) là ngoại lệ phải nêu lý do và đường tách. **Phân xử với NT1: thứ phải thêm là gì?** Chỉ là chiều phụ thuộc và chỗ đặt file → làm ngay, gần miễn phí lúc đầu, sửa sau đập cả cây. Là interface, adapter, tham số cho người dùng *chưa có* → phòng xa, bỏ. Hệ quả: Horcrux **tích luỹ qua các dự án** — làm mới hay cải thiện ở đó là mọi dự án sau hưởng — nên chức năng mang đi được thuộc về Horcrux; bản riêng trong dự án chỉ là **bản tạm** chờ chuyển (`reuse-gate`). → `code-design` › Module |
| 4 | **Editor-first — code lo hành vi, Editor lo cấu hình và kết nối** | Thứ gì **làm chắc chắn được lúc authoring** thì không sinh code cho nó; phép kiểm là *chắc chắn*, không phải *tiện*. Code chỉ để tìm, nối, gán thứ đã tồn tại lúc authoring là đặt sai chỗ. Việc bắt buộc trước khi build là **một bước trong tài liệu**, không phải code canh lúc chạy; **tự tạo tệ hơn tự canh** vì nó giấu setup thiếu. Ngoại lệ nằm ở *thời điểm biết được* (spawn runtime, số lượng động, dữ liệu từ server), không ở độ tiện khi viết code. **Phân xử với "wiring trong asset không grep được": số chỗ phải nối là hằng số nhỏ trong một asset, hay tăng theo số instance?** Hằng nhỏ → kéo thả, ô trống chắc chắn có người nhìn. Tăng theo instance → đăng ký bằng code, gom n ca hỏng-im-lặng thành một dòng đỏ lúc boot. → `editor-first` |

---

## Nguyên tắc còn lại

Nơi duy nhất định nghĩa NT5–NT10; mỗi ô là định nghĩa và phép kiểm.

| # | Nguyên tắc | Nội dung |
|---|---|---|
| 5 | **Hỏi đúng lúc, tự quyết đúng chỗ** | Thiếu ngữ cảnh thì hỏi, không đoán rồi làm. Buộc giả định thì ghi `Giả định (cần xác nhận): …` **tại chỗ dùng**; thao tác khó đảo ngược luôn hỏi trước, nhãn giả định không thay được xác nhận. Phạm vi hiện tại chặn hướng phát triển thật thì nêu kèm giá; mở rộng "cho đầy đủ" thì không (NT1). Quyết định **thuộc developer** thì thu hẹp lựa chọn và nêu giá, không chốt hộ: phạm vi · tên và thuật ngữ của hệ · ba ranh giới cứng của NT6 · chỗ đặt dự án hay Horcrux (`reuse-gate`) · thứ developer đã quyết. → `workflow` › Phỏng vấn |
| 6 | **Phạm vi bàn được, cách làm luôn mở** | Tiêu chí đã chốt thì cách đạt là của người triển khai: thấy cách cùng tiêu chí mà đơn giản, nhanh, rõ hơn thì dùng, kèm lý do và phép kiểm (NT8). **Ranh giới cứng** — tự do chỉ khi cả ba không đổi: **hành vi quan sát được** (kể cả kết quả ngẫu nhiên theo seed) · **dữ liệu ghi ra** (format lẫn giá trị) · **API công khai**. Đụng một là đổi phạm vi, phải hỏi (NT5). Im lặng chọn món trong Sổ tay khi biết có cách tốt hơn là vi phạm. |
| 7 | **Có sẵn thì dùng, không lặp tri thức** | *Code:* trước khi viết, **khảo sát đã có chưa** theo thứ tự ở `reuse-gate` — có thì dùng lại, Horcrux ưu tiên; gần đúng thì mở rộng không sửa cái cũ; phải bẻ cong bài toán cho vừa thì viết mới. Có người dùng thứ hai thì đề xuất nâng thành tái sử dụng (`code-design` › Module). *Tài liệu:* một khái niệm giải thích một nơi, sau đó trỏ về. Áp cho **tri thức trùng**, không áp cho **code trông giống**. Phép thử: *hai chỗ có cùng lý do thay đổi không?* — cùng → gộp · khác → **để lặp**, vì gộp là trói hai nghiệp vụ độc lập rồi hàm chung mọc tham số và nhánh · chưa chắc → để lặp trước: lặp rồi gộp rẻ, trừu tượng sai thì mọi caller đập · mấp mé → hỏi. Ngoại lệ bắt buộc gộp: **hai bản buộc khớp nhau ở runtime** (`code-design` › Bất biến). |
| 8 | **Bằng chứng, không khẳng định suông** | Mọi "tại sao" kèm phép kiểm tái lập được; công thức chốt phải kiểm mốc; code đối chiếu công thức trước khi chốt. Không viết "đã đúng", "đã tối ưu" thiếu mốc, số đo, hay phép thử chạy lại được. **Bản trong trí nhớ, trong prompt, trong lượt trước là bản chết:** tên file, chữ ký, hằng số, nhãn UI, ví dụ, phát hiện review đều **mở file hiện tại đối chiếu** trước khi ghi, kể cả khi vừa viết chính dòng đó — nguồn sai nhiều nhất của tài liệu và review. **Bằng chứng cùng loại với tiêu chí**, và tiêu chí cao nhất của game là **cảm giác chơi**: công thức "sai sách" mà chơi đã tay thì đúng; toán là công cụ đạt cảm giác. Cảm giác chỉ nghiệm thu bằng chơi thử, và chỉ developer chơi được. → `workflow` › Nghiệm thu |
| 9 | **Config chưa chốt thì không phải mốc · chuẩn hoá thì bỏ điểm neo** | Số trong asset, bảng cấu hình, cấu hình từ xa là **nháp của developer**, thành mốc khi developer nói chốt. Đổi đơn vị, trục, công thức: không tự ánh xạ giá trị cũ, không tự dựng đối chiếu trước–sau để chứng minh "cảm giác không đổi" — đó là đóng băng nháp thành chuẩn. Việc phải làm: giữ **hành vi thuật toán** bất biến, chứng minh bằng mốc (NT8), rồi hỏi giá trị cũ là nháp hay mốc (NT5). Số phải so được giữa các ngữ cảnh khác cỡ thì chuẩn hoá là **bỏ** neo, không chọn neo tốt hơn; **mẫu số là đại lượng gốc**, không phải đại lượng đã bị núm khác nhân vào. |
| 10 | **Độ dài canh theo việc người đọc phải làm · kết luận trước · chỉ một hệ** | Một luật cho đối thoại lẫn tài liệu. **Sàn:** đủ để người đọc tự làm việc của họ — nêu *cái đang có*, *cái sẽ đổi*, *cái đánh đổi*, kèm số hoặc đường dẫn để kiểm; nén dưới sàn chỉ tạo thêm một vòng hỏi lại. **Trần:** *dòng này đổi gì trong việc người đọc đang làm?* — không đổi gì thì cắt, kể cả khi đúng. Dạng mật độ cao nhất cho loại nội dung: bảng cho so sánh, diagram cho luồng, công thức cho định lượng, một câu cho trực giác. Trong một mục **kết luận trước, lý do sau**; **một khẳng định = một câu lý do** — phải viết cả đoạn là chưa hiểu đủ để nén hoặc đang biện minh cho thứ không cần có. Giữa các mục: dễ→khó, tổng quan→chi tiết; đánh số khi là quy trình. **Đổi rồi thì chỉ còn một hệ:** code và tài liệu nói hoàn toàn bằng hệ mới — không "trước đây là", không hai đơn vị song song, không giữ tên cũ làm cầu. Giữ vết cũ chỉ khi gọi tên được người dùng thật: payload cũ phải deserialize (`code-design` › Naming) · developer yêu cầu. Lịch sử ở commit. → `workflow` › Văn phong, `docs` › Kỷ luật viết |

---

## Thứ tự ưu tiên khi va chạm

1. **Yêu cầu của developer** ở mục cuối file này. Các yêu cầu trong đó **ngang nhau**; hai yêu cầu va nhau
   thì hỏi developer (NT5), không tự chọn.
2. **NT1–NT10** ở file này; giữa chúng phân xử theo câu viết sẵn trong ô.
3. **Luật trong skill.** Hai skill nói khác nhau về cùng một việc là lỗi của bộ skill: làm theo bản hợp NT
   hơn, nêu ra để developer phân xử (`workflow` › Chưng cất), không tự sửa cả hai.

## Yêu cầu của developer

Mục này **developer tự gõ**. Agent **không sửa, không định dạng lại, không chèn, không xoá** bất kỳ ký tự nào
giữa hai dấu dưới — kể cả khi thấy lỗi chính tả, dòng trùng, hay mâu thuẫn với skill: chỉ báo lại trong chat.
Chưng cất tư tưởng (`workflow` › Chưng cất) ghi vào skill nền, không bao giờ ghi vào đây. Mọi yêu cầu trong
mục ngang nhau và đứng trên mọi skill.

<!-- DEVELOPER-ONLY: START — agent không sửa gì từ dòng này tới END -->

- 

<!-- DEVELOPER-ONLY: END -->
