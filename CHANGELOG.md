# CHANGELOG - bản 2.0

## Lỗi nghiêm trọng (game không chơi được)

1. **Nhân vật không bao giờ di chuyển.** `ProvideInput = true` nhưng không có `INetworkRunnerCallbacks.OnInput` nào gửi input.
   → Thêm `PlayerInputCollector` (bàn phím + cảm ứng) và `OnInput` trong `NetworkRunnerHandler`.
2. **Không spawn người chơi.** Không có `OnPlayerJoined`. → Host spawn/despawn nhân vật theo người vào/ra.
3. **Nhảy chỉ cao khoảng 6cm.** Ngay khi nhảy, lò xo Floating Capsule kéo nhân vật dính lại mặt đất (đã tái hiện bằng mô phỏng).
   → Tắt lò xo 0.25s sau khi nhảy: nhảy cao khoảng 4.5m đúng thiết kế.
4. **Nồi lẩu không bao giờ có người khiêng.** `carrierLeft/Right` phải gán trong Inspector, nhưng người chơi spawn lúc chạy nên luôn null.
   → Tự bám quai khi giữ nút Nắm trong phạm vi 1.1m.
5. **Nồi lẩu (scene object) có thể không được đăng ký mạng** vì `StartGameArgs` không truyền Scene. → Truyền `NetworkSceneInfo` của scene.
6. **AndroidManifest có `package=`**: Unity 6 (AGP 8) báo lỗi build. → Bỏ đi, đặt trong Player Settings.
7. **Không xin quyền micro lúc chạy** (Android 6+) nên voice chat không thu được tiếng. → `MicrophonePermission`.

## Lỗi mạng / logic

8. Client cũng `AddForce` trong `FixedUpdateNetwork`; Fusion resimulate nhiều lần nên lực bị cộng dồn, gây giật hoặc bay người. → Vật lý chỉ chạy trên host.
9. Input di chuyển không bị chặn độ dài (đi chéo nhanh hơn, có thể gian lận). → `ClampMagnitude(1)`.
10. Tát: một vật nhiều collider bị tát nhiều lần; `OverlapSphere` cấp phát mảng mỗi lần; không có cooldown. → Bỏ trùng, NonAlloc, cooldown 0.55s.
11. Nghiêng thân trên bị Animator ghi đè (gán trong Render) và thay thế hẳn tư thế animation. → Áp trong LateUpdate, cộng lên tư thế.
12. Gia tốc tính theo frame nên nghiêng giật cục; trên client Rigidbody kinematic nên vận tốc = 0. → Ước lượng từ vị trí và làm mượt.
13. `StartCoopSession` có thể bị gọi 2 lần cùng lúc (deep link + referrer). → Khoá chống gọi lặp.
14. "Chống đứt mạng" chưa có (`OnApplicationPause` chỉ đặt `captureFramerate`, vô tác dụng). → Tự kết nối lại tối đa 4 lần (1s, 2s, 4s, 8s); host thoát thì người còn lại thành host.
15. Mã phòng từ deep link không được kiểm tra; link sai định dạng làm crash (`new Uri`, `Split()[1]`). → `RoomCode` chỉ nhận chữ số 4-12 ký tự.
16. `deepLinkActivated` không gỡ đăng ký. Đánh dấu đã kiểm tra referrer trước khi có kết quả nên dễ mất lời mời. `Instance` null ở frame đầu.
17. Không có phòng thì nút mời gửi link `room=` rỗng. → Tự tạo phòng mã ngẫu nhiên (bỏ ký tự dễ nhầm 0/O, 1/I/L).
18. Dây Verlet: dây bay từ gốc toạ độ ở frame đầu; dùng `deltaTime` thay đổi nên dây giật/nổ khi tụt FPS. → Khởi tạo đúng, bước cố định 1/60s (kiểm thử cho kết quả giống nhau ở 30/60/144 FPS).
19. Dây chỉ để trang trí, hai người không hề bị trói. → Thêm lực dây thật (tether) giữa 2 nhân vật.
20. `VoiceDopplerEffect`: `targetPlayerRb` không gán được cho người chơi spawn; gán `reverbPreset` mỗi frame gây rè tiếng; pitch giảm sâu khiến giọng stream bị trễ dần. → Tự tìm, chỉ đổi khi cần, giới hạn pitch.
21. AdMob: callback có thể chạy ngoài main thread; `Destroy` quảng cáo ngay lúc đang hiển thị; tải lỗi một lần là hết quảng cáo; không có phản hồi khi chưa có quảng cáo; `Instance` không chống trùng.

## Hiệu năng

22. **RingBufferRecorder dùng khoảng 237MB RAM** (160 khung 540x960 thô) và tạo khoảng 31MB rác/giây (`ToArray()`), dễ bị Android kill.
    → Vòng đệm cấp phát trước khoảng 26MB, không tạo rác mỗi frame.
23. Gán `targetTexture` vào camera được truyền vào: nếu là Main Camera thì màn hình đen, nếu là camera phụ thì cảnh bị render 2 lần mỗi frame.
    → Camera ghi hình riêng, tắt sẵn, chỉ render 12 lần/giây ở 270x480.
24. Ghép và mã hoá ảnh ngay trên main thread đúng lúc cao trào. → Ghép trên luồng phụ, ghi file bất đồng bộ.
25. RenderTexture không được giải phóng; callback đọc GPU sau khi object bị huỷ. → Đã xử lý.
26. Dây: khoảng 264 `SphereCast` mỗi frame (22 node x 12 vòng lặp). → 22 `Raycast` mỗi bước (giảm khoảng 12 lần).

## Gameplay mới

- Thanh nước lẩu: sánh dần theo độ nghiêng, độ kéo giãn, va đập, bị tát; nồi tự giữ thăng bằng hơn khi có 2 người khiêng.
- Checkpoint, tự hồi nồi sau 3s, respawn khi rơi khỏi map, đích giao hàng có chấm 1-3 sao.
- Coyote time + jump buffer (nhảy nhạy hơn), điều khiển trên không, đi chậm hơn khi khiêng.
- Tát làm choáng và tuột tay khỏi nồi.
- Quảng cáo "Bảo hiểm nồi lẩu": hồi nồi đầy nước.
- Nút Nắm dạng bật/tắt trên điện thoại (đỡ phải dùng 3 ngón).

## Đồ hoạ / cảm giác

- Camera tự đóng khung 2 người + nồi, lùi ra khi 2 người xa nhau; rung màn hình khi tát, tiếp đất mạnh, sánh nước, vỡ nồi.
- Hơi nóng bốc lên theo lượng nước, chuyển cam đỏ khi nguy hiểm; nước lẩu bắn tung toé; nồi rung cảnh báo.
- Nhân vật co giãn khi nhảy/tiếp đất, phồng lên khi tát.
- Dây đổi màu đỏ và mảnh lại khi bị kéo căng.
- Ảnh highlight dạng truyện tranh 6 khung (kể lại cả pha làm vỡ nồi) thay vì 1 ảnh; nút "Khoe pha này" thay vì bật bảng chia sẻ giữa lúc chơi.
- Rung điện thoại khi vỡ nồi; HUD có thanh nước nhấp nháy, đếm ngược, thông báo mất mạng, màn hình thắng.
