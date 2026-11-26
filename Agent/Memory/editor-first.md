# editor-first — kinh nghiệm

Bài học đã trả giá khi làm việc với `editor-first`. Đọc trọn cùng lúc với rule hoặc skill đó. Hình dạng và vòng đời ở `horcrux.md` › Memory.

### Tham chiếu serialize nạp ra null sau khi đổi kiểu field
`2026-11` · vấp 1 · đã đo · đúng với prefab đang mở trong Prefab Mode
**Thấy:** đổi kiểu một field `[SerializeField]` tham chiếu (vd. `Transform` → `SpriteRenderer`) lúc prefab dùng nó đang mở; ref cũ nạp ra null, lần lưu kế ghi null xuống đĩa. **Vì:** recompile nạp lại bản trong bộ nhớ theo kiểu mới, object cũ không khớp; Auto Save lưu đúng bản đã mất ref.
**Làm:** không đổi kiểu field tham chiếu khi prefab dùng nó đang mở; đổi xong kiểm ref trên đĩa và bằng eval trên object đã nạp.

### `m_LocalPosition` trong YAML đổi mà không ai chỉnh vị trí
`2026-10` · vấp 1 · đã đo
**Thấy:** so YAML prefab giữa hai mốc thấy `m_LocalPosition` lệch dù không ai chỉnh vị trí. **Vì:** object có `PositionConstraint` bật thì `m_LocalPosition` serialize giá trị constraint đánh giá lúc lưu, không phải giá trị authoring.
**Làm:** khi so prefab vẫn giữ `m_LocalPosition` trong diff; lệch ở object có constraint bật thì đọc là giá trị đánh giá, soát constraint trước khi coi là thay đổi thật.

### Gán sorting layer bằng `intValue` thành 0, không báo
`2026-11` · vấp 1 · đã đo
**Thấy:** gán `uniqueID` sorting layer trong TagManager qua `SerializedProperty.intValue` với số âm, giá trị thành 0 im lặng; so ID với YAML của renderer thì lệch dấu. **Vì:** `uniqueID` là `uint`, còn YAML renderer ghi cùng ID dưới dạng số có dấu.
**Làm:** gán qua `uintValue`; so ID giữa TagManager và renderer thì quy hai phía về một kiểu.

### Tên root prefab dựng bằng script không như đã đặt
`2026-11` · vấp 1 · đã đo
**Thấy:** đặt tên root rồi lưu, root mang tên khác. **Vì:** `PrefabUtility.SaveAsPrefabAsset` đặt tên root theo tên file.
**Làm:** đặt tên ở đường dẫn file; đổi tên root là đổi tên file.

### Dời một nhánh con từ prefab ra scene tưởng phải mổ YAML
`2026-11` · vấp 1 · đã đo
**Thấy:** cần đưa một nhánh con của prefab sang scene, giữ anchor, size và ref nội bộ giữa các object trong nhánh. **Vì:** `Object.Instantiate(prefabAsset.transform.Find(path).gameObject, parent, false)` cho bản sao không link prefab mà ref trong nhánh vẫn trỏ nội bộ.
**Làm:** instantiate như trên, đổi `name` bỏ "(Clone)", đặt sibling index, lưu scene; rồi `LoadPrefabContents` → xoá nhánh cũ → `SaveAsPrefabAsset`. Kiểm `GetObjectOverrides` và `GetAddedGameObjects` của instance prefab trong scene bằng 0, scene không còn fileID cũ.

### Tool Editor vẽ lại hoãn trông như không chạy
`2026-11` · vấp 1 · đã đo · đúng với Editor điều khiển qua MCP lúc không focus
**Thấy:** mở scene bằng script, object do tool Editor vẽ lại vẫn ở trạng thái mặc định của prefab — trông như tool hỏng. **Vì:** `EditorApplication.delayCall` xếp hàng mà không chạy khi Editor không focus, kể cả đã bật auto-tick; developer dùng Editor có focus thì hàng chạy bình thường.
**Làm:** trước khi kết luận, đọc `EditorApplication.delayCall.GetInvocationList()`; muốn kiểm hình vẽ thì gọi thẳng API vẽ của tool, không invoke cả hàng vì trong đó có callback của hệ khác.

