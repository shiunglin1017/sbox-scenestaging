using System;
using Sandbox;

namespace Sandbox;

/// <summary>
/// 掛載於 Avatar 根（與 <see cref="SkinnedModelRenderer"/> 同層）。
/// Prefab 眼高優先讀 ModelDoc Attachment（<c>vr_eyes</c> / <c>vr_floor</c>）；未取得時退回 Head 骨 + 局部偏移。
/// 量測僅 <b>快照一次</b>，避免每幀讀 IK 後骨骼導致分母隨動畫漂移、<c>ScaleFactor</c> 貼近 1。
/// 向父節點取得 <see cref="VRPlayerCalibration"/>，計算 <c>s = H_avatar_prefab / H_player</c>，
/// 並寫入 <see cref="VRTrackingRoot"/> 的均勻 <see cref="GameObject.LocalScale"/>；
/// 追蹤器世界座標由 Transform 鏈自動對齊 Avatar 體型，不再做 <c>MapPoint</c> 手算映射。
/// 若 Inspector 中變更 <see cref="EyeAttachmentName"/>、<see cref="FloorAttachmentName"/>、
/// <see cref="EyeOffsetLocalFromHeadBone"/> 或 <see cref="PrefabEyeBoneName"/>，會自動
/// <see cref="InvalidatePrefabEyeSnapshot"/> 以重新量測 Prefab 眼高。
/// </summary>
public sealed class VRAvatarProportionBinding : Component
{
	public enum PrefabEyeHeightSources
	{
		None,
		ManualOverride,
		Attachment,
		BoneFallback,
		ConstantFallback
	}

	// ============================================================
	//  Scene References
	// ============================================================

	[Property, Group( "Scene References" )]
	public SkinnedModelRenderer ModelRenderer { get; set; }

	/// <summary>場景中包住 Camera／手把追蹤節點的根（例如 VR_Tracking_Root）；<c>ScaleFactor</c> 套於此物件。</summary>
	[Property, Group( "Scene References" )]
	public GameObject VRTrackingRoot { get; set; }

	/// <summary>
	/// 量測 Prefab 眼高用的 ModelDoc Attachment 名稱（預設 <c>vr_eyes</c>，通常掛 Head 骨並於 ModelDoc 內視覺微調至兩眼之間）。
	/// 取得失敗時退回 <see cref="PrefabEyeBoneName"/> + <see cref="EyeOffsetLocalFromHeadBone"/>。
	/// </summary>
	[Property, Group( "Scene References" )]
	public string EyeAttachmentName { get; set; } = "vr_eyes";

	/// <summary>
	/// 量測 Prefab 地板（模型腳底）用的 ModelDoc Attachment 名稱（預設 <c>vr_floor</c>，通常不綁骨頭、落於模型原點）。
	/// 取得失敗時退回 Player 根世界 Z。
	/// </summary>
	[Property, Group( "Scene References" )]
	public string FloorAttachmentName { get; set; } = "vr_floor";

	/// <summary>Fallback：量測 Prefab 眼高的骨骼名稱（預設 Head）。僅在 Attachment 路徑不可用時生效。</summary>
	[Property, Group( "Scene References" )]
	public string PrefabEyeBoneName { get; set; } = "Head";

	/// <summary>
	/// Fallback：自 Head 骨局部空間的偏移，將「頭骨原點」校正到與 HMD 語意一致的視點（眼高補正）。
	/// 僅在 Attachment 路徑不可用時生效。
	/// 變更後下一幀會自動重新快照 Prefab 眼高（見 <see cref="MaybeInvalidatePrefabEyeIfBindingParamsChanged"/>）。
	/// </summary>
	[Property, Group( "Scene References" )]
	public Vector3 EyeOffsetLocalFromHeadBone { get; set; } = Vector3.Zero;

	/// <summary>Fallback：無法讀取 Attachment 或骨骼時，使用的預設 Prefab 眼高（HU，相對地板）。</summary>
	[Property, Group( "Scene References" ), Range( 1f, 300f )]
	public float PrefabEyeHeightFallback { get; set; } = 64f;

	/// <summary>
	/// 手動指定 Prefab 眼高（HU）。大於 0 時優先使用，略過 attachment/bone 偵測。
	/// 當模型 attachment 參考系不穩定時，可先用此值固定開發流程。
	/// </summary>
	[Property, Group( "Scene References" ), Range( 0f, 300f )]
	public float ManualPrefabEyeHeightHu { get; set; }

