using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "HardLevelConfig", menuName = "FlowBlast/UI/Hard Level Config")]
public class HardLevelConfigSO : ScriptableObject
{
    [Tooltip("Danh sach level hard.")]
    [SerializeField] private List<int> hardLevels = new List<int>();

    public bool IsHardLevel(int level)
    {
        int safeLevel = Mathf.Max(1, level);
        if (hardLevels == null || hardLevels.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < hardLevels.Count; i++)
        {
            if (safeLevel == Mathf.Max(1, hardLevels[i]))
            {
                return true;
            }
        }

        return false;
    }
}
