using System;
using System.Text.Json;
using Sandbox;
using Sandbox.VR;

namespace Sandbox;

/// <summary>
/// 掛載於 PlayerController 根物件。持久記錄玩家「目標眼高」（相對地板垂直軸），
/// 供 <see cref="VRAvatarProportionBinding"/> 計算 <c>s = H_avatar_prefab / H_player</c> 並設定 VRTrackingRoot 縮放；
/// 雙手 Grip 長按觸發：將當下 HMD 相對地板高度寫入並標記已校正。
/// </summary>
public sealed class VRPlayerCalibration : Component
{
	private const string DefaultSaveRelativePath = "vr_player_eye_calibration.json";

	// ============================================================
	//  Scene References
	// ============================================================

	/// <summary>可選：帶有追蹤元件的 Camera（僅作除錯參考，不作主要校正來源）。</summary>
	[Property, Group( "Scene References" )]
	public GameObject HeadTracker { get; set; }

	// ============================================================
	//  Calibration
	// ============================================================

	/// <summary>
	/// 玩家目標眼高（HU）：站立時視點相對 <see cref="FloorRoot"/> 世界 Z 的差值。
	/// 可手動填寫；Grip 長按完成後會覆寫為當下量測值。
	/// </summary>
	[Property, Group( "Calibration" ), Range( 40f, 300f )]
	public float PlayerTargetEyeHeight { get; set; } = 145f;

	/// <summary>若 true，啟動時嘗試從磁碟載入（失敗則忽略）。</summary>
	[Property, Group( "Calibration" )]
	public bool AutoLoadOnStart { get; set; }

	/// <summary>相對於 FileSystem.Data 的存檔路徑。</summary>
	[Property, Group( "Calibration" )]
	public string SaveRelativePath { get; set; } = DefaultSaveRelativePath;

	// ============================================================
	//  Grip hold
	// ============================================================

	[Property, Group( "Grip Calibration" ), Range( 0.3f, 5f )]
	public float GripHoldDurationSeconds { get; set; } = 2f;

	[Property, Group( "Grip Calibration" ), Range( 0.2f, 1f )]
	public float GripPressThreshold { get; set; } = 0.65f;

	[Property, Group( "Grip Calibration" )]
	public bool EnableGripCalibration { get; set; } = true;

	// ============================================================
	//  State (read-only)
	// ============================================================

	/// <summary>是否已完成玩家眼高校正（手動或 Grip）。可在 Inspector 先手動勾選以跳過首次 Grip。</summary>
	[Property, Group( "Calibration" )]
	public bool IsPlayerCalibrated { get; set; }

	/// <summary>雙手 Grip 已持續按住時間（秒）。</summary>
	public float GripHoldProgressSeconds { get; private set; }

	/// <summary>
	/// 校正資料版本號：每次成功寫入 <see cref="PlayerTargetEyeHeight"/>／<see cref="IsPlayerCalibrated"/> 時 +1。
	/// 其他組件（如 <see cref="VRThreePointTracker"/>）可比對此值決定是否重設與眼高相依的派生基準（例如站立 LocalZ）。
	/// </summary>
	public int CalibrationVersion { get; private set; }

	// ============================================================
	//  Internal
	// ============================================================

	private float _gripHoldTimer;
	private float _gripCooldown;

	/// <summary>地板原點：使用本元件所在 GameObject 的世界座標（與 VR 追蹤根對齊）。</summary>
	public GameObject FloorRoot => GameObject;

	protected override void OnStart()
	{
		if ( AutoLoadOnStart )
			TryLoadFromDisk();
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsRunningInVR || !EnableGripCalibration )
			return;

		if ( _gripCooldown > 0f )
		{
			_gripCooldown -= Time.Delta;
			_gripHoldTimer = 0f;
			GripHoldProgressSeconds = 0f;
			return;
		}

		bool bothGrips = Input.VR.LeftHand.Active && Input.VR.RightHand.Active
			&& Input.VR.LeftHand.Grip.Value >= GripPressThreshold
			&& Input.VR.RightHand.Grip.Value >= GripPressThreshold;

