# MY_SKILL.md — Tư tưởng thiết kế hệ thống & quy trình làm việc với AI

Dành cho **game trên Unity**. Tư tưởng, không phải tài liệu của một dự án — mang sang dự án nào cũng
dùng nguyên. Áp cho mọi thứ viết ra: runtime, editor, tooling, script tạm, tài liệu.

**Ai đọc:** AI agent, trước khi làm bất cứ việc gì · developer, khi review output.
**Đi kèm:** `DOCS_TEMPLATE.html` (§5.2). **Horcrux** là framework dùng chung đi theo mọi dự án; file
này sống trong đó. Chỉ đọc được một mục thì đọc **§0**.

## Ba tầng ràng buộc

| Tầng | Nhận biết | Nghĩa | AI được phép |
|---|---|---|---|
| **Luật** | *mặc định*, không đánh dấu | tiêu chí nghiệm thu: nói **cần đạt gì**, không nói **làm thế nào** | không bỏ; tự chọn cách đạt |
| **Nền tảng** | khối `> **Nền tảng**` | đáp án đã chốt: **giới hạn thật** của Unity, C#, browser, renderer — hoặc **stack mặc định** của Horcrux | không đi đường khác. Dự án thiếu món nào thì quay về tiêu chí ở phần Luật ngay trên khối |
| **Sổ tay** | dòng `**Sổ tay** —` | một cách đã dùng và chạy được; không phải cách duy nhất | thay bằng cách hay hơn, kèm lý do và phép kiểm (NT6) |

**Ký hiệu:** `NT<n>` là nguyên tắc số n ở §0–§1 · `§x` là mục trong file này. **Số hiệu chỉ sống trong
chính file này** — code comment, tài liệu module và plan viết lý do bằng **nội dung**, không bằng **con
trỏ** ("vi phạm §3.4"): người đọc tài liệu đó không mở file này ra tra, và con trỏ mục nát âm thầm khi
cấu trúc đổi.

§0 và §1 là nền, áp cho mọi việc; §2–§5 là cách đạt cho từng loại việc, tra khi chạm tới. Sổ tay nào
ghi **điều kiện áp dụng** thì ngoài điều kiện đó là vô nghĩa. *"Đã sai một lần"* là bẫy đã trả giá —
tri thức đắt nhất và không đọc ra được từ code; mỗi dòng ghi **triệu chứng**, không tường thuật.

---

# §0 — Bốn ưu tiên đứng trên

Khi phải cân đo, bốn thứ này thắng. **Không đánh đổi âm thầm** — hy sinh cái nào thì nói ra tại chỗ và
nói giá. Nơi **duy nhất** định nghĩa NT1–NT4; các mục sau chỉ trỏ về. Ô nào có vẻ đá nhau thì câu phân
xử nằm ngay trong ô.

| # | Nguyên tắc | Nội dung |
|---|---|---|
| 1 | **Vừa đủ — lõi chạy được trước, hình dạng mở để thêm, tối ưu khi có số đo** | **Đơn giản là mặc định.** Mỗi lớp phức tạp thêm phải trả bằng **một nhu cầu đang có thật** — "phòng khi cần", "cho đầy đủ", "chuẩn hơn" không phải nhu cầu. **Phép kiểm:** *xoá nó đi thì hỏng ở đâu* — không gọi được tên chỗ hỏng thì bỏ. **Giá của một dòng code nằm ở mọi lần đọc sau**, không ở lúc viết; agent sinh code gần như miễn phí nên hay quên vế này — đó là gốc của over-engineering. Chi phí đọc đo bằng *số file phải mở để lần hết một luồng* (asset kéo thả cũng tính, §3.6). Hai bờ vực: **xé vụn** (một tính năng rải qua nhiều mảnh, không mảnh nào nói được hành vi) và **gom bừa** (một class ôm nhiều lý do thay đổi) — tách chỉ theo **lý do thay đổi**, không theo "cho gọn mắt" hay "cho đúng pattern" (§3.1). **Thứ tự làm:** ① lõi chạy được và kiểm được từ input tới kết quả quan sát được → ② ca biên khi có input thật, cấu hình khi có call site → ③ tối ưu khi có số đo (NT2). "Lõi trước" cắt theo **chiều sâu**, không cắt vào mục đích: giao mốc không chạy được là thiếu, không phải gọn. **Mở đường mai** là hình dạng, không phải số lượng: bước kế tiếp là **thêm vào**, không đập ra làm lại. **Chỗ duy nhất đáng phòng xa là thứ sửa sau rất đắt**: chữ ký, ranh giới trách nhiệm, chiều phụ thuộc, chỗ đặt file — hàm thêm sau tốn hai phút, chữ ký sai sửa sau đập mọi caller. **Nghiệm thu:** gọi được tên bước kế tiếp và chỉ ra nó là "thêm" chứ không phải "sửa". → §2.4, §3.1 |
| 2 | **Hiệu năng runtime — mỗi phép tính khai được nhịp của nó** | Nhịp là *mỗi frame* · *mỗi tương tác* · *mỗi lần dữ liệu đổi*. Hàm chạy lặp phải trả lời được nó thuộc nhịp nào; đặt ở nhịp nhanh hơn mức cần thì **không có gì báo sai**, chỉ có hệ chậm dần. **Câu phân xử với NT1: bản nhanh hơn có khó đọc hơn không?** Không — khai đúng nhịp · **không tạo rác ở chỗ chạy lặp** · **không tính lại thứ không đổi** (đầu vào chỉ đổi cùng cấu hình đọc một lần thì kết quả là hằng của phiên — dựng một lần, giữ trong field) — thì **làm luôn ở mọi nhịp, không cần đo**. Có — thêm tầng cache, viết tay vòng lặp, bẻ cấu trúc dữ liệu — thì phải có **chỗ đo và số trước–sau**, chỉ ở hot path đã xác nhận. Cách rẻ nhất thường là **làm phép tính biến mất**, không phải làm nó chạy nhanh hơn. → §3.3 |
| 3 | **Module hoá — hệ độc lập là mặc định, mang đi được** | Mỗi hệ trả lời được: *bê sang dự án sau thì phải sửa gì?* Mặc định là **không sửa gì** — hệ không gọi tên type của dự án, phụ thuộc đi **một chiều: dự án → Horcrux**. **Hệ kết hợp** (dựng trên nhiều hệ khác nên không rời đi một mình được) là ngoại lệ phải gọi tên lý do và chỉ ra đường tách. **Câu phân xử với NT1: thứ phải thêm là gì?** Chỉ là **chiều phụ thuộc** và **chỗ đặt file** thì làm ngay — gần như miễn phí lúc đầu, sửa sau thì đập cả cây. Là một interface, một adapter, một tham số cho người dùng **chưa có** thì là phòng xa — bỏ. **Hệ quả:** Horcrux là package **tích luỹ qua các dự án** — mỗi lần làm mới hay cải thiện ở đó là mọi dự án sau hưởng — nên chức năng mang đi được thì **thuộc về Horcrux**, và cải thiện bản Horcrux luôn đứng trước viết bản riêng trong dự án (§2.4). → §3.2 |
| 4 | **Editor-first — code lo hành vi, Editor lo cấu hình và kết nối** | Thứ gì **làm chắc chắn được lúc authoring** thì không sinh code cho nó. **Phép kiểm là *chắc chắn*, không phải *tiện*.** Đang viết code chỉ để **tìm, nối, hoặc gán** thứ vốn đã tồn tại lúc authoring thì code đó đặt sai chỗ. Việc bắt buộc làm trước khi build là **một bước ghi trong tài liệu**, không phải code canh lúc chạy — và **tự tạo tệ hơn tự canh**, vì nó giấu việc setup còn thiếu. Ngoại lệ nằm ở *thời điểm biết được* (spawn runtime, số lượng động, dữ liệu từ server), không ở *độ tiện khi viết code*. **Câu phân xử với "wiring trong asset không grep được": số chỗ phải nối là hằng số nhỏ trong cùng một asset, hay tăng theo số instance?** Hằng số nhỏ → kéo thả, ô trống chắc chắn có người nhìn vào. Tăng theo instance → đăng ký bằng code, gom n ca hỏng-im-lặng thành **một** dòng đỏ lúc boot. → §3.6 |

---

# §1 — Nguyên tắc còn lại

Nơi **duy nhất** định nghĩa NT5–NT10. Mỗi ô là *định nghĩa và phép kiểm*; dẫn giải ở mục § cuối ô.

| # | Nguyên tắc | Nội dung |
|---|---|---|
| 5 | **Hỏi đúng lúc, tự quyết đúng chỗ** | Thiếu ngữ cảnh thì **hỏi**, không đoán rồi làm. Buộc giả định thì ghi `Giả định (cần xác nhận): …` **tại chỗ dùng**. **Thao tác khó đảo ngược thì luôn hỏi trước khi chạy** — nhãn giả định không thay được xác nhận. Phạm vi hiện tại **chặn** hướng phát triển thật thì nêu ra **kèm giá**; mở rộng vì "cho đầy đủ" thì không (NT1). Quyết định **thuộc developer** thì thu hẹp lựa chọn và nêu giá, không chốt hộ: phạm vi · **tên và thuật ngữ của hệ** · ba thứ ở ranh giới cứng của NT6 · chỗ đặt dự án hay Horcrux (§2.4) · thứ developer đã quyết rồi. → §2.1 |
| 6 | **Phạm vi bàn được, cách làm luôn mở** | Tiêu chí đã chốt thì cách đạt là việc của người triển khai: thấy cách **cùng tiêu chí** mà đơn giản, nhanh, hoặc rõ hơn thì **dùng nó**, kèm lý do và phép kiểm (NT8). **Ranh giới cứng** — tự do chỉ khi cả ba không đổi: **hành vi quan sát được** (kể cả kết quả ngẫu nhiên theo seed) · **dữ liệu ghi ra** (format lẫn giá trị) · **API công khai**. Đụng một trong ba là đổi phạm vi, phải hỏi (NT5). Im lặng chọn món trong Sổ tay khi biết có cách tốt hơn là vi phạm. |
| 7 | **Có sẵn thì dùng, không lặp tri thức** | *Code:* trước khi viết, **khảo sát đã có chưa** — trong hệ đang chạm, trong Horcrux, trong toàn bộ dự án, trong package đã cài — có thì dùng lại, Horcrux ưu tiên hơn; gần đúng thì mở rộng không sửa cái cũ; phải bẻ cong bài toán cho vừa nó thì viết mới (§2.4). Có người dùng thứ hai thì đề xuất nâng thành tái sử dụng (§3.2). *Tài liệu:* một khái niệm giải thích một nơi, sau đó trỏ về. Áp cho **tri thức trùng nhau**, không áp cho **code trông giống nhau**. Phép thử: *hai chỗ có cùng lý do thay đổi không?* — **cùng** → gộp · **khác** → **để lặp**, vì gộp là trói hai nghiệp vụ độc lập rồi hàm chung mọc tham số và nhánh riêng · **chưa chắc** → để lặp trước: lặp rồi gộp sau thì rẻ, trừu tượng hoá sai thì mọi caller phải đập · **mấp mé** → hỏi developer. Ngoại lệ không được "để lặp": **hai bản buộc khớp nhau ở runtime** — gộp là **bắt buộc** kể cả khi công thức hiển nhiên (§3.4). |
| 8 | **Bằng chứng, không khẳng định suông** | Mọi "tại sao" kèm phép kiểm **tái lập được**; công thức chốt phải **kiểm mốc**; code đối chiếu với công thức trước khi chốt. Không viết "đã đúng", "đã tối ưu" mà thiếu mốc, số đo, hoặc phép thử người đọc chạy lại được. **Bản trong trí nhớ, trong prompt, trong lượt trước là bản chết:** mọi tên file, chữ ký, hằng số, nhãn UI, ví dụ minh hoạ, phát hiện review đều **mở file hiện tại đối chiếu lại** trước khi ghi — kể cả khi vừa viết chính dòng đó. Nguồn sai nhiều nhất của tài liệu và của review. **Bằng chứng phải cùng loại với tiêu chí** — và tiêu chí cao nhất của game là **cảm giác chơi**: công thức "sai sách" mà chơi đã tay thì **đúng**; toán là công cụ đạt cảm giác, không phải mục tiêu. Cảm giác chơi chỉ nghiệm thu bằng **chơi thử**, và chỉ developer chơi được. → §2.8 |
| 9 | **Config chưa chốt thì không phải mốc · chuẩn hoá thì bỏ điểm neo** | Số mặc định trong asset, bảng cấu hình, cấu hình từ xa là **bản nháp của developer**, chỉ thành mốc khi developer nói đã chốt. Đổi đơn vị, trục, công thức: **không tự ánh xạ giá trị cũ**, không tự dựng đối chiếu trước–sau để chứng minh "cảm giác không đổi" — đó là đóng băng nháp thành chuẩn. Việc phải làm: giữ **hành vi thuật toán** bất biến, chứng minh bằng mốc (NT8), rồi **hỏi** giá trị cũ là nháp hay mốc (NT5). *Khi một số phải so được giữa các ngữ cảnh khác cỡ:* chuẩn hoá là **bỏ** điểm neo, không phải chọn neo tốt hơn. **Mẫu số là đại lượng gốc**, không phải đại lượng đã bị một núm khác nhân vào — chia cho bản đã nhân biến núm đó thành hệ số âm thầm. |
| 10 | **Độ dài canh theo việc người đọc phải làm · kết luận trước · chỉ một hệ** | Một luật cho cả đối thoại lẫn tài liệu. **Sàn:** đủ dữ kiện để người đọc **tự làm được việc của họ** — developer tự phân tích lại một quyết định, người đọc tài liệu tự dựng hoặc sửa hệ — nêu *cái đang có*, *cái sẽ đổi*, *cái đánh đổi*, kèm số hoặc đường dẫn để kiểm; nén dưới sàn chỉ tạo thêm một vòng hỏi lại. **Trần:** *dòng này thay đổi gì trong việc người đọc đang phải làm?* — không đổi gì thì cắt, kể cả khi nó đúng. Chọn dạng có mật độ cao nhất **cho loại nội dung đó**: bảng cho so sánh, diagram cho luồng, công thức cho quan hệ định lượng, một câu cho trực giác. **Trong một mục: kết luận trước, lý do sau** — người đọc đủ tin thì dừng được; **một khẳng định = một câu lý do**, phải viết cả đoạn là chưa hiểu đủ để nén hoặc đang biện minh cho thứ không cần có. **Giữa các mục:** dễ→khó, tổng quan→chi tiết, mỗi bước chỉ dùng khái niệm đã nêu; đánh số khi là quy trình. **Đổi rồi thì chỉ còn một hệ:** quyết định đã chốt thì code và tài liệu nói **hoàn toàn bằng hệ mới** — không "trước đây là…", không hai đơn vị song song, không giữ tên cũ làm cầu. Giữ vết hệ cũ chỉ khi **gọi tên được người dùng thật của vết đó**: payload cũ phải deserialize được (§3.7) · developer yêu cầu. Lịch sử ghi ở commit. → §2.3, §5.4 |

