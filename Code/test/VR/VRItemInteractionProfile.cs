using Sandbox;
using VRLogic;
using System.Collections.Generic;

/// <summary>
/// 可抓物／武器 prefab 的 **Inspector 集中設定**：質量預設、多握點與 ModelDoc attachment 後備、每點左右手姿勢提示（旋轉）、可選將主握點同步到 <see cref="Grabbable.GrabPivot"/>。
/// 執行期仍以 <see cref="VRGrabber"/> 為 Interactor；本元件為資料與 <see cref="Rigidbody"/> 預設套用，不重複關節邏輯。
/// </summary>
public sealed class VRItemInteractionProfile : Component
{
	[Property, Group( "物理" )]
	public VrPropMassPreset MassPreset { get; set; } = VrPropMassPreset.Medium;

	[Property, Group( "物理" ), Description( "僅在 MassPreset 為 Custom 時作為質量；否則忽略。" )]
	public float CustomMass { get; set; } = 8f;

	[Property, Group( "物理" ), Description( "為真時才以 Profile 覆寫 Rigidbody.MassOverride；否則保留 ModelDoc/Prefab 預設質量。" )]
	public bool OverrideMass { get; set; } = false;

	[Property, Group( "物理" ), Description( "非負時覆寫 Rigidbody 線性阻尼。" )]
	public float LinearDampingOverride { get; set; } = -1f;

	[Property, Group( "物理" ), Description( "非負時覆寫 Rigidbody 角阻尼。" )]
	public float AngularDampingOverride { get; set; } = -1f;

	[Property, Group( "物理" ), Description( "為真時才套用阻尼覆寫值；否則保留 ModelDoc/Prefab 阻尼設定。" )]
	public bool OverrideDamping { get; set; } = false;

	[Property, Group( "物理" ), Description( "非空白時以 Surface.FindByName 解析，並套用到本物件與啟用子階層之 Collider（與 ModelDoc 分工：此為 prefab 端覆寫）。" )]
	public string SurfaceResourceName { get; set; }

	[Property, Group( "物理" ), Description( "為真時才嘗試套用 Profile 的 Rigidbody 預設（仍受各 override 開關控制）；關閉則保留現有設定。" )]
	public bool ApplyRigidbodyDefaultsOnAwake { get; set; } = true;

	[Property, Group( "物理" ), Description( "為真時才以 SurfaceResourceName 覆寫 Collider.Surface；否則保留 ModelDoc/Prefab surface。" )]
	public bool OverrideSurface { get; set; } = false;

	[Property, Group( "抓取點" )]
	public List<VRItemGrabPointEntry> GrabPoints { get; set; } = new();

	[Property, Group( "抓取點" ), Description( "為真時將主握點條目之 Pivot 寫入同物件上的 Grabbable.GrabPivot（若存在）。" )]
	public bool SyncPrimaryPivotToGrabbable { get; set; } = true;

	protected override void OnAwake()
	{
		base.OnAwake();
		if ( ApplyRigidbodyDefaultsOnAwake && Components.TryGet<Rigidbody>( out var rb ) )
		{
			if ( OverrideMass )
			{
				var mass = VRItemInteractionProfileRules.ResolveMass( MassPreset, CustomMass );
				rb.MassOverride = mass;
			}

			if ( OverrideDamping && LinearDampingOverride >= 0f )
				rb.LinearDamping = LinearDampingOverride;
			if ( OverrideDamping && AngularDampingOverride >= 0f )
				rb.AngularDamping = AngularDampingOverride;
		}

		if ( OverrideSurface && !string.IsNullOrWhiteSpace( SurfaceResourceName ) )
		{
			var surf = Surface.FindByName( SurfaceResourceName.Trim() );
			if ( surf.IsValid() )
			{
				foreach ( var col in Components.GetAll<Collider>( FindMode.EnabledInSelfAndDescendants ) )
					col.Surface = surf;
			}
		}

		if ( !SyncPrimaryPivotToGrabbable || GrabPoints is not { Count: > 0 } )
			return;

		var specs = new VRItemGrabPointSpec[GrabPoints.Count];
		for ( var i = 0; i < GrabPoints.Count; i++ )
		{
			var e = GrabPoints[i];
			specs[i] = new VRItemGrabPointSpec( e.Priority, e.IsPrimary, e.AttachmentName ?? VrInteractionConstants.DefaultGripAttachmentName );
		}

		var idx = VRItemInteractionProfileRules.ResolvePrimaryGrabPointIndex( specs );
		if ( idx < 0 || idx >= GrabPoints.Count )
			return;

		var primary = GrabPoints[idx];
		if ( !primary.Pivot.IsValid() )
			return;

		if ( Components.TryGet<Grabbable>( out var gr ) )
			gr.GrabPivot = primary.Pivot;
	}
}

/// <summary>Inspector 條目：抓取對齊參考、attachment 名稱與姿勢提示。</summary>
public sealed class VRItemGrabPointEntry
{
	[Property, Description( "數字越小越優先（同為 Primary 時）。" )]
	public int Priority { get; set; }

	[Property]
	public bool IsPrimary { get; set; }

	[Property, Description( "與 ModelDoc attachment 一致；供關卡／程式對照，預設對齊 VRGrabber / 幽靈手慣例。" )]
	public string AttachmentName { get; set; } = VrInteractionConstants.DefaultGripAttachmentName;

	[Property, Description( "可選：此點的世界對齊參考（通常為子物件 Transform）。會在 SyncPrimaryPivotToGrabbable 時寫入 Grabbable.GrabPivot。" )]
	public GameObject Pivot { get; set; }

	[Property, Group( "姿勢提示" ), Description( "左手局部旋轉提示（Euler 度）；執行期僅文件化，可供日後餵 VRHand／AnimGraph。" )]
	public Angles LeftHandLocalAngles { get; set; }

	[Property, Group( "姿勢提示" ), Description( "右手局部旋轉提示（Euler 度）；執行期僅文件化。" )]
	public Angles RightHandLocalAngles { get; set; }
}