### Cần ảnh Inspector mà cửa sổ Unity bị che
`2026-10` · vấp 1 · suy luận · đúng với dự án dùng Odin
**Thấy:** soát lỗi bố cục Inspector trong khi cửa sổ Unity bị che hoặc không focus, chụp màn hình không ra Inspector. **Vì:** chụp màn hình lấy thứ đang hiện trên màn; `GUIView.GrabPixels` (internal, gọi qua reflection) đọc pixel của chính view.
**Làm:** script tạm mở InspectorWindow nổi, mở foldout Odin qua state của property tree, chụp bằng `GrabPixels`; so với component không có khối Init (`editor-first` › tra cứu › IMGUI).

### Tool nhập ghi theo bản xem trước dựng từ lúc nạp file
`2026-11` · vấp 1 · suy luận · đúng với tool xem trước rồi mới ghi
**Thấy:** bản xem trước (id, số lượng, hộp thoại xác nhận) dựng lúc nạp file; cửa sổ để dock, ai đó sửa asset đích hay Ctrl+Z rồi bấm Nhập — tool ghi theo bản cũ, phép kiểm khớp số bị bỏ qua. **Vì:** asset đích đổi được trong lúc cửa sổ mở; bản xem trước chỉ đúng ở lúc dựng.
**Làm:** dựng lại và kiểm lại ngay trước khi ghi, lỗi thì dừng; hộp thoại lấy số từ bản vừa dựng (`editor-first` › Import/export).

### Dòng dữ liệu đầu file biến mất không báo
`2026-11` · vấp 1 · suy luận
**Thấy:** parser coi dòng đầu "không đọc được số" là tiêu đề; dòng dữ liệu đầu gõ sai (`5a`, ô trống) bị bỏ im lặng, tổng ít đi một. **Vì:** đoán tiêu đề theo "không hợp lệ" biến lỗi dữ liệu thành tiêu đề.
**Làm:** nhận tiêu đề chỉ khi ô đầu khớp đúng nhãn (bỏ hoa thường, khoảng trắng ngoài); còn lại là dữ liệu, sai thì báo lỗi kèm số dòng (`editor-first` › Import/export).

### Tool kiểm dữ liệu báo nhẹ hơn hậu quả thật trong game
`2026-11` · vấp 1 · suy luận
**Thấy:** tool đếm ảnh theo một field, cảnh báo "game lặp ảnh"; game quyết bằng hàm khác (lấy max hai field) và từ chối hẳn khi 0. **Vì:** tool tự viết lại luật của game: hai nguồn cho một luật, lệch không gì báo.
**Làm:** tool gọi đúng hàm game dùng để quyết, tách thành hàm thuần dùng chung nếu game giữ riêng; mức báo (lỗi, cảnh báo) theo hậu quả trong game (`horcrux.md` NT6; `code-design` › Bất biến).

### Đổi hoa thường tên thư mục nguồn: git không thấy, tool sinh asset xoá nhầm
`2026-11` · vấp 1 · đã đo · đúng với Windows, macOS (ổ không phân biệt hoa thường)
**Thấy:** đổi `yellow things` → `Yellow Things` thì `git status` sạch; chạy tool sinh asset theo tên thư mục thì asset sinh ra (atlas) bị xoá như mồ côi, entry vẫn trỏ guid đã mất. **Vì:** `core.ignorecase=true` nên git bỏ qua đổi hoa thường; tool thấy `File.Exists` đúng nên giữ file cũ, rồi so tập đường dẫn ordinal nên coi file đó không thuộc tập giữ.
**Làm:** git: `git rm -r --cached <tên cũ>` rồi `git add <tên mới>`; commit có pathspec đọc lại đĩa nên thêm lại tên cũ (hai bản trùng guid) hoặc báo "file alias" — dựng commit bằng `read-tree HEAD` trên index tạm, `-c core.ignorecase=false rm --cached <tên cũ>`, `write-tree`, `commit-tree`, rồi `ls-tree` soát không còn tên cũ. Tool so đường dẫn hay tên asset với tập giữ thì dùng `StringComparer.OrdinalIgnoreCase`, cùng cách ổ đĩa so (`editor-first` › Import/export).
