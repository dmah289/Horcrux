# MY_SKILL.md — Tư tưởng thiết kế hệ thống & quy trình làm việc với AI

Dành cho **game trên Unity**. Tư tưởng, không phải tài liệu của một dự án — mang sang dự án nào cũng
dùng nguyên. Áp cho mọi thứ viết ra: runtime, editor, tooling, script tạm, tài liệu.

**Ai đọc:** AI agent, trước khi làm bất cứ việc gì · developer, khi review output.
**Đi kèm:** `DOCS_TEMPLATE.html` (§5.2). Chỉ đọc được một mục thì đọc **§0**.

## Ba tầng ràng buộc

| Tầng | Nhận biết | Nghĩa | AI được phép |
|---|---|---|---|
| **Luật** | *mặc định*, không đánh dấu | tiêu chí nghiệm thu: nói **cần đạt gì**, không nói **làm thế nào** | không bỏ; tự chọn cách đạt |
| **Nền tảng** | khối `> **Nền tảng**` | đáp án đã chốt: **giới hạn thật** của Unity, browser, renderer — hoặc **stack mặc định** của bộ này | không đi đường khác. Dự án thiếu món nào thì quay về tiêu chí ở phần Luật ngay trên khối |
| **Sổ tay** | dòng `**Sổ tay** —` | một cách đã dùng và chạy được; không phải cách duy nhất | thay bằng cách hay hơn, kèm lý do và phép kiểm (NT6) |

