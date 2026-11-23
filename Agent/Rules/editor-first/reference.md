# editor-first — tra cứu

Nền tảng và Sổ tay của `editor-first`. Đọc mục được bảng Tra cứu trong `SKILL.md` gọi.

## Wiring

> **Nền tảng** — dữ liệu serialize sửa không cần compile, ai trong team cũng chỉnh được, thiếu thì lộ ô trống thay vì nổ
> giữa gameplay, giá trị thật đọc được bằng mắt trên đối tượng.

**Sổ tay** — reference kéo vào ô · component add sẵn trên prefab · số tinh chỉnh phơi Inspector · preset là
`ScriptableObject` · wire sẵn trong prefab rồi `Instantiate` · `RectTransform` của chính object cũng là một ô thay cho cast.

- **Cặp đăng ký–huỷ:** tín hiệu phát ngay sau khi bật cây thì `Awake` đã chạy còn `Start` chưa, nên cặp là
  `Awake`/`OnDestroy`; view tự ẩn bằng tắt chính nó thì cặp bật/tắt gỡ nó khỏi chủ ngay lần ẩn đầu.
- Asset giữ phần code không chạm — ví dụ màu, khi code ghi độ đậm.

> **Nền tảng** — lúc thoát app, `OnApplicationQuit` của mọi object chạy trước `OnDestroy`: service tự quản đời (singleton
> bên thứ ba) có thể đã trả `null` khi vế huỷ chạy. Vế huỷ của object sống suốt phiên kiểm `null` trước khi gỡ. Cờ "đang
> thoát" có sẵn: `Application.exitCancellationToken` huỷ trước cả `Application.quitting`, lúc mọi object còn sống (đo khi dừng Play trong Editor) — không
> cần tự bật cờ ở `OnApplicationQuit`.

## Sửa asset qua công cụ

- **Lệnh lưu kéo theo mọi asset đang bẩn** trong Editor.
- **Sửa asset bằng text (shader, YAML) thì import lại file đó trước khi đọc qua API:** Editor chưa thấy file đổi thì trả bản
  cũ, không báo gì.
- **Diff scene, prefab đọc bằng `--diff-algorithm=histogram`:** diff mặc định ghép nhầm các khối YAML na ná nhau, báo xoá
  cả dòng không đổi.
- **File mới do package tự sinh khi Editor chạm** (setting trong `Resources`) là cấu hình của build, không phải rác: thiếu
  nó build chạy mặc định trong code, khác Editor — so hai giá trị rồi hỏi commit.
- **`SaveAsPrefabAsset` ghi lại cả field chưa serialize và override thừa** trên prefab lồng nhau: gỡ một component thì sửa
  YAML từ bản đã commit (dòng `- component:` và khối `--- !u!114 &id`), diff chỉ còn đúng khối đó.
- **Lệnh dựng lỗi giữa chừng mà asset chưa lưu** thì mở lại để bỏ phần dở rồi dựng từ đầu, không vá tiếp trên trạng thái
  không ai biết.
- **Sửa text YAML khi developer cũng đang làm:** kiểm asset có đang mở, đang bẩn trong Editor (Editor lưu sau sẽ đè); neo
  theo nội dung, không theo số dòng.

> **Nền tảng** — hai asset trùng GUID (thư mục chép kèm `.meta` ngoài Editor) thì Unity giữ GUID cho bản nhập trước, cấp
> GUID mới cho bản sau và ghi lại `.meta` của nó. `.meta` đổi trong `git status` là Unity tự vá; khôi phục là tạo trùng lần
> nữa. Reference cũ vẫn trỏ bản gốc, nên thứ chép theo (atlas, ô trong asset cấu hình, entry Addressables) vẫn trỏ thư mục
> gốc: trỏ lại chúng vào bản chép là việc phải làm, rồi soát `AssetReference` gãy
> (`docs/Editor/AddressableReferenceAudit.md`).

## Gói asset từ ngoài

Nhập cả gói là đè bản đang chạy bằng bản lệch. Gói của hoạ sĩ, của store hay đóng kèm runtime, plugin và mẫu mà dự án đã có bản khác; nhập đè không lỗi nào báo. Đọc phụ
thuộc từ prefab, scene đích trước, so phần còn lại với file dự án đang có, rồi mới tick. Tool:
`docs/Editor/UnityPackageAudit.md`.

## Import/export và UX của tool

- **Không sửa giá trị import:** ngoài tầm hợp lệ thì từ chối cả file hoặc bỏ entry kèm cảnh báo; clamp là xuất bản lệch
  không ai biết. Bỏ entry hỏng, không bịa entry mới.
- **Chỉ sở hữu phần mình hiểu:** export clone dữ liệu gốc rồi ghi đè đúng khoá tool sở hữu. **Dữ liệu gốc đi theo đối
  tượng**, không tra lại theo toạ độ hay vị trí mảng. Import và Export nằm trong một file.
- **Vai trò của vùng:** ghi dữ liệu / đổi cách xem / dùng mọi lúc.
- **Không giấu thứ có thật:** điều kiện vẽ là *"có dữ liệu"*, tra thiếu tài nguyên thì vẽ dạng lỗi kèm id; field chỉ-đọc vẫn
  hiện, khác kiểu field sửa được. Kiểm hợp lệ không chạy nền mỗi thay đổi.

## IMGUI

> **Nền tảng** — IMGUI: một tương tác gây nhiều lần `OnGUI` cho **cùng một state**. `GUIStyle`, `GUIContent` có icon chỉ tồn
> tại sau khi `GUI.skin`, `EditorGUIUtility` sẵn sàng, không khởi tạo ở static initializer. Khi dự án dùng Odin với
> `EnableUIToolkitSupport`: `PropertyDrawer` có `CreatePropertyGUI` vẽ bằng UI Toolkit đè toạ độ IMGUI, lệch đúng chiều cao
> `IMGUIContainer` phía trên (khối Init của InitArgs) — tắt cờ trong `GeneralDrawerConfig`.

> **Nền tảng** — tool đọc dữ liệu qua đối tượng runtime nhận phụ thuộc bằng `Init` của InitArgs: ngoài Play chưa ai tiêm,
> getter ném `NullReferenceException`. Việc chỉ được làm khi không Play (xoá save) là nút riêng, nguồn lấy từ ô asset.

**Sổ tay** — cache IMGUI: `static readonly` cho bất biến (`Color`, `GUILayoutOption[]`, `GUIContent` chỉ text) · static
lazy trong `EnsureStyles()` cho thứ cần `GUI.skin` · dirty flag cho layout khi resize · tính nặng (filter, sort, format) ở
`EventType.Layout`, `Repaint` dùng lại.
