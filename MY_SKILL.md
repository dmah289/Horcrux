# MY_SKILL.md — Tư tưởng thiết kế hệ thống & quy trình làm việc với AI

Dành cho **game puzzle trên Unity**. Đây là tư tưởng, không phải tài liệu của một dự án — mang sang dự
án nào cũng dùng nguyên. Áp cho **mọi** thứ viết ra: runtime, editor, tooling, script tạm, tài liệu.

**Ai đọc:** AI agent, trước khi làm bất cứ việc gì · tôi, khi review output.
**Đi kèm:** `DOCS_TEMPLATE.html` (§5.2).

## Ba tầng ràng buộc

| Tầng | Nhận biết | Nghĩa | AI được phép |
|---|---|---|---|
| **Luật** | *mặc định*, không đánh dấu | tư tưởng và tiêu chí nghiệm thu: nói **cần đạt gì**, không nói **làm thế nào** | không bỏ; tự chọn cách đạt |
| **Nền tảng** | khối `> **Nền tảng**` | cụ thể vì thực tế chỉ có một đáp án — giới hạn của Unity, browser, renderer | không đi đường khác; mìn đã có người đạp giúp |
| **Sổ tay** | dòng `**Sổ tay** —` | một cách đã dùng và chạy được; không phải cách duy nhất, không phải danh sách đóng | thay bằng cách hay hơn, kèm lý do và phép kiểm (NT12) |

**Ký hiệu trích dẫn:** `NT<n>` là nguyên tắc số n ở §1 · `§x` và `§x.y` là mục trong file này.

Sổ tay nào ghi **điều kiện áp dụng** thì ra ngoài điều kiện đó là vô nghĩa — đừng cố lắp.

§1 và §2 là nền, áp cho mọi việc. §3–§5 là cơ chế riêng của từng loại việc; nắm nền rồi thì đọc lướt là đủ.

---

# §1 — Nguyên tắc chung

Nơi **duy nhất** định nghĩa các nguyên tắc này. Các phần sau chỉ trỏ về, không giải lại.

| # | Nguyên tắc | Nội dung |
|---|---|---|
| 1 | **Mật độ thông tin** | Chọn dạng trình bày có mật độ cao nhất **cho loại nội dung đó**: bảng cho so sánh, diagram cho luồng, công thức cho quan hệ định lượng, một câu văn cho trực giác. Không kể lể, không diễn giải lại thứ vừa nói. Áp cho **tài liệu đầu ra** — đối thoại thì ngược lại (§2.3). |
| 2 | **Trình tự hợp lý** | Dẫn theo mạch **dễ→khó, tổng quan→chi tiết, vấn đề→giải pháp, trực giác→hình thức hóa**. Mỗi bước chỉ dùng khái niệm đã nêu trước; ý phụ thuộc nhau đặt liền kề; đánh số khi là quy trình. |
| 3 | **Giải thích bản chất** | Mỗi khái niệm: cơ chế, "tại sao", trade-off. Không chỉ "dùng X". |
| 4 | **Không lặp** | *Tài liệu:* một khái niệm giải thích một nơi — lần đầu xuất hiện — sau đó trỏ về; khái niệm đã giải ở tài liệu hệ khác thì trỏ sang. Ngoại lệ: bảng tra cứu tổng kết cuối. *Code:* cùng một nguyên tắc — **một chức năng cài đặt một nơi**. Có sẵn và đáp ứng được thì dùng lại, đừng viết bản thứ hai (§2.4); có người dùng thứ hai thì nâng nó thành tái sử dụng được (§3.2). |
| 5 | **Dẫn giải sâu đúng chỗ** | Độ sâu **cân theo độ khó thật**, không mặc định tối đa: suy ra trong 1–2 bước thì kết quả kèm kiểm mốc là đủ; nhiều bước biến đổi, hoặc có chọn lựa mô hình đáng bàn, thì dẫn giải đầy đủ. Dẫn định luật nền để biện minh cho một phép nhân là over-engineering. |
| 6 | **Vừa đủ** | *Bao nhiêu thứ được đưa vào.* Đơn giản nhất mà vẫn phục vụ đủ mục đích ban đầu. Đơn giản là **mặc định**; mỗi lớp phức tạp thêm vào phải trả giá bằng **một nhu cầu đang có thật** — "phòng khi cần", "cho đầy đủ", "chuẩn hơn" **không** phải nhu cầu. Kiểm nhanh: *xoá nó đi thì hỏng ở đâu* — không gọi được tên chỗ hỏng thì bỏ. Hai bờ vực đều sai: **thừa** (đúng, nhưng không ai cần) và **thiếu** (cắt vào mục đích ban đầu — mục đích là **sàn**, không phải chỗ gọt cho ngắn). |
| 7 | **Game feel là tiêu chí nghiệm thu** | Hệ này phục vụ **cảm giác chơi**, không phục vụ độ chính xác vật lý. Công thức "sai sách" mà chơi đã tay thì **đúng**; công thức chuẩn sách mà chơi vô hồn thì **sai**. Toán là *công cụ để đạt cảm giác*, không phải mục tiêu. |
| 8 | **Editor-first** | Thứ gì quyết được lúc authoring thì đừng đẩy sang runtime: **code lo *hành vi*, Editor lo *cấu hình và kết nối*** (§3.3). |
| 9 | **Bằng chứng, không khẳng định suông** | Mọi "tại sao" kèm phép kiểm **tái lập được**; mọi công thức chốt phải **kiểm mốc**; code phải đối chiếu với công thức trước khi chốt. Không viết "đã đúng", "đã tối ưu" mà thiếu mốc, số đo, hoặc phép thử người đọc tự chạy lại được. |
| 10 | **Hỏi đúng lúc, tự quyết đúng chỗ** | *Khi nào hỏi, khi nào tự đi.* Thiếu ngữ cảnh thì **hỏi** (§2.1), không tự đoán rồi làm; buộc phải giả định thì ghi rõ `Giả định (cần xác nhận): …` **tại chỗ dùng**, không giấu vào output như thể đã chốt. **Được đề xuất mở rộng phạm vi** khi phạm vi hiện tại **chặn khả năng phát triển** — nhưng phải **nêu ra kèm giá phải trả**, không âm thầm làm rộng. Ranh giới: mở rộng vì *sẽ bị chặn* thì nêu; vì "cho đầy đủ, cho chuẩn hơn" thì không (NT6). Với thứ user tự nêu, hỏi lại **một lần** để cân đắt–lợi rồi theo quyết định của user. |
| 11 | **Đủ hôm nay, mở đường mai** | *Hình dạng của thứ đã chốt.* Triển khai đúng phạm vi đã chốt, **không code sẵn** thứ chưa ai cần (NT6). Nhưng hình dạng phải để bước phát triển kế tiếp là **thêm vào**, không phải **đập ra làm lại**: chữ ký nhận đủ thông tin nó cần, ranh giới trách nhiệm đặt đúng chỗ, điểm nối để hở. Đây là **cách sắp xếp**, không phải **thêm số lượng** — chi phí hôm nay gần bằng 0. Nghiệm thu: gọi được tên bước kế tiếp, và chỉ ra được nó là "thêm" chứ không phải "sửa". Bản hiện tại sẽ chặn một hướng đáng kể thì nêu ra (NT10). |
| 12 | **Phạm vi bàn được, cách làm luôn mở** | *Mức tự do khi triển khai.* Tiêu chí đã chốt thì cách đạt là việc của người triển khai. Thấy cách đạt **cùng tiêu chí** mà đơn giản, nhanh, hoặc rõ hơn thì **dùng nó** và nói rõ vì sao kèm phép kiểm (NT9). Đổi **cách làm** không cần hỏi lại; đổi **phạm vi** thì cần (NT10). Im lặng chọn món có sẵn trong sổ tay khi biết có cách tốt hơn là vi phạm nguyên tắc này. |

