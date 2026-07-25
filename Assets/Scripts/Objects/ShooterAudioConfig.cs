using UnityEngine;

[CreateAssetMenu(fileName = "ShooterAudioConfig", menuName = "FlowBlast/Shooter/Shooter Audio Config")]
public class ShooterAudioConfig : ScriptableObject
{
    public string shootSfxKey = Const.popShootSFX;
    [Range(0f, 1f)] public float shootSfxVolume = 0.5f;
    public bool useSimulatedShootSfx = true;
    [Min(0.05f)] public float simulatedShootSfxInterval = 0.12f;
    public bool scaleIntervalBySpeedMultiplier = true;
    [Range(0f, 2f)] public float multiplierInfluence = 1f;
}
