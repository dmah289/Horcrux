Code: `Editor/VideoDemo/Tools~/video_demo.py`

# VideoDemo — đọc video demo thành ảnh và số

Biến một video demo (mp4 quay màn hay xuất từ tool dựng) thành thứ agent đọc rẻ: ảnh lưới khung hình có nhãn giây, bảng
đoạn động và chu kỳ lặp, đường vị trí, scale, alpha của một vật kèm ease PrimeTween gần nhất, và độ lệch nhịp giữa demo với
bản quay Unity. Dùng để dựng và chỉnh animation, hiệu ứng theo demo; nghiệm thu cảm giác vẫn là developer chơi thử.

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | agent: đọc demo trước khi dựng anim, so bản dựng với demo sau mỗi lần chỉnh. Đầu ra là PNG nhỏ và vài chục dòng số, không HTML |
| Đầu vào | file video OpenCV đọc được (mp4, mov), mọi cỡ, mọi fps; chuỗi PNG đặt tên theo mẫu `f_%04d.png` (coi 30 fps, bỏ kênh alpha); bản quay Unity theo mục Quay bản Unity |
| Ranh giới | tool **đọc và đo**; chọn tham số tween, Spine, particle là việc của agent sau đó. So một khung tĩnh với Game view dùng `DemoCompare.Diff`, `SideBySide` (`docs/Editor/UIReskin.md`), tool chỉ xuất khung PNG đúng cỡ |
| Chỗ đặt | Python ở `Editor/VideoDemo/Tools~/`: thư mục đuôi `~` Unity không import, không vào build. Không có C# |
| Phụ thuộc | `opencv-python-headless`, `numpy` (cài một lần, mục Cài đặt) |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Bám vật xoay | template match chỉ dò vị trí và scale; vật xoay thì điểm khớp tụt, đọc bằng `sheet` |
| Tự chọn vật cần bám | demo nhiều lớp chồng nhau, chọn sai thì số trông hợp lý mà đo nhầm vật; `motion` in `vùng` để chọn `--roi` |
| Đọc video trong Editor bằng C# | `VideoPlayer` cần Play và render; Python đọc thẳng file trong vài giây |
| Trang HTML có thanh trượt thời gian | người đọc là agent; developer xem video gốc |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| Chạy bằng `python -X utf8 -P`, không `-I` | `-I` bỏ qua site-packages của user, nơi `pip install --user` đặt OpenCV; `-P` vẫn chặn thư mục script khỏi `sys.path` |
| Đọc tuần tự, không seek | seek mp4 rơi về keyframe gần nhất, khung lấy ra lệch mốc mà không báo |
| `compare` canh bằng tương quan cả đường độ động, dò cả nhịp lẫn trễ | canh theo lần động đầu hỏng khi hai bản nhiễu khác nhau; canh một độ trễ cố định trôi dần khi bản Unity chậm vài phần trăm |
| Độ động chia cho phân vị 99 của chính video trước khi so | bản quay Unity và demo khác độ sáng, khác nhiễu nén; so hình dạng đường, không so độ lớn |
| `motion` đo cả khung thì nhịp nhỏ chìm | độ động là trung bình trên vùng; vật nhún 5% trong khung lớn dưới ngưỡng. Đo nhịp nhỏ thì `--roi` quanh vật |

## Luồng dữ liệu

```
video ──► đọc tuần tự (OpenCV) ──┬─► sheet   : khung ở N mốc → cắt roi → ô có nhãn "giây #frame" → lưới PNG
                                 ├─► frame   : khung ở mốc → cắt roi → đổi cỡ (--size) → PNG ──► DemoCompare.Diff / SideBySide
                                 ├─► motion  : ảnh xám rộng 160 px → lệch trung bình giữa hai frame liền e[k] (0–255)
                                 │             → ngưỡng = max(3·nhiễu, nhiễu + 0.5) → đoạn động, cắt cảnh, vùng, frame trùng, chu kỳ lặp
                                 ├─► track   : mẫu = roi ở --ref → khớp mọi frame (vị trí, scale) → alpha theo nền --bg
                                 │             → pha từng kênh → ease PrimeTween gần nhất | dao động (chu kỳ, ease mỗi nửa)
                                 └─► compare : đường độ động A, B chuẩn hoá → dò tB = k·tA + c → cặp sự kiện, nhịp, lưới A trên B dưới
```

