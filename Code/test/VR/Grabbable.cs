using Sandbox;
using System;

/// <summary>
/// 標記可被抓取的物件，並可選擇明確指定或快取要使用的 <see cref="Rigidbody"/>，供 <see cref="VRGrabber"/> 等系統以混合解析讀取。
/// </summary>
public sealed class Grabbable : Component
{
    [Property, Description( "可選：手動指定抓取時要驅動的 Rigidbody；未指定則在本物件與子階層尋找一次並快取。" )]
    public Rigidbody OverrideBody { get; set; }

    [Property, Group( "Grab Pose" ), Description( "可選：物件明確抓取點。若設定，抓取會優先將此點對齊到手部抓取姿態。" )]
    public GameObject GrabPivot { get; set; }

    [Property, Group( "Grab Pose" ), Description( "無 GrabPivot 時，邊緣吸附所使用的估計半徑（世界單位）。" )]
    public float FallbackEdgeRadius { get; set; } = 4.0f;

    private Rigidbody _resolved;

    protected override void OnAwake()
    {
        base.OnAwake();
        RefreshResolved();
    }

    protected override void OnEnabled()
    {
        base.OnEnabled();
        RefreshResolved();
    }

    void RefreshResolved()
    {
        if ( OverrideBody.IsValid() )
            _resolved = OverrideBody;
        else
            _resolved = Components.Get<Rigidbody>( FindMode.EnabledInSelfAndDescendants );
    }

    /// <summary>
    /// 取得 <see cref="OnAwake"/> / <see cref="OnEnabled"/> 時快取的剛體。
    /// </summary>
    public bool TryGetBody( out Rigidbody rb )
    {
        rb = _resolved;
        return rb.IsValid();
    }

    public bool TryGetPivotAlignedPose( Transform handPose, out Transform targetObjectPose )
    {
        targetObjectPose = default;

        if ( !GrabPivot.IsValid() )
            return false;

        var objectToPivotLocal = GameObject.WorldTransform.PointToLocal( GrabPivot.WorldPosition );
        var pivotLocalRotation = GameObject.WorldRotation.Inverse * GrabPivot.WorldRotation;

        var targetRotation = handPose.Rotation * pivotLocalRotation.Inverse;
        var targetPosition = handPose.Position - (targetRotation * objectToPivotLocal);
        targetObjectPose = new Transform( targetPosition, targetRotation );
        return true;
    }

    public Transform ComputeEdgeFallbackPose( Transform handPose )
    {
        var toObject = GameObject.WorldPosition - handPose.Position;
        var fallbackDirection = toObject.LengthSquared > 0.0001f ? toObject.Normal : -handPose.Rotation.Forward;
        var edgeRadius = EstimateEdgeRadius();
        var targetPosition = handPose.Position + fallbackDirection * edgeRadius;
        return new Transform( targetPosition, handPose.Rotation );
    }

    float EstimateEdgeRadius()
    {
        var renderer = Components.Get<ModelRenderer>( FindMode.EnabledInSelfAndDescendants );
        if ( renderer.IsValid() )
        {
            var bounds = renderer.Bounds;
            var extents = (bounds.Maxs - bounds.Mins) * 0.5f;
            var minExtent = MathF.Min( extents.x, MathF.Min( extents.y, extents.z ) );
            if ( minExtent > 0.01f )
                return minExtent;
        }

        return FallbackEdgeRadius;
    }
}