**Ký hiệu:** `NT<n>` là nguyên tắc số n ở §0–§1 · `§x` là mục trong file này. **Số hiệu chỉ sống trong
chính file này** — code comment, tài liệu module và plan viết lý do bằng **nội dung** ("chép công thức
ra chỗ thứ hai thì hai bản sẽ lệch"), không bằng **con trỏ** ("vi phạm §3.4"): người đọc tài liệu đó
không mở file này ra tra, và con trỏ mục nát âm thầm khi cấu trúc đổi.

§0 và §1 là nền, áp cho mọi việc; §2–§5 là cách đạt cho từng loại việc, tra khi chạm tới. Sổ tay nào
ghi **điều kiện áp dụng** thì ngoài điều kiện đó là vô nghĩa.

---

# §0 — Bốn ưu tiên đứng trên

Khi phải cân đo, bốn thứ này thắng. **Không đánh đổi âm thầm** — hy sinh cái nào thì nói ra tại chỗ và
nói giá. Nơi **duy nhất** định nghĩa NT1–NT4; các mục sau chỉ trỏ về.

| # | Nguyên tắc | Nội dung |
|---|---|---|
| 1 | **Vừa đủ — lõi trước, đọc một mạch, mở đường mai** | **Đơn giản là mặc định**; mỗi lớp phức tạp thêm phải trả bằng **một nhu cầu đang có thật** — "phòng khi cần", "cho đầy đủ", "chuẩn hơn" không phải nhu cầu. **Kiểm nhanh:** *xoá nó đi thì hỏng ở đâu* — không gọi được tên chỗ hỏng thì bỏ. **Giá của một dòng code nằm ở mọi lần sau**, không ở lúc viết; agent sinh code gần như miễn phí nên hay quên vế này — đó là gốc của over-engineering. **Chi phí đọc là cùng khoản tiền đó và đo được:** *số file người đọc sau phải mở để lần hết một luồng*. Hai bờ vực: **xé vụn** (một tính năng rải qua nhiều component, nhiều lớp, hay một cây composite — mỗi mảnh đúng nhưng không mảnh nào nói được hành vi) và **gom bừa** (một class ôm nhiều lý do thay đổi). Mặc định là **bậc thấp nhất còn đọc được**; leo bậc phải **gọi được tên thứ bậc dưới không làm được** (§3.1). Composite còn là chi phí runtime: dispatch ảo, con trỏ nhảy, message engine nhân theo số mảnh (NT2). **Thứ tự làm:** mốc đầu là **lõi chạy được và kiểm được** từ input tới kết quả quan sát được; ca biên chưa có input thật, cấu hình chưa có call site, tối ưu chưa có số đo là mốc sau. "Lõi trước" cắt theo **chiều sâu**, không cắt vào mục đích — mục đích là **sàn**, giao mốc không chạy được là thiếu chứ không phải gọn. **Mở đường mai** là cách sắp xếp, không phải thêm số lượng: hình dạng để bước kế tiếp là **thêm vào**, không phải đập ra làm lại; phòng xa chỉ dồn vào **chữ ký và ranh giới trách nhiệm**, vì hàm thêm sau tốn hai phút còn chữ ký sai sửa sau rất đắt. **Nghiệm thu:** gọi được tên bước kế tiếp và chỉ ra nó là "thêm" chứ không phải "sửa". → **§2.4**, **§3.1** |
| 2 | **Hiệu năng runtime — mỗi phép tính khai được nhịp của nó** | Nhịp là *mỗi frame* · *mỗi tương tác* · *mỗi lần dữ liệu đổi*. Hàm chạy trong vòng lặp phải trả lời được nó thuộc nhịp nào; đặt ở nhịp nhanh hơn mức cần thì **không có gì báo sai**, chỉ có hệ chậm dần. Ba việc **luôn làm, không cần đo** vì không làm code khó đọc hơn: khai đúng nhịp · **không tạo rác ở chỗ chạy lặp** · không tính lại thứ không đổi. Tối ưu **đánh đổi bằng độ khó đọc** thì phải đo trước, chỉ ở hot path đã xác nhận; ngoài đó chọn bản dễ đọc nhất. Cách rẻ nhất thường là **làm phép tính biến mất**, không phải làm nó chạy nhanh hơn. → **§3.3** |
| 3 | **Module hoá — hệ độc lập là mặc định, mang đi được** | Mỗi hệ trả lời được: *bê sang dự án sau thì phải sửa gì?* Mặc định là **không sửa gì** — hệ không gọi tên type của dự án, phụ thuộc đi **một chiều: dự án → framework**. **Hệ kết hợp** (dựng trên nhiều hệ khác nên không rời đi một mình được) là ngoại lệ phải gọi tên lý do và chỉ ra đường tách. Tính mang đi được đến từ **chiều phụ thuộc và chỗ đặt file**, không từ lớp trừu tượng — nên không phải phòng xa: quyết đúng từ đầu gần như miễn phí, sửa sau thì đập cả cây phụ thuộc. → **§3.2** |
| 4 | **Editor-first — code lo hành vi, Editor lo cấu hình và kết nối** | Thứ gì **làm chắc chắn được lúc authoring** thì không sinh code cho nó. **Phép kiểm là *chắc chắn*, không phải *tiện*.** Đang viết code chỉ để **tìm, nối, hoặc gán** thứ vốn đã tồn tại lúc authoring thì code đó đặt sai chỗ. Việc bắt buộc làm trước khi build là **một bước ghi trong tài liệu**, không phải code canh lúc chạy — và **tự tạo tệ hơn tự canh**, vì nó giấu việc setup còn thiếu. Ngoại lệ nằm ở *thời điểm biết được*, không ở *độ tiện khi viết code*. → **§3.6** |

## 0.1 Năm cặp trông như đá nhau

| Hai chỗ trông như đá nhau | Câu hỏi phân xử |
|---|---|
| **Hiệu năng là ưu tiên** (NT2) ↔ **chỉ tối ưu hot path đã xác nhận** (NT1) | **Bản nhanh hơn có khó đọc hơn không?** Không — đúng nhịp, không cấp phát trong vòng lặp, không tính lại thứ không đổi — thì **làm luôn**. Có — thêm tầng cache, viết tay vòng lặp, bẻ cấu trúc dữ liệu vì tốc độ — thì phải có **chỗ đo và số trước–sau**. |
| **Chi phí đọc, hạn chế composite** (NT1) ↔ **`S` và `I` của SOLID** (§3.1) | **Tách theo cái gì?** Theo **lý do thay đổi** là `S`, tách đúng. Theo "cho gọn mắt", "cho đúng pattern", "mỗi việc một component" là xé vụn: gộp lại. |
| **Mang đi được** (NT3) ↔ **không phòng xa** (NT1) | **Thứ phải thêm là gì?** Chỉ là **chiều phụ thuộc** và **chỗ đặt file** thì làm ngay. Là một interface, một adapter, một tham số cho người dùng **chưa có** thì là phòng xa — bỏ (§2.4). |
| **Editor-first** (NT4) ↔ **wiring trong asset không grep được** (§3.6) | **Số chỗ phải nối là hằng số nhỏ trong cùng một asset, hay tăng theo số instance?** Hằng số nhỏ → kéo thả: ô trống chắc chắn có người nhìn vào. Tăng theo instance → đăng ký: nó gom n ca hỏng-im-lặng thành **một** dòng đỏ lúc boot. |
| **§2.1 gộp câu hỏi thành 1–2 lượt** ↔ **§2.6 hỏi ở từng câu trả lời** | Hai loại câu hỏi, **không cộng dồn**: §2.1 hỏi để **lấy ngữ cảnh trước khi làm** — gộp lượt; §2.6 hỏi để **chốt một nguyên tắc vào file này** — hiếm. |

---

# §1 — Nguyên tắc còn lại

Nơi **duy nhất** định nghĩa NT5–NT10. Mỗi ô là *định nghĩa và phép kiểm*; dẫn giải ở mục § cuối ô.

| # | Nguyên tắc | Nội dung |
|---|---|---|
| 5 | **Hỏi đúng lúc, tự quyết đúng chỗ** | Thiếu ngữ cảnh thì **hỏi**, không đoán rồi làm. Buộc giả định thì ghi `Giả định (cần xác nhận): …` **tại chỗ dùng**. **Thao tác khó đảo ngược thì luôn hỏi trước khi chạy** — nhãn giả định không thay được xác nhận. Phạm vi hiện tại **chặn** hướng phát triển thật thì nêu ra **kèm giá**; mở rộng vì "cho đầy đủ" thì không (NT1). → **§2.1** |
| 6 | **Phạm vi bàn được, cách làm luôn mở** | Tiêu chí đã chốt thì cách đạt là việc của người triển khai: thấy cách **cùng tiêu chí** mà đơn giản, nhanh, hoặc rõ hơn thì **dùng nó**, kèm lý do và phép kiểm (NT8). **Ranh giới cứng** — tự do chỉ khi cả ba không đổi: **hành vi quan sát được** (kể cả kết quả ngẫu nhiên theo seed) · **dữ liệu ghi ra** (format lẫn giá trị) · **API công khai**. Đụng một trong ba là đổi phạm vi, phải hỏi (NT5). Im lặng chọn món trong Sổ tay khi biết có cách tốt hơn là vi phạm. |
| 7 | **Không lặp tri thức** | *Tài liệu:* một khái niệm giải thích một nơi, sau đó trỏ về. *Code:* **một chức năng cài đặt một nơi** — có sẵn thì dùng lại (§2.4); có người dùng thứ hai thì đề xuất nâng thành tái sử dụng (§3.2). Áp cho **tri thức trùng nhau**, không áp cho **code trông giống nhau**. Phép thử: *hai chỗ có cùng lý do thay đổi không?* — **cùng** → gộp về một nguồn · **khác** → **để lặp**, vì gộp là trói hai nghiệp vụ độc lập rồi hàm chung mọc tham số và nhánh riêng · **chưa chắc** → để lặp trước: lặp rồi gộp sau thì rẻ, trừu tượng hoá sai thì mọi caller phải đập · **mấp mé** → hỏi developer. Ngoại lệ không được "để lặp": **hai phép tính buộc khớp nhau ở runtime** — đo–vẽ, vẽ–hit-test, điều kiện ẩn–hiện — lệch là hỏng ngay, gộp là **bắt buộc** kể cả khi công thức hiển nhiên (§3.4). |
| 8 | **Bằng chứng, không khẳng định suông** | Mọi "tại sao" kèm phép kiểm **tái lập được**; công thức chốt phải **kiểm mốc**; code đối chiếu với công thức trước khi chốt. Không viết "đã đúng", "đã tối ưu" mà thiếu mốc, số đo, hoặc phép thử người đọc chạy lại được. **Bằng chứng phải cùng loại với tiêu chí** — và tiêu chí cao nhất của game là **cảm giác chơi**: công thức "sai sách" mà chơi đã tay thì **đúng**, chuẩn sách mà chơi vô hồn thì **sai**; toán là công cụ đạt cảm giác, không phải mục tiêu. Cảm giác chơi chỉ nghiệm thu bằng **chơi thử**, và chỉ developer chơi được. → **§2.8** |
| 9 | **Config chưa chốt thì không phải mốc · chuẩn hoá thì bỏ điểm neo** | Số mặc định trong asset, bảng cấu hình, cấu hình từ xa là **bản nháp của developer**, chỉ thành mốc khi developer nói đã chốt. Đổi đơn vị, trục, công thức: **không tự ánh xạ giá trị cũ, không tự dựng đối chiếu trước–sau** để chứng minh "cảm giác không đổi" — đó là đóng băng nháp thành chuẩn (NT1). Việc phải làm: giữ **hành vi thuật toán** bất biến, chứng minh bằng mốc (NT8), rồi **hỏi** giá trị cũ là nháp hay mốc (NT5). *Khi một số phải so được giữa các ngữ cảnh khác cỡ:* chuẩn hoá là **bỏ** điểm neo, không phải chọn neo tốt hơn — còn hỏi "chia cho cái nào" là chưa xong. **Mẫu số là đại lượng gốc**, không phải đại lượng đã bị một núm khác nhân vào — chia cho bản đã nhân biến núm đó thành hệ số âm thầm và giết dòng chẩn đoán nói *ai* đang kẹp. |
| 10 | **Mật độ, trình tự, và chỉ một hệ** | Chọn dạng có mật độ cao nhất **cho loại nội dung đó**: bảng cho so sánh, diagram cho luồng, công thức cho quan hệ định lượng, một câu cho trực giác. Dẫn theo mạch **dễ→khó, tổng quan→chi tiết, vấn đề→giải pháp**; mỗi bước chỉ dùng khái niệm đã nêu; đánh số khi là quy trình. **Mặc định một khẳng định = một câu lý do**; phải viết cả đoạn là dấu hiệu chưa hiểu đủ để nén, hoặc đang biện minh cho thứ không cần có. **Đổi rồi thì chỉ còn một hệ:** quyết định đã chốt thì code và tài liệu nói **hoàn toàn bằng hệ mới** — không "trước đây là…", không hai đơn vị song song, không giữ tên cũ làm cầu; người đọc sau **không có hệ cũ trong đầu**. Giữ vết hệ cũ chỉ khi **gọi tên được người dùng thật của vết đó**: payload cũ phải deserialize được (§3.7) · developer đọc log build cũ · developer yêu cầu. Lịch sử ghi ở commit và changelog. Áp cho **tài liệu đầu ra**; đối thoại có luật riêng ở §2.3. |

---

# §2 — Quy trình làm việc với AI

## 2.1 Trước khi làm — phỏng vấn ngữ cảnh

Agent **không** suy đoán ngữ cảnh rồi bắt tay làm: đoán **thừa** là hàm không caller nào gọi, đoán
**thiếu** là chữ ký chặn hướng dùng thật (NT1). Hỏi 5 nhóm dưới, **gộp thành 1–2 lượt**; biết chắc nhóm
nào thì **nêu giả định để developer xác nhận**. **Chưa có câu trả lời thì chưa làm.**

| Nhóm | Với tài liệu | Với code hoặc plan |
|---|---|---|
| **Ai dùng đầu ra** | ai đọc, đọc để làm gì, biết sẵn tới đâu | ai gọi, gọi ở đâu, có caller thật **ngay bây giờ** chưa |
| **Mục tiêu** | đọc xong phải **làm được gì** | phải đạt **cảm giác hoặc hành vi** gì, nghiệm thu bằng gì (§2.8) |
| **Ngân sách** | độ sâu và độ dài nào là đủ | **nhịp** của nó là gì (NT2), có phải hot path không, platform nào |
| **Ranh giới** | phần nào giải ở đây, phần nào trỏ sang tài liệu khác | phần nào của class này, phần nào của hệ khác; hệ này **độc lập hay kết hợp** (NT3) |
| **Hướng phát triển thật** | hệ sắp đổi gì khiến tài liệu phải sửa | **chắc chắn** sắp cần thêm gì; cái gì *có thể* cần nhưng chưa chắc (NT1) |

**Giả định chỉ vá khe hở nhỏ phát hiện giữa chừng, không thay cho phỏng vấn** — đủ cả ba: khe hở
**nhỏ** · đầu ra **đảo ngược được** · nhãn ghi **tại chỗ dùng** (NT5). Khó đảo ngược là: ghi đè hoặc
xoá dữ liệu đã author (file level, save, asset) · migration đổi schema hoặc wire format · thứ nằm ngoài
version control.

**Gợi ý một hướng developer chưa nghĩ tới thì cứ nêu — đó là việc được mong đợi.** Đúng khi dừng ở
**một dòng kèm giá** để developer chọn; sai khi tự đưa vào output vì "tiện thể" (NT1). Trần của gợi ý
là **phạm vi đang bàn** — kéo sang hệ khác thì để một dòng ở "Mở rộng sau" (§2.5). Thứ developer tự nêu
thì hỏi lại **một lần** để cân đắt–lợi, rồi theo developer.

## 2.2 Đọc code rồi phải đối chiếu lại với developer

**Code cho biết cái gì đang chạy, không cho biết vì sao nó được viết như vậy.** **Không mặc định
developer nắm rõ từng ngóc ngách hệ thống của mình**: code có thể do người khác viết, viết từ lâu,
hoặc đã trôi khỏi thiết kế ban đầu. Với **mỗi phát hiện ảnh hưởng đến quyết định đang bàn**, nêu đủ:
**(1) tôi thấy gì** — kèm đường dẫn và dòng để developer mở ra kiểm · **(2) tôi hiểu ý định là gì** —
phát biểu lại bằng lời mình rồi hỏi thẳng có khớp thiết kế ban đầu không · **(3) developer đã biết chỗ
này chưa** — chưa thì là chủ ý hay chỗ đã trôi cần xử lý. Kể lại toàn bộ code vừa đọc là bắt developer
đọc tường thuật thay vì trả lời một câu hỏi (NT10).

## 2.3 Văn phong khi đối thoại

Áp cho mọi lượt đối thoại, **chặt nhất khi brainstorm** (§2.6). Hai bờ đều là bờ vực (NT1):

- **Sàn — đủ dữ kiện để developer tự phân tích lại, không phải chỉ đủ để tin.** Nêu *cái đang có*,
  *cái sẽ đổi*, *cái đánh đổi*, và số hoặc đường dẫn để kiểm. Đây là chỗ **mật độ của NT10 không áp**:
  nén trong đối thoại chỉ tạo thêm một vòng hỏi lại. Sàn là dữ kiện, không phải dẫn giải — bước hiển
  nhiên đúng, hoặc thứ đã chốt ở lượt trước, thì một hai câu nhắc lại là đủ.
- **Trần — đúng phần cần cho quyết định đang chờ, không hơn.** Trần phải tự canh vì thiếu thì developer
  hỏi lại và lộ ra ngay, thừa thì chỉ lộ dưới dạng quyết định ra chậm hơn. Phép kiểm: *dòng này thay
  đổi điều gì trong quyết định developer đang phải ra?* Không đổi gì thì cắt, kể cả khi nó đúng.

Cách viết: một câu nói một ý · **ngắn và dễ hiểu đi trước hoa mỹ** — bỏ câu chuyển tiếp, lời dẫn, tóm
tắt lại · **hạn chế viết tắt và thuật ngữ khó**, buộc dùng thì giải nghĩa ngắn ngay lần đầu · **gọi
khái niệm đúng tên nó có trong hệ**, khớp nguyên văn tên trong code và tài liệu (§3.7) · câu hỏi phải
trả lời được **mà không cần mở code ra đọc lại**, thiếu ngữ cảnh gì thì cung cấp kèm · **kết luận
trước, dẫn giải sau** để developer đủ tin thì dừng đọc được — đối thoại **cố ý** đi khác trình tự của
NT10 vì nó không có người đọc tuần tự.

**Bày phương án, rồi chốt một cái.** Mỗi phương án một dòng: *nó là gì · được gì · mất gì*. Rồi **chốt
một phương án và nói vì sao nó thắng** — tiêu chí là **hợp tư tưởng trong file này nhất** (§0), không
phải "dễ làm nhất" hay "nhiều tính năng nhất". Bỏ phần chốt là đẩy việc khó nhất về developer; bỏ phần
bày phương án là lấy mất dữ kiện để developer bác lại. **Ranh giới:** quyết định thuộc developer thì
**thu hẹp lựa chọn và nêu giá từng cái, không chốt hộ** — phạm vi, ba thứ ở ranh giới cứng của NT6,
thứ developer đã quyết rồi (§5.4), và mọi ca NT5 nói là phải hỏi.

## 2.4 Tái sử dụng, rồi chốt phạm vi

**Trước câu hỏi "có cần không" là câu hỏi "đã có chưa"** (NT7). Khảo sát **trong phạm vi logic liên
quan đến task** — hệ đang chạm, module nó gọi tới, Utilities khi nghi có helper sẵn. Nói rõ đã khảo
sát đâu và kết luận gì (NT8).

| Cái có sẵn | Xử lý |
|---|---|
| Đáp ứng được yêu cầu | dùng lại, không viết bản thứ hai |
| Gần đúng nhưng thiếu | mở rộng nó nếu **thêm được mà không sửa cái cũ**; không được thì viết mới |
| Không có, hoặc phải **bẻ cong bài toán** cho vừa nó | **viết mới** — tái sử dụng không phải lý do để làm sai bài toán |

**Bê một khuôn có sẵn thì soi kỹ nhất đúng những chỗ khuôn cũ *cố ý* làm** — cân nhắc kỹ nghĩa là nó
**bám chặt vào ngữ cảnh cũ**. Với mỗi quyết định cố ý, hỏi lý do gốc có còn đúng ở bài toán này không;
hết thì đảo và ghi lý do ngay tại đó. *Đã sai một lần:* bê một khuôn cố ý serialize giá trị runtime
vào asset sang bài toán lưu dữ liệu người chơi — tiến độ người chơi đi vào asset rồi vào version control.

Sau đó mới tới phạm vi. Mọi thứ định đưa vào qua cùng một luật: **có nhu cầu thật ngay bây giờ thì đưa
vào** (NT1).

| Thứ định thêm | Nhu cầu thật là |
|---|---|
| Interface hoặc abstract | có **implementation thứ hai** đang có thật |
| Tham số | có **call site truyền khác mặc định** |
| Guard, nhánh biên, `try/catch` | theo **một câu hỏi duy nhất** ở §3.4: *cái sai đó lộ ra lúc nào?* |
| Code canh — hoặc tự tạo — thứ dựng được lúc authoring | **không có nhu cầu nào cả**: đó là bước setup, viết vào tài liệu (§3.6) |
| Tối ưu làm code khó đọc hơn | **hot path đã xác nhận** bằng số đo (NT2) |
| Tách lớp, tách class, tách component | **trách nhiệm thật sự khác** — khác lý do thay đổi (NT1) |
| Chương, mục, demo | có người đọc cần nó để **làm được một việc cụ thể** |

Thứ chỉ "có thể cần sau" chia theo **giá của việc thêm sau**: **rẻ** (thêm mục hoặc hàm mới, không sửa
cái cũ) thì **để lại**, ghi một dòng ở "Mở rộng sau" (§2.5) · **đắt** (sửa chữ ký, đập cấu trúc, đảo
chiều phụ thuộc) thì làm ngay — chỗ **duy nhất** đáng phòng xa (NT1). **Tính mở rộng đến từ
Open/Closed**, không từ việc viết sẵn thứ chưa ai cần.

## 2.5 Ghi ngữ cảnh đã chốt vào đầu output

Plan thì đặt mục **"Ngữ cảnh đã chốt"** trước `§0`; tài liệu thì nêu ở phần mở đầu. Gồm: người dùng ·
mục tiêu · ranh giới · **những gì cố ý KHÔNG làm, kèm lý do** · hướng phát triển đã tính tới nhưng
chưa làm (NT1). Người đọc sau biết vì sao phạm vi dừng ở đó, không "bổ sung cho đủ".

## 2.6 Chưng cất tư tưởng khi brainstorm

Brainstorm là nơi tư tưởng của developer lộ ra rõ nhất — dưới dạng **quyết định cho một bài toán cụ
thể**, và trôi mất khi bài toán xong. **Ở từng câu hỏi**, sau khi nhận câu trả lời:

1. **Khái quát hóa** — tách *tư tưởng* khỏi *quyết định riêng của bài toán*: phát biểu lại thành
   nguyên tắc mang sang bài toán khác vẫn dùng được, kèm "vì sao".
2. **Đối chiếu với chính file này** (NT7) — đã có thì thôi; là trường hợp riêng thì trỏ về; **mâu
   thuẫn** với nguyên tắc đã có thì nêu thẳng chỗ lệch để developer phân xử.
3. **Hỏi developer quyết** (NT5) — có thêm vào MY_SKILL không, vào **tầng nào**? Developer chốt thì
   mới ghi, đúng cấu trúc và văn phong của file; từ chối thì bỏ, không ghi tạm đâu khác.

Chỉ khái quát khi câu trả lời **thật sự chứa tư tưởng** — một lựa chọn có "vì sao" lặp lại được. Quyết
định thuần bài toán (hằng số, tên, phạm vi một task) thì không.

## 2.7 Subagent — ngữ cảnh bơm từ orchestrator, không tự đọc lại

Mỗi subagent là một ngữ cảnh trắng: để nó "tự tìm hiểu" là nó đọc lại từ đầu MY_SKILL và tài liệu hệ,
thuế đọc đó **nhân theo số subagent**. *Đã sai một lần:* hàng trăm subagent tự đọc lại tài liệu nền
trong một ngày đốt token gấp hơn chục lần nhịp thường.

- Prompt cho subagent **tự chứa như một task của Plan** (§5.3): trích đoạn tài liệu cần cho task,
  đường dẫn file sẽ chạm, tiêu chí nghiệm thu. Thiếu ngữ cảnh thì subagent báo về, không tự đi đọc.
- Ngoại lệ là **code**: subagent tự đọc code nó sẽ sửa — trích đoạn code trong prompt là bản chết
  (§5.4 "không viết theo trí nhớ" áp cho cả prompt).

## 2.8 Nghiệm thu — chọn phép kiểm theo loại tiêu chí

NT8 đòi bằng chứng; mục này nói bằng chứng **nào** hợp tiêu chí nào. Chọn sai thì lãng phí cả hai
phía: dựng lệnh cho thứ chỉ chơi thử mới biết, hoặc đẩy về tay developer thứ máy quét vài giây là xong.

| Tiêu chí cần chứng minh | Bằng chứng đúng loại | Ai chạy |
|---|---|---|
| Thuật toán tất định, công thức, parser, serialize | phép kiểm chạy được (§4.3) | agent |
| Tên, tham chiếu, đồng bộ tài liệu | grep quét (§3.7, §5) | agent |
| Hiệu năng | số trước–sau tại chỗ đo (§3.3) | agent |
| Đúng–sai xác định được, mà người làm tay thì chậm, sót, hoặc không thấy được | vét cạn theo bảng dưới | agent, **tự đề xuất** |
| Cảm giác chơi, nhịp, độ khó, hình ảnh | **chơi thử** | **developer** |

**Cảm giác chơi — DỪNG và giao, không dựng proxy.** Ép nó về một lệnh chạy được là **đo thứ dễ đo
thay cho thứ cần biết**. Báo thẳng *"phần này chưa nghiệm thu được, cần chơi thử"*, kèm **kịch bản
chơi thử**: *vào đâu* (level nào, cần bật cờ hoặc dữ liệu gì) · *làm gì* (chuỗi thao tác **ngắn nhất**
tái lập được) · *nhìn cái gì* (hiện tượng cụ thể, không phải "xem có ổn không") · *khác trước ra sao* ·
*dấu hiệu hỏng*.

**Đúng–sai xác định được — vét cạn, không đẩy về tay.** Điều kiện là **cả hai**: có đáp án đúng–sai
xác định được, **và** ít nhất một dấu hiệu trong bảng. Đủ thì agent làm và chạy, kể cả khi chưa được
yêu cầu.

| Máy hơn người ở | Dấu hiệu nhận ra | Người làm tay hỏng ở đâu |
|---|---|---|
| **Sức** | không gian đầu vào lớn · phải lặp lại nhiều lần | chậm, và sót vì mỏi |
| **Thiên kiến** | trường hợp biên khó nghĩ ra hết | chỉ thử được thứ mình nghĩ ra, mà chỗ hỏng nằm đúng ở chỗ không ai nghĩ tới |
| **Tầm nhìn** | phải chứng minh **sự vắng mặt**: không còn tham chiếu, caller, tên cũ · trạng thái nội bộ sai trong khi màn hình vẫn đúng (§3.4) · thứ chỉ lộ sau hàng nghìn vòng: rò rỉ, phình dần, pool không trả về | mắt không thấy được thứ *không có* |
| **Nhất quán chéo** | nhiều bản buộc phải khớp nhau mà không suy từ một nguồn được (§3.4) · hành vi trước–sau một lần refactor phải trùng (NT6) | phải mở nhiều nguồn cạnh nhau so từng dòng — sót nhiều nhất |

Riêng nhánh **thiên kiến**, phần đắt giá là **liệt kê biên có hệ thống trước khi chạy**: rỗng · đúng
một phần tử · chạm giới hạn trên và dưới · trùng nhau · ngoài dải · thứ tự đảo · hai sự kiện cùng lúc ·
frame đầu tiên · đối tượng bị huỷ giữa chừng. Không dấu hiệu nào thì đọc code là xong (NT1). Cái neo
khi phân vân: **công sức đắt nhất trong nghiệm thu là của developer**.

---

# §3 — Thiết kế code

**Bốn ưu tiên** §3.1 hình dạng · §3.2 module · §3.3 hiệu năng · §3.6 editor-first — **vận hành** §3.4
bất biến · §3.5 async — **luật ngang mọi code** §3.7 naming · §3.8 import/export — **tool** §3.9.

## 3.1 Hình dạng — bậc cấu trúc, SOLID, và giới hạn của composite

| | Nội dung |
|---|---|
| **S** | Một class là một responsibility. Tách khi có nhiều hơn một **lý do thay đổi** — không tách theo "cho gọn mắt" (NT1, §0.1). |
| **O** | Extend, don't modify. Không sửa trực tiếp class đang chạy ổn định — mở rộng bằng cơ chế phù hợp với bài toán. |
| **L** | Subtype thay thế được base mà không break behavior, không side-effect lạ. |
| **I** | Interface nhỏ, tách theo consumer. Không ép client phụ thuộc method nó không dùng. |
| **D** | **Consumer không tự `new` thứ nó phụ thuộc** — nó nhận vào (factory, pool, container thì đương nhiên phải `new`). `D` **không đòi interface**: **trong một hệ**, nhận vào class cụ thể vẫn là "nhận vào". **Qua ranh giới hệ** thì theo §3.2. |

> **Nền tảng** — DI runtime mặc định là InitArgs (`Sisus.Init`): `[Service(typeof(T))]` để đăng ký,
> `MonoBehaviour<TDep>` + `Init(TDep)` để nhận. **Mỗi class chỉ có MỘT khe `Init` mà framework tự
> gọi** — base generic của thư viện đã tiêu khe đó thì `IInitializable<…>` khai thêm ở class con
> **không ai gọi** (đường tự tiêm thoát sớm, dòng log báo việc đó nằm trong `#if DEV_MODE`). Ca này
> **bắt buộc** một `*Initializer` kéo tay vào scene; hai đường tiêm độc lập, không phá nhau. Editor và
> tooling không bắt buộc dùng InitArgs — constructor injection hoặc static factory ở đó là hợp lệ.

**Đổi từ resolve lười sang resolve sớm là dịch cửa sổ thất bại, không phải bỏ nó.** Gọi lúc dùng thì
hỏng lúc dùng; nhận qua `Init` thì hỏng lúc khởi tạo. Đổi chiều nào cũng phải hỏi lại: **lúc đó thứ
mình cần đã tồn tại chưa?** — thứ tự `Awake` giữa các object không định trước, và service tìm-từ-scene
chỉ có sau khi scene chứa nó nạp xong.

**Bậc cấu trúc leo từ dưới lên, mỗi bậc chỉ leo khi bậc dưới không còn đạt** (NT1). SOLID phục vụ
**người đọc sau**: chia thiếu và chia thừa đều sai ở cùng một chỗ là **chi phí đọc**.

| Bậc | Đủ dùng khi | Leo lên bậc trên khi |
|---|---|---|
| **Logic tại chỗ** | chỉ chạy ở một nơi, đọc một mạch là hiểu hết | có **người gọi thứ hai**, hoặc một ý không còn nhìn hết trong một màn hình |
| **Hàm tách riêng** | đặt được tên nói đúng mục đích (§3.7); không giữ state giữa các lần gọi | phát sinh **state phải giữ**, hoặc một cụm hàm cùng thao tác trên một nhóm dữ liệu |
| **Class hoặc struct** | có **trách nhiệm gọi được tên** và state của riêng nó (`S`); `struct` hay `class` theo §3.3 | có **implementation thứ hai** đang có thật (§2.4) |
| **Delegate làm tham số** | thân thuật toán **giống hệt nhau** ở mọi biến thể, chỉ khác **một thao tác** gọi được tên, biến thể **không giữ state riêng**. Khai `static readonly Func<…>` với lambda `static`: bắt biến ngoài thành **lỗi biên dịch** thay vì rác GC âm thầm (NT2) | biến thể cần **state riêng**, hoặc **hơn một thao tác** đi cùng nhau — lúc đó nó đã là interface |
| **Interface hoặc abstract** | implementation thứ hai **đang có thật**, không phải sắp có | — |

**`MonoBehaviour` · `ScriptableObject` · class thuần không phải ba bậc của thang trên.** Thang trả lời
*chia tới đâu*; ba thứ này trả lời *đóng gói ở đâu* — hỏi **sau**, khi đã biết là cần một class. Gộp
hai câu hỏi làm một là cách một dự án trượt tới chỗ mọi thứ đều là asset.

| Nó làm gì | Là gì | Vì |
|---|---|---|
| trả lời câu hỏi từ **tham số truyền vào**, không giữ gì | **class thuần static** | test không phải dựng gì; không thêm một asset có thể quên kéo |
| trả lời câu hỏi từ **dữ liệu của chính nó**, dữ liệu do người dựng đặt | **`ScriptableObject`** | dữ liệu là cấu hình, và asset thì kéo được từ mọi nơi (§3.6) |
| **làm gì đó theo thời gian** — loop, chờ, subscribe, ghi ra ngoài | **`MonoBehaviour`**, hoặc class thuần do một `MonoBehaviour` sở hữu | cần một **mốc chết tin được** để tắt |

Phép kiểm của hàng cuối là **"có cần tắt được không"**, không phải "có cần vòng đời không": câu sau khó
trả lời, câu trước trả lời được ngay.

> **Nền tảng** — `ScriptableObject` dừng ở **dữ liệu**: cấu hình, bảng số, danh mục, preset. Không làm
> kênh sự kiện, không làm biến dùng chung, không giữ state đổi lúc chạy, **không tự mở
> `CancellationTokenSource`**. Ba lý do: field serialize của asset bị đổi lúc Play **không quay lại**
> khi dừng Play — ra "máy tôi chạy được" mà không một dòng log · asset chỉ nạp lúc có người chạm vào
> lần đầu nên **không có thứ tự khởi tạo** để dựa vào · nó không có mốc kết thúc nào để đóng token.

**`O` không phải lý do để leo bậc.** `O` cấm **đổi hành vi đường cũ**, không cấm **thêm đường mới vào
chính class đó**: thêm method hoặc tham số mới mà đường cũ không đụng là đã đạt `O`. Phải đổi hành vi
đường cũ nghĩa là **trách nhiệm mới** — tách class; chỉ khi có implementation thứ hai đang có thật mới
dựng interface.

**Ba hình dạng composite — viết lý do ra tại chỗ trước khi dùng.** Không cấm, nhưng mặc định là không,
và mỗi lần dùng phải gọi tên được thứ bậc thấp hơn không làm được (NT1). Thước đo là **phạm vi bài
toán**: cùng một cách chia có thể đúng ở hệ nhiều người chạm và thừa ở một tính năng cục bộ.

| Hình dạng | Giá phải trả | Chỉ dùng khi |
|---|---|---|
| **Cây composite** — cha và lá cùng interface, duyệt đệ quy | hành vi không nằm ở đâu cả, phải chạy mới biết; mỗi nút một dispatch ảo và một lần con trỏ nhảy, trong nhịp mỗi frame là trả giá theo số nút (NT2) | **cấu trúc lồng nhau là của dữ liệu thật** — độ sâu do người dùng hoặc dữ liệu tạo ra, không do người viết chọn cho đẹp |
| **Một tính năng xé thành nhiều MonoBehaviour** | thứ tự `Awake`/`Update` giữa các mảnh **không định trước**, wire thiếu chỉ lộ lúc chạy, mỗi mảnh là một lần engine gọi message qua ranh giới native | các mảnh **thật sự lắp lẫn được** giữa nhiều prefab, và **tổ hợp đó đang tồn tại thật** — không phải "mỗi việc một component cho sạch" |
| **Hệ dựng chồng lên nhiều hệ khác** (hệ kết hợp) | không rời đi một mình được (NT3) | không tách nổi thành các hệ độc lập cộng một lớp nối mỏng; khi đó vẫn phải chỉ ra đường tách (§3.2) |

**Bậc delegate giữ bất biến của vòng lặp ở đúng một bản.** Khi n biến thể dùng chung một vòng lặp, thứ
chỉ được có **một bản** là bất biến của vòng lặp — thứ tự chạy, phát tiến độ, xử lý lỗi, điểm thoát khi
bị huỷ. Nhân vòng lặp ra n bản, hoặc cắm `if` biến thể vào giữa, là nhân bất biến ra n bản (NT7).

**Nhiều host chung một nhịp: thân về nơi giữ bảo đảm, đời sống ở lại host.** Thân vòng lặp và con số
cấu hình về nơi giữ bảo đảm mà nhịp đó phục vụ — con số nằm cạnh dữ liệu nó chi phối thì đọc một chỗ ra
hết. **Token** ở lại host vì host là thứ **có đời sống**. **Mỗi loop phải chỉ ra được token của nó là
đời của ai, và đời đó dài đúng bằng nhịp** — nhận token của pha ngắn hơn đời nó thì loop **chết im
lặng**: hủy là hành vi hợp đồng, thư viện async không log, phần còn lại vẫn chạy như thường. Loop không
có chủ thì ở Editor tắt domain reload, mỗi lần Play là một loop nữa xếp lên loop cũ. Editor không bắt
được ca này, phải đọc code.

> **Nền tảng** — thứ tự region trong một class là **cố định**: `Properties` trên cùng ·
> `Unity Callbacks` · `API` (thứ người ngoài gọi) · `Class Methods` (thân private) · `DI` **cuối
> class**, gói field nhận vào cùng `Init`. Trình tự **trạng thái → vòng đời → mặt ngoài → thân**: mở
> file ra là thấy class giữ gì trước khi thấy nó làm gì; phụ thuộc xuống cuối vì đó là thứ hỏi sau cùng.

**Sổ tay** — hình dạng file và class đang dùng trong bộ này:

| Chỗ | Cách làm |
|---|---|
| Code chỉ có ở Editor | file `*.Editor.cs` khai `partial` của cùng class, **bên trong vẫn** `#if UNITY_EDITOR` — file runtime không bị `#if` cắt ngang, nút Editor đọc thẳng private member |
| Nhận phụ thuộc | `MonoBehaviour<T>` cho component thường · `IInitializable<T>` khi class đã kế thừa base khác · không service locator bên trong hệ |
| Dựng host | hệ **không** tự `new GameObject` — host là component kéo tay vào scene, chu kỳ và collection đọc được trong Inspector (§3.6) |
| Log | mọi dòng mở bằng `[TênHệ]: ` để filter console; kèm `this` làm context object để bấm vào ra đúng asset |
| Hàm một biểu thức | expression-bodied (`=>`), kể cả method `void` |
| Ẩn method của contract | mặc định `public` — tin người dùng hệ. Explicit interface implementation **chỉ cho method mà gọi sai gây mất dữ liệu im lặng** |

## 3.2 Module — độc lập trước, kết hợp là ngoại lệ

Ranh giới hệ thống phải **nhìn thấy được** trong cấu trúc dự án; mỗi hệ có một trách nhiệm gọi được
tên. **Module không tham chiếu trực tiếp implementation của nhau** — cơ chế trung gian chọn theo bài
toán (interface, event bus, ScriptableObject channel, dữ liệu thuần), miễn đạt: đổi implementation một
bên mà bên kia không phải sửa. Bên trong một hệ thì không cần tầng này (`D` ở §3.1).

**Phân tầng theo mức phụ thuộc, quyết ngay từ đầu** (NT3): hệ **độc lập** (bê sang dự án khác được) ·
hệ **kết hợp** (dựng trên nhiều hệ độc lập) · **Utilities** static và universal, không phụ thuộc hệ nào.

**Ranh giới framework dùng chung ↔ dự án đi đúng một chiều: dự án → framework.** Framework không gọi
tên type nào của dự án — và **cơ chế mở rộng nó cung cấp cũng không được ép dự án đẩy domain sang phía
framework**. Phép kiểm: *nửa mà dự án viết có nằm trong ranh giới biên dịch của dự án không?* Không
nằm thì mọi type nửa đó gọi tên cũng bị kéo theo cả chuỗi — framework mất tính bê-sang-dự-án-khác dù
thư mục file vẫn đúng chỗ.

> **Nền tảng** — với C# và Unity:
>
> | Cơ chế mở rộng | Kết quả |
> |---|---|
> | `partial class` khai ở framework, nửa kia ở dự án | **Hỏng.** Mọi phần của `partial` phải cùng assembly, nên nửa của dự án phải vào assembly framework bằng `.asmref` — và type nó gọi tên phải tra được từ đó. Type của dự án thì không, vì chiều ngược là **circular reference**. Kết cục: domain nằm ở assembly framework |
> | `abstract class` ở framework, dự án khai subclass | **Đúng.** Subclass là type của dự án, chỉ gọi tên xuống framework. Phần framework tự chạy được một mình biến mất — đó là đặc điểm: tự chạy được nghĩa là đang mang một mẩu domain |
>
> Ba bẫy hỏng-im-lặng đi kèm kế thừa:
>
> | Thứ | Bẫy |
> |---|---|
> | Attribute khai `Inherited = false` — `[Service]` của InitArgs là một | khai ở base **không** tới subclass; quên khai lại là không đăng ký được gì |
> | `GetType().GetFields(NonPublic \| Instance)` | thấy private field của subclass, **không** thấy private field khai trên base — field khai nhầm ở base bị bỏ qua không một dòng lỗi |
> | Magic method của Unity (`OnDestroy`, `Awake`…) | subclass đặt trùng tên là che hẳn bản của base, dọn dẹp của base im lặng không chạy. Khai `protected virtual` để `override` |

**Chức năng có người dùng thứ hai thì đề xuất nâng thành tái sử dụng được** (NT7): hàm thuần → Utilities
hoặc Helper static · có state hoặc nhiều biến thể → interface rồi tách implementation · chỉ khác một
giá trị → thêm tham số. Đặt ở **tầng thấp nhất mà cả hai người dùng đều với tới được**, không thấp hơn
(NT1). Là **đề xuất, không tự làm**: nâng một chức năng là đổi ranh giới trách nhiệm (NT5).

## 3.3 Hiệu năng runtime

Luật ở NT2; mục này là cách đạt. **Nghiệm thu:** chỉ ra được **chỗ đo** và **số trước–sau** (NT8).

Trước khi cache hay tối ưu, hỏi ba câu theo thứ tự — "có" ở câu nào thì dừng ở đó: có thể **không cần
tính** nó không · tính **một lần lúc authoring** được không (§3.6) · đổi **cấu trúc dữ liệu** để câu
hỏi tự biến mất được không? Hết ba câu mới tới kỹ thuật.

**Sổ tay** — kỹ thuật đã dùng:

- *Giảm cấp phát:* pool thay `Instantiate`/`Destroy` lặp lại · pre-alloc capacity · reuse buffer bằng
  `.Clear()` · grow-only buffer khi size dao động · `struct` cho data nhỏ ngắn hạn · `ref` / `in` /
  `Span<T>` thay copy · `static readonly` thay `new` lặp · tránh LINQ, boxing, string concat trong hot
  path · **không closure capture** — lambda bắt biến ngoài cấp phát mỗi lần gọi; cache delegate thành
  `static readonly` hoặc field.
- *Giảm tính toán:* dirty flag · event-driven rebuild · lookup dictionary dựng trước · tách phần tĩnh
  tính một lần khỏi phần động tính incremental · precompute hằng nặng (`exp`, `sqrt`, `sincos`, phép
  chia) ngoài vòng lặp · đổi chia thành nhân · guard thoát sớm · `sqrMagnitude` khi chỉ so khoảng cách.
- *Sửa list an toàn:* duyệt ngược khi xoá · hoặc deferred removal — đánh dấu rồi xử lý sau vòng lặp.

> **Nền tảng** — `Update`, polling loop và `OnGUI` bị gọi lại liên tục cho **cùng một state**. Việc
> nặng đặt trong đó là sai không cần bàn; nó thuộc về event handler hoặc lúc authoring.

## 3.4 Bất biến — bảo vệ bằng cấu trúc, không bằng kỷ luật

Bất biến giữ bằng "mọi người nhớ làm đúng" sẽ vỡ ở đúng người thứ hai. Sắp xếp code sao cho cái sai
**không thể xảy ra**:

| Luật | Nghĩa là |
|---|---|
| **Một sự thật = một chủ sở hữu** | mỗi dữ liệu có đúng một nơi giữ bản gốc; mọi cache chỉ ra được **ai dựng lại** và **khi nào** |
| **Cache mới bám vào bất biến ĐÃ CÓ** | dùng lại dirty flag / version counter đang có, không dựng bất biến thứ hai song song — mỗi bất biến thêm là một điều mọi code sau phải nhớ |
| **Một cờ chỉ được tiêu thụ ở đúng MỘT nơi** | có nơi thứ hai thì nơi chạy sau không bao giờ thấy cờ bật — cache của nó đứng im, không có gì báo |
| **Hai phép tính buộc phải khớp thì suy từ MỘT nguồn** | cùng một hàm, hoặc cùng một biểu thức copy nguyên. Hai bản sẽ lệch, kiểu nhìn-vẫn-đúng-bấm-thì-trượt. Đây là ngoại lệ của luật "để lặp" (NT7) |
| **Cửa hẹp là thân chung của cửa rộng** | bản giữ-lại-một-phần gọi vào thân bản đầy đủ — hai bên không thể lệch nhau |
| **Một bảo đảm phải phủ MỌI đường vào** | hệ tuyên bố "mất không quá X", "luôn hợp lệ" thì **mọi** cửa ghi phải đi qua chỗ tạo ra bảo đảm đó. Cửa thứ hai đi vòng làm bảo đảm **chỉ còn đúng cho một nửa hệ** trong khi tài liệu vẫn phát biểu nguyên câu |

**Phép kiểm của luật cuối — đếm cửa trước, đọc thân sau.** Đường phụ sinh ra sau, người viết không
nghĩ mình đang chạm vào bảo đảm nào. Liệt kê **mọi cửa ghi vào cùng một kho** (grep API ghi của kho),
rồi với từng cửa: chỉ ra nó đi qua chỗ tạo bảo đảm, hoặc **viết tại chỗ rằng cửa này không được bảo
đảm**. Ép mọi cửa về **một** thân là cách rẻ nhất giữ phép kiểm còn đúng về sau (nhánh **tầm nhìn**,
§2.8).

**Quyết định mà lý do là "phải nhớ đừng…" thì chỗ đặt sai, không phải tên sai.** Hỏi: **có chỗ đặt nào
làm việc "đừng" đó bất khả thi không?** Thường có, và thường rẻ. *Đã sai một lần:* một helper tên
`InstantiateAsync` khai trong class con của `MonoBehaviour` che **toàn bộ** overload cùng tên của
`UnityEngine.Object` — chỗ gọi nhìn như API engine nhưng chạy hàm nhà. Đổi tên helper thì ràng buộc vẫn
còn, chỉ đang được tuân thủ; đưa nó ra **extension method** thì ràng buộc biến mất, vì extension không
bao giờ che được instance method.

**Một hệ quả bắt buộc thì đặt trong setter, đừng đặt trong method riêng.** `SetX(v)` cạnh một field `x`
là **hai cửa ghi**; property có setter là một. Chỉ áp khi hệ quả **luôn phải xảy ra**, rẻ, và không ném
— hệ quả tuỳ chọn hay tốn kém thì giữ method, vì sau dấu `=` người đọc không chờ đợi một cái giá. **Đổi
method thành property là đổi hợp đồng ở hai nơi:** interface phải khai `{ get; set; }` nếu không người
ngoài cầm interface không ghi được — lỗi chỉ lộ khi viết tới consumer; và `internal void SetX` thành
`public X { get; set; }` là nới quyền ghi ra cả assembly khác mà không ai để ý.

**Chọn cấu trúc theo bảo đảm mà người dùng nó đang dựa vào, không theo thao tác thuận tay.** Gộp theo
khoá thì `Dictionary` đúng khi kết quả để **tra cứu**, và sai khi kết quả là **danh sách để vẽ**: thứ
tự duyệt `Dictionary` không có bảo đảm nào, UI sẽ đảo hàng giữa các lần chạy và không ai gọi đó là bug.
Gộp vào `List` theo thứ tự gặp đầu tiên thì thứ tự là một bảo đảm viết ra được và test được.

**Một danh sách vừa là lệnh vừa là thứ để vẽ thì hỏi hai vai có chung khoá gộp không.** Vai *thực thi*
gộp theo khoá của hệ nhận lệnh, vai *hiển thị* gộp theo khoá người dùng nhìn thấy — hai khoá đó thường
ánh xạ nhiều-về-một, nên gộp theo khoá của vai này là vai kia mất hàng. **Và tổng hợp đặt ở chỗ nhìn
thấy đủ dữ liệu:** kho cộng dồn qua nhiều chu kỳ thì gộp lúc *ghi vào* không thấy chu kỳ trước, phải
gộp lúc *đọc ra*.

**Guard — một câu hỏi duy nhất: cái sai đó lộ ra lúc nào?** Đích là **bản build không có ca null hay
ca sai nào**; guard hay không guard chỉ là hai đường tới cùng đích.

| Cái sai lộ ra… | Xử lý | Vì sao |
|---|---|---|
| **Ngay lúc authoring, hoặc nổ ngay lần Play đầu** — và **đã kiểm là nó nổ thật** | **để nó nổ**, không guard | exception thô và `LogError` đẹp chặn developer ngang nhau; đổi cái trước thành cái sau là trả phí **vĩnh viễn trong build** cho một lần đọc log dễ hơn. Guard cho ca không bao giờ xảy ra vẫn là một nhánh phải đọc, test, giữ đúng mãi (NT1) |
| **Lọt qua authoring rồi sai âm thầm** giữa gameplay | **guard đầy đủ** — bất biến thật, về bảng trên | Editor không bắt chắc được: reference chỉ có lúc runtime · null ở một prefab variant hoặc một scene trong nhiều scene · sai chỉ hiện ở một tổ hợp cấu hình · thành null sau `Destroy` · **dữ liệu từ ngoài** (import, server, save — §3.8) luôn thuộc nhóm này |
| **Không gọi tên được** lượt kiểm nào bắt nó | **guard** | "chắc là không xảy ra đâu" không phải bằng chứng (NT8) |

**"Để nó nổ" đứng được nhờ vế *nổ* — phải kiểm, không được giả định.** Bốn hình dạng nó **không** nổ:

| Hình dạng | Vì sao im lặng | Trả về chỗ nổ bằng |
|---|---|---|
| **Giá trị mặc định của kiểu là giá trị hợp lệ** — `float` 0, `bool` false, list rỗng, enum phần tử đầu | ô trống trong Inspector **không phân biệt được** với ô cố ý điền giá trị đó | **authoring, không phải guard runtime**: default ngay trong khai báo field, kẹp dải bằng `[Min]`/`[Range]` |
| **Host bắt lỗi fail-open** — vòng dispatch, chain boot log rồi bỏ qua step lỗi | thiếu wire thành **một dòng log lúc boot** rồi cả phiên chạy thiếu hẳn một hệ | không thêm guard: hệ tự đứng được không cần host đó, hoặc thiếu nó phải hỏng ở **cửa mà người chơi chạm** |
| **Vòng lặp nhận sai token** | hủy là hành vi hợp đồng, không log (§3.1) | mỗi loop chỉ ra được token của nó là đời của ai |
| **Chỗ nổ nằm trong `#if DEBUG`** | Editor và dev build ném to, **bản release không có dòng đó** — cùng một ca thành null chạy tiếp rồi NRE ở chỗ khác, xa chỗ sai | đọc `#if` bao quanh mọi guard của thư viện ngoài trước khi tin vào nó |

*Đã sai một lần, nguồn của hai hàng đầu:* một hệ lưu dữ liệu có field khoảng thời gian không default —
quên điền ra 0, thành ghi đĩa **mỗi frame**; cùng hệ gắn loop tự lưu vào token của pha boot mà runner
refresh token mỗi lần load level — loop chết từ level thứ hai. Cả hai không một dòng log.

**`try/catch` đi qua đúng câu hỏi đó.** Chỉ bọc khi **gọi được tên thứ ném ra**: API thật sự ném (I/O,
parse, network, reflection, dữ liệu từ ngoài — §3.8), hoặc code của người khác chạy trong vòng lặp của
mình (§3.5). Không gọi tên được thì bỏ — khối `catch` cho ca không bao giờ xảy ra **nói dối** rằng chỗ
này có rủi ro. Bắt rồi thì phải **làm gì đó**: xử lý, hoặc log kèm ngữ cảnh rồi ném lại; nuốt exception
rồi chạy tiếp là biến lỗi tỏ thành lỗi âm thầm. `catch (Exception)` trần chỉ ở **biên trên cùng**: một
vòng dispatch, một entry point của tool.

**Quy ước chỉ thay được guard khi nó viết ra ở chỗ người vi phạm đang nhìn** — Tooltip, XML doc của
chính API đó, dòng trong tài liệu module — không phải một lượt chat. Và **bug "sai âm thầm" sửa xong
thì chưng cất thành một dòng bất biến trong tài liệu module**, dạng *"đã sai một lần: [triệu chứng]"* —
tri thức đắt nhất và không đọc ra được từ code (§5.4).

## 3.5 Async & tài nguyên

Tiêu chí: **hủy được** (việc dừng theo owner) · **giải phóng được** (thứ giữ tài nguyên có đường trả
lại) · **cô lập được lỗi** (một callback lỗi không kéo cả hệ chết — `try/catch` quanh từng callback
trong vòng dispatch, ca được phép theo §3.4).

> **Nền tảng** — lựa chọn mặc định và ràng buộc đi kèm:
>
> | Nhu cầu | Dùng | Ràng buộc không bỏ được |
> |---|---|---|
> | Async | **UniTask**, không coroutine, không `Task` | propagate `CancellationToken` xuống toàn bộ chain; token là đời của host (§3.1) |
> | Load asset | **Addressables** qua `AssetReference` | không dùng string key; giữ `AsyncOperationHandle` để `Release()` đúng lúc, không giữ là leak |
> | Data lớn | `NativeArray` / `NativeList` | khi truyền GPU hoặc Job System; `StructLayout(Sequential)` khi phải khớp layout native |
> | Tài nguyên nặng | cache `RenderTexture`, `Texture2D`… | có đường dọn dẹp trong `OnDestroy()` |

## 3.6 Editor-first

Luật ở NT4; đây là nơi duy nhất dẫn giải. **Dấu hiệu code đang làm việc của Editor:** `GetComponent` /
`Find` / `AddComponent` / `Resources.Load` để lấy thứ đã có trên prefab · hằng số tinh chỉnh cảm giác
hardcode · dựng hierarchy bằng code · một API `Bind…` / `Attach…` nhận reference từ ngoài vào.

**Kéo thả vào `[SerializeField]` là DI, không phải một cách làm cạnh tranh với DI.** Consumer vẫn
không tự `new` và không tự đi tìm — nó **nhận vào** (`D`, §3.1). Khác duy nhất là composition root:
code DI thì root là một file code chạy lúc boot, kéo thả thì root là **chính cái asset**.

**API nhận reference là code nối, dù nó mang hình dạng DI.** `Bind(Transform)` chỉ chính đáng khi
Inspector **thật sự không kéo được** — và câu đó phải **kiểm**, không phát biểu: cùng scene thì kéo
được, khác scene hoặc prefab dựng lúc chạy thì không. Kéo được mà vẫn viết hàm bind là đổi một ô trống
**nhìn thấy lúc authoring** lấy một lời gọi bị quên **không để lại dấu vết nào**.

> **Nền tảng** — lý do gốc: dữ liệu serialize sửa được **không cần compile**, ai trong team cũng chỉnh
> được, thiếu thì lộ ra ô trống trong Inspector chứ không nổ giữa gameplay, và giá trị thật **đọc được
> bằng mắt ngay trên đối tượng**.

**Sổ tay** — reference kéo thả vào `[SerializeField]` · component add sẵn trên prefab · số tinh chỉnh
phơi ra Inspector · preset thành ScriptableObject · wire sẵn trong prefab rồi `Instantiate`.

**Biên của "kéo được thì kéo" là số chỗ phải nối** (câu phân xử ở §0.1), và lý do nằm ngay trong cái
bảo đảm đang bênh kéo thả: **ô trống chỉ lộ ra khi có người nhìn vào ô đó.** Một ô trên một object thì
chắc chắn thấy; ba mươi ô rải trên ba mươi prefab thì không ai rà, và chúng hỏng từng cái một ở từng
lần chơi khác nhau. **Cái giá phải nhận:** wiring nằm trong asset thì **không grep được** — quan hệ kéo
thả chỉ tồn tại dưới dạng GUID trong file serialize, nên thước đo chi phí đọc của NT1 phải cộng thêm
*"bao nhiêu asset phải mở Editor mới thấy"*; người đọc sau, và mọi agent đọc repo bằng text (§2.7),
đều trả khoản này. Không phải lý do bỏ kéo thả; là lý do **không rải** nó ra nhiều chỗ.

**Ngoại lệ tự nhiên** là thứ chưa tồn tại lúc authoring: object spawn runtime, số lượng động, dữ liệu
từ server. Plan chạm scene hoặc prefab thì mô tả thao tác Editor **như một bước thật**, không lặng lẽ
thay bằng code.

**Bắt buộc phải nối bằng code thì bên đời ngắn tự trình diện với bên đời dài, không phải ngược lại.**
Bên đời ngắn biết chính xác lúc nó xuất hiện và biến mất, nên đăng ký và huỷ đăng ký là **một cặp nằm
trong một file**. Bên đời dài đi tìm thì phải poll, hoặc rốt cuộc vẫn phải chờ được báo — và nó không
có chỗ tự nhiên nào để biết lúc phải gỡ ra.

**Điều kiện dựng hệ ghi vào tài liệu, không sinh code canh.** Tạo asset, dựng GameObject, gán
reference, đặt layer/tag, thêm scene vào build — người dựng làm **một lần**, viết thành một bước ở mục
**"Trước khi chạy"** của tài liệu module (§5.1). Thiếu setup thì **để nó nổ** ở lần Play đầu — nhưng
phải kiểm rằng nó nổ thật (§3.4).

**Bước setup bằng tay là cách developer HIỂU hệ.** Người tự kéo asset vào ô, tự đặt số thì giữ được
trong đầu **các mảnh rời của hệ và chỗ chúng nối vào nhau** — thứ duy nhất dùng được khi hệ hỏng lúc 2
giờ sáng. **Hệ không gánh lỗi quên setup của người dùng nó**; đổi lại hệ **nợ** một mục "Trước khi
chạy" đủ để dựng lại từ 0 — bỏ code canh mà không viết bước setup chỉ là đẩy việc.

## 3.7 Naming — self-documenting code

- **Tên là lời giải thích thứ nhất, comment là phương án cuối.** Tên đạt khi developer mới đọc hiểu
  ngay mà **không cần comment**; phải kèm comment mới hiểu nghĩa là tên chưa đạt — đổi tên trước, không
  viết thêm chữ. Tên tự giải thích **mạnh** nhưng gọn: trần là **5 từ dài**, 6–7 từ ngắn đọc một mạch
  vẫn đạt (`elapsedSecondsSinceLastSave`). Từ dài thì **viết tắt quen mắt** — `curr` · `prev` · `max` ·
  `min` · `Utc` — không tự chế cách viết tắt mới. Viết tắt rồi vẫn vượt trần là khái niệm chưa tách đủ.
- **Tên mang đủ ngữ nghĩa về giá trị nó giữ** — đọc tên biết ngay *cái gì*, *lúc nào*, *đã xảy ra gì*,
  không suy từ ngữ cảnh xung quanh:
  - *Lúc nào / thứ tự*: `curr` · `last` · `prev` · `next` — `currLevelIndex`, không `levelIndex` (level
    nào?). Field cùng nói về một đối tượng dùng chung tiền tố: `CurrLevelIndex` · `CurrLevelScore`.
  - *Đếm hay vị trí*: hậu tố `Amount` cho số lượng, `Index` cho vị trí — `loadedAssetsAmount` vs
    `currAssetIndex`. Danh từ số nhiều đứng một mình (`loadedAssets`) đọc như một danh sách.
  - *Tổng dồn*: tiền tố `total` — `totalSpentCoins`.
  - *Đã xảy ra gì*: quá khứ phân từ nói điều đã xảy ra **với giá trị, theo góc nhìn người chơi hay dữ
    liệu** — `collected` · `animated` · `claimed` · `played`: `currAnimatedScore` (số người chơi đã thấy
    chạy tới), `playedIntro`. Không dùng từ mờ (`introDone`) hay từ tả hành động của hệ
    (`presentedScore`).
  - *Số nhiều là lời hứa về số lượng*: type mang đúng một `ItemIndex` thì tên là `ItemClaimed`, không
    `ItemsClaimed` — người đọc thấy số nhiều sẽ đi tìm một collection không tồn tại.
  - *`-ing` là tiến trình, không phải trạng thái*: `IsPanelOpen` khác `IsPanelOpening`. Dùng nhầm thì
    người sau sẽ thêm một cờ thứ hai cho trạng thái thật.
  - *Biến cục bộ cùng luật*: `cumulativeWeight`, `visibleItemsAmount` — tính từ đứng một mình không
    phải tên. Tính từ đứng trước danh từ theo tiếng Anh: `inclusiveEndIndex`, không `endIndexInclusive`.
- **Tiền tố loại type**: interface `I…`, abstract class `A…` (`ALiveOpsModule`) — nhìn tên biết ngay
  không `new` được và phải tìm subclass.
- Tên method nói rõ **mục đích**: `EnsureMaterial()`, `SwapWriteBuffer()`, `SolveAnalytic()`. Tên vô
  nghĩa cần thay: `Process`, `Handle`, `DoWork`, `Update2`. Boolean đọc như một câu hỏi: `IsPickable`,
  `HasPendingInput`, `frameDataReady`; `IsFinished`, không `Finished`.
- **Comment chỉ khi thật sự cần thiết — mặc định là không có.** Tự giải thích áp cho cả tên **và
  logic**: đoạn nào cần comment mới theo dõi được thì tách hàm có tên, đảo điều kiện, đặt biến trung
  gian có tên — sửa code, không chú thích code. Comment còn lại **chỉ** nói **tại sao** (quyết định
  trái trực giác, bẫy đã sai một lần §3.4, công thức nguồn §4), không bao giờ nói **cái gì**.
- API public có XML doc; `<param>` cho mọi tham số có contract không hiển nhiên. **Comment, tooltip và
  XML doc là chỗ đọc nhanh** — cơ chế và trade-off thuộc tài liệu của hệ (§5). Trần: **17–20 từ** cho
  comment và tooltip · **35 từ** cho `<summary>` · **15 từ** cho `<param>`, `<returns>` và mỗi thẻ còn
  lại. Chạm trần là tín hiệu: **sửa gốc trước khi viết thêm chữ**, hoặc phần đang viết vốn thuộc về
  tài liệu.
- **Một từ = một nghĩa trong toàn hệ thống** — một từ mang hai nghĩa thì đổi tên một bên ngay. Khái
  niệm không đặt nổi tên riêng thường là khái niệm chưa rõ.
- **Đổi tên là đổi cả hệ**: code, comment, chuỗi debug, mọi tài liệu — cùng một lần làm, kiểm bằng
  grep. **Ranh giới:** khoá wire format và dữ liệu đã serialize (key JSON, tên field trong save,
  schema) là **hợp đồng với hệ khác** — không đổi theo, không tính là "tên cũ còn sót".

## 3.8 Dữ liệu đi qua tool — import/export

File mà tool đọc–ghi là **của người dùng và của hệ khác**, không phải của tool:

| Luật | Nghĩa là |
|---|---|
| **Không bao giờ sửa giá trị dữ liệu import** | ngoài tầm hợp lệ thì **từ chối cả file**, hoặc **bỏ qua entry đó** kèm cảnh báo. Clamp là xuất ra bản lệch mà không ai biết |
| **Bỏ entry hỏng, không bịa entry mới** | chèn dữ liệu để "sửa giúp" là thêm thứ không có trong file — tệ hơn thiếu |
| **Chỉ sở hữu phần mình hiểu** | export clone dữ liệu gốc rồi ghi đè đúng những khoá tool sở hữu; khoá lạ **đi qua nguyên vẹn** — dữ liệu của hệ khác, không phải rác |
| **Dữ liệu gốc đi theo đối tượng** | giữ tham chiếu bản gốc trên chính đối tượng, không tra lại theo toạ độ hay vị trí mảng lúc export — hai thứ đó đổi theo thao tác, tra theo chúng là gán dữ liệu cho đối tượng **khác** |
| **Import và Export sống cạnh nhau** | hai chiều của cùng cụm dữ liệu trong một file — không trôi lệch nhau; nghiệm thu bằng round-trip (§4.3) |

## 3.9 Editor tool

Tiêu chí: **một tool là một đơn vị gói kín** · **thứ dùng chung sống một chỗ** (§3.2) · **thứ không
đổi giữa các lần vẽ lại phải có sẵn**.

> **Nền tảng** — IMGUI (`OnGUI`, `EditorWindow`, `PropertyDrawer`): một lần tương tác gây nhiều lần gọi
> `OnGUI` cho **cùng một state**, mỗi lần chạy lại toàn bộ hàm — thứ không đổi mà bị tạo lại trong đó
> là rác thuần. `GUIStyle`, `GUIContent` có icon chỉ tồn tại sau khi `GUI.skin` và `EditorGUIUtility`
> sẵn sàng, nên không khởi tạo được ở static initializer.

**Sổ tay** — *áp cho IMGUI*; UI Toolkit có mô hình repaint khác hẳn nên bảng này không áp:

| Cấp cache | Kỹ thuật | Khi nào dùng |
|---|---|---|
| Static eager | `static readonly` | giá trị bất biến: `Color`, `GUILayoutOption[]`, `GUIContent` chỉ có text |
| Static lazy + guard | init một lần trong `EnsureStyles()` | thứ cần `GUI.skin` hoặc `EditorGUIUtility` |
| Instance lazy | null-check init ở cấp window | style riêng từng tool |
| Dirty-flag | chỉ rebuild khi dữ liệu đổi | layout options khi window resize |
| Event-phase | tính nặng chỉ ở `EventType.Layout` | filter, sort, format — `Repaint` dùng lại kết quả |

**Tool có người dùng thì UX là một phần của thiết kế:**

- **Vùng UI phải nói lên ranh giới** — thứ khác vai trò (ghi vào dữ liệu / chỉ đổi cách xem / dùng ở
  mọi lúc) không nằm chung một vùng. Ranh giới phải giải thích bằng một cột trong hướng dẫn là ranh
  giới người dùng sẽ nhầm.
- **Điều kiện vẽ và điều kiện bấm được suy từ một nguồn** (§3.4) — phá thì nút đang hiện mà bấm không
  có gì xảy ra.
- **Không giấu thứ có thật**: điều kiện vẽ là *"có dữ liệu"*, không phải *"tra được tài nguyên để
  vẽ"* — tra thiếu thì vẽ dạng báo lỗi kèm id, không để dữ liệu biến mất khỏi màn hình trong khi vẫn
  được xử lý và ghi ra file. Field chỉ-đọc vẫn phải hiện, khác kiểu với field sửa được.
- **Phép kiểm tính hợp lệ chạy khi được hỏi**, không chạy nền theo mỗi thay đổi: lúc đang dựng thì dữ
  liệu **luôn** chưa hợp lệ, cảnh báo nền hiện đúng lúc chưa thể sửa.

---

# §4 — Hệ toán học và vật lý

## 4.1 Khi nào dùng tới toán, và sâu tới đâu

Mở ra khi **developer yêu cầu**, hoặc khi **agent thấy toán giải bài toán tốt hơn hẳn** — trường hợp
sau thì nêu kèm được–mất để developer quyết, không tự đưa vào (NT5).

**Mặc định là không cần toán.** Phần lớn logic gameplay là trạng thái và luật rời rạc; toán là một lớp
phức tạp phải trả bằng nhu cầu thật (NT1). Xét hết cách rẻ hơn và dễ chỉnh hơn trước: `AnimationCurve`
hoặc bảng tra do designer chỉnh trong Inspector (NT4) · easing có sẵn · lerp · máy trạng thái · một
hằng số chọn bằng tay. **Dấu hiệu đang ép toán vào chỗ không cần:** phải dẫn định luật nền để biện minh
một phép nhân · công thức chỉ có một call site và không tham số nào thay đổi · designer không chỉnh được
gì · kết quả thay bằng vài giá trị trong bảng là xong.

**Bờ vực còn lại cũng sai:** bài toán vốn liên tục và có ràng buộc — dừng đúng chỗ, va chạm, nội suy
cần đạo hàm liên tục — mà né toán thì thành một đống hằng số không ai hiểu, sửa chỗ này vỡ chỗ khác.
Toán đúng chỗ làm code **ngắn hơn**.

**Đã cần toán thì sâu vừa đủ cho tính năng:** mô hình đủ để chạy đúng và cho cảm giác đúng, không phải
đúng nhất về vật lý — xấp xỉ là **mặc định** (NT8). Khi **thật sự** cần bản đầy đủ thì **làm tử tế**:
cắt nửa vời rồi bù bằng hằng số là cách sinh ra hệ không ai dám sửa.

## 4.2 Cần thì phải cho hiểu sâu

Người đọc phải **hiểu hiện tượng** · **tin công thức là suy ra được** · **kiểm lại được** bằng tay.
Mạch dưới dành cho công thức **không hiển nhiên** (NT10).

| # | Câu hỏi người đọc sẽ hỏi | Phải trả lời được gì | Sổ tay — dạng trình bày |
|---|---|---|---|
| 1 | Cái này mô tả hiện tượng gì? | mô hình thực tế đằng sau, và nó map sang mục đích thế nào | bảng "thành phần → vai trò" |
| 2 | Vì sao mô hình đó đúng? | định luật hoặc định lý gốc | diagram |
| 3 | Phương trình là gì? | phương trình chi phối, ý nghĩa từng ký hiệu | `$$…$$` kèm bảng ký hiệu |
| 4 | Vì sao chọn cái này? | các lựa chọn đã cân, tiêu chí loại | bảng so sánh có cột ✓ |
| 5 | Từ phương trình gốc ra nghiệm trong code thế nào? | từng bước biến đổi, **không nhảy bước**, mỗi bước một câu vì sao | đánh số ①②③ |
| 6 | Làm sao tin nghiệm này đúng? | giá trị tại các mốc biên so với kỳ vọng | bảng "mốc → kỳ vọng → ✓" |

Không thương lượng:

- **Trực giác trước, ký hiệu sau** — ý niệm bằng lời thường ("càng xa đích thì đi càng nhanh"), rồi
  mới ra phương trình.
- **Suy ra, không áp đặt** — công thức chốt *dẫn ra* từ nguyên lý gốc, không "xuất hiện từ hư không"
  rồi giải thích ngược.
- **Ngoại lệ: thứ chọn bằng cảm giác** — hằng số tinh chỉnh, đường cong tự chế thì nói thẳng "chọn
  bằng tai và mắt, số này cho cảm giác X". **Đừng bịa dẫn giải vật lý** cho giá trị chọn bằng cảm nhận:
  nó làm hỏng niềm tin vào cả phần thật sự có dẫn giải.
- **Lệch vật lý chuẩn là bình thường, không phải lỗi cần bào chữa** (NT8) — chỉ nêu lệch ở đâu, vì
  sao, khi nào mới cần bản đầy đủ.

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
tài liệu trong cùng lần làm** — riêng `.html` theo nhịp mốc.

| Loại | Vai trò | Vòng đời |
|---|---|---|
| **`.md`** | tài liệu **agent đọc** để hiểu và phát triển hệ — bản đặc, plain text, rẻ token; nguồn sinh `.html` | cập nhật **cùng lần làm** với mỗi thay đổi |
| **`.html`** | tài liệu **developer và game designer đọc** — trực quan hóa 100% nội dung `.md`; agent không đọc bản này khi đã có `.md` | đồng bộ từ `.md` theo **mốc** — developer yêu cầu, hoặc chốt xong một cụm thay đổi |
| **Plan** (khi developer yêu cầu) | để developer **tự code lại** nhằm học | luôn là `.md`; theo task |
| **Manual** (khi tool có người dùng không phải developer) | người dùng đọc để **thao tác** — luật viết ở §5.4 | sống cùng tool |

**Ai viết: agent, cả bốn loại.** Nghiệm thu tài liệu là **đối chiếu máy móc với code**, đúng chỗ máy
hơn người ở sức và nhất quán chéo (§2.8). Developer **chốt nội dung**: dòng nào lệch thiết kế thì
developer phân xử, agent sửa. Riêng **Plan** có phân công khác — ở §5.3.

**Quy trình:** phỏng vấn ngữ cảnh (§2.1) và đối chiếu hiểu biết về code (§2.2) → đọc **tất cả** source,
hiểu 100% data flow, lifecycle, lý do mỗi quyết định → viết `.md` → sinh `.html` từ `.md` → khi được
yêu cầu thì viết Plan.

**Chuỗi sự thật một chiều `code → .md → .html`** (một dạng "hai bản buộc khớp thì suy từ một nguồn",
§3.4): code là chuẩn, hai tài liệu phản ánh **100% thiết kế đang chạy trong code**. **Không gộp, không
xóa** hai bản. Lệch thì sửa xuôi theo chuỗi: `.html` lệch → đối chiếu `.md` với code trước, rồi đồng bộ
`.html` từ `.md`. **"100%" là không mất nội dung khi chuyển bản, không phải viết cho nhiều** — trần độ
dài do §5.4 canh, trên cả hai bản.

**Sổ tay** — checklist mỗi lần đồng bộ: soát "không viết theo trí nhớ" và "ở thì hiện tại" của §5.4 ·
đổi tên hay xóa file tài liệu thì grep **tham chiếu chết** trong code comment, file hướng dẫn agent của
dự án, tài liệu khác.

## 5.1 `.md` — tài liệu cho agent

Tổ chức theo **đường đi của dữ liệu** (input → processing → output), không theo "lý thuyết → thiết kế →
code": người đọc cần lần theo được một giá trị từ lúc vào đến lúc ra. Code trích nguyên văn, không viết
lại. Bảng metrics tổng kết đặt cuối.

**Sổ tay** — luồng dữ liệu vẽ bằng ASCII vì `.md` được đọc bằng nhiều công cụ; công cụ chắc chắn render
được mermaid thì dùng mermaid cũng được (NT6).

**Một mục bắt buộc: "Trước khi chạy", với mọi hệ cần wire tay** — hợp đồng đi kèm của việc hệ không tự
setup hộ (§3.6). Nghiệm thu: người chưa từng mở hệ dựng lại được **từ 0** chỉ bằng mục này, theo thứ
tự, không đọc code hay hỏi ai. Viết bằng **thao tác và nhãn thật trên UI** — đường dẫn menu, tên ô
trong Inspector, thứ kéo vào ô nào — mỗi bước kèm *"thiếu bước này thì hỏng ở đâu"*. Hệ không cần wire
gì thì không có mục này.

**Còn lại là kho mục để chọn, không phải form để điền.** Mỗi mục đưa vào phải gọi tên được **câu hỏi
của người đọc** mà nó trả lời; hệ nhỏ có ba mục là bình thường. Kho: Data structures · Core algorithm ·
Lifecycle · Implementation details · Framework integration · Design decisions · Safety và error ·
Platform issues · Architecture (file tree kèm vai trò) · Testing · Extension · Performance.

**Nghiệm thu:** lần theo được một giá trị từ input tới output mà không nhảy section · mỗi so sánh đều
thấy **tiêu chí** và **kết luận** · dựng được `.html` 100% từ file này mà **không cần mở source**.

> **Nền tảng — KaTeX:** mọi lệnh có `\` (`\frac`, `\sqrt`…) **phải** nằm trong `$…$` hoặc `$$…$$`;
> trong backtick sẽ hiện raw text. Mỗi block `$$…$$` trên **một dòng**. Chốt xong quét: strip hết
> `$…$` và backtick, còn sót `\[a-zA-Z]` nào là lọt.

## 5.2 `.html` — tài liệu chính

Giữ **cấu trúc section của `.md`** để hai bản đối chiếu được. "Không viết theo trí nhớ" (§5.4) áp cả
cho **số liệu trong demo**. **Để hiểu, không để chép code**: chữ ký API thành bảng, chỉ giữ code khi
đoạn code *là* thứ cần minh hoạ.

Trực quan hóa **theo loại nội dung**: so sánh thì bảng · luồng dữ liệu thì diagram · quan hệ định
lượng thì công thức · giá trị biến thiên liên tục thì Canvas · quá trình nhiều bước thì step. Demo chỉ
làm khi bảng và text **không đủ** để thấy hành vi.

**Nghiệm thu:** đủ 100% nội dung nguồn · single file · TOC khớp section thật · đọc được trên màn hình
nhỏ · người đọc *hiểu* được hệ mà không cần đọc code · mở trang không tương tác thì không tiến trình
nào chạy · mỗi demo thao tác được và cho thấy đúng hành vi đang nói tới.

**Sổ tay** — copy `DOCS_TEMPLATE.html`, thay các chỗ `{…}`, xoá section mẫu và khối demo mẫu. Khối xây
sẵn, thứ template tự làm, và **bảng cấm trong draw loop và handler của Canvas/DOM** nằm trong khối
hướng dẫn ở đầu chính file đó (NT7). Không dùng thư viện tô màu code (NT1).

## 5.3 Plan — để developer tự triển khai

Tiêu chí: **tự chứa**. Developer code lại được từ đầu đến cuối mà **không phải suy đoán**, không phải
mở tài liệu khác. Các task xếp theo **thứ tự phụ thuộc**, mỗi task chỉ cần thứ đã có ở task trước.
Hệ có lõi toán (§4.1) thì mục `§0` của Plan dẫn giải tại chỗ theo mạch §4.2.

**Phân công riêng của Plan: lõi developer viết, test agent viết.** Plan tồn tại vì developer muốn tự gõ
lại để học, nên code lõi trong Plan là bản để đọc và gõ, không phải để agent commit — ngoài Plan thì
agent viết code như mọi task khác. Test thì agent viết và chạy (§2.8), nên Plan **chỉ có danh sách case
sẽ kiểm**, không có code test. **Nhịp:** developer code xong lõi → agent đọc code thật rồi mới viết
test, vì chữ ký lúc viết Plan còn là nháp. Danh sách case là chỗ developer veto hoặc thêm case.

**Sổ tay** — kho phần cho mỗi task, **chỉ lấy phần task này cần**: Files (đường dẫn chính xác) ·
Interfaces (consumes và produces, chữ ký đầy đủ) · bảng "toán → code" trỏ về `§0` · bảng lý do cho
mỗi quyết định thiết kế và tối ưu · **code hoàn chỉnh dán được** với comment trỏ công thức nguồn ·
**Editor setup** khi chạm scene hoặc prefab (§3.6) · **bảng case kiểm thử** (input → kỳ vọng, kèm biên
theo §2.8).

**Plan không thuật lại code.** Bảng lý do ghi *quyết định và vì sao chọn nó*, không kể *code làm gì*
(§5.4).

**Hai loại Plan, hai hình dạng code.** Hệ **chưa có code** thì mỗi khối là **một file trọn vẹn** để
chép. Hệ **đang có code** thì Plan là **chuỗi chỗ đổi theo thứ tự**: mỗi bước một chỗ — file · dòng
hiện tại · đoạn cũ → đoạn mới; dòng không nhắc là dòng giữ nguyên. Chép nguyên khối chỉ khi gần mọi
dòng của khối đều đổi, và kèm một dòng "so với cũ". Chép cả file cho hệ đang có là bắt developer tự so
từng dòng để tìm chỗ khác — đúng việc Plan phải làm hộ.

**Code trong plan — bốn đảm bảo:** **vừa đủ** và **mở đường mai** (NT1) · **đúng với công thức đã
chốt** — "code khớp công thức", không phải "công thức phải khớp vật lý" (NT8) · **hiệu năng** theo NT2
và §3.3 · **self-document** theo §3.7.

**Nghiệm thu riêng:** có mục "Ngữ cảnh đã chốt" (§2.5) · mọi hàm có caller thật, hoặc có lý do phòng
xa chữ ký nói được ra (NT1) · công thức đã đối chiếu với code (§4.3) · phần sẽ test có bảng case, không
có code test.

## 5.4 Kỷ luật viết và bảo trì — áp cho mọi loại tài liệu

**Cắt trước khi giao — bắt buộc.** Bản đầu luôn có nước. Cắt theo thứ tự: câu dẫn *"phần này sẽ nói
về…"* · tóm tắt lại thứ vừa nói · câu chuyển tiếp · nhận xét về chất lượng thiết kế · ẩn dụ và câu chốt
có vần · cùng một ý viết hai lần · lý do dài hơn một câu cho một khẳng định hiển nhiên (NT10). Cắt
xong mà vẫn dài thì nội dung thật sự lớn — **tách file**, đừng nén chữ.

**Luật câu chữ của §2.3 áp cho tài liệu**: một câu một ý · hạn chế thuật ngữ · gọi khái niệm đúng tên
trong code · kết luận trước, dẫn giải sau. Chỉ khác ở **trần**: đối thoại canh theo quyết định đang
chờ, tài liệu canh theo **việc người đọc phải làm được sau khi đọc**.

| Luật | Nghĩa là |
|---|---|
| **Mỗi tài liệu một người đọc** | mỗi loại trả lời đúng câu hỏi của người đọc nó; chép nội dung loại này sang loại kia là sai cả hai. **Cặp `.md`–`.html` không thuộc lỗi này** (§5): cùng nội dung, hai người đọc. Tool có người dùng không phải developer thì có **Manual riêng**: viết theo **nhãn thật trên UI**, trả lời *"bấm gì ra gì, dùng khi nào"* — không tên class, không lý giải cách cài đặt |
| **Không chép lại thứ người đọc đã cầm trong tay** | `.md` và `.html` viết cho người mở được **code** — cột "vai trò" nói lại đúng tên hàm, dòng bảo đảm mà một dòng `catch` đã nói hết, metrics `O(n log n)` của một `Sort`: **bỏ**. Plan viết cho người **chưa có code**, chuẩn là các task trước trong Plan; Manual cho người **không đọc code**, chuẩn là màn hình trước mặt. Phép kiểm (NT1): *xoá dòng này thì người đọc mất gì* — đáp án "mở file kia ra xem" thì bỏ. **Ngoại lệ giữ bằng mọi giá:** danh sách chữ ký API trong `.md`, vì `.html` dựng từ `.md` mà không mở source (§5.1) |
| **Không viết theo trí nhớ** | mọi tên file, signature, hằng số, nhãn UI đều mở code đối chiếu lại trước khi ghi — kể cả khi vừa viết chính dòng code đó (NT8). Nguồn sai nhiều nhất của tài liệu |
| **Code đã tồn tại thì khối code trong Plan là bản sao nguyên văn** | đồng bộ bằng cách chép file rồi **so sánh máy**, không gõ lại — gõ tay là mở lại đúng nguồn sai mà luật trên đang chặn. Lý do ở lại bảng quyết định dưới khối; đó mới là thứ code không tự nói được |
| **Viết cho người đọc lần đầu, ở thì hiện tại** | NT10 áp cho tài liệu. **Ngoại lệ duy nhất:** sổ ghi bẫy *"đã sai một lần"* (§3.4) trong mục quyết định thiết kế — ghi **bài học**, không tường thuật thay đổi, và không lưu tên cũ (§3.7). **Sổ tay** — grep các cụm kể lịch sử ("trước đây", "bản cũ", "giờ đã") |
| **Câu hỏi của người đọc là bằng chứng tài liệu chưa rõ** | trả lời xong phải để câu trả lời lại trong tài liệu, không để nó chết trong hội thoại |
| **Mâu thuẫn thì SỬA dòng cũ** | không thêm dòng thứ hai nói ngược. **Riêng dòng cũ ghi quyết định hoặc ranh giới do developer đặt thì không tự sửa** — nêu chỗ lệch để developer phân xử (NT5) |
| **Quyết định trái trực giác gom về một mục riêng** | chỗ cố ý trông "kém tối ưu" phải có lý do viết sẵn ở một nơi biết trước; người tối ưu sau đọc mục đó **trước khi đụng** (cùng họ với "cố ý KHÔNG làm" của §2.5) |
| **Tư tưởng mới chưng cất ngay trong task** | task, câu hỏi, phản hồi nào xác lập **quy ước còn đúng ở lần sửa sau** thì ghi vào tài liệu module **trước khi báo hoàn thành** — quyết định chỉ sống trong hội thoại thì chết cùng hội thoại. Ghi *quy ước ở thì hiện tại*, không tường thuật task. **Liệt kê nguyên văn các dòng đã ghi trong báo cáo hoàn thành** để developer veto được bản khái quát sai. Thêm vào chính MY_SKILL thì theo §2.6 — hỏi developer trước |
