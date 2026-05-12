using Sandbox;

/// <summary>
/// 控制器雷射 UI：Raycast 命中 <see cref="VRUIInteractable"/> 後提供 hover/press。
/// </summary>
public sealed class VRUIPointerRay : Component
{
	[Property, Group( "References" )]
	public VRGrabber Grabber { get; set; }

	[Property, Group( "References" ), Description( "非 VR 模式瞄準參考；留空則用主相機。" )]
	public GameObject DesktopAimReference { get; set; }

	[Property, Group( "Pointer" )]
	public float MaxDistance { get; set; } = 500f;

	[Property, Group( "Pointer" )]
	public float PressThreshold { get; set; } = 0.8f;

	[Property, Group( "Pointer" )]
	public string DesktopPressAction { get; set; } = "attack1";

	[Property, Group( "Debug" )]
	public bool DebugDraw { get; set; }

	VRUIInteractable _hovered;

	protected override void OnUpdate()
	{
		var aim = ResolveAimTransform();
		var hit = Scene.Trace.Ray( aim.Position, aim.Position + aim.Rotation.Forward * MaxDistance )
			.IgnoreGameObjectHierarchy( GameObject.Parent )
			.Run();

		var target = hit.GameObject.Components.Get<VRUIInteractable>( FindMode.EverythingInSelfAndAncestors );
		if ( target != _hovered )
		{
			_hovered?.SetHover( false );
			_hovered = target;
			_hovered?.SetHover( true );
		}

		if ( _hovered is not null )
		{
			if ( IsPressDown() )
				_hovered.Press();
			else
				_hovered.Release();
		}

		if ( DebugDraw )
		{
			Gizmo.Draw.Color = target is null ? Color.Red : Color.Green;
			Gizmo.Draw.Line( aim.Position, hit.Hit ? hit.HitPosition : aim.Position + aim.Rotation.Forward * MaxDistance );
		}
	}

	bool IsPressDown()
	{
		if ( Game.IsRunningInVR && Grabber.IsValid() )
		{
			var ctl = Grabber.IsLeftHand ? Input.VR.LeftHand : Input.VR.RightHand;
			return ctl.Trigger.Value >= PressThreshold;
		}

		return Input.Down( DesktopPressAction );
	}

	Transform ResolveAimTransform()
	{
		if ( Game.IsRunningInVR && Grabber.IsValid() )
		{
			var ctl = Grabber.IsLeftHand ? Input.VR.LeftHand : Input.VR.RightHand;
			return ctl.AimTransform;
		}

		if ( DesktopAimReference.IsValid() )
			return DesktopAimReference.WorldTransform;

		var cam = Scene.Camera;
		return cam.IsValid() ? cam.WorldTransform : WorldTransform;
	}
}

