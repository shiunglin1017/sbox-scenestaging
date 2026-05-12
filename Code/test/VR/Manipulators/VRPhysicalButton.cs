using Sandbox;

/// <summary>
/// 物理按鈕：沿本地軸向被推入超過閾值時觸發 pressed。
/// </summary>
public sealed class VRPhysicalButton : Component, Component.ITriggerListener
{
	[Property, Group( "Button" )]
	public Vector3 LocalPressAxis { get; set; } = Vector3.Down;

	[Property, Group( "Button" )]
	public float TravelDistance { get; set; } = 2f;

	[Property, Group( "Button" )]
	public float PressThreshold01 { get; set; } = 0.7f;

	[Property, Group( "State" )]
	public float PressDepth01 { get; private set; }

	[Property, Group( "State" )]
	public bool IsPressed { get; private set; }

	Vector3 _startLocalPos;
	Collider _presser;

	protected override void OnStart()
	{
		base.OnStart();
		_startLocalPos = Transform.LocalPosition;
	}

	void Component.ITriggerListener.OnTriggerEnter( Collider other )
	{
		if ( _presser.IsValid() )
			return;
		_presser = other;
	}

	void Component.ITriggerListener.OnTriggerExit( Collider other )
	{
		if ( _presser != other )
			return;
		_presser = null;
	}

	protected override void OnFixedUpdate()
	{
		var axis = LocalPressAxis.LengthSquared <= 0.0001f ? Vector3.Down : LocalPressAxis.Normal;
		var desiredPos = _startLocalPos;

		if ( _presser.IsValid() )
		{
			var localPresser = GameObject.WorldTransform.PointToLocal( _presser.WorldPosition );
			var localDelta = localPresser - _startLocalPos;
			var amount = Vector3.Dot( localDelta, axis ).Clamp( 0f, TravelDistance );
			desiredPos += axis * amount;
		}

		Transform.LocalPosition = desiredPos;
		var moved = (Transform.LocalPosition - _startLocalPos).Length;
		PressDepth01 = (moved / MathF.Max( 0.001f, TravelDistance )).Clamp( 0f, 1f );
		IsPressed = PressDepth01 >= PressThreshold01;
	}
}

