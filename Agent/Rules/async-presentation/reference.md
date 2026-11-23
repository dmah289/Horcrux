# async-presentation — tra cứu

Nền tảng và Sổ tay của `async-presentation`. Đọc mục được bảng Tra cứu trong `SKILL.md` gọi.

## Stack async

> **Nền tảng** — stack Horcrux:
> - **UniTask**, không coroutine, không `Task`: propagate `CancellationToken` toàn chain; tham số token không `= default`
>   khi mọi caller có token. `finally` trong `while` chạy mỗi vòng. `UniTask.Yield(ct)` **ném** khi huỷ — kiểm
>   `IsCancellationRequested` sau nó là code chết. `UniTask` là struct: field chưa gán báo **Succeeded**, `await` trả ngay.
>   `Forget`, `UniTaskVoid` nuốt `OperationCanceledException`.
> - **Addressables** qua `AssetReference` cho đơn vị nạp theo nhu cầu (màn, popup): không string key, giữ handle để
>   `Release()`. Prefab con trong cha đã nạp thì kéo thẳng — nhưng con chia đời với cha. Reference gãy (GUID mất, asset
>   chưa là entry) chỉ lộ ở lần nạp: sau khi chép, đổi tên, xoá asset thì soát bằng `docs/Editor/AddressableReferenceAudit.md`.
> - **PrimeTween**, không tự viết runner: UI đặt `useUnscaledTime`; object huỷ hay về pool thì dừng tween; exception trong
>   callback bị bọc thành log chung — `PRIME_TWEEN_SAFETY_CHECKS` để biết tween nào. `startDelay` lặp ở **mỗi** cycle; trễ
>   một lần thì `ChainDelay` trong Sequence. `await` tween hay Sequence bị `Stop()` trả về như chạy xong — dừng kiểu này
>   thì kiểm token của mình sau `await`. Gắn `SetCancellationToken(ct)` thì huỷ làm `await` **ném**
>   `OperationCanceledException`: dòng kiểm token sau đó là code chết; handler `async void`, `UniTaskVoid` bắt nó riêng trước
>   catch chung, không thì huỷ đúng hợp đồng thành log đỏ.
> - Anim UI dùng `Time.unscaledDeltaTime` (popup chạy khi `timeScale` = 0) · data lớn cho GPU, Job: `NativeArray` · tài
>   nguyên nặng (`RenderTexture`, `Texture2D`) cache, dọn ở `OnDestroy()`.
> - Tham số mặc định của method `virtual` lấy theo kiểu khai báo lúc gọi, không theo override.
> - Mobile: `OnApplicationPause(true)` bắn cả khi hộp thoại hệ thống, quảng cáo, share sheet che app; crash, kill không bắn
>   gì.

- **Loop không chủ** thì ở Editor tắt domain reload mỗi lần Play xếp thêm một loop.
- **Chạm object Unity thì `finally` và callback guard `if (x != null)`**, không `?.`/`??` (bỏ qua so null của Unity).
  `onComplete` một chủ.
- **`Cancel` chạy đồng bộ phần còn lại**, `finally` của chủ có thể đã `Dispose` — vì vậy nơi khác chỉ `?.Cancel()`. Tái
  nhập chặn bằng `if (isOpen) return;`.
- **Gộp hai tween vào một slot chỉ khi cùng thuộc tính** — khác thuộc tính thì dừng là cắt ngang chuyển động đang hiện.
- **Pool:** hệ hạt Stop Action `None`, không `Destroy(go, t)`; kéo dài trình diễn thì soát mọi đồng hồ ẩn của prefab.
- **Vật thật nhường chỗ cho bản sao lúc diễn thì ẩn hình, không tắt GameObject:** vòng dọn của chủ hay duyệt theo
  `activeSelf`, vật tắt rơi khỏi đường về pool. Bản sao không nằm trong danh sách của chủ thì `finally` của lượt diễn tự trả.

## Trình diễn

- **Mốc "đã thấy":** trượt từ A tới B cần A người chơi đã thấy (`currAnimatedScore`); view tắt, dựng lại, nhiều instance
  thì A trong view sai. Component thụ động trượt khi host gọi `PlayAsync(ct)`.
