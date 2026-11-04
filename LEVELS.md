# Kịch bản chiến dịch "Bị Trói Cùng Nhau"

Hai đứa bạn thân làm thuê đủ việc trong một ngày Tết ở miền Tây, xui sao bị trói chung một sợi dây.
Mỗi màn là một "đơn hàng" khác nhau. Độ khó tăng dần, mỗi màn dạy thêm một cơ chế mới,
và màn sau mở khoá khi qua màn trước.

| # | Màn | Kiểu nhiệm vụ | Cơ chế mới | Thời gian | 3★ / 2★ |
|---|---|---|---|---|---|
| 1 | Khiêng lẩu đầu năm | Giao đồ dễ vỡ | Nắm quai, thanh nước lẩu | 3:00 | 1:30 / 2:20 |
| 2 | Bắt gà khai trương | Gom vật (AI chạy trốn) | Gà né người, giãy khỏi tay | 2:00 | 1:10 / 1:40 |
| 3 | Qua cầu khỉ miền Tây | Cùng về đích | Bệ di động, ghe chở người, vùng chết | 2:30 | 1:20 / 2:00 |
| 4 | Cửa hai chìa | Câu đố + cùng về đích | Bàn đạp, cổng, dây trói làm giới hạn | 4:00 | 2:30 / 3:20 |
| 5 | Chạy lũ | Trụ được + vùng an toàn | Nước dâng, lội nước chậm | 1:30 | (sao theo số lần rơi) |
| 6 | Bánh cưới bảy tầng | Giao đồ dễ vỡ | Quạt trần quay, búa con lắc | 3:00 | 1:40 / 2:30 |
| 7 | Gom xoài mùa gió | Gom vật (sinh liên tục) | Gió giật theo chu kỳ, vật rơi từ cây | 2:30 | 1:30 / 2:00 |
| 8 | Đẩy xe ba gác lên dốc | Gom vật nặng (cần 2 người) | Vật nặng, phản lực, dốc | 3:00 | 1:45 / 2:30 |

Kiểu nhiệm vụ = component trong `Scripts/Levels/Objectives`; cơ chế = component trong `Scripts/Levels/Mechanics`.
Mỗi màn có thể có nhiều nhiệm vụ, kể cả nhiệm vụ phụ (bỏ chọn `required`) để thưởng thêm.

---

## Màn 1 · Khiêng lẩu đầu năm (tutorial)

**Chuyện:** Mùng 1, bà chủ quán đặt hai đứa bưng nồi lẩu mắm từ bếp ra bàn khách VIP ngoài sân.

**Bố cục:** Bếp → hành lang hẹp có 2 bậc thang → sân có bàn ghế lộn xộn → bàn khách (đích).
Checkpoint ở cửa bếp và đầu sân.

**Dạy:** di chuyển, nhảy, nắm quai (2 người 2 bên), dây trói giữ 2 người gần nhau, thanh nước lẩu.

**Nhiệm vụ:** `DeliverPotObjective` "Đưa lẩu tới bàn khách". Sao = min(sao thời gian, sao nước lẩu).

**Dựng:** nồi lẩu + `FragileCarryObject` + `HotpotVFX`, `DeliveryZone` ở bàn, 2 `CheckpointZone`.

---

## Màn 2 · Bắt gà khai trương

**Chuyện:** Quán mở hàng, ông chủ lỡ tay làm sổng 5 con gà. Khách sắp tới!

**Bố cục:** Sân vườn hình tròn có bụi chuối, lu nước, đống rơm (chỗ gà trốn). Chuồng gà ở giữa sân, cửa hẹp.

**Lối chơi:** Gà chạy né người trong bán kính 4m, thỉnh thoảng nhảy cẫng lên. Một người dồn, một người chụp.
Mang gà lâu thì gà có thể giãy thoát. Tát đồng đội làm rơi gà là "pha cay cú" kinh điển.

**Nhiệm vụ:** `CollectObjective` category `ga`, target 5, vùng = chuồng, `mustBeReleased` bật (phải thả gà vào chuồng).
Nhiệm vụ phụ (tuỳ chọn): `CollectObjective` category `gatrong` (1 con gà trống chạy nhanh hơn).

**Dựng:** prefab gà = NetworkObject + Rigidbody (mass 1.5, freeze rotation X/Z) + NetworkRigidbody3D + `Collectible`
(`fleeRadius 4`, `fleeSpeed 5.5`, `maxCarriers 1`, `escapeChancePerSecond 0.08`). Đặt 5 con trong scene.

---

