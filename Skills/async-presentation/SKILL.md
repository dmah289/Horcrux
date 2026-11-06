---
name: async-presentation
description: Dùng khi code chạm async, CancellationToken, vòng đời object, pool, tween, Addressables, hoặc trình diễn một thay đổi cho người chơi (mốc đã thấy, điều phối nhiều module, vật thể có chất liệu).
---

# async-presentation — async, vòng đời, trình diễn

Skill nền dưới `Skills/horcrux_skill.md` (NT, ba tầng, ký hiệu định nghĩa ở đó).

Tiêu chí: **huỷ được** theo owner · **giải phóng được** (thứ giữ tài nguyên có đường trả) · **cô lập được lỗi** (`try/catch`
quanh từng callback trong vòng dispatch).

- **Mỗi loop chỉ ra token của nó là đời của ai, dài đúng bằng nhịp.** Token của pha ngắn hơn đời loop thì loop **chết im
  lặng** (huỷ là hợp đồng, không log). Loop không chủ thì ở Editor tắt domain reload mỗi lần Play xếp thêm một loop.
- **Hàm mở tiến trình sở hữu trạng thái cuối ở mọi đường ra** (xong, huỷ, thời lượng 0) qua **một** `try/finally` bọc toàn
  thân — trạng thái cuối và callback đúng một lần. Caller chỉ `await`, ghép `WhenAll`.
- **Chạm object Unity thì `finally` và callback guard `if (x != null)`**, không `?.`/`??` (bỏ qua so null của Unity).
  `onComplete` một chủ. Về pool giữa chừng cũng là đường ra: bỏ liên kết **trước** khi dừng tween, sau `await` kiểm liên kết
  còn là của mình mới ghi.
- **Object về pool không chứa con tự huỷ theo đồng hồ riêng** — hệ hạt Stop Action `None`, không `Destroy(go, t)`; kéo dài
  trình diễn thì soát mọi đồng hồ ẩn của prefab.
- **Gộp hai tween vào một slot chỉ khi cùng thuộc tính** — khác thuộc tính thì dừng là cắt ngang chuyển động đang hiện.
- **`CancellationTokenSource` có một chủ dọn: chỗ tạo**; nơi khác chỉ `?.Cancel()` (`Cancel` chạy đồng bộ phần còn lại,
  `finally` của chủ có thể đã `Dispose`).
- **Hàm mở lượt bằng tài nguyên một lần dùng thì từ chối tái nhập** (`if (isOpen) return;`) — chồng lượt làm người chờ source
  cũ treo vĩnh viễn.
- **Huỷ giữa chừng phân loại theo thứ mất đi:** dữ liệu (tiến độ, grant, save) nhất quán trên mọi đường — grant trước anim;
  trình diễn chỉ cần không để rác — snap đích, tắt.

> **Nền tảng** — stack Horcrux:
> - **UniTask**, không coroutine, không `Task`: propagate `CancellationToken` toàn chain; tham số token không `= default`
>   khi mọi caller có token. `finally` trong `while` chạy mỗi vòng. `UniTask.Yield(ct)` **ném** khi huỷ — kiểm
>   `IsCancellationRequested` sau nó là code chết. `UniTask` là struct: field chưa gán báo **Succeeded**, `await` trả ngay.
> - **Addressables** qua `AssetReference` cho đơn vị nạp theo nhu cầu (màn, popup): không string key, giữ handle để
>   `Release()`. Prefab con trong cha đã nạp thì kéo thẳng — nhưng con chia đời với cha.
> - **PrimeTween**, không tự viết runner: UI đặt `useUnscaledTime`; object huỷ hay về pool thì dừng tween; exception trong
>   callback bị bọc thành log chung — `PRIME_TWEEN_SAFETY_CHECKS` để biết tween nào. `startDelay` lặp ở **mỗi** cycle; trễ
>   một lần thì `ChainDelay` trong Sequence.
> - Anim UI dùng `Time.unscaledDeltaTime` (popup chạy khi `timeScale` = 0) · data lớn cho GPU, Job: `NativeArray` · tài
>   nguyên nặng (`RenderTexture`, `Texture2D`) cache, dọn ở `OnDestroy()`.
> - Tham số mặc định của method `virtual` lấy theo kiểu khai báo lúc gọi, không theo override.

## Trình diễn một thay đổi cho người chơi

Dữ liệu đổi ở một nơi, người chơi thấy ở nơi khác, có khi sau nhiều lần đổi.

- **Mốc "đã thấy" nằm trong save, cạnh sự thật; ghi khi diễn xong.** Trượt từ A tới B cần A người chơi đã thấy
  (`currAnimatedScore`); A trong instance view là sai (view tắt, dựng lại, nhiều instance). Diễn từ mốc tới sự thật **một**
  lần, ghi mốc ở một chỗ cho cả snap lẫn trượt; huỷ giữa chừng thì lần sau diễn lại; reset chu kỳ reset cả mốc.
