using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sandbox;
using VRLogic;

namespace VRLogic.UnitTests;

[TestClass]
public sealed class LocomotionWishRulesTests
{
	[TestMethod]
	public void ComputePlanarWish_forward_stick_yields_forward_times_speed()
	{
		var f = Vector3.Forward;
		var r = Vector3.Right;
		var w = LocomotionWishRules.ComputePlanarWishFromHeadAxes( f, r, new Vector2( 0, 1 ), 100f );
		Assert.IsTrue( (w - Vector3.Forward * 100f).Length < 0.01f );
	}

	[TestMethod]
	public void ComputePlanarWish_zero_stick_yields_zero()
	{
		var w = LocomotionWishRules.ComputePlanarWishFromHeadAxes( Vector3.Forward, Vector3.Right, default, 100f );
		Assert.IsTrue( w.IsNearlyZero() );
	}

	[TestMethod]
	public void ToStickFromAnalogMove_prefers_z_for_forward_axis()
	{
		var stick = LocomotionWishRules.ToStickFromAnalogMove( new Vector3( 0.3f, 0.2f, 1f ) );
		Assert.AreEqual( 0.3f, stick.x, 0.001f );
		Assert.AreEqual( 1f, stick.y, 0.001f );
	}

	[TestMethod]
	public void ToStickFromAnalogMove_falls_back_to_y_when_z_zero()
	{
		var stick = LocomotionWishRules.ToStickFromAnalogMove( new Vector3( -0.5f, 0.75f, 0f ) );
		Assert.AreEqual( -0.5f, stick.x, 0.001f );
		Assert.AreEqual( 0.75f, stick.y, 0.001f );
	}
}
