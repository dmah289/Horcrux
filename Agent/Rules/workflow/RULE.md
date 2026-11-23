---
name: workflow
description: Dùng cho mọi tác vụ, từ lúc nhận việc tới báo cáo hoàn thành: phỏng vấn tới khi chốt kịch bản, đối chiếu code với developer, báo cáo, ngữ cảnh đã chốt, chưng cất tư tưởng, subagent, nghiệm thu đúng loại bằng chứng, đề xuất công cụ cho việc lặp lại, kỷ luật git.
---

# workflow — một việc, từ nhận việc tới báo cáo

Rule dưới `Agent/horcrux.md` (NT, tầng, ký hiệu định nghĩa ở đó).

Trình tự: **phỏng vấn tới khi chốt kịch bản** → **đối chiếu code với developer** → `reuse-gate` → làm theo kịch bản →
**nghiệm thu đúng loại** → **quét cuối chat**.

## Phỏng vấn — hỏi tới khi chốt kịch bản

Luật ở NT1: đoán thừa là hàm không caller, đoán thiếu là chữ ký chặn hướng dùng thật. Hỏi đủ năm nhóm, mỗi vòng gom câu hỏi
lại, **nhiều vòng tới khi hết chỗ mơ hồ**; nhóm đã biết chắc thì nêu giả định để xác nhận.

| Nhóm | Với tài liệu | Với code hoặc plan |
|---|---|---|
| **Ai dùng** | ai đọc, để làm gì, biết sẵn tới đâu | ai gọi, ở đâu, có caller thật **ngay bây giờ** chưa |
| **Mục tiêu** | đọc xong làm được gì | hành vi hay cảm giác gì, nghiệm thu bằng gì |
| **Ngân sách** | sâu, dài bao nhiêu | nhịp (NT3), hot path, platform |
| **Ranh giới** | giải ở đây hay trỏ đi | class này hay hệ khác; độc lập hay kết hợp; dự án hay Horcrux |
| **Hướng thật** | hệ sắp đổi gì | **chắc chắn** sắp cần gì; *có thể* cần thì chưa |

**Chốt bằng kịch bản viết lại:** *ai làm gì → hệ phản ứng gì → thấy gì* · ca biên đã thống nhất · thứ cố ý không làm ·
nghiệm thu bằng gì. Developer xác nhận mới làm; **chưa có câu trả lời thì chưa làm**.

- **Hai câu trả lời chọi nhau** thì hỏi lại đúng chỗ va kèm một ví dụ số; không tự chọn câu nào thắng.
- **Khó đảo ngược** (luôn hỏi): ghi đè hay xoá dữ liệu đã author (level, save, asset) · migration schema, wire format · thứ
  ngoài version control.
- **Hướng developer chưa nghĩ tới** thì nêu một dòng kèm giá, không tự đưa vào output; kéo sang hệ khác thì để "Mở rộng
  sau". Thứ developer tự nêu thì hỏi lại một lần để cân đắt–lợi, rồi theo developer.
- **Yêu cầu đổi vai của một khái niệm là đổi mô hình:** soát mọi thứ sinh ra từ vai cũ rồi đề xuất trên mô hình mới. Đổi
  tên mà giữ cơ chế cũ là chưa đổi.

## Đối chiếu với developer

Code cho biết cái gì chạy, không cho biết vì sao. Với **mỗi phát hiện ảnh hưởng quyết định đang bàn**: **thấy gì** (đường
dẫn, dòng) · **hiểu ý định là gì** — phát biểu lại, hỏi khớp thiết kế không · **developer biết chưa** — chủ ý hay đã trôi.
Không kể lại code. Mỗi lượt review đọc lại file hiện tại (NT10). **Sắp thay hay dựa vào một thứ có sẵn thì soát nó có chạy
thật không** — code gọi mà asset không vẽ thì không lỗi nào báo. **Hành vi đọc giá trị sống ngoài repo** (remote config,
server) thì bản đang chạy là bản ngoài: phát hiện nêu cả hai bản, sửa số trong repo chưa phải sửa hành vi.

