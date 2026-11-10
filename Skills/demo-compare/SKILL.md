---
name: demo-compare
description: Dùng khi so màn game với ảnh demo hay ảnh chụp máy thật, khi bố cục trên máy khác Editor, hoặc trước khi build để xem màn trên máy có tai thỏ, thanh home, tỉ lệ khác. Đo ở hai chế độ — Game view đúng cỡ demo và Device Simulator theo máy — bằng số trên ảnh chụp, không bằng mắt.
---

# demo-compare — so demo ở Game view và trên máy giả lập

Skill công cụ dưới `Skills/horcrux_skill.md` (NT, ba tầng, ký hiệu định nghĩa ở đó). Tool: `docs/Editor/Probe.md` (chạy nhiều cỡ,
nhiều máy, chọn máy Simulator, thêm máy thật, đo khung object) · `docs/Editor/UIReskin.md` (dò dải màu, ghép ảnh cạnh nhau, số lệch `Diff`, fit shader vầng sáng). Dựng
prefab UI theo demo: `ui-reskin`.

## Hai chế độ, hai câu hỏi

| Chế độ | Trả lời | Không thấy |
|---|---|---|
| **Game view đúng cỡ demo** | bố cục có khớp thiết kế không: so pixel thẳng với demo | vùng an toàn: Game view có safe area bằng cả màn |
| **Device Simulator theo máy** | người chơi trên máy đó thấy gì: tỉ lệ, tai thỏ, thanh home | demo khác cỡ màn máy nên không so pixel thẳng với demo |

**Đúng ở Game view chưa phải đúng trên máy.** Thứ gì đọc vùng an toàn chỉ lệch trên máy có inset, và Game view không bao giờ lộ ra.
Mỗi lần chỉnh bố cục: Game view cỡ demo để khớp thiết kế, rồi Simulator vài máy đại diện (một máy tai thỏ, một máy dài, một tablet)
để chắc không gì vỡ, trước khi build. Build lên máy là bước nghiệm thu cuối, không phải chỗ đầu tiên thấy lệch.

**Ảnh máy thật lệch thì tái hiện trước khi sửa, bắt đầu từ Game view đúng cỡ màn máy.** Game view đã trùng ảnh máy thì lệch do tỉ lệ
màn, không do vùng an toàn: sửa luật co giãn (mục Tỉ lệ màn), không cần file máy. Còn lệch, và lệch đều mọi khối cùng một mép, mới là
inset: tái hiện trên Simulator. Máy chưa có trong Simulator thì thêm file máy; thiếu thông số hãng thì
suy từ ảnh chụp, bằng cách so với Game view cùng cỡ màn (các khối neo cùng một mép lệch cùng một số pixel). Simulator ra trùng ảnh máy
theo từng dải màu mới được dùng làm chuẩn để sửa và nghiệm thu; không trùng thì file máy sai, chưa phải lỗi code.

**Đo bằng số, cùng một phép đo cho mọi ảnh.** Demo, ảnh Game view, ảnh Simulator, ảnh máy đo bằng cùng hàm dò dải màu, cùng cột, cùng
tolerance. Khung object chỉ để tìm cột. Đè ảnh mờ lên màn chỉ để nhìn, không để kết luận: hai ảnh khác tỉ lệ thì khối neo theo bề rộng
và khối co theo chiều cao trôi khác nhau.

**So ở trạng thái cao nhất.** Phần tử chỉ hiện theo gameplay (chữ nhiều dòng, badge, thứ trượt ra) ép hiện với nội dung dài nhất rồi mới
đo; đo lúc nó ẩn thì khe trông rộng, dời quá tay.

**Mỗi phép đo là một lệnh chạy lại được.** Kịch bản đo của màn nằm ở dự án, chạy mọi cỡ và mọi máy trong một lần; Editor trả về view,
cỡ, máy cũ sau khi đo. Developer nghiệm thu hình (`workflow` › Nghiệm thu); agent báo số lệch còn lại ở từng chế độ.

## Vùng an toàn và khoảng trống thiết kế

Demo thường đã chừa dải trống trên cùng cho thanh trạng thái và lỗ camera. Cộng cả inset của máy lên trên dải đó là đếm hai lần: cả cụm
trên tụt sâu trên máy có tai thỏ, còn ở Game view vẫn đúng. Inset chỉ đẩy phần vượt dải trống thiết kế. HUD và mọi khối world neo cùng
mép đọc **cùng một quy tắc** (`NT7`, hai bản buộc khớp lúc chạy), để khe giữa chúng không đổi theo máy. Dải trống là số đo của thiết kế,
nằm một chỗ cạnh phần tử trên cùng mà nó đo. *Đã sai một lần:* HUD và cụm world cùng neo trong safe area, khớp demo ở Game view, trên máy
punch-hole tụt cả cụm hơn trăm pixel xuống dưới dải trống demo đã chừa.

