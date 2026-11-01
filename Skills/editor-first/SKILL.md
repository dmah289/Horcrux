---
name: editor-first
description: Dùng khi quyết thứ gì làm ở Editor thay vì code, khi viết tool Editor IMGUI, hoặc khi tool đọc–ghi dữ liệu của người dùng (import/export).
---

# editor-first — Editor lo cấu hình và kết nối, tool, import/export

Skill nền của Horcrux, đứng dưới `Skills/horcrux_skill.md`: `NT<n>`, ba tầng Luật · Nền tảng · Sổ tay và bảng
chỉ dẫn định nghĩa ở đó. Con trỏ `` `skill` › Mục `` trỏ sang skill khác; `mục X` trỏ trong file này.

## Editor-first

Luật ở NT4; đây là nơi duy nhất dẫn giải. **Dấu hiệu code đang làm việc của Editor:** `GetComponent` /
`Find` / `AddComponent` / `Resources.Load` để lấy thứ đã có trên prefab · hằng số tinh chỉnh cảm giác
hardcode · dựng hierarchy bằng code · API `Bind…` / `Attach…` nhận reference từ ngoài · đọc ngầm một giá
trị của prefab ở `Start` thay vì phơi ô.

**Kéo thả vào `[SerializeField]` là DI** (`D`, `code-design` › Hình dạng): consumer vẫn nhận vào, composition root là **chính
cái asset**. **API nhận reference là code nối, dù mang hình dạng DI** — `Bind(Transform)` chỉ chính đáng
khi Inspector **thật sự không kéo được**, và câu đó phải **kiểm**: cùng scene thì kéo được; khác scene hoặc
prefab dựng lúc chạy thì không.

> **Nền tảng** — lý do gốc: dữ liệu serialize sửa được **không cần compile**, ai trong team cũng chỉnh được,
> thiếu thì lộ ô trống trong Inspector chứ không nổ giữa gameplay, và giá trị thật **đọc được bằng mắt
> ngay trên đối tượng**.

**Sổ tay** — reference kéo thả vào `[SerializeField]` · component add sẵn trên prefab · số tinh chỉnh phơi
ra Inspector · preset thành ScriptableObject · wire sẵn trong prefab rồi `Instantiate` · `RectTransform`
của chính object cũng là một ô `[SerializeField]` thay cho cast — mọi thứ class chạm tới đều hiện trên
Inspector, không có tham chiếu ngầm phải đọc code mới biết.

**Giá của kéo thả: wiring trong asset không grep được** — chỉ tồn tại dưới dạng GUID trong file serialize,
nên chi phí đọc của NT1 cộng thêm *"bao nhiêu asset phải mở Editor mới thấy"*. Không phải lý do bỏ kéo thả;
là lý do **không rải** nó — biên là câu phân xử ở NT4: **ô trống chỉ lộ ra khi có người nhìn vào ô đó.**

**Bắt buộc nối bằng code thì bên đời ngắn tự trình diện với bên đời dài.** Bên đời ngắn biết chính xác lúc
nó xuất hiện và biến mất, nên đăng ký và huỷ đăng ký là **một cặp trong một file**, chọn theo đời object và
**xong trước tín hiệu cần tới nó**: tín hiệu phát ngay sau khi bật cây object thì `Awake` cả cây đã chạy
còn `Start` thì chưa (`code-design` › Hình dạng) — cặp đúng là `Awake`/`OnDestroy`; view tự ẩn bằng tắt chính nó thì cặp bật/tắt
gỡ nó khỏi chủ ngay lần ẩn đầu.

**Điều kiện dựng hệ ghi vào tài liệu, không sinh code canh** (NT4). Tạo asset, dựng GameObject, gán
reference, đặt layer, thêm scene vào build — người dựng làm **một lần**, viết thành mục **"Trước khi chạy"**
của tài liệu module (`docs` › `.md`); thiếu thì xử lý theo bảng guard ở `code-design` › Bất biến. **Bước setup bằng tay là cách developer
hiểu hệ**: người tự kéo asset vào ô giữ được trong đầu các mảnh rời và chỗ chúng nối — thứ duy nhất dùng
được khi hệ hỏng lúc 2 giờ sáng. Hệ không gánh lỗi quên setup; đổi lại hệ **nợ** một mục "Trước khi chạy"
đủ để dựng lại từ 0.