## Màn 3 · Qua cầu khỉ miền Tây

**Chuyện:** Giao mâm ngũ quả qua bên kia rạch, mà cây cầu khỉ thì... chỉ có một thân tre.

**Bố cục:** Bờ A → cầu khỉ (dầm hẹp 0.6m) → 2 bè chuối trôi ngang (bệ di động PingPong) → ghe chạy vòng
(Loop) → cầu khỉ thứ 2 lắc lư (Swing quanh trục dọc ±8°) → bờ B (đích). Nước bên dưới là `KillVolume`.
Checkpoint ở giữa rạch (cồn cát).

**Lối chơi:** Dây trói 4.2m khiến một người rơi là kéo người kia chới với. Phải canh nhịp bè cùng nhảy.

**Nhiệm vụ:** `ReachTogetherObjective` "Cả hai sang bờ bên kia" (đứng chung 0.75s).
Rơi 3 lần mất 1 sao (`fallsPerStarLost = 3` trên LevelManager).

**Dựng:** bè/ghe = Rigidbody kinematic + `KinematicMover` (Path). Cầu lắc = `KinematicMover` (Swing, axis = hướng cầu,
amplitude 8, period 2.5). Lệch `timeOffset` giữa các bè.

---

## Màn 4 · Cửa hai chìa

**Chuyện:** Kho lúa của chú Tư có khoá "hai chìa": cửa chỉ mở khi có người đứng trên cả hai bàn đạp.

**Bố cục:** 3 phòng nối tiếp.
- Phòng 1: 2 bàn đạp cách nhau 3m (vừa tầm dây) → cổng 1 (`AllPlates`). Dạy cơ chế.
- Phòng 2: 2 bàn đạp cách nhau 6m (xa hơn dây 4.2m!) → phải đẩy thùng gỗ (`Collectible` mass 20, `heavyObjectsCanPress`)
  lên một bàn đạp. Cổng 2 `latchOpen` để không bị kẹt.
- Phòng 3: bàn đạp A mở cầu rút B và ngược lại (`AnyPlate`, không latch): một người giữ, một người qua, rồi người
  đã qua đứng bàn đạp bên kia giữ cho bạn. Đích sau cầu.

**Nhiệm vụ:** `ReachTogetherObjective` "Vào kho lúa".

**Dựng:** `PressurePlate` (scene NetworkObject, gán plateVisual + indicator). `LevelGate` với door là Rigidbody kinematic,
`openOffset (0,-3,0)` cho cổng sập xuống hoặc `(0,0,4)` cho cầu rút.

---

## Màn 5 · Chạy lũ

**Chuyện:** Nước rằm tháng Chạp lên bất thường. Hai đứa phải leo lên nóc nhà thờ họ trước khi nước cuốn.

**Bố cục:** Xóm nhà sàn nhiều tầng: mái tôn, cầu thang tre, bệ nâng (KinematicMover Path lên xuống), mái nhà thờ cao 12m.
Checkpoint ở mỗi tầng (càng lên cao `order` càng lớn).

**Lối chơi:** Nước bắt đầu dâng sau 5s, 0.12 m/s. Lội nước thì chậm và bị đẩy nổi. Chìm thì về checkpoint nếu
checkpoint còn trên mặt nước, không thì thua màn.

**Nhiệm vụ:** `SurviveObjective` 90s, `safeZoneAtEnd` = mái nhà thờ (hết giờ cả hai phải ở trên mái).
Sao = 3 trừ số lần rơi/chìm.

**Dựng:** mặt nước = plane có shader nước, gán vào `RisingWater.surface` (startY -1, maxY 11).
LevelManager: `timeLimit` = 100 (dư ra cho chắc), `threeStarTime`/`twoStarTime` = 0 (không chấm thời gian).

---

## Màn 6 · Bánh cưới bảy tầng

**Chuyện:** Đám cưới anh Ba. Bánh kem bảy tầng phải đi qua sảnh tiệc có... dãy quạt trần treo thấp đang quay.

**Bố cục:** Bếp bánh → hành lang 3 quạt trần (Spin 120°/s, cánh quạt ngang tầm đầu, mỗi cái lệch pha)
→ đoạn 2 đèn chùm đung đưa (Swing ±50°) → sân khấu (đích).

**Lối chơi:** Như màn 1 nhưng vật dễ vỡ hơn: `PotStability` của bánh có `safeTilt 10`, `maxTilt 35`,
`impactSafeSpeed 2.5`. Quạt đập trúng là bánh lệch, kem bay. Nhảy là mạo hiểm.