**Neo là mép cộng khoảng cách, không là phần của vùng an toàn.** Vùng an toàn ngắn đi vì inset thì điểm đặt bằng tỉ lệ của nó chỉ dời một
phần inset và còn nhích theo inset cạnh đối diện, trong khi HUD trong panel dời đủ. Chọn mép mà khối kề, đo khoảng cách từ mép đó.

## Tỉ lệ màn — world và HUD co cùng một luật

Canvas HUD co theo một trục (thường khớp chiều cao). Khối world kề HUD giữ khe với HUD ở mọi tỉ lệ chỉ khi co theo **cùng** luật: camera
giữ trọn khung thiết kế — số lớn hơn giữa cỡ giữ bề rộng và cỡ giữ chiều cao — và khoảng từ mép tới khối đo bằng pixel thiết kế nhân cùng
hệ số canvas, không bằng unit world. Hai luật khác nhau trùng đúng ở tỉ lệ demo nên Game view cỡ demo không bao giờ lộ; lệch lớn dần theo
tỉ số hai aspect, rõ nhất trên tablet. Mỗi lần chỉnh bố cục so thêm ở Game view một màn rộng hơn demo (16:9, tablet) và một màn cao hơn.
*Đã sai một lần:* camera giữ bề rộng, khoảng cách tính bằng unit world, khớp demo ở 19.5:9; trên tablet 3:2 khối world cách HUD gấp bốn
lần demo, dải bàn chơi còn nửa và scale fit kẹt sàn; developer nghi vùng an toàn, Game view cùng cỡ máy đã ra trùng ảnh máy.

## Bẫy

| Triệu chứng | Nguyên nhân · cách tránh |
|---|---|
| Ảnh chụp khớp demo mà vào chơi khối đè HUD | chụp lúc phần tử hiện theo gameplay chưa hiện · ép hiện với nội dung dài nhất rồi mới đo |
| Số khung object lệch pixel demo vài px dù hình đã khớp | bounds gồm viền trong suốt và bóng của art · khung chỉ để tìm cột; số so với demo lấy bằng dải màu cùng cột trên cả hai ảnh |
| Developer đè demo mờ lên Game view thấy lệch hàng chục px, đo thật chỉ vài px | Game view khác tỉ lệ demo · đọc cỡ Game view trước khi tin ảnh đè; so demo chỉ ở đúng cỡ demo |
| Tablet hay 16:9: khối world xa HUD, vật world nhỏ hơn vật cùng cỡ ở khối khác | camera giữ một trục khác trục canvas · camera giữ trọn khung thiết kế, khoảng cách đo pixel thiết kế (mục Tỉ lệ màn) |
| Đúng ở Editor, lệch trên máy; lệch đều mọi khối cùng một mép | inset của máy, Game view không có · tái hiện trên Simulator với file máy đó |
| Kết quả Simulator mang tên máy mới mà số là của máy cũ | chọn máy hỏng (tên sai) mà vẫn chạy · runner dừng khi chọn máy hỏng; đọc dòng chọn máy trong kết quả |
| Simulator cùng cỡ mà không giống máy | máy mẫu của Unity là máy chung chung, inset và cutout khác máy thật · thêm file máy thật, kiểm bằng ảnh chụp máy |
| Gradient shader dựng theo demo đúng màu tâm và mép mà sai nhịp tắt | hệ số đo ở sRGB của ảnh, shader nội suy ở Linear · `DemoCompare.FitRadialGlow` fit theo không gian màu project kèm số lệch mô phỏng; giao số chỉ sau khi chụp trong Play đúng chế độ canvas và `Diff` với demo |
| `max` lệch lớn ở mép, góc ảnh demo dù nền đã khớp | demo xuất kèm khung viền, mỗi cạnh dày khác nhau · `Runs` ở mép tìm viền, đưa vào `exclude` trước khi tin số |
| Đổi cỡ Game view thì bật thêm một cửa sổ Game nổi | Simulator đang chiếm chỗ view Play · công cụ đổi cỡ tự đưa Game view về; swap tay thì trả lại |