---

# §2 — Quy trình làm việc với AI

## 2.1 Phỏng vấn ngữ cảnh — trước khi làm

Agent **không** suy đoán ngữ cảnh rồi bắt tay làm, vì đoán sai lệch về hai phía và cả hai đều đắt
(NT6): **thừa** là chương không ai đọc, hàm không caller nào gọi, tối ưu chỗ không phải hot path, dẫn
giải 100 dòng cho công thức 1 dòng — trả giá vĩnh viễn bằng thời gian viết, đọc, bảo trì. **Thiếu** là
bỏ mất thứ người dùng cần, hoặc chữ ký chặn hướng dùng thật, phải đập đi làm lại.

Hỏi 5 nhóm dưới, **gộp thành 1–2 lượt**, không hỏi lắt nhắt từng câu. Đã biết chắc nhóm nào thì **nêu
giả định của mình để user xác nhận**, thay vì hỏi lại. **Chưa có câu trả lời thì chưa làm** (NT10).

| Nhóm | Với tài liệu | Với code hoặc plan |
|---|---|---|
| **Ai dùng đầu ra** | ai đọc, đọc để làm gì, biết sẵn tới đâu | ai gọi, gọi ở đâu trong game, có caller thật **ngay bây giờ** chưa |
| **Mục tiêu** | đọc xong phải **làm được gì** | phải đạt **cảm giác hoặc hành vi** gì, nghiệm thu bằng gì |
| **Ngân sách** | độ sâu và độ dài nào là đủ | bao nhiêu lần mỗi giây, có phải hot path không, platform nào |
| **Ranh giới** | phần nào giải ở đây, phần nào trỏ sang tài liệu khác | phần nào của class này, phần nào của caller hoặc hệ khác |
| **Hướng phát triển thật** | hệ sắp đổi gì khiến tài liệu phải sửa | **chắc chắn** sắp cần thêm gì; cái gì *có thể* cần nhưng chưa chắc (NT11) |

## 2.2 Đọc code rồi phải đối chiếu lại với dev

Áp khi brainstorm, và bất cứ khi nào đọc code có sẵn trước khi đề xuất.

