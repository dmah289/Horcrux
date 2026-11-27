# horcrux.md — cửa vào: định nghĩa, bảng chỉ dẫn, tư tưởng thiết kế

Bộ quy tắc và kinh nghiệm phát triển hệ thống game Unity, mang nguyên sang mọi dự án, không mang domain dự án nào; áp cho
mọi thứ agent viết ra (runtime, Editor, tooling, script tạm, tài liệu). **Horcrux** là framework dùng chung đi theo mọi dự
án; đường dẫn tính từ gốc Horcrux: bộ này `Agent/`, Plan `Plans/`, tài liệu hệ `docs/Runtime/<Hệ>/` và `docs/Editor/`.

## Các loại

| Loại | Là gì | Chỗ | Quyền ghi |
|---|---|---|---|
| **Rule** | ràng buộc — một **lựa chọn** của ta | file này · `Rules/<miền>/RULE.md` | developer duyệt |
| **Skill** | tư tưởng và công cụ cho **một loại việc** có đầu có cuối | `Skills/<việc>/SKILL.md` | developer duyệt |
| **Reference** | chi tiết đã ổn định của một rule, skill (Nền tảng, Sổ tay) | `reference.md` cạnh `RULE.md`, `SKILL.md` | qua thang nâng cấp |
| **Memory** | **bài học đã trả giá**, có thể cũ đi | `Memory/<miền hoặc skill>.md` | agent ghi thẳng |
| **Hồ sơ developer** | developer là ai, làm việc thế nào | mục cuối file này | chỉ developer |

Thứ chỉ đúng cho một hệ, một dự án, một máy không vào `Agent/`. Mỗi điều định nghĩa ở **một** nơi; nơi khác trỏ sang, không
chép lại. Ghi gì vào đâu, hình dạng và vòng đời memory: `docs` › Agent.

## Luật đọc

1. Vào session, **trước mọi việc kể cả hàng bảng chỉ dẫn**: đọc **trọn** file này, `Rules/workflow/RULE.md`,
   `Rules/reuse-gate/RULE.md`, `Memory/workflow.md`, `Memory/reuse-gate.md`, và ở gốc dự án
   `docs/agent-memory/workflow.md`, `docs/agent-memory/reuse-gate.md` nếu có.
2. Mỗi tác vụ tra **bảng chỉ dẫn**: rule và skill hàng đó gọi thì **đọc trọn trước khi chạm miền**, kèm memory cùng tên ở
   hai tầng. Cột *Bắt buộc* tuân thủ từng luật, *Thêm khi chạm* áp khi chạm tới; nhiều hàng thì gộp.
3. `reference.md` **không đọc sẵn**. File có nó kết bằng bảng **Tra cứu** (*chạm gì → mục nào*): việc chạm tới thì **đọc
   trọn mục đó trước khi làm**.
4. Không chắc việc đang làm có chạm một miền không thì đọc rule của miền đó — đọc thừa rẻ hơn phạm luật.

## Rule và skill

**Rule:** `workflow` (quy trình một việc, nghiệm thu, git) · `reuse-gate` (có sẵn chưa, có cần không, đặt ở đâu, ai gõ) ·
`code-design` (SOLID, module, hiệu năng, bất biến, naming) · `async-presentation` (async, vòng đời, tween, trình diễn) ·
`editor-first` (việc của Editor, thao tác Editor qua công cụ, tool Editor, import/export) · `math-physics` (toán trong hệ) ·
`docs` (tài liệu, kỷ luật viết, ghi tri thức và memory). **Skill:** `feel-tune` · `build-size` · `compile-check` ·
`ui-reskin` · `demo-compare` · `merge-audit`.

## Bảng chỉ dẫn

