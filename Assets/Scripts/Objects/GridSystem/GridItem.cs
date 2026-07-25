using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum GridItemType
{
    Wall,
    EmptyCell,
    Shooter,
    Tunnel

}
public class GridItem : MonoBehaviour
{
    [Header("Runtime Visual Offset")]
    [SerializeField] private bool applyRuntimeLocalOffset = true;
    [SerializeField] private float runtimeLocalOffsetY = 0.015f;
    [SerializeField] private float runtimeLocalOffsetZ = 0.05f;

    [SerializeField] private int row;
    [SerializeField] private int col;
    [SerializeField] private GridItemType type;
    private HashSet<GridItem> neighbors = new HashSet<GridItem>();
    private bool runtimeLocalOffsetApplied;

    private void Awake()
    {
        ApplyRuntimeLocalOffsetIfNeeded();
    }

    /// <summary>
    /// Initialize node với row, col
    /// </summary>
    public void Initialize(int r, int c, GridItemType cellType)
    {
        row = r;
        col = c;
        type = cellType;
        ApplyRuntimeLocalOffsetIfNeeded();
    }

    private void ApplyRuntimeLocalOffsetIfNeeded()
    {
        if (!applyRuntimeLocalOffset || runtimeLocalOffsetApplied || !Application.isPlaying)
        {
            return;
        }

        Vector3 localPos = transform.localPosition;
        localPos.y += runtimeLocalOffsetY;
        localPos.z += runtimeLocalOffsetZ;
        transform.localPosition = localPos;
        runtimeLocalOffsetApplied = true;
    }
    
    /// <summary>
    /// Lấy row
    /// </summary>
    public int GetRow()
    {
        return row;
    }
    
    /// <summary>
    /// Lấy col
    /// </summary>
    public int GetCol()
    {
        return col;
    }

    public void SetGridCoordinate(int r, int c)
    {
        row = r;
        col = c;
    }

    /// <summary>
    /// Lấy type
    /// </summary>
    public GridItemType GetGridItemType()
    {
        return type;
    }

    /// <summary>
    /// Set type
    /// </summary>
    public void SetGridItemType(GridItemType newType)
    {
        type = newType;
    }
    
    /// <summary>
    /// Lấy danh sách các node lân cận (không bao gồm EmptyCell)
    /// </summary>
    public List<GridItem> GetNeighbors()
    {
        return neighbors.Where(n => n.GetGridItemType() != GridItemType.Wall).ToList();
    }
    public void SetEmptyItem()
    {
        type=GridItemType.EmptyCell;
    }
    
    /// <summary>
    /// Kiểm tra node này có chứa item (bị block) hay không
    /// </summary>
    //public bool HasItem()
    //{
    //    return item != null;
    //}
    
    ///// <summary>
    ///// Lấy item trong node
    ///// </summary>
    //public GameObject GetItem()
    //{
    //    return item;
    //}
    
    ///// <summary>
    ///// Đặt item vào node
    ///// </summary>
    //public void SetItem(GameObject newItem)
    //{
    //    item = newItem;
    //}
    
    ///// <summary>
    ///// Xóa item khỏi node
    ///// </summary>
    //public void RemoveItem()
    //{
    //    item = null;
    //}
    
    /// <summary>
    /// Thêm một node lân cận - bỏ qua nếu type là EmptyCell
    /// </summary>
    public void AddNeighbor(GridItem neighbor)
    {
        if (neighbor != null && !neighbors.Contains(neighbor) && neighbor.GetGridItemType() != GridItemType.Wall)
        {
            neighbors.Add(neighbor);
        }
    }

    public void RemoveNeighbor(GridItem neighbor)
    {
        neighbors.Remove(neighbor);
    }

    public void ClearNeighbors()
    {
        neighbors.Clear();
    }

    /// <summary>Trả về neighbors thô (không lọc) — dùng nội bộ để dọn back-reference.</summary>
    public HashSet<GridItem> GetRawNeighbors() => neighbors;
}