---
name: workflow
description: Dùng cho mọi tác vụ, từ lúc nhận việc tới báo cáo hoàn thành: phỏng vấn tới khi chốt kịch bản, đối chiếu code với developer, văn phong, ngữ cảnh đã chốt, chưng cất tư tưởng, subagent, nghiệm thu đúng loại bằng chứng, đề xuất công cụ cho việc lặp lại.
---

# workflow — một việc, từ nhận việc tới báo cáo

Skill nền dưới `Skills/horcrux_skill.md` (NT, ba tầng, ký hiệu định nghĩa ở đó).

Trình tự: **phỏng vấn tới khi chốt kịch bản** → **đối chiếu code với developer** → `reuse-gate` → làm theo kịch bản →
**nghiệm thu đúng loại** → **quét cuối chat**.

**Việc lặp lại thì đề xuất công cụ ngay lượt đó** (lần thứ hai làm cùng chuỗi thao tác, hoặc biết sẽ làm lại): một dòng
*việc gì · lặp mấy lần · công cụ trông thế nào · chỗ đặt · công viết · tiết kiệm mỗi lần*. Mặc định đặt ở Horcrux (tool
Editor, script `Tools~`, skill); dự án chỉ giữ lớp nối. Developer đồng ý thì viết công cụ, tài liệu, cập nhật skill hoặc
mở skill công cụ mới kèm hàng trong bảng chỉ dẫn. *Đã sai một lần:* hàng trăm lần eval chép cùng một khung không đề xuất gom.

## Phỏng vấn — hỏi tới khi chốt kịch bản

Không đoán rồi làm (NT5): đoán thừa là hàm không caller, đoán thiếu là chữ ký chặn hướng dùng thật. Hỏi đủ năm nhóm, mỗi
vòng gom câu hỏi lại, **hỏi nhiều vòng tới khi hết chỗ mơ hồ**; nhóm đã biết chắc thì nêu giả định để xác nhận.

| Nhóm | Với tài liệu | Với code hoặc plan |
|---|---|---|
| **Ai dùng** | ai đọc, để làm gì, biết sẵn tới đâu | ai gọi, ở đâu, có caller thật **ngay bây giờ** chưa |
| **Mục tiêu** | đọc xong làm được gì | hành vi hay cảm giác gì, nghiệm thu bằng gì |
| **Ngân sách** | sâu, dài bao nhiêu | nhịp (NT2), hot path, platform |
| **Ranh giới** | giải ở đây hay trỏ đi | class này hay hệ khác; độc lập hay kết hợp; dự án hay Horcrux |
| **Hướng thật** | hệ sắp đổi gì | **chắc chắn** sắp cần gì; *có thể* cần thì chưa |

**Chốt bằng kịch bản viết lại:** *ai làm gì → hệ phản ứng gì → thấy gì* · ca biên đã thống nhất · thứ cố ý không làm ·
nghiệm thu bằng gì. Developer xác nhận mới làm; **chưa có câu trả lời thì chưa làm**. Khi triển khai, mỗi hành vi truy về
một dòng kịch bản; thứ kịch bản không nói thì hỏi, không tự thêm.

Giả định chỉ vá khe hở giữa chừng, đủ cả ba: **nhỏ** · **đảo ngược được** · nhãn tại chỗ dùng. Khó đảo ngược: ghi đè hay
xoá dữ liệu đã author (level, save, asset) · migration schema, wire format · thứ ngoài version control. Hướng developer chưa
nghĩ tới thì nêu **một dòng kèm giá**, không tự đưa vào output; kéo sang hệ khác thì để "Mở rộng sau". Thứ developer tự
nêu thì hỏi lại một lần để cân đắt–lợi, rồi theo developer.

## Đối chiếu với developer

Code cho biết cái gì chạy, không cho biết vì sao; developer không nhất thiết nắm từng ngóc ngách. Với **mỗi phát hiện ảnh
hưởng quyết định đang bàn**: **thấy gì** (đường dẫn, dòng) · **hiểu ý định là gì** — phát biểu lại, hỏi khớp thiết kế không
· **developer biết chưa** — chủ ý hay đã trôi. Không kể lại code. Mỗi lượt review đọc lại file hiện tại: báo lại lỗi đã sửa
làm mất tin vào phát hiện còn đúng. Số đọc từ hai file YAML chỉ so được khi quy về một kiểu, hoặc đọc qua API engine.

