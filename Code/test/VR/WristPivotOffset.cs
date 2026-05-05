using Sandbox;

/// <summary>
/// 將控制器追蹤姿態映射到手腕 pivot，避免物理力矩從掌心發力造成揮動不自然。
/// </summary>
public sealed class WristPivotOffset : Component
{
	[Property, Group( "Source" ), Description( "控制器或手部來源（通常為 VRTrackedObject）。" )]
	public GameObject TransformSource { get; set; }

	[Property, Group( "Offset" ), Description( "來源局部空間中的手腕位移補償。" )]
	public Vector3 LocalPositionOffset { get; set; } = new( 0f, -3.0f, -1.5f );

	[Property, Group( "Offset" ), Description( "來源局部空間中的手腕旋轉補償。" )]
	public Angles LocalRotationOffset { get; set; } = Angles.Zero;

	[Property, Group( "Sync" ), Description( "為真時在 FixedUpdate 套用，便於與物理步對齊。" )]
	public bool ApplyInFixedUpdate { get; set; }

	protected override void OnUpdate()
	{
		if ( !ApplyInFixedUpdate )
			ApplyOffset();
	}

	protected override void OnFixedUpdate()
	{
		if ( ApplyInFixedUpdate )
			ApplyOffset();
	}

	void ApplyOffset()
	{
		if ( !TransformSource.IsValid() )
			return;

		var src = TransformSource.WorldTransform;
		var rotOffset = Rotation.From( LocalRotationOffset );
		WorldPosition = src.PointToWorld( LocalPositionOffset );
		WorldRotation = src.Rotation * rotOffset;
	}
}
