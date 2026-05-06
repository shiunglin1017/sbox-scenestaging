using System;

namespace VRLogic;

/// <summary>
/// 旋轉步進限制規則：依角差與每秒最大角速度限制本幀可用的插值係數。
/// </summary>
public static class RotationClampRules
{
	public static float ClampInterpolationBySpeed( float angleDeg, float baseT, float maxDegreesPerSecond, float deltaTime )
	{
		var t = Math.Clamp( baseT, 0.0f, 1.0f );
		if ( maxDegreesPerSecond <= 0.0f )
			return t;

		if ( angleDeg <= 0.001f || deltaTime <= 0.0f )
			return t;

		var maxStepDeg = maxDegreesPerSecond * deltaTime;
		var maxTBySpeed = maxStepDeg / angleDeg;
		return Math.Clamp( MathF.Min( t, maxTBySpeed ), 0.0f, 1.0f );
	}
}
