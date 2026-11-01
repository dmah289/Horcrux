---
name: async-presentation
description: Dùng khi code chạm async, CancellationToken, vòng đời object, pool, tween, Addressables, hoặc trình diễn một thay đổi cho người chơi (mốc đã thấy, điều phối nhiều module, vật thể có chất liệu).
---

# async-presentation — async, vòng đời, và trình diễn

Skill nền của Horcrux, đứng dưới `Skills/horcrux_skill.md`: `NT<n>`, ba tầng Luật · Nền tảng · Sổ tay và bảng
chỉ dẫn định nghĩa ở đó. Con trỏ `` `skill` › Mục `` trỏ sang skill khác; `mục X` trỏ trong file này.

Tiêu chí: **hủy được** (việc dừng theo owner) · **giải phóng được** (thứ giữ tài nguyên có đường trả lại)
· **cô lập được lỗi** (`try/catch` quanh từng callback trong vòng dispatch).

| Luật | Nghĩa là |
|---|---|
| **Mỗi loop chỉ ra được token của nó là đời của ai, dài đúng bằng nhịp** | token của pha ngắn hơn đời loop thì loop **chết im lặng**: hủy là hợp đồng, thư viện async không log. Loop không chủ thì ở Editor tắt domain reload mỗi lần Play xếp thêm một loop. Thân vòng lặp và số cấu hình về nơi giữ bảo đảm; token ở lại host vì host có đời sống |
| **Hàm mở một tiến trình sở hữu trạng thái cuối ở mọi đường ra** | xong, bị hủy, thời lượng 0 — cả ba qua **một** `try/finally` bọc toàn thân, nên trạng thái cuối và callback xảy ra đúng một lần. Caller chỉ `await` và ghép `WhenAll` |
| **Tiến trình chạm object Unity thì `finally` và callback guard `!= null`** | Destroy giữa chừng là đường ra thật. Guard bằng `if (x != null)`, không `?.`/`??`: chúng bỏ qua phép so null overload của Unity. `onComplete` chỉ có một chủ. Về pool giữa chừng cũng là đường ra thật: bỏ liên kết **trước** khi dừng tween, sau `await` kiểm liên kết còn là của mình mới ghi tiếp |
| **Object về pool không chứa con tự huỷ theo đồng hồ riêng** | đời con là đời chủ, pool quyết: hệ hạt Stop Action `None`, không `Destroy(go, t)`. Đồng hồ ẩn chạy đua với trình diễn: trình diễn ngắn hơn thì lỗi nằm im, tune dài ra là reset chạm object đã huỷ, lỗi ném trong callback cuối nên object không về pool. Kéo dài một trình diễn thì soát mọi đồng hồ ẩn của prefab |
| **Gộp hai tween vào một slot chỉ khi cùng thuộc tính** | khác thuộc tính thì dừng là cắt ngang một chuyển động đang hiện; soát cả guard đọc `isAlive` của slot |
| **`CancellationTokenSource` có đúng một chủ dọn: chỗ tạo** | nơi khác chỉ `?.Cancel()`. `Cancel` chạy đồng bộ phần còn lại ngay trong lời gọi, `finally` của chủ có thể đã `Dispose` trước khi bên huỷ tới dòng kế |
| **Hàm mở lượt sống bằng tài nguyên một lần dùng thì từ chối tái nhập** | `Open()` chồng lên lượt đang mở là người đang chờ giữ source cũ treo vĩnh viễn; `if (isOpen) return;` rẻ hơn mọi kỷ luật |
| **Hủy giữa chừng phân loại theo thứ mất đi** | dữ liệu (tiến độ, grant, save) nhất quán trên mọi đường — grant trước anim. Trình diễn chỉ cần không để rác: snap đích, tắt |

