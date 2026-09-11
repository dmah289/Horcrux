# MY_SKILL.md — Tư tưởng thiết kế hệ thống & quy trình làm việc với AI

Dành cho **game trên Unity**. Tư tưởng, không phải tài liệu của một dự án — mang sang dự án nào cũng
dùng nguyên. Áp cho mọi thứ viết ra: runtime, editor, tooling, script tạm, tài liệu.

**Ai đọc:** AI agent, trước khi làm bất cứ việc gì · developer, khi review output.
**Đi kèm:** `DOCS_TEMPLATE.html` (§5.2).

**Ba thứ đứng trên khi phải cân đo:** **hiệu năng runtime** (NT8) · **hệ mang được sang dự án khác**
(NT9) · **một luồng đọc một mạch, không xé vụn** (NT6). Không đánh đổi âm thầm — hy sinh cái nào thì
nói ra tại chỗ và nói giá. Ba cặp chúng hay bị đọc thành đá nhau với NT3 nằm ở §1.8.

## Ba tầng ràng buộc

| Tầng | Nhận biết | Nghĩa | AI được phép |
|---|---|---|---|
| **Luật** | *mặc định*, không đánh dấu | tiêu chí nghiệm thu: nói **cần đạt gì**, không nói **làm thế nào** | không bỏ; tự chọn cách đạt |
| **Nền tảng** | khối `> **Nền tảng**` | đáp án đã chốt: **giới hạn thật** của Unity, browser, renderer — hoặc **stack mặc định** của bộ này | không đi đường khác. Dự án thiếu món nào thì quay về tiêu chí ở phần Luật ngay trên khối |
| **Sổ tay** | dòng `**Sổ tay** —` | một cách đã dùng và chạy được; không phải cách duy nhất | thay bằng cách hay hơn, kèm lý do và phép kiểm (NT2) |

**Ký hiệu:** `NT<n>` là nguyên tắc số n ở §1 · `§x` là mục trong file này.

**Số hiệu chỉ sống trong chính file này.** Code comment, tài liệu module và plan **không trích số
hiệu** — lý do viết bằng **nội dung** ("chép công thức ra chỗ thứ hai thì hai bản sẽ lệch"), không
bằng **con trỏ** ("vi phạm §3.4"): người đọc tài liệu đó không mở file này ra tra, và con trỏ mục nát
âm thầm khi cấu trúc đổi. Hệ quả: số hiệu ở đây **đổi được tự do**.

Sổ tay nào ghi **điều kiện áp dụng** thì ra ngoài điều kiện đó là vô nghĩa. §1 và §2 là nền, áp cho
mọi việc; §3–§5 là cơ chế riêng từng loại việc, tra khi chạm tới.

---

# §1 — Nguyên tắc chung

Nơi **duy nhất** định nghĩa mười lăm nguyên tắc dưới đây; các phần sau chỉ trỏ về. Mỗi ô là *định
nghĩa và phép kiểm*, dẫn giải đầy đủ ở mục § ghi cuối ô.

## 1.1 Ai quyết — cái gì tự đi, cái gì phải hỏi

| # | Nguyên tắc | Nội dung |
|---|---|---|
| 1 | **Hỏi đúng lúc, tự quyết đúng chỗ** | Thiếu ngữ cảnh thì **hỏi**, không đoán rồi làm. Buộc phải giả định thì ghi `Giả định (cần xác nhận): …` **tại chỗ dùng**, không giấu vào output như thể đã chốt. **Thao tác khó đảo ngược thì luôn hỏi trước khi chạy** — nhãn giả định không thay được xác nhận. Phạm vi hiện tại **chặn** hướng phát triển thật thì nêu ra **kèm giá phải trả**; mở rộng vì "cho đầy đủ, cho chuẩn hơn" thì không (NT3). → **§2.1** |
| 2 | **Phạm vi bàn được, cách làm luôn mở** | Tiêu chí đã chốt thì cách đạt là việc của người triển khai: thấy cách **cùng tiêu chí** mà đơn giản, nhanh, hoặc rõ hơn thì **dùng nó**, kèm lý do và phép kiểm (NT10). **Ranh giới cứng** — tự do chỉ khi cả ba thứ này không đổi: **hành vi quan sát được** (kể cả kết quả sinh ngẫu nhiên theo seed) · **dữ liệu ghi ra** (format lẫn giá trị) · **API công khai**. Đụng một trong ba là đổi phạm vi, phải hỏi (NT1). Im lặng chọn món trong sổ tay khi biết có cách tốt hơn là vi phạm. |

## 1.2 Đưa vào cái gì — bao nhiêu, hình dạng ra sao

| # | Nguyên tắc | Nội dung |
|---|---|---|
| 3 | **Vừa đủ** | Đơn giản là **mặc định**; mỗi lớp phức tạp thêm phải trả giá bằng **một nhu cầu đang có thật** — "phòng khi cần", "cho đầy đủ", "chuẩn hơn" **không** phải nhu cầu. Kiểm nhanh: *xoá nó đi thì hỏng ở đâu* — không gọi được tên chỗ hỏng thì bỏ. **Giá của một dòng code không nằm ở lúc viết ra nó** mà ở mọi lần sau: có người phải đọc nó, phải hiểu vì sao nó ở đó, phải nhớ nó khi sửa chỗ khác, phải giữ nó đúng. Agent sinh code gần như miễn phí nên hay bỏ quên vế giá này — đó là gốc của over-engineering. Áp cho cả **số lượng** lẫn **độ phức tạp của cách giải**: chỉ chuyển sang bản phức tạp khi bản đơn giản **chỉ ra được là không đạt** (NT10). Hai bờ vực đều sai: **thừa** (đúng, nhưng không ai cần) và **thiếu** (cắt vào mục đích ban đầu — mục đích là **sàn**). → **§2.4** |
| 4 | **Đủ hôm nay, mở đường mai** | Triển khai đúng phạm vi đã chốt, **không code sẵn** thứ chưa ai cần (NT3); nhưng hình dạng phải để bước kế tiếp là **thêm vào**, không phải **đập ra làm lại**. Đây là **cách sắp xếp**, không phải **thêm số lượng** — chi phí hôm nay gần bằng 0. **Phòng xa dồn vào chữ ký và ranh giới trách nhiệm**: một hàm thêm sau tốn hai phút, giữ nó trong API từ đầu tốn mãi mãi; ngược lại chữ ký sai thì sửa sau rất đắt. Nghiệm thu: gọi được tên bước kế tiếp, và chỉ ra được nó là "thêm" chứ không phải "sửa". |
| 5 | **Không lặp** | *Tài liệu:* một khái niệm giải thích một nơi, sau đó trỏ về; đã giải ở tài liệu hệ khác thì trỏ sang. *Code:* **một chức năng cài đặt một nơi** — có sẵn thì dùng lại (§2.4); có người dùng thứ hai thì nâng thành tái sử dụng được (§3.2). Luật này áp cho **tri thức trùng nhau**, không áp cho **code trông giống nhau**. Phân xử với luật "để lặp" của §2.4 bằng một câu hỏi: **hai bên lệch nhau thì có hỏng ngay không?** Hỏng ngay — đo lệch vẽ, vẽ lệch hit-test — thì gộp về một nguồn là **bắt buộc** (§3.4), kể cả khi công thức hiển nhiên. Không hỏng, chỉ là hai luật độc lập tình cờ giống nhau hôm nay, thì **để lặp**. |
| 6 | **Một luồng đọc một mạch** | Chi phí thật của một thiết kế là **số file người đọc sau phải mở để lần hết một luồng**, trả mãi mãi. Hai bờ vực: **xé vụn** — một tính năng rải qua nhiều component, nhiều lớp, hay một cây composite; mỗi mảnh đúng nhưng không mảnh nào nói được hành vi, phải chạy mới biết · **gom bừa** — một class ôm nhiều lý do thay đổi. Mặc định là **bậc thấp nhất còn đọc được**; leo bậc chỉ khi bậc dưới không còn đạt, và phải **gọi được tên thứ bậc dưới không làm được**. Composite còn là chi phí runtime — dispatch ảo, con trỏ nhảy, message của engine nhân theo số mảnh (NT8). Ba hình dạng composite phải viết lý do tại chỗ trước khi dùng: **§3.1**. |
| 7 | **Editor-first** | Thứ gì quyết được lúc authoring thì đừng đẩy sang runtime: **code lo *hành vi*, Editor lo *cấu hình và kết nối***. Việc bắt buộc làm **trước khi build** — tạo asset, dựng GameObject, gán reference — là **một bước ghi trong tài liệu**, không phải code canh lúc chạy. → **§3.6** |

## 1.3 Hai ràng buộc vận hành đứng trên

| # | Nguyên tắc | Nội dung |
|---|---|---|
| 8 | **Mỗi phép tính khai được nhịp của nó** | Nhịp là: *mỗi frame* · *mỗi tương tác* · *mỗi lần dữ liệu đổi*. Viết một hàm chạy trong vòng lặp là phải trả lời được nó thuộc nhịp nào; đặt ở nhịp nhanh hơn mức cần thì **không có gì báo sai** — chỉ có hệ chậm dần. Ba việc **luôn làm, không cần đo trước** vì không làm code khó đọc hơn: khai đúng nhịp · **không tạo rác ở chỗ chạy lặp** · không tính lại thứ không đổi. Việc **phải đo trước mới được làm**: mọi tối ưu đánh đổi bằng độ khó đọc — chỉ ở hot path đã xác nhận (NT3); ngoài đó **chọn bản dễ đọc nhất**. Cách rẻ nhất thường là **làm phép tính biến mất**, không phải làm nó chạy nhanh hơn. → **§3.3** |
| 9 | **Hệ độc lập là mặc định** | Mỗi hệ phải trả lời được: *bê sang dự án sau thì phải sửa gì?* Mặc định là **không sửa gì** — hệ không gọi tên type của dự án, phụ thuộc đi **một chiều: dự án → framework**. **Hệ kết hợp** (dựng trên nhiều hệ khác nên không rời đi một mình được) là ngoại lệ phải gọi tên được lý do và chỉ ra đường tách. Tính mang đi được đến từ **chiều phụ thuộc và chỗ đặt file**, không từ việc thêm lớp trừu tượng — nên **không phải phòng xa** (NT3): quyết đúng từ đầu gần như miễn phí, sửa sau thì đập cả cây phụ thuộc (NT4). → **§3.2** |

## 1.4 Chứng minh là xong — lấy gì làm bằng chứng

| # | Nguyên tắc | Nội dung |
|---|---|---|
| 10 | **Bằng chứng, không khẳng định suông** | Mọi "tại sao" kèm phép kiểm **tái lập được**; mọi công thức chốt phải **kiểm mốc**; code phải đối chiếu với công thức trước khi chốt. Không viết "đã đúng", "đã tối ưu" mà thiếu mốc, số đo, hoặc phép thử người đọc tự chạy lại được. **Bằng chứng phải cùng loại với tiêu chí** — có loại nghiệm thu agent không tự làm được. → **§2.8** |
| 11 | **Game feel là tiêu chí nghiệm thu** | Hệ này phục vụ **cảm giác chơi**, không phục vụ độ chính xác vật lý. Công thức "sai sách" mà chơi đã tay thì **đúng**; công thức chuẩn sách mà chơi vô hồn thì **sai**. Toán là *công cụ để đạt cảm giác*, không phải mục tiêu. Nghiệm thu bằng **chơi thử**, và chỉ developer chơi được (§2.8). |

## 1.5 Một con số có tư cách gì

