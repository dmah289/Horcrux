Code: `Editor/DeviceProbe/Tools~/device.py`

# DeviceProbe — đo và điều khiển game trên máy Android thật qua adb

Một CLI cho mọi việc agent cần làm với máy đang cắm cáp: đọc máy và app, chụp màn, ghi thao tác người chơi rồi phát lại cả
kịch bản trong một lệnh, ghi FPS mỗi giây kèm mốc, CPU theo luồng, kiểm APK đang cài có bản sửa chưa, lọc logcat, đếm event
analytics app thật sự gửi. Thay cho
việc gõ tay `adb` và script đo viết mỗi lần.

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | agent dò hiệu năng, giật, hành vi chỉ lộ trên máy thật (skill `feel-tune` › Chọn công cụ) |
| Đầu vào | máy Android cắm USB đã cho phép gỡ lỗi; package lấy từ `ProjectSettings` (Android) của dự án chứa thư mục hiện tại, hoặc `--pkg` |
| Ranh giới | đọc máy, gửi chạm, phím, đưa app về foreground, dừng app; không cài, không gỡ, không xoá dữ liệu app; đổi cài đặt máy chỉ ở `res-ab` (độ phân giải), `events` (cờ log Firebase) và giữ màn sáng lúc đo, luôn trả lại |
| Chỗ đặt | `Editor/DeviceProbe/Tools~/`; cần Python 3 và adb (tool tự tìm, mục Nối máy); không có C# |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Cài APK, build | build ký bằng keystore có mật khẩu: developer build và cài, agent kiểm bằng `apk-has` |
| Profiler GPU (counter Mali, Adreno) | cần app debuggable hoặc quyền root; nghẽn GPU vì số pixel kiểm gián tiếp bằng `res-ab` |
| Phân tích thời điểm GPU xong của từng khung | cột của `--latency` khác nghĩa theo bản Android và frame pacing; đã thử, chưa cho kết luận nào |
| iOS | chưa có máy, chưa có nhu cầu |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| FPS đếm khung đã lên màn từ `dumpsys SurfaceFlinger --latency`, không đọc bộ đếm trong game | đo được cả build release, không cần sửa code; số là thứ người chơi thấy |
| Tên layer đọc lại mỗi giây | app quay lại foreground thì SurfaceView mới, tên layer đổi đuôi; giữ tên cũ là đo ra 0 khung |
| Khung "lỡ" so với FPS mục tiêu (`--target`), không với tần số quét | máy 120Hz chạy game 60 FPS thì khung nào cũng dài hơn một nhịp quét |
| `threads` chia theo `/proc/uptime` của máy | đọc stat ~400 luồng và đi qua adb tốn thời gian; chia theo số giây đã xin thì số ms/s phồng lên |
| `mark` ghi thẳng vào file log của `fps` đang chạy nền | thao tác và số đo nằm cùng một trục giờ; `summary` cắt log theo mốc |
| Kịch bản là file chữ (`tap`, `wait`, `mark`…), không phải chuỗi lệnh agent gõ từng bước | đo được: một chạm tốn ~0,1 s, còn mỗi bước agent (gọi tool, chụp, đọc ảnh, chờ cố định) tốn hàng chục giây và nhịp không giống người chơi |
| `record` đọc `getevent` của màn cảm ứng, không đọc log của game | ghi được nhịp tay thật của developer trên build release; file ra là kịch bản sửa tay được |
| `play` gửi chạm qua một `adb shell` giữ sống bằng `cmd input` | đo trên máy 60Hz: `input tap` mỗi lần 155 ms · `cmd input tap` 113 ms · shell giữ sống + `cmd input` 87 ms |

## Nối máy

1. Trên máy: Cài đặt › Thông tin điện thoại › bấm 7 lần **Số hiệu bản tạo** (Build number) → Tuỳ chọn nhà phát triển › bật
   **Gỡ lỗi USB**. Thiếu thì `devices` không thấy máy, dù Windows vẫn hiện máy dạng truyền file (MTP). Xiaomi/HyperOS: bấm 7
   lần **Phiên bản HyperOS**, Tuỳ chọn nhà phát triển nằm trong **Cài đặt bổ sung**, bật thêm **Gỡ lỗi USB (Cài đặt bảo
   mật)** (cần tài khoản Mi), thiếu thì máy từ chối mọi `tap`, `swipe`, `key`.
