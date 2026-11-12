---
name: editor-first
description: Dùng khi quyết thứ gì làm ở Editor thay vì code, khi viết tool Editor IMGUI, hoặc khi tool đọc–ghi dữ liệu của người dùng (import/export).
---

# editor-first — Editor lo cấu hình và kết nối, tool, import/export

Skill nền dưới `Skills/horcrux_skill.md` (NT, ba tầng, ký hiệu định nghĩa ở đó).

## Editor-first

Luật và câu phân xử ở NT4. Trước khi viết code cho một việc, hỏi: *developer làm việc này một lần trong Editor được không?*
Được thì đó là bước setup, không phải code. **Dấu hiệu code đang làm việc của Editor:** `GetComponent`/`Find`/
`AddComponent`/`Resources.Load` lấy thứ đã có trên prefab · hằng số tinh chỉnh hardcode · dựng hierarchy bằng code · API
`Bind…`/`Attach…` nhận reference · đọc ngầm giá trị prefab ở `Start` thay vì phơi ô.

**Kéo thả vào `[SerializeField]` là DI** — composition root là chính cái asset. **API nhận reference là code nối** —
`Bind(Transform)` chỉ khi Inspector **thật sự không kéo được**, và phải kiểm: cùng scene thì kéo được; khác scene hoặc
prefab dựng lúc chạy thì không. Giá của kéo thả: wiring là GUID trong file serialize, không grep được — lý do **không rải**
nó, không phải lý do bỏ.

> **Nền tảng** — dữ liệu serialize sửa không cần compile, ai trong team cũng chỉnh được, thiếu thì lộ ô trống thay vì nổ
> giữa gameplay, giá trị thật đọc được bằng mắt trên đối tượng.

**Sổ tay** — reference kéo vào ô · component add sẵn trên prefab · số tinh chỉnh phơi Inspector · preset là
`ScriptableObject` · wire sẵn trong prefab rồi `Instantiate` · `RectTransform` của chính object cũng là một ô thay cho cast.

**Thuộc tính code ghi lúc chạy không còn là ô cấu hình trên asset** — sửa ở đó không tác dụng mà không gì báo. Núm chỉnh
nằm trên component code; asset chỉ giữ phần code không chạm (màu khi code ghi độ đậm), tooltip nói rõ phần nào.

**Bắt buộc nối bằng code thì bên đời ngắn tự trình diện với bên đời dài:** đăng ký và huỷ là **một cặp trong một file**,
xong **trước tín hiệu cần tới nó** — tín hiệu phát ngay sau khi bật cây thì `Awake` đã chạy còn `Start` chưa, nên cặp là
`Awake`/`OnDestroy`; view tự ẩn bằng tắt chính nó thì cặp bật/tắt gỡ nó khỏi chủ ngay lần ẩn đầu.

> **Nền tảng** — lúc thoát app, `OnApplicationQuit` của mọi object chạy trước `OnDestroy`: service tự quản đời (singleton
> bên thứ ba) có thể đã trả `null` khi vế huỷ chạy. Vế huỷ của object sống suốt phiên kiểm `null` trước khi gỡ.

**Điều kiện dựng hệ ghi vào tài liệu, không sinh code canh.** Tạo asset, dựng GameObject, gán reference, đặt layer, thêm
scene vào build là mục **"Trước khi chạy"** của tài liệu module (`docs` › `.md`); thiếu thì xử lý theo bảng guard
(`code-design` › Bất biến). Bước setup tay là cách developer hiểu hệ — người tự kéo asset giữ được trong đầu chỗ các mảnh
nối. Hệ không gánh lỗi quên setup; đổi lại hệ **nợ** mục "Trước khi chạy" đủ dựng lại từ 0.