| # | Nguyên tắc | Nội dung |
|---|---|---|
| 12 | **Config chưa chốt thì không phải mốc** | Số mặc định trong asset, bảng cấu hình, hay cấu hình từ xa đều là **bản nháp của developer**, không phải hợp đồng — chỉ thành mốc khi developer nói nó đã chốt. Khi đổi đơn vị, đổi trục, đổi công thức: **không tự ánh xạ từng giá trị cũ, không tự dựng đối chiếu trước–sau** để chứng minh "cảm giác không đổi" — vừa là công bảo vệ thứ chưa ai bảo là cần bảo vệ (NT3), vừa **đóng băng bản nháp thành chuẩn**. Việc phải làm: giữ **hành vi thuật toán** bất biến và chứng minh bằng mốc (NT10), rồi **hỏi** giá trị cũ là nháp hay là mốc (NT1). |
| 13 | **Chuẩn hoá thì bỏ điểm neo** | *Khi một con số phải so được giữa các ngữ cảnh khác cỡ.* Chuẩn hoá là **bỏ** điểm neo, không phải chọn điểm neo tốt hơn — còn phải hỏi "chia cho cái nào" là chưa xong. **Mẫu số là đại lượng gốc**, không phải đại lượng đã bị một núm khác nhân vào: chia cho bản đã nhân biến núm đó thành hệ số âm thầm lên mọi thứ, và giết mất dòng chẩn đoán nói *ai* đang kẹp. Trả giá bằng một vùng chết ở đầu thang — vùng chết **giống nhau ở mọi ngữ cảnh** rẻ hơn vùng chết đổi theo ngữ cảnh. Giá trị cũ đặt khi **chưa có trục chuẩn**, nên quy đổi máy móc là phỏng đoán đội lốt migration (NT12). |

## 1.6 Sau khi đã đổi — cái gì còn được nhắc lại

| # | Nguyên tắc | Nội dung |
|---|---|---|
| 14 | **Đổi rồi thì chỉ còn một hệ** | Quyết định đã chốt thì code và tài liệu nói **hoàn toàn bằng hệ mới**: không "trước đây là…", không ghi song song hai đơn vị, không giữ tên hay tham số cũ làm cầu. Người đọc sau **không có hệ cũ trong đầu** — mỗi dòng nhắc lại nó bắt họ nạp một hệ đã chết để hiểu một hệ đang sống. Chỉ giữ vết hệ cũ khi **gọi tên được người dùng thật của vết đó**: payload cũ còn ngoài đời phải deserialize được (§3.7) · developer đang đọc log của build cũ · developer yêu cầu. Nơi ghi lịch sử là commit và changelog. |

## 1.7 Viết ra cho người đọc sau

| # | Nguyên tắc | Nội dung |
|---|---|---|
| 15 | **Mật độ, trình tự, và bản chất** | Chọn dạng trình bày có mật độ cao nhất **cho loại nội dung đó**: bảng cho so sánh, diagram cho luồng, công thức cho quan hệ định lượng, một câu văn cho trực giác. Không kể lể, không diễn giải lại thứ vừa nói. Dẫn theo mạch **dễ→khó, tổng quan→chi tiết, vấn đề→giải pháp**; mỗi bước chỉ dùng khái niệm đã nêu trước; đánh số khi là quy trình. Mỗi khái niệm phải **có** phần cơ chế, "tại sao", trade-off — nhưng độ sâu **cân theo độ khó thật**: suy ra trong 1–2 bước thì kết quả kèm kiểm mốc là đủ; dẫn định luật nền để biện minh một phép nhân là over-engineering. **Mặc định một khẳng định = một câu lý do**; phải viết cả đoạn mới xong là dấu hiệu chưa hiểu đủ để nén, hoặc đang biện minh cho thứ không cần có. Áp cho **tài liệu đầu ra** — **đối thoại có luật riêng ở §2.3**. |

## 1.8 Ranh giới dễ nhầm

Bốn cặp **trông như đá nhau** nhưng không phải; mỗi cặp có một câu hỏi phân xử.

| Hai chỗ trông như đá nhau | Câu hỏi phân xử |
|---|---|
| **Hiệu năng là ưu tiên hàng đầu** (NT8) ↔ **chỉ tối ưu hot path đã xác nhận** (NT3) | **Bản nhanh hơn có khó đọc hơn không?** Không khó hơn — chọn đúng nhịp, không cấp phát trong vòng lặp, không tính lại thứ không đổi — thì **làm luôn**, không cần đo, vì không có gì để đánh đổi. Khó hơn — thêm một tầng cache, viết tay một vòng lặp, bẻ cấu trúc dữ liệu vì tốc độ — thì phải có **chỗ đo và số trước–sau**. |
| **Hạn chế composite** (NT6) ↔ **`S` và `I` của SOLID** (§3.1) | **Tách theo cái gì?** Theo **lý do thay đổi** — hai phần sẽ bị sửa vì hai nguyên nhân khác nhau — thì đó là `S`, tách đúng. Theo "cho gọn mắt", "cho đúng pattern", "mỗi việc một component" thì đó là xé vụn: gộp lại. |
| **Hệ phải mang đi được** (NT9) ↔ **không phòng xa** (NT3) | **Thứ phải thêm là gì?** Chỉ là **chiều phụ thuộc** và **chỗ đặt file** thì làm ngay, gần như miễn phí. Là một interface, một lớp adapter, một tham số cho người dùng **chưa có** thì đó là phòng xa — bỏ (§2.4). |
| **§2.1 gộp câu hỏi thành 1–2 lượt** ↔ **§2.6 hỏi ở từng câu trả lời** | Hai loại câu hỏi khác nhau, **không cộng dồn**. §2.1 hỏi để **lấy ngữ cảnh trước khi làm** — gộp lượt. §2.6 hỏi để **chốt một nguyên tắc vào file này** — hiếm, chỉ khi câu trả lời chứa tư tưởng lặp lại được. |

---

# §2 — Quy trình làm việc với AI

## 2.1 Trước khi làm — phỏng vấn ngữ cảnh và ranh giới tự quyết

Agent **không** suy đoán ngữ cảnh rồi bắt tay làm: đoán **thừa** là hàm không caller nào gọi, tối ưu
chỗ không phải hot path; đoán **thiếu** là chữ ký chặn hướng dùng thật, phải đập đi làm lại (NT3). Hỏi
5 nhóm dưới, **gộp thành 1–2 lượt**; biết chắc nhóm nào thì **nêu giả định để developer xác nhận**.
**Chưa có câu trả lời thì chưa làm.**

| Nhóm | Với tài liệu | Với code hoặc plan |
|---|---|---|
| **Ai dùng đầu ra** | ai đọc, đọc để làm gì, biết sẵn tới đâu | ai gọi, gọi ở đâu, có caller thật **ngay bây giờ** chưa |
| **Mục tiêu** | đọc xong phải **làm được gì** | phải đạt **cảm giác hoặc hành vi** gì, nghiệm thu bằng gì (§2.8) |
| **Ngân sách** | độ sâu và độ dài nào là đủ | **nhịp** của nó là gì (NT8), có phải hot path không, platform nào |
| **Ranh giới** | phần nào giải ở đây, phần nào trỏ sang tài liệu khác | phần nào của class này, phần nào của hệ khác; hệ này **độc lập hay kết hợp** (NT9) |
| **Hướng phát triển thật** | hệ sắp đổi gì khiến tài liệu phải sửa | **chắc chắn** sắp cần thêm gì; cái gì *có thể* cần nhưng chưa chắc (NT4) |

**Giả định chỉ vá khe hở nhỏ phát hiện giữa chừng, không thay cho phỏng vấn** — đủ cả ba: khe hở
**nhỏ** · đầu ra **đảo ngược được** · nhãn ghi **tại chỗ dùng**. **Thao tác khó đảo ngược thì luôn hỏi
trước khi chạy**: ghi đè hoặc xoá dữ liệu đã author (file level, save, asset) · migration đổi schema
hoặc wire format · thứ nằm ngoài version control.

**Mở rộng phạm vi thì nêu ra kèm giá phải trả, không tự làm** — chỉ khi phạm vi hiện tại **chặn** khả
năng phát triển. Thứ developer tự nêu thì hỏi lại **một lần** để cân đắt–lợi, rồi theo developer.

## 2.2 Đọc code rồi phải đối chiếu lại với developer

**Code cho biết cái gì đang chạy, không cho biết vì sao nó được viết như vậy** — khoảng lệch đó sinh
ra cả bug lẫn hiểu sai. **Không mặc định developer nắm rõ từng ngóc ngách hệ thống của mình**: code có
thể do người khác viết, viết từ lâu, hoặc đã trôi khỏi thiết kế ban đầu. Với **mỗi phát hiện ảnh hưởng
đến quyết định đang bàn**, nêu đủ ba phần:

1. **Tôi thấy gì** — kèm đường dẫn và dòng, để developer mở ra kiểm được.
2. **Tôi hiểu ý định là gì** — phát biểu lại bằng lời mình, rồi hỏi thẳng: có khớp thiết kế ban đầu không?
3. **Developer đã biết chỗ này chưa** — nếu chưa, là chủ ý hay chỗ đã trôi cần xử lý?

Kể lại toàn bộ code vừa đọc là bắt developer đọc tường thuật thay vì trả lời một câu hỏi (NT15, NT3).

## 2.3 Văn phong khi đối thoại

Áp cho mọi lượt đối thoại, **chặt nhất khi brainstorm** (§2.6) — đó là lúc developer phải phân tích
nhiều nhất trên chữ của agent. Hai bờ đều là bờ vực (NT3):

- **Sàn — đủ dữ kiện để developer tự phân tích lại, không phải chỉ đủ để tin.** Nêu *cái đang có*,
  *cái sẽ đổi*, *cái đánh đổi*, và số hoặc đường dẫn để kiểm. Thiếu dữ kiện thì developer chỉ còn hai
  lựa chọn là tin hoặc bỏ, và cả hai đều không phải phân tích. Đây là chỗ **mật độ của NT15 không áp**:
  nén trong đối thoại chỉ tạo thêm một vòng hỏi lại. **Ranh giới — sàn là dữ kiện, không phải dẫn
  giải:** bước hiển nhiên đúng, hoặc thứ đã chốt ở lượt trước, thì **một hai câu nhắc lại là đủ**; dẫn
  lại từ đầu bắt developer đọc hết mới biết không có gì mới.
- **Trần — đúng phần cần cho quyết định đang chờ, không hơn.** Trần phải tự canh vì hai bờ hỏng không
  đối xứng: thiếu thì developer hỏi lại và lộ ra ngay; thừa thì chỉ lộ ra dưới dạng quyết định ra chậm
  hơn. Phép kiểm: *dòng này thay đổi điều gì trong quyết định developer đang phải ra?* Không đổi gì
  thì cắt, kể cả khi nó đúng.

Cách viết:

- Rõ ràng, rành mạch. Một câu nói một ý. **Ngắn và dễ hiểu đi trước hoa mỹ** — bỏ câu chuyển tiếp, bỏ
  lời dẫn, bỏ tóm tắt lại thứ vừa nói.
- **Hạn chế viết tắt và thuật ngữ khó** — nói được bằng lời thường thì nói bằng lời thường; buộc dùng
  thì giải nghĩa ngắn ngay lần đầu. Thuật ngữ agent quen dùng chưa chắc developer quen dùng.
- **Gọi khái niệm đúng tên nó có trong hệ** — khớp nguyên văn tên trong code và tài liệu: không rút
  gọn, không đặt tên riêng lúc nói. Bản rút gọn thường đụng một khái niệm khác đang có (§3.7).
- Câu hỏi phải trả lời được **mà không cần mở code ra đọc lại** — thiếu ngữ cảnh gì thì cung cấp kèm.
- **Kết luận trước, dẫn giải sau** — developer đủ tin thì dừng đọc được. Đối thoại **cố ý** đi khác
  trình tự của NT15: tài liệu có người đọc tuần tự, đối thoại thì không.
