# Bị Trói Cùng Nhau (2-Player Physics Chaos) - Android Game Source Code, bản 2.0

Game co-op vật lý 2 người: hai đứa bị trói chung một sợi dây, cùng khiêng nồi lẩu nóng tới bàn khách.
Tối ưu cho viral TikTok/Zalo (mời 1 chạm, ảnh highlight "pha cay cú").

## Cấu trúc thư mục

| Thư mục | Nội dung |
|---|---|
| `Scripts/Core/` | `NetworkRunnerHandler` (tạo/vào phòng, spawn người chơi, gửi input, tự kết nối lại), `RoomCode` (tạo & kiểm tra mã phòng) |
| `Scripts/Player/` | `SemiActiveRagdollController` (Floating Capsule, nhảy, tát, dây trói, respawn, nghiêng thân, co giãn), `PlayerInputCollector`, `NetworkInputData`, `ISlapReceiver` |
| `Scripts/Physics/` | `FragileCarryObject` (nồi lẩu), `PotStability` (luật sánh nước), `VerletRopeRenderer` + `RopeSolver` (vẽ dây), `CoopPhysicsMath` |
| `Scripts/Gameplay/` | `CheckpointZone`, `DeliveryZone` (đích + chấm sao) |
| `Scripts/Visual/` | `CoopCameraRig` (camera đóng khung 2 người + rung), `HotpotVFX` (hơi nóng, nước bắn, nồi rung cảnh báo), `Haptics` |
| `Scripts/UI/` | `CoopHUD`, `MobileJoystick`, `MobileButton` |
| `Scripts/ViralEngine/` | `DeepLinkManager`, `RingBufferRecorder` + `FrameRing` (ảnh highlight 6 khung), `VoiceDopplerEffect`, `MicrophonePermission` |
| `Scripts/Monetization/` | `AdMobRewardCheckpoint` (quảng cáo "Bảo hiểm nồi lẩu") |
| `Plugins/Android/` | AndroidManifest (Deep Link, micro, mạng, rung) |
| `Tests~/` | 48 unit test + stub để biên dịch ngoài Unity (Unity bỏ qua thư mục này) |

## Cài đặt

1. Unity 6 (6000.x), template 3D URP.
2. Import: Photon Fusion 2 **kèm Fusion Physics addon**, Photon Voice 2, Google Mobile Ads (v9+),
   NativeShare (github.com/yasirkula/UnityNativeShare).
3. Chép thư mục `Assets` vào project.
4. **Runner prefab**: GameObject có `NetworkRunner` + `NetworkSceneManagerDefault` + `RunnerSimulatePhysics3D`
   (để vật lý chạy theo tick của Fusion).
5. **Player prefab**: `NetworkObject`, `Rigidbody` (mass 1), `CapsuleCollider`, `NetworkRigidbody3D`,
   `SemiActiveRagdollController`. Tuỳ chọn: gán `spineBone`, `visualRoot` (model con, để co giãn),
   Photon Voice Speaker + `VoiceDopplerEffect`. Đặt `groundMask` **không** chứa layer Player.
6. **Nồi lẩu** (scene object): `NetworkObject`, `Rigidbody` (mass ~3), `NetworkRigidbody3D`,
   `FragileCarryObject` (gán 2 quai), `HotpotVFX` (kéo 1 material URP Particles/Unlit vào).
7. **Scene**: object có `NetworkRunnerHandler` (gán runnerPrefab, playerPrefab), `PlayerInputCollector`,
   `DeepLinkManager`, `MicrophonePermission`, `AdMobRewardCheckpoint`, `RingBufferRecorder`;
   Main Camera gắn `CoopCameraRig`; 1 object có `LineRenderer` + `VerletRopeRenderer` (tự gắn vào 2 người chơi);
   Canvas có `CoopHUD`, joystick và 3 nút (Jump/Grab/Slap). Đặt các `CheckpointZone` dọc đường
   (tăng dần `order`) và một `DeliveryZone` ở đích.
8. Player Settings > Package Name tự đặt (manifest không còn thuộc tính `package`).
9. Trước khi phát hành: thay ID quảng cáo thử nghiệm, đưa file `assetlinks.json` lên tên miền mời,
   viết lớp Java `com.studio.game.InstallReferrerBridge` nếu dùng Install Referrer.

## Điều khiển

- Bàn phím: WASD/mũi tên di chuyển, Space nhảy, giữ E/Shift để nắm quai, F/J để tát.
- Điện thoại: joystick trái; nút Nắm là bật/tắt (không cần giữ ngón), Nhảy, Tát.

## Luật chơi mới

- Hai người bị trói thật bằng dây (lực kéo vật lý), một người chạy là kéo người kia theo.
- Nồi lẩu có **thanh nước lẩu**: nghiêng, kéo giãn, va đập, bị tát đều làm sánh nước. Hết nước hoặc lật úp mới vỡ.
  Bản cũ vỡ ngay khi nghiêng 42°.
- Khiêng một mình thì nồi lệch, nước đổ dần (khoảng 17s nếu nghiêng 30°). Khiêng hai người thì nồi tự giữ thăng bằng tốt hơn.
- Tát đồng đội làm họ choáng 0.6s và tuột tay khỏi nồi.
- Nồi vỡ thì sau 3s tự hồi ở checkpoint gần nhất. Xem quảng cáo thì nồi hồi lại đầy nước.
- Giao nồi tới đích: còn 80% nước trở lên được 3 sao, 50% trở lên được 2 sao.

Chi tiết từng lỗi đã sửa: xem `CHANGELOG.md`.
