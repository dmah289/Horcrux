---
name: workflow
description: Dùng cho mọi tác vụ, từ nhận việc tới báo cáo: phỏng vấn, đối chiếu, báo cáo, chưng cất, subagent, nghiệm thu, công cụ, git.
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
nghiệm thu bằng gì. Developer xác nhận mới làm; **chưa có câu trả lời thì chưa làm** (khe hở nhỏ, đảo ngược được: NT1).

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
Không kể lại code.

- **Sắp thay hay dựa vào một thứ có sẵn thì soát nó có chạy thật không** — code gọi mà asset không vẽ thì không lỗi nào báo.
- **Hành vi đọc giá trị sống ngoài repo** (remote config, server) thì bản đang chạy là bản ngoài: phát hiện nêu cả hai bản,
  sửa số trong repo chưa phải sửa hành vi.
- **File đổi giữa phiên mà mình không chạm** thì đọc `git log -- <file>` và `git status` trước khi gán nguyên nhân: developer
  và session khác sửa song song, bản họ đã commit thắng bản mình; Plan sắp viết lại theo bản đó thì báo phần sẽ đổi trước.

## Báo cáo và trình bày

Viết theo NT12 và `docs` › Kỷ luật viết, thêm: một câu một ý · thuật ngữ buộc dùng thì giải nghĩa lần đầu · gọi khái niệm
**đúng tên trong code** · câu hỏi trả lời được không cần mở code · ví dụ kiểm trên code trước khi đưa · **việc developer làm
tay trong Editor gom một mục cuối** (kể cả bước công cụ bị từ chối, bước phụ thuộc Editor để lại).

**Bày phương án rồi chốt một:** mỗi phương án một dòng *là gì · được gì · mất gì*, rồi chốt và nói vì sao thắng — tiêu chí
là hợp tám yêu cầu hệ thống nhất, không phải dễ làm nhất. Quyết định thuộc developer thì chỉ thu hẹp và nêu giá; tên gợi ý
chưa áp vào code hay tài liệu trước khi developer chốt.

**Ngữ cảnh đã chốt.** Tài liệu nêu ở mở đầu, đề xuất nêu trước khi viết Plan: người dùng · mục tiêu · ranh giới · chỗ đặt ·
**thứ cố ý KHÔNG làm, kèm lý do** · **quyết định trái trực giác** · **"Mở rộng sau"** — hướng đã tính chưa làm, mỗi dòng
một hướng, kèm bản tạm và Plan nếu có.

## Chưng cất tư tưởng

Quyết định cho một bài toán mang tư tưởng của developer, và trôi mất khi bài toán xong. **Bài học ghi ngay lúc vấp; quét
cuối chat là bắt buộc, không đợi nhắc** (`Agent/Tools~/context_audit.py digest`): tìm tư tưởng, bài học dùng chung từ thứ
developer yêu cầu, sửa, hoặc đồng ý; tách khỏi quyết định riêng, kèm vì sao, phép kiểm *đúng cho ít nhất hai loại game khác
nhau không?* — rồi ghi theo `docs` › Agent.

## Subagent

Subagent là ngữ cảnh trắng: prompt **tự chứa như một giai đoạn của Plan** — trích đoạn tài liệu cần, đường dẫn file sẽ chạm,
tiêu chí nghiệm thu; thiếu thì báo về, không tự đi đọc — trừ **code nó sẽ sửa**.

**Chạy Plan bằng subagent:** hỏi gộp một lần trước task đầu (nhánh, commit mỗi task, điểm dừng) rồi chạy liền · mỗi
subagent commit task của nó bằng pathspec · mọi bước chơi thử và duyệt art gom thành một kịch bản cuối · review mỗi task
loại commit ngoài Plan chen giữa.

**Soát chéo khi bằng chứng là phán đoán** (khớp ảnh với demo, review diff, soát merge, duyệt Plan): agent làm đã tin kết
luận, đọc lại chỉ thấy điều xác nhận nó. Subagent soát nhận **vật** (diff, ảnh, file, tiêu chí), không nhận kết luận của
agent làm; agent làm phân xử từng phát hiện bằng bằng chứng, nêu cả phát hiện bác bỏ.

## Nghiệm thu — phép kiểm theo loại tiêu chí

Neo khi phân vân: **công sức đắt nhất là của developer.**

| Tiêu chí | Bằng chứng | Ai chạy |
|---|---|---|
| thuật toán tất định, công thức, parser, serialize | phép kiểm chạy được, đi qua đúng cửa hệ dùng thật | agent |
| tên, tham chiếu, đồng bộ tài liệu | grep quét | agent |
| hiệu năng | số trước–sau tại chỗ đo | agent |
| đúng–sai xác định được mà làm tay chậm, sót | vét cạn | agent, **tự đề xuất** |
| cảm giác chơi, nhịp, độ khó, hình ảnh | **chơi thử** | **developer** |
| kịch bản hành vi trên build | chơi qua kịch bản, cheat rút đường | developer, QA; agent khi máy thật nối được (`feel-tune`) |