- **Bày phương án, rồi chốt một cái.** Liệt kê các phương án đã cân — mỗi cái một dòng: *nó là gì · được
  gì · mất gì*. Rồi **chốt một phương án và nói vì sao nó thắng**. Tiêu chí chốt là **hợp tư tưởng
  trong file này nhất** — vừa đủ (NT3), đọc một mạch (NT6), khai được nhịp (NT8), mang đi được (NT9),
  mở đường mai (NT4) — không phải "dễ làm nhất" hay "nhiều tính năng nhất"; phương án thắng vì lý do
  nào thì gọi tên lý do đó ra. Bỏ phần chốt là đẩy việc khó nhất về phía developer; bỏ phần bày phương án là lấy mất dữ kiện để developer bác lại. Chốt sai mà nói rõ vì sao thì sửa
  trong một câu.
- **Ranh giới của luật ngay trên** — quyết định thuộc developer thì **thu hẹp lựa chọn và nêu giá từng
  cái, không chốt hộ**: phạm vi, ba thứ ở ranh giới cứng của NT2, thứ developer đã quyết rồi (§5.4),
  và mọi ca NT1 nói là phải hỏi.

## 2.4 Tái sử dụng, rồi chốt phạm vi

**Trước câu hỏi "có cần không" là câu hỏi "đã có chưa"** (NT5). Khảo sát **trong phạm vi logic liên
quan đến task** — hệ đang chạm, module nó gọi tới, Utilities khi nghi có helper sẵn. Nói rõ đã khảo
sát đâu và kết luận gì (NT10).

| Cái có sẵn | Xử lý |
|---|---|
| Đáp ứng được yêu cầu | dùng lại, không viết bản thứ hai |
| Gần đúng nhưng thiếu | mở rộng nó nếu **thêm được mà không sửa cái cũ**; không được thì viết mới |
| Không có, hoặc phải **bẻ cong bài toán** cho vừa nó | **viết mới** — tái sử dụng không phải lý do để làm sai bài toán |

**Bê một khuôn có sẵn thì soi kỹ nhất đúng những chỗ khuôn cũ *cố ý* làm.** Phần cố ý trông như phần
đã cân nhắc kỹ nên bê được, nhưng cân nhắc kỹ nghĩa là nó **bám chặt vào ngữ cảnh cũ**. Với mỗi quyết
định cố ý, hỏi lại **lý do gốc có còn đúng ở bài toán này không**: còn thì giữ, hết thì đảo và ghi lý
do ngay tại đó. *Đã sai một lần:* một khuôn cố ý serialize giá trị runtime để developer nhìn thấy
trong asset; bài toán mới lưu dữ liệu người chơi, bê nguyên là ghi tiến độ người chơi vào asset của
project rồi đưa vào version control.

Sau đó mới tới phạm vi. Mọi thứ định đưa vào — chương tài liệu, demo, hàm, tham số, guard, tối ưu, lớp
trừu tượng — qua cùng một luật: **có nhu cầu thật ngay bây giờ thì đưa vào**.

| Thứ định thêm | Nhu cầu thật là |
|---|---|
| Interface hoặc abstract | có **implementation thứ hai** |
| Tham số | có **call site truyền khác mặc định** |
| Guard, nhánh biên, hoặc `try/catch` | có **input thật chạm được biên** — hoặc **gọi được tên thứ ném ra** (§3.4) |
| Code canh — hoặc tự tạo — thứ dựng được lúc authoring | **không có nhu cầu nào cả**: đó là bước setup, viết vào tài liệu (§3.6) |
| Tối ưu làm code khó đọc hơn | **hot path đã xác nhận** bằng số đo (NT8) |
| Tách lớp, tách class, tách component | **trách nhiệm thật sự khác** — khác lý do thay đổi (NT6) |
| Chương, mục, demo | có người đọc cần nó để **làm được một việc cụ thể** |

Thứ chỉ "có thể cần sau" chia theo **giá của việc thêm sau**: **rẻ** (thêm mục hoặc hàm mới, không sửa
cái cũ) thì **để lại**, ghi một dòng ở mục "Mở rộng sau" · **đắt** (sửa chữ ký, đập cấu trúc, đảo
chiều phụ thuộc) thì làm ngay — chỗ **duy nhất** đáng phòng xa. **Tính mở rộng đến từ Open/Closed**,
không đến từ việc viết sẵn thứ chưa ai cần.

**Ranh giới của "không lặp" — tri thức, không phải hình dạng code.** Phép thử: *hai chỗ này có cùng lý
do thay đổi không?*

| Trả lời | Bản chất | Xử lý |
|---|---|---|
| Cùng lý do đổi | trùng lặp thật | gộp về một nguồn — sửa một lần, mọi nơi theo |
| Khác lý do đổi | **trùng lặp ngẫu nhiên** — giống hôm nay, phân kỳ ngày mai | **để lặp** — gộp là trói hai nghiệp vụ độc lập: khi một bên đổi, hàm chung mọc tham số và nhánh riêng cho từng caller (NT3) |
| Chưa trả lời chắc được | chưa hiểu bản chất sự lặp | **để lặp trước** — lặp rồi gộp sau thì rẻ, trừu tượng hoá sai thì mọi caller phải đập; rõ bản chất rồi (thường ở người dùng thứ hai, thứ ba) mới gộp |
| Mấp mé — giống nhiều nhưng hướng phát triển hai bên chưa rõ | cần ngữ cảnh ngoài code | **hỏi dev** (NT1): nêu phạm vi và hướng phát triển từng bên, được–mất của gộp và của để lặp |

Phép thử trả lời rõ thì agent tự quyết và nêu lý do (NT2); chỉ hàng mấp mé mới hỏi. Luật "để lặp"
**không** áp cho hai phép tính buộc khớp nhau ở runtime — phân xử ở NT5.

## 2.5 Ghi ngữ cảnh đã chốt vào đầu output

Plan thì đặt mục **"Ngữ cảnh đã chốt"** trước `§0`; tài liệu thì nêu ở phần mở đầu. Gồm: người dùng ·
mục tiêu · ranh giới · **những gì cố ý KHÔNG làm, kèm lý do** · hướng phát triển đã tính tới nhưng
chưa làm (NT4). Người đọc sau biết vì sao phạm vi dừng ở đó, không "bổ sung cho đủ".

## 2.6 Chưng cất tư tưởng khi brainstorm

Brainstorm là nơi tư tưởng thiết kế của developer lộ ra rõ nhất — nhưng lộ dưới dạng **quyết định cho
một bài toán cụ thể**, và trôi mất khi bài toán xong. File này chỉ lớn lên bằng cách giữ lại đúng
những khoảnh khắc đó. **Ở từng câu hỏi**, sau khi nhận câu trả lời, làm thêm ba bước:

1. **Khái quát hóa** — tách *tư tưởng* khỏi *quyết định riêng của bài toán này*: phát biểu lại thành
   nguyên tắc mang sang bài toán khác vẫn dùng được, kèm cái "vì sao", không chỉ ghi lại lựa chọn.
2. **Đối chiếu với chính file này** (NT5) — đã có thì thôi; là trường hợp riêng thì trỏ về; làm rõ
   thêm hoặc **mâu thuẫn** với nguyên tắc đã có thì nêu thẳng chỗ lệch để developer phân xử.
3. **Hỏi developer quyết** (NT1) — có thêm vào MY_SKILL không, và vào **tầng nào**? Developer chốt thì
   mới ghi, đúng cấu trúc và văn phong của file; từ chối thì bỏ, không ghi tạm đâu khác.

Chỉ khái quát khi câu trả lời **thật sự chứa tư tưởng** — một lựa chọn có "vì sao" mang tính nguyên
tắc, lặp lại được. Quyết định thuần bài toán (chọn hằng số, đặt tên, phạm vi một task) thì không.

## 2.7 Subagent — ngữ cảnh bơm từ orchestrator, không tự đọc lại

Mỗi subagent là một ngữ cảnh trắng: để nó "tự tìm hiểu" là nó đọc lại từ đầu MY_SKILL và tài liệu hệ,
và thuế đọc đó **nhân theo số subagent** — đã sai một lần: 108 subagent tự đọc lại tài liệu nền đốt
~94M token trong một ngày, gấp ~14 lần nhịp thường.

- Prompt cho subagent **tự chứa như một task của Plan** (§5.3): trích đoạn tài liệu cần cho task,
  đường dẫn file sẽ chạm, tiêu chí nghiệm thu. Thiếu ngữ cảnh thì subagent báo về để orchestrator bổ
  sung, không tự đi đọc tài liệu nền.
- Ngoại lệ là **code**: subagent tự đọc code nó sẽ sửa — code đổi liên tục, trích đoạn code trong
  prompt là bản chết (§5.4 "không viết theo trí nhớ" áp cho cả prompt).

## 2.8 Nghiệm thu — chọn phép kiểm theo loại tiêu chí

NT10 đòi bằng chứng; mục này nói bằng chứng **nào** hợp tiêu chí nào. Chọn sai thì lãng phí cả hai
phía: dựng lệnh cho thứ chỉ chơi thử mới biết, hoặc đẩy về tay developer thứ máy quét vài giây là xong.

| Tiêu chí cần chứng minh | Bằng chứng đúng loại | Ai chạy |
|---|---|---|
| Thuật toán tất định, công thức, parser, serialize | phép kiểm chạy được (§4.3) | agent |
| Tên, tham chiếu, đồng bộ tài liệu | grep quét (§3.7, §5) | agent |
| Hiệu năng | số trước–sau tại chỗ đo (§3.3) | agent |
| Đúng–sai xác định được, mà người làm tay thì chậm, sót, hoặc không thấy được | vét cạn theo bảng dưới | agent, **tự đề xuất** |
| Cảm giác chơi, nhịp, độ khó, hình ảnh | **chơi thử** | **developer** |

**Cảm giác chơi — DỪNG và giao, không dựng proxy.** Ép nó về một lệnh chạy được là **đo thứ dễ đo
thay cho thứ cần biết**: tốn công, cho cảm giác an toàn giả, mà developer vẫn phải chơi lại từ đầu.
Báo thẳng *"phần này chưa nghiệm thu được, cần chơi thử"*, kèm **kịch bản chơi thử**: *vào đâu* (level
nào, cần bật cờ hoặc dữ liệu gì trước) · *làm gì* (chuỗi thao tác **ngắn nhất** tái lập được) · *nhìn
cái gì* (hiện tượng cụ thể, không phải "xem có ổn không") · *khác trước ra sao* · *dấu hiệu hỏng*.

**Đúng–sai xác định được — vét cạn, không đẩy về tay.** Điều kiện là **cả hai**: có đáp án đúng–sai
xác định được, **và** ít nhất một dấu hiệu trong bảng. Đủ thì agent làm và chạy, kể cả khi chưa được
yêu cầu.

| Máy hơn người ở | Dấu hiệu nhận ra | Người làm tay hỏng ở đâu |
|---|---|---|
| **Sức** | không gian đầu vào lớn · phải lặp lại nhiều lần | chậm, và sót vì mỏi |
| **Thiên kiến** | trường hợp biên khó nghĩ ra hết | chỉ thử được thứ mình nghĩ ra, mà chỗ hỏng nằm đúng ở chỗ không ai nghĩ tới |
| **Tầm nhìn** | phải chứng minh **sự vắng mặt**: không còn tham chiếu, không còn caller, không sót tên cũ · trạng thái nội bộ sai trong khi màn hình vẫn đúng (§3.4) · thứ chỉ lộ sau hàng nghìn vòng: rò rỉ, phình dần, pool không trả về | mắt không thấy được thứ *không có*; chơi thử thấy màn hình đúng là tin đã đúng |
| **Nhất quán chéo** | nhiều bản buộc phải khớp nhau mà không suy từ một nguồn được (§3.4) · hành vi trước–sau một lần refactor phải trùng (ranh giới cứng NT2) | phải mở nhiều nguồn cạnh nhau so từng dòng — sót nhiều nhất |