**Code cho biết cái gì đang chạy, không cho biết vì sao nó được viết như vậy.** Khoảng lệch giữa hai
thứ đó là nơi sinh ra cả bug lẫn hiểu sai. Hai kiểu thất bại, đều tốn cả một vòng làm lại: AI hiểu sai
**ý định thiết kế** rồi im lặng dựa vào cách hiểu đó, nên mọi đề xuất sau lệch theo mà dev không có cơ
hội phát hiện · AI **mặc định dev đã biết** ngóc ngách vừa đọc được, nên dev bỏ qua một thông tin quan
trọng vì tưởng nó đã được cân nhắc rồi.

**Tuyệt đối không mặc định developer nắm rõ từng ngóc ngách hệ thống của mình.** Code có thể do người
khác viết, viết từ lâu, hoặc đã trôi khỏi thiết kế ban đầu mà chưa ai nhận ra.

Nên với **mỗi phát hiện có ảnh hưởng đến quyết định đang bàn**, nêu đủ ba phần:

1. **Tôi thấy gì** — kèm đường dẫn và dòng cụ thể, để dev mở ra kiểm được.
2. **Tôi hiểu ý định là gì** — phát biểu lại bằng lời của mình, rồi hỏi thẳng: cách hiểu này có khớp
   thiết kế của anh không?
3. **Anh đã biết chỗ này chưa** — nếu chưa, nó là chủ ý hay là chỗ đã trôi cần xử lý?

Giới hạn: chỉ nêu thứ **ảnh hưởng đến quyết định đang bàn**. Kể lại toàn bộ code vừa đọc là vi phạm NT1 và NT6 — dev phải đọc một bản tường thuật thay vì trả lời một câu hỏi.

## 2.3 Văn phong khi đối thoại

Đối thoại có mục tiêu khác tài liệu: ở đây **được hiểu đúng ngay lần đầu** quan trọng hơn ngắn gọn. Đây
là chỗ **NT1 không áp** — nén thông tin trong đối thoại chỉ tạo thêm một vòng hỏi lại.

- Rõ ràng, rành mạch. Một câu nói một ý.
- **Hạn chế viết tắt** — dùng tên đầy đủ; buộc viết tắt thì mở ngoặc giải nghĩa ở lần đầu.
- **Hạn chế thuật ngữ khó hiểu** — nói được bằng lời thường thì nói bằng lời thường; buộc dùng thì
  giải thích ngắn ngay tại chỗ, không để dev phải tra.
- Câu hỏi phải trả lời được **mà không cần mở code ra đọc lại** — thiếu ngữ cảnh gì thì cung cấp kèm.
- Nêu đề xuất thì kèm **cái được và cái mất**, không chỉ nêu kết luận.

## 2.4 Tái sử dụng, rồi chốt phạm vi

**Trước câu hỏi "có cần không" là câu hỏi "đã có chưa"** (NT4). Khảo sát project, submodule, package, và
Utilities của SDK trước khi viết dòng đầu tiên. Ba kết cục:

| Cái có sẵn | Xử lý |
|---|---|
| Đáp ứng được yêu cầu | dùng lại, không viết bản thứ hai |
| Gần đúng nhưng thiếu | mở rộng nó nếu **thêm được mà không sửa cái cũ**; không được thì viết mới |
| Không có, hoặc phải **bẻ cong bài toán** cho vừa nó | **viết mới** — tái sử dụng không phải lý do để làm sai bài toán |

Nói rõ đã khảo sát những đâu và kết luận gì, để dev biết quyết định đến từ đâu (NT9).

Sau đó mới tới phạm vi. **Mọi thứ định đưa vào** — chương tài liệu, demo, hàm, tham số, guard, tối ưu,
lớp trừu tượng — qua cùng một luật: **có nhu cầu thật ngay bây giờ thì đưa vào**. Nhu cầu thật của từng
loại (ví dụ, không phải danh sách đóng):

| Thứ định thêm | Nhu cầu thật là |
|---|---|
| Interface hoặc abstract | có **implementation thứ hai** |
| Tham số | có **call site truyền khác mặc định** |
| Guard hoặc nhánh biên | có **input thật chạm được biên** đó |
| Tối ưu | **hot path đã xác nhận** |
| Tách lớp hoặc tách class | **trách nhiệm thật sự khác**, không phải "cho gọn mắt" |
| Chương, mục, demo | có người đọc cần nó để **làm được một việc cụ thể** |

Thứ chỉ "có thể cần sau" thì chia theo **giá của việc thêm sau**:

| Thêm sau | Xử lý |
|---|---|
| **Rẻ** — thêm mục mới hoặc hàm mới, không sửa cái cũ | **để lại**, ghi một dòng ở mục "Mở rộng sau" |
| **Đắt** — sửa chữ ký, đập cấu trúc, viết lại cả tài liệu | làm ngay; đây là chỗ **duy nhất** đáng phòng xa |

Lý do của cả hai bảng: **tính mở rộng đến từ Open/Closed** — thêm cái mới mà không sửa cái cũ — **không**
đến từ việc viết sẵn thứ chưa ai cần. Một hàm một dòng thêm sau tốn hai phút; giữ nó trong API từ đầu
tốn mãi mãi. Ngược lại chữ ký sai thì sửa sau rất đắt, nên dồn công sức phòng xa vào **chữ ký và ranh
giới trách nhiệm** (NT11), không vào số lượng.