- **Phép kiểm phải đỏ được:** mỗi bước của thuật toán có ít nhất một case đỏ khi bỏ bước đó, chạy đối chứng một lần, ghi kết
  quả cạnh case; case đỏ oan dạy người đọc bỏ qua màu đỏ. Lỗi chập chờn thì đối chứng với bản chưa sửa trên cùng máy, nhiều lượt mỗi bên, báo
  tỉ lệ hỏng.
- **Số đo nằm trên đúng vật được hỏi**, ở đúng điều kiện nơi dùng thật; mắt developer trái số đo thì nghi chỗ đo trước.
- **Cảm giác — dừng và giao, không dựng proxy.** Báo *"cần chơi thử"* kèm kịch bản: *vào đâu* · *làm gì* (chuỗi ngắn nhất) ·
  *nhìn gì* · *khác trước ra sao* · *dấu hiệu hỏng*. Play qua công cụ chỉ cho thứ developer **không cảm được** (trace, số
  đo, trạng thái nội bộ — `feel-tune`).
- **Hướng dẫn developer tự làm là output phải nghiệm thu:** mỗi giá trị, thứ tự, tên ô đã dựng thử trên engine.
- **Vét cạn** khi có đáp án xác định được và ít nhất một dấu hiệu — agent làm kể cả chưa được yêu cầu, vì đây là bằng
  chứng: *sức* (không gian đầu vào lớn) · *thiên kiến* (ca biên khó nghĩ hết) · *tầm nhìn* (chứng minh **vắng mặt**) ·
  *nhất quán chéo* (nhiều bản buộc khớp). Không dấu hiệu nào thì đọc code là xong. **Đổi mô hình của hệ thì chạy lại mọi
  dòng kịch bản đã chốt** trên dữ liệu asset thật.
- **Cheat mở đường tới kịch bản, không mở nắp kỹ thuật** (tra cứu › Cheat). Agent tự dùng cheat để tới nhanh trạng thái cần
  kiểm khi cheat **không đổi chính thứ đang kiểm**; báo cáo nêu cheat nào và vì sao nó không chạm kết quả.

## Việc lặp lại, công cụ và git

Skill nói việc nào dùng tool nào; tool thiếu thì mở rộng theo NT6. **Việc lặp lại thì đề xuất công cụ ngay lượt đó** (lần thứ hai làm cùng chuỗi thao
tác, hoặc biết sẽ làm lại): một dòng *việc gì · lặp mấy lần · công cụ trông thế nào · chỗ đặt · công viết · tiết kiệm mỗi
lần*. Mặc định đặt ở Horcrux (tool Editor, script `Tools~`, skill); dự án chỉ giữ lớp nối. Developer đồng ý thì viết công cụ, tài
liệu, cập nhật skill hoặc mở skill mới kèm hàng trong bảng chỉ dẫn.

**Ngân sách ngữ cảnh khi dùng công cụ:** đọc kết quả tool đã tóm tắt, không đổ log, JSON, diff thô hay ảnh cỡ gốc vào ngữ
cảnh · một lần đo là một lệnh chạy cả kịch bản; đi từng bước qua MCP chỉ khi còn đang dò mình cần nhìn gì · đọc output thẳng, không pipe qua `tail`, `head`
(lệnh bị đẩy nền thì không biết kẹt ở đâu) · lệnh dài hơn một phút chạy nền, không đọc khi chưa xong · không chạy song song
hai lệnh cùng chiếm Editor.

**Git:** commit chỉ mang phần của việc mình (pathspec), không đụng thứ developer đã stage, không `stash` khi còn đường
khác · commit trong Horcrux thì commit luôn con trỏ submodule ở dự án · sau một lượt chờ dài, đọc lại trạng thái trước bước
ghi ra ngoài (push, commit, pop stash) · việc áp nhiều bước Plan chia commit theo nhóm bước, theo thứ tự phụ thuộc, tên
commit ghi bước, mỗi commit compile sạch.

## Tra cứu — `workflow/reference.md`

| Chạm | Mục |
|---|---|
| commit khi working tree có sửa dở, submodule, developer thao tác git song song | Git |
| phỏng vấn, đọc code, asset, cấu hình để đối chiếu | Đối chiếu |
| viết test, đo, Play qua công cụ, vét cạn, hướng dẫn developer, chuỗi eval lặp lại | Nghiệm thu |
| viết cheat, lệnh debug, panel cho QA | Cheat |