Riêng nhánh **thiên kiến**, phần đắt giá là **liệt kê biên có hệ thống trước khi chạy**: rỗng · đúng
một phần tử · chạm giới hạn trên và dưới · trùng nhau · ngoài dải · thứ tự đảo · hai sự kiện cùng lúc ·
frame đầu tiên · đối tượng bị huỷ giữa chừng. Không dấu hiệu nào thì đọc code là xong (NT3). Cái neo
khi phân vân: **công sức đắt nhất trong nghiệm thu là của developer**.

---

# §3 — Thiết kế code

**Ba ưu tiên** §3.1 hình dạng và bậc cấu trúc · §3.2 module và tính mang đi được · §3.3 hiệu năng —
**vận hành** §3.4 bất biến · §3.5 async và tài nguyên · §3.6 editor-first — **luật ngang mọi code**
§3.7 naming · §3.8 dữ liệu import/export — **tool** §3.9.

## 3.1 Hình dạng — bậc cấu trúc, SOLID, và giới hạn của composite

| | Nội dung |
|---|---|
| **S** | Một class là một responsibility. Tách khi class có nhiều hơn một **lý do thay đổi** — không tách theo "cho gọn mắt" (NT6, §1.8). |
| **O** | Extend, don't modify. Không sửa trực tiếp class đang chạy ổn định — mở rộng bằng cơ chế phù hợp với bài toán. |
| **L** | Subtype thay thế được base mà không break behavior, không side-effect lạ. |
| **I** | Interface nhỏ, tách theo consumer. Không ép client phụ thuộc method nó không dùng. |
| **D** | Depend on abstractions. **Consumer không tự `new` thứ nó phụ thuộc** — nó nhận vào. (Factory, pool, container thì đương nhiên phải `new`.) `D` **không đòi interface**: nhận vào một class cụ thể vẫn là "nhận vào". |

> **Nền tảng** — DI runtime mặc định là InitArgs (`Sisus.Init`): `[Service(typeof(T))]` để đăng ký,
> `MonoBehaviour<TDep>` + `Init(TDep)` để nhận. Editor và tooling không bắt buộc dùng InitArgs —
> constructor injection hoặc static factory ở đó là hợp lệ.

**Bậc cấu trúc leo từ dưới lên, mỗi bậc chỉ leo khi bậc dưới không còn đạt** (NT6). SOLID phục vụ
**người đọc sau**: chia thiếu và chia thừa đều sai ở cùng một chỗ là **chi phí đọc**.

| Bậc | Đủ dùng khi | Leo lên bậc trên khi |
|---|---|---|
| **Logic tại chỗ** | chỉ chạy ở một nơi, đọc một mạch là hiểu hết | có **người gọi thứ hai**, hoặc một ý không còn nhìn hết trong một màn hình |
| **Hàm tách riêng** | đặt được tên nói đúng mục đích (§3.7); không giữ state giữa các lần gọi | phát sinh **state phải giữ**, hoặc một cụm hàm cùng thao tác trên một nhóm dữ liệu |
| **Class hoặc struct** | có **trách nhiệm gọi được tên** và state của riêng nó (`S`); chọn `struct` hay `class` theo §3.3 | có **implementation thứ hai** đang có thật (§2.4) |
| **Delegate làm tham số** | thân thuật toán **giống hệt nhau** ở mọi biến thể, chỉ khác **một thao tác** gọi được tên, và biến thể **không giữ state riêng**. Khai `static readonly Func<…>` với lambda `static`: bắt biến ngoài thành **lỗi biên dịch** thay vì rác GC âm thầm (NT8) | biến thể cần **state riêng**, hoặc cần **hơn một thao tác** đi cùng nhau — lúc đó nó đã là interface |
| **Interface hoặc abstract** | §2.4 — implementation thứ hai **đang có thật**, không phải sắp có | — |

**`O` không phải lý do để leo bậc.** `O` cấm **đổi hành vi đường cũ**, không cấm **thêm đường mới vào
chính class đó**: thêm method hoặc tham số mới mà đường cũ không đụng là đã đạt `O` → phải đổi hành vi
đường cũ nghĩa là **trách nhiệm mới**, tách class → chỉ khi có implementation thứ hai đang có thật mới
dựng interface. Phần lớn trường hợp đạt `O` **không cần abstraction nào**.

**Ba hình dạng composite — viết lý do ra tại chỗ trước khi dùng.** Không cấm, nhưng mặc định là không,
và mỗi lần dùng phải gọi tên được thứ bậc thấp hơn không làm được (NT6).

| Hình dạng | Giá phải trả | Chỉ dùng khi |
|---|---|---|
| **Cây composite** — cha và lá cùng interface, duyệt đệ quy | hành vi không nằm ở đâu cả, phải chạy mới biết; mỗi nút là một lần dispatch ảo và một lần con trỏ nhảy, đặt trong nhịp mỗi frame là trả giá theo số nút (NT8) | **cấu trúc lồng nhau là của dữ liệu thật** — độ sâu do người dùng hoặc dữ liệu tạo ra, không do người viết chọn cho đẹp |
| **Một tính năng xé thành nhiều MonoBehaviour** | thứ tự `Awake`/`Update` giữa các mảnh **không định trước**, wire thiếu chỉ lộ lúc chạy, và mỗi mảnh là một lần engine gọi message | các mảnh **thật sự lắp lẫn được** giữa nhiều prefab khác nhau, và **tổ hợp đó đang tồn tại thật** — không phải "mỗi việc một component cho sạch" |
| **Hệ dựng chồng lên nhiều hệ khác** (hệ kết hợp) | không rời đi một mình được — mất tính mang sang dự án khác (NT9) | không tách nổi thành các hệ độc lập cộng một lớp nối mỏng; và khi đó vẫn phải chỉ ra đường tách (§3.2) |

**Thước đo là phạm vi bài
toán, không phải số khái niệm nghĩ ra được** — cùng một cách chia có thể đúng ở hệ nhiều người chạm và
thừa ở một tính năng cục bộ. Hỏi *"người đọc sau phải mở bao nhiêu file để lần hết một luồng?"* trước
khi tách; phân vân thì áp kiểm nhanh của NT3 — gộp xuống bậc dưới thì hỏng ở đâu?

**Bậc delegate giữ bất biến của vòng lặp ở đúng một bản.** Khi n biến thể dùng chung một vòng lặp, thứ
chỉ được phép có **một bản** là bất biến của vòng lặp — thứ tự chạy, phát tiến độ, xử lý lỗi, điểm
thoát khi bị huỷ — chứ không phải thân biến thể. Nhân vòng lặp ra n bản, hoặc cắm `if` biến thể vào
giữa, là nhân bất biến ra n bản (NT5).

**Nhiều host chung một nhịp: thân về nơi giữ bảo đảm, đời sống ở lại host.** Khi n host cùng chạy một
vòng lặp trên một hệ, chia theo *cái mỗi bên có mà bên kia không có*. **Thân vòng lặp và con số cấu
hình** về nơi giữ bảo đảm mà nhịp đó phục vụ — nhịp là một phần của bảo đảm, và con số nằm cạnh dữ
liệu nó chi phối thì đọc một chỗ ra hết. **Token** ở lại host, vì host là thứ **có đời sống**; chiều
này không đảo được — một `ScriptableObject` hay một plain class không có mốc kết thúc tin được, nên
không được tự mở `CancellationTokenSource`: không ai đóng nó, và ở Editor tắt domain reload thì mỗi
lần Play là một loop nữa xếp lên loop cũ. Loop nhận token của một pha ngắn hơn đời nó thì chết im
lặng — hình dạng thứ ba ở §3.4.

> **Nền tảng** — thứ tự region trong một class là **cố định**, để người mở một file lạ biết trước tìm
> gì ở đâu: `Unity Callbacks` trên cùng · `Properties` · `API` (thứ người ngoài gọi) · `Class Methods`
> (thân private) · `DI` **cuối class**, gói field nhận vào cùng `Init`. Trình tự đi từ **vòng đời →
> mặt ngoài → thân**; phụ thuộc xuống cuối vì đó là thứ hỏi sau cùng.

**Sổ tay** — hình dạng file và class đang dùng trong bộ này:

| Chỗ | Cách làm |
|---|---|
| Code chỉ có ở Editor | file `*.Editor.cs` khai `partial` của cùng class, **bên trong vẫn** `#if UNITY_EDITOR`. File runtime không bị khối `#if` cắt ngang, và nút Editor đọc thẳng private member của class |
| Nhận phụ thuộc | `MonoBehaviour<T>` cho component thường · `IInitializable<T>` khi class đã kế thừa base khác · không service locator bên trong hệ |
| Dựng host | hệ **không** tự `new GameObject` — host là component kéo tay vào scene, chu kỳ và collection đọc được trong Inspector (§3.6) |
| Log | mọi dòng mở bằng `[TênHệ]: ` để filter console theo hệ; kèm `this` làm context object để bấm vào ra đúng asset |
| Hàm một biểu thức | expression-bodied (`=>`), kể cả method `void` |
| Ẩn method của contract | mặc định `public` — tin người dùng hệ. Chuyển sang explicit interface implementation **đúng những method mà gọi sai gây mất dữ liệu im lặng**, không phải mọi method chỉ dùng nội bộ |

## 3.2 Module — độc lập trước, kết hợp là ngoại lệ

Ranh giới hệ thống phải **nhìn thấy được** trong cấu trúc dự án, không chỉ trong đầu người viết; mỗi
hệ có một trách nhiệm gọi được tên. **Module không tham chiếu trực tiếp implementation của nhau** —
cơ chế trung gian chọn theo bài toán (interface, event bus, ScriptableObject channel, dữ liệu thuần),
miễn đạt được: đổi implementation một bên mà bên kia không phải sửa.

**Phân tầng theo mức phụ thuộc, quyết ngay từ đầu** (NT9): hệ **độc lập** (bê sang dự án khác được) so
với hệ **kết hợp** (dựng trên nhiều hệ độc lập). **Utilities** là static và universal — không phụ
thuộc hệ thống nào.

**Ranh giới giữa framework dùng chung và dự án dùng nó đi đúng một chiều: dự án → framework.**
Framework không gọi tên type nào của dự án — và **cơ chế mở rộng nó cung cấp cũng không được ép dự án
đẩy domain của mình sang phía framework**. Phép kiểm khi chọn cơ chế: *nửa mà dự án viết có nằm trong
ranh giới biên dịch của dự án không?* Không nằm thì mọi type nửa đó **gọi tên** cũng bị kéo theo, lan
tiếp theo cả chuỗi phụ thuộc — và framework mất tính bê-sang-dự-án-khác dù thư mục file vẫn đúng chỗ.