## 2.5 Ghi ngữ cảnh đã chốt vào đầu output

Plan thì đặt mục **"Ngữ cảnh đã chốt"** trước `§0`; tài liệu thì nêu ở phần mở đầu. Gồm: người dùng ·
mục tiêu · ranh giới · **những gì cố ý KHÔNG làm, kèm lý do** · hướng phát triển đã tính tới nhưng chưa
làm (NT11). Người đọc sau biết vì sao phạm vi dừng ở đó, không "bổ sung cho đủ".

---

# §3 — Thiết kế code

## 3.1 SOLID

| | Nội dung |
|---|---|
| **S** | Một class là một responsibility. Tách khi class có nhiều hơn một lý do thay đổi. |
| **O** | Extend, don't modify. Không sửa trực tiếp class đang chạy ổn định — mở rộng bằng cơ chế phù hợp với bài toán. Chỉ tạo abstraction khi có nhu cầu thật (§2.4). |
| **L** | Subtype thay thế được base mà không break behavior, không side-effect lạ. |
| **I** | Interface nhỏ, tách theo consumer. Không ép client phụ thuộc method nó không dùng. |
| **D** | Depend on abstractions. **Consumer không tự `new` thứ nó phụ thuộc** — nó nhận vào. (Factory, pool, container thì đương nhiên phải `new`; đó là trách nhiệm của chúng.) |

> **Nền tảng** — DI runtime của dự án là InitArgs (`Sisus.Init`): `[Service(typeof(T))]` để đăng ký,
> `MonoBehaviour<TDep>` + `Init(TDep)` để nhận. Editor và tooling không bắt buộc dùng InitArgs —
> constructor injection hoặc static factory ở đó là hợp lệ.

## 3.2 Module — không monolithic

Ranh giới hệ thống phải **nhìn thấy được** trong cấu trúc dự án, không chỉ tồn tại trong đầu người
viết. Mỗi hệ thống có một trách nhiệm gọi được tên; đọc tên là biết nó lo gì.

**Module không tham chiếu trực tiếp implementation của nhau.** Cơ chế trung gian chọn theo bài toán —
interface, event bus, ScriptableObject channel, dữ liệu thuần đều được — miễn đạt được: đổi
implementation một bên mà bên kia không phải sửa.

**Phân tầng theo mức phụ thuộc, quyết ngay từ đầu:** hệ **độc lập** (bê sang dự án khác được, không phụ
thuộc hệ nào trong SDK) so với hệ **kết hợp** (dựng trên nhiều hệ độc lập). Phân loại sai chỗ này là
sửa sau rất đắt (§2.4). **Utilities** là static và universal — dự án nào cũng cần, không phụ thuộc bất
kỳ hệ thống nào trong SDK.

**Chức năng có người dùng thứ hai thì đề xuất nâng nó thành tái sử dụng được** (NT4) — đừng copy sang
chỗ mới. Nâng bằng cách nào thì tuỳ bản chất chức năng: hàm thuần không giữ state → Utilities hoặc
Helper static · có state hoặc sẽ có nhiều biến thể → trừu tượng hoá thành interface rồi tách
implementation · chỉ khác nhau ở một giá trị → thêm tham số, không thêm hàm. Đặt ở **tầng thấp nhất mà
cả hai người dùng đều với tới được**, không thấp hơn — đẩy một thứ chỉ hai hệ dùng lên Utilities là làm
Utilities phình ra vì lý do không có thật (NT6).

Đây là **đề xuất, không tự làm**: nâng một chức năng là đổi ranh giới trách nhiệm, mà đổi ranh giới thì
cần user quyết (NT10).

## 3.3 Editor-first

Thứ gì quyết được lúc authoring thì để lúc authoring quyết. Đang viết code chỉ để **tìm, nối, hoặc
gán** thứ vốn đã tồn tại lúc authoring thì code đó đặt sai chỗ. Dấu hiệu: `GetComponent` / `Find` /
`AddComponent` / `Resources.Load` để lấy thứ đã có trên prefab · hằng số tinh chỉnh cảm giác hardcode
· dựng hierarchy bằng code.

> **Nền tảng** — lý do gốc, không phải sở thích: dữ liệu serialize sửa được **không cần compile**, ai
> trong team cũng chỉnh được, và khi thiếu thì lộ ra ô trống trong Inspector chứ không nổ giữa gameplay.

**Sổ tay** — cách đã dùng: reference kéo thả vào `[SerializeField]` · component add sẵn trên prefab ·
số tinh chỉnh phơi ra Inspector · preset thành ScriptableObject · wire sẵn trong prefab rồi
`Instantiate` thay vì dựng bằng code.

**Ngoại lệ tự nhiên** là thứ chưa tồn tại lúc authoring: object spawn runtime, số lượng động, dữ liệu
từ server. Ngoại lệ nằm ở *thời điểm biết được*, không phải ở *độ tiện khi viết code*.

Plan chạm scene hoặc prefab thì mô tả thao tác Editor **như một bước thật**, không lặng lẽ thay bằng
code cho tiện viết.

## 3.4 Async & tài nguyên