## Cài đặt

```bash
python -m pip install opencv-python-headless
python -X utf8 -P "<horcrux>/Editor/VideoDemo/Tools~/video_demo.py" selftest
```

`selftest` dò bảng ease với PrimeTween rồi dựng một video tổng hợp có đáp án sẵn và đo lại (mục Nghiệm thu); xanh hết mới tin số
trên demo thật.

## Lệnh

```bash
T="<horcrux>/Editor/VideoDemo/Tools~/video_demo.py"
python -X utf8 -P "$T" sheet   <video> --out <png> [--from s] [--to s] [--count 12 | --step s] [--cols 6] [--cell 180] [--roi R]
python -X utf8 -P "$T" frame   <video> --at 1.2,3.4 --out-dir <dir> [--size 1080x2340] [--roi R]
python -X utf8 -P "$T" motion  <video> [--from s] [--to s] [--roi R] [--bin 0.1] [--threshold v]
python -X utf8 -P "$T" track   <video> --roi R --ref t [--bg t] [--from s] [--to s] [--csv <file>] [--min-score 0.7]
python -X utf8 -P "$T" compare <a> <b> [--roi R] [--a-from s] [--a-to s] [--b-from s] [--b-to s] [--out <png>] [--count 8] [--max-lag 5] [--tempo 1.25] [--pair-window 0.4]
```

| Cờ | Nghĩa |
|---|---|
| `--roi x,y,w,h` | vùng gốc trên trái. Số nguyên là pixel video; cả bốn số `<= 1` và có dấu chấm là phần của khung — dùng cho `compare` vì hai video khác cỡ |
| `--from`, `--to` | khoảng giây đọc |
| `sheet --step` | giây giữa hai ô; `--step 0.0334` ở video 30 fps là mọi frame — xem từng frame quanh một mốc |
| `frame --size` | đổi cỡ khung xuất ra, ví dụ cỡ Game view để `Diff` |
| `motion --bin` | giây mỗi ký tự của dải độ động |
| `motion --threshold` | ép ngưỡng động (0–255) khi ngưỡng tự tính bắt nhiễu nén |
| `track --ref` | giây vật hiện **rõ, đúng dáng cuối, nằm trong `--roi`**; vùng ở mốc này là mẫu |
| `track --bg` | giây vùng chưa có vật; có thì đo alpha |
| `track --min-score` | điểm khớp (`TM_CCOEFF_NORMED`) dưới mức này là không thấy. Mẫu thu rất nhỏ khớp nhiễu tới ~0.65 |
| `track --eps-px` | vị trí đổi dưới mức này mỗi frame coi là đứng (mặc định 1 px) |
| `track --csv` | ghi mọi frame: `t,frame,score,dx,dy,scale,alpha` |
| `compare --a-from`, `--a-to`, `--b-from`, `--b-to` | đoạn so của từng video, giây in ra vẫn là giây của cả video. Demo nhiều lượt, bản Unity có thêm màn sau anim thì cắt mỗi bên đúng một lượt |
| `compare --max-lag`, `--tempo` | dò trễ trong ±giây, nhịp B so với A trong `[1/tempo, tempo]` |
| `compare --pair-window` | hai sự kiện cùng loại cách mốc canh tới mức này là một cặp |

Thời gian: `motion`, `sheet`, `frame` vài giây cho video 15 s; `track` ~10 s cho 100 frame cỡ 440×940; `compare` vài giây.

## Quy trình dựng anim theo demo

1. `sheet` cả video (12–18 ô) → biết có mấy lượt chơi, các chặng chính, cỡ khung.
2. `motion` cả video → đoạn động, cắt cảnh, frame trùng (nhịp thật của bản quay). Chọn một lượt chơi.
3. `sheet --step 0.0334 --from … --to …` quanh mỗi mốc bắt đầu → thứ tự xuất hiện của từng lớp, dáng của vật ở mỗi chặng.
4. `track` từng vật, mỗi chặng một mẫu (vật đổi dáng giữa chặng thì mẫu chặng sau không khớp chặng trước) → thời lượng, quãng,
   ease, độ vượt → tham số PrimeTween. `motion --roi` quanh vật đang lặp → chu kỳ idle.
