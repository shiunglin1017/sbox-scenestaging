using Sandbox;
using System;
using VRLogic;

/// <summary>
/// 手部 root 穩定器：確保 root 具有穩定碰撞體，並以 Slerp 平滑追隨目標旋轉。
/// </summary>
public sealed class VRRootColliderStabilizer : Component
{
	[Property, Group( "Follow" ), Description( "旋轉追隨來源（可指向 WristPivotOffset 或 GhostHandTarget）。" )]
	public GameObject RotationSource { get; set; }

	[Property, Group( "Follow" ), Description( "旋轉平滑速度（越大越緊跟）。" )]
	public float RotationLerpSpeed { get; set; } = 18.0f;

	[Property, Group( "Follow" ), Description( "每秒最大角速度（度），避免突發翻腕。" )]
	public float MaxDegreesPerSecond { get; set; } = 540.0f;

	[Property, Group( "Root Collider" ), Description( "若 root 沒有 Collider，會自動建立 SphereCollider。" )]
	public bool EnsureRootCollider { get; set; } = true;

	[Property, Group( "Root Collider" ), Description( "自動建立 root collider 時使用的半徑。" )]
	public float RootColliderRadius { get; set; } = 3.5f;

	protected override void OnAwake()
	{
		base.OnAwake();
		if ( EnsureRootCollider )
			EnsureCollider();
	}

	protected override void OnUpdate()
	{
		if ( !RotationSource.IsValid() )
			return;

		var target = RotationSource.WorldRotation;
		var currentForward = WorldRotation.Forward.Normal;
		var targetForward = target.Forward.Normal;
		var dot = Math.Clamp( Vector3.Dot( currentForward, targetForward ), -1.0f, 1.0f );
		var angleDeg = MathF.Acos( dot ) * 57.29578f;
		var baseT = Time.Delta * RotationLerpSpeed;
		var t = RotationClampRules.ClampInterpolationBySpeed( angleDeg, baseT, MaxDegreesPerSecond, Time.Delta );
		WorldRotation = Rotation.Slerp( WorldRotation, target, t );
	}

	void EnsureCollider()
	{
		var collider = Components.Get<Collider>();
		if ( collider.IsValid() )
			return;

		var sphere = Components.Create<SphereCollider>();
		sphere.Radius = RootColliderRadius;
		sphere.IsTrigger = false;
	}
}
