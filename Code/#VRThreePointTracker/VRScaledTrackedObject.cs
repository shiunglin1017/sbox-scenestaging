using System;
using Sandbox;
using Sandbox.VR;

namespace Sandbox;

/// <summary>
/// 自訂 VR 追蹤元件：以 Input.VR 原始姿態為基礎，套用比例縮放後再寫入 GameObject.Transform。
/// 目標是取代內建 VRTrackedObject，避免被其每幀覆寫世界座標而失去比例映射效果。
/// </summary>
public sealed class VRScaledTrackedObject : Component
{
	public enum PoseSources
	{
		Head,
		LeftHand,
		RightHand
	}

	public enum PoseTypes
	{
		Grip,
		Aim
	}

	[Flags]
	public enum TrackingTypes
	{
		Position = 1,
		Rotation = 2,
		All = Position | Rotation
	}

	/// <summary>追蹤來源（頭/左手/右手）。</summary>
	[Property, Group( "Tracking" )]
	public PoseSources PoseSource { get; set; } = PoseSources.Head;

	/// <summary>手把姿態模式（僅手把生效）：Grip=握把中心，Aim=指向姿態。</summary>
	[Property, Group( "Tracking" )]
	public PoseTypes PoseType { get; set; } = PoseTypes.Grip;

	/// <summary>要寫回哪些 Transform 分量。</summary>
	[Property, Group( "Tracking" )]
	public TrackingTypes TrackingType { get; set; } = TrackingTypes.All;

	/// <summary>渲染前再更新一次，避免邏輯幀與渲染幀位置不一致。</summary>
	[Property, Group( "Tracking" )]
	public bool UpdateInPreRender { get; set; } = true;

	/// <summary>
	/// 啟用後以父節點（通常是 VR_Tracking_Root）的世界等比縮放作為比例因子。
	/// 停用時改用 <see cref="ManualScaleFactor"/>。
	/// </summary>
	[Property, Group( "Scale" )]
	public bool UseParentScaleAsFactor { get; set; } = true;

	/// <summary>當 <see cref="UseParentScaleAsFactor"/> 關閉時使用的手動縮放係數。</summary>
	[Property, Group( "Scale" ), Range( 0.1f, 4f )]
	public float ManualScaleFactor { get; set; } = 1f;

	/// <summary>縮放樞紐點；留空時自動使用父節點的父節點（通常是 Player Controller）。</summary>
	[Property, Group( "Scale" )]
	public GameObject ScalePivotOverride { get; set; }

	[Property, ReadOnly, Group( "Debug (ReadOnly)" )]
	public float ActiveScaleFactor { get; private set; } = 1f;

	[Property, ReadOnly, Group( "Debug (ReadOnly)" )]
	public bool HasValidPoseThisFrame { get; private set; }

	protected override void OnUpdate()
	{
		if ( !Enabled || Scene.IsEditor || !Game.IsRunningInVR || IsProxy )
			return;

		UpdateTrackedPose();
	}

	protected override void OnPreRender()
	{
		if ( !Enabled || !UpdateInPreRender || Scene.IsEditor || !Game.IsRunningInVR || IsProxy )
			return;

		UpdateTrackedPose();
	}

	private void UpdateTrackedPose()
	{
		if ( !TryGetRawPose( out Transform rawPose ) )
		{
			HasValidPoseThisFrame = false;
			return;
		}

		HasValidPoseThisFrame = true;
		float scale = ResolveScaleFactor();
		Vector3 pivot = ResolvePivotWorldPosition();
		Vector3 scaledWorldPos = pivot + (rawPose.Position - pivot) * scale;

		if ( TrackingType.HasFlag( TrackingTypes.Position ) )
			GameObject.WorldPosition = scaledWorldPos;

		if ( TrackingType.HasFlag( TrackingTypes.Rotation ) )
			GameObject.WorldRotation = rawPose.Rotation;
	}

	private bool TryGetRawPose( out Transform pose )
	{
		pose = default;

		switch ( PoseSource )
		{
			case PoseSources.Head:
				pose = Input.VR.Head;
				return true;

			case PoseSources.LeftHand:
				if ( !Input.VR.LeftHand.Active )
					return false;

				pose = PoseType == PoseTypes.Aim ? Input.VR.LeftHand.AimTransform : Input.VR.LeftHand.Transform;
				return true;

			case PoseSources.RightHand:
				if ( !Input.VR.RightHand.Active )
					return false;

				pose = PoseType == PoseTypes.Aim ? Input.VR.RightHand.AimTransform : Input.VR.RightHand.Transform;
				return true;
		}

		return false;
	}

	private float ResolveScaleFactor()
	{
		float scale = ManualScaleFactor;
		if ( UseParentScaleAsFactor )
			scale = GameObject.Parent?.WorldScale.x ?? 1f;

		ActiveScaleFactor = MathF.Max( 0.01f, scale );
		return ActiveScaleFactor;
	}

	private Vector3 ResolvePivotWorldPosition()
	{
		if ( ScalePivotOverride is not null )
			return ScalePivotOverride.WorldPosition;

		var parent = GameObject.Parent;
		if ( parent?.Parent is not null )
			return parent.Parent.WorldPosition;

		return parent?.WorldPosition ?? Vector3.Zero;
	}
}