5. Dựng trong Unity. Quay Game view bằng Unity Recorder (mục Quay bản Unity), cùng tỉ lệ khung với demo.
6. `compare demo.mp4 unity.mp4 --roi <phần khung> --out cmp.png` → nhịp, trễ, sự kiện lệch; mở `cmp.png` xem cùng mốc.
7. Khung tĩnh cuối: `frame --size <cỡ Game view>` rồi `DemoCompare.Diff` với ảnh chụp Game view.

## Quay bản Unity

Window › General › Recorder › Recorder Window → Add Recorder › Movie. Source **Game View**, Output Resolution theo Game view, Format
MP4. Playback **Constant**, Target FPS 30 (thiếu thì nhịp trôi theo máy, `compare` báo lệch giả). Game view đặt cùng tỉ lệ demo
(`demo-compare`). Bấm Start Recording rồi vào màn cần quay; Recorder dừng khi thoát Play.

**Sổ tay** — không cần Recorder: probe `playrun.sh` (`docs/Editor/Probe.md`) đặt `Time.captureFramerate = 30`, gọi lệnh mở anim, rồi
mỗi frame `FrameSheet.CaptureAsync` + `Save(<thư mục>/f_%04d.png, 1)`. Nhịp tất định dù máy ghi PNG chậm, vì anim chạy theo
`Time.deltaTime` (anim chạy unscaled thì không dùng được cách này). Thư mục ảnh đặt ngoài thư mục tạm của `playrun.sh`, vì runner xoá thư mục tạm
khi xong. Ảnh có viền 4 px của `FrameSheet`: so bằng `--roi` theo phần khung.

## Đọc kết quả

| Thấy gì | Nghĩa là | Làm gì |
|---|---|---|
| `trùng 30% → ~21 hình/s` | bản quay lặp frame: mỗi mốc thời gian chỉ chính xác theo nhịp thật | đừng tin thời lượng lệch dưới một nhịp thật |
| `cắt cảnh: t` | khung đổi đột ngột một frame: hết lượt, vật biến mất, chuyển màn | tách lượt theo mốc này |
| `lặp ~1.00s (giống 89%)` | đoạn có chu kỳ: lệch giữa hai frame cách một chu kỳ tụt về gần nhiễu | đó là idle loop; không in khi chỉ là hạt trôi |
| `vùng x,y,w,h` | khung bao chỗ có chuyển động trong đoạn | lấy làm `--roi` cho `track`, `motion` |
| `scale 0.33–0.80s 0.411 → 1.002 dài 0.47s vượt 13%` + `OutBack 0.027, … → Easing.Overshoot(1.18)` | một tween đơn; RMS là lệch tiến độ, dưới ~0.03 là khớp sát | `Tween.Scale(…, 0.47f, Easing.Overshoot(1.18f))`; giá trị đầu là lần **thấy** đầu (mục Giới hạn đo) |
| `dao động 0.948…1.048 chu kỳ 0.98s (5 nửa nhịp, mỗi nửa InOutSine 0.49s)` | nhún lặp: Yoyo, mỗi nửa nhịp là một tween | chu kỳ chính xác hơn (±0.01 s) lấy từ `motion --roi` |
| `dao động … (không ease nào khớp, RMS 0.6)` | pha nhiều chặng hoặc vật đổi dáng | `sheet --step` từng frame; `--csv` xem số |
| `thấy 0/… frame` | mẫu không giống vật ở frame nào | `--ref` lúc vật rõ, `--roi` ôm sát vật, hạ `--min-score` |
| `tương quan độ động 0.3, thấp` | hai video khác nội dung ở vùng so | đặt `--roi` phần khung chỉ chứa anim cần so |
| `nhịp … → B chậm hơn A 8%` | mọi thời lượng bản B dài hơn 8% | chia thời lượng tween cho 1.08, hoặc kiểm `Time.timeScale`, Recorder không Constant |

## Toán

