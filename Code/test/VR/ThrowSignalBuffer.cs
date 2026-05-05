using Sandbox;
using System;
using System.Collections.Generic;

/// <summary>
/// 快取近期手部拋擲訊號（線速度/角速度），供釋放瞬間做峰值鄰域估算。
/// </summary>
public sealed class ThrowSignalBuffer
{
	public readonly struct Sample
	{
		public Sample( Vector3 linearVelocity, Vector3 angularVelocity, float timestamp )
		{
			LinearVelocity = linearVelocity;
			AngularVelocity = angularVelocity;
			Timestamp = timestamp;
		}

		public Vector3 LinearVelocity { get; }
		public Vector3 AngularVelocity { get; }
		public float Timestamp { get; }
	}

	readonly List<Sample> _samples = new();

	public void Push( Vector3 linearVelocity, Vector3 angularVelocity, float timestamp, int maxSamples )
	{
		_samples.Add( new Sample( linearVelocity, angularVelocity, timestamp ) );
		if ( _samples.Count <= maxSamples )
			return;

		var toRemove = _samples.Count - maxSamples;
		_samples.RemoveRange( 0, toRemove );
	}

	public bool TryEstimatePeakNeighborhoodAverage( int neighborhood, out Vector3 linearVelocity, out Vector3 angularVelocity )
	{
		linearVelocity = Vector3.Zero;
		angularVelocity = Vector3.Zero;
		if ( _samples.Count == 0 )
			return false;

		var peakIndex = 0;
		var peakSpeed = -1.0f;
		for ( var i = 0; i < _samples.Count; i++ )
		{
			var speed = _samples[i].LinearVelocity.LengthSquared;
			if ( speed <= peakSpeed )
				continue;
			peakSpeed = speed;
			peakIndex = i;
		}

		var start = Math.Max( 0, peakIndex - neighborhood );
		var end = Math.Min( _samples.Count - 1, peakIndex + neighborhood );
		var count = 0;
		for ( var i = start; i <= end; i++ )
		{
			linearVelocity += _samples[i].LinearVelocity;
			angularVelocity += _samples[i].AngularVelocity;
			count++;
		}

		if ( count <= 0 )
			return false;

		linearVelocity /= count;
		angularVelocity /= count;
		return true;
	}
}
