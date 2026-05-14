using Sandbox;

public sealed class VRFallbackSimulator : Component
{
    [Property, Group("References")] public GameObject CameraRig { get; set; }
    [Property, Group("References")] public GameObject LeftHand { get; set; }
    [Property, Group("References")] public GameObject RightHand { get; set; }

    [Property, Group("Settings")] public float ReachDistance { get; set; } = 30f; // 手距離眼睛多遠

    protected override void OnUpdate()
    {
        // 1. 如果偵測到玩家有戴真實的 VR 頭盔，這個模擬器就直接關閉不做事
        if ( Game.IsRunningInVR ) return;

        // 2. 模擬頭盔轉動 (變成一般的 FPS 鍵鼠視角)
        if ( CameraRig != null )
        {
            var angles = CameraRig.LocalRotation.Angles();
            angles += Input.AnalogLook;
            angles.pitch = angles.pitch.Clamp( -89f, 89f );
            CameraRig.LocalRotation = angles.ToRotation();

            WorldRotation = Rotation.FromYaw( angles.yaw );
        }

        var camPos = CameraRig.WorldPosition;
        var camRot = CameraRig.WorldRotation;

        if ( LeftHand != null )
        {
            LeftHand.WorldPosition = camPos + camRot.Forward * ReachDistance + camRot.Left * 10f - camRot.Up * 10f;
            LeftHand.WorldRotation = camRot;
        }

        if ( RightHand != null )
        {
            RightHand.WorldPosition = camPos + camRot.Forward * ReachDistance + camRot.Right * 10f - camRot.Up * 10f;
            RightHand.WorldRotation = camRot;
        }
    }
}