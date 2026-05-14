using Sandbox;
using System;
using System.Reflection;

/// <summary>
/// VR 玩家根節點：集中啟用／停用位移、桌面模擬、雙手抓取與幽靈目標，並可選自動串接 <see cref="CharacterController"/>。
/// </summary>
public sealed class VRPlayerRig : Component
{
	public enum LocomotionAuthority
	{
		LegacyVRPlayerController,
		XMovement
	}

	[Property, Group( "Features" )]
	public bool EnableLocomotion { get; set; } = true;

	[Property, Group( "Features" )]
	public bool EnableDesktopFallback { get; set; } = true;

	[Property, Group( "Features" )]
	public bool EnableLeftGrab { get; set; } = true;

	[Property, Group( "Features" )]
	public bool EnableRightGrab { get; set; } = true;

	[Property, Group( "Features" ), Description( "子階層所有 VRGhostHandTarget（武器幽靈追蹤目標）。" )]
	public bool EnableGhostTargets { get; set; } = true;

	[Property, Group( "Auto wire" ), Description( "為同物件上的 VRPlayerController 填入 CharacterController（若尚未指定）。" )]
	public bool AutoWireCharacterController { get; set; } = true;

	[Property, Group( "Locomotion" ), Description( "主位移權威。XMovement 與 VRPlayerController 必須擇一啟用，避免雙重 Move。" )]
	public LocomotionAuthority MovementAuthority { get; set; } = LocomotionAuthority.XMovement;

	[Property, Group( "Locomotion" ), Description( "XMovement 路線優先使用 Simple 控制器；若不存在則回退 Complex。" )]
	public bool PreferSimpleXMovement { get; set; } = true;

	protected override void OnAwake()
	{
		ApplyFeatureToggles();
	}

	/// <summary>與 Inspector 相同的一次同步；執行時期可呼叫以切換功能。</summary>
	public void ApplyFeatureToggles()
	{
		CharacterController cc = null;
		if ( AutoWireCharacterController )
			cc = Components.Get<CharacterController>();

		var locomotion = Components.Get<VRPlayerController>();
		var useLegacy = MovementAuthority == LocomotionAuthority.LegacyVRPlayerController && EnableLocomotion;
		var useXMovement = MovementAuthority == LocomotionAuthority.XMovement && EnableLocomotion;

		if ( locomotion is not null )
		{
			locomotion.Enabled = useLegacy;
			if ( useLegacy && cc is not null && locomotion.Controller is null )
				locomotion.Controller = cc;
		}

		ApplyXMovementToggles( useXMovement );
		AutoWireInputSources();

		var fallback = Components.Get<VRFallbackSimulator>();
		if ( fallback is not null )
			fallback.Enabled = EnableDesktopFallback;

		foreach ( var grabber in Components.GetAll<VRGrabber>( FindMode.EnabledInSelfAndDescendants ) )
		{
			grabber.Enabled = grabber.IsLeftHand ? EnableLeftGrab : EnableRightGrab;
		}

		foreach ( var ghost in Components.GetAll<VRGhostHandTarget>( FindMode.EnabledInSelfAndDescendants ) )
			ghost.Enabled = EnableGhostTargets;
	}

	void ApplyXMovementToggles( bool enableXMovement )
	{
		var simple = FindComponentByName( "PlayerWalkControllerSimple" );
		var complex = FindComponentByName( "PlayerWalkControllerComplex" );

		if ( simple is null && complex is null )
			return;

		if ( PreferSimpleXMovement )
		{
			SetComponentEnabled( simple, enableXMovement );
			SetComponentEnabled( complex, false );
			return;
		}

		SetComponentEnabled( complex, enableXMovement );
		SetComponentEnabled( simple, false );
	}

	void AutoWireInputSources()
	{
		var composite = FindComponentByName( "CompositeMovementInputSource" );
		if ( composite is null )
			return;

		var vrSource = FindComponentByName( "VRMovementInputSource" );
		var kbmSource = FindComponentByName( "KeyboardMovementInputSource" );
		var provider = FindComponentByName( "SandboxVRInputProvider" );

		if ( GetPropertyValue( composite, "VRSource" ) is null )
		{
			if ( vrSource is not null )
				SetPropertyValue( composite, "VRSource", vrSource );
		}

		if ( GetPropertyValue( composite, "KbmSource" ) is null )
		{
			if ( kbmSource is not null )
				SetPropertyValue( composite, "KbmSource", kbmSource );
		}

		var compositeVrSource = GetPropertyValue( composite, "VRSource" );
		if ( compositeVrSource is not null && GetPropertyValue( compositeVrSource, "Provider" ) is null )
		{
			if ( provider is not null )
				SetPropertyValue( compositeVrSource, "Provider", provider );
		}
	}

	Component FindComponentByName( string typeName )
	{
		foreach ( var c in Components.GetAll<Component>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( c is null )
				continue;
			var t = c.GetType();
			if ( string.Equals( t.Name, typeName, StringComparison.Ordinal ) )
				return c;
		}
		return null;
	}

	static void SetComponentEnabled( Component component, bool enabled )
	{
		if ( component is null )
			return;
		component.Enabled = enabled;
	}

	static object GetPropertyValue( object target, string name )
	{
		if ( target is null )
			return null;
		var prop = target.GetType().GetProperty( name, BindingFlags.Instance | BindingFlags.Public );
		if ( prop is null || !prop.CanRead )
			return null;
		return prop.GetValue( target );
	}

	static void SetPropertyValue( object target, string name, object value )
	{
		if ( target is null )
			return;
		var prop = target.GetType().GetProperty( name, BindingFlags.Instance | BindingFlags.Public );
		if ( prop is null || !prop.CanWrite )
			return;
		if ( value is not null && !prop.PropertyType.IsAssignableFrom( value.GetType() ) )
			return;
		prop.SetValue( target, value );
	}
}
