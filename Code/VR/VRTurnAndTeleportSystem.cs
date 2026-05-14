using Sandbox;
using VRLogic;

/// <summary>
/// VR 轉向與瞬移系統：提供 Snap/Smooth Turn、Arc Teleport 與舒適化輸出值。
/// </summary>
public sealed class VRTurnAndTeleportSystem : Component
{
	[Property, Group( "References" )]
	public GameObject PlayerRoot { get; set; }

	[Property, Group( "References" )]
	public GameObject HeadReference { get; set; }

	[Property, Group( "References" ), Description( "可選：讀取速度用於 vignette 強度；未指定時以 Root 位移估算。" )]
	public CharacterController CharacterController { get; set; }

	[Property, Group( "Turn" )]
	public bool UseSnapTurn { get; set; } = true;

	[Property, Group( "Turn" )]
	public float SnapTurnAngle { get; set; } = 45f;

	[Property, Group( "Turn" )]
	public float SnapTurnThreshold { get; set; } = 0.5f;

	[Property, Group( "Turn" )]
	public float SnapTurnResetThreshold { get; set; } = 0.2f;

	[Property, Group( "Turn" )]
	public float SmoothTurnSpeed { get; set; } = 120f;

	[Property, Group( "Teleport" )]
	public bool EnableTeleport { get; set; } = true;

	[Property, Group( "Teleport" ), Description( "預設用右手 A 鍵按住瞄準，放開時瞬移。" )]
	public string DesktopTeleportAction { get; set; } = "attack2";

	[Property, Group( "Teleport" )]
	public float TeleportLaunchSpeed { get; set; } = 350f;

	[Property, Group( "Teleport" )]
	public float TeleportStepTime { get; set; } = 0.03f;

	[Property, Group( "Teleport" )]
	public int TeleportMaxSteps { get; set; } = 36;

	[Property, Group( "Teleport" )]
	public float TeleportMaxDistance { get; set; } = 600f;

	[Property, Group( "Teleport" ), Description( "落點法線上向量內積低於此值視為不可瞬移（太陡）。" )]
	public float MinTeleportUpDot { get; set; } = 0.6f;

	[Property, Group( "Comfort" ), Description( "平面速度高於此值才開始增加 vignette。" )]
	public float ComfortStartSpeed { get; set; } = 40f;

	[Property, Group( "Comfort" ), Description( "平面速度達此值時 vignette = 1。" )]
	public float ComfortFullSpeed { get; set; } = 220f;

	public float ComfortStrength01 { get; private set; }
	public bool TeleportAimActive { get; private set; }
	public bool HasValidTeleportTarget { get; private set; }
	public Vector3 TeleportTargetPosition { get; private set; }
	public Vector3 TeleportTargetNormal { get; private set; }

	bool _canSnapTurn = true;
	Vector3 _lastRootPos;
	bool _wasTeleportHeld;

	protected override void OnStart()
	{
		base.OnStart();
		if ( !PlayerRoot.IsValid() )
			PlayerRoot = GameObject;
		if ( !HeadReference.IsValid() )
			HeadReference = GameObject;
		_lastRootPos = PlayerRoot.WorldPosition;
	}

	protected override void OnUpdate()
	{
		if ( !PlayerRoot.IsValid() )
			return;

		UpdateTurn();
		UpdateTeleport();
		UpdateComfort();
	}

	void UpdateTurn()
	{
		var axis = ReadTurnAxis();
		if ( UseSnapTurn )
		{
			if ( MathF.Abs( axis ) > SnapTurnThreshold && _canSnapTurn )
			{
				var yaw = axis > 0f ? SnapTurnAngle : -SnapTurnAngle;
				PlayerRoot.WorldRotation *= Rotation.FromYaw( yaw );
				_canSnapTurn = false;
			}
			else if ( MathF.Abs( axis ) < SnapTurnResetThreshold )
			{
				_canSnapTurn = true;
			}
			return;
		}

		if ( MathF.Abs( axis ) > 0.1f )
			PlayerRoot.WorldRotation *= Rotation.FromYaw( -axis * SmoothTurnSpeed * Time.Delta );
	}

	float ReadTurnAxis()
	{
		if ( Game.IsRunningInVR )
			return Input.VR.RightHand.Joystick.Value.x;
		return 0f;
	}

	void UpdateTeleport()
	{
		TeleportAimActive = false;
		HasValidTeleportTarget = false;

		if ( !EnableTeleport )
			return;

		var held = IsTeleportHeld();
		var released = _wasTeleportHeld && !held;
		_wasTeleportHeld = held;
		TeleportAimActive = held;

		if ( held && TryCalculateTeleportTarget( out var hitPos, out var hitNormal ) )
		{
			HasValidTeleportTarget = true;
			TeleportTargetPosition = hitPos;
			TeleportTargetNormal = hitNormal;
		}

		if ( released && HasValidTeleportTarget )
			ExecuteTeleport( TeleportTargetPosition );
	}

	bool IsTeleportHeld()
	{
		if ( Game.IsRunningInVR )
			return Input.VR.RightHand.ButtonA.IsPressed;
		return Input.Down( DesktopTeleportAction );
	}

	bool TryCalculateTeleportTarget( out Vector3 hitPos, out Vector3 hitNormal )
	{
		hitPos = default;
		hitNormal = default;

		var aim = GetTeleportAimTransform();
		var gravity = Scene.PhysicsWorld.Gravity;
		if ( gravity.LengthSquared <= 0.0001f )
			gravity = Vector3.Down * 800f;

		var found = TeleportArcRules.TryFindTeleportPoint(
			aim.Position,
			aim.Rotation.Forward * TeleportLaunchSpeed,
			gravity,
			TeleportStepTime,
			TeleportMaxSteps,
			TeleportMaxDistance,
			TraceArcStep,
			out hitPos,
			out hitNormal );

		if ( !found )
			return false;

		return Vector3.Dot( hitNormal, Vector3.Up ) >= MinTeleportUpDot;
	}

	SceneTraceResult TraceArcStep( Vector3 from, Vector3 to )
	{
		var tr = Scene.Trace
			.Ray( from, to )
			.IgnoreGameObjectHierarchy( PlayerRoot )
			.Run();
		return tr;
	}

	Transform GetTeleportAimTransform()
	{
		if ( Game.IsRunningInVR )
			return Input.VR.RightHand.AimTransform;

		var cam = Scene.Camera;
		if ( cam.IsValid() )
			return cam.WorldTransform;
		return HeadReference.WorldTransform;
	}

	void ExecuteTeleport( Vector3 target )
	{
		var headPos = HeadReference.IsValid() ? HeadReference.WorldPosition : PlayerRoot.WorldPosition;
		var planarOffset = headPos.WithZ( 0f ) - PlayerRoot.WorldPosition.WithZ( 0f );
		var destination = target - planarOffset;
		PlayerRoot.WorldPosition = destination.WithZ( target.z );
	}

	void UpdateComfort()
	{
		float speed;
		if ( CharacterController.IsValid() )
		{
			speed = CharacterController.Velocity.WithZ( 0f ).Length;
		}
		else
		{
			var now = PlayerRoot.WorldPosition;
			speed = (now - _lastRootPos).WithZ( 0f ).Length / MathF.Max( Time.Delta, 0.0001f );
			_lastRootPos = now;
		}

		ComfortStrength01 = TeleportArcRules.EvaluateComfortVignette( speed, ComfortStartSpeed, ComfortFullSpeed ).Clamp( 0f, 1f );
	}
}

