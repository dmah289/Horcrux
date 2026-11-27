# ui-reskin — kinh nghiệm

Bài học đã trả giá khi làm việc với `ui-reskin`. Đọc trọn cùng lúc với rule hoặc skill đó. Hình dạng và vòng đời ở `docs` › Agent.

### Đổi màu viền chữ phải sửa từng prefab
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** Đổi màu viền chữ phải sửa từng prefab. **Vì:** viền, bóng đặt thẳng trên component.
**Làm:** kiểu chữ là `StylePreset` dùng chung gắn vào Style Presets, như material preset của TMP.

### Eval cần type `internal` của dự án
`2026-10` · vấp 1 · đã đo
**Thấy:** eval không gọi được type `internal` để thêm component hay gán field. **Vì:** eval biên dịch ở assembly riêng.
**Làm:** lấy type bằng `System.Type.GetType("Ns.Type, TênAssembly")` rồi `AddComponent(type)`; field `[SerializeField]` gán qua `SerializedObject.FindProperty(...)`; sửa prefab đi `PrefabUtility.LoadPrefabContents` → sửa → `SaveAsPrefabAsset` → `UnloadPrefabContents` (`editor-first` › tra cứu › Eval và Play qua công cụ).
