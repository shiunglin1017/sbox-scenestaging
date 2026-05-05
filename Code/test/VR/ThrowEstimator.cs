using Sandbox;

/// <summary>
/// 將手部訊號轉為釋放速度：峰值鄰域平均 + 速度上限。
/// </summary>
public static class ThrowEstimator
{
	public static Vector3 ClampMagnitude( Vector3 value, float maxMagnitude )
	{
		if ( maxMagnitude <= 0.0f )
			return Vector3.Zero;

		var length = value.Length;
		if ( length <= maxMagnitude )
			return value;
		if ( length < 0.0001f )
			return Vector3.Zero;

		return value / length * maxMagnitude;
	}

	public static bool TryEstimate(
		ThrowSignalBuffer buffer,
		int neighborhood,
		float maxLinearSpeed,
		float maxAngularSpeed,
		out Vector3 releaseLinearVelocity,
		out Vector3 releaseAngularVelocity )
	{
		releaseLinearVelocity = Vector3.Zero;
		releaseAngularVelocity = Vector3.Zero;

		if ( !buffer.TryEstimatePeakNeighborhoodAverage( neighborhood, out var linear, out var angular ) )
			return false;

		releaseLinearVelocity = ClampMagnitude( linear, maxLinearSpeed );
		releaseAngularVelocity = ClampMagnitude( angular, maxAngularSpeed );
		return true;
	}
}