Tiêu chí: **hủy được** (việc dừng theo owner của nó) · **giải phóng được** (thứ giữ tài nguyên phải có
đường trả lại) · **cô lập được lỗi** (một callback lỗi không kéo cả hệ chết).

> **Nền tảng** — lựa chọn và ràng buộc của dự án:
>
> | Nhu cầu | Dùng | Ràng buộc không bỏ được |
> |---|---|---|
> | Async | **UniTask**, không coroutine, không `Task` | propagate `CancellationToken` xuống toàn bộ chain — hủy an toàn khi MonoBehaviour bị destroy |
> | Load asset | **Addressables** qua `AssetReference` | không dùng string key (type-safe, không lỗi runtime do sai tên); giữ `AsyncOperationHandle` để `Release()` đúng lúc, không giữ là leak |
> | Data lớn | `NativeArray` / `NativeList` | khi cần truyền GPU hoặc Job System; `StructLayout(Sequential)` khi phải khớp layout GPU hoặc native |
> | Tài nguyên nặng | cache `RenderTexture`, `Texture2D`… | có đường dọn dẹp trong `OnDestroy()` |

**Sổ tay** — cô lập lỗi listener: try/catch quanh từng callback trong vòng dispatch.

## 3.5 Hiệu năng runtime

Ba tiêu chí:

1. **Không tạo rác ở chỗ chạy lặp** — mỗi frame, mỗi vòng lặp, mỗi lần vẽ.
2. **Không tính lại thứ không đổi** — input không đổi thì đừng tính lại.
3. **Chỉ tối ưu chỗ đã xác nhận là hot path** (§2.4). Ngoài hot path thì **chọn bản dễ đọc nhất** — đó
   không phải nhượng bộ, đó là quyết định đúng.

**Cách rẻ nhất thường là làm phép tính biến mất, không phải làm nó chạy nhanh hơn.** Trước khi cache
hay tối ưu, hỏi: có thể không cần tính nó không · tính một lần lúc authoring được không (§3.3) · đổi
cấu trúc dữ liệu để câu hỏi tự biến mất được không?

**Nghiệm thu:** chỉ ra được **chỗ đo** và **số trước–sau** (NT9).

**Sổ tay** — kỹ thuật đã dùng:

- *Giảm cấp phát:* pool thay `Instantiate`/`Destroy` lặp lại · pre-alloc capacity dự đoán trước · reuse
  buffer bằng `.Clear()` · grow-only buffer khi size dao động · `struct` cho data nhỏ ngắn hạn ·
  `ref` / `in` / `Span<T>` thay copy · `static readonly` thay `new` lặp · tránh LINQ, boxing, string
  concat trong hot path (cần thì `StringBuilder` hoặc cache sẵn) · **không closure capture** — lambda bắt
  biến ngoài thì cấp phát mỗi lần gọi; cache delegate thành `static readonly` hoặc field.
- *Giảm tính toán:* dirty flag · event-driven rebuild · lookup dictionary dựng trước · tách phần tĩnh
  tính một lần khỏi phần động tính incremental · precompute hằng nặng (`exp`, `sqrt`, `sincos`, phép
  chia) ngoài vòng lặp · đổi chia thành nhân · guard thoát sớm · `sqrMagnitude` thay `magnitude` khi
  chỉ so sánh khoảng cách.
- *Sửa list an toàn:* duyệt ngược khi xoá (`RemoveAt` cuối là O(1)) · hoặc deferred removal — đánh dấu
  rồi xử lý sau vòng lặp, không xoá khi đang iterate.

> **Nền tảng** — `Update`, polling loop và `OnGUI` bị gọi lại liên tục cho **cùng một state**. Việc
> nặng đặt trong đó là sai không cần bàn; nó thuộc về event handler, hoặc thuộc về lúc authoring.

## 3.6 Editor tool

Tiêu chí: **một tool là một đơn vị gói kín** (mở thư mục ra là thấy hết thứ nó cần) · **thứ dùng chung
sống một chỗ** (phát hiện logic tái sử dụng được thì chuyển ra, ví dụ `Common/`) · **thứ không đổi giữa
các lần vẽ lại phải có sẵn**.

> **Nền tảng** — với IMGUI (`OnGUI`, `EditorWindow`, `PropertyDrawer`): một lần tương tác của người
> dùng gây ra nhiều lần gọi `OnGUI` cho **cùng một state**, mỗi lần gọi lại chạy lại toàn bộ hàm — nên
> thứ không đổi mà bị tạo lại trong đó là rác thuần. Một số thứ (`GUIStyle`, `GUIContent` có icon) chỉ
> tồn tại sau khi `GUI.skin` và `EditorGUIUtility` sẵn sàng, nên không khởi tạo được ở static initializer.

**Sổ tay** — *áp cho IMGUI*. Dùng UI Toolkit thì mô hình repaint hoàn toàn khác (cây phần tử tồn tại
liên tục, không chạy lại mỗi frame) nên bảng này không áp; tiêu chí ở trên vẫn giữ, cách đạt thì khác.

