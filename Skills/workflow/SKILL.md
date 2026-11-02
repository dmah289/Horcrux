---
name: workflow
description: Dùng cho mọi tác vụ, từ lúc nhận việc tới báo cáo hoàn thành: phỏng vấn ngữ cảnh, đối chiếu code với developer, văn phong, ngữ cảnh đã chốt, chưng cất tư tưởng, subagent, nghiệm thu đúng loại bằng chứng, đề xuất công cụ cho việc lặp lại.
---

# workflow — quy trình một việc, từ nhận việc tới báo cáo

Skill nền của Horcrux, đứng dưới `Skills/horcrux_skill.md`: `NT<n>`, ba tầng Luật · Nền tảng · Sổ tay và bảng
chỉ dẫn định nghĩa ở đó. Con trỏ `` `skill` › Mục `` trỏ sang skill khác; `mục X` trỏ trong file này.

Trình tự một việc: **phỏng vấn** (mục Phỏng vấn) → **đối chiếu code với developer** (mục Đối chiếu với developer) → **có sẵn chưa, có
cần không, đặt ở đâu** (`reuse-gate`) → làm → **nghiệm thu đúng loại** (mục Nghiệm thu) → **quét cuối chat** (mục Chưng cất).

**Tác vụ lặp lại thì đề xuất viết thành công cụ ngay lượt đó, developer quyết.** Dấu hiệu: lần thứ hai
làm cùng một chuỗi thao tác, hoặc biết sẽ làm lại ở task sau hay dự án sau. Đề xuất một dòng: *việc gì ·
lặp mấy lần · công cụ trông thế nào · chỗ đặt · công viết · tiết kiệm mỗi lần*. Chỗ đặt mặc định là
Horcrux — công cụ Editor, script `Tools~`, skill; dự án chỉ giữ lớp nối gọi tên type của nó. Developer
đồng ý thì viết công cụ, tài liệu, thêm vào skill (hoặc mở skill mới, thêm hàng vào bảng chỉ dẫn của `horcrux_skill`). *Đã sai một
lần:* hàng trăm lần eval chép lại cùng một khung — vào Play, gõ input, ghi file — không lần nào đề xuất gom.

## Phỏng vấn ngữ cảnh — trước khi làm

Không suy đoán ngữ cảnh rồi làm: đoán thừa là hàm không caller, đoán thiếu là chữ ký chặn hướng dùng thật
(NT1). Hỏi 5 nhóm, **gộp 1–2 lượt**; biết chắc nhóm nào thì nêu giả định để developer xác nhận. **Chưa có
câu trả lời thì chưa làm.**

| Nhóm | Với tài liệu | Với code hoặc plan |
|---|---|---|
| **Ai dùng đầu ra** | ai đọc, để làm gì, biết sẵn tới đâu | ai gọi, ở đâu, có caller thật **ngay bây giờ** chưa |
| **Mục tiêu** | đọc xong phải làm được gì | cảm giác hoặc hành vi gì, nghiệm thu bằng gì (mục Nghiệm thu) |
| **Ngân sách** | sâu và dài bao nhiêu là đủ | nhịp (NT2), hot path không, platform nào |
| **Ranh giới** | giải ở đây hay trỏ sang tài liệu khác | của class này hay hệ khác; độc lập hay kết hợp; dự án hay Horcrux (`reuse-gate`) |
| **Hướng phát triển thật** | hệ sắp đổi gì khiến tài liệu phải sửa | **chắc chắn** sắp cần gì; *có thể* cần thì chưa (NT1) |

Giả định chỉ vá khe hở phát hiện giữa chừng, đủ cả ba: khe hở **nhỏ** · đầu ra **đảo ngược được** · nhãn
ghi tại chỗ dùng (NT5). Khó đảo ngược: ghi đè hay xoá dữ liệu đã author (level, save, asset) · migration
đổi schema hay wire format · thứ ngoài version control.

Gợi ý hướng developer chưa nghĩ tới thì cứ nêu — **một dòng kèm giá** để developer chọn, không tự đưa vào
output vì "tiện thể". Trần là phạm vi đang bàn; kéo sang hệ khác thì để ở "Mở rộng sau" (mục Ngữ cảnh đã chốt). Thứ
developer tự nêu thì hỏi lại một lần để cân đắt–lợi, rồi theo developer.