---

# §2 — Quy trình làm việc với AI

Trình tự một việc: **phỏng vấn** (§2.1) → **đối chiếu code với developer** (§2.2) → **khảo sát có sẵn,
chốt phạm vi, chốt chỗ đặt** (§2.4) → làm → **nghiệm thu đúng loại** (§2.8) → **quét cuối chat** (§2.6).

## 2.1 Trước khi làm — phỏng vấn ngữ cảnh

Agent **không** suy đoán ngữ cảnh rồi bắt tay làm: đoán **thừa** là hàm không caller nào gọi, đoán
**thiếu** là chữ ký chặn hướng dùng thật (NT1). Hỏi 5 nhóm dưới, **gộp thành 1–2 lượt**; biết chắc nhóm
nào thì **nêu giả định để developer xác nhận**. **Chưa có câu trả lời thì chưa làm.**

| Nhóm | Với tài liệu | Với code hoặc plan |
|---|---|---|
| **Ai dùng đầu ra** | ai đọc, đọc để làm gì, biết sẵn tới đâu | ai gọi, gọi ở đâu, có caller thật **ngay bây giờ** chưa |
| **Mục tiêu** | đọc xong phải **làm được gì** | phải đạt **cảm giác hoặc hành vi** gì, nghiệm thu bằng gì (§2.8) |
| **Ngân sách** | độ sâu và độ dài nào là đủ | **nhịp** của nó là gì (NT2), có phải hot path không, platform nào |
| **Ranh giới** | phần nào giải ở đây, phần nào trỏ sang tài liệu khác | phần nào của class này, phần nào của hệ khác; hệ này **độc lập hay kết hợp**, **thuộc dự án hay Horcrux** (§2.4) |
| **Hướng phát triển thật** | hệ sắp đổi gì khiến tài liệu phải sửa | **chắc chắn** sắp cần thêm gì; cái gì *có thể* cần nhưng chưa chắc (NT1) |

**Giả định chỉ vá khe hở nhỏ phát hiện giữa chừng, không thay cho phỏng vấn** — đủ cả ba: khe hở
**nhỏ** · đầu ra **đảo ngược được** · nhãn ghi **tại chỗ dùng** (NT5). Khó đảo ngược là: ghi đè hoặc
xoá dữ liệu đã author (file level, save, asset) · migration đổi schema hoặc wire format · thứ nằm ngoài
version control.

**Gợi ý một hướng developer chưa nghĩ tới thì cứ nêu — đó là việc được mong đợi.** Đúng khi dừng ở
**một dòng kèm giá** để developer chọn; sai khi tự đưa vào output vì "tiện thể". Trần của gợi ý là
**phạm vi đang bàn** — kéo sang hệ khác thì để một dòng ở "Mở rộng sau" (§2.5). Thứ developer tự nêu
thì hỏi lại **một lần** để cân đắt–lợi, rồi theo developer.

## 2.2 Đọc code rồi phải đối chiếu lại với developer

**Code cho biết cái gì đang chạy, không cho biết vì sao nó được viết như vậy.** Không mặc định developer
nắm rõ từng ngóc ngách: code có thể do người khác viết, viết từ lâu, hoặc đã trôi khỏi thiết kế ban
đầu. Với **mỗi phát hiện ảnh hưởng đến quyết định đang bàn**, nêu đủ: **(1) tôi thấy gì** — kèm đường
dẫn và dòng · **(2) tôi hiểu ý định là gì** — phát biểu lại rồi hỏi thẳng có khớp thiết kế không ·
**(3) developer đã biết chỗ này chưa** — chưa thì là chủ ý hay chỗ đã trôi. Kể lại toàn bộ code vừa đọc
là bắt developer đọc tường thuật thay vì trả lời một câu hỏi. Mỗi lượt review đọc lại file hiện tại
(NT8): báo lại lỗi đã sửa làm mất tin vào cả những phát hiện còn đúng.

## 2.3 Văn phong khi đối thoại

NT10 áp nguyên; mục này chỉ thêm phần riêng của đối thoại. Cách viết: một câu một ý · ngắn và dễ hiểu
đi trước hoa mỹ — bỏ câu dẫn, câu chuyển, tóm tắt lại · hạn chế viết tắt và thuật ngữ, buộc dùng thì
giải nghĩa ngay lần đầu · **gọi khái niệm đúng tên nó có trong hệ**, khớp nguyên văn code và tài liệu
(§3.7) · câu hỏi phải trả lời được **mà không cần mở code ra đọc lại** · việc developer phải làm bằng tay
trong Editor gom về **một mục riêng ở cuối** (§5.4) · ví dụ minh hoạ cũng là khẳng định, kiểm trên code
trước khi đưa (NT8) — ví dụ sai làm developer nghi luôn định nghĩa đúng đi kèm nó.

**Bày phương án, rồi chốt một cái.** Mỗi phương án một dòng: *nó là gì · được gì · mất gì*. Rồi **chốt
một phương án và nói vì sao nó thắng** — tiêu chí là **hợp tư tưởng trong file này nhất** (§0), không
phải "dễ làm nhất". Bỏ phần chốt là đẩy việc khó nhất về developer; bỏ phần bày phương án là lấy mất
dữ kiện để developer bác lại. Quyết định thuộc developer (NT5) thì chỉ thu hẹp và nêu giá; tên gợi ý
chưa áp vào code hay tài liệu trước khi developer chốt, bản nháp lỡ áp thì hoàn về.

## 2.4 Có sẵn chưa → có cần không → đặt ở đâu

Ba câu hỏi theo đúng thứ tự, trước cả khi viết Plan hay đoạn mẫu — code trong Plan là code developer
sẽ gõ nguyên, nên thừa ở đó cũng là thừa.

### Có sẵn chưa (NT7)

Khảo sát **toàn bộ** dự án, theo thứ tự: **hệ đang chạm và module nó gọi** → **Horcrux** (hệ ở
`Foundations/`, helper ở `Utilities/`) → **phần còn lại của dự án** → **package đã cài**. Cả dự án lẫn
Horcrux đều có thì **ưu tiên bản Horcrux** — nó đi theo mọi dự án sau, và bản trong dự án khi đó là một
bản thứ hai cần đề xuất gộp (NT7). Nói rõ đã khảo sát đâu và kết luận gì (NT8): trong Plan là bảng
**"Đã khảo sát"** ba cột — *nguồn · lấy gì · không lấy, vì sao* — gồm cả khuôn cũ trong dự án và thư viện
bên thứ ba; cột "không lấy" là nơi ghi lý do để người sau không bê lại cái đã bị loại.

| Cái có sẵn | Xử lý |
|---|---|
| Đáp ứng được yêu cầu | dùng lại, không viết bản thứ hai |
| Gần đúng nhưng thiếu | mở rộng nó nếu **thêm được mà không sửa cái cũ**; không được thì viết mới |
| Không có, hoặc phải **bẻ cong bài toán** cho vừa nó | **viết mới** — tái sử dụng không phải lý do để làm sai bài toán |

**Không dùng bản Horcrux thì phải đề xuất cải thiện Horcrux** — kèm cái thiếu, cách sửa, và ước lượng
công — để developer quyết: sửa Horcrux ngay (mọi dự án sau hưởng) hay viết bản dự án vì tiến độ. Lý do
viết bản dự án không bao giờ là "Horcrux không có", chỉ là "chưa kịp tiến độ"; khi đó dòng "Mở rộng sau"
(§2.5) ghi bản dự án là bản tạm, chờ chuyển vào Horcrux (NT3).

*Đã sai một lần:* viết vòng tween riêng và công thức scale riêng cho một hiệu ứng bay, trong khi
Horcrux đã có runner tween và class squash-stretch — cả mục phải viết lại ở lượt review.

**Bê một khuôn có sẵn thì soi kỹ nhất đúng những chỗ khuôn cũ *cố ý* làm** — cân nhắc kỹ nghĩa là nó
bám chặt ngữ cảnh cũ. Với mỗi quyết định cố ý, hỏi lý do gốc có còn đúng ở bài toán này không; hết thì
đảo và ghi lý do tại đó. *Đã sai một lần:* bê một khuôn cố ý serialize giá trị runtime vào asset sang
bài toán lưu dữ liệu người chơi — tiến độ người chơi đi vào asset rồi vào version control.

**Hệ nguồn đã trả về kiểu thì nhận kiểu, không nhận chuỗi rồi tự parse lại.** Chuỗi trung gian là parse
cài đặt ở hai nơi: hệ nguồn đã có đường parse, log lỗi và fallback; bản thứ hai phía consumer chép lại
đúng việc đó, che lỗi khỏi đường log của hệ nguồn, và bỏ mất kiểm tra biên dịch trên tên field. Phần
còn lại phía consumer chỉ là **kiểm luật nghiệp vụ** trên object đã có kiểu (§3.4). *Đã sai một lần:*
khai biến remote config kiểu chuỗi rồi viết một class `*ConfigParser` bọc deserialize và `try/catch` —
toàn bộ phần đó đã có trong hệ remote config.

> **Nền tảng** — hệ remote config và persistence của Horcrux ép kiểu sẵn qua cùng một serializer
> (xem `RemoteConfigSystem.md`, mục "Kiểu T hỗ trợ"). Giá trị có cấu trúc thì khai generic với một
> class dữ liệu thuần, **không** khai `string`. JSON hỏng thì hệ tự `LogError` và giữ cache — consumer
> không `try/catch`. Serializer khớp property **không phân biệt hoa thường** nhưng không đổi tên: đổi
> **tên** field thì key cũ rơi thành `null` im lặng — chỉ cửa kiểm luật nghiệp vụ mới bắt được.

### Có cần không (NT1)

Mọi thứ định đưa vào qua cùng một luật: **có nhu cầu thật ngay bây giờ thì đưa vào.**

| Thứ định thêm | Nhu cầu thật là |
|---|---|
| Interface hoặc abstract | có **implementation thứ hai** đang có thật |
| Tham số | có **call site truyền khác mặc định** |
| Overload, wrapper, hàm "cho đủ bộ" | có **người gọi thứ hai** cần đúng nó, hoặc nó **mã hoá một luật caller không nên biết**. Một họ helper cùng tiền tố không kéo theo nghĩa vụ dựng sequence, loop, bảng easing cho đủ; hai hàm chỉ gọi cùng một thân với hai bộ hằng, trong khi caller đã nói hướng bằng trạng thái của chính nó, là tên nói hai lần một điều |
| Guard, nhánh biên, `try/catch` | theo **một câu hỏi duy nhất** ở §3.4: *cái sai đó lộ ra lúc nào?* |
| Code canh — hoặc tự tạo — thứ dựng được lúc authoring | **không có nhu cầu nào cả**: đó là bước setup, viết vào tài liệu (§3.6) |
| Tối ưu làm code khó đọc hơn | **hot path đã xác nhận** bằng số đo (NT2) |
| Tách lớp, tách class, tách component | **trách nhiệm thật sự khác** — khác lý do thay đổi |
| Chương, mục, demo | có người đọc cần nó để **làm được một việc cụ thể** |

Thứ chỉ "có thể cần sau" chia theo **giá của việc thêm sau** (NT1): **rẻ** (thêm hàm hay mục mới, không
sửa cái cũ — **kể cả thêm một tham số tuỳ chọn có mặc định**: call site cũ không đổi, chỉ implementer
sửa) thì **để lại**, ghi một dòng ở "Mở rộng sau" (§2.5) · **đắt** (chữ ký mà **mọi caller** phải sửa, đập cấu trúc,
đảo chiều phụ thuộc) thì làm ngay. Tính mở rộng đến từ Open/Closed, không từ việc viết sẵn thứ chưa ai
cần.

### Đặt ở đâu — dự án hay Horcrux (NT3)

Với **mỗi chức năng mới**, trước khi viết, trả lời: *bê sang dự án khác có dùng nguyên được không?*

| Chức năng | Nơi đặt | Ai viết |
|---|---|---|
| Không gọi tên type nào của dự án; dự án sau cũng cần | Horcrux — `Foundations/` nếu là hệ, `Utilities/` nếu là helper thuần | **developer tự gõ**, theo **Plan** agent viết (§5.3) |
| Dựng trên nhiều hệ Horcrux, vẫn không mang domain dự án | Horcrux — `Composites/` | developer, theo Plan |
| Gọi tên domain của dự án | dự án | agent |
| Lõi chung + lớp nối dự án | **tách**: lõi vào Horcrux theo Plan; lớp nối agent viết trong dự án | cả hai |