| Loại việc | Bắt buộc | Thêm khi chạm |
|---|---|---|
| Mọi tác vụ — đầu chat | `workflow`, `reuse-gate`, NT7 (săn thứ đáng module hoá) | |
| Cuối chat; vừa vấp hay được nhờ ghi điều đáng ghi — sau Luật đọc 1 | `docs` › Agent | |
| Sinh code mới, hệ mới, tính năng mới | `reuse-gate` → `code-design` | `async-presentation` khi có async, tween, vòng đời · `editor-first` khi có cấu hình, wire, tool |
| Sửa lỗi, refactor code runtime | `code-design` | `async-presentation` · `feel-tune` › Chọn công cụ khi phải tái hiện hay đọc trạng thái trong Play |
| Viết code, tài liệu, Plan vào Horcrux | NT8, `reuse-gate` › Đặt ở đâu | `code-design` › Module · `docs` |
| Developer giao kiểm kịch bản hành vi trong Play qua công cụ | `feel-tune` › Chọn công cụ | `workflow` › Nghiệm thu |
| Cheat, lệnh debug cho QA | `workflow` › tra cứu › Cheat | `code-design` |
| Tool Editor, import/export, cấu hình nội dung, setup scene và prefab; thao tác Editor qua MCP, eval | `editor-first` | `code-design` |
| Nhận gói hoạ sĩ (Spine JSON, `.unitypackage`), thay anim, hiệu ứng | `editor-first` › tra cứu › Gói asset từ ngoài; `docs/Editor/` SpineJson, UnityPackageAudit, PrefabYaml | `feel-tune` khi khớp nhịp code với anim |
| Hệ có công thức toán, vật lý | `math-physics`, `code-design` | `docs` khi phải dẫn giải |
| Viết hoặc cập nhật tài liệu, Plan, Manual, file trong `Agent/` | `docs` | `math-physics` khi có công thức |
| Chỉnh feeling, đo GC và batches lúc trình diễn | `feel-tune` | `async-presentation` |
| Dựng, reskin UI hay art world theo demo; prefab UI; thêm art, atlas | `ui-reskin`, `editor-first` | `code-design` khi viết script UI · `build-size` khi đo byte trên build · `demo-compare` khi so với demo |
| So màn với demo, ảnh máy thật; bố cục trên máy khác Editor; xem trên máy trước khi build; dựng anim, hiệu ứng theo video demo | `demo-compare` | `ui-reskin` khi sửa prefab UI · `editor-first` khi chỉnh scene, prefab · `feel-tune` khi chỉnh tween theo số đo |
| Build nặng, giảm size | `build-size` | |
| Kết thúc task sửa code C#, trước khi báo xong hay commit | `compile-check` | |
| Merge nhánh, `git pull` ra merge commit, giải conflict, soát merge người khác đẩy lên | `merge-audit`, `compile-check` | `editor-first` khi ghép prefab, scene |
| Orchestrator bơm ngữ cảnh cho subagent | hàng của việc được giao (`workflow` › Subagent) | |

## Tầng ràng buộc

| Tầng | Nhận biết | Nghĩa | Agent được phép |
|---|---|---|---|
| **Luật** | nội dung `RULE.md`, `SKILL.md` | tiêu chí nghiệm thu: **cần đạt gì**, không phải trình tự | không bỏ; tự chọn cách đạt (NT9) |
| **Nền tảng** | khối `> **Nền tảng**` | giới hạn thật của Unity, C#, renderer, hoặc stack Horcrux | không đi đường khác; dự án thiếu món nào thì quay về Luật |
| **Sổ tay** | `**Sổ tay** —` | một cách đã chạy được | thay bằng cách hay hơn, kèm lý do và phép kiểm (NT9) |
| **Kinh nghiệm** | khối `###` trong memory | lỗi đã trả giá, ghi theo **dấu hiệu** | dùng để nhận ra ca tương tự; không phải luật mới |

**Stack Horcrux:** InitArgs, UniTask, PrimeTween, Addressables. Công cụ ngoài stack chỉ ghi dạng *"khi dự án dùng X"*.
**Ký hiệu:** `NT<n>` ở file này · `` `tên` › Mục `` trỏ `RULE.md` hoặc `SKILL.md` của tên đó · `` `tên` › tra cứu › Mục ``
trỏ `reference.md` · `mục X` trong cùng file. Số hiệu và con trỏ chỉ sống trong `Agent/`; code comment, tài liệu, Plan
viết lý do bằng nội dung.

---

## Tám yêu cầu hệ thống — ngang nhau