## Báo cáo và trình bày

NT12 áp nguyên. Thêm: một câu một ý · thuật ngữ buộc dùng thì giải nghĩa lần đầu · gọi khái niệm **đúng tên trong code** ·
câu hỏi trả lời được không cần mở code · ví dụ kiểm trên code trước khi đưa · việc developer làm tay trong Editor gom một
mục cuối.

**Bày phương án rồi chốt một:** mỗi phương án một dòng *là gì · được gì · mất gì*, rồi chốt và nói vì sao thắng — tiêu chí
là hợp tám yêu cầu hệ thống nhất, không phải dễ làm nhất. Quyết định thuộc developer thì chỉ thu hẹp và nêu giá; tên gợi ý
chưa áp vào code hay tài liệu trước khi developer chốt.

**Ngữ cảnh đã chốt.** Plan đặt mục này trước `§0`, tài liệu nêu ở mở đầu: người dùng · mục tiêu · ranh giới · chỗ đặt ·
**thứ cố ý KHÔNG làm, kèm lý do** · **quyết định trái trực giác** · **"Mở rộng sau"** — hướng đã tính chưa làm, mỗi dòng
một hướng, kèm bản tạm và Plan nếu có.

## Chưng cất tư tưởng

Quyết định cho một bài toán cụ thể mang tư tưởng của developer, và trôi mất khi bài toán xong. **Bài học ghi ngay lúc vấp;
quét cuối chat là bắt buộc, không đợi nhắc:** duyệt cả chat, tìm rule, tư tưởng và bài học dùng chung cho game Unity từ thứ
developer yêu cầu, sửa, hoặc đồng ý. Mỗi dòng định ghi đi qua: **① khái quát** — tách tư tưởng khỏi quyết định riêng, kèm
vì sao; phép kiểm *đúng cho ít nhất hai loại game khác nhau không?* · **② cổng ghi** ở `horcrux.md` — loại nào, file nào,
quyền gì · **③ đối chiếu toàn bộ `Agent/`** — đã có thì thôi · trường hợp riêng thì trỏ về · gộp được vào luật đang có
thì gộp · mâu thuẫn thì nêu để developer phân xử.

**Code thật lệch Plan:** *lỗi* → báo kèm bằng chứng · *trôi* (tên, thứ tự, chi tiết không mang lý do) → sync Plan theo
code · *tư tưởng* (đổi hình dạng, ranh giới, kiểu, cách tiêm) → qua cổng ghi.

Chỉ khái quát lựa chọn có "vì sao" lặp lại được; hằng số, tên, phạm vi một task thì không. Dòng ghi vào `Agent/` **không
mang domain dự án** (NT8), theo `docs` › Agent, không bao giờ vào mục developer. Báo cáo kết bằng khối *Đã ghi / Đề xuất /
Về bạn / Không ghi* của `horcrux.md`.

## Subagent

Subagent là ngữ cảnh trắng; để nó tự tìm hiểu là thuế đọc nhân theo số subagent. Prompt **tự chứa như một giai đoạn của
Plan**: trích đoạn tài liệu cần, đường dẫn file sẽ chạm, tiêu chí nghiệm thu; thiếu thì báo về, không tự đi đọc — trừ
**code nó sẽ sửa**.

**Chạy Plan bằng subagent:** hỏi gộp một lần trước task đầu (nhánh, commit mỗi task, điểm dừng) rồi chạy liền · mỗi
subagent commit task của nó bằng pathspec · mọi bước chơi thử và duyệt art gom thành một kịch bản cuối · review mỗi task
loại commit ngoài Plan chen giữa.

## Nghiệm thu — phép kiểm theo loại tiêu chí

Neo khi phân vân: **công sức đắt nhất là của developer.**

