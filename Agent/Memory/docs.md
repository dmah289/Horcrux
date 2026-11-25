# docs — kinh nghiệm

Bài học đã trả giá khi làm việc với `docs`. Đọc trọn cùng lúc với rule hoặc skill đó. Hình dạng và vòng đời ở `horcrux.md` › Memory.

### Code của Plan phải qua compiler mà không được ghi vào Horcrux
`2026-11` · vấp 1 · đã đo · đúng với máy build csproj bằng MSBuild
**Thấy:** Plan đòi code đã qua compiler, nhưng code runtime Horcrux là phần developer gõ, ghi tạm vào Horcrux rồi xoá thì đụng hook chặn xoá và có thể trùng file developer đang gõ. **Vì:** compile chỉ cần csproj, không cần file nằm trong `Assets/`.
**Làm:** chép csproj của assembly consumer (assembly dự án gọi lớp con) ra scratchpad bằng script ghi bằng Write: thay file bản tạm bằng file mẫu, thêm `<Compile Include>` cho file Horcrux mẫu, đổi đường dẫn `Assets\`, `Library\`, `ProjectReference` thành tuyệt đối, `OutputPath` về scratchpad; build với `-p:BuildProjectReferences=false`. Mỗi giai đoạn một csproj; rồi ráp đoạn cũ → mới của Plan bằng script, so từng ký tự với file mẫu (`docs` › tra cứu › Plan).
