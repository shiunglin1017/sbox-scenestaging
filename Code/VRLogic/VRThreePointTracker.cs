using System;
using Sandbox;
using Sandbox.VR;

namespace Sandbox;

// ============================================================
//  VRThreePointTracker（v3）
//  VR 三點追蹤控制器
//
//  設計哲學：
//    - IK 解算與物理交由 S&BOX AnimGraph C++ 底層負責
//    - C# 僅讀取 Tracker 數據、推送 AnimGraph 參數、管理
//      Avatar 根物件（Shina）的局部位置與朝向
//    - 不操作 WorldPosition，不覆寫骨骼（無 SetBoneTransform）
//
//  AnimGraph 必要節點對應：
//    CSolveIKChainAnimNode  "head_pos"  ← head_target_pos  (World Space Vector)
//    CTwoBoneIKAnimNode     "head_rot"  ← head_target_pos + head_target_rot  (World Space)
//    CTwoBoneIKAnimNode     "VRhandL"   ← hand_l_pos + hand_l_rot  (World Space)
//    CTwoBoneIKAnimNode     "VRhandR"   ← hand_r_pos + hand_r_rot  (World Space)
//    CBlendAnimNode         "crouch"    ← crouch  Float 0-1  (Phase 2)
// ============================================================

/// <summary>VR 裝置追蹤狀態。</summary>
public enum VRDeviceState
{
	NotConnected,
	Tracking,
	TrackingLost
}

/// <summary>Avatar 身體轉向追隨頭部的策略。</summary>
public enum VRBodyTurnBehavior
{
	Instant,
	Smooth,
	Threshold
}

/// <summary>
/// VR 三點追蹤控制器。
/// 掛載於與 SkinnedModelRenderer 同一 GameObject（Shina），
/// 並在 Inspector 中拖入 HeadTracker（Camera）、LeftHandTracker（VRhandL）、
/// RightHandTracker（VRhandR）的 GameObject 引用。
/// </summary>
public sealed class VRThreePointTracker : Component
{
	// ============================================================
	//  校正資料結構（未來比例映射擴充用）
	// ============================================================

	/// <summary>
	/// VR 校正資料。
	/// 目前記錄站立基準高度，未來可擴充身高比例映射欄位。
	/// </summary>
	public struct CalibrationData
	{
		/// <summary>站立時 HMD 的世界 Z 高度，作為蹲下偵測基準。</summary>
		public float StandingHeadZ;

		/// <summary>Avatar 根物件的地板 Z 高度（備用，未來比例映射用）。</summary>
		public float AvatarBaseZ;

		/// <summary>是否已完成有效校正。</summary>
		public bool IsValid;

		// ── 預留欄位（未來啟用）──────────────────────────────
		// public float PlayerHeight       => StandingHeadZ - AvatarBaseZ;
		// public float AvatarModelHeight;   // 從骨骼計算 Avatar 身高
		// public float HeightScaleFactor;   // AvatarModelHeight / PlayerHeight
	}

	// ============================================================
	//  Scene References
	// ============================================================

	/// <summary>Avatar 的 SkinnedModelRenderer，用於推送 AnimGraph 參數。</summary>
	[Property, Group( "Scene References" )]
	public SkinnedModelRenderer AvatarRenderer { get; set; }

	/// <summary>帶有 VRTrackedObject（Head）的 Camera GameObject。</summary>
	[Property, Group( "Scene References" )]
	public GameObject HeadTracker { get; set; }

	/// <summary>帶有 VRTrackedObject（LeftHand）的 VRhandL GameObject。</summary>
	[Property, Group( "Scene References" )]
	public GameObject LeftHandTracker { get; set; }

	/// <summary>帶有 VRTrackedObject（RightHand）的 VRhandR GameObject。</summary>
	[Property, Group( "Scene References" )]
	public GameObject RightHandTracker { get; set; }

	// ============================================================
	//  Feature Toggles
	// ============================================================

	[Property, Group( "Feature Toggles" )]
	public bool EnableHeadTracking { get; set; } = true;

	[Property, Group( "Feature Toggles" )]
	public bool EnableHandTracking { get; set; } = true;