> **Nền tảng** — với C# và Unity thì đáp án đã chốt:
>
> | Cơ chế mở rộng | Kết quả |
> |---|---|
> | `partial class` khai ở framework, nửa kia ở dự án | **Hỏng.** Mọi phần của một `partial` phải cùng một assembly, nên nửa của dự án buộc phải vào assembly framework bằng `.asmref` — và type nó gọi tên phải tra được từ assembly đó. Type của dự án thì không, vì thêm chiều ngược lại là **circular reference** và Unity từ chối biên dịch. Kết cục: domain nằm ở assembly framework |
> | `abstract class` ở framework, dự án khai subclass | **Đúng.** Subclass là type của dự án và nó chỉ gọi tên xuống phía framework. Phần framework tự chạy được một mình biến mất — đó là đặc điểm, không phải thiếu sót: tự chạy được nghĩa là nó đang mang một mẩu domain |
>
> Ba thứ đi kèm khi chuyển sang kế thừa, mỗi thứ một bẫy hỏng-im-lặng:
>
> | Thứ | Bẫy |
> |---|---|
> | Attribute khai `Inherited = false` — `[Service]` của InitArgs là một | Khai ở base **không** tới được subclass, nên dự án phải tự khai. Quên là không đăng ký được gì, lỗi nổ ở lần chạm đầu tiên |
> | `GetType().GetFields(NonPublic \| Instance)` | Thấy private field của subclass vì nó đọc type lúc chạy, nhưng **không** thấy private field khai trên chính base. Field khai nhầm ở base bị bỏ qua **không một dòng lỗi nào** |
> | Magic method của Unity (`OnDestroy`, `Awake`…) | Subclass đặt trùng tên là che hẳn bản của base, phần dọn dẹp của base im lặng không chạy. Khai `protected virtual` để `override` là đường duy nhất |
>
> Ở tầng interface: **contract của framework không được derive interface generic mang thành viên
> static** — ở bộ này là `IService<T>`, cái cho đọc `IFoo.Service`. Contract mà derive nó thì interface
> của dự án — derive cả contract lẫn `IService<chính mình>` — thừa hưởng **hai** thành viên `Service`
> khác instantiation, và mọi lần đọc là **CS0229 Ambiguity**: lỗi biên dịch, nổ đúng ở thứ khuôn này
> tồn tại để giữ. Nên chỉ interface của dự án derive nó; code cần contract thì đọc qua dạng đóng tường
> minh `IService<Contract>.Service`.

**Chức năng có người dùng thứ hai thì đề xuất nâng thành tái sử dụng được** (NT5) — đừng copy sang chỗ
mới. Cách nâng tuỳ bản chất: hàm thuần không giữ state → Utilities hoặc Helper static · có state hoặc
sẽ có nhiều biến thể → interface rồi tách implementation · chỉ khác một giá trị → thêm tham số. Đặt ở
**tầng thấp nhất mà cả hai người dùng đều với tới được**, không thấp hơn (NT3). Đây là **đề xuất,
không tự làm**: nâng một chức năng là đổi ranh giới trách nhiệm, cần developer quyết (NT1).

## 3.3 Hiệu năng runtime

Luật ở NT8; mục này là cách đạt. **Nghiệm thu:** chỉ ra được **chỗ đo** và **số trước–sau** (NT10).

Trước khi cache hay tối ưu, hỏi ba câu theo thứ tự — trả lời "có" ở câu nào thì dừng ở đó: có thể
**không cần tính** nó không · tính **một lần lúc authoring** được không (§3.6) · đổi **cấu trúc dữ
liệu** để câu hỏi tự biến mất được không? Hết ba câu mới tới kỹ thuật.

**Sổ tay** — kỹ thuật đã dùng:

- *Giảm cấp phát:* pool thay `Instantiate`/`Destroy` lặp lại · pre-alloc capacity dự đoán trước · reuse
  buffer bằng `.Clear()` · grow-only buffer khi size dao động · `struct` cho data nhỏ ngắn hạn ·
  `ref` / `in` / `Span<T>` thay copy · `static readonly` thay `new` lặp · tránh LINQ, boxing, string
  concat trong hot path (cần thì `StringBuilder` hoặc cache sẵn) · **không closure capture** — lambda
  bắt biến ngoài thì cấp phát mỗi lần gọi; cache delegate thành `static readonly` hoặc field.
- *Giảm tính toán:* dirty flag · event-driven rebuild · lookup dictionary dựng trước · tách phần tĩnh
  tính một lần khỏi phần động tính incremental · precompute hằng nặng (`exp`, `sqrt`, `sincos`, phép
  chia) ngoài vòng lặp · đổi chia thành nhân · guard thoát sớm · `sqrMagnitude` thay `magnitude` khi
  chỉ so sánh khoảng cách.
- *Sửa list an toàn:* duyệt ngược khi xoá (`RemoveAt` cuối là O(1)) · hoặc deferred removal — đánh dấu
  rồi xử lý sau vòng lặp, không xoá khi đang iterate.

> **Nền tảng** — `Update`, polling loop và `OnGUI` bị gọi lại liên tục cho **cùng một state**. Việc
> nặng đặt trong đó là sai không cần bàn; nó thuộc về event handler, hoặc thuộc về lúc authoring. Mỗi
> `MonoBehaviour` có `Update` là một lần engine gọi qua ranh giới native — n mảnh của một tính năng là
> n lần (§3.1).

## 3.4 Bất biến — bảo vệ bằng cấu trúc, không bằng kỷ luật

Bất biến giữ bằng "mọi người nhớ làm đúng" sẽ vỡ ở đúng người thứ hai. Sắp xếp code sao cho cái sai
**không thể xảy ra**, thay vì dặn đừng sai:

| Luật | Nghĩa là |
|---|---|
| **Một sự thật = một chủ sở hữu** | mỗi dữ liệu có đúng một nơi giữ bản gốc; mọi cache phải chỉ ra được **ai dựng lại** và **khi nào** |
| **Cache mới bám vào bất biến ĐÃ CÓ** | dùng lại dirty flag / version counter đang có, không dựng bất biến thứ hai song song — mỗi bất biến thêm là một điều mọi code sau phải nhớ, quên là cache cũ nằm lại **âm thầm** |
| **Một cờ chỉ được tiêu thụ ở đúng MỘT nơi** | có nơi thứ hai thì nơi chạy sau không bao giờ thấy cờ bật — cache của nó đứng im, không có gì báo |
| **Hai phép tính buộc phải khớp thì suy từ MỘT nguồn** | đo–vẽ, vẽ–hit-test, điều kiện ẩn–hiện: cùng một hàm, hoặc cùng một biểu thức copy nguyên — không viết hai bản "giống nhau", kể cả khi công thức hiển nhiên. Hai bản sẽ lệch, và lệch kiểu nhìn-vẫn-đúng-bấm-thì-trượt. Ranh giới với luật "để lặp" của §2.4: xem NT5 |
| **Cửa hẹp là thân chung của cửa rộng** | hai đường làm gần cùng một việc (bản đầy đủ và bản giữ-lại-một-phần) thì bản hẹp gọi vào thân bản rộng — hai bên không thể lệch nhau |
| **Một bảo đảm phải phủ MỌI đường vào** | hệ tuyên bố "mất không quá X", "luôn hợp lệ", "luôn đúng thứ tự" thì **mọi** cửa ghi phải đi qua đúng chỗ tạo ra bảo đảm đó. Một cửa thứ hai đi vòng không làm bảo đảm sai — nó làm bảo đảm **chỉ còn đúng cho một nửa hệ**, trong khi tài liệu vẫn phát biểu nguyên câu; nửa kia hỏng im lặng |

**Phép kiểm của luật cuối — đếm cửa trước, đọc thân sau.** Đường phụ sinh ra sau, cho một cỡ dữ liệu
khác hoặc một tiện ích nhỏ, và người viết chúng không nghĩ mình đang chạm vào bảo đảm nào. Phải **liệt
kê mọi cửa ghi vào cùng một kho** — grep API ghi của kho đó là ra hết — rồi với từng cửa, hoặc chỉ ra
nó đi qua chỗ tạo bảo đảm, hoặc **viết ngay tại chỗ rằng cửa này không được bảo đảm**. Ép mọi cửa về
**một** thân là cách rẻ nhất giữ phép kiểm còn đúng về sau. Nghiệm thu thuộc nhánh **tầm nhìn** của
§2.8: mắt người không thấy được cửa mà mình không biết là có.

**Ngưỡng dưới — chỗ dừng, không phải chỗ nới.** Đích vẫn là **bản build không có ca null hay ca sai
nào**; bỏ guard chỉ là đường rẻ hơn tới đúng đích đó. **Mặc định là guard đầy đủ**, chỉ bỏ khi **gọi
tên được lượt kiểm Editor bắt nó**: thao tác nào, lộ ra ở đâu, người thứ hai mở project cũng thấy.
Không gọi tên được thì guard, kể cả khi "chắc là không xảy ra đâu". Gọi tên được thì **không sinh code
runtime canh nó** — guard cho ca không bao giờ xảy ra vẫn là một nhánh phải đọc, phải test, và phải
giữ đúng mãi mãi (NT3).

Câu hỏi phân xử — **cái sai đó lộ ra lúc nào?** Lộ ngay lúc authoring hoặc nổ ngay lần Play đầu ⇒ **để
nó nổ**: exception thô và `LogError` có nhãn đẹp lộ ngang nhau, đều chặn developer lại ngay; đổi cái
trước thành cái sau là trả phí **vĩnh viễn trong build** cho một lần đọc log dễ hơn. Lọt qua authoring
rồi **sai âm thầm** giữa gameplay ⇒ bất biến thật, quay lại bảng trên. **Editor không bắt chắc được**
thì guard đầy đủ: reference chỉ có lúc runtime · ô null ở một prefab variant hoặc một scene trong
nhiều scene · sai chỉ hiện ở một tổ hợp cấu hình · thứ thành null sau một lần `Destroy`. Dữ liệu từ
ngoài (import, server, save) luôn thuộc nhóm này — §3.8.

**Bỏ guard vì "developer sẽ setup" chỉ đúng khi thiếu setup LỘ RA — phải kiểm, không được giả định.**
Câu "để nó nổ" đứng được nhờ vế *nổ*. Mất vế đó, nó thành thiếu-setup-chạy-tiếp-sai-âm-thầm — đúng
thứ ngưỡng dưới tồn tại để chặn. Ba hình dạng nó **không** nổ, và cách trả từng cái về chỗ nổ:

| Hình dạng | Vì sao im lặng | Trả về chỗ nổ bằng |
|---|---|---|
| **Giá trị mặc định của kiểu là giá trị hợp lệ** — `float` 0, `bool` false, list rỗng, enum phần tử đầu | Ô trống trong Inspector **không phân biệt được** với ô người ta cố ý điền giá trị đó; không có gì để null-check | **Authoring, không phải guard runtime**: cho default ngay trong khai báo field, kẹp dải bằng `[Min]`/`[Range]`. Ô chưa ai chạm khi đó ra số dùng được, và số vô lý không gõ vào được |
| **Host bắt lỗi fail-open** — một vòng dispatch, một chain boot log rồi bỏ qua step lỗi | Thiếu wire thành **một dòng log lúc boot** rồi cả phiên chạy như bình thường nhưng thiếu hẳn một hệ; QA và build release không ai đọc dòng đó | Không phải thêm guard: hoặc hệ tự đứng được không cần host đó, hoặc thiếu nó phải hỏng ở **cửa mà người chơi chạm** |
| **Vòng lặp nhận sai token** — nhịp gắn vào token của một pha ngắn hơn đời của nó | Framework hủy token đúng như hợp đồng của nó, thư viện async coi hủy là bình thường nên **không log**; loop dừng, phần còn lại vẫn chạy như thường | Editor không bắt được, phải đọc code: mỗi loop chỉ ra được **token của nó là đời của ai**, và đời đó dài đúng bằng nhịp (§3.1) |

*Đã sai một lần, và là nguồn của hai hàng trên:* một hệ save có `[SerializeField] float intervalSeconds;`
không default — quên điền ra 0, thành flush cả kho xuống đĩa **mỗi frame**; cùng hệ đó gắn loop autosave
vào token của pha boot, mà runner refresh token mỗi lần load level — autosave chết từ level thứ hai.
Cả hai không một dòng log.

