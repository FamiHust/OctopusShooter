using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WinFeatureUnlockConfig", menuName = "FlowBlast/UI/Win Feature Unlock Config")]
public class WinFeatureUnlockConfigSO : ScriptableObject
{
    [Tooltip("Unlock levels mapped by index to WinUIManager newFeatureObjects list. Win UI shows feature at unlockLevel - 1.")]
    [SerializeField] private List<int> featureUnlockLevels = new List<int> { 15, 25, 35 };

    public int GetUnlockFeatureIndexForLevel(int completedLevel)
    {
        if (featureUnlockLevels == null || featureUnlockLevels.Count == 0)
        {
            return -1;
        }

        for (int i = 0; i < featureUnlockLevels.Count; i++)
        {
            int previewLevel = Mathf.Max(1, featureUnlockLevels[i] - 1);
            if (completedLevel == previewLevel)
            {
                return i;
            }
        }

        return -1;
    }
}
