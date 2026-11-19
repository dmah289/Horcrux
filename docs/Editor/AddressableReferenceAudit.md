# AddressableReferenceAudit — soát `AssetReference` gãy trước khi vào Play

Code: `Editor/AddressablesLayout/AddressableReferenceAudit.cs`

Mỗi `AssetReference` đã gán trong prefab, file `.asset` (kể cả sub-asset) và scene đang mở được kiểm hai điều: GUID còn asset
không, asset đó có là entry Addressables không (trực tiếp hay qua thư mục cha đã đăng ký). Hai lỗi này không có dòng đỏ nào
trong Editor; chúng chỉ lộ ở lần nạp đầu lúc chạy, thành `InvalidKeyException` hay asset rỗng ở đúng màn dùng tới nó.

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | developer sau khi chép, đổi tên, xoá asset hay chạy lại script setup Addressables; agent khi lỗi nạp asset chỉ lộ ở một màn |
| Đầu vào | `AddressableAssetSettingsDefaultObject.GetSettings(false)`; prefab và `.asset` trong `Assets/`; scene đang mở |
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
| Quét scene trên đĩa | phải mở từng scene, đổi trạng thái Editor; mở scene cần soát rồi chạy |
| Tự chạy mỗi lần asset đổi, hay trước build | quét toàn project mất cỡ chục giây trở lên; chạy khi được hỏi |
| Cửa sổ riêng | mỗi lỗi một dòng Console có context là đủ để tới asset |

## Luồng

```
Run(out cancelled)
  ├─ settings null → cảnh báo, trả rỗng
  ├─ prefab trong Assets/          ─┐
  ├─ mọi .asset (main + sub-asset) ─┼─ SerializedPropertyWalker.Walk → Visitor.Visit(AssetReference)
  └─ scene đang mở                 ─┘      ├─ m_AssetGUID rỗng           → bỏ qua
                                           ├─ GUIDToAssetPath rỗng / load null → MissingAsset
                                           ├─ FindAssetEntry(guid, includeImplicit: true) null → NotAddressable
                                           └─ còn lại → hợp lệ
```

Huỷ ở thanh tiến độ thì `cancelled = true` và danh sách chỉ là một phần: không được kết luận "sạch".

## API

| Member | Dùng |
|---|---|
| `List<Issue> Run(out bool cancelled)` | gọi từ eval, test, hay hàm `static` của dự án cho `-executeMethod` |
| `Issue.Kind`, `OwnerPath`, `Location`, `Guid`, `TargetPath` | `Location` là `Object > Field[i] > Field`, cùng dạng với Usage Finder |
| menu `Horcrux > Addressables > Reference Audit` | ghi Console; tổng kết một dòng cuối |

## Liên quan

- Tìm *ai trỏ tới một asset* qua `AssetReference`: Usage Finder, tab Addressable Usages (`docs/Editor/UsageFinder.md`).
  Hai tool dùng chung `SerializedPropertyWalker` và cùng phạm vi quét.
- Khai group, address bằng bảng tĩnh: `docs/Editor/AddressablesLayout.md`. Chạy audit sau mỗi lần áp bảng hay chạy lại setup.
