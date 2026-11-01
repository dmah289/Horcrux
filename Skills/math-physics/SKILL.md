---
name: math-physics
description: Dùng khi hệ cần công thức toán hay vật lý: quyết có cần toán không và sâu tới đâu, dẫn giải cho người đọc hiểu sâu, đối chiếu công thức với code bằng phép kiểm chạy được.
---

# math-physics — hệ toán học và vật lý

Skill nền của Horcrux, đứng dưới `Skills/horcrux_skill.md`: `NT<n>`, ba tầng Luật · Nền tảng · Sổ tay và bảng
chỉ dẫn định nghĩa ở đó. Con trỏ `` `skill` › Mục `` trỏ sang skill khác; `mục X` trỏ trong file này.

## Khi nào dùng tới toán, và sâu tới đâu

Mở ra khi **developer yêu cầu**, hoặc khi **agent thấy toán giải bài toán tốt hơn hẳn** — trường hợp sau
nêu kèm được–mất để developer quyết (NT5).

**Mặc định là không cần toán.** Phần lớn logic gameplay là trạng thái và luật rời rạc. Xét hết cách rẻ hơn
và dễ chỉnh hơn trước: `AnimationCurve` hoặc bảng tra do designer chỉnh trong Inspector (NT4) · easing có
sẵn · lerp · máy trạng thái · một hằng số chọn bằng tay. **Dấu hiệu ép toán vào chỗ không cần:** phải dẫn
định luật nền để biện minh một phép nhân · công thức chỉ một call site và không tham số nào thay đổi ·
designer không chỉnh được gì. **Bờ vực còn lại cũng sai:** bài toán vốn liên tục và có ràng buộc — dừng
đúng chỗ, va chạm, nội suy cần đạo hàm liên tục — mà né toán thì thành một đống hằng số không ai hiểu.
Toán đúng chỗ làm code **ngắn hơn**. Đã cần toán thì **xấp xỉ là mặc định** (NT8); khi thật sự cần bản
đầy đủ thì làm tử tế — cắt nửa vời rồi bù bằng hằng số là cách sinh ra hệ không ai dám sửa.

**Núm phơi ra là đại lượng người tune nhìn thấy trên màn hình, không phải tham số trung gian của công
thức.** Biên độ là đúng độ lệch thật, số lần lắc là đúng số đếm được bằng mắt. Hình dạng chuyển động gọi
tên được theo pha thì dựng từ hàm có sẵn đúng hình dạng đó. **Quãng đường không cố định — khoảng cách,
chênh lệch giá trị, đoạn còn lại — thì núm là tốc độ, không phải thời lượng:** thời lượng cố định trên quãng
đường đổi làm tốc độ cảm nhận đổi theo; tốc độ giữ một nhịp ở mọi quãng, và chuyển động bị ngắt giữa chừng
đi tiếp từ chỗ đang đứng không đổi nhịp.

## Cho hiểu sâu — khi đã cần toán thì cho tới nơi

Người đọc phải **hiểu hiện tượng** · **tin công thức là suy ra được** · **kiểm lại được** bằng tay. Mạch
dưới dành cho công thức **không hiển nhiên**.

| # | Câu hỏi người đọc sẽ hỏi | Phải trả lời được gì | Sổ tay — dạng trình bày |
|---|---|---|---|
| 1 | Cái này mô tả hiện tượng gì? | mô hình thực tế đằng sau, map sang mục đích thế nào | bảng "thành phần → vai trò" |
| 2 | Vì sao mô hình đó đúng? | định luật hoặc định lý gốc | diagram |
| 3 | Phương trình là gì? | phương trình chi phối, ý nghĩa từng ký hiệu | `$$…$$` kèm bảng ký hiệu |
| 4 | Vì sao chọn cái này? | các lựa chọn đã cân, tiêu chí loại | bảng so sánh có cột ✓ |
| 5 | Từ phương trình gốc ra nghiệm trong code thế nào? | từng bước biến đổi, **không nhảy bước** | đánh số ①②③ |
| 6 | Làm sao tin nghiệm này đúng? | giá trị tại các mốc biên so với kỳ vọng | bảng "mốc → kỳ vọng → ✓" |

Không thương lượng: **trực giác trước, ký hiệu sau** · **suy ra, không áp đặt** · **thứ chọn bằng cảm giác
thì nói thẳng** "chọn bằng tai và mắt, số này cho cảm giác X" — không bịa dẫn giải vật lý cho nó · **lệch
vật lý chuẩn là bình thường** (NT8) — chỉ nêu lệch ở đâu, vì sao, khi nào mới cần bản đầy đủ.

## Đối chiếu công thức với code

Mỗi công thức đã chốt map sang code bằng một **phép kiểm chạy được**, không bằng cảm giác "trông giống"
(NT8). Đây là đối chiếu **công thức với code**, không phải nghiệm thu cảm giác chơi (`workflow` › Nghiệm thu).

**Sổ tay** — đối chiếu từng số hạng: mỗi công thức map thẳng một dòng code, kiểm **từng hệ số và từng
dấu** · kiểm mốc chéo: giá trị biên phải khớp bảng kiểm chứng của task · đạo hàm số: so `f'(t)` với
`(f(t+h)−f(t−h))/2h`, `h=1e-4` · round-trip: cặp converter `A→B→A` phải về gần chính nó.
