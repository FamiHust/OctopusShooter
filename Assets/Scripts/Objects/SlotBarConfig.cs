using UnityEngine;

[CreateAssetMenu(fileName = "SlotBarConfig", menuName = "FlowBlast/SlotBar/Slot Bar Config")]
public class SlotBarConfig : ScriptableObject
{
    [Min(1)] public int slotCount = 5;
    public Slot slotPrefab;
    public float slotSpacing = 0.4f;
}