**Agent làm thao tác Editor qua công cụ (MCP, eval) khi được giao** thì kết quả là asset đã lưu (prefab, scene,
`ScriptableObject`) developer mở ra thấy, không phải code chạy lúc boot làm thay. Lệnh lưu kéo theo mọi asset đang bẩn
trong Editor: soát `git status` ngay sau, file ngoài phạm vi thì báo developer. *Đã sai một lần:* lưu scene làm asset cấu
hình rơi dữ liệu của field đã xoá khỏi code. Lưu prefab bằng `SaveAsPrefabAsset` ghi lại cả field chưa serialize và override
thừa trên prefab lồng nhau: gỡ một component thì sửa YAML từ bản đã commit (dòng `- component:` và khối `--- !u!114 &id`),
diff chỉ còn đúng khối đó.

## Import/export — dữ liệu đi qua tool

File tool đọc–ghi là **của người dùng và hệ khác**. **Không sửa giá trị import** — ngoài tầm hợp lệ thì từ chối cả file hoặc
bỏ entry kèm cảnh báo; clamp là xuất bản lệch không ai biết · **bỏ entry hỏng, không bịa entry mới** · **chỉ sở hữu phần
mình hiểu** — export clone dữ liệu gốc rồi ghi đè đúng khoá tool sở hữu, khoá lạ đi qua nguyên vẹn · **dữ liệu gốc đi theo
đối tượng**, không tra lại theo toạ độ hay vị trí mảng · **Import và Export sống cạnh nhau** trong một file, nghiệm thu bằng
round-trip.

**Gói asset từ ngoài chỉ nhập phần thứ cần dùng kéo theo.** Gói của hoạ sĩ, của store hay đóng kèm runtime, plugin và mẫu
mà dự án đã có bản khác; nhập cả gói là đè bản đang chạy bằng bản lệch, không lỗi nào báo. Đọc phụ thuộc từ prefab, scene
đích trước, so phần còn lại với file dự án đang có, rồi mới tick. Tool: `docs/Editor/UnityPackageAudit.md`.

## Editor tool

Tiêu chí: **một tool là một đơn vị gói kín** · **thứ dùng chung sống một chỗ** (`Editor/Common/` của Horcrux) · **thứ không
đổi giữa các lần vẽ phải có sẵn**.

> **Nền tảng** — IMGUI: một tương tác gây nhiều lần `OnGUI` cho **cùng một state**. `GUIStyle`, `GUIContent` có icon chỉ tồn
> tại sau khi `GUI.skin`, `EditorGUIUtility` sẵn sàng, không khởi tạo ở static initializer. Khi dự án dùng Odin với
> `EnableUIToolkitSupport`: `PropertyDrawer` có `CreatePropertyGUI` vẽ bằng UI Toolkit đè toạ độ IMGUI, lệch đúng chiều cao
> `IMGUIContainer` phía trên (khối Init của InitArgs) — tắt cờ trong `GeneralDrawerConfig`.

> **Nền tảng** — tool đọc dữ liệu qua đối tượng runtime nhận phụ thuộc bằng `Init` của InitArgs: ngoài Play chưa ai tiêm,
> getter ném `NullReferenceException`. Tool đọc **trong Play qua chính đối tượng đó** — thấy đúng thứ runtime thấy — không
> dựng bản thứ hai của getter để chạy ngoài Play; việc chỉ được làm khi không Play (xoá save) là nút riêng, nguồn lấy từ ô asset.

**Sổ tay** — cache IMGUI: `static readonly` cho bất biến (`Color`, `GUILayoutOption[]`, `GUIContent` chỉ text) · static
lazy trong `EnsureStyles()` cho thứ cần `GUI.skin` · dirty flag cho layout khi resize · tính nặng (filter, sort, format) ở
`EventType.Layout`, `Repaint` dùng lại.

**Tool có người dùng thì UX là thiết kế:** thứ khác vai trò (ghi dữ liệu / đổi cách xem / dùng mọi lúc) không chung một vùng
· điều kiện vẽ và điều kiện bấm suy từ một nguồn · **không giấu thứ có thật** — điều kiện vẽ là *"có dữ liệu"*, tra thiếu
tài nguyên thì vẽ dạng lỗi kèm id; field chỉ-đọc vẫn hiện, khác kiểu field sửa được · kiểm hợp lệ chạy **khi được hỏi**,
không chạy nền mỗi thay đổi.