## Đối chiếu với developer sau khi đọc code

Code cho biết cái gì đang chạy, không cho biết vì sao. Không mặc định developer nắm từng ngóc ngách: code
có thể do người khác viết, viết từ lâu, hoặc đã trôi khỏi thiết kế. Với **mỗi phát hiện ảnh hưởng quyết
định đang bàn**: **(1) thấy gì** kèm đường dẫn và dòng · **(2) hiểu ý định là gì** — phát biểu lại rồi hỏi
có khớp thiết kế không · **(3) developer đã biết chưa** — chưa thì là chủ ý hay đã trôi. Không kể lại toàn
bộ code vừa đọc. Mỗi lượt review đọc lại file hiện tại (NT8): báo lại lỗi đã sửa làm mất tin vào phát hiện
còn đúng. *Đã sai một lần:* so số đọc từ hai file serialize ghi khác kiểu rồi kết luận "đã chết" — số từ
hai file YAML chỉ so được khi quy về một kiểu, hoặc đọc qua API của engine.

## Văn phong khi đối thoại

NT10 áp nguyên. Thêm: một câu một ý · bỏ câu dẫn, câu chuyển, tóm tắt lại · hạn chế viết tắt và thuật ngữ,
buộc dùng thì giải nghĩa lần đầu · gọi khái niệm **đúng tên nó có trong hệ**, khớp nguyên văn code (`code-design` › Naming)
· câu hỏi phải trả lời được mà không cần mở code · ví dụ cũng là khẳng định, kiểm trên code trước khi đưa
(NT8) · việc developer làm tay trong Editor gom về một mục riêng ở cuối (`docs` › Kỷ luật viết).

**Bày phương án, rồi chốt một cái.** Mỗi phương án một dòng: *là gì · được gì · mất gì*. Rồi chốt một và
nói vì sao thắng — tiêu chí là hợp `horcrux_skill` › Bốn ưu tiên nhất, không phải dễ làm nhất. Bỏ phần chốt là đẩy việc khó nhất về
developer; bỏ phần bày là lấy mất dữ kiện để bác lại. Quyết định thuộc developer (NT5) thì chỉ thu hẹp và
nêu giá; tên gợi ý chưa áp vào code hay tài liệu trước khi developer chốt, bản nháp lỡ áp thì hoàn về.
## Ngữ cảnh đã chốt và "Mở rộng sau"

Plan đặt mục **"Ngữ cảnh đã chốt"** trước `§0`; tài liệu nêu ở mở đầu. Gồm: người dùng · mục tiêu · ranh
giới · chỗ đặt (dự án hay Horcrux) · **những gì cố ý KHÔNG làm, kèm lý do** · **quyết định trái trực giác**
— chỗ cố ý trông "kém tối ưu" có lý do viết sẵn để người tối ưu sau đọc **trước khi đụng** · **"Mở rộng
sau"** — hướng đã tính nhưng chưa làm, mỗi dòng một hướng, kèm bản tạm và đường dẫn Plan nếu có. Người đọc
sau biết vì sao phạm vi dừng ở đó, không "bổ sung cho đủ".

## Chưng cất tư tưởng

Tư tưởng của developer lộ ra dưới dạng quyết định cho một bài toán cụ thể và trôi mất khi bài toán xong.
Ba nguồn, ba nhịp ghi:

| Nguồn | Khi nào | Cách ghi |
|---|---|---|
| **Câu trả lời khi brainstorm** | từng câu trả lời | ① khái quát hoá: tách *tư tưởng* khỏi *quyết định riêng của bài toán*, kèm vì sao · ② đối chiếu **toàn bộ** bộ skill: đã có thì thôi · trường hợp riêng thì trỏ về · gộp được vào yêu cầu đang có để nó mạnh hơn thì gộp, không mở yêu cầu mới · mâu thuẫn thì chọn bản hợp toàn file hơn, nói vì sao thắng (mục Văn phong), developer phân xử — không tự sửa dòng cũ (NT5, `docs` › Kỷ luật viết) · ③ **hỏi** developer có ghi không, vào tầng nào — chốt mới ghi |
| **Code thật lệch khỏi Plan** | lượt so code–plan sau mỗi step (`docs` › Plan) | phân loại từng chỗ lệch: *lỗi* → báo kèm bằng chứng (mục Đối chiếu với developer) · *trôi* (tên, thứ tự, chi tiết không mang lý do) → sync plan theo code · *tư tưởng* (đổi hình dạng, ranh giới, kiểu, cách tiêm, và lý do còn đúng ở bài toán khác) → ①② rồi ghi thẳng, liệt kê để veto: developer đã quyết bằng chính dòng code. Lý do ở chỗ lệch luôn mạnh hơn plan, hoặc là lỗi — không có ca thứ ba |
| **Quét cuối chat — bắt buộc, không đợi nhắc** | trước báo cáo hoàn thành của mỗi chat có làm việc | duyệt **cả chat**, tổng hợp vào skill nền phù hợp mọi rule, tư tưởng, kịch bản dùng chung cho game Unity từ hai nguồn: thứ developer yêu cầu hoặc sửa, và gợi ý của agent developer đã đồng ý (kể cả bằng cách sửa code theo gợi ý). Mỗi dòng qua ②. **Ghi thẳng, liệt kê nguyên văn trong báo cáo** để veto; mâu thuẫn thì ghi bản thắng kèm lý do, dòng cũ giữ tới khi developer phân xử; nguồn không rõ thì nêu giả định |

Chỉ khái quát khi câu trả lời **thật sự chứa tư tưởng** — lựa chọn có "vì sao" lặp lại được; hằng số, tên,
phạm vi một task thì không. Câu hỏi ở ③ không tính vào 1–2 lượt của mục Phỏng vấn. Mọi dòng ghi vào bộ skill **không
mang domain dự án** — bỏ tên type, tên hệ, hằng số của dự án; đúng cấu trúc và văn phong của skill nhận nó; không bao giờ ghi vào mục developer của `horcrux_skill`. Quy ước
chỉ đủ chung cho một hệ thì ghi ở tài liệu module (`docs` › Kỷ luật viết).

## Subagent — ngữ cảnh bơm từ orchestrator, không tự đọc lại

Mỗi subagent là ngữ cảnh trắng; để nó "tự tìm hiểu" là nó đọc lại cả bộ skill và tài liệu hệ, thuế đọc **nhân
theo số subagent**. Prompt cho subagent **tự chứa như một task của Plan** (`docs` › Plan): trích đoạn tài liệu cần
cho task, đường dẫn file sẽ chạm, tiêu chí nghiệm thu; thiếu ngữ cảnh thì subagent báo về, không tự đi
đọc. Ngoại lệ: subagent tự đọc **code nó sẽ sửa** (NT8).

## Nghiệm thu — chọn phép kiểm theo loại tiêu chí

NT8 đòi bằng chứng; mục này nói bằng chứng **nào** hợp tiêu chí nào. Neo khi phân vân: **công sức đắt nhất
trong nghiệm thu là của developer.**

| Tiêu chí cần chứng minh | Bằng chứng đúng loại | Ai chạy |
|---|---|---|
| Thuật toán tất định, công thức, parser, serialize | phép kiểm chạy được (`math-physics` › Đối chiếu công thức) | agent |
| Tên, tham chiếu, đồng bộ tài liệu | grep quét (`code-design` › Naming, `docs`) | agent |
| Hiệu năng | số trước–sau tại chỗ đo (`code-design` › Hiệu năng) | agent |
| Đúng–sai xác định được, mà làm tay chậm, sót, hoặc không thấy | vét cạn theo bảng dưới | agent, **tự đề xuất** |
| Cảm giác chơi, nhịp, độ khó, hình ảnh | **chơi thử** | **developer** |
| Kịch bản hành vi trên build — event, kinh tế, save, tutorial | chơi qua kịch bản, cheat rút đường tới trạng thái cần kiểm | developer, QA |