**Alpha.** Vật nửa trong suốt trên nền: $F = a\,T + (1-a)\,B$, với $T$ mẫu, $B$ nền ở `--bg`, $F$ vùng đang đo. Bình phương tối
thiểu theo $a$ cho

$$a = \frac{\sum (F-B)(T-B)}{\sum (T-B)^2}$$

Tử và mẫu lấy trên vùng khớp đã đưa mẫu về đúng scale. Mẫu gần trùng nền (chênh trung bình dưới 5 mức xám) thì bỏ, vì mẫu số
quá nhỏ. Lệch khi vật vừa mờ vừa đổi scale, vì vị trí khớp kém chính xác hơn lúc vật đậm.

**Ease.** Mỗi pha chuẩn hoá về tiến độ $p = (v - v_0)/(v_1 - v_0)$. Với từng ease $f$ trong bảng PrimeTween, dò cặp
$(t_0, t_1)$ cho RMS của $p - f\left(\frac{t - t_0}{t_1 - t_0}\right)$ nhỏ nhất:
- $t_0$ trong một frame quanh frame tĩnh cuối;
- $t_1$ từ frame cuối còn đổi tới hết đoạn đứng sau pha (tối đa 0.3 s).

Đuôi ease-out đổi chưa tới ngưỡng mỗi frame, nên $t_1$ phải được dò cả vào đoạn đứng. RMS trên 0.15 thì không phải một tween đơn.

**OutBack.** $f(t) = 1 + u^2\left((s+1)u + s\right)$, $u = t - 1$. Đạo hàm bằng 0 tại $u = -\frac{2s}{3(s+1)}$, nên đỉnh vượt
là $o = \frac{4s^3}{27(s+1)^2}$. Với $s = 1.70158$ (mặc định PrimeTween) ra $o = 0.100$. Tool đảo hàm này bằng chia đôi để có
$s$ từ độ vượt đo được, rồi in $k = s / 1.70158$ cho `Easing.Overshoot(k)`.

**Dao động.** Ba pha ease trở lên liền nhau, đổi chiều luân phiên, biên độ chênh dưới 50% thì gộp làm một. Pha dò được bỏ phần
gần đứng ở đỉnh, nên mỗi nửa nhịp được fit lại từ đỉnh tới đỉnh. Mốc đỉnh là tâm của dải frame nằm trong 10% biên độ quanh cực
trị, vì đỉnh phẳng và giá trị đo theo bước rời rạc làm frame cực trị nhảy ±1 frame. Chu kỳ là $2 \times$ (đỉnh cuối − đỉnh
đầu) / số khoảng.

**Chu kỳ lặp (`motion`).** $d(L)$ là lệch trung bình giữa frame $i$ và frame $i+L$ ở nửa sau đoạn. Lặp thật thì $d$ lên rồi
tụt về gần nhiễu ở $L = $ chu kỳ; hạt trôi thì $d$ tăng đều. Tỉ lệ là $d(L^*)/\max_{L \le L^*} d(L)$, nhận khi dưới 0.35; đáy
được nội suy parabol để có phần lẻ frame.

**Canh nhịp (`compare`).** Tìm $k, c$ cho tương quan Pearson lớn nhất giữa $A(t)$ và $B(k\,t + c)$. $k$ dò bước 1% trong
$[1/\text{tempo}, \text{tempo}]$, $c$ dò bước $1/60$ s. Sau đó ghép các cặp sự kiện quanh $k\,t_A + c$ rồi fit thẳng
$t_B = k\,t_A + c$ để in nhịp.

## Giới hạn đo

- Scale dò theo bước 1%. Mẫu thu nhỏ dưới khoảng 6 px hay khớp kém hơn `--min-score` thì chưa được tính là thấy. Vì vậy giá trị đầu
  của pha "hiện ra" là lần thấy đầu, không phải giá trị đầu thật: quãng ngắn đi và độ vượt bị đẩy lên. Video tổng hợp scale 0.2→1 với
  `Overshoot(1)` đo ra 0.41→1 và `Overshoot(1.18)`. Thời lượng và tên ease vẫn đúng. Độ vượt thì đọc thêm đỉnh ở `--csv`.
