# inbox — bài học chưa rõ miền

Chờ lượt dọn xếp về đúng miền.

### Báo cáo doanh thu quảng cáo có một nhóm placement, level mặc định ("0", level 1) mà code không gửi giá trị đó
`2026-11` · vấp 1 · đã đo · đúng với SDK quảng cáo tự log doanh thu thẳng lên analytics, còn event của game đi qua cổng có anti-cheat
**Thấy:** nhóm nhỏ user có doanh thu quảng cáo dồn ở level 1, placement "0", cả interstitial lẫn rewarded; level sau gần như 0.
**Vì:** máy cài APK ngoài store bị anti-cheat của game coi là cheater, cổng analytics của game bỏ mọi event và user property
(level, ad_track) nhưng SDK quảng cáo vẫn log `ad_impression` thẳng; báo cáo ghép thiếu nên rơi về mặc định.
**Làm:** trên máy cắm cáp: `dumpsys package <pkg>` xem `installerPackageName`, bật `log.tag.FA VERBOSE` rồi đếm tên event
trong logcat — chỉ có `ad_impression` là cổng đang chặn; thống nhất một luật anti-cheat cho cả hai đường, hoặc lọc nhóm cài
ngoài store khỏi báo cáo (`app_info.install_source`).
