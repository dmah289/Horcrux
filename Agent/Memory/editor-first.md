# editor-first — kinh nghiệm

Bài học đã trả giá khi làm việc với `editor-first`. Đọc trọn cùng lúc với rule hoặc skill đó. Hình dạng và vòng đời ở `horcrux.md` › Memory.

### Asset cấu hình mất dữ liệu sau một lần lưu scene
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** lưu scene làm một asset cấu hình rơi dữ liệu của field đã xoá khỏi code. **Vì:** lệnh lưu kéo theo mọi asset đang bẩn trong Editor.
**Làm:** sau lệnh lưu qua công cụ soát `git status`; file không thuộc task thì không stage, stage theo danh sách file của task (`workflow` › tra cứu › Git).

### `.meta` đổi trong git status sau khi chép thư mục
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** `.meta` đổi được báo là vỡ reference; khôi phục xong Unity đổi lại, và atlas chép vẫn pack thư mục cũ. **Vì:** hai asset trùng GUID thì Unity cấp GUID mới cho bản nhập sau và ghi lại `.meta` của nó; reference cũ vẫn trỏ bản gốc.
**Làm:** không khôi phục `.meta`; trỏ lại thứ chép theo (atlas, ô trong asset cấu hình, entry Addressables) vào bản chép, rồi soát `AssetReference` gãy (`editor-first` › tra cứu › Sửa asset).

### Dòng YAML vừa vá biến mất sau recompile hay import
`2026-11` · vấp 2 · đã đo
**Thấy:** vá YAML một prefab, eval thấy đúng, rồi sau recompile, import hay lần lưu của developer dòng vá mất — kể cả khi prefab sạch trong git. **Vì:** prefab đang mở trong Prefab Mode bật Auto Save: bản trong bộ nhớ ghi đè file ở lần lưu kế; "sạch trong git" không nói prefab có đang mở không.
**Làm:** trước khi sửa YAML kiểm `PrefabStageUtility.GetCurrentPrefabStage()` bằng eval, đang mở thì hỏi developer đóng; sau khi developer commit, soát phần mình bằng `git show <commit>:<path>` (`editor-first` › tra cứu › Sửa asset qua công cụ).

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
`2026-10` · vấp 1 · đã đo · đúng với dự án dùng Odin
**Thấy:** soát lỗi bố cục Inspector trong khi cửa sổ Unity bị che hoặc không focus, chụp màn hình không ra Inspector. **Vì:** chụp màn hình lấy thứ đang hiện trên màn; `GUIView.GrabPixels` (internal, gọi qua reflection) đọc pixel của chính view.
**Làm:** script tạm mở InspectorWindow nổi, mở foldout Odin qua state của property tree, chụp bằng `GrabPixels`; so với component không có khối Init (`editor-first` › tra cứu › IMGUI).
