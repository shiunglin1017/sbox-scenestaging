using Sandbox;
using VRLogic;
using System;
using System.Collections.Generic;

/// <summary>
/// VR 手部抓取（Interactor）：Hover 由 Trigger 驅動；Attach／Release 在 <see cref="OnFixedUpdate"/> 執行以對齊物理步。
/// 可選用 <see cref="SkinnedModelRenderer.GetAttachment"/> 對齊 ModelDoc attachment，再建立 <see cref="FixedJoint"/>。
/// </summary>
/// <remarks>
/// 編輯器設定檢核：見原類別註解；<see cref="AttachmentName"/> 預設與 <see cref="VrInteractionConstants.DefaultGripAttachmentName"/> 一致。
/// 桌面模式（非 VR）：勿使用 <see cref="Game.IsRunningInVR"/> 為假時的 <c>Input.VR</c>；以 <see cref="PcGripActionLeft"/>／<see cref="PcGripActionRight"/>（預設 attack1／attack2）類比左右手 Grip，放手時線速度為零。
/// </remarks>
public sealed class VRGrabber : Component, Component.ITriggerListener
{
	[Property, Group( "VR 設定" ), Description( "勾選表示此元件掛在左手；未勾選表示右手。" )]
	public bool IsLeftHand { get; set; }

	[Property, Group( "吸附設定" )]
	public SkinnedModelRenderer HandRenderer { get; set; }

	[Property, Group( "吸附設定" ), Description( "ModelDoc attachment 名稱；須與模型完全一致（含大小寫）。" )]
	public string AttachmentName { get; set; } = VrInteractionConstants.DefaultGripAttachmentName;

	[Property, Group( "輸入" )]
	public float GripPressThreshold { get; set; } = VrInteractionConstants.DefaultGripPressThreshold;

	[Property, Group( "輸入" )]
	public float GripReleaseThreshold { get; set; } = VrInteractionConstants.DefaultGripReleaseThreshold;

	[Property, Group( "輸入" ), Description( "桌面模式：左手 Grip 對應的 Input action（預設滑鼠左鍵／attack1）。" )]
	public string PcGripActionLeft { get; set; } = "attack1";

	[Property, Group( "輸入" ), Description( "桌面模式：右手 Grip 對應的 Input action（預設滑鼠右鍵／attack2）。" )]
	public string PcGripActionRight { get; set; } = "attack2";

	[Property, Group( "抓取條件" ), Description( "手部抓取點與候選物中心的最大距離；即使 Trigger 進入，超過此距離也不抓取。" )]
	public float MaxGrabDistance { get; set; } = 12f;

	[Property, Group( "抓取條件" ), Description( "抓取瞬間是否清空物件線速度與角速度，降低關節建立時的抖動。" )]
	public bool ResetVelocityOnGrab { get; set; } = true;

	[Property, Group( "投擲估算" ), Description( "釋放時使用近期速度訊號做峰值鄰域平均，而非只取當下瞬間速度。" )]
	public bool UseThrowSignalEstimator { get; set; } = true;

	[Property, Group( "投擲估算" ), Description( "保留最近 N 筆手部速度樣本。" )]
	public int ThrowSignalSampleCount { get; set; } = AlyxFeelTuningDefaults.ThrowSignalSampleCount;

	[Property, Group( "投擲估算" ), Description( "峰值前後取樣的鄰域大小；3 表示峰值前後各 3 筆。" )]
	public int ThrowPeakNeighborhood { get; set; } = AlyxFeelTuningDefaults.ThrowPeakNeighborhood;

	[Property, Group( "投擲估算" ), Description( "放手線速度上限（世界單位/秒）。" )]
	public float MaxReleaseLinearSpeed { get; set; } = AlyxFeelTuningDefaults.ThrowMaxLinearSpeed;

	[Property, Group( "投擲估算" ), Description( "放手角速度上限（弧度/秒）。" )]
	public float MaxReleaseAngularSpeed { get; set; } = AlyxFeelTuningDefaults.ThrowMaxAngularSpeed;

	[Property, Group( "投擲估算" ), Description( "為真時放手沿用估算角速度；否則維持既有行為（角速度歸零）。" )]
	public bool PreserveReleaseAngularVelocity { get; set; } = true;