Phân loại là **đề xuất kèm giá**, developer quyết (NT5) — nhưng việc hỏi là bắt buộc: **im lặng viết vào
dự án thứ vốn mang đi được là vi phạm**, vì sửa chỗ đặt sau là đập cả cây phụ thuộc. **Mọi dòng vào
Horcrux developer tự gõ, trong hay ngoài Plan:** framework đi theo mọi dự án sau nên developer phải hiểu
và chịu trách nhiệm từng dòng; agent viết Plan, đoạn mẫu, review, test. Áp cho cả việc **nâng** một
chức năng đang có lên Horcrux khi có người dùng thứ hai (§3.2).

## 2.5 Ghi ngữ cảnh đã chốt vào đầu output

Plan thì đặt mục **"Ngữ cảnh đã chốt"** trước `§0`; tài liệu thì nêu ở phần mở đầu. Gồm: người dùng ·
mục tiêu · ranh giới · chỗ đặt (dự án hay Horcrux) · **những gì cố ý KHÔNG làm, kèm lý do** · **quyết
định trái trực giác** — chỗ cố ý trông "kém tối ưu" phải có lý do viết sẵn ở đây, để người tối ưu sau
đọc **trước khi đụng** · hướng phát triển đã tính tới nhưng chưa làm ("Mở rộng sau"). Người đọc sau biết
vì sao phạm vi dừng ở đó, không "bổ sung cho đủ".

## 2.6 Chưng cất tư tưởng

Tư tưởng của developer lộ ra dưới dạng **quyết định cho một bài toán cụ thể** và trôi mất khi bài toán
xong. Ba nguồn, ba nhịp ghi:

| Nguồn | Khi nào | Cách ghi |
|---|---|---|
| **Câu trả lời khi brainstorm** | ở từng câu trả lời | ① khái quát hoá: tách *tư tưởng* khỏi *quyết định riêng của bài toán*, kèm "vì sao" · ② đối chiếu với **toàn bộ** file này: đã có thì thôi · là trường hợp riêng thì trỏ về · **gộp được vào một yêu cầu đang có để yêu cầu đó mạnh hơn thì gộp**, không mở yêu cầu mới · **mâu thuẫn** với yêu cầu đang có thì **chọn bản hợp với toàn bộ file hơn, nói vì sao nó thắng** (§2.3), và developer phân xử — không tự sửa dòng cũ (NT5, §5.4) · ③ **hỏi** developer có ghi không, vào tầng nào — chốt mới ghi, từ chối thì bỏ |
| **Code thật lệch khỏi Plan** | lượt so code–plan sau mỗi step (§5.3) | phân loại từng chỗ lệch: *lỗi* → báo kèm bằng chứng (§2.2) · *trôi* (tên, thứ tự, chi tiết không mang lý do) → sync plan theo code · *tư tưởng* (đổi hình dạng, ranh giới, kiểu, cách tiêm, và lý do còn đúng ở bài toán khác) → ba bước trên **bỏ bước hỏi**: developer đã quyết bằng chính dòng code, ghi thẳng rồi liệt kê để veto. Chỗ lệch là nơi tư tưởng lộ rõ nhất: lý do ở đó luôn mạnh hơn plan, hoặc là lỗi — không có ca thứ ba |
| **Quét cuối chat — bắt buộc, không đợi nhắc** | trước báo cáo hoàn thành của mỗi chat có làm việc | duyệt **cả chat**, tổng hợp vào file này mọi rule, tư tưởng, kịch bản dùng chung cho game Unity từ hai nguồn: thứ developer yêu cầu hoặc sửa, và gợi ý của agent mà developer đã đồng ý (kể cả bằng cách sửa code theo gợi ý). Mỗi dòng đi qua bước ② ở trên. **Ghi thẳng, liệt kê nguyên văn trong báo cáo** để developer veto — ca mâu thuẫn thì ghi bản thắng kèm lý do, dòng cũ giữ nguyên tới khi developer phân xử; nguồn không rõ thì nêu giả định, không đoán lặng lẽ |

Chỉ khái quát khi câu trả lời **thật sự chứa tư tưởng** — một lựa chọn có "vì sao" lặp lại được. Quyết
định thuần bài toán (hằng số, tên, phạm vi một task) thì không. Câu hỏi ở bước ③ **không tính vào ngân
sách 1–2 lượt của §2.1**: §2.1 hỏi để lấy ngữ cảnh, đây hỏi để chốt một nguyên tắc — hiếm. Mọi dòng ghi
vào file này: **không mang domain dự án** — bỏ tên type, tên hệ, hằng số của dự án; đúng cấu trúc và
văn phong của file. Quy ước chỉ sống ở **tài liệu module** (không đủ chung để vào đây) thì theo §5.4.

## 2.7 Subagent — ngữ cảnh bơm từ orchestrator, không tự đọc lại

Mỗi subagent là một ngữ cảnh trắng: để nó "tự tìm hiểu" là nó đọc lại từ đầu file này và tài liệu hệ,
thuế đọc **nhân theo số subagent**. *Đã sai một lần:* hàng trăm subagent tự đọc lại tài liệu nền trong
một ngày đốt token gấp hơn chục lần nhịp thường. Prompt cho subagent **tự chứa như một task của Plan**
(§5.3): trích đoạn tài liệu cần cho task, đường dẫn file sẽ chạm, tiêu chí nghiệm thu; thiếu ngữ cảnh
thì subagent báo về, không tự đi đọc. Ngoại lệ là **code**: subagent tự đọc code nó sẽ sửa (NT8).

## 2.8 Nghiệm thu — chọn phép kiểm theo loại tiêu chí

NT8 đòi bằng chứng; mục này nói bằng chứng **nào** hợp tiêu chí nào. Chọn sai thì lãng phí cả hai
phía: dựng lệnh cho thứ chỉ chơi thử mới biết, hoặc đẩy về tay developer thứ máy quét vài giây là xong.
Cái neo khi phân vân: **công sức đắt nhất trong nghiệm thu là của developer.**

| Tiêu chí cần chứng minh | Bằng chứng đúng loại | Ai chạy |
|---|---|---|
| Thuật toán tất định, công thức, parser, serialize | phép kiểm chạy được (§4.3) | agent |
| Tên, tham chiếu, đồng bộ tài liệu | grep quét (§3.7, §5) | agent |
| Hiệu năng | số trước–sau tại chỗ đo (§3.3) | agent |
| Đúng–sai xác định được, mà người làm tay thì chậm, sót, hoặc không thấy được | vét cạn theo bảng dưới | agent, **tự đề xuất** |
| Cảm giác chơi, nhịp, độ khó, hình ảnh | **chơi thử** | **developer** |
| Kịch bản hành vi trên build — event, kinh tế, save, tutorial | **chơi qua kịch bản**, cheat rút đường tới trạng thái cần kiểm | **developer, QA** |

**Cảm giác chơi — DỪNG và giao, không dựng proxy.** Ép nó về một lệnh chạy được là **đo thứ dễ đo
thay cho thứ cần biết**. Báo thẳng *"phần này chưa nghiệm thu được, cần chơi thử"*, kèm **kịch bản
chơi thử**: *vào đâu* (level, cờ, dữ liệu cần bật) · *làm gì* (chuỗi thao tác **ngắn nhất**) · *nhìn cái
gì* (hiện tượng cụ thể, không phải "xem có ổn không") · *khác trước ra sao* · *dấu hiệu hỏng*.

**Đúng–sai xác định được — vét cạn, không đẩy về tay.** Điều kiện là **cả hai**: có đáp án xác định
được, **và** ít nhất một dấu hiệu dưới. Đủ thì agent làm và chạy, kể cả khi chưa được yêu cầu — đây là
bằng chứng (NT8), không phải mở rộng phạm vi.

| Máy hơn người ở | Dấu hiệu nhận ra | Người làm tay hỏng ở đâu |
|---|---|---|
| **Sức** | không gian đầu vào lớn · phải lặp lại nhiều lần | chậm, và sót vì mỏi |
| **Thiên kiến** | trường hợp biên khó nghĩ ra hết | chỉ thử được thứ mình nghĩ ra, mà chỗ hỏng nằm đúng ở chỗ không ai nghĩ tới |
| **Tầm nhìn** | phải chứng minh **sự vắng mặt**: không còn tham chiếu, caller, tên cũ · trạng thái nội bộ sai trong khi màn hình vẫn đúng · thứ chỉ lộ sau hàng nghìn vòng: rò rỉ, pool không trả về | mắt không thấy được thứ *không có* |
| **Nhất quán chéo** | nhiều bản buộc phải khớp nhau (§3.4) · hành vi trước–sau một lần refactor phải trùng (NT6) | phải mở nhiều nguồn cạnh nhau so từng dòng |

Riêng nhánh **thiên kiến**, phần đắt giá là **liệt kê biên có hệ thống trước khi chạy**: rỗng · đúng
một phần tử · chạm giới hạn trên và dưới · trùng nhau · ngoài dải · thứ tự đảo · hai sự kiện cùng lúc ·
frame đầu tiên · đối tượng bị huỷ giữa chừng. Không dấu hiệu nào thì đọc code là xong (NT1).

**Cheat — mở đường tới kịch bản, không mở nắp kỹ thuật.** Một lệnh chỉ chính đáng khi kịch bản **không
tới được bằng đường người chơi trong thời gian hợp lý**: thời gian (hết tuần, hết cửa sổ) · khối lượng
(đủ điểm cho mốc cuối) · trạng thái ban đầu (save mới, tutorial chưa xem). Thứ chơi là tới thì QA đi
đúng đường người chơi; cheat cho nó là kiểm kỹ thuật đội lốt kiểm hành vi, và mỗi nút là một dòng phải
giữ đúng mãi. Hàng đọc chỉ cho **thứ màn hình không hiện**. Mặt cheat đọc qua API **sẵn có** của hệ —
không sinh property `Debug*` bọc lại thứ đã public; lệnh riêng chỉ khi phải chạm `private` (đổi save,
giả thời gian), và khi đó gọi đúng lệnh thật của hệ, không đi tắt qua state. Plan liệt kê cheat theo
tiêu chí này (§5.3); developer bảo cần thêm thì mới thêm.

> **Nền tảng** — tab Options của SRDebugger, một category mỗi hệ. **Có trong bản build chính thức, không
> `#if`**, vì QA kiểm trên đúng bản người chơi cầm. Cách **mở** panel là của developer từng dự án, giấu
> khỏi người chơi; agent không thiết kế và không viết nó. Trigger của SRDebugger để `Off`.

**Sổ tay** — partial `<Hệ>.Debug.cs` giữ lệnh (với tới `private`; ca `partial` được phép, §3.1), plain class `<Hệ>DebugOptions` giữ
hàng UI, nhận hệ qua constructor · `Init/Dispose` ở `Awake/OnDestroy` · container `internal sealed`,
không public gì ngoài hàng vì SRDebugger quét mọi public member · hàng đọc là `string` read-only · lệnh
có tham số = một property số + một method không tham số · `INotifyPropertyChanged` bắn `null` sau mỗi
lệnh để hàng đọc cập nhật · chưa có save thì hàng nói lý do thay vì NRE.

---

# §3 — Thiết kế code

**Bốn ưu tiên** §3.1 hình dạng · §3.2 module · §3.3 hiệu năng · §3.6 editor-first — **vận hành** §3.4
bất biến · §3.5 async — **luật ngang mọi code** §3.7 naming · §3.8 import/export — **tool** §3.9.

## 3.1 Hình dạng — bậc cấu trúc, SOLID, và giới hạn của composite

| | Nội dung |
|---|---|
| **S** | Một class là một responsibility. Tách khi có nhiều hơn một **lý do thay đổi** (NT1). |
| **O** | Extend, don't modify. `O` cấm **đổi hành vi đường cũ**, không cấm **thêm đường mới vào chính class đó**: thêm method hoặc tham số mà đường cũ không đụng là đã đạt `O`. Phải đổi hành vi đường cũ nghĩa là **trách nhiệm mới** — tách class. `O` **không phải lý do để leo bậc** (bảng dưới). |
| **L** | Subtype thay thế được base mà không break behavior, không side-effect lạ. |
| **I** | Interface nhỏ, tách theo consumer. Không ép client phụ thuộc method nó không dùng. |
| **D** | **Consumer không tự `new` thứ nó phụ thuộc** — nó nhận vào (factory, pool, container thì đương nhiên phải `new`). `D` **không đòi interface**: trong một hệ, nhận vào class cụ thể vẫn là "nhận vào". Qua ranh giới hệ thì theo §3.2. Kéo thả vào `[SerializeField]` cũng là nhận vào (§3.6). |

> **Nền tảng** — DI runtime mặc định là InitArgs (`Sisus.Init`): `[Service(typeof(T))]` để đăng ký,
> `MonoBehaviour<TDep>` + `Init(TDep)` để nhận. **Mỗi class chỉ có MỘT khe `Init` mà framework tự
> gọi** — base generic đã tiêu khe đó thì `IInitializable<…>` khai thêm ở class con **không ai gọi**
> (đường tự tiêm thoát sớm, log chỉ có trong `#if DEV_MODE`). Ca này **bắt buộc** một `*Initializer`
> kéo tay vào scene. Editor và tooling không bắt buộc dùng InitArgs — constructor injection hoặc static
> factory ở đó hợp lệ. `Awake` của base gọi `Init` **trước** `OnAwake`, nên dùng phụ thuộc đã tiêm trong
> `OnAwake` an toàn ngang `Start`.

**Đổi từ resolve lười sang resolve sớm là dịch cửa sổ thất bại, không phải bỏ nó.** Gọi lúc dùng thì
hỏng lúc dùng; nhận qua `Init` thì hỏng lúc khởi tạo. Đổi chiều nào cũng phải hỏi: **lúc đó thứ mình
cần đã tồn tại chưa?**

**Bậc cấu trúc leo từ dưới lên, mỗi bậc chỉ leo khi bậc dưới không còn đạt** (NT1).

