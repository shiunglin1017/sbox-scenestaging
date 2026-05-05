using Sandbox;
using VRLogic;
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

	/// <summary>Idle：無候選；Hovering：Trigger 內有物；Holding：已建立關節。</summary>
	public GrabInteractorState State { get; private set; } = GrabInteractorState.Idle;

	readonly HashSet<GameObject> _touchingObjects = new();
	GameObject _heldObject;
	FixedJoint _grabJoint;

	GameObject _pendingGrabTarget;
	bool _pendingRelease;
	Vector3 _releaseLinearVelocity;

	protected override void OnUpdate()
	{
		ReadGripAndThrowVelocity( out var grip, out var throwVelocity );
		var candidate = FindClosestValidCandidate();

		if ( GrabInteractionRules.ShouldStartGrab( grip, GripPressThreshold, _heldObject is not null, candidate is not null ) )
			_pendingGrabTarget = candidate;

		if ( GrabInteractionRules.ShouldReleaseGrab( grip, GripReleaseThreshold, _heldObject is not null ) )
		{
			_pendingRelease = true;
			_releaseLinearVelocity = throwVelocity;
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
			ReadGripAndThrowVelocity( out var grip, out _ );
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
	void ReadGripAndThrowVelocity( out float grip, out Vector3 throwVelocity )
	{
		if ( Game.IsRunningInVR )
		{
			var ctl = IsLeftHand ? Input.VR.LeftHand : Input.VR.RightHand;
			grip = ctl.Grip.Value;
			throwVelocity = ctl.Velocity;
			return;
		}

		var action = IsLeftHand ? PcGripActionLeft : PcGripActionRight;
		grip = Input.Down( action ) ? 1f : 0f;
		throwVelocity = Vector3.Zero;
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

		if ( HandRenderer is not null && !string.IsNullOrEmpty( AttachmentName ) )
		{
			var attachmentTx = HandRenderer.GetAttachment( AttachmentName );

			if ( attachmentTx.HasValue )
			{
				obj.Transform.Position = attachmentTx.Value.Position;
				obj.Transform.Rotation = attachmentTx.Value.Rotation;

				if ( TryResolveRigidbody( obj, out var rb ) )
				{
					rb.Velocity = Vector3.Zero;
					rb.AngularVelocity = Vector3.Zero;
				}
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
			rb.AngularVelocity = Vector3.Zero;
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
}

/// <summary>VR 抓取 Interactor 高階狀態（細節仍以物理與 Trigger 為準）。</summary>
public enum GrabInteractorState
{
	Idle,
	Hovering,
	Holding
}