| Cấp cache | Kỹ thuật | Khi nào dùng |
|---|---|---|
| Static eager | `static readonly` | giá trị bất biến: `Color`, `GUILayoutOption[]`, `GUIContent` chỉ có text |
| Static lazy + guard | init một lần trong `EnsureStyles()` | thứ cần `GUI.skin` hoặc `EditorGUIUtility` mới có |
| Instance lazy | null-check init ở cấp window | style riêng từng tool, không cần chia sẻ |
| Dirty-flag | chỉ rebuild khi dữ liệu đổi | layout options khi window resize |
| Event-phase | tính toán nặng chỉ ở `EventType.Layout` | filter, sort, format — `Repaint` dùng lại kết quả |

Cùng nhóm: dynamic `GUIContent` — một instance duy nhất, chỉ cập nhật `.text` khi giá trị thực sự đổi ·
một `GUIContent` dùng chung để đo `CalcHeight`.

## 3.7 Naming — self-documenting code

- Tên method nói rõ **mục đích**: `EnsureMaterial()`, `SwapWriteBuffer()`, `SolveAnalytic()`. Tên vô
  nghĩa cần thay: `Process`, `Handle`, `DoWork`, `Update2`.
- Boolean đọc như một câu hỏi: `IsPickable`, `HasCars`, `frameDataReady`.
- Code tự giải thích được thì comment **chỉ** nói **tại sao**, không nói **cái gì**.
- API public có XML doc; `<param>` cho mọi tham số có contract không hiển nhiên — miền giá trị, đơn vị,
  ai là người cấp giá trị đó.

---

# §4 — Hệ toán học và vật lý

## 4.1 Khi nào dùng tới toán, và sâu tới đâu

Mục này **không** phải nghĩa vụ áp cho mọi hệ. Nó chỉ mở ra trong hai trường hợp:

1. **User yêu cầu.**
2. **Agent thấy toán giải bài toán tốt hơn hẳn** — trường hợp này thì **nêu ra để user quyết**, kèm cái
   được và cái mất, không tự đưa vào (NT10).

**Mặc định là không cần toán.** Phần lớn logic puzzle là trạng thái và luật rời rạc, không phải phương
trình. Toán là một lớp phức tạp, phải trả giá bằng nhu cầu thật như mọi lớp khác (NT6). Xét hết những
cách thường đủ, rẻ hơn, và dễ chỉnh hơn trước đã: `AnimationCurve` hoặc bảng tra do designer chỉnh
trong Inspector (NT8) · easing có sẵn · lerp · máy trạng thái · một hằng số chọn bằng tay.

**Dấu hiệu đang ép toán vào chỗ không cần:** phải dẫn định luật nền để biện minh một phép nhân (NT5) ·
công thức chỉ có một call site và không tham số nào thay đổi · designer không chỉnh được gì trong đó ·
kết quả thay bằng vài giá trị trong một bảng là xong.

**Bờ vực còn lại cũng sai** (NT6): bài toán vốn liên tục và có ràng buộc — chuyển động phải dừng đúng
chỗ, va chạm, nội suy cần đạo hàm liên tục — mà né toán thì thành một đống hằng số tinh chỉnh không ai
hiểu, sửa chỗ này vỡ chỗ khác. Toán đúng chỗ làm code **ngắn hơn**, không dài hơn.

**Đã cần toán thì sâu vừa đủ cho tính năng.** Chọn mô hình đủ để tính năng chạy đúng và cho cảm giác
đúng, **không** chọn mô hình đúng nhất về vật lý: xấp xỉ là **mặc định** (NT7), không tự nâng lên bản
đầy đủ vì "chuẩn hơn" (NT10). Nhưng khi bài toán **thật sự** cần bản đầy đủ thì **làm tử tế** — cắt nửa
vời rồi bù bằng hằng số là cách sinh ra hệ mà sau này không ai dám sửa, tệ hơn cả hai lựa chọn sạch.

> Hai độ sâu này khác nhau, đừng lẫn: độ sâu của **mô hình** theo đoạn trên · độ sâu của **dẫn giải**
> theo NT5. Mô hình xấp xỉ vẫn có thể cần dẫn giải đầy đủ, và ngược lại.

## 4.2 Cần thì phải cho hiểu sâu

Đã xác định là cần toán thì không được dừng ở công thức cuối để dán vào code. Người đọc phải đạt ba
thứ: **hiểu hiện tượng** đằng sau · **tin công thức là suy ra được**, không phải phép màu · **kiểm lại
được** bằng tay.

> Cân độ sâu trước (NT5): mạch dưới đây dành cho công thức **không hiển nhiên**.

Sáu câu hỏi người đọc sẽ hỏi, theo đúng thứ tự họ hỏi:

| # | Câu hỏi | Phải trả lời được gì |
|---|---|---|
| 1 | Cái này mô tả hiện tượng gì? | mô hình thực tế đằng sau, và nó map sang mục đích của mình thế nào |
| 2 | Vì sao mô hình đó đúng? | định luật hoặc định lý gốc mà nó dựa vào |
| 3 | Phương trình là gì? | phương trình chi phối, kèm ý nghĩa từng ký hiệu |
| 4 | Vì sao chọn cái này, không chọn cái khác? | các lựa chọn đã cân, và tiêu chí để loại |
| 5 | Từ phương trình gốc ra nghiệm trong code thế nào? | từng bước biến đổi, **không nhảy bước**, mỗi bước kèm một câu vì sao |
| 6 | Làm sao tin nghiệm này đúng? | giá trị tại các mốc biên, so với kỳ vọng |

