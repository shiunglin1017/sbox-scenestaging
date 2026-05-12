using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sandbox;
using VRLogic;

namespace VRLogic.UnitTests;

[TestClass]
public sealed class TeleportArcRulesTests
{
	[TestMethod]
	public void EvaluateComfortVignette_zero_below_start()
	{
		Assert.AreEqual( 0f, TeleportArcRules.EvaluateComfortVignette( 20f, 40f, 220f ), 0.0001f );
	}

	[TestMethod]
	public void EvaluateComfortVignette_one_above_full()
	{
		Assert.AreEqual( 1f, TeleportArcRules.EvaluateComfortVignette( 250f, 40f, 220f ), 0.0001f );
	}

	[TestMethod]
	public void EvaluateComfortVignette_interpolates_between_bounds()
	{
		var v = TeleportArcRules.EvaluateComfortVignette( 130f, 40f, 220f );
		Assert.IsTrue( v > 0.49f && v < 0.51f );
	}

	[TestMethod]
	public void TryFindTeleportPoint_false_when_trace_never_hits()
	{
		var ok = TeleportArcRules.TryFindTeleportPoint(
			origin: Vector3.Zero,
			initialVelocity: Vector3.Forward * 100f,
			gravity: Vector3.Down * 100f,
			stepTime: 0.02f,
			maxSteps: 8,
			maxDistance: 20f,
			traceStep: static (_, _) => default,
			out _,
			out _ );

		Assert.IsFalse( ok );
	}
}

