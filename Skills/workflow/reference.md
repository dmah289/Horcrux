# workflow — tra cứu

Nền tảng, Sổ tay và bẫy của `workflow`. Đọc mục được bảng Tra cứu trong `SKILL.md` gọi.

## Git

- **Con trỏ submodule** là thứ duy nhất ghi dự án đi với bản Horcrux nào, kể cả khi chỉ đổi skill, tài liệu hay Plan: bỏ
  lại thì máy khác, nhánh khác, phiên agent sau đọc bản cũ, và `git status` luôn bẩn che thay đổi thật. Submodule hay đứng ở
  detached HEAD: commit xong thì đưa nhánh lên commit đó, không để commit không nhánh nào giữ.
- **File trùng với sửa dở của developer** thì tách theo hunk qua một index tạm; soát `git show --stat` sau mỗi commit.
  Commit theo pathspec lấy nội dung working tree, bỏ qua phần đã tách vào index. Công cụ: `docs/Editor/PartialCommit.md`.
  Merge khi index có file đã stage: `merge-audit`.
- **Developer thao tác git song song** (app GUI pull, push, trả stash): trước bước ghi ra ngoài đọc lại `status`, `reflog`,
  nhánh remote — trạng thái đọc trước lượt chờ là bản chết.
- Vào Play làm đổi file không thuộc task (config, scene khởi động, xuống dòng): stage theo danh sách file của task.

## Đối chiếu

- **Hai câu trả lời chọi nhau:** ví dụ số là một ca mà hai cách hiểu cho hai kết quả khác nhau — hỏi lại bằng lời trừu
  tượng chỉ nhận về hai câu cũ.
- **Chữ người chơi thấy là hành vi quan sát được:** đổi logic không kéo theo đổi nhãn, kể cả khi nhãn cũ nghe lệch nghĩa —
  nêu một dòng.
- **Đổi vai của một khái niệm:** thứ sinh ra từ vai cũ gồm cờ, khoá, tên field, nhánh riêng.
- Số đọc từ hai file YAML chỉ so được khi quy về một kiểu, hoặc đọc qua API engine.
- Code có nhánh `#if` thì nhánh đang chạy đọc trên assembly đã compile, không suy từ source: define toàn cục và define theo
  phiên bản package chồng nhau chọn nhánh khác điều source gợi ý.
- Hiệu ứng có sẵn mà asset không vẽ (object tắt, thiếu sprite, sai lớp): nêu trước khi thiết kế bản mới đứng trên nó.
- Giá trị sống ngoài repo: bản trong repo chỉ là lưới đỡ; nghiệm thu giao developer đối chiếu bằng công cụ so.
  *Bẫy:* hạ một hệ số trong asset rồi coi là đã sửa, trong khi console ngoài vẫn giữ số cũ.
- Báo lại lỗi đã sửa làm mất tin vào phát hiện còn đúng.
- *Bẫy:* đổi logic một nút rồi đổi luôn nhãn cho "đúng nghĩa" — developer bắt trả lại.
- *Bẫy:* developer muốn bỏ một cơ chế vì vai của input đã đổi; đề xuất giữ cơ chế dưới tên mới, phải brainstorm lại.

## Nghiệm thu

- **Wire format** kiểm qua đúng cửa đọc–ghi hệ dùng, không gọi serializer trực tiếp — setting của serializer là một phần
  của format. **State đầu** (cài mới, save cũ) dựng qua đúng đường hệ tạo ra nó, không `new` trong test.
- **Case khẳng định "luôn"** chọn input mà bảo đảm có hiệu lực — input ngẫu nhiên rơi vào ca biên được phép thì đỏ oan.
- **Chỗ nhiều vật chồng nhau trên ảnh** thì định vị vật bằng toạ độ runtime của nó ở frame đó rồi mới lấy mẫu.
  *Bẫy:* lấy mẫu vật nền thay vì vật đang bay qua nó, báo sai màu.
- **Dấu hiệu vét cạn, cụ thể:** *tầm nhìn* gồm state nội bộ sai khi màn hình đúng · *nhất quán chéo* gồm trước–sau refactor
  phải trùng.
- **Điều kiện đo** (chế độ render, đơn vị, quality): số trong tài liệu mang theo đơn vị và điều kiện nó được đo; công cụ kiểm đổi điều kiện (chụp ở chế độ
  render khác) không chứng minh gì cho điều kiện kia. *Bẫy:* chép bề dày từ tài liệu đo ở đơn vị khác · kiểm hiệu ứng hạt
  bằng ảnh chụp ở chế độ canvas khác chế độ game chạy · code mẫu gắn prefab của canvas vào vật ngoài canvas, không vẽ gì.
- **Play qua công cụ chạy trên save thật của developer:** sao lưu đúng các key sẽ chạm trước khi vào, trả lại và so khớp
  trước khi báo (`playrun.sh` tự làm với PlayerPrefs; eval rời thì `PrefsSnapshot`, `docs/Editor/Probe.md`). Dừng ở phần
  tĩnh (code, YAML, eval không vào Play) khi thứ cần kiểm developer cảm được. *Bẫy:* dò luồng bằng eval rời làm đổi tiến độ
  và tài nguyên trên save của developer.