Không yêu cầu nào thắng mặc định. Va nhau thì dùng câu ở bảng **Phân xử**; bảng không có thì hỏi developer. Hy sinh cái nào
thì nói tại chỗ kèm giá.

**NT1 · Hỏi tới khi rõ, không đoán rồi làm.** Yêu cầu chưa rõ hay trình bày thiếu thì **hỏi — nhiều câu, nhiều vòng** — tới
khi viết lại được **kịch bản** developer xác nhận: ai làm gì, hệ phản ứng gì, thấy gì, ca biên, nghiệm thu bằng gì
(`workflow` › Phỏng vấn). Kịch bản đã chốt là **hợp đồng khi triển khai**: mỗi hành vi truy về một dòng kịch bản; thứ kịch
bản không nói thì hỏi, không tự thêm; cần lệch thì hỏi trước. Giả định chỉ vá khe hở **nhỏ, đảo ngược được**, ghi
`Giả định (cần xác nhận): …` tại chỗ dùng; thao tác khó đảo ngược luôn hỏi. Quyết định **thuộc developer** thì thu hẹp và
nêu giá, không chốt hộ: phạm vi · tên và thuật ngữ · ba ranh giới cứng (NT9) · chỗ đặt dự án hay Horcrux · thứ developer
đã quyết.

**NT2 · Vừa đủ — lõi chạy được trước, mở để thêm, tối ưu dần.** Mỗi lớp phức tạp trả bằng **một nhu cầu đang có thật**;
"phòng khi", "cho đầy đủ", "chuẩn hơn" không phải nhu cầu. Phép kiểm: *xoá đi thì hỏng ở đâu?* Giá một dòng code nằm ở mọi
lần đọc sau — *số file phải mở để lần hết một luồng*, kể cả asset kéo thả. Hai bờ vực: **xé vụn** (tính năng rải nhiều
mảnh, không mảnh nào nói được hành vi) và **gom bừa** (class ôm nhiều lý do thay đổi). **Thứ tự:** ① lõi chạy được, kiểm
được từ input tới kết quả quan sát được → ② ca biên khi có input thật, cấu hình khi có call site → ③ tối ưu làm khó đọc khi
có số đo. Lõi trước cắt chiều sâu, không cắt mục đích. **Mở để thêm** là hình dạng: bước kế tiếp là *thêm vào*, không đập
ra — chỉ phòng xa thứ sửa sau rất đắt (chữ ký bắt buộc, ranh giới trách nhiệm, chiều phụ thuộc, chỗ đặt file). Nghiệm thu:
gọi được tên bước kế tiếp và nó là "thêm".

**NT3 · Hiệu năng — bộ nhớ và thời gian, mỗi phép tính khai được nhịp:** *mỗi frame* · *mỗi tương tác* · *mỗi lần dữ liệu
đổi*. Đặt ở nhịp nhanh hơn cần thì không gì báo sai, chỉ có hệ chậm dần. Tối ưu **không làm khó đọc** (đúng nhịp, không rác
ở chỗ lặp, không tính lại thứ không đổi) thì làm luôn, không cần đo. Tối ưu **làm khó đọc** (cache, bẻ cấu trúc dữ liệu,
vòng lặp viết tay) cần **chỗ đo và số trước–sau**, chỉ ở hot path đã xác nhận. Rẻ nhất là làm phép tính **biến mất**.

**NT4 · Module hoá — OOP, SOLID, mang đi được.** Mỗi class một lý do thay đổi, phụ thuộc nhận vào, ranh giới hệ thấy được
trong cấu trúc dự án (`code-design`). Hệ **độc lập** là mặc định: *bê sang dự án sau phải sửa gì?* — **không gì**; không gọi
tên type dự án, phụ thuộc một chiều **dự án → Horcrux**. Hệ **kết hợp** là ngoại lệ phải nêu lý do và đường tách.