| Bậc | Đủ dùng khi | Leo lên bậc trên khi |
|---|---|---|
| **Logic tại chỗ** | chỉ chạy ở một nơi, đọc một mạch là hiểu hết | có **người gọi thứ hai**, hoặc một ý không còn nhìn hết trong một màn hình |
| **Hàm tách riêng** | đặt được tên nói đúng mục đích (§3.7); không giữ state giữa các lần gọi | phát sinh **state phải giữ**, hoặc một cụm hàm cùng thao tác trên một nhóm dữ liệu |
| **Class hoặc struct** | có **trách nhiệm gọi được tên** và state của riêng nó (`S`) | có **implementation thứ hai** đang có thật |
| **Delegate làm tham số** | thân thuật toán **giống hệt** ở mọi biến thể, chỉ khác **một thao tác** gọi được tên, biến thể **không giữ state riêng**. Khai `static readonly Func<…>` với lambda `static`: bắt biến ngoài thành **lỗi biên dịch** thay vì rác GC âm thầm (NT2). Callback sinh ở call site thì **chữ ký trả lại chủ thể** (`Action<Transform>` nhận đúng transform vừa chạy) để handler là method group `static` — bất biến "không cấp phát" nằm ở chữ ký, không ở kỷ luật người gọi. Bất biến của vòng lặp — thứ tự chạy, phát tiến độ, xử lý lỗi, điểm thoát khi huỷ — chỉ có **một bản**; nhân vòng lặp ra n bản hay cắm `if` biến thể vào giữa là nhân bất biến ra n bản (NT7) | biến thể cần **state riêng**, hoặc **hơn một thao tác** đi cùng nhau — lúc đó nó đã là interface |
| **Interface hoặc abstract** | implementation thứ hai **đang có thật**, không phải sắp có | — |

**Biến thể là một cửa riêng, không phải một cờ trên cửa chung.** Hai chế độ loại trừ nhau là **overload
nhận spec riêng**, không phải một spec mang cờ chế độ, và không chồng hiệu ứng của biến thể kia lên;
kịch bản mới của một service dùng chung là **consumer mới** đưa nội dung vào qua tham số, không phải cờ
mọc thêm trên service — service mọc cờ theo từng kịch bản là gom domain của mọi consumer về một class
(NT1, NT3); component dùng chung ở nhiều host **không có cờ chế độ trên instance** — một ô `autoPlay`
quên tick trên một instance không để lại dấu vết (§3.6). Hai chế độ buộc phải là một giá trị thì là
`enum` hai phần tử, không `bool` (§3.7).

**`MonoBehaviour` · `ScriptableObject` · class thuần không phải ba bậc của thang trên.** Thang trả lời
*chia tới đâu*; ba thứ này trả lời *đóng gói ở đâu* — hỏi **sau**, khi đã biết là cần một class.

| Nó làm gì | Là gì | Vì |
|---|---|---|
| trả lời câu hỏi từ **tham số truyền vào**, không giữ gì | **class thuần static** | test không phải dựng gì; không thêm một asset có thể quên kéo |
| trả lời câu hỏi từ **dữ liệu của chính nó**, dữ liệu do người dựng đặt | **`ScriptableObject`** | dữ liệu là cấu hình, và asset thì kéo được từ mọi nơi (§3.6) |
| **làm gì đó theo thời gian** — loop, chờ, subscribe, ghi ra ngoài | **`MonoBehaviour`**, hoặc class thuần do một `MonoBehaviour` sở hữu | cần một **mốc chết tin được** để tắt. Phép kiểm là *"có cần tắt được không"*, không phải *"có cần vòng đời không"* |

> **Nền tảng** — `ScriptableObject` dừng ở **dữ liệu**: cấu hình, bảng số, danh mục, preset. Không làm
> kênh sự kiện, không làm biến dùng chung, không giữ state đổi lúc chạy, không tự mở
> `CancellationTokenSource`. Ba lý do: field serialize bị đổi lúc Play **không quay lại** khi dừng Play
> — ra "máy tôi chạy được" không một dòng log · asset chỉ nạp lúc có người chạm lần đầu nên **không có
> thứ tự khởi tạo** để dựa vào · không có mốc kết thúc nào để đóng token.

**Ba hình dạng composite — viết lý do ra tại chỗ trước khi dùng.** Không cấm, nhưng mặc định là không,
và mỗi lần dùng phải gọi tên được thứ bậc thấp hơn không làm được. Thước đo là **phạm vi bài toán**:
cùng một cách chia có thể đúng ở hệ nhiều người chạm và thừa ở một tính năng cục bộ.

| Hình dạng | Giá phải trả | Chỉ dùng khi |
|---|---|---|
| **Cây composite** — cha và lá cùng interface, duyệt đệ quy | hành vi không nằm ở đâu cả, phải chạy mới biết; mỗi nút một dispatch ảo và một lần con trỏ nhảy, trong nhịp mỗi frame là trả giá theo số nút (NT2) | **cấu trúc lồng nhau là của dữ liệu thật** — độ sâu do người dùng hoặc dữ liệu tạo ra, không do người viết chọn cho đẹp |
| **Một tính năng xé thành nhiều MonoBehaviour** | thứ tự `Awake`/`Update` giữa các mảnh **không định trước** (khối Nền tảng dưới), wire thiếu chỉ lộ lúc chạy, mỗi mảnh là một lần engine gọi message qua ranh giới native | các mảnh **thật sự lắp lẫn được** giữa nhiều prefab, và **tổ hợp đó đang tồn tại thật** |
| **Hệ dựng chồng lên nhiều hệ khác** (hệ kết hợp) | không rời đi một mình được (NT3) | không tách nổi thành các hệ độc lập cộng một lớp nối mỏng; khi đó vẫn phải chỉ ra đường tách (§3.2) |

> **Nền tảng** — thứ tự region trong một class là **cố định**: `Properties` trên cùng · `Unity Callbacks`
> · `API` (thứ người ngoài gọi) · `Class Methods` (thân private) · `DI` **cuối class**, gói field nhận
> vào cùng `Init`. Trình tự **trạng thái → vòng đời → mặt ngoài → thân**: mở file ra là thấy class giữ gì
> trước khi thấy nó làm gì.

> **Nền tảng** — **Callback của Unity là cửa mỏng: thân nằm trong hàm có tên, cửa chỉ gọi hàm đó.**
> `OnEnable` gọi `Show()`, `Awake` gọi `Init` rồi `Bind` — đọc callback là thấy trình tự. **Thứ tự
> `Awake`/`OnEnable` giữa các object do engine chọn, không định trước**, Script Execution Order là cấu
> hình ẩn ngoài code — nên thứ tự giữa nhiều việc, nhiều object do **một chủ gọi** xếp bằng lời gọi
> tường minh. Lợi hai đầu: thứ tự chắc, và hàm có tên sẵn cho người gọi thứ hai. **Chấp nhận để engine
> gọi** khi chủ gọi tường minh phải trả giá lớn: biết một kiểu qua ranh giới không được biết (§3.2), hay
> kéo tham chiếu qua nhiều object chỉ để gọi một dòng — khi đó callback vẫn chỉ là cửa gọi hàm có tên.
> Bảo đảm engine có cho: trong một cây object vừa bật, `Awake` của cả cây chạy xong trước `Start` (§3.6).

**`partial` chỉ để giảm độ dài file ở chỗ không có OOP để vi phạm.** Ba ca được phép: **Editor tool và
Debug của một đối tượng** — không nằm trong đường chạy runtime của class · **static class chứa extension
method** — không state, không kế thừa, tách theo nhóm cho file ngắn · **khối field serialize của một nhóm
Inspector kèm property phơi chúng, không thân hàm** — là khai báo dữ liệu, không phải logic. Ngoài ba ca
đó, một class phải chia file mới đọc nổi là một class ôm nhiều lý do thay đổi: **tách class, không tách
file** (`S`). `partial` cho logic runtime là dấu hiệu SOLID và OOP chưa đạt, không phải công cụ tổ chức code.

**Sổ tay** — hình dạng file và class đang dùng trong Horcrux:

| Chỗ | Cách làm |
|---|---|
| Code chỉ có ở Editor (ca `partial` được phép) | file `*.Editor.cs` khai `partial` của cùng class, **bên trong vẫn** `#if UNITY_EDITOR` — file runtime không bị `#if` cắt ngang, nút Editor đọc thẳng private member |
| Nhận phụ thuộc | `MonoBehaviour<T>` cho component thường · `IInitializable<T>` khi class đã kế thừa base khác · không service locator bên trong hệ |
| Helper thao tác lên một object có sẵn (ca `partial` được phép) | extension method trên type đó, file partial `<Type>Extensions.<Nhóm>.cs` — call site đọc thành câu, tìm được từ chính object, không bao giờ che instance method (§3.4) |
| Dựng host | hệ **không** tự `new GameObject` — host là component kéo tay vào scene, chu kỳ và collection đọc được trong Inspector (§3.6) |
| Field serialize | nhóm bằng `[Splitter("References")]` (ô kéo) rồi `[Splitter("Configs")]` (số chỉnh), References đứng trước — mở Inspector thấy ngay thứ **phải nối** trước thứ có thể để mặc định |
| Field private runtime | tiền tố `_` (`_module`, `_isOpen`); field `[SerializeField]` **không** `_` — đọc tên trong thân hàm biết giá trị đến từ Inspector hay từ code |
| Log | mọi dòng mở bằng `[TênHệ]: ` để filter console; kèm `this` làm context object để bấm vào ra đúng asset |
| Cụm núm tinh chỉnh của một helper chuyển động | struct `[Serializable]` gom các núm đi cùng nhau, truyền nguyên vào helper — chữ ký không mọc theo số núm, dải hợp lệ khai bằng `[Min]`; biến thể theo luật "cửa riêng" ở trên |
| Hàm một biểu thức | expression-bodied (`=>`), kể cả method `void` |
| Ẩn method của contract | mặc định `public` — tin người dùng hệ. Explicit interface implementation **chỉ cho method mà gọi sai gây mất dữ liệu im lặng** (§3.4) |

## 3.2 Module — độc lập trước, kết hợp là ngoại lệ

Ranh giới hệ thống phải **nhìn thấy được** trong cấu trúc dự án; mỗi hệ có một trách nhiệm gọi được
tên. **Module không tham chiếu trực tiếp implementation của nhau** — cơ chế trung gian chọn theo bài
toán (interface, event bus, dữ liệu thuần), miễn đạt: đổi implementation một bên mà bên kia không phải
sửa. Bên trong một hệ thì không cần tầng này (`D` ở §3.1).

**Phân tầng theo mức phụ thuộc, quyết ngay từ đầu** (NT3): hệ **độc lập** (bê sang dự án khác được) ·
hệ **kết hợp** (dựng trên nhiều hệ độc lập) · **Utilities** static và universal, không phụ thuộc hệ nào.

**Sổ tay** — Horcrux bày tầng đó ra thư mục, để chỗ đặt file trả lời câu hỏi của NT3 mà không mở code:
`Abstractions/` giữ thứ dự án **gọi tên** (interface, base `A*`, model thuần, event, hằng) ·
`Implementations/` giữ class cụ thể dự án chỉ **wire** vào scene (host, bootstep, `*Initializer`) ·
trong mỗi bên `Foundations/` là hệ độc lập, `Composites/` là hệ kết hợp · `Utilities/` ngoài cả hai.
Một file đặt nhầm bên là một phụ thuộc giấu: dự án gọi tên class trong `Implementations/` thì đổi
implementation bên đó là sửa dự án.

**Cơ chế mở rộng Horcrux cung cấp không được ép dự án đẩy domain sang phía framework** (NT3). Phép
kiểm: *nửa mà dự án viết có nằm trong ranh giới biên dịch của dự án không?*

> **Nền tảng** — với C# và Unity:
>
> | Cơ chế mở rộng | Kết quả |
> |---|---|
> | `partial class` khai ở framework, nửa kia ở dự án | **Hỏng.** Mọi phần của `partial` phải cùng assembly, nên nửa của dự án phải vào assembly framework bằng `.asmref` — và type của dự án không tra được từ đó (**circular reference**). Kết cục: domain nằm ở assembly framework |
> | `abstract class` ở framework, dự án khai subclass | **Đúng.** Subclass là type của dự án, chỉ gọi tên xuống framework. Phần framework tự chạy được một mình biến mất — đó là đặc điểm: tự chạy được nghĩa là đang mang một mẩu domain |
>
> Ba bẫy hỏng-im-lặng đi kèm kế thừa: attribute khai `Inherited = false` (`[Service]` của InitArgs là
> một) khai ở base **không** tới subclass · `GetType().GetFields(NonPublic | Instance)` **không** thấy
> private field khai trên base · magic method của Unity (`OnDestroy`, `Awake`…) subclass đặt trùng tên
> là che hẳn bản của base — khai `protected virtual` để `override`.

**Nâng một chức năng lên tái sử dụng khi người dùng thứ hai đang có thật** (NT7): hàm thuần → Utilities
· có state hoặc nhiều biến thể → interface rồi tách implementation · chỉ khác một giá trị → thêm tham
số. Đặt ở **tầng thấp nhất mà cả hai người dùng đều với tới được**, không thấp hơn. Thời điểm: khi sắp
viết bản thứ n của một cơ chế đã có ở các module anh em — bản mới là bản dùng chung đầu tiên; bản cũ
chuyển sau bằng cách thêm bản dùng chung rồi xoá code cũ, không sửa base — việc rẻ, để sau được. Là đề
xuất; developer gõ (§2.4).

**Nâng bằng kế thừa thì cắt ở ranh *cơ chế / quyết định*: base giữ cơ chế, lớp con chỉ còn một quyết
định.** Cơ chế là phần giống hệt ở mọi nơi dùng — tham chiếu đích, bộ đệm, nhịp ghi lần đầu, định dạng;
quyết định là phần mỗi nơi một khác — *nói gì, lúc nào*. Phép kiểm: lớp con đọc hết trong một màn hình
và **không chạm vào bộ phận của base**, chỉ gọi động từ của nó.

