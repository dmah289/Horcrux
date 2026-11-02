---
name: math-physics
description: Dùng khi hệ cần công thức toán hay vật lý: quyết có cần toán không và sâu tới đâu, dẫn giải cho người đọc hiểu sâu, đối chiếu công thức với code bằng phép kiểm chạy được.
---

# math-physics — toán và vật lý trong hệ

Skill nền dưới `Skills/horcrux_skill.md` (NT, ba tầng, ký hiệu định nghĩa ở đó).

## Khi nào dùng toán, sâu tới đâu

Mở ra khi **developer yêu cầu**, hoặc khi **agent thấy toán giải tốt hơn hẳn** — khi đó nêu được–mất để developer quyết.

**Mặc định là không cần toán.** Logic gameplay phần lớn là trạng thái và luật rời rạc. Xét cách rẻ, dễ chỉnh hơn trước:
`AnimationCurve` hay bảng tra designer chỉnh trong Inspector (NT4) · easing có sẵn · lerp · máy trạng thái · hằng số chọn
tay. **Dấu hiệu ép toán:** phải dẫn định luật để biện minh một phép nhân · công thức một call site, không tham số nào đổi ·
designer không chỉnh được gì. **Bờ vực kia cũng sai:** bài toán vốn liên tục có ràng buộc (dừng đúng chỗ, va chạm, nội suy
cần đạo hàm liên tục) mà né toán thì thành đống hằng số không ai hiểu. Toán đúng chỗ làm code **ngắn hơn**. Đã cần thì
**xấp xỉ là mặc định**; cần bản đầy đủ thì làm tử tế — cắt nửa vời rồi bù hằng số sinh ra hệ không ai dám sửa.

**Núm phơi ra là đại lượng người tune nhìn thấy, không phải tham số trung gian:** biên độ là độ lệch thật, số lần lắc là số
đếm được bằng mắt. Hình dạng chuyển động gọi tên được theo pha thì dựng từ hàm có sẵn đúng hình dạng đó. **Quãng đường
không cố định** (khoảng cách, chênh giá trị, đoạn còn lại) **thì núm là tốc độ, không phải thời lượng** — tốc độ giữ một
nhịp ở mọi quãng, chuyển động bị ngắt giữa chừng đi tiếp không đổi nhịp.

## Cho hiểu sâu

Người đọc phải **hiểu hiện tượng** · **tin công thức suy ra được** · **kiểm lại được** bằng tay. Mạch dưới cho công thức
**không hiển nhiên**:

| # | Người đọc hỏi | Phải trả lời | Sổ tay — dạng |
|---|---|---|---|
| 1 | mô tả hiện tượng gì? | mô hình thực tế, map sang mục đích | bảng "thành phần → vai trò" |
| 2 | vì sao mô hình đúng? | định luật, định lý gốc | diagram |
| 3 | phương trình là gì? | phương trình chi phối, nghĩa từng ký hiệu | `$$…$$` + bảng ký hiệu |
| 4 | vì sao chọn cái này? | lựa chọn đã cân, tiêu chí loại | bảng so sánh có cột ✓ |
| 5 | từ phương trình ra nghiệm trong code? | từng bước, **không nhảy bước** | đánh số ①②③ |
| 6 | sao tin nghiệm đúng? | giá trị tại mốc biên so với kỳ vọng | bảng "mốc → kỳ vọng → ✓" |

Không thương lượng: **trực giác trước, ký hiệu sau** · **suy ra, không áp đặt** · **thứ chọn bằng cảm giác thì nói thẳng**
("chọn bằng tai và mắt, số này cho cảm giác X"), không bịa dẫn giải vật lý · **lệch vật lý chuẩn là bình thường** (NT8) —
nêu lệch ở đâu, vì sao, khi nào mới cần bản đầy đủ.

## Đối chiếu công thức

Mỗi công thức đã chốt map sang code bằng một **phép kiểm chạy được**, không bằng "trông giống". Đây là đối chiếu công thức
với code, không phải nghiệm thu cảm giác (`workflow` › Nghiệm thu).

**Sổ tay** — mỗi số hạng map một dòng code, kiểm **từng hệ số, từng dấu** · mốc chéo: giá trị biên khớp bảng kiểm chứng ·
đạo hàm số: `f'(t)` so với `(f(t+h)−f(t−h))/2h`, `h=1e-4` · round-trip: `A→B→A` về gần chính nó.
