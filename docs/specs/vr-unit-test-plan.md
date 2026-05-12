# Spec：VR 相關 Unit 測試計畫

## 原則

- **必測**：可純函式化、無 Scene、無 VR 硬體之邏輯（`dotnet test`）。  
- **文件化排除**：AnimGraph C++ IK、`VRThreePointTracker` 整體整合、`Input.VR` 硬體路徑；以 spec + 手動清單補足。

## CI／本機指令

見 [dotnet-build-test.md](../commands/dotnet-build-test.md)。

## 已覆蓋（現況）

| 區域 | 測試檔 | 說明 |
|------|--------|------|
| 抓取閾值 | `UnitTests/GrabInteractionRulesTests.cs` | `GrabInteractionRules` |
| 位移 wish | `UnitTests/LocomotionWishRulesTests.cs` | `LocomotionWishRules` |
| Socket 規則 | `UnitTests/VRInteractionRulesTests.cs` | `VRInteractionRules` |
| 物品 Profile 規則 | `UnitTests/VRItemInteractionProfileRulesTests.cs` | 質量預設、主握點索引、距離到 curl 映射 |

## 計畫擴充（待實作／待抽離）

| 區域 | 測試對象 | 類型 |
|------|----------|------|
| `XMovement` | wish／摩擦等可抽純函式 | 單元（於 `Libraries/tft.vr.movement` 內抽離後） |
| DI | `NullController`、`CompositeMovementInputSource` 選源 | 單元（需可注入或非靜態 `Game.IsRunningInVR` 之替身策略） |
| Locomotion | 與 `VRPlayerController` 互斥之設定假設 | 文件化場景檢查為主，見 [vr-locomotion-xmovement.md](./vr-locomotion-xmovement.md) |
| 射擊雙模式 | `Trace`/`Projectile` 共用 impact 規則 | 先整合測試（場景）；可抽純函式後補單元 |
| Cloud prop 適配 | `AutoPropAdapterSystem` 過濾條件與覆寫策略 | 先整合測試（場景）；可抽規則後補單元 |

## 不可單測範圍（須 spec／手動）

- **AnimGraph C++**：IK 解算在引擎內；C# 單測僅能覆蓋「參數推導與命名常量」，見 [vr-animgraph-contract.md](./vr-animgraph-contract.md)。  
- **`VRThreePointTracker`**：依賴 Tracker 與 `SkinnedModelRenderer.Set`；以手動 VR + Gizmo + `vr-three-point-tracker-architecture.md` 驗收。  
- **硬體**：實機 HMD／控制器行為。

## SDD 對齊

本計畫與 [2026-05-05-vr-interaction-stack.md](./2026-05-05-vr-interaction-stack.md)、[ENGINEERING_WORKFLOW.md](../ENGINEERING_WORKFLOW.md) 一致；程式變更應可指回對應 spec 條目。