**`try/catch` đi qua đúng cửa đó.** Chỉ bọc khi **gọi được tên thứ ném ra**: API thật sự ném (I/O,
parse, network, reflection, dữ liệu từ ngoài — §3.8), hoặc code của người khác chạy trong vòng lặp
của mình (§3.5). Không gọi tên được thì bỏ — một khối `catch` cho ca không bao giờ xảy ra vẫn bắt
người đọc sau dừng lại đọc, và nó **nói dối** rằng chỗ này có rủi ro (NT3). Bắt rồi thì phải **làm gì
đó**: xử lý, hoặc log kèm ngữ cảnh rồi ném lại. Nuốt exception rồi chạy tiếp với trạng thái hỏng là
biến một lỗi tỏ thành lỗi âm thầm — đúng thứ bảng trên tồn tại để chặn. `catch (Exception)` trần chỉ
ở **biên trên cùng**: một vòng dispatch, một entry point của tool.

**Quy ước chỉ thay được guard khi nó viết ra ở chỗ người vi phạm đang nhìn** — Tooltip, XML doc của
chính API đó, dòng trong tài liệu module — không phải chỉ nói trong một lượt chat.

**Bug "sai âm thầm" sửa xong thì chưng cất thành một dòng bất biến trong tài liệu module**, dạng *"đã
sai một lần: [triệu chứng]"* — loại tri thức đắt nhất và không đọc ra được từ code (NT10, §5.4).

## 3.5 Async & tài nguyên

Tiêu chí: **hủy được** (việc dừng theo owner của nó) · **giải phóng được** (thứ giữ tài nguyên phải có
đường trả lại) · **cô lập được lỗi** (một callback lỗi không kéo cả hệ chết).

> **Nền tảng** — lựa chọn mặc định và ràng buộc đi kèm:
>
> | Nhu cầu | Dùng | Ràng buộc không bỏ được |
> |---|---|---|
> | Async | **UniTask**, không coroutine, không `Task` | propagate `CancellationToken` xuống toàn bộ chain — hủy an toàn khi MonoBehaviour bị destroy |
> | Load asset | **Addressables** qua `AssetReference` | không dùng string key (type-safe, không lỗi runtime do sai tên); giữ `AsyncOperationHandle` để `Release()` đúng lúc, không giữ là leak |
> | Data lớn | `NativeArray` / `NativeList` | khi cần truyền GPU hoặc Job System; `StructLayout(Sequential)` khi phải khớp layout GPU hoặc native |
> | Tài nguyên nặng | cache `RenderTexture`, `Texture2D`… | có đường dọn dẹp trong `OnDestroy()` |

**Sổ tay** — cô lập lỗi listener: `try/catch` quanh từng callback trong vòng dispatch. Đây là một
trong hai ca `try/catch` được phép; luật chọn lọc ở §3.4.

## 3.6 Editor-first

Thứ gì quyết được lúc authoring thì để lúc authoring quyết. Đang viết code chỉ để **tìm, nối, hoặc
gán** thứ vốn đã tồn tại lúc authoring thì code đó đặt sai chỗ. Dấu hiệu: `GetComponent` / `Find` /
`AddComponent` / `Resources.Load` để lấy thứ đã có trên prefab · hằng số tinh chỉnh cảm giác hardcode
· dựng hierarchy bằng code.

> **Nền tảng** — lý do gốc, không phải sở thích: dữ liệu serialize sửa được **không cần compile**, ai
> trong team cũng chỉnh được, và khi thiếu thì lộ ra ô trống trong Inspector chứ không nổ giữa gameplay.

**Sổ tay** — reference kéo thả vào `[SerializeField]` · component add sẵn trên prefab · số tinh chỉnh
phơi ra Inspector · preset thành ScriptableObject · wire sẵn trong prefab rồi `Instantiate`.

**Ngoại lệ tự nhiên** là thứ chưa tồn tại lúc authoring: object spawn runtime, số lượng động, dữ liệu
từ server — ngoại lệ nằm ở *thời điểm biết được*, không phải ở *độ tiện khi viết code*. Plan chạm scene
hoặc prefab thì mô tả thao tác Editor **như một bước thật**, không lặng lẽ thay bằng code.

**Điều kiện dựng hệ thì ghi vào tài liệu, không sinh code canh.** Tạo asset, dựng GameObject bắt
buộc, gán reference, đặt layer/tag, thêm scene vào build — người dựng làm **một lần** lúc authoring.
Viết thành một bước ở mục **"Trước khi chạy"** của tài liệu module (§5.1) thì tốn một dòng, đọc một
lần rồi thôi. Sinh code tự kiểm hoặc tự tạo thì tốn mãi mãi: một nhánh nằm trong build, phải đọc,
phải test, phải giữ đúng qua mọi lần refactor (NT3). **Tự tạo tệ hơn tự kiểm** — nó giấu mất việc
setup còn thiếu, và dựng nguồn sự thật thứ hai cạnh bản authoring (§3.4). Thiếu setup thì **để nó
nổ** ở lần Play đầu; đó đã là báo lỗi đủ rõ, và là đúng chỗ dừng của ngưỡng dưới ở §3.4 — nhưng phải kiểm
rằng nó nổ thật, ba hình dạng im lặng ở ngay dưới đó.

**Bước setup bằng tay là cách developer HIỂU hệ, không chỉ là đường rẻ hơn.** Đây là lý do thứ hai,
độc lập với chi phí: người tự kéo asset vào ô, tự bật component, tự đặt số thì giữ được trong đầu
**các mảnh rời của hệ và chỗ chúng nối vào nhau** — thứ duy nhất dùng được khi hệ hỏng lúc 2 giờ sáng.
Hệ tự wire để không ai phải biết gì thì đúng lúc cần, không ai biết gì. Nói ngắn: **hệ không gánh lỗi
quên setup của người dùng nó.** Đổi lại, hệ **nợ** người dùng một mục "Trước khi chạy" đủ để dựng
lại từ 0 (§5.1) — bỏ code canh mà không viết bước setup thì chỉ là đẩy việc, không phải Editor-first.

## 3.7 Naming — self-documenting code

- Tên method nói rõ **mục đích**: `EnsureMaterial()`, `SwapWriteBuffer()`, `SolveAnalytic()`. Tên vô
  nghĩa cần thay: `Process`, `Handle`, `DoWork`, `Update2`.
- Boolean đọc như một câu hỏi: `IsPickable`, `HasPendingInput`, `frameDataReady`.
- Code tự giải thích được thì comment **chỉ** nói **tại sao**, không nói **cái gì**.
- API public có XML doc; `<param>` cho mọi tham số có contract không hiển nhiên.
- **Comment, tooltip và XML doc là chỗ đọc nhanh, không phải chỗ giải thích tường tận** — nghĩa vụ
  giải thích cơ chế và trade-off (NT15) **chuyển sang tài liệu của hệ** (§5), không mất đi. Mức trần:
  **17–20 từ** cho comment và tooltip (vì sao dòng này tồn tại, hoặc bấm vào thì được gì) · **35 từ**
  cho XML `<summary>` · **15 từ** cho `<param>`, `<returns>` và mỗi thẻ còn lại. Đây là **trần, không
  phải chỉ tiêu**. Chạm trần là tín hiệu — hoặc code và UI chưa tự giải thích thì **sửa gốc trước khi
  viết thêm chữ**, hoặc phần đang viết vốn thuộc về tài liệu.
- **Một từ = một nghĩa trong toàn hệ thống** — một từ bắt đầu mang hai nghĩa thì đổi tên một bên ngay.
  Khái niệm không đặt nổi tên riêng thường là khái niệm chưa rõ.
- **Đổi tên là đổi cả hệ**: code, comment, chuỗi debug, và mọi tài liệu — cùng một lần làm, không sót
  tên cũ ở bất kỳ đâu (kiểm bằng grep). **Ranh giới:** khoá wire format và dữ liệu đã serialize (key
  JSON, tên field trong save, schema) là **hợp đồng với hệ khác** — không đổi theo, và không bị tính
  là "tên cũ còn sót".

## 3.8 Dữ liệu đi qua tool — import/export

File mà tool đọc–ghi là **của người dùng và của hệ khác**, không phải của tool:

| Luật | Nghĩa là |
|---|---|
| **Không bao giờ sửa giá trị dữ liệu import** | ngoài tầm hợp lệ thì **từ chối cả file**, hoặc **bỏ qua entry đó** kèm cảnh báo. Clamp là xuất ra bản lệch — mất dữ liệu thật mà không ai biết |
| **Bỏ entry hỏng, không bịa entry mới** | chèn dữ liệu để "sửa giúp" là thêm thứ không có trong file — tệ hơn thiếu |
| **Chỉ sở hữu phần mình hiểu** | export clone dữ liệu gốc rồi ghi đè đúng những khoá tool sở hữu; mọi khoá lạ **đi qua nguyên vẹn** — đó là dữ liệu của hệ khác mà tool chưa hỗ trợ, không phải rác |
| **Dữ liệu gốc đi theo đối tượng** | giữ tham chiếu bản gốc trên chính đối tượng, không tra lại theo toạ độ hay vị trí mảng lúc export — hai thứ đó đổi theo thao tác, tra theo chúng là gán dữ liệu của đối tượng này cho đối tượng **khác**, sai âm thầm |
| **Import và Export sống cạnh nhau** | hai chiều của cùng một cụm dữ liệu nằm trong một file — nằm cạnh nhau thì không trôi lệch nhau; nghiệm thu bằng round-trip (§4.3) |

## 3.9 Editor tool

Tiêu chí: **một tool là một đơn vị gói kín** · **thứ dùng chung sống một chỗ** (logic tái sử dụng được
thì chuyển ra, theo §3.2) · **thứ không đổi giữa các lần vẽ lại phải có sẵn**.

> **Nền tảng** — với IMGUI (`OnGUI`, `EditorWindow`, `PropertyDrawer`): một lần tương tác gây ra nhiều
> lần gọi `OnGUI` cho **cùng một state**, mỗi lần chạy lại toàn bộ hàm — nên thứ không đổi mà bị tạo
> lại trong đó là rác thuần. Một số thứ (`GUIStyle`, `GUIContent` có icon) chỉ tồn tại sau khi
> `GUI.skin` và `EditorGUIUtility` sẵn sàng, nên không khởi tạo được ở static initializer.

**Sổ tay** — *áp cho IMGUI*. UI Toolkit có mô hình repaint khác hẳn (cây phần tử tồn tại liên tục) nên
bảng này không áp; tiêu chí ở trên vẫn giữ.

| Cấp cache | Kỹ thuật | Khi nào dùng |
|---|---|---|
| Static eager | `static readonly` | giá trị bất biến: `Color`, `GUILayoutOption[]`, `GUIContent` chỉ có text |
| Static lazy + guard | init một lần trong `EnsureStyles()` | thứ cần `GUI.skin` hoặc `EditorGUIUtility` mới có |
| Instance lazy | null-check init ở cấp window | style riêng từng tool, không cần chia sẻ |
| Dirty-flag | chỉ rebuild khi dữ liệu đổi | layout options khi window resize |
| Event-phase | tính toán nặng chỉ ở `EventType.Layout` | filter, sort, format — `Repaint` dùng lại kết quả |

**Tool có người dùng thì UX là một phần của thiết kế**, không phải phần trang trí:

- **Vùng UI phải nói lên ranh giới** — thứ khác vai trò (ghi vào dữ liệu / chỉ đổi cách xem / dùng ở
  mọi lúc) không nằm chung một vùng. Đặt vùng mới thì hỏi *"người dùng đang nghĩ gì lúc đi tìm nó?"*.
  Ranh giới nào phải giải thích bằng một cột trong hướng dẫn là ranh giới người dùng sẽ nhầm.
