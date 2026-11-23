# ui-reskin — kinh nghiệm

Bài học đã trả giá khi làm việc với `ui-reskin`. Đọc trọn cùng lúc với rule hoặc skill đó. Hình dạng và vòng đời ở `horcrux.md` › Memory.

### Màu nút giữ theo nghĩa của skin cũ
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** màn tham chiếu dùng một màu nút, agent giữ màu theo ý nghĩa của skin cũ dưới nhãn giả định; developer bắt đổi. **Vì:** màu, nhãn mà skin cũ gán theo ý nghĩa cũng là skin cũ: giữ lại là trộn hai skin.
**Làm:** mọi thuộc tính hình lấy từ màn tham chiếu; vai tham chiếu không có hình cho thì hỏi, không lấy từ skin cũ (`ui-reskin` › Tổ chức prefab).

### Giá trị cũ còn nằm ở gốc chung sau khi sửa từng variant
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** chép layout sang các variant anh em thay vì đặt ở gốc; giá trị cũ lại nằm ở gốc chung với một nhóm khác. **Vì:** chép sang từng variant là n bản buộc khớp.
**Làm:** đặt ở prefab gốc hẹp nhất phủ đúng nhóm; cần thì override tại gốc tầng giữa (`PrefabAudit.ApplyOverrides`) (`ui-reskin` › Tổ chức prefab).

### Chữ art khớp khung và `Diff` mà developer thấy lệch
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** chữ dựng khớp khung và `Diff`, developer nghiệm thu thấy màu lệch, viền dày mỏng khác demo, bóng đổ bị bỏ hẳn. **Vì:** khung bao đo bằng màu ruột không thấy viền và bóng; lớp mỏng chiếm ít pixel nên chìm trong `Diff` trung bình.
**Làm:** tách chữ demo thành lớp bằng số (`DemoCompare.Layers`), nghiệm thu theo lớp (`DemoCompare.LayerDiff`) (`ui-reskin` › Tổ chức prefab).

### Khung eval, bộ đo chép lại thay vì gọi hàm có sẵn
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** hàng trăm eval chép cùng khung dump, sửa, chụp, so; đoạn code phải đào lại từ lịch sử chat nhiều lần · viết lại bộ đọc PNG và dò dải màu bằng Python ngoài Editor trong khi `DemoCompare.Runs` đã có. **Vì:** demo và ảnh chụp đo bằng cùng một hàm, cùng cột, cùng tolerance mới so được.
**Làm:** gọi hàm Horcrux, không chép khung vào eval; khung nào eval tới lần thứ hai thì đề xuất công cụ (`ui-reskin` › Làm việc qua Editor tool; `workflow` › Việc lặp lại).

### Đổi màu viền chữ phải sửa từng prefab
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** Đổi màu viền chữ phải sửa từng prefab. **Vì:** viền, bóng đặt thẳng trên component.
**Làm:** kiểu chữ là `StylePreset` dùng chung gắn vào Style Presets, như material preset của TMP.

### Gọi service trong Play làm game kẹt ở màn chờ
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** Gọi service trong Play làm game kẹt ở màn chờ. **Vì:** gọi trước khi scene chứa service nạp.
**Làm:** `PlayProbe.WaitForSceneAsync`.

### Ảnh chụp qua lệnh MCP rơi vào `Assets/`, xuất hiện muộn
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** `manage_camera`, `game_view` với `save_path` không ghi ảnh ra ngoài project; ảnh xuất hiện muộn và Unity import nó thành asset. **Vì:** đường dẫn tính tương đối `Assets/`, lệnh ghi bất đồng bộ.
**Làm:** `UICapture` ghi đường dẫn bất kỳ.

### Eval cần type `internal` của dự án
`2026-10` · vấp 1 · đã đo
**Thấy:** eval không gọi được type `internal` để thêm component hay gán field. **Vì:** eval biên dịch ở assembly riêng.
**Làm:** lấy type bằng `System.Type.GetType("Ns.Type, TênAssembly")` rồi `AddComponent(type)`; field `[SerializeField]` gán qua `SerializedObject.FindProperty(...)`; sửa prefab đi `PrefabUtility.LoadPrefabContents` → sửa → `SaveAsPrefabAsset` → `UnloadPrefabContents` (`ui-reskin` › Làm việc qua Editor tool).

### Eval thất bại không dữ liệu ngay sau khi vào Play
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** Eval thất bại không dữ liệu ngay sau khi vào Play. **Vì:** Editor đang domain reload.
**Làm:** thử lại.
