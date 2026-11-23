# AddressableReferenceAudit — soát `AssetReference` gãy trước khi vào Play

Code: `Editor/AddressablesLayout/AddressableReferenceAudit.cs`

Mỗi `AssetReference` đã gán trong prefab, file `.asset` (kể cả sub-asset) và scene (đang mở lẫn đang đóng) được kiểm hai điều: GUID còn asset
không, asset đó có là entry Addressables không (trực tiếp hay qua thư mục cha đã đăng ký). Hai lỗi này không có dòng đỏ nào
trong Editor; chúng chỉ lộ ở lần nạp đầu lúc chạy, thành `InvalidKeyException` hay asset rỗng ở đúng màn dùng tới nó.

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | developer sau khi chép, đổi tên, xoá asset hay chạy lại script setup Addressables; agent khi lỗi nạp asset chỉ lộ ở một màn |
| Đầu vào | `AddressableAssetSettingsDefaultObject.GetSettings(false)`; prefab, `.asset`, scene trong `Assets/` |
| Đầu ra | danh sách `Issue`; menu ghi mỗi lỗi một dòng đỏ, bấm là chọn asset chứa reference |
| Ranh giới | chỉ báo, không sửa; không biết domain (reference nào bắt buộc, sprite nào phải có trong atlas) — kiểm nội dung là việc của dự án |
| Chỗ đặt | `Editor/AddressablesLayout/`, menu `Horcrux > Addressables > Reference Audit`, assembly `com.horcrux.editor` |

**Hai loại lỗi**

| `IssueKind` | Báo khi | Hỏng ở đâu lúc chạy |
|---|---|---|
| `MissingAsset` | GUID không còn file (asset bị xoá, hay `.meta` bị đổi GUID khi chép thư mục ngoài Editor) | `RuntimeKeyIsValid` vẫn đúng; nạp thì `InvalidKeyException` |
| `NotAddressable` | asset còn nhưng không là entry, cả trực tiếp lẫn qua thư mục cha | `InvalidKeyException` ở lần nạp đầu, thường kéo theo lỗi "không tìm thấy" ở hệ dùng asset |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Báo ô `AssetReference` chưa gán | ô trống là lựa chọn của người author; hệ nào bắt buộc thì tự guard lúc boot |
| Mở scene đang đóng để walk | đổi trạng thái Editor; scene đóng đọc GUID từ YAML, vị trí là số dòng thay cho đường dẫn hierarchy |
| Tự chạy mỗi lần asset đổi, hay trước build | chạy khi được hỏi; quét nhanh nhờ lọc chữ nhưng vẫn đọc mọi file |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| Đọc chữ file trước, chỉ load file có `m_AssetGUID` | mọi `AssetReference` đã serialize để lại chuỗi đó trong YAML, cả override trên prefab lồng (`propertyPath: x.m_AssetGUID`); load mọi prefab, `.asset` để walk là phần tốn gần hết thời gian, trong khi chỉ vài file có reference. Dự án nguồn (170 prefab, 589 `.asset`, 2 file có reference): 48 s → 0,65 s |
| Asset lưu binary thì load hết như cũ, bỏ scene đóng kèm cảnh báo | không đọc được chữ thì không lọc được; báo thiếu thay vì kết luận sạch |
| Scene đang mở vẫn walk object, không đọc file | thấy cả sửa chưa lưu, và vị trí là đường dẫn hierarchy |
| Cửa sổ riêng | mỗi lỗi một dòng Console có context là đủ để tới asset |

## Luồng

```
Run(out cancelled)
  ├─ settings null → cảnh báo, trả rỗng
  ├─ prefab trong Assets/          ─┐ (text: file không chứa "m_AssetGUID" → bỏ, không load)
  ├─ mọi .asset (main + sub-asset) ─┼─ SerializedPropertyWalker.Walk → Visitor.Visit(AssetReference) ─┐
  ├─ scene đang mở                 ─┘                                                                ├─ TryClassify(guid)
  └─ scene đang đóng (text) ── regex "m_AssetGUID: g" | "propertyPath: …m_AssetGUID / value: g" ─────┘
        TryClassify: guid rỗng → bỏ qua · GUIDToAssetPath rỗng / load null → MissingAsset
                     · FindAssetEntry(guid, includeImplicit: true) null → NotAddressable · còn lại → hợp lệ
```

Huỷ ở thanh tiến độ thì `cancelled = true` và danh sách chỉ là một phần: không được kết luận "sạch".

## API

| Member | Dùng |
|---|---|
| `List<Issue> Run(out bool cancelled)` | gọi từ eval, test, hay hàm `static` của dự án cho `-executeMethod` |
| `Issue.Kind`, `OwnerPath`, `Location`, `Guid`, `TargetPath` | `Location` là `Object > Field[i] > Field`, cùng dạng với Usage Finder; scene đang đóng: `dòng N (field, scene đang đóng)` |
| menu `Horcrux > Addressables > Reference Audit` | ghi Console; tổng kết một dòng cuối |

## Liên quan

- Tìm *ai trỏ tới một asset* qua `AssetReference`: Usage Finder, tab Addressable Usages (`docs/Editor/UsageFinder.md`).
  Hai tool dùng chung `SerializedPropertyWalker`; audit quét thêm scene đang đóng bằng YAML.
- Khai group, address bằng bảng tĩnh: `docs/Editor/AddressablesLayout.md`. Chạy audit sau mỗi lần áp bảng hay chạy lại setup.