	/// <summary>啟用後，C# 負責管理 Avatar 根物件的 Yaw 與 XY 位置。</summary>
	[Property, Group( "Feature Toggles" )]
	public bool EnableAvatarRootControl { get; set; } = true;

	[Property, Group( "Feature Toggles" )]
	public bool EnableCrouchDetection { get; set; } = true;

	// ============================================================
	//  Head Settings
	// ============================================================

	/// <summary>
	/// 頭骨旋轉補償（Pitch, Yaw, Roll）。
	/// 補正模型頭骨 bind pose 軸向與 HMD 座標系的差異。
	/// 初次測試保持 Zero，有偏轉再調整。
	/// </summary>
	[Property, Group( "Head Settings" )]
	public Angles HeadRotationOffset { get; set; } = Angles.Zero;

	// ============================================================
	//  Hand Settings
	// ============================================================

	/// <summary>左手旋轉補償，修正控制器 grip pose 軸向差異。</summary>
	[Property, Group( "Hand Settings" )]
	public Angles LeftHandRotationOffset { get; set; } = Angles.Zero;

	/// <summary>右手旋轉補償，修正控制器 grip pose 軸向差異。</summary>
	[Property, Group( "Hand Settings" )]
	public Angles RightHandRotationOffset { get; set; } = Angles.Zero;

	// ============================================================
	//  Body Turn Settings
	// ============================================================

	[Property, Group( "Body Turn Settings" )]
	public VRBodyTurnBehavior TurnMode { get; set; } = VRBodyTurnBehavior.Threshold;

	/// <summary>Threshold 模式：頭身 Yaw 差超過此值才觸發轉身（degrees）。</summary>
	[Property, Group( "Body Turn Settings" ), Range( 10f, 180f )]
	public float BodyTurnThreshold { get; set; } = 60f;

	/// <summary>
	/// 磁滯收斂角度（degrees）。
	/// 觸發轉身後，差值降至此角度才停止，防止閾值邊界處抖動。
	/// </summary>
	[Property, Group( "Body Turn Settings" ), Range( 0f, 60f )]
	public float BodyTurnReleaseAngle { get; set; } = 10f;

	/// <summary>轉身速度（degrees/s）。</summary>
	[Property, Group( "Body Turn Settings" ), Range( 10f, 720f )]
	public float BodyTurnSpeed { get; set; } = 180f;

	// ============================================================
	//  Avatar Follow Settings（XY 死區）
	// ============================================================

	/// <summary>
	/// 啟用後，當 HMD 的 XY 位置偏離 Avatar 根物件超過死區半徑時，
	/// Avatar 跟隨移動（室內追蹤場景用）。
	/// </summary>
	[Property, Group( "Avatar Follow Settings" )]
	public bool EnableXYFollow { get; set; } = true;

	/// <summary>XY 死區半徑（HU）。HMD 在此半徑內漂移時 Avatar 完全不動。</summary>
	[Property, Group( "Avatar Follow Settings" ), Range( 0f, 30f )]
	public float BodyXYDeadzone { get; set; } = 6f;

	/// <summary>超出死區後，Avatar 追向 HMD XY 的速度（HU/s）。</summary>
	[Property, Group( "Avatar Follow Settings" ), Range( 10f, 500f )]
	public float BodyXYFollowSpeed { get; set; } = 120f;

	// ============================================================
	//  Crouch Settings
	// ============================================================

	/// <summary>
	/// HMD 從站立基準下移多少 HU 視為完全蹲下（CrouchRatio = 1）。
	/// 建議值：15~40 HU。值越小，蹲下動作觸發越靈敏。
	/// </summary>
	[Property, Group( "Crouch Settings" ), Range( 1f, 120f )]
	public float CrouchRange { get; set; } = 35f;

	/// <summary>
	/// 站立容忍帶（HU）。HMD 在此距離內的微小下移（呼吸、追蹤飄移）不計入蹲下。
	/// 設為 0 則完全不設死區，任何下移都立即映射至 crouch 比例。
	/// </summary>
	[Property, Group( "Crouch Settings" ), Range( 0f, 20f )]
	public float CrouchTopDeadzone { get; set; } = 0f;