- **Điều kiện vẽ và điều kiện bấm được suy từ một nguồn** (§3.4) — phá thì nút đang hiện mà bấm không có gì xảy ra.
- **Không giấu thứ có thật**: điều kiện vẽ là *"có dữ liệu"*, không phải *"tra được tài nguyên để
  vẽ"* — tra thiếu thì vẽ dạng báo lỗi kèm id, đừng để dữ liệu biến mất khỏi màn hình trong khi vẫn
  được xử lý và ghi ra file. Field chỉ-đọc vẫn phải hiện, khác kiểu với field sửa được.
- **Phép kiểm tính hợp lệ chạy khi được hỏi**, không chạy nền theo mỗi thay đổi: lúc đang dựng thì dữ
  liệu **luôn** chưa hợp lệ, cảnh báo nền hiện gần như toàn thời gian vào đúng lúc chưa thể sửa — đúng
  mà vô ích.

---

# §4 — Hệ toán học và vật lý

## 4.1 Khi nào dùng tới toán, và sâu tới đâu

Mục này chỉ mở ra khi **developer yêu cầu**, hoặc khi **agent thấy toán giải bài toán tốt hơn hẳn** —
trường hợp sau thì nêu để developer quyết, kèm cái được và cái mất, không tự đưa vào (NT1).

**Mặc định là không cần toán.** Phần lớn logic gameplay là trạng thái và luật rời rạc. Toán là một lớp
phức tạp, phải trả giá bằng nhu cầu thật như mọi lớp khác (NT3) — xét hết những cách rẻ hơn và dễ
chỉnh hơn trước đã: `AnimationCurve` hoặc bảng tra do designer chỉnh trong Inspector (NT7) · easing có
sẵn · lerp · máy trạng thái · một hằng số chọn bằng tay. **Dấu hiệu đang ép toán vào chỗ không cần:**
phải dẫn định luật nền để biện minh một phép nhân (NT15) · công thức chỉ có một call site và không
tham số nào thay đổi · designer không chỉnh được gì · kết quả thay bằng vài giá trị trong bảng là xong.

**Bờ vực còn lại cũng sai** (NT3): bài toán vốn liên tục và có ràng buộc — chuyển động phải dừng đúng
chỗ, va chạm, nội suy cần đạo hàm liên tục — mà né toán thì thành một đống hằng số tinh chỉnh không ai
hiểu, sửa chỗ này vỡ chỗ khác. Toán đúng chỗ làm code **ngắn hơn**.

**Đã cần toán thì sâu vừa đủ cho tính năng:** chọn mô hình đủ để chạy đúng và cho cảm giác đúng, không
chọn mô hình đúng nhất về vật lý — xấp xỉ là **mặc định** (NT11). Nhưng khi **thật sự** cần bản đầy đủ
thì **làm tử tế**: cắt nửa vời rồi bù bằng hằng số là cách sinh ra hệ sau này không ai dám sửa. Độ sâu
của **mô hình** theo đoạn này, độ sâu của **dẫn giải** theo NT15.

## 4.2 Cần thì phải cho hiểu sâu

Người đọc phải đạt ba thứ: **hiểu hiện tượng** đằng sau · **tin công thức là suy ra được**, không phải
phép màu · **kiểm lại được** bằng tay. Mạch dưới dành cho công thức **không hiển nhiên** (NT15).

| # | Câu hỏi người đọc sẽ hỏi | Phải trả lời được gì |
|---|---|---|
| 1 | Cái này mô tả hiện tượng gì? | mô hình thực tế đằng sau, và nó map sang mục đích của mình thế nào |
| 2 | Vì sao mô hình đó đúng? | định luật hoặc định lý gốc mà nó dựa vào |
| 3 | Phương trình là gì? | phương trình chi phối, kèm ý nghĩa từng ký hiệu |
| 4 | Vì sao chọn cái này, không chọn cái khác? | các lựa chọn đã cân, và tiêu chí để loại |
| 5 | Từ phương trình gốc ra nghiệm trong code thế nào? | từng bước biến đổi, **không nhảy bước**, mỗi bước kèm một câu vì sao |
| 6 | Làm sao tin nghiệm này đúng? | giá trị tại các mốc biên, so với kỳ vọng |

**Sổ tay** — bảng "thành phần → vai trò" cho (1) · diagram cho (2) · `$$…$$` kèm bảng ký hiệu cho (3) ·
bảng so sánh có cột ✓ cho (4) · đánh số ①②③ cho (5) · bảng "mốc → kỳ vọng → ✓" cho (6).

Không thương lượng:

- **Trực giác trước, ký hiệu sau** — nêu ý niệm bằng lời thường ("càng xa đích thì đi càng nhanh"),
  rồi mới ra phương trình.
- **Suy ra, không áp đặt** — công thức chốt phải *dẫn ra* từ nguyên lý gốc, không "xuất hiện từ hư
  không" rồi mới giải thích ngược.
- **Ngoại lệ: thứ chọn bằng cảm giác.** Hằng số tinh chỉnh, đường cong tự chế cho đã tay thì nói thẳng
  "chọn bằng tai và mắt, số này cho cảm giác X". **Đừng bịa dẫn giải vật lý** cho giá trị chọn bằng
  cảm nhận — nó làm hỏng niềm tin vào cả những phần thật sự có dẫn giải.
- **Lệch vật lý chuẩn là bình thường, không phải lỗi cần bào chữa** (NT11) — chỉ nêu lệch ở đâu, vì
  sao, và khi nào mới cần bản đầy đủ.

## 4.3 Đối chiếu công thức với code

Mỗi công thức đã chốt phải map sang code bằng một **phép kiểm chạy được**, không phải bằng cảm giác
"trông giống" (NT10). Đây là đối chiếu **công thức với code**, không phải nghiệm thu cảm giác chơi —
bằng chứng khác loại (§2.8).

**Sổ tay** — hệ nào có phép kiểm phù hợp hơn thì dùng cái đó:

| Phép kiểm | Cách làm |
|---|---|
| Đối chiếu từng số hạng | mỗi công thức chốt map thẳng một dòng code; kiểm **từng hệ số và từng dấu** |
| Kiểm mốc chéo | giá trị biên nêu ở phần toán phải khớp bảng kiểm chứng của task |
| Đạo hàm số | khi có hàm đạo hàm: so `f'(t)` với `(f(t+h)−f(t−h))/2h`, `h=1e-4` |
| Round-trip | khi có cặp converter hoặc overload: `A→B→A` phải về gần chính nó |

---

# §5 — Tài liệu

Mỗi hệ thống và mỗi tool có tài liệu riêng, đặt cùng thư mục với nó. **Thay đổi hệ thống thì cập nhật
tài liệu trong cùng lần làm** — riêng `.html` theo nhịp mốc.

| Loại | Vai trò | Vòng đời |
|---|---|---|
| **`.md`** | tài liệu **agent đọc** để hiểu và phát triển hệ — bản đặc, plain text, rẻ token; đồng thời là nguồn nội dung sinh `.html` | sống cùng hệ; cập nhật **cùng lần làm** với mỗi thay đổi |
| **`.html`** | tài liệu **developer và game designer đọc** — trực quan hóa 100% nội dung `.md`; agent không đọc bản này khi hệ đã có `.md` | sống cùng hệ; đồng bộ từ `.md` theo **mốc** — developer yêu cầu, hoặc chốt xong một cụm thay đổi |
| **Plan** (khi developer yêu cầu) | để developer **tự code lại** nhằm học | luôn là `.md`; vòng đời theo task |
| **Manual** (khi tool có người dùng không phải developer) | người dùng đọc để **thao tác** — luật viết ở §5.4 | sống cùng tool |

**Ai viết: agent, cả bốn loại.** Phân công với developer là *developer viết code lõi và làm bước
Editor · agent viết test và tài liệu*; hai vế sau cùng một lý do — nghiệm thu chúng là **đối chiếu máy
móc với code**, mở lại từng tên, từng chữ ký, từng hằng số ("không viết theo trí nhớ", §5.4), đúng chỗ
máy hơn người ở sức và nhất quán chéo (§2.8). Developer **chốt nội dung**: dòng nào lệch thiết kế thì
developer phân xử, agent sửa.

**Quy trình:** phỏng vấn ngữ cảnh (§2.1) và đối chiếu hiểu biết về code với developer (§2.2) → đọc
**tất cả** source, hiểu 100% data flow, lifecycle, lý do của mỗi quyết định → viết `.md` → sinh `.html`
từ `.md` → khi được yêu cầu thì viết Plan.

