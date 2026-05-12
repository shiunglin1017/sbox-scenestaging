# VR 三項異常手動驗證清單

本次修正涵蓋：

- VR 右手顯示與追蹤來源 fallback。
- PC 模式移動方向與鍵盤對應。
- 抓取需近距離才成立（無 attachment 也可抓取）。
- XMovement 主線 + 非 VR 輸入 fallback。
- Teleport / SnapTurn / SmoothTurn、Distance Grab、UI Ray/Poke、雙手握持與 Socket 防競態。

## 測試場景

- `Assets/Scenes/Tests/test.vr.scene`
- 場景內 `Cube`（`Rigidbody` + `Grabbable`）應放在 `Player_Root` 可伸手觸及的範圍內；`VRGrabber.MaxGrabDistance` 預設為 `12` 世界單位，若 Cube 被挪到遠處，Trigger 與距離檢查會讓抓取無法觸發。

## 驗證步驟

1. 進入場景後確認左右手都可見，且右手不會在啟動時消失。
2. VR 模式下移動手把，確認左右 `GhostTarget` 均能穩定跟隨。
3. 若手模型沒有 `weapon_hold` attachment，觀察 Console：應提示一次警告，但手部仍可追蹤。
4. 切到 PC 模式，使用 `W/A/S/D`，確認移動方向符合鏡頭前/右方向。
5. 對可抓取物（如 `Cube`）測試：手未靠近時按 Grip 不可抓取。
6. 將手靠近物件後按 Grip，應可抓取；放開後恢復正常掉落/拋擲。
7. 對「無 attachment/pivot」物件 hover，確認幾何 fallback 視覺預覽生效（手勢先轉向目標、手指 curl 隨距離增加）。
8. 將一個帶 `prop` tag 的 cloud asset 拖入場景（不手動加元件），若場景有 `AutoPropAdapterSystem`，應能在短時間內被補齊並可抓取。
9. 啟用 `VRTurnAndTeleportSystem`：按住 Teleport 觸發鍵確認有合法落點，再放開，玩家應瞬移到落點附近（頭部相對 root 偏移保持）。
10. 切換 `UseSnapTurn`：
   - 開啟：右搖桿 x 軸應固定角度段轉。
   - 關閉：右搖桿 x 軸應平滑轉向。
11. 在可抓物上測試 `VRDistanceGrabber`：遠處目標可被吸附拉近，接近手部後轉為一般持握。
12. 在掛有 `VRUIInteractable` 的按鈕代理物件上測試：
   - `VRUIPointerRay` 可 hover/press。
   - `VRUIPokeInteractor` 近距離觸碰可 press。
13. 讓左右手同時抓同一件物品（`VRTwoHandGripStabilizer`）：
   - 後手作為 pivot，前手方向決定物件朝向。
   - 若同時進入 socket 區域，`BlockSnapWhileTwoHanded` 開啟時不應被強制吸附。

## 參數建議

- `VRGrabber.MaxGrabDistance`：建議從 `10~14` 起調。
- `VRGrabber.GripPressThreshold`：建議 `0.45~0.60`。
- `VRGrabber.GripReleaseThreshold`：建議 `0.15~0.30`。

## VRRootColliderStabilizer 角速度上限驗證（180/360/540）

在 `Right Hand` 或 `Left Hand` 上掛載的 `VRRootColliderStabilizer` 中，固定 `RotationLerpSpeed`，只調整 `MaxDegreesPerSecond`。

1. 設為 `180`，快速甩腕，觀察 root collider 明顯較慢追隨，轉向延遲最大。
2. 設為 `360`，重複相同動作，應比 180 更快但仍有可感知限制。
3. 設為 `540`，重複相同動作，應接近原本手感，限制感最弱。
4. 三組都確認：不應出現瞬間翻轉或大幅過衝。

## 預期結果

- 數值越小，追隨越保守；數值越大，追隨越靈敏。
- 在相同甩腕幅度下，`180` 的姿態變化速度應小於 `360`，`360` 應小於 `540`。

## 建置與自動化測試

合併前請執行 `dotnet test`（指令見 [dotnet-build-test.md](./dotnet-build-test.md)）。