	/// <summary>
	/// 啟用後將 CrouchRatio 直接推送至 AnimGraph "crouch" 參數，
	/// 觸發 idle ↔ crouch 動畫融合（零延遲，直接跟隨 HMD 高度）。
	/// Phase 1 保持 false，三點追蹤穩定後再啟用。
	/// </summary>
	[Property, Group( "Crouch Settings" )]
	public bool EnableCrouchAnimation { get; set; } = false;

	// ============================================================
	//  Debug
	// ============================================================

	/// <summary>在編輯器中顯示 IK 目標點與死區圓（開發用，出版前關閉）。</summary>
	[Property, Group( "Debug" )]
	public bool ShowDebugGizmos { get; set; } = false;

	// ============================================================
	//  公開唯讀狀態
	// ============================================================

	public Vector3  HeadWorldPos       { get; private set; }
	public Rotation HeadWorldRot       { get; private set; }
	public Vector3  LeftHandWorldPos   { get; private set; }
	public Rotation LeftHandWorldRot   { get; private set; }
	public Vector3  RightHandWorldPos  { get; private set; }
	public Rotation RightHandWorldRot  { get; private set; }

	public VRDeviceState HeadState      { get; private set; } = VRDeviceState.NotConnected;
	public VRDeviceState LeftHandState  { get; private set; } = VRDeviceState.NotConnected;
	public VRDeviceState RightHandState { get; private set; } = VRDeviceState.NotConnected;

	/// <summary>蹲下比例：0 = 站立，1 = 完全蹲下。Phase 1 僅作唯讀數據。</summary>
	public float CrouchRatio { get; private set; }

	/// <summary>Avatar 身體目前的 Yaw 角度（PlayerController 局部空間）。</summary>
	public float BodyYaw { get; private set; }

	/// <summary>當前校正資料。IsValid = false 表示尚未完成校正。</summary>
	public CalibrationData Calibration { get; private set; }

	// ============================================================
	//  內部狀態
	// ============================================================

	private float _bodyLocalYaw;
	private bool  _autoCalibrated;
	private bool  _isTurning;      // Threshold 磁滯旗標

	// ============================================================
	//  生命週期
	// ============================================================

	protected override void OnStart()
	{
		WarnMissingReferences();
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsRunningInVR ) return;

		// ── Step 1：快照（本幀所有模組共用同一份 Tracker 數據）
		SnapshotTrackerTransforms();

		// ── Step 2：更新裝置追蹤狀態
		UpdateDeviceStates();

		// ── Step 3：首次自動校正（等 HMD 提供有效高度後觸發）
		if ( !_autoCalibrated && HeadWorldPos.z > 1f )
		{
			ExecuteCalibration();
			_autoCalibrated = true;
		}

		// ── Step 4：Avatar 根物件（Shina）Yaw 與 XY 跟隨
		//    全部在 PlayerController 局部空間（LocalPosition / LocalRotation）操作，
		//    不碰 WorldPosition，避免與 PlayerController 競爭。
		if ( EnableAvatarRootControl && HeadState == VRDeviceState.Tracking )
		{
			UpdateBodyYaw();
			UpdateAvatarRootXY();
		}

		// ── Step 5：頭部 IK 參數 → AnimGraph SolveIK + TwoBoneIK
		if ( EnableHeadTracking && HeadState == VRDeviceState.Tracking )
			SendHeadIKParams();

		// ── Step 6：手部 IK 參數 → AnimGraph TwoBoneIK
		if ( EnableHandTracking )
			SendHandIKParams();

