using Sandbox;

/// <summary>
/// 雙手持握穩定器：兩手抓同一物時，以後手為 pivot、前手決定前向。
/// </summary>
public sealed class VRTwoHandGripStabilizer : Component
{
	[Property, Group( "References" )]
	public VRGrabber RearHandGrabber { get; set; }

	[Property, Group( "References" )]
	public VRGrabber FrontHandGrabber { get; set; }

	[Property, Group( "Settings" )]
	public float RotationLerpSpeed { get; set; } = 20f;

	[Property, Group( "State" )]
	public bool IsTwoHandActive { get; private set; }

	protected override void OnFixedUpdate()
	{
		IsTwoHandActive = false;

		if ( !RearHandGrabber.IsValid() || !FrontHandGrabber.IsValid() )
			return;
		if ( !RearHandGrabber.IsHoldingObject || !FrontHandGrabber.IsHoldingObject )
			return;
		if ( RearHandGrabber.HeldObject != FrontHandGrabber.HeldObject )
			return;

		var held = RearHandGrabber.HeldObject;
		if ( !held.IsValid() )
			return;
		if ( !VRGrabber.TryResolveRigidbody( held, out var rb ) || !rb.IsValid() )
			return;

		var rearPos = RearHandGrabber.WorldPosition;
		var frontPos = FrontHandGrabber.WorldPosition;
		var forward = frontPos - rearPos;
		if ( forward.LengthSquared <= 0.001f )
			return;

		IsTwoHandActive = true;
		var targetRot = Rotation.LookAt( forward.Normal, RearHandGrabber.WorldRotation.Up );
		held.WorldRotation = Rotation.Slerp( held.WorldRotation, targetRot, (RotationLerpSpeed * Time.Delta).Clamp( 0f, 1f ) );
	}
}