- **Giá trị nghỉ đọc ở `Awake`.**
- **Chỗ nghỉ lần đầu:** bố cục tính được thì đặt cùng frame tạo vật; chờ frame chỉ khi đọc từ thứ engine dựng sau (layout
  UI, bounds), và ẩn vật tới lúc đặt xong. Đích là ô developer đặt.
- **Một nhịp nhiều lớp:** đỉnh trùng nhau dù chỉnh số nào; căn bằng delay rời thì lệch ở lần chỉnh sau. Cú bùng (hạt, loé)
  bắn lúc nhịp **bắt đầu**: chờ tới đỉnh mới bắn, mắt đọc thành trễ. Nhịp nằm trong anim của hoạ sĩ (Spine, Animator) thì
  hiệu ứng neo vào **event đặt trong anim**; chặng code chạy song song với anim lấy thời lượng từ chính dữ liệu anim. Nhịp
  do code dựng thì mốc lấy từ hàm định nghĩa pha — chỉnh núm thì tiếng dời theo.
- **Kết cục:** rời màn giữa lúc diễn không mất kết quả.
- **Phản hồi kéo dài:** nhạc suốt phiên phát từ thứ sống suốt phiên, không bật lại ở mỗi màn.
- **Chỉ dẫn** (tay chỉ, viền sáng) tắt ngay frame người chơi tác động, và bám vật người chơi sắp tác động: vật bắt đầu di chuyển kéo chỉ dẫn theo, chỉ dẫn còn
  lại đọc thành trễ.
- **Dạy một thao tác có kết quả ngẫu nhiên trên trạng thái đã thiết kế: dựng trạng thái "trước" từ trạng thái đích, thao
  tác đưa về đúng đích.** Chạy ngẫu nhiên thật thì ý đồ của người thiết kế mất ngay lượt dạy. Trạng thái "trước" là **cái
  thao tác giải**, không ngẫu nhiên thuần (có thể ra trạng thái vẫn ổn, cú dạy trông như không làm gì), và tất định theo nội
  dung. Dựng sau mọi thứ đọc bản thiết kế lúc vào, trước lần vẽ đầu, khoá input tới khi thao tác xong; đường không chạy được
  chỉ dẫn thì trả về đích ngay, không diễn.
- **Service overlay giữ cơ chế, nội dung là canvas của consumer.**
- **Điều phối viên:** giai đoạn cố định trong code, ưu tiên chỉ xếp trong giai đoạn. Luồng idempotent: đọc save, làm phần
  còn nợ, trả tài nguyên trong `finally`, `try/catch` từng luồng. Tín hiệu vào/ra màn qua **một** bridge. Vòng điều phối là
  class thuần. Cơ chế: `docs/Runtime/LiveOpsHomeFlow/LiveOpsHomeFlow.md`.
- **Nhiều module cùng mất gì đó ở một sự kiện:** bắt đầu, huỷ chờ, ghi mất mát có một chủ gọi; cờ chờ trong save từng
  module, gỡ cờ **trước** khi kiểm còn gì để mất; luật vòng đời cờ ở lớp cha, không `virtual`.

## Vật thể

- **Vật cứng** chỉ nghiêng theo hướng bay (2.5D: ≤ ±12°).
- **Sprite có cạnh dày (2.5D): tách gốc và thân, bóng là con của gốc** — gốc giữ vị trí và scale chủ đặt, hiệu ứng ghi lên
  thân; reset thân là về nghỉ.
- **Lực của cú đáp:** "đập vào" là tốc độ lớn nhất lúc chạm rồi vật nhận đáp lại; "đặt xuống" là chậm dần tới đích.
- **Sprite giữ tỉ lệ hoạ sĩ vẽ:** cỡ và vị trí đo pixel trên ảnh đích, quy về phần đặc của sprite.
- **Vật bay giữa hai chủ:** một node mang toàn bộ cỡ nhìn thấy, chủ mới chia lại cỡ ở một chỗ lúc đáp; rời cha giữ cỡ thế giới, mỗi frame một hàm đường cong đặt cỡ. Điểm cất cánh chụp lúc nguồn còn
  đứng chỗ người chơi thấy, trước khi chủ nguồn bắt đầu rời đi (anim đóng thu nhỏ, trượt ra).
