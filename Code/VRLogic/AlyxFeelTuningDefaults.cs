namespace VRLogic;

/// <summary>
/// Alyx 向手感之**預設建議值與說明**（非執行期強制；供關卡設計與 Rigidbody／關節調校對照）。
/// </summary>
public static class AlyxFeelTuningDefaults
{
	/// <summary>輕小道具建議質量級距（引擎單位，依專案縮放調整）。</summary>
	public const float SuggestedPropMassLight = 1f;

	/// <summary>一般手持道具。 </summary>
	public const float SuggestedPropMassMedium = 8f;

	/// <summary>重型／長形物。 </summary>
	public const float SuggestedPropMassHeavy = 25f;

	/// <summary>釋放時是否應優先繼承手部線速度（與目前 VRGrabber 一致）。角速度常需引擎型別轉換或略過。</summary>
	public const bool PreferHandLinearVelocityOnRelease = true;

	/// <summary>長物第二隻手：建議修飾主手關節目標，而非第二個 FixedJoint 焊死本體（設計註記）。</summary>
	public const string TwoHandedNote = "Secondary hand should bias primary grab constraint or use auxiliary spring, not duplicate fixed weld.";

	/// <summary>投擲訊號：預設保留樣本數。</summary>
	public const int ThrowSignalSampleCount = 10;

	/// <summary>投擲訊號：峰值鄰域大小（峰值前後各 N 筆）。</summary>
	public const int ThrowPeakNeighborhood = 3;

	/// <summary>放手線速度安全上限。</summary>
	public const float ThrowMaxLinearSpeed = 1200f;

	/// <summary>放手角速度安全上限。</summary>
	public const float ThrowMaxAngularSpeed = 60f;
}