	/// <summary>
	/// 啟動後最多等待幾幀再讀 Head 骨做快照；超過仍失敗則改用 <see cref="PrefabEyeHeightFallback"/> 快照。
	/// （約 60fps 下 180 ≈ 3 秒。）
	/// </summary>
	[Property, Group( "Scene References" ), Range( 1, 600 )]
	public int PrefabEyeSnapshotMaxWaitFrames { get; set; } = 180;

	// ============================================================
	//  Validation
	// ============================================================

	/// <summary>
	/// 啟用後，attachment/bone 量測值若落在不合理範圍外，會被拒絕並改走下一層回退。
	/// </summary>
	[Property, Group( "Validation" )]
	public bool RejectOutOfRangeSamples { get; set; } = true;

	/// <summary>可接受的 Prefab 眼高下限（HU）。</summary>
	[Property, Group( "Validation" ), Range( 1f, 300f )]
	public float MinValidPrefabEyeHeightHu { get; set; } = 35f;

	/// <summary>可接受的 Prefab 眼高上限（HU）。</summary>
	[Property, Group( "Validation" ), Range( 1f, 300f )]
	public float MaxValidPrefabEyeHeightHu { get; set; } = 220f;

	// ============================================================
	//  Scale limits
	// ============================================================

	[Property, Group( "Scale" ), Range( 0.2f, 3f )]
	public float MinScaleFactor { get; set; } = 0.35f;

	[Property, Group( "Scale" ), Range( 0.5f, 4f )]
	public float MaxScaleFactor { get; set; } = 2.5f;

	// ============================================================
	//  Read-only
	// ============================================================

	/// <summary>Prefab 眼高相對地板（HU），快照完成後為常數直至 <see cref="InvalidatePrefabEyeSnapshot"/>。</summary>
	[Property, ReadOnly, Group( "Debug (ReadOnly)" )]
	public float PrefabEyeHeightHu { get; private set; }

	/// <summary>是否已完成 Prefab 眼高快照（骨骼或 Fallback）。</summary>
	[Property, ReadOnly, Group( "Debug (ReadOnly)" )]
	public bool HasPrefabEyeSnapshot { get; private set; }

	/// <summary>套用於 <see cref="VRTrackingRoot"/> 的均勻縮放係數 <c>s = H_avatar_prefab / H_player</c>（未就緒時為 1）。</summary>
	[Property, ReadOnly, Group( "Debug (ReadOnly)" )]
	public float ScaleFactor { get; private set; } = 1f;

	/// <summary>玩家與 Avatar 資料皆可用且比例已計算。</summary>
	[Property, ReadOnly, Group( "Debug (ReadOnly)" )]
	public bool IsScaleReady { get; private set; }

	/// <summary>
	/// 目前追蹤縮放有效值（方法 2 下等同 <see cref="ScaleFactor"/>，提供給 Inspector 快速比對）。
	/// </summary>
	[Property, ReadOnly, Group( "Debug (ReadOnly)" )]
	public float EffectiveTrackingScale => ScaleFactor;

	/// <summary>本次快照採樣來源。</summary>
	[Property, ReadOnly, Group( "Debug (ReadOnly)" )]
	public PrefabEyeHeightSources PrefabEyeHeightSource { get; private set; }

	/// <summary>最近一次被拒絕的候選眼高（HU）。</summary>
	[Property, ReadOnly, Group( "Debug (ReadOnly)" )]
	public float LastRejectedSampleHu { get; private set; }

	/// <summary>最近一次拒絕原因。</summary>
	[Property, ReadOnly, Group( "Debug (ReadOnly)" )]
	public string LastRejectedReason { get; private set; } = string.Empty;

	// ============================================================
	//  Internal
	// ============================================================

	private VRPlayerCalibration _playerCal;
	private GameObject          _playerRoot;
	private int                 _snapshotWaitFrames;

	/// <summary>上次成功快照時使用的偏移與骨骼名，用於偵測 Inspector 調參並觸發重新快照。</summary>
	private Vector3 _eyeOffsetAtSnapshot;

	private string _boneNameAtSnapshot = string.Empty;

	private string _eyeAttachmentAtSnapshot = string.Empty;

	private string _floorAttachmentAtSnapshot = string.Empty;

	private float _manualEyeHeightAtSnapshot;
	private bool  _rejectOutOfRangeAtSnapshot;
	private float _minValidEyeAtSnapshot;
	private float _maxValidEyeAtSnapshot;