2. Cắm cáp, máy hỏi **Cho phép gỡ lỗi USB?** → Cho phép (tick luôn cho máy tính này). Bỏ qua thì máy hiện `unauthorized`.
3. `device.py devices` in đường dẫn adb và danh sách máy. Tool tìm adb theo thứ tự: biến `ADB` → `adb` trên PATH → adb đi kèm
   Android Build Support của đúng bản Unity trong `ProjectVersion.txt` (thư mục Hub, cả `secondaryInstallPath.json`) → bản
   Unity khác → `ANDROID_HOME`, `ANDROID_SDK_ROOT` → `%LOCALAPPDATA%\Android\Sdk`.
4. Nhiều máy: `--serial <mã>` hoặc biến `ANDROID_SERIAL`.

## Dùng

```bash
D="python -X utf8 -I <horcrux>/Editor/DeviceProbe/Tools~/device.py"
$D info                                     # máy, tần số quét, bản app, nguồn cài, save có phải bản khôi phục từ backup, pid, độ phân giải render
$D shot a.jpg --scale 0.35                  # chụp màn thu nhỏ (~40 KB), in công thức đổi về toạ độ tap
$D shot b.jpg --crop 0,900,1080,1400        # chỉ vùng cần soi
$D record play1.txt --seconds 90            # developer chơi trên máy, ra file kịch bản đúng toạ độ và nhịp tay
$D play play1.txt --fps-out fps.log         # phát lại cả kịch bản trong một lệnh, ghi FPS song song, in tóm tắt theo mốc
$D play play1.txt --speed 2                 # chờ và vuốt nhanh gấp đôi (chạm không đổi)
$D tap 540 1596 --wait 2 --shot c.jpg --scale 0.35  # một thao tác lẻ: chạm, chờ, chụp trong một lệnh
$D tap 118 1584 278 1584 440 1584 --gap 0.3 # nhiều chạm liền
$D home --wait 8; $D resume                 # ra nền 8 s rồi quay lại như bấm icon (không cần chạm giả lập)
$D fps --seconds 600 --out fps.log          # chạy nền: mỗi giây một dòng
$D mark fps.log "mở popup"                  # mốc vào cùng log
$D mark fps.log "hết" --stop                # `fps` nền dừng ở giây kế tiếp và trả cài đặt máy; không kill
$D summary fps.log                          # TB, thấp nhất, lỡ/giây theo từng đoạn giữa hai mốc
$D threads --seconds 5                      # ms CPU mỗi giây của các luồng nặng nhất
$D apk-has _NewField NewClassName           # APK đang cài có chuỗi này trong code chưa
$D log --grep "Unity|GOS:" --max 40         # logcat đã lọc, khử dòng trùng, giữ dòng mới nhất
$D events --script play1.txt --params placement,ad_format  # event Firebase app gửi trong lúc chơi, so với callback doanh thu
$D res-ab --size 720x1600 --seconds 25 --yes  # FPS ở độ phân giải gốc rồi độ phân giải nhỏ, luôn trả lại
$D adb shell dumpsys gfxinfo <pkg>          # adb thô với đúng đường dẫn và máy
```

| Lệnh | Ghi chú |
|---|---|
| `shot` | `--scale` cần Pillow (tool tự thêm thư mục gói của user vào path khi chạy `-I`); thiếu thì lưu PNG gốc và báo; ảnh PNG/JPG theo đuôi file |
| `record` | ghi ngón đầu: chạm → `tap`, dời quá `--move` px hoặc giữ quá 0,5 s → `swipe` kèm thời lượng, khoảng nghỉ → `wait`; chạm đa điểm bỏ; toạ độ quy về `wm size` hiện tại |
| `play` | dòng: `tap x y` · `swipe x1 y1 x2 y2 [ms]` · `key HOME` · `home` · `wait <giây>` · `resume` · `mark <chữ>` · `shot <file> [scale]` · `#` chú thích; lệnh lạ thì từ chối cả file trước khi chạy |
| `tap`, `swipe`, `key`, `home`, `resume` | thao tác lẻ khi chưa biết bước sau; `--wait <giây>` rồi `--shot <file> --scale` |
| `fps` | `--target 60` (mặc định); dòng `HH:MM:SS fps= lỡ= max=`; dòng `-` là app không ở trước mặt |
| `summary` | `--skip 2` bỏ giây đầu mỗi đoạn (chuyển cảnh); `--last <n>` chỉ n giây cuối |
| `res-ab` | không có `--yes` thì chỉ in lời nhắc: đổi độ phân giải toàn máy, phải hỏi developer trước |
| `apk-has` | kéo `base.apk` về thư mục tạm, tìm chuỗi (UTF-8, UTF-16) trong `assets/bin/Data/Managed/` (metadata IL2CPP, dll Mono), xoá file tạm |
| `stop` | `am force-stop`, giữ dữ liệu app |
| `events` | in nguồn cài (`installerPackageName`), bật `debug.firebase.analytics.app` và `log.tag.FA`/`FA-SVC` VERBOSE, mở lại app, ghi logcat trong `--seconds` hoặc suốt `--script`, trả cờ trong `finally`; in số event theo tên kèm giá trị `--params`, user property, callback doanh thu (`--revenue`, mặc định AppLovin MAX Unity) và dòng so với `--impression`; `--log` giữ logcat thô |