		if ( bothGrips )
		{
			_gripHoldTimer += Time.Delta;
			GripHoldProgressSeconds = _gripHoldTimer;

			if ( _gripHoldTimer >= GripHoldDurationSeconds )
			{
				ApplySampleFromCurrentHeadHeight();
				_gripHoldTimer = 0f;
				GripHoldProgressSeconds = 0f;
				_gripCooldown = 0.75f;
				Input.VR.LeftHand.TriggerHaptics( HapticEffect.HardImpact );
				Input.VR.RightHand.TriggerHaptics( HapticEffect.HardImpact );
			}
		}
		else
		{
			_gripHoldTimer = 0f;
			GripHoldProgressSeconds = 0f;
		}
	}

	/// <summary>
	/// 以當前 HMD 相對地板高度完成校正並標記已校正。
	/// <para>
	/// 取樣公式：<c>PlayerTargetEyeHeight = Input.VR.Head.Position.z − FloorRoot.WorldPosition.z</c>。
	/// <para>
	/// 必須使用 <c>Input.VR.Head</c>（原始追蹤）而非場景中的 HeadTracker 物件，
	/// 因為 HeadTracker 可能已被比例映射（例如 <c>VRScaledTrackedObject</c>）改寫，
	/// 若再拿來做校正會形成「用縮放後結果反算縮放」的回授震盪（例如 52 ↔ 20 來回跳動）。
	/// </para>
	/// </summary>
	public void ApplySampleFromCurrentHeadHeight()
	{
		float floorZ = FloorRoot.WorldPosition.z;
		float eyeZ   = Input.VR.Head.Position.z;
		float sampled = eyeZ - floorZ;

		if ( sampled <= 1f )
		{
			Log.Warning( $"[VRPlayerCalibration] 取樣眼高異常：RawHeadZ={eyeZ:F2}, FloorZ={floorZ:F2}, diff={sampled:F2} HU。請確認 HMD 已正確追蹤、地板原點對齊。" );
		}

		PlayerTargetEyeHeight = MathF.Max( 1f, sampled );
		IsPlayerCalibrated    = true;
		CalibrationVersion++;
		Log.Info( $"[VRPlayerCalibration] 玩家眼高已設定為 {PlayerTargetEyeHeight:F1} HU（Raw HMD Z - 地板 WorldZ）。版本={CalibrationVersion}" );
	}

	/// <summary>手動寫入眼高並標記為已校正（供 UI / Inspector 按鈕呼叫）。</summary>
	public void SetManualEyeHeightAndMarkCalibrated( float heightAboveFloorHu )
	{
		PlayerTargetEyeHeight = Math.Clamp( heightAboveFloorHu, 1f, 500f );
		IsPlayerCalibrated    = true;
		CalibrationVersion++;
	}

	public void ClearCalibration()
	{
		IsPlayerCalibrated = false;
		_gripHoldTimer     = 0f;
		GripHoldProgressSeconds = 0f;
		CalibrationVersion++;
	}

	public bool TrySaveToDisk()
	{
		try
		{
			var dto = new CalibrationDto
			{
				PlayerTargetEyeHeight = PlayerTargetEyeHeight,
				IsPlayerCalibrated    = IsPlayerCalibrated
			};
			string json = JsonSerializer.Serialize( dto, new JsonSerializerOptions { WriteIndented = true } );
			string path = NormalizeSavePath( SaveRelativePath );
			FileSystem.Data.WriteAllText( path, json );
			return true;
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[VRPlayerCalibration] 存檔失敗。" );
			return false;
		}
	}

	public bool TryLoadFromDisk()
	{
		try
		{
			string path = NormalizeSavePath( SaveRelativePath );
			if ( !FileSystem.Data.FileExists( path ) )
				return false;

			string json = FileSystem.Data.ReadAllText( path );
			var dto = JsonSerializer.Deserialize<CalibrationDto>( json );
			if ( dto is null )
				return false;

			PlayerTargetEyeHeight = dto.PlayerTargetEyeHeight;
			IsPlayerCalibrated    = dto.IsPlayerCalibrated;
			CalibrationVersion++;
			return true;
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[VRPlayerCalibration] 讀檔失敗。" );
			return false;
		}
	}

	private static string NormalizeSavePath( string relative )
	{
		if ( string.IsNullOrWhiteSpace( relative ) )
			relative = DefaultSaveRelativePath;
		relative = relative.Replace( '\\', '/' ).TrimStart( '/' );
		return relative;
	}

	private sealed class CalibrationDto
	{
		public float PlayerTargetEyeHeight { get; set; }
		public bool  IsPlayerCalibrated    { get; set; }
	}
}
