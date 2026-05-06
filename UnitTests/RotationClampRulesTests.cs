using Microsoft.VisualStudio.TestTools.UnitTesting;
using VRLogic;

namespace VRLogic.UnitTests;

[TestClass]
public sealed class RotationClampRulesTests
{
	[TestMethod]
	public void ClampInterpolationBySpeed_returns_baseT_when_unlimited_speed()
	{
		var t = RotationClampRules.ClampInterpolationBySpeed( angleDeg: 120f, baseT: 0.6f, maxDegreesPerSecond: 0f, deltaTime: 1f / 60f );
		Assert.AreEqual( 0.6f, t, 0.0001f );
	}

	[TestMethod]
	public void ClampInterpolationBySpeed_keeps_baseT_when_angle_is_tiny()
	{
		var t = RotationClampRules.ClampInterpolationBySpeed( angleDeg: 0.0005f, baseT: 0.4f, maxDegreesPerSecond: 180f, deltaTime: 1f / 60f );
		Assert.AreEqual( 0.4f, t, 0.0001f );
	}

	[TestMethod]
	public void ClampInterpolationBySpeed_reduces_t_when_angle_large_and_speed_limited()
	{
		var t = RotationClampRules.ClampInterpolationBySpeed( angleDeg: 180f, baseT: 1f, maxDegreesPerSecond: 180f, deltaTime: 1f / 60f );
		Assert.IsTrue( t < 0.02f );
		Assert.IsTrue( t > 0f );
	}

	[TestMethod]
	public void ClampInterpolationBySpeed_scales_with_delta_time()
	{
		var t60 = RotationClampRules.ClampInterpolationBySpeed( angleDeg: 90f, baseT: 1f, maxDegreesPerSecond: 180f, deltaTime: 1f / 60f );
		var t30 = RotationClampRules.ClampInterpolationBySpeed( angleDeg: 90f, baseT: 1f, maxDegreesPerSecond: 180f, deltaTime: 1f / 30f );
		Assert.IsTrue( t30 > t60 );
		Assert.AreEqual( t60 * 2f, t30, 0.0001f );
	}
}