	/// <summary>快照時量到的地板世界 Z（除錯／Gizmo 用）；Attachment 路徑為 <c>vr_floor</c> 世界 Z，骨骼路徑為 Player 根世界 Z。</summary>
	private float _prefabFloorZWorldAtSnapshot;

	protected override void OnStart()
	{
		if ( ModelRenderer is null )
			ModelRenderer = Components.Get<SkinnedModelRenderer>();

		ResolvePlayerRoot();
		MaybeInvalidatePrefabEyeIfBindingParamsChanged();
		TrySnapshotPrefabEyeHeight();
		RecomputeScaleFactor();
	}

	protected override void OnUpdate()
	{
		MaybeInvalidatePrefabEyeIfBindingParamsChanged();
		ResolvePlayerRoot();
		TrySnapshotPrefabEyeHeight();
		RecomputeScaleFactor();
	}

	/// <summary>
	/// 清除 Prefab 眼高快照（例如更換 Avatar 模型後呼叫），下一幀起會重新嘗試快照。
	/// </summary>
	public void InvalidatePrefabEyeSnapshot()
	{
		HasPrefabEyeSnapshot         = false;
		_snapshotWaitFrames          = 0;
		PrefabEyeHeightHu            = 0f;
		PrefabEyeHeightSource        = PrefabEyeHeightSources.None;
		_eyeAttachmentAtSnapshot     = string.Empty;
		_floorAttachmentAtSnapshot   = string.Empty;
		_prefabFloorZWorldAtSnapshot = 0f;
		ResetTrackingRootScale();
	}

	private void MaybeInvalidatePrefabEyeIfBindingParamsChanged()
	{
		if ( !HasPrefabEyeSnapshot )
			return;

		if ( PrefabEyeBoneName != _boneNameAtSnapshot
			|| EyeOffsetLocalFromHeadBone != _eyeOffsetAtSnapshot
			|| EyeAttachmentName != _eyeAttachmentAtSnapshot
			|| FloorAttachmentName != _floorAttachmentAtSnapshot
			|| ManualPrefabEyeHeightHu != _manualEyeHeightAtSnapshot
			|| RejectOutOfRangeSamples != _rejectOutOfRangeAtSnapshot
			|| MinValidPrefabEyeHeightHu != _minValidEyeAtSnapshot
			|| MaxValidPrefabEyeHeightHu != _maxValidEyeAtSnapshot )
			InvalidatePrefabEyeSnapshot();
	}

	private void ResetTrackingRootScale()
	{
		if ( VRTrackingRoot is not null )
			VRTrackingRoot.LocalScale = Vector3.One;
	}

	private void ResolvePlayerRoot()
	{
		var parent = GameObject.Parent;
		if ( parent is null )
		{
			_playerCal  = null;
			_playerRoot = GameObject;
			return;
		}

		_playerRoot = parent;
		_playerCal  = parent.Components.Get<VRPlayerCalibration>();
	}