## Chơi qua kịch bản nhanh và giống người chơi

1. Có developer: `record` trong lúc developer chơi đúng chuỗi họ báo lỗi — nhịp tay, chỗ chạm, lúc ra nền là của người thật.
2. Không có: một `shot --scale 0.35` mỗi màn mới để lấy toạ độ, viết cả đoạn vào file kịch bản (`wait` 0,5–1 s giữa hai chạm
   như người chơi, 2–3 s sau chuyển màn), thêm `mark` trước mỗi trạng thái cần đo và `shot` ở điểm kiểm.
3. `play --fps-out`: một lệnh chạy hết, đọc ảnh điểm kiểm để chắc kịch bản đi đúng đường, rồi đọc bảng tóm tắt.
4. Lệch đường (popup lạ, quảng cáo) thì sửa file từ dòng lệch rồi chạy lại đoạn đó, không lùi về đi từng bước.

## Đo một nghi vấn FPS

1. `info`: tần số quét (60 hay 90/120), bản app đang cài đúng build cần đo chưa (`apk-has` với tên field, class vừa thêm).
2. `play --fps-out` với kịch bản có `mark` trước mỗi trạng thái màn cần so; giữa chừng cần tay thì `fps --out` chạy nền,
   `mark` từng lần.
3. Mỗi trạng thái giữ **ít nhất 20 giây**, `summary` so theo đoạn. Hai trạng thái cần so thì đo liền nhau.
4. Tách nghẽn: `threads` — luồng main ms/s chia FPS là ms CPU mỗi khung, nhỏ hơn nhiều so với 16.7 thì không do CPU game;
   `res-ab` trên đúng màn chậm — lên rõ khi giảm pixel là nghẽn ở số pixel GPU vẽ (lớp phủ toàn màn, vẽ chồng).
5. Lỗi chập chờn (FPS kẹt sau khi app về foreground): kịch bản lặp N lượt `home` · `wait <giây nền>` · `mark lượt i` ·
   `resume` · `wait 13`, `play --fps-out` rồi `summary --skip 1` ra mỗi lượt một dòng. Chạy trên build có bản sửa **và**
   build không có (developer cài lại), báo số lượt kẹt k/N mỗi bên. N đủ khi `(1 − p)^N` nhỏ, p là tỉ lệ kẹt đo ở build cũ.

## Kiểm event analytics và doanh thu quảng cáo

Dùng khi báo cáo analytics lệch với hành vi game (event thiếu tham số, dồn về giá trị mặc định, một nhóm user không có event).

1. `events --script <kịch bản>` (hoặc `--seconds` khi developer chơi tay) đi qua đúng chỗ bắn event cần kiểm; cheat được dùng
   khi nó không chạm thứ đang kiểm (`workflow` › Nghiệm thu).
2. Đọc ba bảng: event theo tên và giá trị tham số · user property · callback doanh thu của SDK quảng cáo. Dòng `so:` lệch
   (callback có, `ad_impression` không, hoặc ngược lại) là doanh thu đi một cổng khác với event của game.
3. Nguồn cài khác store mà game có anti-cheat theo nguồn cài: cổng analytics của game có thể chặn event của chính nó trong
   khi SDK quảng cáo vẫn gửi doanh thu thẳng.

## Bẫy