**NT5 · Editor-first — code lo hành vi, Editor lo cấu hình và kết nối.** Thứ **làm chắc chắn được lúc authoring** (gán
reference, đặt số, dựng hierarchy, tạo asset) thì làm trong Editor, không sinh code; phép kiểm là *chắc chắn*, không phải
*tiện*. Code chỉ để tìm, nối, gán thứ đã có lúc authoring là đặt sai chỗ. Việc bắt buộc trước khi build là **một bước trong tài
liệu module**, không phải code canh (dò setup lúc chạy) hay code tự tạo thứ thiếu — tự tạo tệ hơn tự canh vì giấu setup
thiếu; chỗ dùng gặp thiếu thì xử theo bảng guard (`code-design` › Bất biến). Ngoại lệ ở *thời điểm biết được* (spawn runtime, số lượng động, dữ liệu server).

**NT6 · Có sẵn thì dùng — Horcrux trước, rồi dự án.** Khảo sát trước khi viết (`reuse-gate`); script tạm, eval, công cụ đo
cũng là code. Đáp ứng → dùng lại · gần đúng → mở rộng bằng **thêm** · phải bẻ cong bài toán → viết mới. Bản Horcrux chưa
hợp thì **đề xuất cải tiến Horcrux ngay lượt đó và áp trong cùng việc**, theo quyền ở `reuse-gate` › Đặt ở đâu; không lặng
lẽ viết bản thứ hai ở dự án. **Thiếu công cụ thì mở rộng công cụ trong Horcrux** rồi cập nhật skill, không viết script dùng
một lần bên cạnh. **Một tri thức một nơi** (code lẫn tài liệu) — áp cho tri thức trùng, không cho code trông giống (bảng
Phân xử).

**NT7 · Săn thứ đáng module hoá — đề xuất, developer quyết.** Ở mọi lượt (viết, sửa, review, trả lời), với mỗi chức năng
hay phần logic chạm tới, hỏi: *gói được thành hàm, class, service gọi lại không?* · *bê sang dự án khác dùng nguyên không?* ·
*code hay Plan sẵn có của Horcrux tối ưu thêm được không?* Có thì **đề xuất ngay lượt đó** kèm lý do, được–mất, use case
(`reuse-gate` › Đặt ở đâu); im lặng viết vào dự án thứ mang đi được là vi phạm, vì dời chỗ đặt sau là đập cả cây phụ thuộc.
Đề xuất chưa phải quyết.

**NT8 · Horcrux là hệ base — không mang phạm vi dự án vào.** Code, tài liệu, Plan, skill viết cho Horcrux không gọi tên
type, từ vựng, kịch bản hay con số của dự án nào. **Framework sở hữu cơ chế, dự án sở hữu từ vựng:** thứ dự án đặt tên đi
vào qua type parameter, tham số, hoặc lớp nối nằm ở dự án. Phép kiểm: *dự án thứ hai, khác thể loại, dùng nguyên được
không — có phải truyền giá trị giả hay nhận lỗi giả không?* Nhu cầu chỉ một dự án có thì ở lại lớp nối.

### Phân xử

| Va nhau | Câu hỏi | Kết quả |
|---|---|---|
| NT2 × NT3 | bản nhanh có khó đọc hơn không? | không → làm luôn · có → chờ số đo |
| NT2 × NT4 | thứ phải thêm là gì? | chiều phụ thuộc, chỗ đặt file → làm ngay · interface, adapter, tham số cho người dùng *chưa có* → bỏ |
| NT2 × NT7 | chỗ đặt hay trừu tượng hoá? | **chỗ đặt** xét theo *mang đi được không* · **trừu tượng hoá** xét theo *người dùng thứ hai đang có thật* |
| NT6 × NT2 | hai đoạn có cùng lý do thay đổi không? | cùng → gộp · khác → **để lặp** · chưa chắc → để lặp, lặp rồi gộp rẻ hơn trừu tượng sai · mấp mé → hỏi. Ngoại lệ bắt buộc gộp: hai bản buộc khớp ở runtime (`code-design` › Bất biến) |
| NT6 × NT8 | cái thiếu ở Horcrux là cơ chế hay từ vựng dự án? | cơ chế → cải tiến Horcrux · từ vựng, kịch bản → lớp nối ở dự án |
| NT5 × wiring không grep được | số chỗ nối là hằng nhỏ hay tăng theo instance? | hằng nhỏ → kéo thả · tăng theo instance → đăng ký bằng code, gom n ca hỏng-im-lặng thành một dòng đỏ lúc boot |
| NT1 × tiến độ | khe hở có nhỏ và đảo ngược được không? | có → giả định có nhãn, làm tiếp · không → dừng và hỏi |

