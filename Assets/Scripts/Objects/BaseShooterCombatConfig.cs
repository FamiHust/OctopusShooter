using UnityEngine;
using DG.Tweening;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "BaseShooterCombatConfig", menuName = "FlowBlast/Shooter/Base Shooter Combat Config")]
public class BaseShooterCombatConfig : ScriptableObject
{
    [Header("Shooter VFX")]
    public GameObject disappearParticle;
    public GameObject jumpDisappearParticle;
    public GameObject startJumpVfx;
    public GameObject jumpEffect;
    [Min(0f)] public float jumpVfxYOffset = 0.01f;
    [Min(0f)] public float jumpVfxTowardCameraOffset = 0.06f;
    public Vector3 jumpVfxRotationOffsetEuler = Vector3.zero;
    [Min(1)] public int jumpVfxOrderBelowShooter = 1;

    [Header("Shooting")]
    public GameObject bulletPrefab;
    [Min(0.1f)] public float fireRate = 20f;
    [Min(1)] public int bulletsPerShot = 2;
    [Min(0f)] public float bulletSpreadShotDelay = 0.02f;
    [Min(0f)] public float seedShotInterval = 0.08f;
    [Min(0f)] public float bulletSpreadRadius = 0.2f;
    [Min(0f)] public float rowHandoffDelay = 0f;
    [Min(0f)] public float bulletDecreaseInterval = 0.25f;
    [Min(1)] public int bulletDecreaseAmount = 5;

    [Header("Recoil")]
    [Min(0f)] public float recoilDistance = 0.2f;
    [Min(0.001f)] public float recoilDuration = 0.05f;

    [Header("Deck Visual")]
    [Range(0f, 0.5f)] public float deckOutlineDarkenAmount = 0.16f;
    [Range(0.8f, 1.2f)] public float deckLandingScaleMultiplier = 1.05f;

    [Header("Jump Tween")]
    public Ease jumpScaleEase = Ease.OutSine;
    public Ease jumpMoveEase = Ease.Linear;

    [Header("Magic Stone")]
    [FormerlySerializedAs("magicStonePrefab")]
    public GameObject magicStoneVfxPrefab;
    public GameObject magicStoneComboShooterVfxPrefab;
    public Vector3 magicStoneComboShooterVfxLocalOffset = Vector3.zero;
    [Min(0.01f)] public float magicStoneComboShooterVfxFadeInDuration = 0.2f;
    [Min(1)] public int magicStoneShotStreakThreshold = 50;
    [Min(0f)] public float magicStoneComboBreakGapSeconds = 0.6f;
    public bool spawnMagicStoneOncePerShootingState = true;
}
