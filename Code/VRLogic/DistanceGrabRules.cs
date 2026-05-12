namespace VRLogic;

/// <summary>
/// Distance grab 純規則：候選評分與吸附速度計算。
/// </summary>
public static class DistanceGrabRules
{
	public static float ScoreTarget( float distance, float alignmentDot, float maxDistance )
	{
		if ( maxDistance <= 0f || distance > maxDistance || alignmentDot <= 0f )
			return float.MinValue;

		var normalizedDistance = 1f - (distance / maxDistance).Clamp( 0f, 1f );
		return normalizedDistance * 0.6f + alignmentDot.Clamp( 0f, 1f ) * 0.4f;
	}

	public static Vector3 ComputePullVelocity( Vector3 from, Vector3 to, float pullSpeed )
	{
		var delta = to - from;
		if ( delta.LengthSquared <= 0.0001f )
			return Vector3.Zero;
		return delta.Normal * MathF.Max( 0f, pullSpeed );
	}
}