**Cảm giác chơi — dừng và giao, không dựng proxy.** Ép nó về một lệnh chạy được là đo thứ dễ đo thay cho
thứ cần biết. Báo *"phần này cần chơi thử"* kèm kịch bản: *vào đâu* (level, cờ, dữ liệu) · *làm gì* (chuỗi
ngắn nhất) · *nhìn gì* (hiện tượng cụ thể) · *khác trước ra sao* · *dấu hiệu hỏng*. **Developer chơi là cảm
được thì agent không vào Play qua công cụ** — bố cục, vị trí, hình ảnh, nhịp, chỗ lệch nhìn là thấy: agent
dừng ở phần tĩnh (đọc code và YAML, eval trên asset đã nạp không vào Play), chỉ ra nguyên nhân, sửa, giao
kịch bản. Play qua công cụ chỉ cho thứ developer **không cảm được**: trace chứng minh refactor giữ chuyển
động, số đo hiệu năng, trạng thái nội bộ không hiện (skill `feel-tune`).

**Đúng–sai xác định được — vét cạn, không đẩy về tay.** Điều kiện là cả hai: có đáp án xác định được **và**
ít nhất một dấu hiệu dưới. Đủ thì agent làm và chạy kể cả chưa được yêu cầu — đây là bằng chứng (NT8),
không phải mở rộng phạm vi. Không dấu hiệu nào thì đọc code là xong (NT1).

| Máy hơn người ở | Dấu hiệu nhận ra |
|---|---|
| **Sức** | không gian đầu vào lớn · lặp nhiều lần — người chậm và sót vì mỏi |
| **Thiên kiến** | ca biên khó nghĩ ra hết — phần đắt giá là **liệt kê biên có hệ thống trước khi chạy**: rỗng · một phần tử · chạm giới hạn trên và dưới · trùng · ngoài dải · thứ tự đảo · hai sự kiện cùng lúc · frame đầu · object huỷ giữa chừng |
| **Tầm nhìn** | chứng minh **sự vắng mặt** (không còn tham chiếu, caller, tên cũ) · trạng thái nội bộ sai khi màn hình vẫn đúng · thứ chỉ lộ sau hàng nghìn vòng (rò rỉ, pool không trả về) |
| **Nhất quán chéo** | nhiều bản buộc khớp nhau (`code-design` › Bất biến) · hành vi trước–sau refactor phải trùng (NT6) |

**Sổ tay** — refactor phải giữ nguyên **chuyển động** (tween, trình diễn): quay trace mỗi frame ở cuối
frame với frame rate khoá (`Time.captureFramerate`), kịch bản gõ qua đúng đường input thật, ghi mọi thuộc
tính nhìn thấy của mọi entity; chứng minh trace **tất định** trước (hai lần quay mốc 0 chênh — so theo chữ
ký trạng thái đầu, vì thời gian nạp đổi số lần gọi `Random`) rồi mới so trước–sau. Chứng minh "không đổi",
không thay chơi thử. Công cụ: `docs/Editor/FeelTrace.md`; dự án chỉ viết kịch bản.

**Cheat — mở đường tới kịch bản, không mở nắp kỹ thuật.** Một lệnh chỉ chính đáng khi kịch bản **không tới
được bằng đường người chơi trong thời gian hợp lý**: thời gian (hết tuần) · khối lượng (đủ điểm mốc cuối) ·
trạng thái ban đầu (save mới). Thứ chơi là tới thì QA đi đường người chơi; mỗi nút là một dòng phải giữ
đúng mãi. Hàng đọc chỉ cho thứ màn hình không hiện, qua API sẵn có — không sinh property `Debug*` bọc thứ
đã public; lệnh riêng chỉ khi phải chạm `private`, và khi đó gọi đúng lệnh thật của hệ, không đi tắt qua
state. Plan liệt kê cheat theo tiêu chí này (`docs` › Plan).

> **Nền tảng** — tab Options của SRDebugger, một category mỗi hệ, **có trong build chính thức, không `#if`**
> vì QA kiểm trên bản người chơi cầm. Cách mở panel là của developer từng dự án; agent không viết. Trigger `Off`.

**Sổ tay** — partial `<Hệ>.Debug.cs` giữ lệnh (với tới `private`), plain class `<Hệ>DebugOptions` giữ hàng
UI nhận hệ qua constructor · `Init/Dispose` ở `Awake/OnDestroy` · container `internal sealed`, không public
gì ngoài hàng vì SRDebugger quét mọi public member · hàng đọc là `string` read-only · lệnh có tham số = một
property số + một method không tham số · `INotifyPropertyChanged` bắn `null` sau mỗi lệnh · chưa có save
thì hàng nói lý do thay vì NRE.
