using Sandbox;
using System.Collections.Generic;

/// <summary>
/// 將場景中的 cloud prop（或一般模型物件）補齊為可被 VRGrabber 使用的最小組件集合：
/// Rigidbody + Collider + Grabbable。
/// </summary>
public sealed class AutoPropAdapterSystem : Component
{
	[Property, Group( "掃描" ), Description( "僅處理帶 prop tag 的物件。" )]
	public bool RequirePropTag { get; set; } = true;

	[Property, Group( "掃描" ), Description( "是否持續增量掃描（避免只在開場生效）。" )]
	public bool IncrementalScan { get; set; } = true;

	[Property, Group( "掃描" ), Description( "增量掃描間隔秒數。" ), Range( 0.1f, 10f ), Step( 0.1f )]
	public float RescanInterval { get; set; } = 1.0f;

	[Property, Group( "掃描" ), Description( "每次更新最多處理幾個候選，避免尖峰。" ), Range( 1, 512 )]
	public int MaxProcessPerTick { get; set; } = 32;

	[Property, Group( "排除" ), Description( "帶這些 tag 的物件不自動適配（逗號分隔，例如 player,nograb,staticprop）。" )]
	public string ExcludedTagsCsv { get; set; } = "player,nograb";

	[Property, Group( "可選" ), Description( "為真時若缺 VRItemInteractionProfile 也會補上（預設保持 false，避免覆寫物理策略）。" )]
	public bool AddInteractionProfile { get; set; }

	TimeSince _sinceScan;
	readonly Queue<GameObject> _pending = new();
	readonly HashSet<GameObject> _known = new();
	string[] _excludedTags = [];

	protected override void OnAwake()
	{
		base.OnAwake();
		_excludedTags = ParseExcludedTags();
		EnqueueCandidates();
	}

	protected override void OnUpdate()
	{
		if ( IncrementalScan && _sinceScan >= RescanInterval )
		{
			_sinceScan = 0;
			EnqueueCandidates();
		}

		var budget = MaxProcessPerTick;
		while ( budget-- > 0 && _pending.Count > 0 )
		{
			var go = _pending.Dequeue();
			if ( !go.IsValid() || !ShouldAdapt( go ) )
				continue;

			Adapt( go );
		}
	}

	void EnqueueCandidates()
	{
		foreach ( var renderer in Scene.GetAllComponents<ModelRenderer>() )
		{
			var go = renderer.GameObject;
			if ( !go.IsValid() || _known.Contains( go ) )
				continue;

			_known.Add( go );
			_pending.Enqueue( go );
		}
	}

	bool ShouldAdapt( GameObject go )
	{
		if ( RequirePropTag && !go.Tags.Has( "prop" ) )
			return false;

		foreach ( var tag in _excludedTags )
		{
			if ( !string.IsNullOrWhiteSpace( tag ) && go.Tags.Has( tag ) )
				return false;
		}

		return true;
	}

	void Adapt( GameObject go )
	{
		var rb = go.Components.Get<Rigidbody>( FindMode.EnabledInSelfAndDescendants );
		if ( !rb.IsValid() )
			rb = go.Components.Create<Rigidbody>();

		var hasCollider = go.Components.Get<Collider>( FindMode.EnabledInSelfAndDescendants ).IsValid();
		if ( !hasCollider )
		{
			var box = go.Components.Create<BoxCollider>();
			var renderer = go.Components.Get<ModelRenderer>( FindMode.EnabledInSelfAndDescendants );
			if ( renderer.IsValid() )
			{
				var bounds = renderer.Bounds;
				box.Scale = bounds.Maxs - bounds.Mins;
				box.Center = go.WorldTransform.PointToLocal( bounds.Center );
			}
		}

		if ( !go.Components.Get<Grabbable>( FindMode.EnabledInSelfAndDescendants ).IsValid() )
			go.Components.Create<Grabbable>();

		if ( AddInteractionProfile && !go.Components.Get<VRItemInteractionProfile>( FindMode.EnabledInSelfAndDescendants ).IsValid() )
			go.Components.Create<VRItemInteractionProfile>();
	}

	string[] ParseExcludedTags()
	{
		if ( string.IsNullOrWhiteSpace( ExcludedTagsCsv ) )
			return [];

		var split = ExcludedTagsCsv.Split( ',' );
		for ( var i = 0; i < split.Length; i++ )
			split[i] = split[i].Trim();
		return split;
	}
}