| Luật | Vì |
|---|---|
| Ô kéo thả ở base khai **kiểu rộng nhất còn đủ member base dùng** | kiểu hẹp hơn là ràng buộc base không cần, và đóng cửa một nửa nơi dùng ngay từ chữ ký |
| Base phơi **động từ theo việc** (`protected`); bộ phận là `private` | lớp con nói bằng ngôn ngữ của việc; đổi cách base ghi ra đích không chạm lớp con (`O`) |
| Hook vòng đời khai `protected virtual` ở base; lớp con `override`, gọi `base` rồi nối nhịp riêng | một chỗ quyết phần chung; quên `virtual` là bẫy che magic method ở trên |
| Base là `MonoBehaviour` thì **giá là khe DI**: lớp con nhận phụ thuộc qua `*Initializer` gắn tay (§3.1) | giá đó ghi vào Editor setup của từng chỗ dùng, không giấu — thiếu là null ngay frame đầu |

## 3.3 Hiệu năng runtime

Luật ở NT2; mục này là cách đạt. **Nghiệm thu:** chỉ ra được **chỗ đo** và **số trước–sau** (NT8).

Trước khi cache hay tối ưu, hỏi ba câu theo thứ tự — "có" ở câu nào thì dừng ở đó: có thể **không cần
tính** nó không · tính **một lần lúc authoring** được không (§3.6) · đổi **cấu trúc dữ liệu** để câu
hỏi tự biến mất được không? Hết ba câu mới tới kỹ thuật.

*Đã sai một lần:* một chuỗi nội suy từ một số đọc từ remote config được để cấp phát lại mỗi lần tap,
với lý do "tap là tương tác" — nhịp được đem ra miễn trừ đúng việc NT2 nói không được miễn trừ.

> **Nền tảng** — `Update`, polling loop và `OnGUI` bị gọi lại liên tục cho **cùng một state**. Việc
> nặng đặt trong đó là sai không cần bàn; nó thuộc về event handler hoặc lúc authoring.

**Sổ tay** — kỹ thuật đã dùng:

- *Giảm cấp phát:* pool thay `Instantiate`/`Destroy` lặp lại · pre-alloc capacity · reuse buffer bằng
  `.Clear()` · `struct` cho data nhỏ ngắn hạn · `ref` / `in` / `Span<T>` thay copy · `static readonly`
  thay `new` lặp · tránh LINQ, boxing, string concat trong hot path · **không closure capture** — cache
  delegate thành `static readonly` hoặc field.
- *Giảm tính toán:* dirty flag · event-driven rebuild · lookup dictionary dựng trước · tách phần tĩnh
  tính một lần khỏi phần động tính incremental · precompute hằng nặng ngoài vòng lặp · guard thoát
  sớm · `sqrMagnitude` khi chỉ so khoảng cách.
- *Sửa list an toàn:* duyệt ngược khi xoá · hoặc deferred removal.

## 3.4 Bất biến — bảo vệ bằng cấu trúc, không bằng kỷ luật

Bất biến giữ bằng "mọi người nhớ làm đúng" sẽ vỡ ở đúng người thứ hai. Sắp xếp code sao cho cái sai
**không thể xảy ra**. **Quyết định mà lý do là "phải nhớ đừng…" thì chỗ đặt sai, không phải tên sai**
— hỏi: *có chỗ đặt nào làm việc "đừng" đó bất khả thi không?* Thường có, và thường rẻ. *Đã sai một lần:*
một helper tên `InstantiateAsync` khai trong class con của `MonoBehaviour` che **toàn bộ** overload cùng
tên của `UnityEngine.Object`; đổi tên thì ràng buộc vẫn còn, đưa ra **extension method** thì ràng buộc
biến mất, vì extension không bao giờ che được instance method.

| Luật | Nghĩa là |
|---|---|
| **Một sự thật = một chủ sở hữu** | mỗi dữ liệu có đúng một nơi giữ bản gốc; mọi cache chỉ ra được **ai dựng lại** và **khi nào**. Cache mới bám vào bất biến **đã có** (dirty flag, version counter), không dựng bất biến thứ hai song song. **Một cờ chỉ được tiêu thụ ở đúng một nơi** — có nơi thứ hai thì nơi chạy sau không bao giờ thấy cờ bật, cache của nó đứng im |
| **Hai bản buộc phải khớp thì suy từ MỘT nguồn** (ngoại lệ của "để lặp", NT7) | *công thức* — đo–vẽ, vẽ–hit-test, điều kiện ẩn–hiện, điều kiện vẽ–điều kiện bấm: cùng một hàm, hoặc cùng một biểu thức copy nguyên; hai bản sẽ lệch kiểu nhìn-vẫn-đúng-bấm-thì-trượt · *chuỗi định danh hai hệ phải khớp* (placement, tên event, khoá): khai **một lần ở phía phát**, phía nhận trỏ symbol — literal chép sang lệch một ký tự thì phép so trả `false` im lặng; phía phát chưa có hằng thì thêm hằng vào phía phát · *định danh số chia sẻ với asset hay hệ khác*: `enum` gán số tường minh từ `= 1` (để `0` — giá trị `default` — không trùng định danh thật), không `const int`; `switch` được compiler soát đủ nhánh; số đã nằm trong asset là wire format — **không đánh số lại**, phần tử mới thêm ở cuối |
| **Một trạng thái = một cửa ghi, một chủ gọi** | mở, huỷ, chốt của cùng một trạng thái nằm trong **một** class; nơi khác chỉ **phát sự thật** — sự kiện đặt tên theo điều đã xảy ra, publish đồng bộ, đúng một người nghe hành động; cần mốc chưa có thì **thêm sự kiện mới**, không mượn sự kiện cũ đang có người nghe mang tác dụng phụ khác. Hệ quả **luôn phải xảy ra**, rẻ, không ném thì đặt trong **setter** — `SetX(v)` cạnh field `x` là hai cửa ghi; hệ quả tuỳ chọn hay tốn kém giữ method, vì sau dấu `=` người đọc không chờ một cái giá (đổi method thành property là đổi hợp đồng: interface phải khai `{ get; set; }`, `internal void` thành `public` property là nới quyền ghi ra assembly khác). **Hai lời gọi luôn phải đi cùng nhau là một kỷ luật**: bước tiền đề đi vào **trong** mỗi cửa public, thứ chỉ là tiền đề thành `private` — mặt ngoài nói **trạng thái caller muốn thấy**, không nói **cơ chế** |
| **Một bảo đảm phải phủ MỌI đường vào** | hệ tuyên bố "mất không quá X", "luôn hợp lệ" thì **mọi** cửa ghi đi qua chỗ tạo bảo đảm; cửa hẹp là **thân chung** của cửa rộng (bản giữ-lại-một-phần gọi vào thân bản đầy đủ). Phép kiểm: **đếm cửa trước, đọc thân sau** — grep mọi API ghi của kho, với từng cửa chỉ ra nó đi qua chỗ tạo bảo đảm, hoặc viết tại chỗ rằng cửa này không được bảo đảm |
| **Một phép biến đổi chỉ áp ở MỘT tầng** | runner đã áp ease cho `t` thì thân nhận `t` đã ease, không ease lần nữa; hai tầng cùng có tham số ease thì một tầng cố định `Linear` |
| **Chọn cấu trúc theo bảo đảm người dùng đang dựa vào** | `Dictionary` đúng khi kết quả để **tra cứu**, sai khi kết quả là **danh sách để vẽ**: thứ tự duyệt không có bảo đảm, UI đảo hàng giữa các lần chạy. Thứ tự đọc từ cấu hình ngoài phải **toàn phần và tất định**: giá trị thiếu rơi về ưu tiên thấp nhất; hoà phá bằng **định danh ổn định**, không bằng thứ tự đăng ký |
| **Mặc định của một lựa chọn là ca số đông, viết bằng tên** | ca số ít — đặc quyền, đi trước người khác — phải khai rõ; quên khai thì rơi về số đông. Mặc định viết tường minh (`=> Mode.Normal`), không dựa vào phần tử đầu của enum |
| **Một danh sách vừa là lệnh vừa là thứ để vẽ thì hỏi hai vai có chung khoá gộp không** | vai *thực thi* gộp theo khoá hệ nhận lệnh, vai *hiển thị* gộp theo khoá người dùng nhìn — gộp theo vai này là vai kia mất hàng. Kho cộng dồn qua nhiều chu kỳ thì gộp lúc *đọc ra*, không lúc *ghi vào* |

**Guard — một câu hỏi duy nhất: cái sai đó lộ ra lúc nào?** Đích là **bản build không có ca null hay
ca sai nào**; guard hay không guard chỉ là hai đường tới cùng đích.

| Cái sai lộ ra… | Xử lý | Vì sao |
|---|---|---|
| **Ngay lúc authoring, hoặc nổ ngay lần Play đầu** — và **đã kiểm là nó nổ thật** | **để nó nổ**, không guard | exception thô và `LogError` đẹp chặn developer ngang nhau; đổi cái trước thành cái sau là trả phí **vĩnh viễn trong build** cho một lần đọc log dễ hơn |
| **Sai lúc setup nhưng không nổ** — view lẽ ra đã gắn mà chưa, hệ thiếu wire chạy tiếp thiếu một tính năng | **`LogError` rồi thoát**, kể cả khi lặp mỗi lượt | thoát im lặng biến một bước setup thiếu thành một tính năng vắng mặt cả phiên mà không ai biết |
| **Lọt qua authoring rồi sai âm thầm** giữa gameplay | **guard đầy đủ** — bất biến thật, về bảng trên | Editor không bắt chắc được: reference chỉ có lúc runtime · null ở một prefab variant · sai chỉ hiện ở một tổ hợp cấu hình · thành null sau `Destroy` · **dữ liệu từ ngoài** (import, server, save) |
| **Ca hợp lệ** — chưa tới lượt, chưa có gì để làm | **thoát im lặng** | không phải lỗi |
| **Không gọi tên được** lượt kiểm nào bắt nó | **guard** | "chắc là không xảy ra đâu" không phải bằng chứng (NT8) |

**Dữ liệu ngoài kiểm một lần ở cửa vào; sau cửa, consumer tin hợp đồng.** Guard ở **cửa** là một hàm
thuần kiểm luật nghiệp vụ, sai thì từ chối cả cấu hình kèm `LogError` — không phải mỗi consumer một
nhánh (cùng luật với tool import ở §3.8). View runtime **không có nhánh vẽ lỗi** cho dữ liệu đã qua
cửa: chủ dữ liệu `LogError` kèm `this` khi tra thiếu là toàn bộ chẩn đoán. Nhánh duy nhất view được có
khi tra thiếu là **xoá vết cũ**: view dùng lại (pool) gán `null` cho ô sẽ hiện sai nhất — sprite — rồi
thoát, để cái thiếu lộ thành ô trống thay vì hiện dữ liệu của item trước. "Không giấu thứ có thật" của
§3.9 là cho Editor tool, nơi người nhìn màn hình là người sửa được dữ liệu; trong build người nhìn là
người chơi.

**"Để nó nổ" đứng được nhờ vế *nổ* — phải kiểm, không được giả định.** Bốn hình dạng nó **không** nổ:

| Hình dạng | Vì sao im lặng | Trả về chỗ nổ bằng |
|---|---|---|
| **Giá trị mặc định của kiểu là giá trị hợp lệ** — `float` 0, `bool` false, list rỗng, enum phần tử đầu | ô trống trong Inspector **không phân biệt được** với ô cố ý điền giá trị đó | **authoring, không phải guard runtime**: default ngay trong khai báo field, kẹp dải bằng `[Min]`/`[Range]` |
| **Host bắt lỗi fail-open** — vòng dispatch, chain boot log rồi bỏ qua step lỗi | thiếu wire thành **một dòng log lúc boot** rồi cả phiên chạy thiếu hẳn một hệ | hệ tự đứng được không cần host đó, hoặc thiếu nó phải hỏng ở **cửa mà người chơi chạm** |
| **Vòng lặp nhận sai token** | hủy là hành vi hợp đồng, không log (§3.5) | mỗi loop chỉ ra được token của nó là đời của ai |
| **Chỗ nổ nằm trong `#if DEBUG`** | bản release không có dòng đó — cùng một ca thành null chạy tiếp rồi NRE ở chỗ khác | đọc `#if` bao quanh mọi guard của thư viện ngoài trước khi tin vào nó |

*Đã sai một lần, nguồn của hai hàng đầu:* một hệ lưu dữ liệu có field khoảng thời gian không default —
quên điền ra 0, thành ghi đĩa **mỗi frame**; cùng hệ gắn loop tự lưu vào token của pha boot mà runner
refresh token mỗi lần load level — loop chết từ level thứ hai. Cả hai không một dòng log.

**`try/catch` đi qua đúng câu hỏi đó.** Chỉ bọc khi **gọi được tên thứ ném ra**: API thật sự ném (I/O,
parse, network, reflection, dữ liệu từ ngoài), hoặc code của người khác chạy trong vòng lặp của mình
(§3.5). Không gọi tên được thì bỏ — khối `catch` cho ca không bao giờ xảy ra **nói dối** rằng chỗ này
có rủi ro. Bắt rồi thì phải **làm gì đó**: xử lý, hoặc log kèm ngữ cảnh rồi ném lại; nuốt exception là
biến lỗi tỏ thành lỗi âm thầm. `catch (Exception)` trần chỉ ở **biên trên cùng**: một vòng dispatch,
một entry point của tool.

**Quy ước chỉ thay được guard khi nó viết ra ở chỗ người vi phạm đang nhìn** — Tooltip, XML doc của
chính API đó, dòng trong tài liệu module — không phải một lượt chat (§5.4).

