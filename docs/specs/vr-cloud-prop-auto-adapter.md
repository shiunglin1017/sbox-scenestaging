# Spec：Cloud Prop 自動適配（零／低設定抓取）

## 目的

讓雲端資產（常見 `prop`）在不額外手動掛大量元件的情況下，能被 `VRGrabber` 以最小組件集合抓取與互動。

## 元件

- `Code/test/VR/AutoPropAdapterSystem.cs`

## 最小補齊策略

當候選物件符合掃描條件（預設 `prop` tag）時，自動補齊：

1. `Rigidbody`（若缺少）
2. `Collider`（若缺少，先補 `BoxCollider`）
3. `Grabbable`（若缺少）

> 不預設覆寫質量／阻尼／surface，維持 ModelDoc/Prefab 為物理權威。

## 效能策略

- 以 `ModelRenderer` 清單作為候選來源。
- 使用增量掃描與每幀處理上限（`MaxProcessPerTick`），避免尖峰。
- 支援排除 tag（`ExcludedTagsCsv`）避免誤抓玩家或固定道具。

## 風險控制

- **誤抓取**：透過 `RequirePropTag` + `ExcludedTagsCsv` 共同控制。
- **碰撞箱不準**：`BoxCollider` 僅作零設定保底，必要時由關卡後續手調或改用資產端 collider。

## 驗收

- 拖入 cloud prop（`prop` tag）後，不手動加元件亦可被 `VRGrabber` 偵測並抓取。
- 不影響既有手動精調 prefab（不覆寫其物理權威）。
