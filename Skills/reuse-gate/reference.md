# reuse-gate — tra cứu

Nền tảng, Sổ tay và bẫy của `reuse-gate`. Đọc mục được bảng Tra cứu trong `SKILL.md` gọi.

## Khảo sát

- **Đường di sản song song** (hệ cũ còn sống cạnh hệ mới): đếm call site đường cũ trước khi thêm vào đường mới — nhánh sót
  lại vẫn chạy, chỉ lệch cấu hình. *Bẫy:* một nửa số call site đã qua hệ mới, nửa kia còn đi hệ cũ nên không chỉnh được ở
  một chỗ, hai nửa lệch nhau.
- **Hệ port từ dự án khác** thì diff với **bản nguồn hiện tại** (kể cả giá trị mặc định trong scene), hỏi nhánh nào là lõi
  trước khi tối giản. Thứ developer nhớ là của dự án nguồn có thể nằm trong package nó cài: tìm cả package, rồi soát dự án
  đích đã cài sẵn chưa.
- *Bẫy:* bê khuôn serialize giá trị runtime vào asset sang bài toán lưu state người chơi — state vào version control.
- *Bẫy:* viết vòng tween và công thức riêng trong khi Horcrux đã có runner và hàm đúng hình dạng đó.
- *Bẫy:* đã đọc skill nêu sẵn hàm dump và hàm đo, vẫn viết khung dump trong eval và script đo riêng, task sau lại viết lần
  nữa.

## Dữ liệu có kiểu

Nhận chuỗi rồi tự parse là parse hai nơi: che lỗi khỏi log hệ nguồn, mất kiểm tra biên dịch tên field.

> **Nền tảng** — remote config và persistence của Horcrux ép kiểu qua cùng serializer: giá trị có cấu trúc khai generic với
> class dữ liệu thuần, không `string`; JSON hỏng thì hệ tự `LogError` và giữ cache, consumer không `try/catch`. Khớp
> property không phân biệt hoa thường nhưng không đổi tên: đổi tên field thì key cũ rơi thành `null` im lặng, DTO đòi đủ
> khoá thì cả giá trị bị từ chối và máy chạy cache; key thừa luôn bị bỏ qua. Lệch khoá giữa build và console soát bằng tool
> `RemoteConfigDiff`. Chi tiết: `docs/Runtime/RemoteConfigSystem/RemoteConfigSystem.md`.

## Chuyển bản tạm

Bản tạm đã gắn trong asset thì `.meta` bản Horcrux nhận lại GUID của bản tạm, để reference trong prefab, scene không gãy.