## 3.5 Async, vòng đời, và trình diễn

Tiêu chí: **hủy được** (việc dừng theo owner) · **giải phóng được** (thứ giữ tài nguyên có đường trả
lại) · **cô lập được lỗi** (một callback lỗi không kéo cả hệ chết — `try/catch` quanh từng callback
trong vòng dispatch).

| Luật | Nghĩa là |
|---|---|
| **Mỗi loop chỉ ra được token của nó là đời của ai, và đời đó dài đúng bằng nhịp** | nhận token của pha ngắn hơn đời nó thì loop **chết im lặng**: hủy là hành vi hợp đồng, thư viện async không log. Loop không có chủ thì ở Editor tắt domain reload, mỗi lần Play là một loop nữa xếp lên loop cũ. **Thân** vòng lặp và con số cấu hình về nơi giữ bảo đảm; **token** ở lại host vì host là thứ có đời sống |
| **Hàm mở một tiến trình theo thời gian sở hữu trạng thái cuối của nó ở mọi đường ra** | xong, bị hủy, thời lượng bằng 0 — cả ba đi qua **một** `try/finally` bọc toàn thân hàm, nên trạng thái cuối và callback xảy ra đúng một lần. Caller chỉ `await` và ghép bằng `WhenAll`, không `try/finally` |
| **Tiến trình chạm object Unity thì `finally` và callback guard `!= null`** | Destroy giữa chừng là một đường ra thật, và chạm object đã huỷ ném ngay trong `finally`. Guard bằng `if (x != null)`, không `x?.Foo()`: `?.` bỏ qua phép so sánh null đã overload của Unity. `onComplete` chỉ có **một** chủ: đã gán cho `finally` ngoài thì không truyền thêm vào lời gọi bên trong |
| **`CancellationTokenSource` có đúng một chủ dọn: chỗ tạo ra nó** | nơi khác chỉ `?.Cancel()`. `Cancel` chạy tiếp đồng bộ phần còn lại ngay trong lời gọi, nên `finally` của chủ có thể đã `Dispose` trước khi bên huỷ tới dòng kế |
| **Hàm mở một lượt sống bằng tài nguyên một lần dùng thì từ chối tái nhập ở cửa** | `Open()` chồng lên lượt đang mở là thay source mới trong khi người đang chờ còn giữ source cũ — họ treo **im lặng, vĩnh viễn**. Một dòng `if (isOpen) return;` rẻ hơn mọi kỷ luật |
| **Hủy giữa chừng phân loại theo thứ mất đi** | dữ liệu (tiến độ, grant, save) nhất quán trên mọi đường — grant đứng trước anim. Trình diễn chỉ cần không để rác trên màn hình: snap đích, tắt, xong |

> **Nền tảng** — lựa chọn mặc định và ràng buộc đi kèm:
>
> | Nhu cầu | Dùng | Ràng buộc không bỏ được |
> |---|---|---|
> | Async | **UniTask**, không coroutine, không `Task` | propagate `CancellationToken` xuống toàn bộ chain; tham số token **không mang `= default`** khi mọi caller đều có token — mặc định chỉ cho phép quên truyền mà compiler không báo |
> | Load asset | **Addressables** qua `AssetReference`, cho **đơn vị nạp theo nhu cầu** (màn, popup) | không string key; giữ `AsyncOperationHandle` để `Release()`. Prefab con nằm trong prefab cha đã nạp thì `[SerializeField]` kéo thẳng — nhưng con **chia đời sống với cha**: thứ phải hiện **khi cha đang đóng** không được là con của cha |
> | Tween | **PrimeTween**, không tự viết runner tween · `EaseType` tự viết chỉ giữ cho toán thuần | tween của UI đặt `useUnscaledTime`; object bị huỷ hay trả pool thì dừng tween của nó |
> | Anim UI | `Time.unscaledDeltaTime` · `DelayType.UnscaledDeltaTime` | popup phải chạy khi `timeScale` bằng 0 |
> | Data lớn | `NativeArray` / `NativeList` | khi truyền GPU hoặc Job System |
> | Tài nguyên nặng | cache `RenderTexture`, `Texture2D`… | có đường dọn dẹp trong `OnDestroy()` |
>
> `finally` đặt **trong** `while` chạy **mỗi vòng**, đặt ngay sau `Delay` chạy khi delay vừa xong — chỉ
> một `try/finally` bọc toàn thân mới cho "đúng một lần". `UniTask.Yield(ct)` **ném** ở `await` khi bị
> hủy, nên `if (ct.IsCancellationRequested)` đứng sau nó là code chết. Giá trị mặc định của tham số trên
> method `virtual` lấy theo **kiểu khai báo của biến lúc gọi**, không theo override.

**Trình diễn một thay đổi cho người chơi.** Dữ liệu đổi ở một nơi và người chơi thấy nó ở nơi khác, có
khi sau nhiều lần đổi. Luật cho mọi trình diễn kiểu đó:

| Luật | Nghĩa là |
|---|---|
| **Mốc "đã thấy" nằm trong save, cạnh sự thật; ghi mốc khi diễn xong** | mọi thứ trượt hay đếm từ A tới B cần một A người chơi đã xác nhận bằng mắt (`currAnimatedScore`, `lastSeenRankIndex` — quá khứ phân từ, §3.7). A sống trong instance view là sai: view bị tắt, dựng lại, hoặc có nhiều instance. Sự thật đổi bao nhiêu lần giữa hai lần xem cũng được — trình diễn đi từ mốc tới sự thật **một** lần. Ghi mốc **khi diễn xong**, ở một chỗ cho cả snap lẫn trượt: huỷ giữa chừng thì mốc chưa nhích, lần sau diễn lại. Reset chu kỳ reset cả mốc. Trình diễn **tách bước** cần save giữ **cấu tạo** của đúng một lần đổi chưa xem, xoá khi diễn xong |
| **Component dùng chung ở nhiều host thì mặc định thụ động; host cần dàn dựng gọi API `await` được** | bật lên chỉ đứng ở mốc; trượt khi host gọi `PlayAsync(ct)`. Không cờ chế độ trên instance (§3.1) |
| **Hiệu ứng "báo có thay đổi" ở đúng một điểm trình diễn; chỗ khác vẽ tĩnh** | cùng một hiệu ứng ở hai màn là diễn hai lần cùng một thay đổi |
| **Trạng thái nghỉ là trạng thái dựng trong prefab; đích trình diễn là ô developer đặt; mỗi lần hiện đặt lại về nghỉ** | vị trí lộ là `[SerializeField]`, không đọc ngầm `anchoredPosition` ở `Start` — hợp đồng không hiện trong Inspector (§3.6). Mỗi lần hiện, code đặt về nghỉ rồi mới hỏi có gì để diễn |
| **Lớp vẽ trên đè lớp dưới, không bật/tắt thứ bị che** | bật/tắt theo vị trí là một trạng thái nữa phải giữ đồng bộ với tween |
| **Giá trị đổi lúc tới nơi, không lúc bắt đầu** | đổi giữa đường là hai thông tin mâu thuẫn trên một vật |
| **Bước diễn thiếu dữ liệu thì bỏ bước, không rẽ nhánh cả luồng** | nhánh "ca này khỏi diễn" là một đường trình diễn thứ hai phải giữ đồng bộ với đường chính |
| **Service overlay giữ cơ chế; nội dung là canvas của consumer được nâng cùng** | kịch bản mới là consumer mới, không sửa service (§3.1) |
| **Nhiều module cùng trình diễn khi vào một màn thì một điều phối viên chạy theo giai đoạn cố định** | mỗi module tự khởi động thì chồng lên nhau; lease, hàng đợi, chờ một frame chỉ vá triệu chứng. Giai đoạn chia theo **loại** trình diễn, cố định trong code — **thay đổi tiến trình** → **hướng dẫn lần đầu** → **ép xem tiến trình** (mốc vừa đạt, tiến trình vừa mất) → **quảng bá** (khi các hệ đã ổn định); ưu tiên chỉ xếp các module trong cùng giai đoạn, và chỉ ưu tiên với cờ ép là cấu hình từ xa. Luồng dùng **tài nguyên độc quyền** (lớp tối màn, màn chồng) chạy **lần lượt**; luồng chỉ chạm view của mình chạy đồng thời. Mọi luồng **idempotent**: đọc save, chỉ làm phần còn nợ, kết thúc khi mọi thứ nó mở đã đóng, trả tài nguyên trong `finally`; `try/catch` quanh **từng** luồng. Yêu cầu chạy lượt **gộp lại** bằng một cờ xoá **trước** khi chạy lượt. Tín hiệu vào/ra màn đi vào điều phối viên qua **một** bridge của dự án; module không tự nghe nó. Vòng điều phối là class thuần để test không cần engine. Cơ chế đầy đủ ở tài liệu của hệ điều phối trong Horcrux |
| **Nhiều module cùng mất gì đó ở một sự kiện thì một luồng chung, định nghĩa sự kiện ở một chỗ** | "thế nào là thua" khai **một chỗ** cho mọi module, nếu không mỗi module vá một lỗ né khác nhau. Bắt đầu chờ, huỷ chờ và ghi mất mát có **một chủ gọi** (§3.4); cờ chờ nằm trong save của **từng** module (sang chu kỳ thì xoá cùng), gỡ cờ **trước** khi kiểm còn gì để mất. Luật vòng đời cờ ở lớp cha, không `virtual` — module chỉ khai: có gì để mất, hiện gì khi cảnh báo, cờ lưu ở đâu, mất thì mất gì |

## 3.6 Editor-first

Luật ở NT4; đây là nơi duy nhất dẫn giải. **Dấu hiệu code đang làm việc của Editor:** `GetComponent` /
`Find` / `AddComponent` / `Resources.Load` để lấy thứ đã có trên prefab · hằng số tinh chỉnh cảm giác
hardcode · dựng hierarchy bằng code · một API `Bind…` / `Attach…` nhận reference từ ngoài vào · đọc ngầm
một giá trị của prefab ở `Start` thay vì phơi ô.

**Kéo thả vào `[SerializeField]` là DI** (`D`, §3.1): consumer vẫn nhận vào, chỉ khác composition root
là **chính cái asset** thay vì một file code chạy lúc boot. **API nhận reference là code nối, dù mang
hình dạng DI** — `Bind(Transform)` chỉ chính đáng khi Inspector **thật sự không kéo được**, và câu đó
phải **kiểm**: cùng scene thì kéo được; khác scene hoặc prefab dựng lúc chạy thì không.

> **Nền tảng** — lý do gốc: dữ liệu serialize sửa được **không cần compile**, ai trong team cũng chỉnh
> được, thiếu thì lộ ra ô trống trong Inspector chứ không nổ giữa gameplay, và giá trị thật **đọc được
> bằng mắt ngay trên đối tượng**.

**Sổ tay** — reference kéo thả vào `[SerializeField]` · component add sẵn trên prefab · số tinh chỉnh
phơi ra Inspector · preset thành ScriptableObject · wire sẵn trong prefab rồi `Instantiate` ·
`RectTransform` của chính object cũng là một ô `[SerializeField]` thay cho cast — mọi thứ class chạm
tới đều hiện trên Inspector, không có tham chiếu ngầm nào phải đọc code mới biết.

**Giá của kéo thả: wiring trong asset không grep được** — chỉ tồn tại dưới dạng GUID trong file
serialize, nên chi phí đọc của NT1 cộng thêm *"bao nhiêu asset phải mở Editor mới thấy"*; mọi agent đọc
repo bằng text đều trả khoản này. Không phải lý do bỏ kéo thả; là lý do **không rải** nó — biên là câu
phân xử ở NT4: **ô trống chỉ lộ ra khi có người nhìn vào ô đó.**

**Bắt buộc phải nối bằng code thì bên đời ngắn tự trình diện với bên đời dài.** Bên đời ngắn biết chính
xác lúc nó xuất hiện và biến mất, nên đăng ký và huỷ đăng ký là **một cặp trong một file**. Cặp chọn
theo đời object và **phải xong trước tín hiệu cần tới nó**: view tự ẩn bằng cách tắt chính nó thì cặp
bật/tắt gỡ nó khỏi chủ ngay lần ẩn đầu; tín hiệu phát ngay sau khi bật cây object thì `Awake` cả cây đã
chạy còn `Start` thì chưa (§3.1). Cặp đúng là `Awake`/`OnDestroy`.

**Điều kiện dựng hệ ghi vào tài liệu, không sinh code canh** (NT4). Tạo asset, dựng GameObject, gán
reference, đặt layer, thêm scene vào build — người dựng làm **một lần**, viết thành mục **"Trước khi
chạy"** của tài liệu module (§5.1); thiếu thì xử lý theo bảng guard ở §3.4. **Bước setup bằng tay là
cách developer hiểu hệ**: người tự kéo asset vào ô giữ được trong đầu các mảnh rời và chỗ chúng nối vào
nhau — thứ duy nhất dùng được khi hệ hỏng lúc 2 giờ sáng. Hệ không gánh lỗi quên setup; đổi lại hệ
**nợ** một mục "Trước khi chạy" đủ để dựng lại từ 0.

## 3.7 Naming — self-documenting code

- **Tên là lời giải thích thứ nhất, comment là phương án cuối.** Phải kèm comment mới hiểu nghĩa là
  tên chưa đạt — đổi tên trước. Trần là **5 từ dài**, 6–7 từ ngắn đọc một mạch vẫn đạt
  (`elapsedSecondsSinceLastSave`). Viết tắt **quen mắt** — `curr` · `prev` · `max` · `min` · `Utc` —
  không tự chế. Viết tắt rồi vẫn vượt trần là khái niệm chưa tách đủ.
