using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu (fileName ="LevelDataBase", menuName = "LevelDataBase")  ]
public class LevelDataBase : ScriptableObject
{
    [Tooltip("Kéo toàn bộ prefab level vào đây theo đúng thứ tự. Level = index + 1.")]
    public List<GameObject> listPrefab = new List<GameObject>();

    [SerializeField, HideInInspector]
    private List<LevelConfig> config = new List<LevelConfig>();

    public GameObject GetLevelPrefab(int level)
    {
        int index = level - 1;
        if (listPrefab != null && index >= 0 && index < listPrefab.Count)
        {
            return listPrefab[index];
        }

        // Backward compatibility cho data cũ nếu listPrefab chưa được gán đầy đủ.
        LevelConfig found = config.FirstOrDefault(x => x.level == level);
        return found != null ? found.levelPrefab : null;
    }

    private void OnValidate()
    {
        RebuildConfigFromList();
    }

    private void RebuildConfigFromList()
    {
        if (config == null)
        {
            config = new List<LevelConfig>();
        }
        else
        {
            config.Clear();
        }

        if (listPrefab == null)
        {
            return;
        }

        for (int i = 0; i < listPrefab.Count; i++)
        {
            config.Add(new LevelConfig
            {
                level = i + 1,
                levelPrefab = listPrefab[i]
            });
        }
    }
}
[System.Serializable]
public class LevelConfig
{
    public int level;
    public GameObject levelPrefab;
}