- Mốc thời gian chỉ chính xác tới nửa frame của video, hoặc tới nửa nhịp thật khi bản quay có frame trùng.
- Vật đổi dáng (Spine đổi attachment, chữ đổi viền) thì mẫu chỉ khớp chặng có cùng dáng. Mỗi chặng cần một `--ref` riêng.
- Vật xoay thì không đo được góc. Pha đó đọc bằng `sheet`.
- Nhịp của `compare` chỉ tin được khi có từ ba cặp sự kiện trở lên. Có một cặp thì `k` chỉ là điểm tương quan cao nhất: bản quay có frame
  trùng có thể kéo `k` lệch 10% trong khi mốc từng lớp vẫn khớp. Khi đó đo mốc xuất hiện của từng lớp (màu, vùng) trên cả hai bản rồi so hiệu số.

## Đã sai một lần

- Chạy tool bằng `-I` theo thói quen: báo thiếu OpenCV dù `pip` vừa cài, vì `-I` bỏ qua site-packages của user.
- Ease-out đo ngắn hơn thật khoảng 30%: pha dừng ở frame cuối đổi quá ngưỡng, trong khi đuôi còn trôi dưới ngưỡng. Cần dò $t_1$ vào cả đoạn đứng sau pha.
- Nhún idle báo thành chuỗi tween rời nhau: các nửa nhịp bị tách ở đỉnh, nơi vật gần đứng. Gộp lại rồi mới đủ, vì fit trên pha
  đã cắt đỉnh thì nhún hình sin ra Linear, và chu kỳ tính từ tâm pha ra 0.87 s thay vì 1 s. Phải fit và đo từ đỉnh tới đỉnh.
- Hạt trôi báo `lặp ~0.23s`: phép đo cũ lấy đáy ở lag ngắn nhất rồi so với lệch trung bình. Tiêu chí đúng là đáy thấp hẳn đỉnh ở các lag ngắn hơn.
- Suy frame trùng từ kết quả `track` trùng nhau: báo khoảng 20 hình/s trên video khoảng 27 hình/s, vì scale dò theo bước 1% nên hai frame khác vẫn ra cùng số. Frame trùng chỉ đọc bằng lệch pixel ở `motion`.
- So cả demo hai lượt với bản Unity một lượt: canh ra nhịp 1.15, không ghép được cặp sự kiện nào, vì đường độ động khớp nhầm lượt.
  Cắt mỗi bên một lượt bằng `--a-from`, `--b-to`.
- Mẫu nhỏ ở scale 0.09 khớp nhiễu với điểm 0.65 và tạo ra một pha "bay vào" giả. Cách sửa: `--min-score` mặc định 0.7, và mỗi đoạn thấy liền mạch được mô tả riêng.

## Nghiệm thu

`selftest` phải xanh hết. Video tổng hợp có các đáp án sau.

**Dựng video:**
- Vật 80×50 hiện ở 0.3 s: scale 0.2→1 bằng OutBack trong 0.5 s, alpha bằng Linear trong 0.2 s.
- Trượt x thêm 60 px bằng OutCubic trong 0.6 s từ 1.2 s.
- Nhún scale 1 ± 5%, chu kỳ 1 s, tới hết 4.8 s.
- Nền có chấm sáng ngẫu nhiên mỗi frame.

**Phải đọc ra:**
- OutBack, dài 0.50 ± 0.05 s, `Overshoot` 1.0 ± 0.2.
- OutCubic nằm trong ba ease gần nhất, quãng 60 ± 2 px, dài 0.60 ± 0.05 s.
- Nhún 0.95…1.05, chu kỳ 1.00 ± 0.08 s, mỗi nửa nhịp InOutSine.
- `motion --roi` ra lặp 1.00 ± 0.05 s.
- Alpha lúc đứng ≈ 1.
- `compare` với bản chậm 10%, trễ 0.3 s ra nhịp 1.10 ± 0.02 và trễ 0.30 ± 0.1 s.

**Bảng ease:** khớp mốc biên và các giá trị giữa tính từ `StandardEasing.cs` của PrimeTween. Khi PrimeTween đổi công thức,
sửa `EASES` rồi chạy lại.