> **Nền tảng** — lựa chọn mặc định và ràng buộc đi kèm:
>
> | Nhu cầu | Dùng | Ràng buộc không bỏ được |
> |---|---|---|
> | Async | **UniTask**, không coroutine, không `Task` | propagate `CancellationToken` toàn chain; tham số token **không mang `= default`** khi mọi caller đều có token |
> | Load asset | **Addressables** qua `AssetReference`, cho đơn vị nạp theo nhu cầu (màn, popup) | không string key; giữ handle để `Release()`. Prefab con trong cha đã nạp thì kéo thẳng — nhưng con chia đời sống với cha: thứ phải hiện khi cha đang đóng không được là con của cha |
> | Tween | **PrimeTween**, không tự viết runner | UI đặt `useUnscaledTime`; object huỷ hay về pool thì dừng tween của nó · exception trong callback bị bọc thành log chung, stack thật ở log ngay sau; define `PRIME_TWEEN_SAFETY_CHECKS` để biết tween nào |
> | Anim UI | `Time.unscaledDeltaTime` · `DelayType.UnscaledDeltaTime` | popup chạy khi `timeScale` bằng 0 |
> | Data lớn | `NativeArray` / `NativeList` | khi truyền GPU hoặc Job System |
> | Tài nguyên nặng | cache `RenderTexture`, `Texture2D`… | dọn trong `OnDestroy()` |
>
> `finally` trong `while` chạy mỗi vòng — chỉ một `try/finally` bọc toàn thân mới cho "đúng một lần".
> `UniTask.Yield(ct)` **ném** khi bị hủy, nên `if (ct.IsCancellationRequested)` sau nó là code chết. Mặc
> định tham số của method `virtual` lấy theo kiểu khai báo lúc gọi, không theo override. `UniTask` là struct:
> field chưa gán là `default` báo **Succeeded**, `await` trả về ngay — field chờ gán ở đầu mỗi cửa public (`code-design` › Bất biến).

**Trình diễn một thay đổi cho người chơi.** Dữ liệu đổi ở một nơi và người chơi thấy nó ở nơi khác, có khi
sau nhiều lần đổi.

| Luật | Nghĩa là |
|---|---|
| **Mốc "đã thấy" nằm trong save, cạnh sự thật; ghi mốc khi diễn xong** | mọi thứ trượt từ A tới B cần một A người chơi đã xác nhận bằng mắt (`currAnimatedScore` — quá khứ phân từ, `code-design` › Naming). A trong instance view là sai: view bị tắt, dựng lại, nhiều instance. Trình diễn đi từ mốc tới sự thật **một** lần; ghi mốc khi diễn xong, ở một chỗ cho cả snap lẫn trượt — huỷ giữa chừng thì mốc chưa nhích, lần sau diễn lại. Reset chu kỳ reset cả mốc. Trình diễn tách bước thì save giữ cấu tạo của đúng một lần đổi chưa xem |
| **Component dùng chung mặc định thụ động; host dàn dựng gọi API `await` được** | bật lên đứng ở mốc; trượt khi host gọi `PlayAsync(ct)`. Không cờ chế độ trên instance (`code-design` › Hình dạng) |
| **Hiệu ứng "báo có thay đổi" ở đúng một điểm; chỗ khác vẽ tĩnh** | hai màn cùng hiệu ứng là diễn hai lần một thay đổi |
| **Trạng thái nghỉ dựng trong prefab; đích là ô developer đặt; mỗi lần hiện đặt lại về nghỉ** | vị trí lộ là `[SerializeField]`, không đọc ngầm ở `Start` (`editor-first`). **Tween lên thuộc tính đã author nhân hoặc cộng trên giá trị nghỉ đọc ở `Awake`, không ghi tuyệt đối; reset về nghỉ, không về `one`/`zero`.** *Đã sai một lần:* nhún ghi `localScale` quanh 1.0 lên node prefab đặt scale khác 1 — co vĩnh viễn sau lần đầu, không một dòng log. *Đã sai một lần:* object tắt sẵn chỉ `Awake` lúc bật lần đầu — gán scale 0 rồi `SetActive(true)` thì mốc nghỉ chụp là 0; bật trước, ghi sau |
| **Lớp trên đè lớp dưới, không bật/tắt thứ bị che hết** | bật/tắt theo vị trí là thêm một trạng thái phải đồng bộ với tween |
| **Giá trị đổi lúc tới nơi, không lúc bắt đầu** | đổi giữa đường là hai thông tin mâu thuẫn trên một vật |
| **Bước thiếu dữ liệu thì bỏ bước, không rẽ nhánh cả luồng** | nhánh "khỏi diễn" là đường trình diễn thứ hai phải đồng bộ |
| **Service overlay giữ cơ chế; nội dung là canvas của consumer** | kịch bản mới là consumer mới, không sửa service (`code-design` › Hình dạng) |
| **Nhiều module cùng trình diễn khi vào một màn thì một điều phối viên theo giai đoạn cố định** | module tự khởi động thì chồng nhau; lease, hàng đợi, chờ một frame chỉ vá triệu chứng. Giai đoạn chia theo **loại** trình diễn, cố định trong code; ưu tiên chỉ xếp trong cùng giai đoạn. Luồng dùng **tài nguyên độc quyền** chạy lần lượt, luồng chỉ chạm view của mình chạy đồng thời. Mọi luồng **idempotent**: đọc save, làm phần còn nợ, trả tài nguyên trong `finally`, `try/catch` quanh từng luồng. Tín hiệu vào/ra màn qua **một** bridge của dự án. Vòng điều phối là class thuần để test không cần engine. Cơ chế đầy đủ: tài liệu hệ điều phối trong Horcrux |
| **Nhiều module cùng mất gì đó ở một sự kiện thì định nghĩa sự kiện ở một chỗ** | "thế nào là thua" khai một chỗ cho mọi module; bắt đầu chờ, huỷ chờ, ghi mất mát có một chủ gọi (`code-design` › Bất biến); cờ chờ trong save của từng module, gỡ cờ **trước** khi kiểm còn gì để mất. Luật vòng đời cờ ở lớp cha, không `virtual` |

