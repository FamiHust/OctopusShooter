using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewLevelData", menuName = "Configs/Level Data")]
public class LevelData : ScriptableObject
{
    public string levelName;

    [Header("Conveyor Settings")]
    public Vector3 beltPos;
    public List<Vector3> beltPathPint = new List<Vector3>();
    public List<Vector3> wayPos;
    public List<Vector3> wayPathPoint = new List<Vector3>();
    public Vector3 blockPos = new Vector3();
    [Header("Shooter Settings")]
    public List<ShooterInfo> shooters = new List<ShooterInfo>();

    [Header("Grid Setting")]
    public Vector3 gridPanel;

    [System.Serializable]
    public struct ShooterInfo
    {
        public Vector3 position;
        public Quaternion rotation;
        public string typeTag; // Ví dụ: "RedShooter", "BlueShooter"
    }
}