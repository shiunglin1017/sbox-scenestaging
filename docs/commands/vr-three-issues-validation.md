# VR 三項異常手動驗證清單

本次修正涵蓋：

- VR 右手顯示與追蹤來源 fallback。
- PC 模式移動方向與鍵盤對應。
- 抓取需近距離才成立（無 attachment 也可抓取）。

## 測試場景

- `Assets/Scenes/Tests/test.vr.scene`

## 驗證步驟

1. 進入場景後確認左右手都可見，且右手不會在啟動時消失。
2. VR 模式下移動手把，確認左右 `GhostTarget` 均能穩定跟隨。
3. 若手模型沒有 `weapon_hold` attachment，觀察 Console：應提示一次警告，但手部仍可追蹤。
4. 切到 PC 模式，使用 `W/A/S/D`，確認移動方向符合鏡頭前/右方向。
5. 對可抓取物（如 `Cube`）測試：手未靠近時按 Grip 不可抓取。
6. 將手靠近物件後按 Grip，應可抓取；放開後恢復正常掉落/拋擲。

## 參數建議

- `VRGrabber.MaxGrabDistance`：建議從 `10~14` 起調。
- `VRGrabber.GripPressThreshold`：建議 `0.45~0.60`。
- `VRGrabber.GripReleaseThreshold`：建議 `0.15~0.30`。
