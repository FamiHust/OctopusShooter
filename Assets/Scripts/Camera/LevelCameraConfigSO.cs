using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelCameraConfig", menuName = "FlowBlast/Camera/Level Camera Config")]
public class LevelCameraConfigSO : ScriptableObject
{
    [Serializable]
    public class LevelCameraEntry
    {
        [Min(1)] public int level = 1;
        public float orthographicSize = 2f;
        public float yPosition = 0.5f;
    }

    [Header("Default Values")]
    public float defaultOrthographicSize = 2f;
    public float defaultYPosition = 0.5f;

    [Header("Per Level Overrides")]
    public List<LevelCameraEntry> levelEntries = new List<LevelCameraEntry>();

    public void GetValues(int level, out float orthographicSize, out float yPosition)
    {
        orthographicSize = defaultOrthographicSize;
        yPosition = defaultYPosition;

        if (levelEntries == null || levelEntries.Count == 0)
            return;

        for (int i = 0; i < levelEntries.Count; i++)
        {
            LevelCameraEntry entry = levelEntries[i];
            if (entry == null) continue;
            if (entry.level != level) continue;

            orthographicSize = entry.orthographicSize;
            yPosition = entry.yPosition;
            return;
        }
    }
}
