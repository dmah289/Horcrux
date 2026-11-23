---
name: code-design
description: Dùng khi viết hoặc sửa code runtime: SOLID, bậc cấu trúc, giới hạn của composite, module và phân tầng phụ thuộc, hiệu năng bộ nhớ và thời gian, bất biến và guard, naming.
---

# code-design — hình dạng, module, hiệu năng, bất biến, naming

Skill nền dưới `Skills/horcrux_skill.md` (NT, tầng, ký hiệu định nghĩa ở đó).

## Hình dạng — SOLID, bậc cấu trúc

- **S** — một class một **lý do thay đổi**; định nghĩa mà NT2, NT6, `reuse-gate` trỏ về. Không tách "cho gọn mắt", "cho
  đúng pattern".
- **O** — cấm **đổi hành vi đường cũ**, không cấm thêm đường mới vào chính class. Phải đổi đường cũ là trách nhiệm mới:
  tách class.
- **L** — subtype thay được base, không side-effect lạ. **I** — interface nhỏ, tách theo consumer.
- **D** — **consumer không tự `new` thứ nó phụ thuộc**, nhận vào (factory, pool, container thì `new`). Không đòi interface:
  trong một hệ nhận class cụ thể vẫn là nhận vào; kéo thả vào `[SerializeField]` cũng là nhận vào.

**Bậc cấu trúc leo từ dưới, chỉ leo khi bậc dưới không còn đạt** (NT2) — nơi duy nhất định nghĩa "khi nào cần interface".

| Bậc | Leo lên khi |
|---|---|
| logic tại chỗ | có **người gọi thứ hai**, hoặc một ý không còn vừa một màn hình |
| hàm tách riêng (tên nói đúng mục đích, không state) | phát sinh **state**, hoặc cụm hàm cùng thao tác một nhóm dữ liệu |
| class hoặc struct (trách nhiệm gọi được tên) | có **implementation thứ hai đang có thật** |
| delegate làm tham số (thân giống hệt, chỉ khác **một thao tác**, biến thể không state) | biến thể cần state hoặc hơn một thao tác |
| interface hoặc abstract | — hoặc framework phải gọi sang assembly không được tham chiếu: lý do là **chiều phụ thuộc**, không "phòng khi đổi lib" |

- **Bất biến của vòng lặp** (thứ tự, tiến độ, lỗi, điểm thoát khi huỷ) chỉ có **một bản**; nhân vòng lặp hay cắm `if` biến
  thể là nhân bất biến.
- **Biến thể là cửa riêng, không phải cờ trên cửa chung:** hai chế độ loại trừ → overload nhận spec riêng · kịch bản mới
  của service dùng chung → **consumer mới** đưa nội dung qua tham số, không mọc cờ trên service · component dùng ở nhiều
  host **không có cờ chế độ trên instance** — ô quên tick không để lại dấu vết.
- **Đóng gói ở đâu** (sau khi biết cần class): trả lời từ tham số, không giữ gì → **class thuần static** · trả lời từ dữ
  liệu của chính nó → **`ScriptableObject`**, dừng ở dữ liệu · làm gì đó theo thời gian (loop, chờ, subscribe, ghi ra
  ngoài) → **`MonoBehaviour`** hoặc class thuần do nó sở hữu — cần **mốc chết tin được** để tắt.
- **Composite — mặc định là không; viết lý do tại chỗ trước khi dùng:** cây composite chỉ khi cấu trúc lồng **là của dữ
  liệu thật** · xé một tính năng thành nhiều `MonoBehaviour` chỉ khi các mảnh **thật sự lắp lẫn** giữa nhiều prefab đang có
  · hệ kết hợp chỉ khi không tách nổi thành hệ độc lập + lớp nối mỏng.
- **`partial` chỉ ở chỗ không có OOP để vi phạm** (Editor tool và Debug của một đối tượng · static class extension · khối
  field serialize kèm property); ngoài ra class phải chia file mới đọc nổi là ôm nhiều lý do thay đổi: **tách class**.
- **Gọi tường minh, không giao thứ tự cho callback Unity.** **Một** callback của **một chủ** gọi lần lượt các hàm có tên —
  thứ tự nằm ở một chỗ. Rải việc vào callback riêng của từng object chỉ khi gọi tường minh **đắt hơn hẳn**, nói giá tại
  chỗ. **Dọn cũng vậy:** trả tài nguyên cho hệ khác nằm ở đường rời chủ động, chủ gọi trước khi unload; `OnDestroy` chỉ
  dừng việc của chính nó.

## Module — độc lập trước, kết hợp là ngoại lệ (NT4, NT8)

