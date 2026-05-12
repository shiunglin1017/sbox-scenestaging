namespace VRLogic;

/// <summary>
/// 與 <see cref="AlyxFeelTuningDefaults"/> 對齊的質量預設分級（關卡設計用 enum）。
/// </summary>
public enum VrPropMassPreset
{
	Light,
	Medium,
	Heavy,
	Custom
}

/// <summary>
/// 供序列化／單元測試使用的抓取點規格（不含場景引用）。
/// </summary>
public readonly record struct VRItemGrabPointSpec( int Priority, bool IsPrimary, string AttachmentName )
{
	public static VRItemGrabPointSpec DefaultPrimary => new( 0, true, VrInteractionConstants.DefaultGripAttachmentName );
}

/// <summary>
/// 從多筆抓取點條目推算主握點索引等純邏輯（無 Scene／無 VR 硬體）。
/// </summary>
public static class VRItemInteractionProfileRules
{
	public static float ResolveMass( VrPropMassPreset preset, float customMass )
	{
		return preset switch
		{
			VrPropMassPreset.Light => AlyxFeelTuningDefaults.SuggestedPropMassLight,
			VrPropMassPreset.Medium => AlyxFeelTuningDefaults.SuggestedPropMassMedium,
			VrPropMassPreset.Heavy => AlyxFeelTuningDefaults.SuggestedPropMassHeavy,
			VrPropMassPreset.Custom => customMass > 0f ? customMass : AlyxFeelTuningDefaults.SuggestedPropMassMedium,
			_ => AlyxFeelTuningDefaults.SuggestedPropMassMedium
		};
	}

	/// <summary>
	/// 回傳主握點索引：優先 <c>IsPrimary</c> 為真者中 <c>Priority</c> 最小；若無 Primary 則取全體 <c>Priority</c> 最小；空列表回傳 -1。
	/// </summary>
	public static int ResolvePrimaryGrabPointIndex( ReadOnlySpan<VRItemGrabPointSpec> points )
	{
		if ( points.Length == 0 )
			return -1;

		var best = -1;
		var bestPri = int.MaxValue;
		for ( var i = 0; i < points.Length; i++ )
		{
			if ( !points[i].IsPrimary )
				continue;
			if ( points[i].Priority < bestPri )
			{
				bestPri = points[i].Priority;
				best = i;
			}
		}

		if ( best >= 0 )
			return best;

		best = 0;
		bestPri = points[0].Priority;
		for ( var i = 1; i < points.Length; i++ )
		{
			if ( points[i].Priority < bestPri )
			{
				bestPri = points[i].Priority;
				best = i;
			}
		}

		return best;
	}

	/// <summary>
	/// 將「手部中心射線到碰撞點距離」映射為手指 curl（0~1）。
	/// </summary>
	/// <param name="distance">量測距離。</param>
	/// <param name="fullCurlDistance">此距離（含）以內視為完全彎曲。</param>
	/// <param name="startCurlDistance">此距離（含）以外視為不彎曲。</param>
	public static float MapDistanceToCurl( float distance, float fullCurlDistance, float startCurlDistance )
	{
		if ( startCurlDistance <= fullCurlDistance )
			return distance <= fullCurlDistance ? 1f : 0f;

		if ( distance <= fullCurlDistance )
			return 1f;
		if ( distance >= startCurlDistance )
			return 0f;

		var t = 1f - ((distance - fullCurlDistance) / (startCurlDistance - fullCurlDistance));
		return t.Clamp( 0f, 1f );
	}
}
