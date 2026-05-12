using Sandbox;

/// <summary>
/// 指尖戳擊 UI：以 Trigger 進入/離開控制 <see cref="VRUIInteractable"/> 按壓狀態。
/// </summary>
public sealed class VRUIPokeInteractor : Component, Component.ITriggerListener
{
	[Property, Group( "Poke" )]
	public bool RequireInputPress { get; set; } = false;

	[Property, Group( "Poke" )]
	public float TriggerThreshold { get; set; } = 0.7f;

	[Property, Group( "Poke" )]
	public string DesktopPokeAction { get; set; } = "attack1";

	[Property, Group( "Poke" )]
	public bool IsLeftHand { get; set; } = true;

	VRUIInteractable _active;

	void Component.ITriggerListener.OnTriggerEnter( Collider other )
	{
		var ui = other.GameObject.Components.Get<VRUIInteractable>( FindMode.EverythingInSelfAndAncestors );
		if ( ui is null )
			return;

		_active = ui;
		ui.SetHover( true );
		if ( !RequireInputPress || IsPressDown() )
			ui.Press();
	}

	void Component.ITriggerListener.OnTriggerExit( Collider other )
	{
		if ( _active is null )
			return;

		var exiting = other.GameObject.Components.Get<VRUIInteractable>( FindMode.EverythingInSelfAndAncestors );
		if ( exiting != _active )
			return;

		_active.Release();
		_active.SetHover( false );
		_active = null;
	}

	protected override void OnUpdate()
	{
		if ( _active is null )
			return;

		if ( !RequireInputPress )
			return;

		if ( IsPressDown() )
			_active.Press();
		else
			_active.Release();
	}

	bool IsPressDown()
	{
		if ( Game.IsRunningInVR )
		{
			var ctl = IsLeftHand ? Input.VR.LeftHand : Input.VR.RightHand;
			return ctl.Trigger.Value >= TriggerThreshold;
		}

		return Input.Down( DesktopPokeAction );
	}
}