	/// <summary>Idle：無候選；Hovering：Trigger 內有物；Holding：已建立關節。</summary>
	public GrabInteractorState State { get; private set; } = GrabInteractorState.Idle;

	readonly HashSet<GameObject> _touchingObjects = new();
	GameObject _heldObject;
	FixedJoint _grabJoint;

	GameObject _pendingGrabTarget;
	bool _pendingRelease;
	Vector3 _releaseLinearVelocity;
	Vector3 _releaseAngularVelocity;
	readonly ThrowSignalBuffer _throwSignalBuffer = new();

	protected override void OnUpdate()
	{
		ReadGripAndThrowVelocity( out var grip, out var throwVelocity, out var throwAngularVelocity );
		_throwSignalBuffer.Push(
			throwVelocity,
			throwAngularVelocity,
			Time.Now,
			Math.Max( 1, ThrowSignalSampleCount ) );
		var candidate = FindClosestValidCandidate();

		if ( GrabInteractionRules.ShouldStartGrab( grip, GripPressThreshold, _heldObject is not null, candidate is not null ) )
			_pendingGrabTarget = candidate;

		if ( GrabInteractionRules.ShouldReleaseGrab( grip, GripReleaseThreshold, _heldObject is not null ) )
		{
			_pendingRelease = true;
			_releaseLinearVelocity = throwVelocity;
			_releaseAngularVelocity = throwAngularVelocity;
			if ( UseThrowSignalEstimator &&
				ThrowEstimator.TryEstimate(
					_throwSignalBuffer,
					Math.Max( 0, ThrowPeakNeighborhood ),
					MaxReleaseLinearSpeed,
					MaxReleaseAngularSpeed,
					out var estimatedLinear,
					out var estimatedAngular ) )
			{
				_releaseLinearVelocity = estimatedLinear;
				_releaseAngularVelocity = estimatedAngular;
			}
		}

		UpdatePresentationState();
	}

	protected override void OnFixedUpdate()
	{
		if ( _pendingRelease )
		{
			_pendingGrabTarget = null;
			_pendingRelease = false;
			ReleaseObjectInternal();
		}

		if ( _pendingGrabTarget is not null && _heldObject is null )
		{
			ReadGripAndThrowVelocity( out var grip, out _, out _ );
			if ( grip < GripPressThreshold )
			{
				_pendingGrabTarget = null;
				return;
			}

			var target = _pendingGrabTarget;
			_pendingGrabTarget = null;
			GrabObjectInternal( target );
		}
	}

	/// <summary>VR：實際 Grip 與手部線速度；桌面：類比 Grip（0／1）與零速度。</summary>
	void ReadGripAndThrowVelocity( out float grip, out Vector3 throwVelocity, out Vector3 throwAngularVelocity )
	{
		if ( Game.IsRunningInVR )
		{
			var ctl = IsLeftHand ? Input.VR.LeftHand : Input.VR.RightHand;
			grip = ctl.Grip.Value;
			throwVelocity = ctl.Velocity;
			var angular = ctl.AngularVelocity;
			throwAngularVelocity = new Vector3( angular.pitch, angular.yaw, angular.roll );
			return;
		}

		var action = IsLeftHand ? PcGripActionLeft : PcGripActionRight;
		grip = Input.Down( action ) ? 1f : 0f;
		throwVelocity = Vector3.Zero;
		throwAngularVelocity = Vector3.Zero;
	}

	void UpdatePresentationState()
	{
		if ( _heldObject is not null )
			State = GrabInteractorState.Holding;
		else if ( FindClosestValidCandidate() is not null )
			State = GrabInteractorState.Hovering;
		else
			State = GrabInteractorState.Idle;
	}

	GameObject FindClosestValidCandidate()
	{
		_touchingObjects.RemoveWhere( go => go is null || !go.IsValid() );
		if ( _touchingObjects.Count == 0 )
			return null;

		var grabPoint = ResolveGrabPoint();
		var maxDistanceSq = MaxGrabDistance * MaxGrabDistance;
		GameObject best = null;
		float bestDistanceSq = float.MaxValue;

		foreach ( var go in _touchingObjects )
		{
			if ( !go.IsValid() || !TryResolveRigidbody( go, out _ ) )
				continue;

			var d2 = (go.WorldPosition - grabPoint).LengthSquared;
			if ( !GrabInteractionRules.WithinGrabDistanceSquared( d2, maxDistanceSq ) )
				continue;

			if ( d2 < bestDistanceSq )
			{
				bestDistanceSq = d2;
				best = go;
			}
		}

		return best;
	}