- Ranh giới hệ **nhìn thấy được** trong cấu trúc dự án. **Module không tham chiếu implementation của nhau** — trung gian
  (interface, event bus, dữ liệu thuần) chọn theo bài toán, miễn đổi một bên không sửa bên kia. **Consumer cần method nào
  thì method đó thuộc contract.**
- Phân tầng quyết từ đầu: hệ **độc lập** · hệ **kết hợp** · **Utilities** static, không phụ thuộc hệ nào. File đặt nhầm
  tầng là một phụ thuộc giấu.
- **Cơ chế mở rộng không ép dự án đẩy domain sang framework.** Phép kiểm: *nửa dự án viết có nằm trong ranh giới biên dịch
  của dự án không?*
- **Framework sở hữu cơ chế, dự án sở hữu từ vựng** (NT8). Tập giá trị dự án đặt tên (khoá, loại, trạng thái) → framework
  khai **type parameter** có ràng buộc, dự án chốt bằng class con. Phép kiểm: *dự án có phải cast, gõ số, gõ chuỗi ở call
  site hay trong asset không?* Tập giá trị **do dữ liệu ngoài định nghĩa** thì từ vựng thuộc dữ liệu, không enum. **Hàm
  kiểm dữ liệu của framework kiểm bất biến của chính dữ liệu, không kiểm kịch bản** của một dự án.
- **Trừu tượng hoá cho tái sử dụng khi người dùng thứ hai đang có thật:** hàm thuần → Utilities · state hay nhiều biến thể
  → interface · chỉ khác một giá trị → tham số; đặt ở **tầng thấp nhất cả hai với tới**. Sắp viết bản thứ n của cơ chế đã
  có ở module anh em thì bản mới là bản dùng chung đầu tiên, bản cũ chuyển sau bằng thêm rồi xoá.
- **Nâng bằng kế thừa thì cắt ở ranh *cơ chế / quyết định*:** base giữ phần giống hệt mọi nơi, lớp con chỉ còn *nói gì,
  lúc nào*.

## Hiệu năng (NT3)

**Nghiệm thu:** **chỗ đo** và **số trước–sau**; đo "0 cấp phát" kèm **đối chứng** — cùng dụng cụ phải bắt được một cấp phát
cỡ đang tìm. Trước khi cache hay tối ưu, dừng ở câu "có" đầu tiên: **không cần tính** được không · tính **một lần lúc
authoring** được không · đổi **cấu trúc dữ liệu** để câu hỏi biến mất được không? Nhịp chậm không phải miễn trừ: rác mỗi
tương tác vẫn là rác. `Update`, polling, `OnGUI` bị gọi lại cho **cùng một state**; việc nặng thuộc event handler hoặc
authoring.

## Bất biến — bảo vệ bằng cấu trúc, không bằng kỷ luật

**Lý do là "phải nhớ đừng…" thì chỗ đặt sai** — tìm chỗ đặt làm việc "đừng" bất khả thi. Tập "tất cả X" chỉ gom phần tử
cùng hành vi.

| Luật | Nghĩa là |
|---|---|
| **Một sự thật = một chủ sở hữu** | một nơi giữ bản gốc; cache chỉ ra ai dựng lại, khi nào, bám bất biến **đã có** (dirty flag, version). Một cờ tiêu thụ ở **một** nơi |
| **Hai bản buộc khớp thì suy từ MỘT nguồn** | đo–vẽ, vẽ–hit-test, điều kiện ẩn–hiện: một hàm · chuỗi định danh hai hệ: khai một lần phía phát · phản hồi nhiều kênh của một khoảnh khắc (tiếng, rung, hình) gọi cạnh nhau trong **một hàm** |
| **Một trạng thái = một cửa ghi, một chủ gọi** | mở, huỷ, chốt trong **một** class; nơi khác chỉ **phát sự thật** (sự kiện tên theo điều đã xảy ra). Hệ quả luôn xảy ra, rẻ, không ném → trong **setter**. Hai lời gọi luôn đi cùng nhau thì gộp vào trong cửa public |
| **Một bảo đảm phủ MỌI đường vào** | mọi cửa ghi đi qua chỗ tạo bảo đảm; **đếm cửa trước, đọc thân sau** — grep mọi API ghi |
| **Một phép biến đổi chỉ áp ở MỘT tầng** | runner đã ease thì thân nhận `t` đã ease; hai tầng cùng ease thì một tầng `Linear` |
| **Chọn cấu trúc theo bảo đảm người dùng dựa vào** | `Dictionary` để tra, không để vẽ danh sách; thứ tự từ cấu hình ngoài toàn phần, tất định. Mặc định là ca số đông, viết bằng tên. Framework không chọn nguồn ngẫu nhiên — nhận từ caller |

