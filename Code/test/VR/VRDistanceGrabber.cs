using Sandbox;
using VRLogic;

/// <summary>
/// 遠距抓取：指向候選物後啟動吸附，物件接近手部時委託 VRGrabber 建立關節。
/// </summary>
public sealed class VRDistanceGrabber : Component
{
	[Property, Group( "References" )]
	public VRGrabber Grabber { get; set; }

	[Property, Group( "References" ), Description( "可選：用於非 VR 模式瞄準。" )]
	public GameObject AimReference { get; set; }

	[Property, Group( "Distance Grab" )]
	public float MaxDistance { get; set; } = 220f;

	[Property, Group( "Distance Grab" )]
	public float AcquireDotThreshold { get; set; } = 0.6f;

	[Property, Group( "Distance Grab" )]
	public float PullSpeed { get; set; } = 420f;

	[Property, Group( "Distance Grab" )]
	public float CatchDistance { get; set; } = 14f;

	[Property, Group( "Distance Grab" ), Description( "VR 模式：使用 Trigger 超過此值啟動隔空抓取。" )]
	public float TriggerPressThreshold { get; set; } = 0.75f;

	[Property, Group( "Distance Grab" ), Description( "桌面模式：隔空抓取 action。" )]
	public string DesktopGrabAction { get; set; } = "reload";

	public GameObject ActiveTarget { get; private set; }

	protected override void OnUpdate()
	{
		if ( !Grabber.IsValid() )
			return;
		if ( Grabber.IsHoldingObject )
		{
			ActiveTarget = null;
			return;
		}

		if ( IsGrabPressed() && ActiveTarget is null )
			ActiveTarget = FindBestTarget();
	}

	protected override void OnFixedUpdate()
	{
		if ( !ActiveTarget.IsValid() || !Grabber.IsValid() )
			return;

		if ( !VRGrabber.TryResolveRigidbody( ActiveTarget, out var rb ) || !rb.IsValid() )
		{
			ActiveTarget = null;
			return;
		}

		var grabPoint = Grabber.WorldPosition;
		var itemPos = ActiveTarget.WorldPosition;
		var dist = itemPos.Distance( grabPoint );

		if ( dist <= CatchDistance )
		{
			if ( Grabber.TryQueueExternalGrab( ActiveTarget ) )
				ActiveTarget = null;
			return;
		}

		rb.Velocity = DistanceGrabRules.ComputePullVelocity( itemPos, grabPoint, PullSpeed );
	}

	bool IsGrabPressed()
	{
		if ( Game.IsRunningInVR )
		{
			var ctl = Grabber.IsLeftHand ? Input.VR.LeftHand : Input.VR.RightHand;
			return ctl.Trigger.Value >= TriggerPressThreshold;
		}

		return Input.Down( DesktopGrabAction );
	}

	GameObject FindBestTarget()
	{
		var aim = ResolveAimTransform();
		GameObject best = null;
		var bestScore = float.MinValue;

		foreach ( var rb in Scene.GetAllComponents<Rigidbody>() )
		{
			if ( !rb.IsValid() || rb.GameObject == GameObject || rb.GameObject == Grabber.GameObject )
				continue;

			var toTarget = rb.WorldPosition - aim.Position;
			var distance = toTarget.Length;
			if ( distance <= 0.001f || distance > MaxDistance )
				continue;

			var dot = Vector3.Dot( toTarget.Normal, aim.Rotation.Forward );
			if ( dot < AcquireDotThreshold )
				continue;

			var score = DistanceGrabRules.ScoreTarget( distance, dot, MaxDistance );
			if ( score <= bestScore )
				continue;

			var tr = Scene.Trace
				.Ray( aim.Position, rb.WorldPosition )
				.IgnoreGameObjectHierarchy( GameObject.Parent )
				.Run();
			if ( tr.Hit && tr.GameObject.IsValid() && tr.GameObject != rb.GameObject )
				continue;

			bestScore = score;
			best = rb.GameObject;
		}

		return best;
	}

	Transform ResolveAimTransform()
	{
		if ( Game.IsRunningInVR )
		{
			var ctl = Grabber.IsLeftHand ? Input.VR.LeftHand : Input.VR.RightHand;
			return ctl.AimTransform;
		}

		if ( AimReference.IsValid() )
			return AimReference.WorldTransform;

		var cam = Scene.Camera;
		return cam.IsValid() ? cam.WorldTransform : Grabber.WorldTransform;
	}
}

