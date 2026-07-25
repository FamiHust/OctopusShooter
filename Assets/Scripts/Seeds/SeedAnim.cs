using DG.Tweening;
using UnityEngine;

[CreateAssetMenu(fileName = "SeedAnim", menuName = "FlowBlast/Seeds/Seed Anim", order = 0)]
public class SeedAnim : ScriptableObject
{
    [Header("Seed Animation")]
    [Min(0.01f)] public float scaleDownDuration = 0.08f;
    [Min(0f)] public float delayBetweenSeeds = 0.04f;
    [Min(0f)] public float delayBetweenRows = 0.08f;
    [Min(0f)] public float seedLiftHeight = 0.2f;
    public Ease scaleEase = Ease.InBack;
    public Ease liftEase = Ease.OutQuad;

    public void ValidateValues()
    {
        scaleDownDuration = Mathf.Max(0.01f, scaleDownDuration);
        delayBetweenSeeds = Mathf.Max(0f, delayBetweenSeeds);
        delayBetweenRows = Mathf.Max(0f, delayBetweenRows);
        seedLiftHeight = Mathf.Max(0f, seedLiftHeight);
    }

    void OnValidate()
    {
        ValidateValues();
    }
}