	/// <summary>
	/// 僅執行一次：優先讀 ModelDoc Attachment（<c>vr_eyes</c> / <c>vr_floor</c>），
	/// 失敗時退回 Head 骨 + <see cref="EyeOffsetLocalFromHeadBone"/>；逾時則使用 Fallback 常數。
	/// </summary>
	private void TrySnapshotPrefabEyeHeight()
	{
		if ( HasPrefabEyeSnapshot )
			return;

		// 0) 手動覆蓋：大於 0 直接使用
		if ( ManualPrefabEyeHeightHu > 0f )
		{
			PrefabEyeHeightHu = MathF.Max( 0.01f, ManualPrefabEyeHeightHu );
			CommitSnapshot( floorZWorld: _playerRoot?.WorldPosition.z ?? 0f, PrefabEyeHeightSources.ManualOverride );
			return;
		}

		// 1) Attachment 路徑：眼 = vr_eyes(world).z；地板 = vr_floor(world).z（不綁骨時等同模型原點）。
		if ( ModelRenderer is not null
			&& !string.IsNullOrWhiteSpace( EyeAttachmentName )
			&& !string.IsNullOrWhiteSpace( FloorAttachmentName ) )
		{
			var eyeAtt   = ModelRenderer.GetAttachment( EyeAttachmentName,   worldSpace: true );
			var floorAtt = ModelRenderer.GetAttachment( FloorAttachmentName, worldSpace: true );

			if ( eyeAtt.HasValue && floorAtt.HasValue )
			{
				float eyeZ   = eyeAtt.Value.Position.z;
				float floorZ = floorAtt.Value.Position.z;
				float sample = eyeZ - floorZ;
				if ( TryAcceptSample( sample, "Attachment", out float accepted ) )
				{
					PrefabEyeHeightHu = accepted;
					CommitSnapshot( floorZWorld: floorZ, PrefabEyeHeightSources.Attachment );
					return;
				}
			}
		}

		// 2) Fallback：Head 骨 + 偏移；地板取 Player 根世界 Z。
		float playerRootZ = _playerRoot?.WorldPosition.z ?? 0f;

		if ( ModelRenderer is not null
			&& ModelRenderer.TryGetBoneTransform( PrefabEyeBoneName, out var headTx ) )
		{
			var eyeWorld = headTx.Position + headTx.Rotation * EyeOffsetLocalFromHeadBone;
			float sample = eyeWorld.z - playerRootZ;
			if ( TryAcceptSample( sample, "BoneFallback", out float accepted ) )
			{
				PrefabEyeHeightHu = accepted;
				CommitSnapshot( floorZWorld: playerRootZ, PrefabEyeHeightSources.BoneFallback );
				return;
			}
		}

		// 3) 仍取不到：等待 N 幀後使用 Fallback 常數
		_snapshotWaitFrames++;
		if ( _snapshotWaitFrames >= PrefabEyeSnapshotMaxWaitFrames )
		{
			PrefabEyeHeightHu = MathF.Max( 0.01f, PrefabEyeHeightFallback );
			CommitSnapshot( floorZWorld: playerRootZ, PrefabEyeHeightSources.ConstantFallback );
		}
	}

	private bool TryAcceptSample( float sample, string sourceName, out float accepted )
	{
		accepted = MathF.Max( 0.01f, sample );
		if ( accepted <= 0.01f )
		{
			LastRejectedSampleHu = sample;
			LastRejectedReason = $"{sourceName}: sample<=0";
			return false;
		}

		if ( RejectOutOfRangeSamples
			&& (accepted < MinValidPrefabEyeHeightHu || accepted > MaxValidPrefabEyeHeightHu) )
		{
			LastRejectedSampleHu = sample;
			LastRejectedReason = $"{sourceName}: out-of-range [{MinValidPrefabEyeHeightHu:F1}, {MaxValidPrefabEyeHeightHu:F1}]";
			return false;
		}

		return true;
	}

	/// <summary>
	/// 統一寫入快照狀態與比對基準。所有 Inspector 可變更欄位都記下「快照當下的值」，
	/// 之後 <see cref="MaybeInvalidatePrefabEyeIfBindingParamsChanged"/> 才能正確判斷是否需重新量測。
	/// </summary>
	private void CommitSnapshot( float floorZWorld, PrefabEyeHeightSources source )
	{
		HasPrefabEyeSnapshot         = true;
		PrefabEyeHeightSource        = source;
		_eyeOffsetAtSnapshot         = EyeOffsetLocalFromHeadBone;
		_boneNameAtSnapshot          = PrefabEyeBoneName;
		_eyeAttachmentAtSnapshot     = EyeAttachmentName ?? string.Empty;
		_floorAttachmentAtSnapshot   = FloorAttachmentName ?? string.Empty;
		_manualEyeHeightAtSnapshot   = ManualPrefabEyeHeightHu;
		_rejectOutOfRangeAtSnapshot  = RejectOutOfRangeSamples;
		_minValidEyeAtSnapshot       = MinValidPrefabEyeHeightHu;
		_maxValidEyeAtSnapshot       = MaxValidPrefabEyeHeightHu;
		_prefabFloorZWorldAtSnapshot = floorZWorld;
	}

	private void RecomputeScaleFactor()
	{
		if ( _playerCal is null || !_playerCal.IsPlayerCalibrated || PrefabEyeHeightHu < 0.01f )
		{
			ScaleFactor  = 1f;
			IsScaleReady = false;
			ResetTrackingRootScale();
			return;
		}

		float hPlayer = MathF.Max( 0.01f, _playerCal.PlayerTargetEyeHeight );
		float s       = PrefabEyeHeightHu / hPlayer;
		ScaleFactor   = Math.Clamp( s, MinScaleFactor, MaxScaleFactor );
		IsScaleReady  = true;

		if ( VRTrackingRoot is not null )
			VRTrackingRoot.LocalScale = Vector3.One * ScaleFactor;
		else
			ResetTrackingRootScale();
	}
}