**Chuỗi sự thật một chiều `code → .md → .html`** (một dạng của "hai bản buộc khớp thì suy từ một
nguồn", §3.4): code là chuẩn, cả hai tài liệu phản ánh **100% thiết kế đang chạy trong code**. **Không
gộp, không xóa** — kể cả khi hai bản trông như nói cùng một thứ. Lệch thì sửa xuôi theo chuỗi: `.html`
lệch → đối chiếu `.md` với code trước, rồi đồng bộ `.html` từ `.md`; không sửa ngược rồi để `.md` trôi. **"100%" là không mất nội dung khi chuyển bản, không phải viết
cho nhiều** — trần độ dài do §5.4 canh, canh trên cả hai bản.

**Sổ tay** — checklist mỗi lần đồng bộ: soát hai luật "không viết theo trí nhớ" và "ở thì hiện tại"
của §5.4 · đổi tên hay xóa file tài liệu thì grep quét **tham chiếu chết** trong code comment, file
hướng dẫn agent của dự án, và tài liệu khác.

## 5.1 `.md` — tài liệu cho agent

Tổ chức theo **đường đi của dữ liệu** (input → processing → output), **không** theo trình tự hàn lâm
"lý thuyết → thiết kế → code": người đọc cần lần theo được một giá trị từ lúc vào đến lúc ra. Code
trích nguyên văn, không viết lại. Bảng metrics tổng kết đặt cuối.

**Sổ tay** — luồng dữ liệu vẽ bằng ASCII vì `.md` được đọc bằng nhiều công cụ; công cụ nào chắc chắn
render được mermaid thì dùng mermaid cũng được (NT2).

**Một mục bắt buộc: "Trước khi chạy", với mọi hệ cần wire tay.** Hệ nào không dựng được chỉ bằng cách
tham chiếu code — phải tạo asset, add component, kéo reference, điền số — thì mục này là **hợp đồng đi
kèm** của việc hệ không tự setup hộ (§3.6), không phải mục tuỳ chọn. Nghiệm thu: người chưa từng mở hệ
dựng lại được **từ 0** chỉ bằng mục này, theo thứ tự, không phải đọc code hay hỏi ai. Viết bằng **thao
tác và nhãn thật trên UI** — đường dẫn menu, tên ô trong Inspector, thứ kéo vào ô nào — và mỗi bước
kèm *"thiếu bước này thì hỏng ở đâu"*, vì đó là thứ chặn người dựng bỏ qua nó. Hệ chạy được mà không
cần wire gì thì không có mục này.

**Còn lại là kho mục để chọn, không phải form để điền.** Mỗi mục đưa vào phải gọi tên được **câu hỏi
của người đọc** mà nó trả lời; không gọi tên được thì bỏ. Hệ nhỏ có ba mục là bình thường. Kho: Data
structures · Core algorithm · Lifecycle · Implementation details · Framework integration · Design
decisions · Safety và error · Platform issues · Architecture (file tree kèm vai trò) · Testing ·
Extension · Performance.

**Nghiệm thu:** lần theo được một giá trị từ input tới output mà không nhảy section · mỗi so sánh
nhiều lựa chọn đều thấy được **tiêu chí** và **kết luận** · dựng được `.html` 100% từ file này mà
**không cần mở source** · đã quét lại KaTeX theo khối dưới đây.

> **Nền tảng — KaTeX**, lỗi hay tái diễn nhất: bất kỳ lệnh có `\` (`\frac`, `\sqrt`, `\cos`, `\tfrac`…)
> **phải** nằm trong `$…$` (inline) hoặc `$$…$$` (block); viết trong backtick sẽ hiện ra **raw text**.
> Backtick chỉ dùng cho ký hiệu Unicode thuần như `ω₀`, `ζ`. Mỗi block `$$…$$` phải nằm trên **một
> dòng**. Chốt xong quét lại: strip hết `$…$` và backtick, còn sót `\[a-zA-Z]` nào là lọt.

## 5.2 `.html` — tài liệu chính

Giữ **cấu trúc section của `.md`** để hai bản đối chiếu được. Đây là bản developer và game designer
thật sự đọc, nên luật "không viết theo trí nhớ" (§5.4) áp cả cho **số liệu trong demo**. **Để hiểu,
không để chép code**: chữ ký API thành bảng, chỉ giữ code khi bản thân đoạn code *là* thứ cần minh hoạ.

Trực quan hóa **theo loại nội dung**: so sánh thì bảng · luồng dữ liệu thì diagram · quan hệ định
lượng thì công thức · giá trị biến thiên liên tục thì Canvas · quá trình nhiều bước thì step. Demo chỉ
làm khi bảng và text **không đủ** để thấy hành vi.

**Nghiệm thu:** đủ 100% nội dung nguồn · single file · TOC khớp section thật · đọc được trên màn hình
nhỏ · người đọc *hiểu* được hệ thống mà không cần đọc code · mở trang không tương tác thì không tiến
trình nào chạy · mỗi demo thao tác được và cho thấy đúng hành vi đang nói tới.

**Sổ tay** — copy `DOCS_TEMPLATE.html`, thay các chỗ `{…}`, xoá section mẫu và khối demo mẫu, xoá hai
dòng KaTeX nếu tài liệu không có công thức. Khối xây sẵn, bốn dạng demo chạy được, và những thứ
template tự làm (bọc bảng vào khung cuộn, `PALETTE`, `setupCanvas` theo `devicePixelRatio`, đường dự
phòng khi KaTeX không tải được) liệt kê trong khối hướng dẫn ở đầu chính file đó (NT5). Không dùng thư
viện tô màu code: tài liệu này vốn hạn chế show code (NT3).

> **Nền tảng** — cạm bẫy của Canvas và DOM, cấm trong draw loop và trong handler:
>
> | Không dùng | Vì sao | Đã thay bằng |
> |---|---|---|
> | `ctx.shadowBlur` | Gaussian blur mỗi lần vẽ | radial gradient |
> | `createImageData()` mỗi event | cấp phát W×H×4 bytes mỗi lần | tạo một lần rồi reuse |
> | per-pixel math mỗi `mousemove` | O(W×H) hơn 60 lần mỗi giây | pre-render ra ImageData rồi cache |
> | `mousemove` vẽ thẳng | vẽ 2–3 lần giữa hai frame | gom vào rAF: lưu toạ độ scalar và dirty flag |
> | `ctx.fillStyle='var(--x)'` | Canvas không parse CSS variable | hằng `PALETTE`, đọc từ CSS một lần |
> | `putImageData` sau khi `ctx.scale()` | phương thức này **không** chịu ma trận biến đổi | dựng ImageData theo `canvas.width/height` (pixel thiết bị), không theo toạ độ logic |
> | `innerHTML` trong vòng lặp | parser cộng reflow | `textContent` |
> | quên `cancelAnimationFrame` | rAF chạy tiếp sau khi chuột rời | cancel trong handler `leave` |
> | ẩn nội dung bằng `opacity:0` rồi chờ JavaScript bật lại | JavaScript lỗi hoặc tắt là **mất nội dung** | chỉ ẩn khi JavaScript đã xác nhận chạy được |

## 5.3 Plan — để developer tự triển khai

Tiêu chí: **tự chứa**. Developer code lại được từ đầu đến cuối mà **không phải suy đoán** và không phải
mở tài liệu khác. Các task xếp theo **thứ tự phụ thuộc**, mỗi task chỉ cần thứ đã có ở task trước.
**Nếu** hệ có lõi toán (§4.1) thì mục `§0` của chính Plan dẫn giải tại chỗ theo mạch §4.2.

**Phân công: lõi developer viết, test agent viết** — chính file Plan thì agent viết như mọi tài liệu
khác (§5); mục này chia phần **nội dung bên trong** nó. Code lõi trong Plan là bản để developer đọc và
gõ lại, không phải bản để agent commit. Test thì ngược: agent viết và chạy (§2.8), nên Plan **chỉ có danh
sách case sẽ kiểm**, không có code test — developer không gõ lại test nên code test ở đây là công bỏ
đi. **Nhịp:** developer code xong lõi → agent đọc code thật rồi mới viết test, vì chữ ký lúc viết Plan
còn là bản nháp. Danh sách case là chỗ developer veto hoặc thêm case trước khi agent viết.

**Sổ tay** — kho phần cho mỗi task, **chỉ lấy phần task này cần**: Files (đường dẫn chính xác) ·
Interfaces (consumes và produces, chữ ký đầy đủ) · bảng "toán → code" trỏ về `§0` · bảng lý do cho
mỗi quyết định thiết kế và tối ưu · **code hoàn chỉnh dán được** với comment trỏ công thức nguồn ·
**Editor setup** khi chạm scene hoặc prefab (§3.6) · **bảng case kiểm thử** (input → kỳ vọng, kèm biên
theo §2.8). Task không có toán thì không có bảng toán→code; không chạm scene thì không có Editor setup.

**Plan không thuật lại code.** Bảng lý do ghi *quyết định và vì sao chọn nó*, không kể *code làm gì*
— code nằm ngay đó rồi (§5.4).

**Code trong plan — năm đảm bảo:** **vừa đủ** (NT3) · **mở đường mai** (NT4) · **đúng với công thức đã
chốt** (khớp 100% công thức `§0`, mỗi nghiệm đã kiểm mốc; là "code khớp công thức", **không** phải
"công thức phải khớp vật lý" — NT11) · **hiệu năng** theo NT8 và §3.3 · **self-document** theo §3.7.

**Nghiệm thu riêng:** có mục "Ngữ cảnh đã chốt" (§2.5) · mọi hàm có caller thật, hoặc có lý do phòng
xa chữ ký nói được ra (NT4) · công thức đã đối chiếu với code (§4.3) · phần sẽ test có bảng case,
không có code test.

## 5.4 Kỷ luật viết và bảo trì — áp cho mọi loại tài liệu

**Cắt trước khi giao — bắt buộc, không phải tuỳ chọn.** Bản đầu luôn có nước. Cắt theo thứ tự này,
không cần cân nhắc: câu dẫn *"phần này sẽ nói về…"* · tóm tắt lại thứ vừa nói · câu chuyển tiếp ·
nhận xét về chất lượng thiết kế · ẩn dụ và câu chốt có vần · cùng một ý viết hai lần cho chắc · phần
lý do dài hơn một câu cho một khẳng định hiển nhiên (NT15). Cắt xong mà vẫn dài thì nội dung thật sự
lớn — **tách file**, đừng nén chữ cho vừa.

**Luật câu chữ của §2.3 áp cho tài liệu**: một câu một ý · hạn chế thuật ngữ · gọi khái niệm đúng tên
nó có trong code · kết luận trước, dẫn giải sau. Câu nhiều mệnh đề lồng nhau là chỗ người đọc phải
đọc hai lần. Chỉ khác nhau ở **trần**: đối thoại canh theo quyết định đang chờ, tài liệu canh theo
**việc người đọc phải làm được sau khi đọc**.

| Luật | Nghĩa là |
|---|---|
| **Mỗi tài liệu một người đọc** | mỗi loại trả lời đúng câu hỏi của người đọc nó; chép nội dung loại này sang loại kia là sai cả hai. **Cặp `.md`–`.html` của một hệ không thuộc lỗi này** (§5): cùng nội dung, hai người đọc, giữ khớp bằng chuỗi một chiều. Tool có người dùng không phải developer thì có **Manual riêng**: viết theo **nhãn thật trên UI**, trả lời *"bấm gì ra gì, dùng khi nào"* — không chứa tên class, không lý giải cách cài đặt |
| **Không chép lại thứ người đọc đã cầm trong tay** | mỗi tài liệu chỉ chứa thứ người đọc **của nó** không tự lấy được từ nguồn sự thật đang có: `.md` và `.html` viết cho người mở được **code** — cột "vai trò" nói lại đúng tên hàm, dòng bảo đảm mà một dòng `catch` đã nói hết, metrics ghi `O(n log n)` của một `Sort`: **bỏ**. Plan viết cho người **chưa có code**, chuẩn của nó là các task trước trong chính Plan; Manual viết cho người **không đọc code**, chuẩn của nó là màn hình trước mặt. Phép kiểm lấy của NT3: *xoá dòng này thì người đọc mất gì* — đáp án là "mở file kia ra xem" thì bỏ. **Ngoại lệ giữ bằng mọi giá:** danh sách chữ ký API trong `.md`, vì `.html` dựng từ `.md` mà không mở source (§5.1) — nén cho đặc chứ không bỏ |
| **Không viết theo trí nhớ** | mọi tên file, signature, hằng số, nhãn UI đều mở code đối chiếu lại trước khi ghi — kể cả khi vừa viết chính dòng code đó (NT10). Đây là nguồn sai nhiều nhất của tài liệu |
| **Viết cho người đọc lần đầu, ở thì hiện tại** | NT14 áp cho tài liệu: mô tả hệ *như nó đang là*, không kể *nó đã đổi thế nào*. **Ngoại lệ duy nhất:** sổ ghi bẫy *"đã sai một lần"* (§3.4) trong mục quyết định thiết kế — đó là ghi **bài học**, không phải tường thuật thay đổi; và kể cả ở đó cũng không lưu tên cũ (§3.7). **Sổ tay** — grep các cụm kể lịch sử ("trước đây", "bản cũ", "giờ đã") |
| **Câu hỏi của người đọc là bằng chứng tài liệu chưa rõ** | chỗ phải hỏi chính là chỗ hệ thống khó đoán; trả lời xong phải để câu trả lời lại trong tài liệu, không để nó chết trong hội thoại |
| **Mâu thuẫn thì SỬA dòng cũ** | không thêm dòng thứ hai nói ngược — hai dòng đá nhau tệ hơn không có dòng nào. **Riêng dòng cũ ghi quyết định hoặc ranh giới do developer đặt thì không tự sửa** — nêu chỗ lệch để developer phân xử (NT1) |
| **Quyết định trái trực giác gom về một mục riêng** | chỗ cố ý trông "kém tối ưu" phải có lý do viết sẵn ở một nơi biết trước; người tối ưu sau đọc mục đó **trước khi đụng** (cùng họ với "những gì cố ý KHÔNG làm" của §2.5) |
| **Tư tưởng mới chưng cất ngay trong task** | task, câu hỏi, phản hồi nào xác lập **quy ước còn đúng ở lần sửa sau** thì ghi vào tài liệu module **trước khi báo hoàn thành** — quyết định chỉ sống trong hội thoại thì chết cùng hội thoại. Ghi *quy ước ở thì hiện tại*, không tường thuật task. **Và liệt kê nguyên văn các dòng đã ghi trong báo cáo hoàn thành** — developer phải thấy để veto được bản khái quát sai trước khi nó thành luật cho agent sau. Riêng thêm vào chính MY_SKILL thì theo §2.6 — hỏi developer trước |
