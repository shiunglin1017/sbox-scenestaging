using Sandbox;

/// <summary>
/// 旋轉機關：將局部旋轉限制於單軸角度範圍。
/// </summary>
public sealed class VRRotaryDriveInteractable : Component
{
	[Property, Group( "Drive" )]
	public RotationAxis Axis { get; set; } = RotationAxis.Yaw;

	[Property, Group( "Drive" )]
	public float MinAngle { get; set; } = -90f;

	[Property, Group( "Drive" )]
	public float MaxAngle { get; set; } = 90f;

	[Property, Group( "State" )]
	public float CurrentAngle { get; private set; }

	[Property, Group( "State" )]
	public float Normalized01 { get; private set; }

	Angles _startAngles;

	protected override void OnStart()
	{
		base.OnStart();
		_startAngles = Transform.LocalRotation.Angles();
	}

	protected override void OnFixedUpdate()
	{
		var a = Transform.LocalRotation.Angles();
		var raw = Axis switch
		{
			RotationAxis.Pitch => a.pitch - _startAngles.pitch,
			RotationAxis.Roll => a.roll - _startAngles.roll,
			_ => a.yaw - _startAngles.yaw
		};

		CurrentAngle = raw.Clamp( MinAngle, MaxAngle );
		var applied = _startAngles;
		switch ( Axis )
		{
			case RotationAxis.Pitch:
				applied.pitch = _startAngles.pitch + CurrentAngle;
				applied.yaw = _startAngles.yaw;
				applied.roll = _startAngles.roll;
				break;
			case RotationAxis.Roll:
				applied.roll = _startAngles.roll + CurrentAngle;
				applied.pitch = _startAngles.pitch;
				applied.yaw = _startAngles.yaw;
				break;
			default:
				applied.yaw = _startAngles.yaw + CurrentAngle;
				applied.pitch = _startAngles.pitch;
				applied.roll = _startAngles.roll;
				break;
		}

		Transform.LocalRotation = applied.ToRotation();
		var range = MathF.Max( 0.001f, MaxAngle - MinAngle );
		Normalized01 = ((CurrentAngle - MinAngle) / range).Clamp( 0f, 1f );
	}
}

public enum RotationAxis
{
	Pitch,
	Yaw,
	Roll
}