---

## Kỷ luật làm việc

**NT9 · Phạm vi bàn được, cách làm luôn mở.** Tiêu chí đã chốt thì cách đạt là của người triển khai; cách đơn giản, nhanh,
rõ hơn thì dùng, kèm lý do và phép kiểm. **Ba ranh giới cứng** — tự do chỉ khi cả ba không đổi: **hành vi quan sát được**
(kể cả ngẫu nhiên theo seed, chữ người chơi thấy) · **dữ liệu ghi ra** (format, giá trị) · **API công khai**; đụng một là
đổi phạm vi, phải hỏi. Biết cách tốt hơn mà im lặng chọn Sổ tay là vi phạm.

**NT10 · Bằng chứng, không khẳng định suông.** Mọi "tại sao" kèm phép kiểm tái lập được; không viết "đã đúng", "đã tối ưu"
thiếu mốc hay số đo. **Bản trong trí nhớ, prompt, lượt trước là bản chết:** tên file, chữ ký, hằng số, nhãn UI, phát hiện
review đều **mở file hiện tại đối chiếu** trước khi ghi. **Bằng chứng cùng loại với tiêu chí** (`workflow` › Nghiệm thu);
tiêu chí cao nhất là **cảm giác chơi** — công thức "sai sách" mà chơi đã tay thì đúng, và chỉ developer chơi thử được.

**NT11 · Config chưa chốt thì không phải mốc.** Số trong asset, bảng cấu hình, remote config là **nháp của developer** tới
khi developer nói chốt. Đổi đơn vị, trục, công thức: không tự ánh xạ giá trị cũ, không tự dựng đối chiếu để chứng minh "cảm
giác không đổi"; giữ **hành vi thuật toán** bất biến, chứng minh bằng mốc, rồi hỏi giá trị cũ là nháp hay mốc. Chuẩn hoá là
**bỏ** neo, không chọn neo tốt hơn; **mẫu số là đại lượng gốc**, không phải đại lượng đã bị núm khác nhân vào.

**NT12 · Viết vừa đủ · kết luận trước · chỉ một hệ.** **Sàn:** đủ để người đọc tự làm việc của họ — *đang có*, *sẽ đổi*,
*đánh đổi*, kèm số hay đường dẫn để kiểm. **Trần:** *dòng này đổi gì trong việc người đọc?* — không thì cắt. **Kết luận
trước, lý do sau; một khẳng định = một câu lý do.** **Đổi rồi thì chỉ còn một hệ:** không "trước đây là", không hai đơn vị
song song, không tên cũ làm cầu — trừ khi gọi tên được người dùng thật (payload cũ phải deserialize, developer yêu cầu).
Lịch sử ở commit. Chi tiết: `docs` › Kỷ luật viết.

---

## Thứ tự khi va chạm

1. **Yêu cầu của developer** ở mục cuối; va nhau thì hỏi.
2. **NT1–NT12**.
3. **Luật trong rule và skill.** Hai nơi nói khác nhau là lỗi của bộ này: làm theo bản hợp NT hơn, nêu để developer phân
   xử, không tự sửa cả hai.
4. **Memory** không thắng luật: bài học trái luật thì nêu để developer phân xử.

## Yêu cầu của developer

Mục này **developer tự gõ**. Agent **không sửa, không định dạng lại, không chèn, không xoá** ký tự nào giữa hai dấu dưới —
kể cả khi thấy lỗi, dòng trùng, mâu thuẫn: chỉ báo trong chat.

<!-- DEVELOPER-ONLY: START — agent không sửa gì từ dòng này tới END -->

- Bất kỳ skill nào được thêm vào đều chỉ nên màng tư tưởng, không quá cụ thể, tránh khiến agent triển khai cứng nhắc.

<!-- DEVELOPER-ONLY: END -->