- **Đích đọc lại mỗi frame:** chủ mới có thể đang trượt, lún, căn lại; chốt thì vật đáp chỗ cũ rồi giật. Giá là một lần
  đọc vị trí mỗi frame — rẻ hơn mọi cờ chọn vật nào được bám. Đích là chỗ **chưa có** (ô sắp mở, hàng đang giãn) thì hỏi
  chính hàm bố cục hàng dùng để xếp, với số phần tử sau thay đổi.
- **Cú thu thập:** đích cỡ theo lòng vật chứa, nhỏ hơn đỉnh chuyến bay, cỡ về 0 tốc độ lúc chạm.
- **Vật vào vật chứa:** vật chứa tách mép trước khỏi thân; vật vào bay trên cả vật chứa, lúc đáp mới xuống giữa lòng và mép
  rồi chìm dưới mép. Nắp và mép nằm ngoài vùng vật chiếm lúc đáp — đo bằng số trên prefab trước khi giao chơi thử. Nhiều
  vật liên tiếp qua cùng một cửa thì cửa mở một lần cho cả loạt. Hết việc thì vật chứa rút lui tại chỗ, không bay ngược về
  nguồn tranh chú ý với thứ vừa nhận.
- **Nhóm cùng chịu tác động:** cùng báo trước, rồi lệch nhau theo vị trí để đọc được nguồn lực — lệch ở thời điểm, biên độ,
  số vòng, không thành một tấm cứng; demo cho đi cùng lúc thì theo demo.
- **Bản sao UI bay tới vật world:** cỡ quy từ bề ngang đích trên màn hình, tới nơi nhường vật thật cùng frame. Lớp vẽ chọn
  trong cây canvas sẵn có trước khi mở canvas mới. Đổi cỡ bằng khung, không scale, khi có con phải giữ cỡ riêng (đuôi hạt).

## Renderer và chữ

> **Nền tảng** — autosize TextMeshPro chỉ co khi tràn **chiều cao**; từ rộng hơn bề ngang bị ngắt giữa từ. "Tối đa n dòng"
> = khung cao n dòng + test quét dữ liệu thật (`ForceMeshUpdate(true)` rồi `textInfo`). Autosize UniText co theo **cả hai**
> chiều; đo chữ ngay sau khi đặt bằng `MeasureText` truyền `maxWidth` và `maxHeight` của khung — thiếu `maxWidth` là đo
> không wrap, `GetRangeBounds` ngoài vòng render chưa có vị trí dòng. Lớp vẽ UniText xếp theo thứ tự style: Styles của
> chính chữ → từng preset theo danh sách → preset global; style riêng nằm **dưới** mọi lớp của look — lớp phải đè lên look
> (dải sáng, phủ màu) đặt trong preset xếp sau, lớp phải nằm sau look (Glow quầng sáng) đặt trong style riêng: Glow trong
> preset xếp sau phủ trắng cả mặt chữ.

> **Nền tảng** — `SpriteMask` không bật Custom Range ghi stencil cho **mọi** tầng dưới nó — giới hạn dải về đúng tầng nó
> phục vụ. `SpriteRenderer` Sliced, Tiled vẽ theo `size`; `sprite.bounds` vẫn là cỡ gốc của ảnh — đo phần nhìn thấy theo
> `drawMode`, đổi art sang Sliced là đổi số đo. Hai renderer cùng layer, cùng order trong một `SortingGroup` thì thứ tự giữa
> hai object không cố định, đẩy z về phía camera cũng không quyết được: lớp phải đè renderer của **object kề bên** cần order
> riêng, giãn bậc của lớp dưới. Hai canvas Overlay cùng sorting order cũng vậy: thứ phải đè lấy order cao hơn hẳn, và order
> suy từ một mốc (+1, +bậc) phải còn dưới lớp kế trên. Lỗi lớp vẽ chỉ hiện ở một trong n instance dựng giống nhau là dấu
> hiệu hoà thứ tự. `overrideSorting`, `sortingOrder` đặt lên canvas lồng đang tắt không ăn: bật rồi mới đặt, trả rồi mới
> tắt.