| Bẫy | Hệ quả, cách xử |
|---|---|
| Gán FPS tụt cho thao tác vừa làm (ra nền, mở popup) | thứ đang hiện trên màn lúc đó mới là nguyên nhân; so theo trạng thái màn bằng `mark`, không theo thao tác |
| Máy Samsung: Game Optimizing Service | coi app là game: siết trần xung CPU, GPU theo tải (logcat `HYPER-HAL … MaxFreq`, có lúc GPU còn ~300 MHz), nới tạm vài giây khi app vào foreground, và tự render game ở 75% độ phân giải (`info` › render). FPS cùng một màn trôi theo thời gian: so bằng TB nhiều giây, đo hai bên liền nhau |
| Banner quảng cáo động | WebView vẽ cùng GPU, nội dung đổi mỗi lần refresh: thêm nhiễu cho FPS |
| Không chạm màn 5 phút trên máy Samsung | Game Booster phủ lớp khoá chạm ("Drag Lock icon to unlock"): `swipe` từ icon khoá ra xa để mở |
| `tap` theo toạ độ ảnh đã thu nhỏ | toạ độ là pixel màn; `shot --scale/--crop` in công thức đổi, ảnh còn bị trình xem thu nhỏ thêm thì nhân cả tỉ lệ đó |
| `record` khi agent tự chạm bằng `tap`, `play` | chạm tiêm qua `input` không đi qua thiết bị cảm ứng nên `getevent` không thấy; `record` chỉ ghi ngón tay thật |
| Kịch bản viết cho một độ phân giải | toạ độ gắn với `wm size` lúc ghi (dòng đầu file); máy khác hay sau `wm size` thì ghi lại hoặc quy đổi |
| Sau `res-ab` hay `wm size` tay | layout game đổi theo tỉ lệ mới; luôn `wm size reset` (`res-ab` tự trả trong `finally`) và soát lại bằng `info` |
| `logcat` không lọc | vài nghìn dòng mỗi phút; luôn `log --grep`, logcat cũ thì `log --clear` trước kịch bản |
| Đếm event Firebase mà không mở lại app | cờ debug chỉ có hiệu lực từ lần mở app sau; `events` mặc định mở lại, `--no-restart` thì có thể ra 0 dòng FA dù app có gửi |
| Máy từ chối chạm giả lập (`SecurityException … INJECT_EVENTS`, hay gặp ở Xiaomi) | lỗi về stderr, `input` vẫn trả 0: tool dừng ở chạm đầu và in cách bật (mục Nối máy); `adb shell input` gõ tay thì đọc stderr |
| Log FPS toàn dòng `-` dù app chạy | dòng `-` ghi lý do: `màn tắt`, `màn khoá` (màn tự tắt sau vài phút không chạm, nhất là lúc app nằm nền; máy có mật khẩu thì agent không mở được, nhờ developer), hoặc `app không ở trước mặt`. `fps`, `play`, `record`, `res-ab`, `events` giữ màn sáng khi cắm USB (`svc power stayon usb`) và trả cài đặt cũ sau đó. Tên layer Android 16 khác bản cũ (`RequestedLayerState{… SurfaceView[<pkg>/…](BLAST)#id …}`): tool đọc cả hai; bản Android mới đổi tiếp thì sửa `surface_layer` |
| Dừng `fps` nền bằng `kill` | trên Windows `kill` của Git Bash dừng hẳn Python, bước trả cài đặt không chạy: máy giữ màn sáng mãi khi cắm USB. Dừng bằng `mark <log> <chữ> --stop`; lỡ kill thì `adb shell svc power stayon false` |
| Shell ghi cài đặt hệ thống bị từ chối (Xiaomi: `settings put global` cần `WRITE_SECURE_SETTINGS`) | đổi rồi trả bằng lệnh dịch vụ (`svc power stayon`), không bằng `settings put`; lỗi về stderr nên đọc lại giá trị sau khi trả |
| Gỡ app cài lại mà save cũ vẫn còn | Android Auto Backup khôi phục dữ liệu app ngay lúc cài, kể cả APK cài tay, khi manifest không đặt `allowBackup="false"`; hạ bản thì Android bỏ khôi phục ("Restore version higher") và app ra save sạch. `info` › dữ liệu in kết cục khôi phục của chính lần cài đang có. Cần save sạch thì `adb shell pm clear <pkg>` (hay Xoá dữ liệu trong cài đặt app) |
| Gọi `adb` từ PATH | máy dev hay không có; dùng `device.py adb …` để lấy đúng adb của bản Unity |
