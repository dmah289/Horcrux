# Player Prefs Editor

Tool xem, sửa, xoá `PlayerPrefs` trực tiếp từ Windows Registry.

Mở: **Horcrux → Player Prefs Editor**

---

## Kiến trúc

```
PlayerPrefsEditor/
├── IPlayerPrefsProvider.cs      — Interface: đọc danh sách + dirty flag
├── WindowsPlayerPrefsProvider.cs — Implementation: đọc từ Windows Registry
├── PlayerPrefsEditor.cs         — EditorWindow: hiển thị + chỉnh sửa
├── PlayerPrefsPair.cs           — Struct: Key/Value + metadata (TypeColor, AliasType)
├── JsonEditWindow.cs            — Popup: xem/sửa JSON dạng indented
└── PLayerPrefsEditor.md         — Tài liệu này
```

### Trách nhiệm từng file

| File | Trách nhiệm |
|------|-------------|
| `IPlayerPrefsProvider` | Định nghĩa contract: `PlayerPrefsPairs` (getter) + `MarkDirty()` |
| `WindowsPlayerPrefsProvider` | Đọc Registry, phân biệt int/float/string. Cache kết quả, chỉ đọc lại khi `isDirty == true` |
| `PlayerPrefsEditor` | EditorWindow chính: toolbar, search, bảng dữ liệu, status bar. Event-driven refresh (OnFocus, play mode change, nút Refresh) |
| `PlayerPrefsPair` | Struct chứa `Key` + `Value` (object). Tính `TypeColor` và `AliasType` từ runtime type |
| `JsonEditWindow` | Popup utility: indent/minify JSON, validate realtime, trả compact JSON về row chính qua callback |

### Data flow

```
                        MarkDirty()
  OnEnable / OnFocus / PlayModeChange / Save / Delete ──→ WindowsPlayerPrefsProvider
                                                                    │
                                                            isDirty = true
                                                                    │
                                          OnGUI (Layout event) ─────┘
                                                │
                                      PlayerPrefsPairs getter
                                      (đọc Registry nếu dirty)
                                                │
                                        CachePerRowData()
                              (valueStr[], isJson[], keyToIndex)
                                                │
                                    DrawPlayerPrefs() — Repaint
                                    (dùng cached data, không I/O)
```

### Caching

| Cấp | Áp dụng | Chi tiết |
|-----|---------|----------|
| **Dirty-flag** | Registry I/O | Chỉ đọc khi `isDirty`, không polling mỗi frame |
| **Event-phase** | Per-row data | `cachedValueStrs[]`, `cachedIsJson[]`, `cachedAliasTypes[]`, `cachedTypeColors[]`, `keyToIndex` — tính ở Layout, Repaint dùng lại |
| **Dirty-flag** | Layout options | `keyWidthOpt`, `cachedValueWidth` — chỉ rebuild khi width thay đổi |
| **Dirty-flag** | String row height | `cachedStringMaxHeightValue`, `cachedStringMaxHeightOpt` — chỉ rebuild khi lineHeight thay đổi |
| **O(1) lookup** | `FindPairIndex()` | `keyToIndex` dictionary thay vì linear scan O(n) |
| **Instance lazy** | GUIStyle | `typeStyle`, `wordWrapStyle` — init 1 lần với null-check |
| **Static eager** | Color, GUILayoutOption[], GUIContent | `RowEvenColor`, `jsonBtnWidth`, `clearSearchContent` etc. — `static readonly` |
| **Dynamic GUIContent** | Status bar | `statusContent.text` chỉ cập nhật khi giá trị thay đổi |

---

## Chỉnh sửa

Nhập trực tiếp vào ô Value. Khi có thay đổi:

- Ô Value chuyển **cam** → nút 💾 Save / ↩ Revert được kích hoạt
- 💾 **Save** — ghi vào Registry, giữ đúng kiểu dữ liệu gốc. Nếu giá trị không hợp lệ → không lưu, Console hiện warning
- ↩ **Revert** — huỷ thay đổi, trả về giá trị gốc

### Giá trị string dài

String hiển thị với word-wrap. Nếu nội dung vượt quá **7 dòng hiển thị** (tính theo wrap thực tế, không phải số `\n`), ô Value tự chuyển sang dạng cuộn — chiều cao cố định 7 dòng, cuộn dọc để xem/sửa phần còn lại.

---

## JSON Editor

Nếu value là **JSON hợp lệ** (bắt đầu bằng `{` hoặc `[`, cặp ngoặc đóng đúng), nút 📋 xuất hiện cạnh ô Value.

Nhấn 📋 mở popup:

- JSON hiển thị dạng indented (4 spaces)
- Validate realtime — JSON hỏng → cảnh báo vàng + nút Save bị disabled
- 💾 **Save** — compact JSON → trả về row chính (row chuyển cam, chưa ghi Registry)
- **Cancel** — đóng popup, không thay đổi gì
- Chỉ mở được 1 popup tại 1 thời điểm

Luồng: 📋 → sửa indented → Save popup → row cam → 💾 Save row → ghi Registry.

---

## Xoá

- 🗑 **Delete** trên từng dòng — xoá ngay, không hỏi xác nhận
- 🗑 **Delete All** — có hộp xác nhận. Khi đang search, cảnh báo rõ sẽ xoá **tất cả** entry chứ không chỉ kết quả đang lọc

---

## Tìm kiếm

Gõ vào ô Search → lọc tức thì theo Key, Value, Type (không phân biệt hoa thường). Nhấn **X** để xoá bộ lọc.

> Save All / Delete All tác động lên **tất cả** entry, kể cả entry đang bị ẩn bởi bộ lọc.

---

## Quick Buttons

| Nút | Khi nào disabled |
|-----|------------------|
| 💾 Save All / ↩ Revert All | Không có entry nào bị modified |
| 🗑 Delete All | Danh sách rỗng |
| ↻ Refresh | Luôn enabled — đọc lại Registry thủ công |

Save All lưu từng entry theo kiểu gốc. Entry nào parse fail thì giữ cam + warning, các entry hợp lệ vẫn lưu bình thường.

---

## Lưu ý

- **Cập nhật tự động khi**: focus window, vào/thoát Play Mode, hoặc sau mỗi Save/Delete. Nút ↻ Refresh để cập nhật thủ công
- **Float precision**: `0.3f` có thể hiện `0.300000012` — hành vi bình thường của floating point
- **Không tạo key mới** từ tool — dùng `PlayerPrefs.SetXxx()` trong code
- **Không có Undo** cho thao tác đã Save/Delete
- **Chỉ hỗ trợ Windows** (đọc từ Registry). Platform khác hiện thông báo không hỗ trợ