- **Tên mang đủ ngữ nghĩa về giá trị nó giữ** — đọc tên biết *cái gì*, *lúc nào*, *đã xảy ra gì*:
  - *Lúc nào / thứ tự*: `curr` · `last` · `prev` · `next` — `currLevelIndex`, không `levelIndex`. Field
    cùng nói về một đối tượng dùng chung tiền tố.
  - *Đếm hay vị trí*: hậu tố `Amount` cho số lượng, `Index` cho vị trí. Danh từ số nhiều đứng một mình
    đọc như một danh sách; type mang đúng một `ItemIndex` thì tên là `ItemClaimed`, không `ItemsClaimed`.
  - *Tổng dồn*: tiền tố `total`.
  - *Khoá tra cứu*: hậu tố `Name` cho **chuỗi**, `Id` cho **số**. `…Id` giữ một chuỗi làm người đọc đi
    tìm một bảng số không tồn tại.
  - *Đã xảy ra gì*: quá khứ phân từ theo góc nhìn người chơi hay dữ liệu — `collected` · `animated` ·
    `claimed`: `currAnimatedScore` (số người chơi đã thấy chạy tới). Không dùng từ mờ (`introDone`) hay
    từ tả hành động của hệ (`presentedScore`).
  - *`-ing` là tiến trình, không phải trạng thái*: `IsPanelOpen` khác `IsPanelOpening`.
  - *Biến cục bộ cùng luật*: tính từ đứng một mình không phải tên; tính từ đứng trước danh từ theo
    tiếng Anh (`inclusiveEndIndex`).
- **Tiền tố loại type**: interface `I…`, abstract class `A…` — nhìn tên biết ngay không `new` được.
- Tên method nói rõ **mục đích**: `EnsureMaterial()`, `SwapWriteBuffer()`. Tên vô nghĩa cần thay:
  `Process`, `Handle`, `DoWork`. Boolean đọc như một câu hỏi: `IsPickable`, `HasPendingInput`. Cờ nói
  **cái gì** đang chờ hay đang chạy, không mượn hình ảnh cơ chế: `hasPendingPassRequest`, không `isDirty`.
- **Tên hàm khớp thứ nó trả về, không khớp thứ nó dựa vào**: hàm trả **một điểm** trên đường cong là
  `EvaluateQuadraticBezier`, không `GetQuadraticBezierCurve`. `Evaluate` cho lấy mẫu tại `t`, `Compute`
  cho dẫn xuất một đại lượng.
- **`Setup(data)` cho view nhận dữ liệu để vẽ; `Bind`/`Attach` chỉ cho nối reference** (§3.6).
- **Casing theo quyền truy cập, không theo loại member**: private field camelCase · public field (data
  class, DTO) PascalCase như property — người đọc ngoài class thấy `step.Goal`, không cần biết nó là
  field hay property. Khoá wire format sinh từ tên field thì casing là hợp đồng: chốt trước khi bản dữ
  liệu đầu tiên rời máy developer.
- **Hai chế độ loại trừ nhau là `enum` hai phần tử, không `bool`**: `ScrollDirection.Upwards` đọc được
  ở cả Inspector lẫn call site; `bool` chỉ nói một phía, và chế độ thứ ba thêm vào `bool` là đổi kiểu.
  **Chuỗi bước có bước lúc có lúc không là `enum` bước, không bộ đếm**: enum nói thẳng đang ở đâu; bước
  đầu có hay không quyết **một lần** lúc mở.
- **Đại lượng hình học đặt tên theo hai đầu mút**, dạng `<từ>To<đến>Dist`: `pivotToTopViewDist`. Đủ hai
  đầu thì phép trừ `A − B` đọc thành hình vẽ ngay tại dòng code.
- **Comment chỉ khi thật sự cần — mặc định là không có.** Đoạn nào cần comment mới theo dõi được thì
  tách hàm có tên, đảo điều kiện, đặt biến trung gian — sửa code, không chú thích code. Comment còn lại
  **chỉ** nói **tại sao** (quyết định trái trực giác, bẫy đã sai một lần, công thức nguồn), không bao giờ
  nói **cái gì**. API public có XML doc, `<param>` cho mọi tham số có contract không hiển nhiên. Trần:
  **17–20 từ** cho comment và tooltip · **35 từ** cho `<summary>` · **15 từ** cho mỗi thẻ còn lại —
  chạm trần là sửa gốc trước khi viết thêm chữ, hoặc phần đang viết thuộc về tài liệu (§5).
- **Một từ = một nghĩa trong toàn hệ thống.** Khái niệm không đặt nổi tên riêng thường là khái niệm
  chưa rõ. **Tên hệ theo cơ chế nó cung cấp, không theo người dùng đầu tiên**: hệ overlay đặt tên theo
  tutorial thành sai khi màn nhận thưởng cũng dùng nó. **Trùng tên với một lớp di sản dự án cố ý giữ**
  (contract đóng băng để kéo module từ dự án khác) thì framework **giữ tên của nó**; dự án giải trùng
  bằng alias `using` ở đúng file lớp nối — không đổi tên framework theo di sản của một dự án.
- **Đổi tên là đổi cả hệ** (NT10): code, comment, chuỗi debug, mọi tài liệu — cùng một lần làm, kiểm
  bằng grep; file `.cs` và thư mục đổi tên **trong Project window của Unity** để giữ `.meta` và GUID.
  **Ranh giới:** khoá wire format và dữ liệu đã serialize là **hợp đồng với hệ khác** — không đổi theo.
  Asset trong repo **không** thuộc ranh giới đó: field `[SerializeField]` đổi tên thì đổi luôn key trong
  YAML, cùng lần làm — không gắn `FormerlySerializedAs`. Attribute đó là cầu, chỉ cho asset ở chỗ không
  sửa tay được (nhiều scene, prefab variant, dữ liệu người dùng), và gỡ **sau khi** asset đã lưu lại
  theo tên mới. *Đã sai một lần:* gỡ cầu khi asset còn key cũ — Unity nạp thành entry rỗng, không một
  dòng lỗi lúc import, hệ tự tắt ở lần Play đầu.

## 3.8 Dữ liệu đi qua tool — import/export

File mà tool đọc–ghi là **của người dùng và của hệ khác**, không phải của tool:

| Luật | Nghĩa là |
|---|---|
| **Không bao giờ sửa giá trị dữ liệu import** | ngoài tầm hợp lệ thì **từ chối cả file**, hoặc **bỏ qua entry đó** kèm cảnh báo. Clamp là xuất ra bản lệch mà không ai biết |
| **Bỏ entry hỏng, không bịa entry mới** | chèn dữ liệu để "sửa giúp" là thêm thứ không có trong file — tệ hơn thiếu |
| **Chỉ sở hữu phần mình hiểu** | export clone dữ liệu gốc rồi ghi đè đúng những khoá tool sở hữu; khoá lạ **đi qua nguyên vẹn** |
| **Dữ liệu gốc đi theo đối tượng** | giữ tham chiếu bản gốc trên chính đối tượng, không tra lại theo toạ độ hay vị trí mảng lúc export |
| **Import và Export sống cạnh nhau** | hai chiều của cùng cụm dữ liệu trong một file; nghiệm thu bằng round-trip (§4.3) |

## 3.9 Editor tool

Tiêu chí: **một tool là một đơn vị gói kín** · **thứ dùng chung sống một chỗ** (§3.2) · **thứ không
đổi giữa các lần vẽ lại phải có sẵn**.

> **Nền tảng** — IMGUI (`OnGUI`, `EditorWindow`, `PropertyDrawer`): một lần tương tác gây nhiều lần gọi
> `OnGUI` cho **cùng một state**. `GUIStyle`, `GUIContent` có icon chỉ tồn tại sau khi `GUI.skin` và
> `EditorGUIUtility` sẵn sàng, nên không khởi tạo được ở static initializer.

**Sổ tay** — *áp cho IMGUI*; UI Toolkit có mô hình repaint khác nên bảng này không áp:

| Cấp cache | Kỹ thuật | Khi nào dùng |
|---|---|---|
| Static eager | `static readonly` | giá trị bất biến: `Color`, `GUILayoutOption[]`, `GUIContent` chỉ có text |
| Static lazy + guard | init một lần trong `EnsureStyles()` | thứ cần `GUI.skin` hoặc `EditorGUIUtility` |
| Dirty-flag | chỉ rebuild khi dữ liệu đổi | layout options khi window resize |
| Event-phase | tính nặng chỉ ở `EventType.Layout` | filter, sort, format — `Repaint` dùng lại kết quả |

**Tool có người dùng thì UX là một phần của thiết kế:** vùng UI phải nói lên ranh giới — thứ khác vai
trò (ghi vào dữ liệu / chỉ đổi cách xem / dùng ở mọi lúc) không nằm chung một vùng · điều kiện vẽ và
điều kiện bấm được suy từ một nguồn (§3.4) · **không giấu thứ có thật**: điều kiện vẽ là *"có dữ
liệu"*, không phải *"tra được tài nguyên để vẽ"* — tra thiếu thì vẽ dạng báo lỗi kèm id; field chỉ-đọc
vẫn phải hiện, khác kiểu với field sửa được · phép kiểm tính hợp lệ chạy **khi được hỏi**, không chạy
nền theo mỗi thay đổi: lúc đang dựng thì dữ liệu **luôn** chưa hợp lệ.

---

# §4 — Hệ toán học và vật lý

## 4.1 Khi nào dùng tới toán, và sâu tới đâu

Mở ra khi **developer yêu cầu**, hoặc khi **agent thấy toán giải bài toán tốt hơn hẳn** — trường hợp
sau thì nêu kèm được–mất để developer quyết (NT5).

**Mặc định là không cần toán.** Phần lớn logic gameplay là trạng thái và luật rời rạc. Xét hết cách rẻ
hơn và dễ chỉnh hơn trước: `AnimationCurve` hoặc bảng tra do designer chỉnh trong Inspector (NT4) ·
easing có sẵn · lerp · máy trạng thái · một hằng số chọn bằng tay. **Dấu hiệu đang ép toán vào chỗ không
cần:** phải dẫn định luật nền để biện minh một phép nhân · công thức chỉ có một call site và không tham
số nào thay đổi · designer không chỉnh được gì. **Bờ vực còn lại cũng sai:** bài toán vốn liên tục và có
ràng buộc — dừng đúng chỗ, va chạm, nội suy cần đạo hàm liên tục — mà né toán thì thành một đống hằng số
không ai hiểu. Toán đúng chỗ làm code **ngắn hơn**.

**Đã cần toán thì sâu vừa đủ cho tính năng:** xấp xỉ là **mặc định** (NT8). Khi **thật sự** cần bản đầy
đủ thì **làm tử tế**: cắt nửa vời rồi bù bằng hằng số là cách sinh ra hệ không ai dám sửa.

**Núm phơi ra là đại lượng người tune nhìn thấy trên màn hình, không phải tham số trung gian của công
thức.** Biên độ là đúng độ lệch thật, số lần lắc là đúng số đếm được bằng mắt. Hình dạng chuyển động gọi
tên được theo pha thì dựng từ hàm có sẵn đúng hình dạng đó, thay vì công thức tự chế phải dẫn giải mới hiểu.

**Chuyển động mà quãng đường không cố định — khoảng cách, chênh lệch giá trị, độ dài đoạn còn lại — thì núm
là tốc độ, không phải thời lượng.** Thời lượng cố định trên quãng đường đổi làm tốc độ cảm nhận đổi theo: đoạn
ngắn lê thê, đoạn dài vội. Tốc độ giữ một nhịp thống nhất ở mọi quãng đường, và chuyển động bị ngắt giữa chừng đi
tiếp từ chỗ đang đứng mà không đổi nhịp. Thời lượng chỉ là núm đúng khi quãng đường cố định giữa hai mốc đã định.

## 4.2 Cần thì phải cho hiểu sâu

Người đọc phải **hiểu hiện tượng** · **tin công thức là suy ra được** · **kiểm lại được** bằng tay.
Mạch dưới dành cho công thức **không hiển nhiên**.

| # | Câu hỏi người đọc sẽ hỏi | Phải trả lời được gì | Sổ tay — dạng trình bày |
|---|---|---|---|
| 1 | Cái này mô tả hiện tượng gì? | mô hình thực tế đằng sau, và nó map sang mục đích thế nào | bảng "thành phần → vai trò" |
| 2 | Vì sao mô hình đó đúng? | định luật hoặc định lý gốc | diagram |
| 3 | Phương trình là gì? | phương trình chi phối, ý nghĩa từng ký hiệu | `$$…$$` kèm bảng ký hiệu |
| 4 | Vì sao chọn cái này? | các lựa chọn đã cân, tiêu chí loại | bảng so sánh có cột ✓ |
| 5 | Từ phương trình gốc ra nghiệm trong code thế nào? | từng bước biến đổi, **không nhảy bước** | đánh số ①②③ |
| 6 | Làm sao tin nghiệm này đúng? | giá trị tại các mốc biên so với kỳ vọng | bảng "mốc → kỳ vọng → ✓" |

Không thương lượng: **trực giác trước, ký hiệu sau** · **suy ra, không áp đặt** — công thức chốt *dẫn
ra* từ nguyên lý gốc · **thứ chọn bằng cảm giác thì nói thẳng** "chọn bằng tai và mắt, số này cho cảm
giác X" — đừng bịa dẫn giải vật lý cho nó, vì nó làm hỏng niềm tin vào phần thật sự có dẫn giải · **lệch
vật lý chuẩn là bình thường** (NT8) — chỉ nêu lệch ở đâu, vì sao, khi nào mới cần bản đầy đủ.

## 4.3 Đối chiếu công thức với code

Mỗi công thức đã chốt map sang code bằng một **phép kiểm chạy được**, không bằng cảm giác "trông
giống" (NT8). Đây là đối chiếu **công thức với code**, không phải nghiệm thu cảm giác chơi (§2.8).

**Sổ tay** — hệ nào có phép kiểm phù hợp hơn thì dùng cái đó:

