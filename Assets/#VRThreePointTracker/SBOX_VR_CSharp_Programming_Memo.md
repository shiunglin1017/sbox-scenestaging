# S&BOX VR C# 編程備忘錄

> **用途**：供 Cursor AI 進行 C# 編程時的進度彙整與注意事項參考  
> **引擎**：S&BOX（基於 Source 2 引擎）  
> **最後更新**：2026-05-19

---

## 目錄

1. [專案場景階層結構](#1-專案場景階層結構)
2. [VR 三點追蹤與比例映射系統](#2-vr-三點追蹤與比例映射系統)
3. [S&BOX 原生 VR Component 的支援度與衝突問題](#3-sbox-原生-vr-component-的支援度與衝突問題)
4. [VR 身體碰撞系統](#4-vr-身體碰撞系統)
5. [S&BOX 碰撞相關 Component 的支援度與衝突問題](#5-sbox-碰撞相關-component-的支援度與衝突問題)
6. [IK 解算修正](#6-ik-解算修正)
7. [未來預定功能規劃](#7-未來預定功能規劃)
8. [關鍵 API 速查表](#8-關鍵-api-速查表)
9. [已知陷阱與踩坑記錄](#9-已知陷阱與踩坑記錄)

---

## 1. 專案場景階層結構

```
Player Controller                    ← VRPlayerCalibration
├── Shina（Avatar）                  ← SkinnedModelRenderer
│   ├── VRThreePointTracker          ← 三點追蹤 + AnimGraph IK 推送
│   ├── VRAvatarProportionBinding    ← 比例映射計算 + VRTrackingRoot.LocalScale 寫入
│   ├── VRTPoseCalibrator            ← T-Pose 臂展校正（備選版本）
│   └── VRAvatarBoneCollider         ← 身體碰撞系統（待實作）
└── VR_Tracking_Root                 ← LocalScale 由 VRAvatarProportionBinding 驅動
    ├── Camera                       ← VRScaledTrackedObject (Head)
    ├── VRhandL                      ← VRScaledTrackedObject (LeftHand)
    └── VRhandR                      ← VRScaledTrackedObject (RightHand)
```

---

## 2. VR 三點追蹤與比例映射系統

### 2.1 核心流程

```
玩家站直 → 雙手 Grip 長按觸發校正
  → VRPlayerCalibration 取樣 HMD 高度（Input.VR.Head.Position.z − FloorRoot.WorldPosition.z）
  → VRAvatarProportionBinding 快照 Avatar 眼高（ModelDoc Attachment vr_eyes − vr_floor）
  → 計算 ScaleFactor = AvatarEyeH / PlayerEyeH
  → 寫入 VRTrackingRoot.LocalScale = Vector3.One * ScaleFactor
  → VRScaledTrackedObject 讀取 Input.VR 原始姿態 → 以 ScaleFactor 縮放偏移 → 寫入 WorldPosition
```

### 2.2 VRScaledTrackedObject（自製，取代官方 VRTrackedObject）

**職責**：讀取 `Input.VR` 的原始追蹤資料，套用比例縮放後寫入 GameObject.Transform。

**比例縮放公式**：
```csharp
Vector3 scaledWorldPos = pivot + (rawPose.Position - pivot) * scale;
```
- `pivot`：縮放樞紐點（預設為 PlayerController 的 WorldPosition）
- `scale`：從父物件 `VRTrackingRoot.WorldScale.x` 讀取，或使用手動值
- 旋轉不縮放，直接寫入

**關鍵設計決策**：
- 必須使用 `Input.VR.Head.Position`（原始追蹤）取樣玩家高度，不能用場景中被縮放後的 HeadTracker.WorldPosition，否則會產生回授震盪
- `OnUpdate` + `OnPreRender` 雙重更新，避免邏輯幀與渲染幀位置不一致

### 2.3 VRPlayerCalibration

**職責**：記錄玩家現實身體資料，跨 Avatar 替換持久保留。

**校正資料**：
- `PlayerTargetEyeHeight`：站立時 HMD 相對地板的高度（HU）
- `PlayerArmSpan`（T-Pose 版本）：左右手 Tracker 的距離（HU）
- `CalibrationVersion` / `PlayerCalibrationRevision`：每次校正遞增，供下游元件做邊緣觸發

**校正觸發**：雙手 Grip 長按 2 秒

### 2.4 VRAvatarProportionBinding

**職責**：讀取 Avatar 骨骼尺寸 + 玩家資料，計算 ScaleFactor 並寫入 VRTrackingRoot.LocalScale。

**Prefab 眼高快照（僅執行一次）**：
1. 優先讀 ModelDoc Attachment（`vr_eyes` / `vr_floor`）
2. Fallback：Head 骨 + EyeOffsetLocalFromHeadBone
3. 逾時：使用 PrefabEyeHeightFallback 常數
4. 可選：ManualPrefabEyeHeightHu 手動覆蓋

**快照保護**：讀到的值只存一次，避免每幀讀 IK 後骨骼導致分母隨動畫漂移。Inspector 參數變更時自動 Invalidate 重新快照。

**驗證機制**：`RejectOutOfRangeSamples` 啟用時，attachment/bone 量測值落在不合理範圍外會被拒絕。

### 2.5 VRThreePointTracker

**職責**：每幀讀取 Tracker 座標、推送 IK 參數到 AnimGraph。

**執行順序**：
1. `SnapshotTrackerTransforms()` — 快照 Head/LeftHand/RightHand 的 WorldPosition/WorldRotation
2. `UpdateAvatarRootXY()` — Avatar 根物件水平跟隨 HMD
3. `UpdateCrouchRatio()` — 蹲下比例計算
4. `SendHeadIkParams()` / `SendSingleHandParams()` — 推送 AnimGraph 參數

**HMD 視角修正（已完成）**：
- 計算 `vr_eyes` Attachment 相對 Head 骨的局部偏移
- IK 目標 = HMD 世界座標 − HeadWorldRot × eyeOffsetLocal
- 效果：Head 骨被拉到正確位置，Avatar 雙眼之間對齊 HMD

**Hand Bone 偏移修正（已完成）**：
- `LeftHandIkPositionOffset` / `RightHandIkPositionOffset`：控制器追蹤點到 Hand 骨的位置偏移
- `LeftHandRotationOffset` / `RightHandRotationOffset`：旋轉偏移

---

## 3. S&BOX 原生 VR Component 的支援度與衝突問題

### 3.1 VRTrackedObject（官方元件）— ⛔ 不可用

**問題**：每幀由 C++ 層直接覆寫 GameObject 的 WorldPosition，無法從 C# 介入控制。寫入的是原始物理追蹤座標，不受 `VRTrackingRoot.LocalScale` 或 `Input.VR.Scale` 影響。

**證據**：
- 開關 `InvertAnchorScale`（Scale 0.912 vs 2.857）在 VR 中感覺不到任何差異
- 程式碼注釋記錄：「VRAnchor 在實測中不會把 LocalScale 套到 playspace」
- `WorldPosition.z` 對任何 LocalScale 設定都是物理上穩定不變的值

**解決方案**：已自製 `VRScaledTrackedObject` 完全取代，從 `Input.VR` 讀取原始資料後手動套用比例縮放再寫入 Transform。

### 3.2 Input.VR.Scale（官方 API）— ⛔ 不生效

**問題**：`VRInput.Scale` 屬性可讀寫不拋異常，但寫入後 VRTrackedObject 的追蹤輸出不受影響。可能屬於舊版 Entity System 時代的 API，Scene System 中未更新。

**證據**：寫入極端值（例如 10f）在 VR 中感覺不到任何差異。

**解決方案**：不使用此 API，改用 `VRTrackingRoot.LocalScale` + 自製 `VRScaledTrackedObject` 的手動縮放方案。注意：`VRTrackingRoot.LocalScale` 必須搭配自製的 `VRScaledTrackedObject` 才能生效——若場景中仍使用官方 `VRTrackedObject`，LocalScale 同樣不會影響追蹤輸出。

### 3.3 VRAnchor（官方元件）— ⚠️ 功能有限

**問題**：將玩家遊玩空間綁定到 GameObject Transform，但不讀取 LocalScale，無法透過它做比例映射。

**目前狀態**：仍可用於綁定遊玩空間的基本位置。

### 3.4 VR Hand（官方元件）— ✅ 正常

SteamVR 骨骼輸入對應到手部模型的 animgraph。

---

## 4. VR 身體碰撞系統

### 4.1 設計方案：自建 GameObject + 手動 Collider 跟隨骨骼

**原因**：S&BOX 的 `ModelPhysics` 和 `ModelCollider` 在 VR 追蹤環境下都無法正常使用（詳見第 5 節）。

**架構**：新建獨立 Component `VRAvatarBoneCollider`，掛在 Avatar 上，OnStart 時動態建立 GameObject + Collider，OnUpdate 每幀同步骨骼位置。

**碰撞骨骼建議配置**：

| 骨骼 | 形狀 | 半徑 | 高度 | 模式 | 用途 |
|------|------|------|------|------|------|
| Head | Sphere | 5 | — | Trigger | 互動偵測 |
| Spine2 | Capsule | 7 | 12 | Trigger | 身體碰撞偵測 |
| UpperArm_L/R | Capsule | 3 | 10 | Trigger | 手臂碰撞偵測 |
| Forearm_L/R | Capsule | 2.5 | 8 | Trigger | 前臂碰撞偵測 |
| Hand_L/R | Sphere | 3 | — | Kinematic | 推動場景物件 |

**碰撞體類型**：
- **Trigger**（`IsTrigger = true`）：偵測碰撞事件但不阻擋物件，用於身體/頭部
- **Kinematic**（`Rigidbody.PhysicsType = Keyframed`）：可推動 Dynamic 物件但自身不受物理力，用於手部

**Collision Tag 設定**：
- 所有骨骼碰撞體使用 Tag `avatarbody`
- 在 Project Settings → Collision Matrix 中設定 `avatarbody` 不與 `player`（CharacterController）碰撞
- `avatarbody` 與 `solid`（牆壁）和 `prop`（可推動物件）保持碰撞

### 4.2 手部碰撞阻擋（方式 A：Half-Life Alyx 風格）

**原理**：每幀移動碰撞體之前，用 `Scene.Trace.Sphere` 從當前位置掃掠到目標位置。碰到 `solid` 物件時停在碰撞點，碰到 `prop` 物件時正常通過。

```csharp
var trace = Scene.Trace
    .Sphere( entry.Radius, currentPos, targetPos )
    .WithTag( "solid" )
    .WithoutTags( CollisionTag )
    .Run();

if ( trace.Hit )
    obj.WorldPosition = trace.EndPosition;  // 停在碰撞點
else
    obj.WorldPosition = targetPos;          // 正常跟隨
```

**行為**：
- 手碰牆壁/固定物件 → 虛擬手停住，真實手繼續移動（手和控制器分離）
- 手碰可推動物件 → Keyframed 碰撞體正常推動物件
- 手碰自己身體 → 忽略（Tag 排除）

### 4.3 從 ModelDoc 讀取碰撞形狀（替代手動輸入數值）

**API 路徑**：
```
Model.Physics.Parts → List<PhysicsPart>
  ├── Hulls → Bounds（BBox，Mins/Maxs）
  ├── Spheres
  └── Capsules
```

**用途**：讀取 ModelDoc 中已定義的 PhysicsShape 幾何資料（位置、大小、類型），自動建立對應的 Collider，取代在 Inspector 中手動輸入數值。需先用測試程式碼探查 API 完整結構，確認是否包含骨骼綁定資訊。

**測試程式碼**：
```csharp
var model = AvatarRenderer.Model;
foreach ( var part in model.Physics.Parts )
{
    Log.Info( $"Hulls={part.Hulls?.Count} Spheres={part.Spheres?.Count} Capsules={part.Capsules?.Count}" );
    foreach ( var hull in part.Hulls ?? Enumerable.Empty<...>() )
        Log.Info( $"  Hull Bounds: {hull.Bounds}" );
}
```

---

## 5. S&BOX 碰撞相關 Component 的支援度與衝突問題

### 5.1 ModelPhysics — ⛔ VR 環境下不可用

**問題**：為每根骨骼建立獨立的 Dynamic PhysicsBody。和 VR 追蹤程式碼每幀覆寫 Transform 產生根本衝突。

**已驗證的症狀**：
- `Renderer = SkinnedModelRenderer`：Avatar 變布娃娃癱倒
- `Renderer = None, Motion Enabled = true`：Avatar 被彈飛
- `Renderer = None, Motion Enabled = false`：Avatar 仍被彈飛
- 關閉 ModelDoc 中所有 PhysicsShape（紅色禁止圖標）：Avatar 仍被彈飛
- **非 VR 模式下完全正常**：碰撞正常運作，無任何異常

**根因**：`VRThreePointTracker.UpdateAvatarRootXY()` 每幀覆寫 `avatarObj.LocalPosition`，ModelPhysics 建立的 PhysicsBody 偵測到瞬移後和場景碰撞體（CharacterController 膠囊、場景 Cube）發生穿透碰撞，產生巨大分離力。

**結論**：VR Avatar 不應掛載 ModelPhysics。ModelDoc 中的 PhysicsShape/Joint 保留用於未來布娃娃效果（角色死亡等），平時不啟用。

### 5.2 ModelCollider — ⛔ VR 角色不可用

**問題**：將 ModelDoc 的所有 PhysicsShape 合成一個整體碰撞體（剛體），形狀固定在初始姿態（T-Pose），不會跟隨骨骼動畫。

**症狀**：Avatar 手臂彎曲但碰撞體保持 T-Pose，手部碰撞位置和實際手臂位置完全不符。

**適用場景**：靜態物件（桌子、牆壁）或不變形的動態物件（箱子、杯子），搭配 Rigidbody 使用。

### 5.3 CharacterController — ✅ VR 環境可用

**用途**：角色移動碰撞（防穿牆、走斜坡、上台階）。

**注意**：需在 Collision Matrix 中排除與 `avatarbody` Tag 的碰撞，避免和骨骼碰撞體衝突。

### 5.4 BoxCollider / SphereCollider / CapsuleCollider — ✅ 推薦用於 VR

**用途**：手動建立的簡單碰撞體，掛在自建的 GameObject 上跟隨骨骼。不依賴 ModelDoc。

**搭配 Rigidbody (Keyframed)**：可推動 Dynamic 物件。搭配 `IsTrigger = true`：僅偵測碰撞事件。

---

## 6. IK 解算修正

### 6.1 AnimGraph TwoBoneIK 的限制

S&BOX 的 AnimGraph TwoBoneIK 不支援 Pole Target（極點目標），肘/膝彎曲方向由鏈中第一根骨骼的初始朝向決定。社群已提交 Feature Request（Issue #6084）。

**影響**：手臂 IK 解算時肘關節可能彎向錯誤方向，或穿進身體。

### 6.2 可行的修正路線

**路線 A：AnimGraph 參數間接控制**
- 在 AnimGraph 中建立額外的 IK/Blend 節點
- 從 C# 推送 elbow hint 參數

**路線 B：Post-IK 骨骼覆寫**
- 用 `SetBoneTransform` 或 `CreateBoneObjects` 在 AnimGraph 之後覆寫骨骼
- 需實測確認時序：AnimGraph 是否會再次覆蓋 C# 的寫入
- 測試方法：強制旋轉 UpperArm_L 45 度，觀察是否穩定生效

**路線 C：IK 目標座標預處理（不改 Hand 座標時不適用）**
- 在送進 AnimGraph 前修正 Hand 目標座標
- 適用於防止手穿進身體的場景
- 不適用於「Hand 不動、只改手臂旋轉」的需求

**路線 D：C# 自製 TwoBoneIK + Pole Target**
- 完全繞過 AnimGraph IK
- 用 C# 計算 UpperArm/Forearm 旋轉
- 透過 `SetBoneTransform` 或 `CreateBoneObjects` 寫入

### 6.3 `SetBoneTransform` 時序驗證

```csharp
// 測試程式碼：確認 Post-IK 覆寫是否生效
if ( AvatarRenderer.TryGetBoneTransform( "UpperArm_L", out Transform currentTx ) )
{
    Rotation testRotation = currentTx.Rotation * Rotation.FromAxis( Vector3.Up, 45f );
    Transform newTx = currentTx.WithRotation( testRotation );
    var bone = AvatarRenderer.Model.Bones.GetBone( "UpperArm_L" );
    AvatarRenderer.SetBoneTransform( ref bone, newTx );
}
```

**可能結果**：
- A) 穩定旋轉 → Post-IK 覆寫可行
- B) 無變化 → AnimGraph 覆蓋了寫入，需嘗試 `CreateBoneObjects`
- C) 閃爍抖動 → 時序衝突，需尋找 Post-AnimGraph 回調

---

## 7. 未來預定功能規劃

### 7.1 Avatar 臉部物件隱藏功能

**目的**：VR 玩家第一人稱視角下，Avatar 的臉部模型（臉、頭髮前部）會擋住視線，需要隱藏。

**預計做法**：
- 使用 ModelDoc 的 Body Groups 功能，為臉部建立獨立的 Body Group
- 在 C# 中根據是否為本機玩家動態切換 Body Group 的可見性
- 或使用 material override 將臉部材質設為透明

**注意事項**：
- 只對本機玩家隱藏，其他玩家看到的 Avatar 應保持完整
- 需確認 `SkinnedModelRenderer.SetBodyGroup()` API 是否可用

### 7.2 Avatar 手勢對應功能

**目的**：將 VR 控制器的按鍵/手指追蹤映射到 Avatar 的手部動畫。

**預計做法**：
- 讀取 `Input.VR.LeftHand.Grip`、`Trigger`、`Joystick` 等數值
- 如有手指追蹤（Meta Quest、Valve Index）：讀取 `Input.VR.LeftHand.FingerCurl` 等
- 映射到 AnimGraph 的手勢參數（例如 `hand_grip_l`, `hand_point_l`, `hand_fist_l`）
- 或透過 AnimGraph Blend 節點混合預設的手勢動畫

**注意事項**：
- 需在 AnimGraph 中為每種手勢建立對應的動畫狀態或 Blend 節點
- Grip/Trigger 的值域（0~1）可直接作為 Blend 權重
- 不同控制器的按鍵佈局不同，需做適配層

### 7.3 VR 身體碰撞功能（待實作）

**狀態**：方案已規劃完成，待編碼。

**實作清單**：
1. 建立 `VRAvatarBoneCollider` Component
2. 定義 `BoneColliderEntry` 結構體（骨骼名、形狀、半徑、模式、偏移）
3. OnStart：為每根骨骼建立 GameObject + Collider（+ Rigidbody Keyframed for 手部）
4. OnUpdate：`TryGetBoneTransform` → 同步 WorldPosition/WorldRotation
5. 手部碰撞阻擋：`Scene.Trace.Sphere` 掃掠檢測 → 碰到 solid 停住
6. 設定 Collision Tag `avatarbody` + Collision Matrix 排除 CharacterController
7. （可選）從 `Model.Physics.Parts` 讀取 ModelDoc 碰撞形狀自動建立
8. （可選）DrawGizmos 視覺化碰撞體線框

---

## 8. 關鍵 API 速查表

### VR 追蹤
```csharp
Input.VR.Head.Position          // HMD 原始世界座標（公尺）
Input.VR.Head.Rotation          // HMD 原始旋轉
Input.VR.LeftHand.Transform     // 左手 Grip Pose
Input.VR.LeftHand.AimTransform  // 左手 Aim Pose
Input.VR.LeftHand.Grip.Value    // Grip 按壓值 0~1
Input.VR.LeftHand.Trigger.Value // Trigger 按壓值 0~1
Input.VR.LeftHand.Active        // 控制器是否啟用
Input.VR.LeftHand.TriggerHaptics( HapticEffect.SoftImpact )  // 震動回饋
Game.IsRunningInVR              // 是否在 VR 模式
```

### 骨骼操作
```csharp
renderer.TryGetBoneTransform( "BoneName", out Transform tx )  // 讀取骨骼世界 Transform
renderer.GetAttachment( "AttachmentName", worldSpace: true )   // 讀取 ModelDoc Attachment
renderer.SetBoneTransform( ref bone, newTransform )            // 寫入骨骼 Transform（需驗證時序）
renderer.CreateBoneObjects = true                               // 啟用骨骼 GameObject
renderer.GetBoneObject( "BoneName" )                            // 取得骨骼 GameObject
renderer.Set( "param_name", value )                             // 推送 AnimGraph 參數
renderer.TryGetBoneTransformAnimation( "BoneName", out tx )    // 讀取動畫層骨骼（IK 之前）
```

### 物理碰撞
```csharp
Scene.Trace.Sphere( radius, from, to )     // 球形掃掠偵測
    .WithTag( "solid" )                    // 只命中特定 Tag
    .WithoutTags( "avatarbody" )           // 排除特定 Tag
    .Run()                                 // 執行，返回 SceneTraceResult

// SceneTraceResult
trace.Hit           // 是否命中
trace.EndPosition   // 碰撞點世界座標
trace.Normal        // 碰撞法線
trace.GameObject    // 被命中的 GameObject
```

### 模型物理資料讀取
```csharp
var model = renderer.Model;
model.Physics.Parts                  // 物理部件列表
part.Hulls                           // 凸包碰撞殼列表
hull.Bounds                          // BBox（Mins, Maxs）
```

---

## 9. 已知陷阱與踩坑記錄

### 9.1 座標系統

- S&BOX 使用 **Z 軸為垂直軸**（向上為正），不是 Y 軸
- `WorldPosition.z` = 高度，`WorldPosition.x/y` = 水平面
- ModelDoc Attachment 的 `GetAttachment(worldSpace: true)` 回傳值單位是**公尺**，不是 HU
- 變數命名中「HU」可能實際存的是公尺值，需注意歷史遺留的命名混亂

### 9.2 LocalPosition vs WorldPosition

- `VRTrackedObject` 覆寫的是 `WorldPosition`，不是 `LocalPosition`
- `LocalPosition` 是由 `WorldPosition` 反推回本地空間的結果，會被父物件 `LocalScale` 反向放大
- **不可用 `LocalPosition` 做「未縮放眼高」的取樣**，必須用 `WorldPosition` 或 `Input.VR.Head.Position`

### 9.3 比例映射：VR.Scale 不可用，必須用自製方案

- **`Input.VR.Scale`（官方 API）已確認不生效**：寫入任何值（包括極端值 10f）在 VR 中感覺不到任何差異。原因是官方 `VRTrackedObject`（C++ 原生元件）在每幀最後階段會以 OpenXR 原始追蹤座標直接覆寫 GameObject 的 WorldPosition，忽略所有 C# 層的 Scale 設定
- **`VRTrackingRoot.LocalScale` 搭配官方 `VRTrackedObject` 也不生效**：同樣的原因——`VRTrackedObject` 覆寫的是 WorldPosition，父物件的 LocalScale 不會影響覆寫結果
- **唯一可行的方案**：移除官方 `VRTrackedObject`，使用自製的 `VRScaledTrackedObject`。此元件從 `Input.VR` 讀取原始追蹤資料，在 C# 中手動套用比例縮放後再寫入 WorldPosition。搭配 `VRTrackingRoot.LocalScale` 作為比例因子來源
- `ScaleFactor = AvatarEyeH / PlayerEyeH`：Avatar 比玩家矮 → s < 1 → 縮小追蹤空間偏移量
- 舊版程式碼中的 `InvertAnchorScale` 旗標已無意義（因為不再使用 `Input.VR.Scale`），可移除

### 9.4 Avatar 眼高快照

- **必須只快照一次**，不能每幀讀取骨骼位置計算眼高
- 每幀讀取會因為 IK 動畫導致分母漂移，ScaleFactor 不穩定
- Inspector 參數變更時自動 Invalidate 重新快照

### 9.5 回授震盪防護

- 校正取樣必須用 `Input.VR.Head.Position`（原始追蹤），不能用場景中的 HeadTracker.WorldPosition
- 場景中的 HeadTracker 已被 VRScaledTrackedObject 縮放過，用它反算縮放會形成 52 ↔ 20 的震盪

### 9.6 ModelPhysics + VR 追蹤 = 彈飛

- 任何每幀覆寫 Transform 的程式碼（UpdateAvatarRootXY、VRScaledTrackedObject）都和 ModelPhysics 衝突
- ModelPhysics 只要掛載就可能建立 PhysicsBody，即使 ModelDoc PhysicsShape 全部關閉
- 非 VR 模式不會觸發（因為追蹤程式碼在非 VR 下不執行）

### 9.7 Jiggle Bone 限制

- S&BOX 的 AnimGraph Jiggle Bone 沒有內建的碰撞球防穿模機制
- 頭髮/衣物穿進身體的問題需要自行在 C# 中實作碰撞偵測和骨骼位置修正
- 不要混淆 VRChat 的 PhysBone Collider 系統——S&BOX 沒有等效功能

---

> **編輯提示**：本文件應隨開發進度持續更新。每次解決新問題或發現新陷阱時，請更新對應章節。
