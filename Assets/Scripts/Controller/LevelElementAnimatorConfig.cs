using UnityEngine;
using DG.Tweening;

[CreateAssetMenu(menuName = "FlowBlast/Level Element Animator Config", fileName = "LevelElementAnimatorConfig")]
public class LevelElementAnimatorConfig : ScriptableObject
{
    [Header("Animation")]
    public bool playOnStart = true;
    public float levelStartZ = 2f;
    public float levelEndZ = 0.65f;
    public float gridZOffsetFromLevel = 0.12f;
    public float duration = 1f;
    public float introLevelDelay = 0f;
    public float introGridDelay = 0.08f;
    public float introSlotDelay = 0.5f;
    public float outroLevelDelay = 0f;
    public float outroGridDelay = 0.08f;
    public float outroSlotDelay = 0.5f;
    public float outroOnlyObjectDelay = 0f;
    public float slotStartOffsetX = 0.8f;
    public float slotDuration = 1.15f;
    public float outroOnlyObjectOffsetZ = 1.35f;
    public Ease slotEase = Ease.InOutSine;
    public Ease ease = Ease.OutBack;
    public Ease outroEase = Ease.InBack;
}
