# inbox — bài học chưa rõ miền

Chờ lượt dọn xếp về đúng miền.

### Báo cáo doanh thu quảng cáo có một nhóm placement, level mặc định ("0", level 1) mà code không gửi giá trị đó
`2026-11` · vấp 1 · đã đo · đúng với SDK quảng cáo tự log doanh thu thẳng lên analytics, còn event của game đi qua cổng có anti-cheat
**Thấy:** nhóm nhỏ user có doanh thu quảng cáo dồn ở level 1, placement "0", cả interstitial lẫn rewarded; level sau gần như 0;
đọc code mấy vòng không ra chỗ sinh "0". **Vì:** máy cài APK ngoài store bị anti-cheat của game coi là cheater, cổng analytics
của game bỏ mọi event và user property (level, ad_track) nhưng SDK quảng cáo log `ad_impression` thẳng, hỏi anti-cheat riêng
của SDK (chưa đăng ký nên luôn "không cheat"); báo cáo ghép thiếu nên rơi về mặc định.
**Làm:** đo trên máy cắm cáp sớm, trước khi đoán tiếp: DeviceProbe `events` in nguồn cài, event Firebase theo tên và
tham số, callback doanh thu của SDK và dòng so hai bên (`docs/Editor/DeviceProbe.md` › Kiểm event analytics). Sửa bằng một luật: đăng
ký interface anti-cheat của SDK trỏ về anti-cheat của game (cả hai cùng chặn), hoặc cho cả hai cùng qua rồi lọc theo
`app_info.install_source` ở phía dữ liệu.