## Văn phong

NT10 áp nguyên. Thêm: một câu một ý · bỏ câu dẫn, câu chuyển, tóm tắt lại · thuật ngữ buộc dùng thì giải nghĩa lần đầu ·
gọi khái niệm **đúng tên trong code** · câu hỏi trả lời được không cần mở code · ví dụ kiểm trên code trước khi đưa · việc
developer làm tay trong Editor gom một mục cuối.

**Bày phương án rồi chốt một:** mỗi phương án một dòng *là gì · được gì · mất gì*, rồi chốt và nói vì sao thắng — tiêu chí
là hợp NT1–NT4 nhất, không phải dễ làm nhất. Quyết định thuộc developer thì chỉ thu hẹp và nêu giá; tên gợi ý chưa áp vào
code hay tài liệu trước khi developer chốt.

## Ngữ cảnh đã chốt và "Mở rộng sau"

Plan đặt **"Ngữ cảnh đã chốt"** trước `§0`, tài liệu nêu ở mở đầu: người dùng · mục tiêu · ranh giới · chỗ đặt · **thứ cố ý
KHÔNG làm, kèm lý do** · **quyết định trái trực giác** (để người tối ưu sau đọc trước khi đụng) · **"Mở rộng sau"** — hướng
đã tính chưa làm, mỗi dòng một hướng, kèm bản tạm và Plan nếu có.

## Chưng cất tư tưởng

Quyết định cho một bài toán cụ thể mang tư tưởng của developer, và trôi mất khi bài toán xong. Mỗi dòng định ghi đi qua:
**① khái quát** — tách tư tưởng khỏi quyết định riêng, kèm vì sao · **② đối chiếu toàn bộ skill** — đã có thì thôi ·
trường hợp riêng thì trỏ về · gộp được vào luật đang có thì gộp · mâu thuẫn thì nêu bản hợp NT hơn để developer phân xử,
không tự sửa dòng cũ · **③ ghi**:

| Nguồn | Ghi |
|---|---|
| câu trả lời khi brainstorm | **hỏi** developer có ghi không, vào tầng nào; chốt mới ghi |
| code thật lệch Plan (lượt so sau mỗi step) | *lỗi* → báo kèm bằng chứng · *trôi* (tên, thứ tự, chi tiết không mang lý do) → sync Plan theo code · *tư tưởng* (đổi hình dạng, ranh giới, kiểu, cách tiêm) → ghi thẳng, liệt kê để veto |
| **quét cuối chat — bắt buộc, không đợi nhắc** | duyệt cả chat: rule, tư tưởng dùng chung cho game Unity từ thứ developer yêu cầu, sửa, hoặc đồng ý. **Ghi thẳng, liệt kê nguyên văn trong báo cáo** để veto |

Chỉ khái quát lựa chọn có "vì sao" lặp lại được; hằng số, tên, phạm vi một task thì không. Dòng ghi vào skill **không mang
domain dự án**, đúng văn phong skill nhận, không bao giờ vào mục developer. Quy ước chỉ chung cho một hệ thì ghi ở tài liệu
module.

## Subagent

Subagent là ngữ cảnh trắng; để nó tự tìm hiểu là thuế đọc nhân theo số subagent. Prompt **tự chứa như một task của Plan**:
trích đoạn tài liệu cần, đường dẫn file sẽ chạm, tiêu chí nghiệm thu; thiếu thì báo về, không tự đi đọc — trừ **code nó sẽ
sửa**.

## Nghiệm thu — phép kiểm theo loại tiêu chí

Neo khi phân vân: **công sức đắt nhất là của developer.**

| Tiêu chí | Bằng chứng | Ai chạy |
|---|---|---|
| thuật toán tất định, công thức, parser, serialize | phép kiểm chạy được | agent |
| tên, tham chiếu, đồng bộ tài liệu | grep quét | agent |
| hiệu năng | số trước–sau tại chỗ đo | agent |
| đúng–sai xác định được mà làm tay chậm, sót | vét cạn | agent, **tự đề xuất** |
| cảm giác chơi, nhịp, độ khó, hình ảnh | **chơi thử** | **developer** |
| kịch bản hành vi trên build (event, kinh tế, save, tutorial) | chơi qua kịch bản, cheat rút đường | developer, QA |

