using Sandbox;
using TFT.VR.Abstractions;

namespace TFT.VR.Services;

/// <summary>
/// Picks the right <see cref="IMovementInputSource"/> based on
/// <c>Game.IsRunningInVR</c>. <see cref="PlayerWalkControllerSimple"/> only
/// resolves this single component; switching modes never requires touching
/// the controller's code.
/// </summary>
[Title( "Composite Movement Input Source" )]
[Category( "VR/Services" )]
[Icon( "merge_type" )]
public sealed class CompositeMovementInputSource : Component, IMovementInputSource
{
	[Property] public VRMovementInputSource VRSource { get; set; }
	[Property] public KeyboardMovementInputSource KbmSource { get; set; }

	private IMovementInputSource ResolvedSource =>
		Game.IsRunningInVR ? (IMovementInputSource)VRSource : KbmSource;

	public Vector3 WishMove   => ResolvedSource?.WishMove   ?? Vector3.Zero;
	public bool WantsJump     => ResolvedSource?.WantsJump     == true;
	public bool WantsCrouch   => ResolvedSource?.WantsCrouch   == true;
	public bool WantsSlowWalk => ResolvedSource?.WantsSlowWalk == true;
}