		// ── Step 7：蹲下比例計算（Phase 2 推送至 AnimGraph）
		if ( EnableCrouchDetection && Calibration.IsValid )
			UpdateCrouchRatio();
	}

	// ============================================================
	//  Module 1：Tracker 快照
	// ============================================================

	private void SnapshotTrackerTransforms()
	{
		if ( HeadTracker is not null )
		{
			HeadWorldPos = HeadTracker.WorldPosition;
			HeadWorldRot = HeadTracker.WorldRotation;
		}

		if ( LeftHandTracker is not null )
		{
			LeftHandWorldPos = LeftHandTracker.WorldPosition;
			LeftHandWorldRot = LeftHandTracker.WorldRotation;
		}

		if ( RightHandTracker is not null )
		{
			RightHandWorldPos = RightHandTracker.WorldPosition;
			RightHandWorldRot = RightHandTracker.WorldRotation;
		}
	}

	// ============================================================
	//  Module 2：裝置追蹤狀態
	// ============================================================

	private void UpdateDeviceStates()
	{
		bool vr = Game.IsRunningInVR;

		HeadState = ResolveDeviceState(
			vr && HeadTracker is not null,
			HeadState );

		LeftHandState = ResolveDeviceState(
			vr && Input.VR.LeftHand.Active && LeftHandTracker is not null,
			LeftHandState );

		RightHandState = ResolveDeviceState(
			vr && Input.VR.RightHand.Active && RightHandTracker is not null,
			RightHandState );
	}

	private static VRDeviceState ResolveDeviceState( bool active, VRDeviceState prev )
		=> active                         ? VRDeviceState.Tracking
		:  prev == VRDeviceState.Tracking ? VRDeviceState.TrackingLost
		:                                   VRDeviceState.NotConnected;

	// ============================================================
	//  Module 3A：Body Yaw（Threshold 閾值轉身）
	// ============================================================

	private void UpdateBodyYaw()
	{
		// HeadTracker（Camera）與 AvatarRenderer（Shina）同為 PlayerController 子物件。
		// 在 PlayerController 局部空間比較 Yaw，確保座標系一致。
		float headLocalYaw = HeadTracker.LocalRotation.Yaw();
		float delta        = NormalizeDelta( headLocalYaw - _bodyLocalYaw );

		switch ( TurnMode )
		{
			case VRBodyTurnBehavior.Instant:
				_bodyLocalYaw = headLocalYaw;
				_isTurning    = false;
				break;

			case VRBodyTurnBehavior.Smooth:
				_bodyLocalYaw += FloatSign( delta ) * MathF.Min( MathF.Abs( delta ), BodyTurnSpeed * Time.Delta );
				_bodyLocalYaw  = NormalizeAngle( _bodyLocalYaw );
				_isTurning     = false;
				break;

			case VRBodyTurnBehavior.Threshold:
			default:
				// 磁滯觸發：差值超過 Threshold 才開始轉身
				if ( !_isTurning && MathF.Abs( delta ) > BodyTurnThreshold )
					_isTurning = true;

				if ( _isTurning )
				{
					_bodyLocalYaw += FloatSign( delta ) * MathF.Min( MathF.Abs( delta ), BodyTurnSpeed * Time.Delta );
					_bodyLocalYaw  = NormalizeAngle( _bodyLocalYaw );

					// 磁滯收斂：差值降至 ReleaseAngle 才停止，防止閾值邊界抖動
					if ( MathF.Abs( NormalizeDelta( headLocalYaw - _bodyLocalYaw ) ) <= BodyTurnReleaseAngle )
						_isTurning = false;
				}
				break;
		}

		BodyYaw = _bodyLocalYaw;

		// 套用至 Avatar 根物件的局部旋轉
		if ( AvatarRenderer is not null )
			AvatarRenderer.GameObject.LocalRotation = Rotation.FromYaw( _bodyLocalYaw );
	}

	// ============================================================
	//  Module 3B：XY 死區跟隨
	// ============================================================

	private void UpdateAvatarRootXY()
	{
		if ( !EnableXYFollow || AvatarRenderer is null ) return;

		var avatarObj = AvatarRenderer.GameObject;

		// 在 PlayerController 局部空間（Local）進行比較與移動
		float hx   = HeadTracker.LocalPosition.x;
		float hy   = HeadTracker.LocalPosition.y;
		float bx   = avatarObj.LocalPosition.x;
		float by   = avatarObj.LocalPosition.y;

		float dx   = hx - bx;
		float dy   = hy - by;
		float dist = MathF.Sqrt( dx * dx + dy * dy );

		if ( dist > BodyXYDeadzone && dist > 0.001f )
		{
			float inv  = 1f / dist;
			float move = MathF.Min( dist - BodyXYDeadzone, BodyXYFollowSpeed * Time.Delta );
			bx += dx * inv * move;
			by += dy * inv * move;
		}

		// Z 固定為 0（PlayerController 局部空間地板基準）
		// 高度由 AnimGraph crouch 參數與動畫處理，不透過根物件 Z 調整
		avatarObj.LocalPosition = new Vector3( bx, by, 0f );
	}

	// ============================================================
	//  Module 4：頭部 IK 參數推送
	// ============================================================

	private void SendHeadIKParams()
	{
		if ( AvatarRenderer is null ) return;

		Rotation rot = HeadWorldRot;
		if ( HeadRotationOffset != Angles.Zero )
			rot = rot * Rotation.From( HeadRotationOffset );

		// AnimGraph SolveIK (head_pos chain: Chest→Neck→Head)
		//   ← head_target_pos（World Space）驅動整段頸部鏈位置
		// AnimGraph TwoBoneIK (head_rot chain: Neck→Head)
		//   ← head_target_pos + head_target_rot（World Space）精調頭骨位置與旋轉
		AvatarRenderer.Set( "head_target_pos", HeadWorldPos );
		AvatarRenderer.Set( "head_target_rot", rot );
	}

	// ============================================================
	//  Module 5：手部 IK 參數推送
	// ============================================================

	private void SendHandIKParams()
	{
		if ( LeftHandState  == VRDeviceState.Tracking ) SendSingleHandParams( isLeft: true );
		if ( RightHandState == VRDeviceState.Tracking ) SendSingleHandParams( isLeft: false );
		// TrackingLost → 保持上一幀數值，手臂凍結在最後有效位置
		// 未來可在此加入「平滑移動至身側安全姿態」邏輯
	}

	private void SendSingleHandParams( bool isLeft )
	{
		if ( AvatarRenderer is null ) return;

		Vector3  pos    = isLeft ? LeftHandWorldPos       : RightHandWorldPos;
		Rotation rawRot = isLeft ? LeftHandWorldRot       : RightHandWorldRot;
		Angles   offset = isLeft ? LeftHandRotationOffset : RightHandRotationOffset;

		Rotation rot = offset != Angles.Zero ? rawRot * Rotation.From( offset ) : rawRot;

		if ( isLeft )
		{
			AvatarRenderer.Set( "hand_l_pos", pos );
			AvatarRenderer.Set( "hand_l_rot", rot );
		}
		else
		{
			AvatarRenderer.Set( "hand_r_pos", pos );
			AvatarRenderer.Set( "hand_r_rot", rot );
		}
	}

	// ============================================================
	//  Module 6：蹲下比例計算
	// ============================================================

	private void UpdateCrouchRatio()
	{
		float drop = Calibration.StandingHeadZ - HeadWorldPos.z;

		// 死區以外才開始計入蹲下量（CrouchTopDeadzone = 0 時等同於無死區）
		float effectiveDrop  = MathF.Max( 0f, drop - CrouchTopDeadzone );
		float effectiveRange = MathF.Max( 1f, CrouchRange - CrouchTopDeadzone );

		// 直接比例映射，無任何延遲或平滑——VR 必須零延遲跟隨 HMD 高度
		CrouchRatio = Math.Clamp( effectiveDrop / effectiveRange, 0f, 1f );

		if ( EnableCrouchAnimation && AvatarRenderer is not null )
			AvatarRenderer.Set( "crouch", CrouchRatio );
	}

	// ============================================================
	//  Module 7：校正流程
	// ============================================================

	/// <summary>
	/// 執行校正，記錄站立基準數據。
	/// OnUpdate 中在首次有效 HMD 高度時自動觸發，
	/// 也可在子類或外部邏輯中手動呼叫 Recalibrate() 重觸發。
	/// </summary>
	private void ExecuteCalibration()
	{
		Calibration = new CalibrationData
		{
			StandingHeadZ = HeadWorldPos.z,
			AvatarBaseZ   = AvatarRenderer is not null
				? AvatarRenderer.GameObject.WorldPosition.z
				: 0f,
			IsValid = true,
		};
	}

	/// <summary>
	/// 重新觸發校正（可從 Input 事件、UI 按鈕或 VR 控制器手勢呼叫）。
	/// 下一幀 HMD 高度有效時自動執行新的校正。
	/// </summary>
	public void Recalibrate()
	{
		_autoCalibrated = false;
		Calibration     = default;
	}

	// ============================================================
	//  Module 8：擴充接口預留
	// ============================================================

	// ── 比例映射（未來：玩家身高/臂展 → Avatar 對應縮放）─────────────────
	// public Vector3 ApplyProportionScale( Vector3 worldPos ) => worldPos; // placeholder

	// ── 頭部可見性（未來：一人稱視角隱藏頭骨網格）──────────────────────────
	// [Property, Group("Visibility")] public bool HideHeadMesh { get; set; } = false;
	// private void ApplyHeadVisibility() { ... }

	// ── 額外 Tracker 插槽（未來：腰部/腳部追蹤）─────────────────────────────
	// [Property, Group("Extra Trackers")] public GameObject WaistTracker      { get; set; }
	// [Property, Group("Extra Trackers")] public GameObject LeftFootTracker   { get; set; }
	// [Property, Group("Extra Trackers")] public GameObject RightFootTracker  { get; set; }

	// ─── 移動參數（未來：配合玩家移動系統推送 moveX / moveY）─────────────────
	// private void SendLocomotionParams() { ... }

	// ============================================================
	//  Debug Gizmos（編輯器內視覺化，出版前關閉 ShowDebugGizmos）
	// ============================================================

	protected override void DrawGizmos()
	{
		if ( !ShowDebugGizmos ) return;

		// 頭部 IK 目標點 + 朝向箭頭
		Gizmo.Draw.Color = Color.Cyan;
		Gizmo.Draw.SolidSphere( HeadWorldPos, 2f );
		Gizmo.Draw.Line( HeadWorldPos, HeadWorldPos + HeadWorldRot.Forward * 8f );

		// 左手 IK 目標點
		if ( LeftHandState == VRDeviceState.Tracking )
		{
			Gizmo.Draw.Color = Color.Green;
			Gizmo.Draw.SolidSphere( LeftHandWorldPos, 1.5f );
		}

		// 右手 IK 目標點
		if ( RightHandState == VRDeviceState.Tracking )
		{
			Gizmo.Draw.Color = Color.Blue;
			Gizmo.Draw.SolidSphere( RightHandWorldPos, 1.5f );
		}

		// XY 死區範圍圈（以 Avatar 根物件世界位置為中心）
		if ( EnableXYFollow && AvatarRenderer is not null )
		{
			Gizmo.Draw.Color = Color.Yellow.WithAlpha( 0.5f );
			Gizmo.Draw.LineSphere( AvatarRenderer.GameObject.WorldPosition, BodyXYDeadzone );
		}
	}

	// ============================================================
	//  初始化驗證
	// ============================================================

	private void WarnMissingReferences()
	{
		if ( AvatarRenderer  is null ) Log.Warning( $"[VRThreePointTracker] AvatarRenderer 未設定（{GameObject.Name}）" );
		if ( HeadTracker     is null ) Log.Warning( $"[VRThreePointTracker] HeadTracker 未設定（{GameObject.Name}）" );
		if ( LeftHandTracker is null ) Log.Warning( $"[VRThreePointTracker] LeftHandTracker 未設定（{GameObject.Name}）" );
		if ( RightHandTracker is null ) Log.Warning( $"[VRThreePointTracker] RightHandTracker 未設定（{GameObject.Name}）" );
	}

	// ============================================================
	//  工具函數
	// ============================================================

	/// <summary>將角度差正規化至 (-180, 180] 範圍。</summary>
	private static float NormalizeDelta( float delta )
	{
		while ( delta >  180f ) delta -= 360f;
		while ( delta < -180f ) delta += 360f;
		return delta;
	}

	/// <summary>將角度正規化至 [0, 360) 範圍。</summary>
	private static float NormalizeAngle( float angle )
	{
		while ( angle >= 360f ) angle -= 360f;
		while ( angle <    0f ) angle += 360f;
		return angle;
	}

	private static float FloatSign( float v ) => v > 0f ? 1f : (v < 0f ? -1f : 0f);
}
