using Sandbox;

/// <summary>
/// 可被 VR 射線或戳擊操作的 UI 代理元件。
/// </summary>
public sealed class VRUIInteractable : Component
{
	[Property, Group( "State" )]
	public bool IsHovered { get; private set; }

	[Property, Group( "State" )]
	public bool IsPressed { get; private set; }

	[Property, Group( "Debug" )]
	public bool LogTransitions { get; set; }

	public void SetHover( bool hovered )
	{
		if ( IsHovered == hovered )
			return;

		IsHovered = hovered;
		if ( LogTransitions )
			Log.Info( $"[VRUIInteractable] hover={hovered} {GameObject?.Name}" );
	}

	public void Press()
	{
		IsPressed = true;
		if ( LogTransitions )
			Log.Info( $"[VRUIInteractable] press {GameObject?.Name}" );
	}

	public void Release()
	{
		IsPressed = false;
		if ( LogTransitions )
			Log.Info( $"[VRUIInteractable] release {GameObject?.Name}" );
	}
}

