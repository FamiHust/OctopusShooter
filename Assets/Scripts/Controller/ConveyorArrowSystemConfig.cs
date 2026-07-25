using UnityEngine;

[CreateAssetMenu(fileName = "ConveyorArrowSystemConfig", menuName = "FlowBlast/Conveyor/Conveyor Arrow System Config")]
public class ConveyorArrowSystemConfig : ScriptableObject
{
    public Transform arrowPrefab;
    [Min(1)] public int arrowCount = 12;
    [Min(0f)] public float speed = 0.15f;
    public bool syncWithSeedSpeed = true;
}