**Sổ tay** — cách trình bày đã dùng: bảng "thành phần → vai trò" cho (1) · diagram cho (2) · `$$…$$`
kèm bảng ký hiệu cho (3) · bảng so sánh có cột ✓ cho (4) · đánh số ①②③ cho (5) · bảng "mốc → kỳ vọng →
✓" cho (6).

Không thương lượng:

- **Trực giác trước, ký hiệu sau** — nêu ý niệm bằng lời thường ("càng xa đích thì đi càng nhanh"), rồi
  mới ra phương trình.
- **Suy ra, không áp đặt** — công thức chốt phải *dẫn ra* từ nguyên lý gốc, không "xuất hiện từ hư
  không" rồi mới giải thích ngược.
- **Ngoại lệ: thứ chọn bằng cảm giác.** Hằng số tinh chỉnh, đường cong tự chế cho đã tay thì nói thẳng
  "chọn bằng tai và mắt, số này cho cảm giác X". **Đừng bịa dẫn giải vật lý** cho một giá trị vốn chọn
  bằng cảm nhận — nó làm hỏng cả niềm tin vào những phần thật sự có dẫn giải.
- **Lệch vật lý chuẩn là bình thường, không phải lỗi cần bào chữa** (NT7). Chỉ cần nêu **lệch ở đâu, vì
  sao, và khi nào mới cần bản đầy đủ** — để người đọc tra sách xong không bối rối.

## 4.3 Đối chiếu công thức với code

Mỗi công thức đã chốt phải map sang code bằng một **phép kiểm chạy được**, không phải bằng cảm giác
"trông giống" (NT9).

**Sổ tay** — phép kiểm đã dùng; hệ nào có phép kiểm phù hợp hơn thì dùng cái đó:

| Phép kiểm | Cách làm |
|---|---|
| Đối chiếu từng số hạng | mỗi công thức chốt map thẳng một dòng code; kiểm **từng hệ số và từng dấu** |
| Kiểm mốc chéo | giá trị biên nêu ở phần toán phải khớp bảng kiểm chứng của task |
| Đạo hàm số | khi có hàm đạo hàm: so `f'(t)` với `(f(t+h)−f(t−h))/2h`, `h=1e-4` |
| Round-trip | khi có cặp converter hoặc overload: `A→B→A` phải về gần chính nó |

---

# §5 — Tài liệu

Mỗi hệ thống và mỗi tool có tài liệu riêng, đặt cùng thư mục với nó. **Thay đổi hệ thống thì cập nhật
tài liệu trong cùng lần làm**, không để sau. Ba loại đầu ra, cùng một bộ tư tưởng (§1, §2):

| Loại | Vai trò |
|---|---|
| **`.md`** | **nguồn sự thật** — agent đọc để hiểu và phát triển tiếp |
| **`.html`** | tài liệu **chính** developer đọc — visualize `.md` |
| **Plan** (khi user yêu cầu) | để developer **tự code lại** nhằm học |

**Quy trình:** phỏng vấn ngữ cảnh (§2.1) và đối chiếu hiểu biết về code với dev (§2.2) → đọc **tất cả**
source, hiểu 100% data flow, lifecycle, lý do của mỗi quyết định → viết `.md` → sinh `.html` từ `.md` →
khi được yêu cầu thì viết Plan.

## 5.1 `.md` — nguồn sự thật

Tổ chức theo **đường đi của dữ liệu** (input → processing → output), **không** theo trình tự hàn lâm
"lý thuyết → thiết kế → code": người đọc cần lần theo được một giá trị từ lúc vào đến lúc ra. Code
trích nguyên văn, không viết lại. Bảng metrics tổng kết đặt cuối.

**Sổ tay** — luồng dữ liệu vẽ bằng ASCII, vì `.md` được đọc bằng nhiều công cụ và ASCII là thứ hiện đúng
ở mọi nơi (`.html` có `.arch` cho đúng dạng này). Công cụ nào chắc chắn render được mermaid thì dùng
mermaid cũng được (NT12).

**Nghiệm thu:**
- Lần theo được một giá trị từ input tới output mà không nhảy section.
- Mỗi so sánh nhiều lựa chọn đều thấy được **tiêu chí** và **kết luận**, không chỉ liệt kê.
- Dựng được `.html` 100% từ file này mà **không cần mở source**.
- Đã quét lại KaTeX theo khối Nền tảng dưới đây.

**Sổ tay** — các mục thường có, chọn mục hợp hệ thống chứ không điền cho đủ: Data structures · Core
algorithm · Lifecycle · Implementation details · Framework integration · Design decisions · Safety và
error · Platform issues · Architecture (file tree kèm vai trò) · Testing (checklist và cách debug) ·
Extension · Performance.

