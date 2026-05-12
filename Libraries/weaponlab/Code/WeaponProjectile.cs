using Sandbox;
using System;

/// <summary>
/// 輕量 projectile：每幀以前進線段做 trace 命中檢測，命中後回呼 impact resolver。
/// </summary>
public sealed class WeaponProjectile : Component
{
	Vector3 _velocity;
	float _gravity;
	float _lifetime;
	GameObject _ignoreRoot;
	Action<SceneTraceResult> _onImpact;
	TimeSince _sinceSpawn;

	public static GameObject Spawn(
		Scene scene,
		Vector3 origin,
		Vector3 velocity,
		float gravity,
		float lifetime,
		GameObject ignoreRoot,
		Action<SceneTraceResult> onImpact )
	{
		var go = scene.CreateObject();
		go.WorldPosition = origin;

		var p = go.Components.Create<WeaponProjectile>();
		p._velocity = velocity;
		p._gravity = gravity;
		p._lifetime = MathF.Max( 0.1f, lifetime );
		p._ignoreRoot = ignoreRoot;
		p._onImpact = onImpact;
		p._sinceSpawn = 0;
		return go;
	}

	protected override void OnUpdate()
	{
		var dt = Time.Delta;
		var start = WorldPosition;
		var end = start + _velocity * dt;

		var trace = Scene.Trace.Ray( start, end );
		if ( _ignoreRoot.IsValid() )
			trace = trace.IgnoreGameObjectHierarchy( _ignoreRoot );
		var tr = trace.Run();
		if ( tr.Hit )
		{
			_onImpact?.Invoke( tr );
			GameObject.Destroy();
			return;
		}

		WorldPosition = end;
		_velocity += Vector3.Down * _gravity * dt;

		if ( _sinceSpawn > _lifetime )
			GameObject.Destroy();
	}
}
