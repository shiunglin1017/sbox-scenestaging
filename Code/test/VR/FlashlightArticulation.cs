using Sandbox;

/// <summary>
/// 副手手電筒對齊控制：副手進入穩定區時，手電筒以球關節風格平滑轉向主手武器。
/// </summary>
public sealed class FlashlightArticulation : Component
{
	public enum WeaponPosePreset
	{
		Pistol,
		Shotgun,
		Smg,
		Custom
	}

	[Property, Group( "Targets" ), Description( "主手武器或手部朝向來源。" )]
	public GameObject MainHandAimSource { get; set; }

	[Property, Group( "Targets" ), Description( "副手參考點（通常是副手 wrist 或 hand root）。" )]
	public GameObject OffhandReference { get; set; }

	[Property, Group( "Activation" ), Description( "副手距主手小於此距離時啟動自動對齊。" )]
	public float EngageDistance { get; set; } = 20.0f;

	[Property, Group( "Activation" ), Description( "副手離開此距離時解除對齊（可避免邊界抖動）。" )]
	public float DisengageDistance { get; set; } = 26.0f;

	[Property, Group( "Drive" ), Description( "旋轉追隨速度。" )]
	public float RotationLerpSpeed { get; set; } = 10.0f;

	[Property, Group( "Pose" )]
	public WeaponPosePreset PosePreset { get; set; } = WeaponPosePreset.Pistol;

	[Property, Group( "Pose" ), Description( "當 PosePreset=Custom 時使用。Pitch/Yaw/Roll。" )]
	public Angles CustomPoseOffset { get; set; } = Angles.Zero;

	bool _isEngaged;

	protected override void OnUpdate()
	{
		if ( !MainHandAimSource.IsValid() || !OffhandReference.IsValid() )
			return;

		var distance = (OffhandReference.WorldPosition - MainHandAimSource.WorldPosition).Length;
		if ( !_isEngaged && distance <= EngageDistance )
			_isEngaged = true;
		else if ( _isEngaged && distance >= DisengageDistance )
			_isEngaged = false;

		if ( !_isEngaged )
			return;

		var baseRotation = MainHandAimSource.WorldRotation;
		var presetOffset = Rotation.From( ResolvePoseOffset() );
		var targetRotation = baseRotation * presetOffset;
		WorldRotation = Rotation.Slerp( WorldRotation, targetRotation, Time.Delta * RotationLerpSpeed );
	}

	Angles ResolvePoseOffset()
	{
		return PosePreset switch
		{
			WeaponPosePreset.Pistol => new Angles( 4f, 0f, 0f ),
			WeaponPosePreset.Shotgun => new Angles( 9f, -6f, 0f ),
			WeaponPosePreset.Smg => new Angles( 6f, -3f, 0f ),
			_ => CustomPoseOffset
		};
	}
}