> **Nền tảng — KaTeX**, lỗi hay tái diễn nhất: bất kỳ lệnh có `\` (`\frac`, `\sqrt`, `\cos`, `\tfrac`…)
> **phải** nằm trong `$…$` (inline) hoặc `$$…$$` (block); viết trong backtick sẽ hiện ra **raw text**.
> Backtick chỉ dùng cho ký hiệu Unicode thuần như `ω₀`, `ζ`. Mỗi block `$$…$$` phải nằm trên **một
> dòng** — block trải nhiều dòng bị vỡ ở một số renderer. Chốt xong quét lại: strip hết `$…$` và
> backtick, còn sót `\[a-zA-Z]` nào là lọt.

## 5.2 `.html` — tài liệu chính

Giữ **cấu trúc section của `.md`**. Ba tiêu chí:

1. **Đủ 100% nội dung `.md`** — đây là bản developer thật sự đọc, không phải bản rút gọn.
2. **Để hiểu, không để chép code.** Chữ ký API thì thành bảng. Chỉ giữ code khi bản thân đoạn code *là*
   thứ cần minh họa — một dòng lỗi, một pattern then chốt. Không dán nguyên class hoặc nguyên hàm.
3. **Zero idle cost** — trang mở ra mà người đọc không tương tác thì không tốn CPU.

Trực quan hóa **theo loại nội dung**, không theo thói quen: so sánh thì bảng · luồng dữ liệu thì
diagram · quan hệ định lượng thì công thức · giá trị biến thiên liên tục thì Canvas · quá trình nhiều
bước thì step. Demo chỉ làm khi bảng và text **không đủ** để thấy hành vi; hệ toán và vật lý thường cần
vì hành vi phải *thấy* mới hiểu.

**Nghiệm thu:**
- Đủ 100% nội dung `.md`; single file; TOC khớp với section thật; đọc được trên màn hình nhỏ.
- Người đọc *hiểu* được hệ thống mà không cần đọc code — chỗ nào phải dán code mới hiểu là chỗ chưa
  trực quan hóa xong.
- Mở trang, không tương tác gì: không có tiến trình nào đang chạy.
- Mỗi demo thao tác được và cho thấy đúng hành vi đang nói tới.

**Sổ tay** — cơ chế: copy `DOCS_TEMPLATE.html`, thay các chỗ `{…}`, xoá section mẫu và khối demo mẫu,
xoá hai dòng KaTeX nếu tài liệu không có công thức. Khối xây sẵn, bốn dạng demo chạy được, và những thứ
template tự làm (bọc bảng vào khung cuộn, `PALETTE`, `setupCanvas` theo `devicePixelRatio`, đường dự
phòng khi KaTeX không tải được) đều liệt kê trong khối hướng dẫn ở đầu chính file đó — đọc ở đó (NT4).
Không dùng thư viện tô màu code: tài liệu này vốn hạn chế show code, nên tải một thư viện từ mạng cho
vài đoạn code ngắn là không đáng (NT6).

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
> | ẩn nội dung bằng `opacity:0` rồi chờ JavaScript bật lại | JavaScript lỗi hoặc tắt là **mất nội dung**, không phải mất hiệu ứng | chỉ ẩn khi JavaScript đã xác nhận chạy được |

## 5.3 Plan — để developer tự triển khai

Tiêu chí: **tự chứa**. Developer code lại được từ đầu đến cuối mà **không phải suy đoán** và không phải
mở tài liệu khác. Các task xếp theo **thứ tự phụ thuộc** (nền trước, dùng lại sau), mỗi task chỉ cần
thứ đã có ở task trước. **Nếu** hệ có lõi toán (§4.1) thì mục `§0` của chính Plan dẫn giải tại chỗ theo
mạch §4.2 — hệ không cần toán thì không có `§0`, đừng dựng một mục toán cho đủ hình thức.

**Sổ tay** — mỗi task thường gồm: Files (đường dẫn chính xác) · Interfaces (consumes và produces, chữ
ký đầy đủ) · bảng "toán → code" trỏ về `§0` · bảng lý do cho mỗi quyết định thiết kế và tối ưu ·
**code hoàn chỉnh dán được** với comment trỏ công thức nguồn · **Editor setup** khi chạm scene hoặc
prefab (§3.3) · bảng kiểm chứng input → kỳ vọng, nói rõ nếu không kèm code test.

**Code trong plan — năm đảm bảo:**

| Đảm bảo | Cụ thể |
|---|---|
| **Vừa đủ** (NT6) | đơn giản nhất trong các cách **cùng đúng**; mỗi hàm, tham số, nhánh, lớp trừu tượng đều gọi được tên chỗ hỏng nếu xoá đi |
| **Mở đường mai** (NT11) | chữ ký và ranh giới để bước kế tiếp là "thêm", không phải "đập" |
| **Đúng với công thức đã chốt** | code khớp 100% công thức `§0`; mỗi nghiệm đã kiểm mốc trước khi vào code; comment trỏ công thức nguồn. Là "code khớp công thức", **không** phải "công thức phải khớp vật lý" (NT7) |
| **Hiệu năng** | theo §3.5 |
| **Self-document** | theo §3.7 |

**Nghiệm thu riêng:** có mục "Ngữ cảnh đã chốt" (§2.5) · mọi hàm có caller thật, hoặc có lý do phòng xa
chữ ký nói được ra (NT11) · công thức đã đối chiếu với code (§4.3).
