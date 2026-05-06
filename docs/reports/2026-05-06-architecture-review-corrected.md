# 每日架構審查報告（修正版）— 2026-05-06

## CI/CD 狀態

- `NETSDK1045` 屬於環境 SDK 版本不匹配（本機 `.NET 8` 嘗試建置 `net10.0` 目標），不是業務程式碼語法錯誤。
- 本倉庫已存在 GitLab CI 設定：`.gitlab-ci.yml` 使用 `mcr.microsoft.com/dotnet/sdk:10.0` 執行 `dotnet test`。

## 架構核對結論

- `VRGrabber` 抓取流程使用 `FixedJoint`，非手抓取時 `SetParent`，符合互動層設計。
- `VRSocket.ParentToSocket` 使用 `SetParent` 屬插槽鎖定例外，與手抓取規則不衝突。
- `VRGhostHandTarget` 為純 Transform 追蹤，無剛體/碰撞體，職責分離正確。

## 已確認問題與修正

### 問題：`VRRootColliderStabilizer` 角速度上限語意不精準

原先公式將 `MaxDegreesPerSecond` 當作 `Slerp(t)` 的縮放因子，無法直接對應「每秒最大旋轉角度」。

### 修正方式

- 保留 `Slerp` 平滑，但先計算目前與目標前向夾角（度）。
- 以 `maxStepDeg = MaxDegreesPerSecond * Time.Delta` 限制本幀最大角度。
- 將本幀插值係數 `t` 截斷為 `min(baseT, maxStepDeg / angleDeg)`，再 clamp 到 `[0,1]`。

此修正讓 `MaxDegreesPerSecond` 具備可預期上限語意，降低翻腕時 root collider 過快旋轉風險。

## 建議後續驗證

1. 在 `test.vr.scene` 快速甩手測試，觀察 root collider 是否仍穩定貼近手部且不過衝。
2. 比較 `MaxDegreesPerSecond = 180 / 360 / 540` 三組手感，確認上限參數可調且有效。
3. 若後續需要嚴格 full-rotation 限制（含 roll），可再改成四元數角距離計算版本。