- **Component dùng chung mặc định thụ động:** bật lên đứng ở mốc, trượt khi host gọi `PlayAsync(ct)`; không cờ chế độ.
- **Hiệu ứng "báo có thay đổi" ở đúng một điểm**, chỗ khác vẽ tĩnh.
- **Trạng thái nghỉ dựng trong prefab, đích là ô developer đặt, mỗi lần hiện đặt lại về nghỉ.** Tween lên thuộc tính đã
  author **nhân hoặc cộng trên giá trị nghỉ đọc ở `Awake`**, reset về nghỉ, không về `one`/`zero`. *Đã sai một lần:* nhún
  quanh scale 1 trên node scale khác 1 — co vĩnh viễn · gán scale 0 rồi mới bật object tắt sẵn — `Awake` chụp nghỉ là 0.
- **Vật hiện ra lần đầu đã đứng ở chỗ nghỉ:** bố cục tính được thì đặt cùng frame tạo vật; chờ frame chỉ khi đọc từ thứ
  engine dựng sau (layout UI, bounds), và ẩn vật tới lúc đặt xong.
- **Lớp trên đè lớp dưới**, không bật/tắt thứ bị che hết · **giá trị đổi lúc tới nơi**, không lúc bắt đầu · **bước thiếu dữ
  liệu thì bỏ bước**, không rẽ nhánh cả luồng · **service overlay giữ cơ chế, nội dung là canvas của consumer**.
- **Nhiều module cùng trình diễn khi vào một màn → một điều phối viên theo giai đoạn cố định.** Lease, hàng đợi, chờ frame
  chỉ vá triệu chứng. Giai đoạn chia theo **loại** trình diễn, cố định trong code; ưu tiên chỉ xếp trong giai đoạn. Luồng
  dùng tài nguyên độc quyền chạy lần lượt, luồng chỉ chạm view mình chạy đồng thời. Mọi luồng **idempotent** (đọc save, làm
  phần còn nợ, trả tài nguyên trong `finally`, `try/catch` từng luồng). Tín hiệu vào/ra màn qua **một** bridge. Vòng điều
  phối là class thuần. Cơ chế: `docs/Runtime/LiveOpsHomeFlow/LiveOpsHomeFlow.md`.
- **Nhiều module cùng mất gì đó ở một sự kiện → định nghĩa sự kiện ở một chỗ**; bắt đầu, huỷ chờ, ghi mất mát có một chủ
  gọi; cờ chờ trong save từng module, gỡ cờ **trước** khi kiểm còn gì để mất; luật vòng đời cờ ở lớp cha, không `virtual`.

## Trình diễn vật thể có chất liệu

Chất liệu quyết *cái gì được biến dạng*; phần vui đến từ nhịp và phản ứng của môi trường, không từ bóp méo vật.

- **Vật cứng không biến dạng khi bay;** chỉ nghiêng theo hướng bay (2.5D: ≤ ±12°). Cú "nặng" đi vào vật nhận (lún, nảy,
  loé, hạt) và bóng tách khỏi vật để đọc độ cao.
- **Sprite có cạnh dày (2.5D): tách gốc và thân, bóng là con của gốc** — gốc giữ vị trí và scale chủ đặt, hiệu ứng ghi lên
  thân; reset thân là về nghỉ.
- **Vật bay giữa hai chủ: một node mang toàn bộ cỡ nhìn thấy;** rời cha giữ cỡ thế giới, mỗi frame một hàm đường cong đặt
  cỡ, chủ mới chia lại cỡ ở một chỗ lúc đáp.
- **Đích chuyến bay đọc lại mỗi frame, không chốt lúc cất cánh:** chủ mới có thể đang trượt, lún, căn lại; chốt thì vật đáp
  chỗ cũ rồi giật. Giá là một lần đọc vị trí mỗi frame — rẻ hơn mọi cờ chọn vật nào được bám.
- **Cú thu thập:** đích cỡ theo lòng vật chứa, nhỏ hơn đỉnh chuyến bay, cỡ về 0 tốc độ lúc chạm.
- **Sprite giữ tỉ lệ hoạ sĩ vẽ;** cỡ và vị trí đo pixel trên ảnh đích, quy về phần đặc của sprite.

> **Nền tảng** — autosize TextMeshPro chỉ co khi tràn **chiều cao**; từ rộng hơn bề ngang bị ngắt giữa từ. "Tối đa n dòng"
> = khung cao n dòng + test quét dữ liệu thật (`ForceMeshUpdate(true)` rồi `textInfo`). Autosize UniText co theo **cả hai**
> chiều; đo chữ ngay sau khi đặt bằng `MeasureText` truyền `maxWidth` và `maxHeight` của khung — thiếu `maxWidth` là đo
> không wrap, `GetRangeBounds` ngoài vòng render chưa có vị trí dòng. `SpriteMask` không bật Custom Range
> ghi stencil cho **mọi** tầng dưới nó — giới hạn dải về đúng tầng nó phục vụ. `SpriteRenderer` Sliced, Tiled vẽ theo
> `size`; `sprite.bounds` vẫn là cỡ gốc của ảnh — đo phần nhìn thấy theo `drawMode`, đổi art sang Sliced là đổi số đo.
