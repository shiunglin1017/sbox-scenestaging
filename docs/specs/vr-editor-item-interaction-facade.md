# Spec：Editor 物品互動 Facade（Inspector 集中設定）

## 目的

以**單一 Component** 讓關卡／設計在 Inspector 完成常用 VR 可抓物設定，避免每次手動多元件串接。對應遷移計畫 todo `editor-vr-item-facade`。

## 現況實作（本 repo）

| 項目 | 說明 |
|------|------|
| 元件 | `Code/test/VR/VRItemInteractionProfile.cs` |
| 純規則／可測邏輯 | `Code/VRLogic/VRItemInteractionProfileRules.cs` |
| 與抓取堆疊關係 | 與 `Grabbable`、`Rigidbody`、`VRGrabber` 協調；**Interactor** 仍為 `VRGrabber`（見 `2026-05-05-vr-interaction-stack.md`）。 |

## Inspector 分組（規範）

1. **Grab 點**：多筆條目、優先序、主握點；可選同步至 `Grabbable.GrabPivot`。  
2. **ModelDoc Attachment**：名稱對照（預設對齊 `VrInteractionConstants.DefaultGripAttachmentName`）。  
3. **手姿勢（每點）**：左右手提示欄位（預留／與 `VRHand` 圖參數銜接見 `vr-animgraph-contract.md`）。  
4. **物理預設**：質量級（Light／Medium／Heavy）對應 `AlyxFeelTuningDefaults`；執行期可寫入 `Rigidbody.MassOverride`、可選線／角阻尼；可選 **`SurfaceResourceName`**（`Surface.FindByName` 套用到啟用子階層之 `Collider`）。
5. **Hover 手勢預覽（幾何 fallback）**：僅在無 attachment / pivot 時，使用中心射線距離映射手指 curl（視覺-only）。

## 決策條文（與抓取堆疊同步）

- 抓取姿勢權威順序固定為：**Attachment > GrabPivot > fallback**。  
- `Attachment` 由 `Grabbable.GrabAttachmentName`（預設 `VrInteractionConstants.DefaultGripAttachmentName`）走 `SkinnedModelRenderer` 解析；失敗後才嘗試 `GrabPivot`。  
- `VRItemInteractionProfile` 同步主握點到 `Grabbable.GrabPivot` 僅影響第二層優先序，不覆蓋 Attachment 權威。
- `VRGrabber` 的 hover 預覽可輸出幾何 fallback 手勢（`TryGetHoverPreviewHandPose` / `HoverCurlPreview01`），供 `VRGhostHandTarget` 或手模型參數使用。

## 決策條文（物理預設覆寫）

- `ModelDoc/Prefab` 為質量、阻尼、surface 的預設權威。  
- `VRItemInteractionProfile` 僅在 override 開關啟用時覆寫：`OverrideMass`、`OverrideDamping`、`OverrideSurface`。  
- 若 override 關閉，即使 `ApplyRigidbodyDefaultsOnAwake` 為 true，也必須保留 ModelDoc/Prefab 原值。

## 與其他 spec

- 製作流程總線：`vr-item-weapon-production-workflow.md`  
- 武器分類：`vr-weapon-taxonomy.md`  
- 單元測試範圍：`vr-unit-test-plan.md`