## Import/export — dữ liệu đi qua tool

File mà tool đọc–ghi là **của người dùng và của hệ khác**, không phải của tool:

| Luật | Nghĩa là |
|---|---|
| **Không bao giờ sửa giá trị dữ liệu import** | ngoài tầm hợp lệ thì **từ chối cả file**, hoặc **bỏ qua entry đó** kèm cảnh báo. Clamp là xuất ra bản lệch mà không ai biết |
| **Bỏ entry hỏng, không bịa entry mới** | chèn dữ liệu để "sửa giúp" là thêm thứ không có trong file |
| **Chỉ sở hữu phần mình hiểu** | export clone dữ liệu gốc rồi ghi đè đúng khoá tool sở hữu; khoá lạ **đi qua nguyên vẹn** |
| **Dữ liệu gốc đi theo đối tượng** | giữ tham chiếu bản gốc trên chính đối tượng, không tra lại theo toạ độ hay vị trí mảng lúc export |
| **Import và Export sống cạnh nhau** | hai chiều của cùng cụm dữ liệu trong một file; nghiệm thu bằng round-trip (`math-physics` › Đối chiếu công thức) |

## Editor tool

Tiêu chí: **một tool là một đơn vị gói kín** · **thứ dùng chung sống một chỗ** (`code-design` › Module) · **thứ không đổi
giữa các lần vẽ lại phải có sẵn**.

> **Nền tảng** — IMGUI (`OnGUI`, `EditorWindow`, `PropertyDrawer`): một lần tương tác gây nhiều lần gọi
> `OnGUI` cho **cùng một state**. `GUIStyle`, `GUIContent` có icon chỉ tồn tại sau khi `GUI.skin` và
> `EditorGUIUtility` sẵn sàng, không khởi tạo được ở static initializer. Odin bật `EnableUIToolkitSupport`
> thì `PropertyDrawer` có `CreatePropertyGUI` của Unity được vẽ bằng phần tử UI Toolkit đè theo toạ độ IMGUI;
> editor có `IMGUIContainer` chèn phía trên (khối Init của InitArgs) thì phần tử lệch đúng chiều cao khối đó
> — tắt cờ trong `GeneralDrawerConfig` để Odin gọi `OnGUI`.

**Sổ tay** — *áp cho IMGUI*; UI Toolkit có mô hình repaint khác:

| Cấp cache | Kỹ thuật | Khi nào dùng |
|---|---|---|
| Static eager | `static readonly` | giá trị bất biến: `Color`, `GUILayoutOption[]`, `GUIContent` chỉ có text |
| Static lazy + guard | init một lần trong `EnsureStyles()` | thứ cần `GUI.skin` hoặc `EditorGUIUtility` |
| Dirty-flag | chỉ rebuild khi dữ liệu đổi | layout options khi window resize |
| Event-phase | tính nặng chỉ ở `EventType.Layout` | filter, sort, format — `Repaint` dùng lại kết quả |

**Tool có người dùng thì UX là một phần của thiết kế:** vùng UI phải nói lên ranh giới — thứ khác vai trò
(ghi vào dữ liệu / chỉ đổi cách xem / dùng ở mọi lúc) không nằm chung một vùng · điều kiện vẽ và điều kiện
bấm suy từ một nguồn (`code-design` › Bất biến) · **không giấu thứ có thật**: điều kiện vẽ là *"có dữ liệu"*, không phải *"tra
được tài nguyên để vẽ"* — tra thiếu thì vẽ dạng báo lỗi kèm id; field chỉ-đọc vẫn phải hiện, khác kiểu
với field sửa được · phép kiểm hợp lệ chạy **khi được hỏi**, không chạy nền theo mỗi thay đổi: lúc đang
dựng thì dữ liệu **luôn** chưa hợp lệ.