**Cảm giác — dừng và giao, không dựng proxy.** Báo *"cần chơi thử"* kèm kịch bản: *vào đâu* · *làm gì* (chuỗi ngắn nhất) ·
*nhìn gì* · *khác trước ra sao* · *dấu hiệu hỏng*. **Developer chơi là cảm được thì agent không vào Play qua công cụ**: dừng
ở phần tĩnh (code, YAML, eval không vào Play), chỉ ra nguyên nhân, sửa, giao kịch bản. Play qua công cụ chỉ cho thứ
developer **không cảm được**: trace chứng minh refactor giữ chuyển động, số đo, trạng thái nội bộ (`feel-tune`).

**Vét cạn khi có đáp án xác định được và ít nhất một dấu hiệu** — agent làm kể cả chưa được yêu cầu, vì đây là bằng chứng:
*sức* (không gian đầu vào lớn, lặp nhiều) · *thiên kiến* (ca biên khó nghĩ hết — **liệt kê biên trước khi chạy**: rỗng,
một phần tử, chạm giới hạn, trùng, ngoài dải, thứ tự đảo, hai sự kiện cùng lúc, frame đầu, object huỷ giữa chừng) · *tầm
nhìn* (chứng minh **vắng mặt**; state nội bộ sai khi màn hình đúng; rò rỉ sau hàng nghìn vòng) · *nhất quán chéo* (nhiều
bản buộc khớp; trước–sau refactor phải trùng). Không dấu hiệu nào thì đọc code là xong.

**Cheat — mở đường tới kịch bản, không mở nắp kỹ thuật.** Chính đáng khi kịch bản **không tới được bằng đường người chơi
trong thời gian hợp lý**: thời gian (hết tuần) · khối lượng (đủ điểm mốc cuối) · trạng thái đầu (save mới). Mỗi nút là một
dòng phải giữ đúng mãi. Hàng đọc chỉ cho thứ màn hình không hiện, qua API sẵn có, không bọc thứ đã public; lệnh riêng chỉ khi
phải chạm `private`, gọi đúng lệnh thật của hệ. Panel có trong **build chính thức, không `#if`** — QA kiểm bản người chơi cầm.
Lệnh, cửa đọc `private` và suy luận thuộc **hệ**, không phụ thuộc công cụ debug — hệ framework mang được sang dự án dùng
panel khác; lớp nối với panel thuộc dự án. **Đưa về trạng thái đầu** đi qua chủ giữ state: thay bản trong RAM lẫn bản lưu,
flush, rồi **thoát app** — xoá kho lưu giữa phiên bị flush lúc pause/quit ghi đè, việc chỉ chạy lúc boot (grant, cache dẫn
xuất) không chạy lại. "Như cài mới" xoá đúng danh sách key, không xoá cả kho (consent, prefs của SDK).

> **Nền tảng** — khi dự án dùng SRDebugger: tab Options, một category mỗi hệ, trigger `Off`; cách mở panel do dự án quyết.
> Option chỉ đọc lại getter khi bind, `OnEnable` (mở panel, đổi tab) hoặc khi được báo, không theo frame: lệnh xong phải
> báo các hàng đọc.
> **Sổ tay** — partial `<Hệ>.Debug.cs` giữ lệnh, plain class `<Hệ>DebugOptions` giữ hàng UI (nhận hệ qua constructor,
> `Init/Dispose` ở `Awake/OnDestroy`, `internal sealed` vì SRDebugger quét mọi public member) · hàng đọc là `string`
> read-only · lệnh có tham số = property số hoặc chuỗi + method không tham số · `INotifyPropertyChanged` bắn `null` sau mỗi
> lệnh · hàng là delegate đọc hệ thì `DynamicOptionContainer` + `OptionDefinition.Create`/`FromMethod`, báo bằng
> `Property.NotifyValueChanged()`, không còn class public cho SRDebugger quét.