**Trình diễn vật thể có chất liệu.** Chất liệu quyết *cái gì được phép biến dạng*; phần vui đến từ nhịp và
phản ứng của môi trường, không từ bóp méo vật.

| Luật | Nghĩa là |
|---|---|
| **Vật liệu cứng không biến dạng khi bay; trọng lượng va chạm đi vào vật nhận và bóng đổ** | squash/stretch lên vật cứng biến nó thành thạch. Vật bay giữ tỉ lệ, chỉ nghiêng vào hướng bay (2.5D không quá ±12°); cú "nặng" đi vào vật nhận (lún, nảy, loé, hạt) và bóng tách khỏi vật để đọc độ cao |
| **Sprite 2.5D có cạnh dày: tách gốc và thân; bóng là con của gốc** | gốc giữ vị trí và scale do chủ (ô chứa) đặt; mọi hiệu ứng ghi lên thân, nên không ghi đè thứ ô đã đặt và reset thân là đủ về nghỉ. Bóng treo ở gốc thì thân nhấc lên bóng vẫn ở đất |
| **Vật bay giữa hai chủ: một node mang toàn bộ cỡ nhìn thấy; chủ mới chia lại cỡ ở đúng một chỗ lúc đáp** | rời cha giữ cỡ thế giới, không gán cỡ đích trước khi cất cánh; mỗi frame một hàm đường cong đặt cỡ; hàm đáp là chỗ duy nhất chia cỡ giữa gốc và thân. Hai kênh to lên nối nhau đọc thành nhịp rối |
| **Cú thu thập: đích cỡ theo lòng vật chứa, nhỏ hơn đỉnh chuyến bay** | vật "chui vào" chỉ khi đích nhỏ hơn đỉnh và cỡ về 0 tốc độ lúc chạm; đích lớn hơn cỡ lúc bay đọc thành vật to hơn vật chứa |
| **Sprite art giữ tỉ lệ hoạ sĩ vẽ; cỡ và vị trí lấy từ ảnh đích** | scale không đều là dãn hình vẽ. Có ảnh đích thì đo pixel trên đó, quy về phần đặc của sprite gốc. Sprite đúng cỡ mà lộ thứ vật thật che thì tắt thứ đó lúc bị che |

> **Nền tảng** — autosize của TextMeshPro chỉ co chữ khi tràn **chiều cao**; từ rộng hơn bề ngang bị ngắt
> giữa từ. "Tối đa n dòng, tràn thì co" = khung cao đúng n dòng cộng test quét dữ liệu thật; đo số dòng bằng
> `ForceMeshUpdate(true)` rồi `textInfo`. `SpriteMask` không bật Custom Range ghi stencil cho **mọi** tầng
> và sorting group dưới nó, làm mask cục bộ của entity khác mất tác dụng — giới hạn dải về đúng tầng nó phục vụ.
