using Sandbox;

/// <summary>
/// 線性推拉機關：將物件位移限制在單一軸向範圍內。
/// </summary>
public sealed class VRLinearDriveInteractable : Component
{
	[Property, Group( "Drive" )]
	public Vector3 LocalAxis { get; set; } = Vector3.Forward;

	[Property, Group( "Drive" )]
	public float MinDistance { get; set; } = 0f;

	[Property, Group( "Drive" )]
	public float MaxDistance { get; set; } = 20f;

	[Property, Group( "State" )]
	public float Normalized01 { get; private set; }

	Vector3 _startLocalPos;
	Rotation _startLocalRot;

	protected override void OnStart()
	{
		base.OnStart();
		_startLocalPos = LocalPosition;
		_startLocalRot = LocalRotation;
	}

	protected override void OnFixedUpdate()
	{
		var axis = LocalAxis.LengthSquared <= 0.0001f ? Vector3.Forward : LocalAxis.Normal;
		var offset = LocalPosition - _startLocalPos;
		var distance = Vector3.Dot( offset, axis ).Clamp( MinDistance, MaxDistance );
		LocalPosition = _startLocalPos + axis * distance;
		LocalRotation = _startLocalRot;

		var range = MathF.Max( 0.001f, MaxDistance - MinDistance );
		Normalized01 = ((distance - MinDistance) / range).Clamp( 0f, 1f );
	}
}

