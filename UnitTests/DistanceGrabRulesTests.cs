using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sandbox;
using VRLogic;

namespace VRLogic.UnitTests;

[TestClass]
public sealed class DistanceGrabRulesTests
{
	[TestMethod]
	public void ScoreTarget_prefers_close_and_aligned()
	{
		var nearAligned = DistanceGrabRules.ScoreTarget( 40f, 0.95f, 200f );
		var farMisaligned = DistanceGrabRules.ScoreTarget( 180f, 0.65f, 200f );
		Assert.IsTrue( nearAligned > farMisaligned );
	}

	[TestMethod]
	public void ScoreTarget_min_value_when_invalid()
	{
		var score = DistanceGrabRules.ScoreTarget( 300f, 0.9f, 200f );
		Assert.AreEqual( float.MinValue, score );
	}

	[TestMethod]
	public void ComputePullVelocity_uses_requested_speed()
	{
		var v = DistanceGrabRules.ComputePullVelocity( Vector3.Zero, Vector3.Right * 10f, 120f );
		Assert.AreEqual( 120f, v.Length, 0.001f );
		Assert.IsTrue( Vector3.Dot( v.Normal, Vector3.Right ) > 0.99f );
	}
}