| Phép kiểm | Cách làm |
|---|---|
| Đối chiếu từng số hạng | mỗi công thức chốt map thẳng một dòng code; kiểm **từng hệ số và từng dấu** |
| Kiểm mốc chéo | giá trị biên ở phần toán phải khớp bảng kiểm chứng của task |
| Đạo hàm số | so `f'(t)` với `(f(t+h)−f(t−h))/2h`, `h=1e-4` |
| Round-trip | cặp converter hoặc overload: `A→B→A` phải về gần chính nó |

---

# §5 — Tài liệu

Mỗi hệ thống và mỗi tool có tài liệu riêng, đặt cùng thư mục với nó. **Thay đổi hệ thống thì cập nhật
tài liệu trong cùng lần làm** — riêng `.html` theo nhịp mốc. Một utility lẻ trong thư mục phẳng mà có
tài liệu hay demo đi kèm thì **xuống thư mục con mang tên nó** (`.cs` · `.md` · `.html` cạnh nhau).

| Loại | Vai trò | Vòng đời |
|---|---|---|
| **`.md`** | tài liệu **agent đọc** để hiểu và phát triển hệ — bản đặc, plain text, rẻ token; nguồn sinh `.html` | cập nhật **cùng lần làm** với mỗi thay đổi |
| **`.html`** | tài liệu **developer và game designer đọc** — trực quan hóa 100% nội dung `.md`; agent không đọc bản này khi đã có `.md` | đồng bộ từ `.md` theo **mốc** — developer yêu cầu, hoặc chốt xong một cụm thay đổi |
| **Plan** | để developer **tự gõ** — khi developer yêu cầu, và mọi code vào Horcrux (§2.4) | luôn là `.md`; theo task |
| **Manual** (khi tool có người dùng không phải developer) | người dùng đọc để **thao tác** — luật viết ở §5.4 | sống cùng tool |

**Ai viết: agent, cả bốn loại.** Nghiệm thu tài liệu là **đối chiếu máy móc với code** (§2.8). Developer
**chốt nội dung**: dòng nào lệch thiết kế thì developer phân xử, agent sửa.

**Quy trình:** phỏng vấn ngữ cảnh (§2.1) và đối chiếu hiểu biết về code (§2.2) → đọc **tất cả** source,
hiểu 100% data flow, lifecycle, lý do mỗi quyết định → viết `.md` → sinh `.html` từ `.md` → khi được
yêu cầu thì viết Plan.

**Chuỗi sự thật một chiều `code → .md → .html`** (§3.4): code là chuẩn, hai tài liệu phản ánh **100%
thiết kế đang chạy trong code**; `.html` dựng được từ `.md` mà **không mở source**. **Không gộp, không
xóa** hai bản. Lệch thì sửa xuôi theo chuỗi: `.html` lệch → đối chiếu `.md` với code trước, rồi đồng bộ
`.html` từ `.md`. "100%" là không mất nội dung khi chuyển bản, không phải viết cho nhiều — trần độ dài
do NT10 canh.

**Sổ tay** — checklist mỗi lần đồng bộ: soát "bản chết" (NT8) và "ở thì hiện tại" (§5.4) · đổi tên hay
xóa file tài liệu thì grep **tham chiếu chết** trong code comment, file hướng dẫn agent của dự án, tài
liệu khác.

## 5.1 `.md` — tài liệu cho agent

Tổ chức theo **đường đi của dữ liệu** (input → processing → output), không theo "lý thuyết → thiết kế →
code": người đọc cần lần theo được một giá trị từ lúc vào đến lúc ra. Luồng dữ liệu vẽ bằng ASCII vì
`.md` được đọc bằng nhiều công cụ. **Code chỉ xuất hiện khi đoạn code *là* thứ cần minh hoạ, và khi đó
trích nguyên văn**, không viết lại; chữ ký API thành bảng (§5.4).

**Một mục bắt buộc: "Trước khi chạy", với mọi hệ cần wire tay** (§3.6). Nghiệm thu: người chưa từng mở
hệ dựng lại được **từ 0** chỉ bằng mục này, theo thứ tự, không đọc code hay hỏi ai. Viết bằng **thao
tác và nhãn thật trên UI** — đường dẫn menu, tên ô trong Inspector, thứ kéo vào ô nào — mỗi bước kèm
*"thiếu bước này thì hỏng ở đâu"*. Hệ không cần wire gì thì không có mục này.

**Còn lại là kho mục để chọn, không phải form để điền.** Mỗi mục đưa vào phải gọi tên được **câu hỏi
của người đọc** mà nó trả lời; hệ nhỏ có ba mục là bình thường. Kho: Data structures · Core algorithm ·
Lifecycle · Framework integration · Design decisions (nội dung theo §2.5, kèm "đã sai một lần") ·
Safety và error · Platform issues · Architecture (file tree kèm vai trò) · Testing · Extension ·
Performance.

**Nghiệm thu:** lần theo được một giá trị từ input tới output mà không nhảy section · mỗi so sánh đều
thấy **tiêu chí** và **kết luận**.

> **Nền tảng — KaTeX:** mọi lệnh có `\` (`\frac`, `\sqrt`…) **phải** nằm trong `$…$` hoặc `$$…$$`;
> trong backtick sẽ hiện raw text. Mỗi block `$$…$$` trên **một dòng**. Chốt xong quét: strip hết
> `$…$` và backtick, còn sót `\[a-zA-Z]` nào là lọt.

## 5.2 `.html` — tài liệu chính

Giữ **cấu trúc section của `.md`** để hai bản đối chiếu được; số liệu trong demo cũng là "bản chết" nếu
không đối chiếu lại code (NT8). Trực quan hóa **theo loại nội dung**: so sánh thì bảng · luồng dữ liệu
thì diagram · quan hệ định lượng thì công thức · giá trị biến thiên liên tục thì Canvas · quá trình
nhiều bước thì step. Demo chỉ làm khi bảng và text **không đủ** để thấy hành vi.

**Nghiệm thu:** đủ 100% nội dung nguồn · single file · TOC khớp section thật · đọc được trên màn hình
nhỏ · mở trang không tương tác thì không tiến trình nào chạy · mỗi demo thao tác được và cho thấy đúng
hành vi đang nói tới.

**Sổ tay** — copy `DOCS_TEMPLATE.html`, thay các chỗ `{…}`, xoá section mẫu và khối demo mẫu. Khối xây
sẵn và **bảng cấm trong draw loop của Canvas/DOM** nằm trong khối hướng dẫn ở đầu file đó (NT7). Không
dùng thư viện tô màu code (NT1).

## 5.3 Plan — để developer tự triển khai

Tiêu chí: **tự chứa**. Developer code lại được từ đầu đến cuối mà **không phải suy đoán**, không phải
mở tài liệu khác. Các task xếp theo **thứ tự phụ thuộc**, mỗi task chỉ cần thứ đã có ở task trước. Hệ
có lõi toán thì mục `§0` của Plan dẫn giải tại chỗ theo mạch §4.2.

**Lõi developer viết, test agent viết.** Test thì agent viết và chạy (§2.8), nên Plan **chỉ có danh sách
case sẽ kiểm**, không có code test. **Nhịp:** developer code xong lõi → agent đọc code thật rồi mới
viết test, vì chữ ký lúc viết Plan còn là nháp. Việc phát sinh giữa lúc đang làm Plan thì đầu ra là
Plan, không tạo file code; chỗ đặt đầu ra chưa rõ thì hỏi một câu (NT5). *Đã sai một lần:* tạo hai file
`.cs` mới khi developer đang chờ một mục trong Plan.

**Plan chạm cả Horcrux lẫn consumer thì phần Horcrux chỉ thêm** — member mới mang mặc định (`virtual`),
API cũ giữ nguyên — để task framework xong là compile sạch và consumer chưa áp dụng vẫn chạy như cũ;
mỗi task sau cũng là một mốc compile sạch. Xoá API cũ là việc riêng, sau khi mọi consumer đã chuyển.

**Developer gõ lệch plan là bình thường** (NT6). Lượt kiểm của agent sau mỗi step: compile, rồi **so
code với plan và phân loại từng chỗ lệch** theo §2.6.

**Hai loại Plan, hai hình dạng code.** Hệ **chưa có code** thì mỗi khối là **một file trọn vẹn** để
chép. Hệ **đang có code** thì Plan là **chuỗi chỗ đổi theo thứ tự**: mỗi bước một chỗ — file · dòng
hiện tại · đoạn cũ → đoạn mới; dòng không nhắc là dòng giữ nguyên. Chép cả file cho hệ đang có là bắt
developer tự so từng dòng để tìm chỗ khác — đúng việc Plan phải làm hộ.

**Dẫn giải của Plan dừng ở luồng; Editor setup thì đầy đủ.** Người đọc Plan là developer đang có agent
bên cạnh, nên kênh bù cho chỗ chưa rõ là **hỏi trực tiếp**; câu hỏi lặp lại thì ghi câu trả lời ngược
vào Plan (§5.4). Chiều gọn đó **không áp cho Editor setup**: bước tay thiếu không có compile error nào bắt.

**Sổ tay** — kho phần cho mỗi task, **chỉ lấy phần task này cần**: bảng "Đã khảo sát" (§2.4) · Files (đường dẫn chính xác) ·
Interfaces (consumes và produces, chữ ký đầy đủ) · bảng "toán → code" trỏ về `§0` · bảng lý do cho
mỗi quyết định thiết kế — ghi *quyết định và vì sao*, không kể *code làm gì* · **code hoàn chỉnh dán
được** · **Editor setup** — mục riêng của task · **bảng case kiểm thử** (input → kỳ vọng, kèm biên theo
§2.8) · **danh sách cheat** theo tiêu chí ở §2.8.

**Nghiệm thu riêng:** có mục "Ngữ cảnh đã chốt" (§2.5) · mọi hàm có caller thật, hoặc có lý do phòng
xa chữ ký nói được ra (NT1) · code **vừa đủ**, **đúng công thức đã chốt**, **đúng nhịp** (NT2),
**self-document** (§3.7) · công thức đã đối chiếu với code (§4.3) · phần sẽ test có bảng case, không
có code test.

## 5.4 Kỷ luật viết và bảo trì — áp cho mọi loại tài liệu

**Cắt trước khi giao — bắt buộc** (NT10). Bản đầu luôn có nước. Cắt theo thứ tự: câu dẫn *"phần này sẽ
nói về…"* · tóm tắt lại thứ vừa nói · câu chuyển tiếp · nhận xét về chất lượng thiết kế · ẩn dụ · cùng
một ý viết hai lần · lý do dài hơn một câu cho một khẳng định hiển nhiên. Cắt xong mà vẫn dài thì nội
dung thật sự lớn — **tách file**, đừng nén chữ. Cách viết câu theo §2.3.

| Luật | Nghĩa là |
|---|---|
| **Mỗi tài liệu một người đọc** | mỗi loại trả lời đúng câu hỏi của người đọc nó; chép nội dung loại này sang loại kia là sai cả hai. Cặp `.md`–`.html` không thuộc lỗi này: cùng nội dung, hai người đọc. Manual viết theo **nhãn thật trên UI**, trả lời *"bấm gì ra gì, dùng khi nào"* — không tên class |
| **Không chép lại thứ người đọc đã cầm trong tay** | `.md` và `.html` viết cho người mở được **code** — cột "vai trò" nói lại đúng tên hàm, metrics `O(n log n)` của một `Sort`: **bỏ**. Plan viết cho người **chưa có code**; Manual cho người **không đọc code**. Phép kiểm: *xoá dòng này thì người đọc mất gì* — đáp án "mở file kia ra xem" thì bỏ. **Ngoại lệ:** danh sách chữ ký API trong `.md`, vì `.html` dựng từ `.md` mà không mở source |
| **Code đã tồn tại thì khối code trong Plan là bản sao nguyên văn** | đồng bộ bằng cách chép file rồi **so sánh máy**, không gõ lại (NT8). **Sổ tay** — script dò mỗi khối mở bằng `using`/`namespace` tới file `.cs` giống nhất, thay nguyên khối; chạy lại tới khi báo 0 khối lệch, rồi mới sửa prose tay |
| **Thao tác Editor là một mục riêng** | mọi việc developer phải làm bằng tay trong Editor gom về **một mục riêng** ở mọi đầu ra: Plan (Editor setup của task) · tài liệu module ("Trước khi chạy") · **câu trả lời trong chat**. Bước setup trộn vào logic là bước bị đọc lướt qua rồi quên |
| **Viết cho người đọc lần đầu, ở thì hiện tại** | NT10 áp cho tài liệu. **Ngoại lệ duy nhất:** sổ ghi bẫy *"đã sai một lần"* trong mục quyết định thiết kế — ghi **bài học**, không tường thuật thay đổi, không lưu tên cũ. **Sổ tay** — grep các cụm kể lịch sử ("trước đây", "bản cũ", "giờ đã") |
| **Mâu thuẫn thì SỬA dòng cũ** | không thêm dòng thứ hai nói ngược. **Riêng dòng cũ ghi quyết định hoặc ranh giới do developer đặt thì không tự sửa** — nêu chỗ lệch để developer phân xử (NT5) |
| **Tri thức sinh trong hội thoại phải vào tài liệu trước khi báo hoàn thành** | ba nguồn, một đích: *câu hỏi của người đọc* là bằng chứng tài liệu chưa rõ — câu trả lời phải để lại trong tài liệu · *bug "sai âm thầm" vừa sửa* thành một dòng "đã sai một lần" · *quy ước mới* xác lập trong task — ghi ở **tài liệu module** dưới dạng quy ước ở thì hiện tại, không tường thuật task. Quyết định chỉ sống trong hội thoại thì chết cùng hội thoại. **Liệt kê nguyên văn các dòng đã ghi trong báo cáo** để developer veto; đủ chung cho mọi dự án thì vào file này theo §2.6 |