| Tiêu chí | Bằng chứng | Ai chạy |
|---|---|---|
| thuật toán tất định, công thức, parser, serialize | phép kiểm chạy được, đi qua đúng cửa hệ dùng thật | agent |
| tên, tham chiếu, đồng bộ tài liệu | grep quét | agent |
| hiệu năng | số trước–sau tại chỗ đo | agent |
| đúng–sai xác định được mà làm tay chậm, sót | vét cạn | agent, **tự đề xuất** |
| cảm giác chơi, nhịp, độ khó, hình ảnh | **chơi thử** | **developer** |
| kịch bản hành vi trên build | chơi qua kịch bản, cheat rút đường | developer, QA |

- **Phép kiểm phải đỏ được:** mỗi bước của thuật toán có ít nhất một case đỏ khi bỏ bước đó; chạy đối chứng một lần, ghi
  kết quả cạnh case. Case đỏ oan dạy người đọc bỏ qua màu đỏ.
- **Số đo nằm trên đúng vật được hỏi**, ở đúng điều kiện nơi dùng thật; mắt developer trái số đo thì nghi chỗ đo trước.
- **Cảm giác — dừng và giao, không dựng proxy.** Báo *"cần chơi thử"* kèm kịch bản: *vào đâu* · *làm gì* (chuỗi ngắn nhất) ·
  *nhìn gì* · *khác trước ra sao* · *dấu hiệu hỏng*. Thứ developer chơi là cảm được thì agent không vào Play qua công cụ;
  Play qua công cụ chỉ cho thứ developer **không cảm được** (trace, số đo, trạng thái nội bộ — `feel-tune`), và trả nguyên
  save của developer sau khi chạy.
- **Hướng dẫn developer tự làm là output phải nghiệm thu:** mỗi giá trị, thứ tự, tên ô đã dựng thử trên engine.
- **Vét cạn** khi có đáp án xác định được và ít nhất một dấu hiệu — agent làm kể cả chưa được yêu cầu, vì đây là bằng
  chứng: *sức* (không gian đầu vào lớn) · *thiên kiến* (ca biên khó nghĩ hết) · *tầm nhìn* (chứng minh **vắng mặt**) ·
  *nhất quán chéo* (nhiều bản buộc khớp). Không dấu hiệu nào thì đọc code là xong. **Đổi mô hình của hệ thì chạy lại mọi
  dòng kịch bản đã chốt** trên dữ liệu asset thật.
- **Cheat mở đường tới kịch bản, không mở nắp kỹ thuật:** chỉ khi kịch bản không tới được bằng đường người chơi trong thời
  gian hợp lý; gọi đúng lệnh thật của hệ, có trong build chính thức.

## Việc lặp lại và git

**Việc lặp lại thì đề xuất công cụ ngay lượt đó** (lần thứ hai làm cùng chuỗi thao tác, hoặc biết sẽ làm lại): một dòng
*việc gì · lặp mấy lần · công cụ trông thế nào · chỗ đặt · công viết · tiết kiệm mỗi lần*. Mặc định đặt ở Horcrux (tool
Editor, script `Tools~`, skill); dự án chỉ giữ lớp nối. Developer đồng ý thì viết công cụ, tài liệu, cập nhật skill hoặc mở skill mới kèm hàng trong bảng chỉ dẫn.

**Git:** commit chỉ mang phần của việc mình, không đụng thứ developer đã stage, không `stash` khi còn đường khác · commit
trong Horcrux thì commit luôn con trỏ submodule ở dự án · sau một lượt chờ dài, đọc lại trạng thái trước bước ghi ra ngoài
(push, commit, pop stash) · việc áp nhiều bước Plan chia commit theo nhóm bước, theo thứ tự phụ thuộc, tên commit ghi
bước, mỗi commit compile sạch; bước phụ thuộc Editor để lại và nêu ở mục Editor của báo cáo.

## Tra cứu — `workflow/reference.md`

| Chạm | Mục |
|---|---|
| commit khi working tree có sửa dở, submodule, developer thao tác git song song | Git |
| phỏng vấn, đọc code, asset, cấu hình để đối chiếu | Đối chiếu |
| viết test, đo, Play qua công cụ, vét cạn, hướng dẫn developer, chuỗi eval lặp lại | Nghiệm thu |
| viết cheat, lệnh debug, panel cho QA | Cheat |