**Nhiệm vụ:** `DeliverPotObjective` "Đưa bánh lên sân khấu". Nhiệm vụ phụ: `SurviveObjective` không cần (dùng sao).

**Dựng:** dùng lại prefab nồi lẩu nhưng đổi model bánh, chỉnh Settings. Cánh quạt = Rigidbody kinematic + `KinematicMover` (Spin).
Đổi màu `HotpotVFX` (hơi trắng → hạt kem hồng).

---

## Màn 7 · Gom xoài mùa gió

**Chuyện:** Gió chướng về, xoài cát Hoà Lộc rụng đầy vườn. Hứng đủ 12 trái vào sọt trước khi trời tối.

**Bố cục:** Vườn xoài có 4 cây (điểm sinh), mương nước giữa vườn (`KillVolume`, xoài rơi xuống là mất),
2 cây cầu ván, sọt xoài trên chòi cao. Vùng gió thổi ngang vườn.

**Lối chơi:** `ItemSpawner` thả xoài mỗi 2.5s (tối đa 6 trái chưa nhặt). Gió giật chu kỳ 5s, 40% thời gian lặng:
canh lúc gió lặng mà qua cầu. Xoài nhẹ bị gió thổi mạnh hơn người.

**Nhiệm vụ:** `CollectObjective` category `xoai`, target 12, `mustBeReleased` tắt (ném/thả trúng sọt là được).

**Dựng:** prefab xoài = NetworkObject + Rigidbody (mass 0.3) + NetworkRigidbody3D + `Collectible` (`despawnOnFall` bật,
`fleeRadius 0`). Thêm prefab vào Network Prefabs của Fusion. `WindVolume` = BoxCollider bao vườn, direction (1,0,0),
strength 14.

---

## Màn 8 · Đẩy xe ba gác lên dốc (màn cuối)

**Chuyện:** Chở nguyên xe ba gác bánh tét lên chùa trên đồi cho kịp giao thừa.

**Bố cục:** Chân dốc → dốc 18° dài 25m có vài ổ gà (bậc nhỏ) → đoạn bằng có bệ di động ngang → dốc 2 → sân chùa (đích).
Checkpoint giữa dốc. Hai bên dốc là vực (`KillVolume`).

**Lối chơi:** Xe nặng 80kg, mỗi người kéo tối đa 300N nên một mình kéo không nổi trên dốc; cả hai phải cùng nắm
(`maxCarriers 2`). Phản lực ghì người kéo lại. Thả tay là xe trôi ngược!

**Nhiệm vụ:** `CollectObjective` category `xe`, target 1, vùng = sân chùa, `mustBeReleased` tắt.
Nhiệm vụ phụ: `CollectObjective` category `banhtet` target 3 (bánh tét rơi khỏi xe dọc đường, nhặt lại).

**Dựng:** xe = NetworkObject + Rigidbody (mass 80, drag 0.5) + NetworkRigidbody3D + `Collectible`
(`maxCarriers 2`, `maxPullForcePerCarrier 300`, `reactionShare 0.6`, `fleeRadius 0`).

---

## Dựng một màn mới (tóm tắt)

1. Tạo scene trong `Assets/_Project/Scenes/`, thêm vào **Build Profiles > Scene List** (scene Menu ở vị trí 0).
2. Thêm 1 GameObject `Level` có `NetworkObject` + `LevelManager` (đặt `levelId`, thời gian, playerSpawnPoints).
3. Thêm các nhiệm vụ (mỗi nhiệm vụ là component trên một scene object có `NetworkObject`).
4. Thêm cơ chế: `KinematicMover`, `PressurePlate` + `LevelGate`, `RisingWater`, `WindVolume`, `KillVolume`, `ItemSpawner`,
   `CheckpointZone`.
5. Canvas có `CoopHUD` + `LevelHUD`. Main Camera có `CoopCameraRig`. 1 `VerletRopeRenderer`.
6. Thêm một dòng `LevelInfo` vào `CampaignCatalog` (Id trùng `levelId`).

## Ý tưởng cho các màn sau

- **Chợ nổi Cái Răng:** nhảy giữa các ghe đang trôi để giao bún riêu (DeliverPot + KinematicMover Loop).
- **Thả đèn trời:** giữ thăng bằng trên bè giữa sông 60s trong khi bị người kia kéo (Survive + Swing).
- **Đua heo:** bắt heo đất (Collectible chạy nhanh, nặng) trong chuồng bùn trơn.
- **Giao thừa:** màn tổng hợp: nồi lẩu + cổng hai chìa + nước dâng.