	Vector3 ResolveGrabPoint()
	{
		if ( HandRenderer is not null && !string.IsNullOrEmpty( AttachmentName ) )
		{
			var attachmentTx = HandRenderer.GetAttachment( AttachmentName );
			if ( attachmentTx.HasValue )
				return attachmentTx.Value.Position;
		}

		return WorldPosition;
	}

	public static bool TryResolveRigidbody( GameObject root, out Rigidbody rb )
	{
		if ( root.Components.TryGet<Grabbable>( out var grabbable ) && grabbable.TryGetBody( out rb ) )
			return true;

		if ( root.Components.TryGet<Rigidbody>( out rb ) && rb.IsValid() )
			return true;

		rb = root.Components.Get<Rigidbody>( FindMode.EnabledInSelfAndDescendants );
		return rb.IsValid();
	}

	void GrabObjectInternal( GameObject obj )
	{
		if ( obj is null || !obj.IsValid() )
			return;

		_heldObject = obj;

		if ( ComputeGrabPose( obj, out var targetObjectPose ) )
		{
			obj.Transform.Position = targetObjectPose.Position;
			obj.Transform.Rotation = targetObjectPose.Rotation;
			if ( ResetVelocityOnGrab && TryResolveRigidbody( obj, out var rb ) )
			{
				rb.Velocity = Vector3.Zero;
				rb.AngularVelocity = Vector3.Zero;
			}
		}

		_grabJoint = Components.Create<FixedJoint>();
		_grabJoint.Body = obj;
	}

	void ReleaseObjectInternal()
	{
		var released = _heldObject;

		_grabJoint?.Destroy();
		_grabJoint = null;

		if ( released is not null && released.IsValid() && TryResolveRigidbody( released, out var rb ) )
		{
			rb.Velocity = AlyxFeelTuningDefaults.PreferHandLinearVelocityOnRelease ? _releaseLinearVelocity : rb.Velocity;
			rb.AngularVelocity = PreserveReleaseAngularVelocity ? _releaseAngularVelocity : Vector3.Zero;
		}

		if ( released is not null && released.IsValid() )
			GripReleaseNotification.Publish?.Invoke( Scene, released );

		_heldObject = null;
	}

	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		if ( TryResolveRigidbody( other.GameObject, out _ ) )
			_touchingObjects.Add( other.GameObject );
	}

	void ITriggerListener.OnTriggerExit( Collider other )
	{
		_touchingObjects.Remove( other.GameObject );
	}

	bool ComputeGrabPose( GameObject obj, out Transform objectPose )
	{
		objectPose = obj.WorldTransform;

		var handPose = ResolveHandPose();
		var grabbable = obj.Components.Get<Grabbable>( FindMode.EnabledInSelfAndDescendants );

		if ( grabbable.IsValid() && grabbable.TryGetPivotAlignedPose( handPose, out objectPose ) )
			return true;

		if ( grabbable.IsValid() )
		{
			objectPose = grabbable.ComputeEdgeFallbackPose( handPose );
			return true;
		}

		var toObject = obj.WorldPosition - handPose.Position;
		var fallbackDirection = toObject.LengthSquared > 0.0001f ? toObject.Normal : -handPose.Rotation.Forward;
		objectPose = new Transform( handPose.Position + fallbackDirection * 4.0f, handPose.Rotation );
		return true;
	}

	Transform ResolveHandPose()
	{
		if ( HandRenderer is not null && !string.IsNullOrEmpty( AttachmentName ) )
		{
			var attachmentTx = HandRenderer.GetAttachment( AttachmentName );
			if ( attachmentTx.HasValue )
				return new Transform( attachmentTx.Value.Position, attachmentTx.Value.Rotation );
		}

		return WorldTransform;
	}
}

/// <summary>VR 抓取 Interactor 高階狀態（細節仍以物理與 Trigger 為準）。</summary>
public enum GrabInteractorState
{
	Idle,
	Hovering,
	Holding
}
