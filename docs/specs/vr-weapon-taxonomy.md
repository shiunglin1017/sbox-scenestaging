# Spec：VR 武器／互動物四分類（Taxonomy）

## 用途

關卡、美術與程式共用之**最小欄位表**；與 `VRGrabber`／`VRSocket`／`Grabbable`／`VRItemInteractionProfile` 對照。製作流程見 [vr-item-weapon-production-workflow.md](./vr-item-weapon-production-workflow.md)。

## 分類

| 類型 | 英文鍵 | 必備／建議欄位 | 與本專案元件 |
|------|--------|----------------|--------------|
| **槍械** | `Firearm` | 彈藥、換彈模式（timer／實體彈匣）、瞄準、射擊（trace／projectile）；Muzzle attachment | `TestWeapon` 或自訂武器 Component + `Rigidbody` + `Grabbable` |
| **單手近戰** | `OneHandedMelee` | 單一主握點、質量預設、揮擊距離／傷害（遊戲層） | 單手 `VRGrabber` + `VRItemInteractionProfile` |
| **雙手** | `TwoHanded` | 主／副握點；副手引導（`VRGhostHandTarget` 或第二 attachment）；見 `AlyxFeelTuningDefaults.TwoHandedNote` | 雙手各一 `VRGrabber`；副手對齊幽靈或輔助 pivot |
| **投擲物** | `Throwable` | 放手速度、`VRGrabber` 投擲估算欄位、可選引信 | `Grabbable` + `Rigidbody`；釋放速度見 `VRGrabber` |

## 與 Socket

- **Firearm**：彈匣 Socket 使用 `VRSocket` + `Socketable`，`AcceptId` 與物品 `SocketId` 一致。  
- **Throwable**：通常無長期 Socket；可選「插銷」為遊戲層狀態機。

## 擴充

新增子類型時應同步更新 [vr-item-weapon-production-workflow.md](./vr-item-weapon-production-workflow.md) 檢核表與 `PROJECT_ARCHITECTURE_OVERVIEW.md` 索引句。
