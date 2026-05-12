namespace VRLogic;

/// <summary>
/// Teleport 與舒適化的純規則，避免元件直接內嵌數學邏輯。
/// </summary>
public static class TeleportArcRules
{
	public static float EvaluateComfortVignette( float planarSpeed, float startSpeed, float fullSpeed )
	{
		if ( fullSpeed <= startSpeed )
			return planarSpeed > startSpeed ? 1f : 0f;
		if ( planarSpeed <= startSpeed )
			return 0f;
		if ( planarSpeed >= fullSpeed )
			return 1f;
		return (planarSpeed - startSpeed) / (fullSpeed - startSpeed);
	}

	public static bool TryFindTeleportPoint(
		Vector3 origin,
		Vector3 initialVelocity,
		Vector3 gravity,
		float stepTime,
		int maxSteps,
		float maxDistance,
		Func<Vector3, Vector3, SceneTraceResult> traceStep,
		out Vector3 hitPoint,
		out Vector3 hitNormal )
	{
		hitPoint = default;
		hitNormal = default;

		if ( stepTime <= 0f || maxSteps <= 0 || maxDistance <= 0f || traceStep is null )
			return false;

		var velocity = initialVelocity;
		var from = origin;
		var travelled = 0f;

		for ( var i = 0; i < maxSteps; i++ )
		{
			var to = from + velocity * stepTime;
			var tr = traceStep( from, to );
			if ( tr.Hit )
			{
				hitPoint = tr.HitPosition;
				hitNormal = tr.Normal;
				return true;
			}

			travelled += from.Distance( to );
			if ( travelled >= maxDistance )
				return false;

			velocity += gravity * stepTime;
			from = to;
		}

		return false;
	}
}

