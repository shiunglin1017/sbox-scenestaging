using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using VRLogic;

namespace VRLogic.UnitTests;

[TestClass]
public sealed class VRItemInteractionProfileRulesTests
{
	[TestMethod]
	public void ResolveMass_uses_Alyx_defaults_for_presets()
	{
		Assert.AreEqual( AlyxFeelTuningDefaults.SuggestedPropMassLight, VRItemInteractionProfileRules.ResolveMass( VrPropMassPreset.Light, 999f ) );
		Assert.AreEqual( AlyxFeelTuningDefaults.SuggestedPropMassMedium, VRItemInteractionProfileRules.ResolveMass( VrPropMassPreset.Medium, 999f ) );
		Assert.AreEqual( AlyxFeelTuningDefaults.SuggestedPropMassHeavy, VRItemInteractionProfileRules.ResolveMass( VrPropMassPreset.Heavy, 999f ) );
	}

	[TestMethod]
	public void ResolveMass_custom_positive()
	{
		Assert.AreEqual( 3.5f, VRItemInteractionProfileRules.ResolveMass( VrPropMassPreset.Custom, 3.5f ) );
	}

	[TestMethod]
	public void ResolveMass_custom_nonpositive_falls_back_medium()
	{
		Assert.AreEqual( AlyxFeelTuningDefaults.SuggestedPropMassMedium, VRItemInteractionProfileRules.ResolveMass( VrPropMassPreset.Custom, 0f ) );
		Assert.AreEqual( AlyxFeelTuningDefaults.SuggestedPropMassMedium, VRItemInteractionProfileRules.ResolveMass( VrPropMassPreset.Custom, -1f ) );
	}

	[TestMethod]
	public void ResolvePrimary_empty_returns_minus_one()
	{
		Assert.AreEqual( -1, VRItemInteractionProfileRules.ResolvePrimaryGrabPointIndex( Array.Empty<VRItemGrabPointSpec>() ) );
	}

	[TestMethod]
	public void ResolvePrimary_prefers_smallest_priority_among_primary()
	{
		var s = new VRItemGrabPointSpec[]
		{
			new( 10, false, "a" ),
			new( 1, true, "b" ),
			new( 0, true, "c" )
		};
		Assert.AreEqual( 2, VRItemInteractionProfileRules.ResolvePrimaryGrabPointIndex( s ) );
	}

	[TestMethod]
	public void ResolvePrimary_when_no_primary_uses_smallest_priority_overall()
	{
		var s = new VRItemGrabPointSpec[]
		{
			new( 5, false, "a" ),
			new( 2, false, "b" )
		};
		Assert.AreEqual( 1, VRItemInteractionProfileRules.ResolvePrimaryGrabPointIndex( s ) );
	}

	[TestMethod]
	public void MapDistanceToCurl_behaves_as_expected()
	{
		Assert.AreEqual( 1f, VRItemInteractionProfileRules.MapDistanceToCurl( 1f, fullCurlDistance: 2f, startCurlDistance: 12f ) );
		Assert.AreEqual( 0f, VRItemInteractionProfileRules.MapDistanceToCurl( 20f, fullCurlDistance: 2f, startCurlDistance: 12f ) );
		var mid = VRItemInteractionProfileRules.MapDistanceToCurl( 7f, fullCurlDistance: 2f, startCurlDistance: 12f );
		Assert.IsTrue( mid > 0f && mid < 1f );
	}

	[TestMethod]
	public void MapDistanceToCurl_handles_invalid_range()
	{
		Assert.AreEqual( 1f, VRItemInteractionProfileRules.MapDistanceToCurl( 1f, fullCurlDistance: 3f, startCurlDistance: 3f ) );
		Assert.AreEqual( 0f, VRItemInteractionProfileRules.MapDistanceToCurl( 4f, fullCurlDistance: 3f, startCurlDistance: 3f ) );
	}
}
