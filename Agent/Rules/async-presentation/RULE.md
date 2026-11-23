---
name: async-presentation
description: Dùng khi code chạm async, CancellationToken, vòng đời object, pool, tween, Addressables, hoặc trình diễn một thay đổi cho người chơi (mốc đã thấy, điều phối nhiều module, vật thể có chất liệu).
---

# async-presentation — async, vòng đời, trình diễn

Rule dưới `Agent/horcrux.md` (NT, tầng, ký hiệu định nghĩa ở đó).

Tiêu chí: **huỷ được** theo owner · **giải phóng được** (thứ giữ tài nguyên có đường trả) · **cô lập được lỗi** (`try/catch`
quanh từng callback trong vòng dispatch).

## Async và vòng đời

- **Mỗi loop chỉ ra token của nó là đời của ai, dài đúng bằng nhịp.** Token của pha ngắn hơn đời loop thì loop **chết im
  lặng** — huỷ là hợp đồng, không log.
- **Bước sau một lượt chờ thuộc chủ sống qua cả lượt.** Caller tự huỷ khi đóng mà còn chờ bằng token của chính nó thì bước
  sau chết im lặng. Cặp "chờ rồi làm" vào chủ đời dài, caller gọi một hàm.
- **Hàm mở tiến trình sở hữu trạng thái cuối ở mọi đường ra** (xong, huỷ, thời lượng 0) qua **một** `try/finally` bọc toàn
  thân — trạng thái cuối và callback đúng một lần. Caller chỉ `await`, ghép `WhenAll`. Đường dọn từ ngoài về đúng trạng thái cuối của đường
  ra tự nhiên.
- **Điều kiện kiểm trước một lượt chờ là bản chết sau lượt chờ:** chỗ đã nhắm (ô trống, đích, lượt) kiểm lại ngay trước
  bước người chơi thấy; mất rồi thì bỏ bước, không diễn nửa chừng.
- **Trạng thái tạm có một nghĩa vụ trả, nghĩa vụ đi theo bên đang cầm.** Bên dựng trả nó trên mọi đường ra cho tới mốc bàn
  giao; `finally` quyết theo biến cục bộ ghi ngay sau mốc đó. **Trả theo thứ tự ngược lúc dựng.**
- **`CancellationTokenSource` có một chủ dọn: chỗ tạo**; nơi khác chỉ `?.Cancel()`.
- **Hàm mở lượt bằng tài nguyên một lần dùng thì từ chối tái nhập** — chồng lượt làm người chờ source cũ treo vĩnh viễn.
- **Huỷ giữa chừng phân loại theo thứ mất đi:** dữ liệu (tiến độ, grant, save) nhất quán trên mọi đường — ghi trước anim;
  trình diễn chỉ cần không để rác — snap đích, tắt.
- **Về pool cũng là một đường ra:** bỏ liên kết **trước** khi dừng tween, sau `await` kiểm liên kết còn là của mình mới
  ghi. Object về pool không chứa con tự huỷ theo đồng hồ riêng.
- **Mobile không có mốc rời phiên tin được:** việc dở dang lúc app chết phải để lại trạng thái vô hại, không chốt ở pause.

## Trình diễn một thay đổi cho người chơi

Dữ liệu đổi ở một nơi, người chơi thấy ở nơi khác, có khi sau nhiều lần đổi.

- **Kết cục có hai mốc: sự thật ghi ngay, trình diễn neo vào thứ người chơi thấy.** Ghi save và báo hệ khác ở mốc xảy ra
  rồi mới diễn; bước kế bắt đầu theo sự kiện nhìn thấy, không theo delay cố định.
- **Mốc "đã thấy" nằm trong save, cạnh sự thật; ghi khi diễn xong.** Diễn từ mốc tới sự thật **một** lần, ghi mốc ở một chỗ
  cho cả snap lẫn trượt; huỷ giữa chừng thì lần sau diễn lại; reset chu kỳ reset cả mốc. Mốc trong instance view là sai.
- **Component dùng chung mặc định thụ động:** bật lên đứng ở mốc, diễn khi host gọi; không cờ chế độ.
- **Trạng thái nghỉ dựng trong prefab, mỗi lần hiện đặt lại về nghỉ.** Tween lên thuộc tính đã author **nhân hoặc cộng trên
  giá trị nghỉ**, reset về nghỉ, không về `one`/`zero`. Vật hiện ra lần đầu đã đứng ở chỗ nghỉ.
- **Nhiều lớp chung một nhịp ghép một Sequence, chung thời lượng và ease.** Phản hồi (tiếng, rung, hạt) neo vào **mốc bên
  trong** chuyển động — callback trong Sequence, hoặc event đặt trong anim của hoạ sĩ — không bằng delay đo tay.
- **Phản hồi kéo dài (nhạc, tiếng loop) thuộc chủ có cùng đời;** bắt đầu, dừng lúc nào là một câu của kịch bản — hỏi (NT1).
  **Chỉ dẫn nhường ngay cho hành động của người chơi**, không fade.
- **Hiệu ứng "báo có thay đổi" ở đúng một điểm**, chỗ khác vẽ tĩnh · **giá trị đổi lúc tới nơi**, không lúc bắt đầu ·
  **lớp trên đè lớp dưới**, không bật/tắt thứ bị che hết · **bước thiếu dữ liệu thì bỏ bước**, không rẽ nhánh cả luồng.
- **Nhiều module cùng trình diễn khi vào một màn → một điều phối viên theo giai đoạn cố định.** Giai đoạn chia theo **loại**
  trình diễn; luồng dùng tài nguyên độc quyền chạy lần lượt, luồng chỉ chạm view mình chạy đồng thời; mọi luồng
  **idempotent**. Lease, hàng đợi, chờ frame chỉ vá triệu chứng.
- **Nhiều module cùng chịu một sự kiện → định nghĩa sự kiện ở một chỗ**, bắt đầu và huỷ có một chủ gọi.

## Trình diễn vật thể có chất liệu

Chất liệu quyết *cái gì được biến dạng*; phần vui đến từ nhịp và phản ứng của môi trường, không từ bóp méo vật.

- **Vật cứng không biến dạng khi bay;** cú "nặng" đi vào vật nhận (lún, nảy, loé, hạt) và bóng tách khỏi vật để đọc độ cao.
- **Một cử chỉ một hình dạng ở mọi nơi** — cùng một hàm đường cong dù diễn trong world hay trên UI; mỗi bên chỉ quy đổi hệ
  toạ độ.
- **Lực của cú đáp đọc từ gia tốc cuối chuyến;** vật chuyển giữa hai chủ thì **đích đọc lại mỗi frame**, không chốt lúc
  cất cánh.
- **Sprite giữ tỉ lệ hoạ sĩ vẽ.**

## Tra cứu — `async-presentation/reference.md`

| Chạm | Mục |
|---|---|
| UniTask, token, `await`, Addressables, PrimeTween, pool, pause/quit | Stack async |
| viết luồng trình diễn, mốc đã thấy, Sequence, phản hồi, chỉ dẫn, dạy thao tác, điều phối màn | Trình diễn |
| vật bay, đáp, vào vật chứa, bản sao UI ↔ world, sprite có cạnh dày | Vật thể |
| chữ autosize, lớp vẽ, sorting, mask, `SpriteRenderer` | Renderer và chữ |