Các bất biến chuyên biệt — thao tác có giá, đếm tiến độ, lệnh tới hệ ngoài, lý do của trạng thái, danh sách vừa là lệnh vừa
để vẽ — cùng gốc "một cửa ghi", phát biểu ở tra cứu › Bất biến, cùng chi tiết và bẫy của các hàng trên.

**Guard — cái sai lộ ra lúc nào?** Lộ ngay lúc authoring hay nổ ngay lần Play đầu → **để nổ** (và kiểm vế *nổ*) · sai lúc
setup mà không nổ (wire thiếu) → **`LogError` rồi thoát** · lọt authoring rồi sai âm thầm giữa gameplay (reference chỉ có
lúc chạy, null ở một variant, một tổ hợp cấu hình, sau `Destroy`, dữ liệu ngoài) → **guard đầy đủ** · ca hợp lệ chưa tới lượt → thoát im lặng ·
không gọi tên được lượt kiểm nào bắt nó → guard.

- **Dữ liệu ngoài kiểm một lần ở cửa vào** bằng hàm thuần kiểm luật nghiệp vụ, sai thì từ chối cả cấu hình kèm `LogError`;
  sau cửa consumer tin hợp đồng. **Cấu hình ngoài đổi gameplay đứng yên suốt phiên:** nhận ở **một** mốc boot có hạn giờ.
- **Hàm thuần không log:** trả cờ hay cộng dòng vào danh sách của caller; **host quyết log đi đâu, mức nào**.
- **`try/catch` chỉ khi gọi được tên thứ ném ra** (I/O, parse, network, reflection, dữ liệu ngoài, code người khác trong
  vòng lặp của mình); bắt rồi phải xử lý, không nuốt. `catch (Exception)` trần chỉ ở biên trên cùng.
- **Quy ước chỉ thay được guard khi viết ở chỗ người vi phạm đang nhìn** (Tooltip, XML doc, tài liệu module).

## Naming — self-documenting code

- **Tên là lời giải thích thứ nhất, comment là phương án cuối.** Trần **5 từ dài**; vẫn vượt trần là khái niệm chưa tách
  đủ. Tên mang đủ ngữ nghĩa — *lúc nào*, *đếm hay vị trí*, *đã xảy ra* — không từ mờ.
- **Method nói mục đích** (không `Process`, `Handle`, `DoWork`); boolean đọc như câu hỏi; cờ nói **cái gì** đang chờ; hai
  chế độ loại trừ là `enum`, không `bool`. Tiền tố: interface `I…`, abstract `A…`.
- **Comment mặc định là không có**; chỉ nói **tại sao** (trái trực giác, bẫy đã trả giá, công thức nguồn). API public có
  XML doc. Trần **17–20 từ** comment, tooltip · **35 từ** `<summary>` — chạm trần thì sửa gốc hoặc dời vào tài liệu.
- **Một từ = một nghĩa toàn hệ.** **Tên hệ, field, lý do theo cơ chế, không theo người dùng hay kịch bản đầu tiên** — kịch
  bản thứ hai tới thì tên nói sai.
- **Đổi tên là đổi cả hệ** (NT12): code, comment, chuỗi debug, tài liệu cùng lần, kiểm bằng grep. Khoá wire format, dữ
  liệu ngoài repo không đổi theo: định danh vừa là tên trong code vừa là khoá ngoài repo thì đổi tên là **hai quyết định**,
  hỏi tách trước khi làm. Đổi đơn vị cũng vậy.

## Tra cứu — `code-design/reference.md`

| Chạm | Mục |
|---|---|
| DI, `Init`, `[Service]`, thứ tự khởi tạo, `ScriptableObject`, callback Unity, region, quy ước code | Stack và quy ước |
| thư mục và namespace Horcrux, `partial`/`abstract` qua ranh assembly, kế thừa, type parameter cho từ vựng dự án, hàm kiểm dữ liệu của framework | Module |
| cấp phát, cache, vòng lặp nóng, đo GC | Hiệu năng |
| guard, default của field, DTO, `null` trong asset, cấu hình ngoài lúc boot, view vẽ ô lỗi | Guard |
| viết cửa ghi, sự kiện, bộ đếm, định danh chia sẻ với asset, vật world kề canvas; thứ tự từ cấu hình, mặc định của enum, nguồn ngẫu nhiên; mua, trả, thưởng, đếm tiến độ, lệnh gửi hệ ngoài (SDK, service) | Bất biến |
| đặt tên member, GameObject, namespace; đổi tên file, asset, khoá | Naming |