- **Vét cạn — liệt kê biên trước khi chạy:** rỗng, một phần tử, chạm giới hạn, trùng, ngoài dải, thứ tự đảo, hai sự kiện
  cùng lúc, frame đầu, object huỷ giữa chừng; rò rỉ sau hàng nghìn vòng. Test dựng dữ liệu riêng không thấy config đã trôi;
  luật thuần chạy qua eval không vào Play, Play chỉ còn cho wiring và đường host.
- *Bẫy:* hàng trăm lần eval chép cùng một khung mà không đề xuất gom thành công cụ.

## Cheat

**Khi nào chính đáng:** kịch bản không tới được bằng đường người chơi trong thời gian hợp lý — thời gian (hết chu kỳ) ·
khối lượng (đủ điểm mốc cuối) · trạng thái đầu (save mới). Mỗi nút là một dòng phải giữ đúng mãi.

- **Hàng đọc** chỉ cho thứ màn hình không hiện, qua API sẵn có, không bọc thứ đã public. **Lệnh riêng** chỉ khi phải chạm
  `private`, gọi đúng lệnh thật của hệ.
- **Lệnh thật có cổng** (khoá, cửa sổ thời gian) mà đường thật đóng cổng trước khi QA kịp bấm thì cheat là lệnh riêng vượt
  cổng, đặt trong hệ. *Bẫy:* nút gọi lệnh thật chỉ chạm vào cổng đã khoá từ phiên đầu, bấm không đổi gì.
- **Cheat đổi môi trường** để kiểm một kịch bản: lần theo kịch bản tới chỗ nó chạm hệ được đổi trước khi làm nút — kịch bản
  không đi qua hệ đó thì nút đúng mà vô nghĩa; cổng chặn im lặng dọc đường (anti-cheat, consent, cờ tắt) ghi vào kịch bản QA.
- **Lệnh thật nằm sau ranh `internal` của SDK bên thứ ba** thì mở công cụ debug của chính SDK: reflection hay ghi lại khoá
  lưu của SDK câm im lặng ở bản SDK sau.
- **Lệnh rời trạng thái giữa chừng đi đường người chơi rời nó;** màn con còn mở lúc chuyển màn bị đóng theo nhánh huỷ của
  chính nó — đếm hệ quả nhánh đó tự làm trước khi gọi thêm. Cheat không trừ thứ đường thật không trừ.
- **Lệnh đặt thẳng một giá trị** bỏ qua việc đường thật làm khi giá trị đi qua ngưỡng: làm cùng việc đó, không thì hệ tự
  sửa lại ở nhịp sau.
- **Cheat chọn cửa ghi không bắn tracking:** số cheat lẫn vào số liệu thì không lọc ra được.
- **Ô nhập ngoài miền người chơi tới được** thì từ chối kèm log, không kẹp — QA thấy mình gõ sai.
- **Panel có trong build chính thức, không `#if`** — QA kiểm bản người chơi cầm.
- **Lệnh, cửa đọc `private` và suy luận thuộc hệ**, không phụ thuộc công cụ debug — hệ mang được sang dự án dùng panel
  khác; lớp nối với panel thuộc dự án.
- **Đưa về trạng thái đầu** đi qua chủ giữ state: thay bản trong RAM lẫn bản lưu, flush, rồi **thoát app** — xoá kho lưu
  giữa phiên bị flush lúc pause/quit ghi đè, việc chỉ chạy lúc boot không chạy lại. Lệnh đổi cấu hình chỉ đọc lúc boot cũng
  ghi rồi thoát; ở Editor thoát là dừng Play, `Application.Quit` không tác dụng. Cờ của khởi tạo lười giữ ở "đã kiểm" tới
  hết tiến trình: mở lại thì thứ đọc trước lúc thoát khởi tạo lại từ state cũ và bật lại cờ vừa xoá. "Như cài mới" xoá đúng danh sách key, không
  xoá cả kho (consent, prefs của SDK).

> **Nền tảng** — khi dự án dùng SRDebugger: tab Options, một category mỗi hệ, trigger `Off`; cách mở panel do dự án quyết.
> Option chỉ đọc lại getter khi bind, `OnEnable` (mở panel, đổi tab) hoặc khi được báo, không theo frame: lệnh xong phải
> báo các hàng đọc. Panel khác mở từ một hàng mà xếp dưới panel SRDebugger thì không nhận chạm: ẩn panel SRDebugger trước.

**Sổ tay** — partial `<Hệ>.Debug.cs` giữ lệnh, plain class `<Hệ>DebugOptions` giữ hàng UI (nhận hệ qua constructor,
`Init/Dispose` ở `Awake/OnDestroy`, `internal sealed` vì SRDebugger quét mọi public member) · hàng đọc là `string`
read-only · lệnh có tham số = property số hoặc chuỗi + method không tham số · `INotifyPropertyChanged` bắn `null` sau mỗi
lệnh · hàng là delegate đọc hệ thì `DynamicOptionContainer` + `OptionDefinition.Create`/`FromMethod`, báo bằng
`Property.NotifyValueChanged()`, không còn class public cho SRDebugger quét.
