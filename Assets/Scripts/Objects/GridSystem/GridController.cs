using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GridController : MonoBehaviour
{
    [System.Serializable]
    private class EndNodeSnapshot
    {
        public int row;
        public int col;
        public GridItemType type;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
        public int layer;
        public string tag;
        public string objectName;
    }

    [Header("Grid Size")]
    [SerializeField] private int col;
    [SerializeField] private int row;

    [SerializeField] private List<GridItem> nodes = new List<GridItem>();
    [SerializeField] private List<GridItem> endNodes;
    [SerializeField] private List<EndNodeSnapshot> endNodeSnapshots = new List<EndNodeSnapshot>();
    [SerializeField] private float endNodeIntegrityCheckInterval = 0.35f;
   

    // SỬA LỖI 1: Phải khởi tạo biến (new HashSet) để không bị Null
    private HashSet<Vector2Int> lockItemBytunel = new HashSet<Vector2Int>();
    private float nextEndNodeIntegrityCheckTime;
    private bool endNodeIntegrityDirty = true;

    private void OnValidate()
    {
        RefreshEndNodesFromTopRowActiveShooters();
        MarkEndNodeIntegrityDirty();
    }

    private void RefreshEndNodesFromTopRowActiveShooters()
    {
        if (nodes == null)
        {
            nodes = new List<GridItem>();
        }

        if (endNodes == null)
        {
            endNodes = new List<GridItem>();
        }

        endNodes.Clear();

        IEnumerable<GridItem> sourceNodes = nodes.Where(n => n != null);
        if (!sourceNodes.Any())
        {
            sourceNodes = GetComponentsInChildren<GridItem>(true);
        }

        foreach (GridItem item in sourceNodes)
        {
            if (item == null || item.GetRow() != 0)
            {
                continue;
            }

            if (!HasActiveBaseShooterInItem(item))
            {
                continue;
            }

            if (!endNodes.Contains(item))
            {
                endNodes.Add(item);
            }
        }
    }

    private static bool HasActiveBaseShooterInItem(GridItem item)
    {
        if (item == null)
        {
            return false;
        }

        BaseShooter[] shooters = item.GetComponentsInChildren<BaseShooter>(true);
        if (shooters == null || shooters.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < shooters.Length; i++)
        {
            BaseShooter shooter = shooters[i];
            if (shooter == null)
            {
                continue;
            }

            GameObject shooterObject = shooter.gameObject;
            if (shooter.enabled && shooterObject != null && shooterObject.activeSelf)
            {
                return true;
            }
        }

        return false;
    }

    private void Start()
    {
        GameEventHub.Instance.Invoke(GameEventType.OnGridControllerInit, this);
        RefreshEndNodesFromTopRowActiveShooters();
        PruneInvalidEndNodeData();
        SetNeightForAllItem();
        EnsureEndNodeIntegrity();
    }

    private void LateUpdate()
    {
        if (!endNodeIntegrityDirty)
        {
            return;
        }

        if (Time.time < nextEndNodeIntegrityCheckTime)
        {
            return;
        }

        nextEndNodeIntegrityCheckTime = Time.time + Mathf.Max(0.05f, endNodeIntegrityCheckInterval);
        EnsureEndNodeIntegrity();
    }

    private void MarkEndNodeIntegrityDirty()
    {
        endNodeIntegrityDirty = true;
    }

    // --- [LOCK LOGIC] ---
    public void SetLockItemByTunel(int col, int row)
    {
        lockItemBytunel.Add(new Vector2Int(col, row));
    }

    public void RemoveLockItemByTunel(int col, int row)
    {
        lockItemBytunel.Remove(new Vector2Int(col, row));
    }

    private bool CheckNodeLockByTunel(GridItem item)
    {
        if (lockItemBytunel == null) return false;
        return lockItemBytunel.Contains(new Vector2Int(item.GetCol(), item.GetRow()));
    }

    // --- [GRAPH SETUP] ---
    private void SetNeightForAllItem()
    {
        if (nodes == null)
        {
            nodes = new List<GridItem>();
            return;
        }

        for (int i = nodes.Count - 1; i >= 0; i--)
        {
            if (nodes[i] == null)
            {
                nodes.RemoveAt(i);
                continue;
            }

            nodes[i].ClearNeighbors();
        }

        foreach (var item in nodes)
        {
            // SỬA LỖI 2: Chỉ loại bỏ Wall. 
            // KHÔNG ngắt neighbor của node bị Tunnel khóa, vì Shooter sinh ra ở đó vẫn cần neighbor để đi ra!
            if (item.GetGridItemType() == GridItemType.Wall)
            {
                continue;
            }

            int currentRow = item.GetRow();
            int currentCol = item.GetCol();
            int[][] directions = { new[] { -1, 0 }, new[] { 1, 0 }, new[] { 0, -1 }, new[] { 0, 1 } };

            foreach (var dir in directions)
            {
                int neighborRow = currentRow + dir[0];
                int neighborCol = currentCol + dir[1];

                GridItem neighbor = nodes.FirstOrDefault(n => n.GetRow() == neighborRow && n.GetCol() == neighborCol);

                if (neighbor != null && neighbor.GetGridItemType() != GridItemType.Wall)
                {
                    item.AddNeighbor(neighbor);
                }
            }
        }
        ;
    }

    public void RecalculateGridForPathfinding()
    {
        SetNeightForAllItem();
        MarkEndNodeIntegrityDirty();
    }

    // --- [PATHFINDING] ---
    public bool HasPathToAnyEndNode(GridItem startNode)
    {
        if (startNode == null || endNodes == null || endNodes.Count == 0) return false;
        return DFSPathfinding(startNode, new HashSet<GridItem>());
    }

    public bool HasPathFromAnyNode(List<GridItem> startNodes)
    {
        if (startNodes == null || startNodes.Count == 0) return false;
        foreach (var startNode in startNodes)
        {
            if (HasPathToAnyEndNode(startNode)) return true;
        }
        return false;
    }

    private bool DFSPathfinding(GridItem currentNode, HashSet<GridItem> visited)
    {
        if (endNodes.Contains(currentNode)) return true;

        visited.Add(currentNode);

        foreach (var neighbor in currentNode.GetNeighbors())
        {
            if (visited.Contains(neighbor))
            {
                continue;
            }

            // Chỉ coi là chạm đích khi đi từ một ô EmptyCell sang EndNode.
            // Tránh trường hợp shooter chạm trực tiếp shooter-end ở đầu game và bị mở lock sai.
            if (endNodes.Contains(neighbor) &&
                currentNode.GetGridItemType() == GridItemType.EmptyCell &&
                !CheckNodeLockByTunel(neighbor))
            {
                return true;
            }

            // SỬA LỖI 3: Thực hiện "Chặn đường" ngay trong thuật toán DFS
            // Nếu neighbor bị Tunnel khóa -> Mù, coi như không thấy đường đi qua nó!
            if (neighbor.GetGridItemType() != GridItemType.EmptyCell ||
                CheckNodeLockByTunel(neighbor))
            {
                continue;
            }

            if (DFSPathfinding(neighbor, visited)) return true;
        }

        return false;
    }

    // --- [CORE] ---
    public void Clear()
    {
        nodes.Clear();
        endNodes.Clear();
        MarkEndNodeIntegrityDirty();
    }

    public List<GridItem> GetAllNodes() => nodes;

    public bool IsEndNode(GridItem node) => endNodes != null && endNodes.Contains(node);

    public bool RemoveNode(GridItem node)
    {
        if (node == null) return false;

        bool wasEndNode = endNodes != null && endNodes.Contains(node);
        foreach (var nb in new HashSet<GridItem>(node.GetRawNeighbors()))
        {
            nb.RemoveNeighbor(node);
        }

        nodes.Remove(node);
        if (endNodes != null)
        {
            endNodes.Remove(node);
        }

        if (wasEndNode)
        {
            CaptureEndNodeSnapshot(node);
            EnsureFakeEndNode(node);
        }

        MarkEndNodeIntegrityDirty();

        return wasEndNode;
    }

    private void EnsureFakeEndNode(GridItem removedEndNode)
    {
        if (removedEndNode == null)
        {
            return;
        }

        int removedRow = removedEndNode.GetRow();
        int removedCol = removedEndNode.GetCol();

        GridItem existingNode = nodes.FirstOrDefault(n =>
            n != null &&
            n.GetRow() == removedRow &&
            n.GetCol() == removedCol);

        if (existingNode != null)
        {
            if (endNodes == null)
            {
                endNodes = new List<GridItem>();
            }

            if (!endNodes.Contains(existingNode))
            {
                endNodes.Add(existingNode);
            }
            return;
        }

        EndNodeSnapshot snapshot = FindEndNodeSnapshot(removedRow, removedCol);
        if (snapshot == null)
        {
            snapshot = BuildSnapshotFromGridItem(removedEndNode);
            endNodeSnapshots.Add(snapshot);
        }

        CreateReplacementEndNode(snapshot);
    }

    public void RegisterNode(GridItem node, bool markAsEndNode = false)
    {
        if (node == null)
        {
            return;
        }

        bool alreadyRegistered = nodes.Contains(node);
        if (!alreadyRegistered)
        {
            nodes.Add(node);
        }

        if (alreadyRegistered)
        {
            foreach (GridItem nb in new HashSet<GridItem>(node.GetRawNeighbors()))
            {
                if (nb != null)
                {
                    nb.RemoveNeighbor(node);
                }
            }

            node.ClearNeighbors();
        }

        if (markAsEndNode)
        {
            if (endNodes == null)
            {
                endNodes = new List<GridItem>();
            }

            if (!endNodes.Contains(node))
            {
                endNodes.Add(node);
            }

            CaptureEndNodeSnapshot(node);
        }

        int r = node.GetRow();
        int c = node.GetCol();
        int[][] dirs = { new[] { -1, 0 }, new[] { 1, 0 }, new[] { 0, -1 }, new[] { 0, 1 } };

        foreach (var dir in dirs)
        {
            GridItem nb = nodes.FirstOrDefault(n =>
                n != node &&
                n.GetRow() == r + dir[0] &&
                n.GetCol() == c + dir[1] &&
                n.GetGridItemType() != GridItemType.Wall);

            if (nb == null) continue;
            node.AddNeighbor(nb);
            nb.AddNeighbor(node);
        }

        MarkEndNodeIntegrityDirty();
    }

    private void EnsureEndNodeIntegrity()
    {
        if (nodes == null)
        {
            nodes = new List<GridItem>();
        }

        if (endNodes == null)
        {
            endNodes = new List<GridItem>();
        }

        if (endNodeSnapshots == null)
        {
            endNodeSnapshots = new List<EndNodeSnapshot>();
        }

        for (int i = nodes.Count - 1; i >= 0; i--)
        {
            if (nodes[i] == null)
            {
                nodes.RemoveAt(i);
            }
        }

        for (int i = endNodes.Count - 1; i >= 0; i--)
        {
            if (endNodes[i] == null)
            {
                endNodes.RemoveAt(i);
            }
        }

        PruneInvalidEndNodeData();

        for (int i = 0; i < endNodes.Count; i++)
        {
            GridItem endNode = endNodes[i];
            if (endNode == null)
            {
                continue;
            }

            if (!nodes.Contains(endNode))
            {
                nodes.Add(endNode);
            }

            CaptureEndNodeSnapshot(endNode);
        }

        for (int i = 0; i < endNodeSnapshots.Count; i++)
        {
            EndNodeSnapshot snapshot = endNodeSnapshots[i];
            if (snapshot == null)
            {
                continue;
            }

            GridItem nodeAtCoord = nodes.FirstOrDefault(n =>
                n != null &&
                n.GetRow() == snapshot.row &&
                n.GetCol() == snapshot.col);

            if (nodeAtCoord == null)
            {
                CreateReplacementEndNode(snapshot);
                continue;
            }

            if (!endNodes.Contains(nodeAtCoord))
            {
                endNodes.Add(nodeAtCoord);
            }

            CaptureEndNodeSnapshot(nodeAtCoord);
        }

        endNodeIntegrityDirty = false;
    }

    private void PruneInvalidEndNodeData()
    {
        if (endNodes != null)
        {
            for (int i = endNodes.Count - 1; i >= 0; i--)
            {
                GridItem node = endNodes[i];
                if (node == null || node.GetRow() != 0)
                {
                    endNodes.RemoveAt(i);
                }
            }
        }

        if (endNodeSnapshots != null)
        {
            for (int i = endNodeSnapshots.Count - 1; i >= 0; i--)
            {
                EndNodeSnapshot snapshot = endNodeSnapshots[i];
                if (snapshot == null || snapshot.row != 0)
                {
                    endNodeSnapshots.RemoveAt(i);
                }
            }
        }
    }

    private void CaptureEndNodeSnapshot(GridItem node)
    {
        if (node == null)
        {
            return;
        }

        EndNodeSnapshot snapshot = FindEndNodeSnapshot(node.GetRow(), node.GetCol());
        EndNodeSnapshot source = BuildSnapshotFromGridItem(node);

        if (snapshot == null)
        {
            endNodeSnapshots.Add(source);
            return;
        }

        snapshot.row = source.row;
        snapshot.col = source.col;
        snapshot.type = source.type;
        snapshot.localPosition = source.localPosition;
        snapshot.localRotation = source.localRotation;
        snapshot.localScale = source.localScale;
        snapshot.layer = source.layer;
        snapshot.tag = source.tag;
        snapshot.objectName = source.objectName;
    }

    private EndNodeSnapshot BuildSnapshotFromGridItem(GridItem node)
    {
        Transform tr = node.transform;
        return new EndNodeSnapshot
        {
            row = node.GetRow(),
            col = node.GetCol(),
            type = node.GetGridItemType(),
            localPosition = tr != null ? tr.localPosition : Vector3.zero,
            localRotation = tr != null ? tr.localRotation : Quaternion.identity,
            localScale = tr != null ? tr.localScale : Vector3.one,
            layer = node.gameObject.layer,
            tag = node.gameObject.tag,
            objectName = node.gameObject.name
        };
    }

    private EndNodeSnapshot FindEndNodeSnapshot(int targetRow, int targetCol)
    {
        if (endNodeSnapshots == null)
        {
            return null;
        }

        for (int i = 0; i < endNodeSnapshots.Count; i++)
        {
            EndNodeSnapshot snapshot = endNodeSnapshots[i];
            if (snapshot == null)
            {
                continue;
            }

            if (snapshot.row == targetRow && snapshot.col == targetCol)
            {
                return snapshot;
            }
        }

        return null;
    }

    private void CreateReplacementEndNode(EndNodeSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        GridItem existingNode = nodes.FirstOrDefault(n =>
            n != null &&
            n.GetRow() == snapshot.row &&
            n.GetCol() == snapshot.col);

        if (existingNode != null)
        {
            if (endNodes != null && !endNodes.Contains(existingNode))
            {
                endNodes.Add(existingNode);
            }
            return;
        }

        string nodeName = string.IsNullOrWhiteSpace(snapshot.objectName)
            ? $"RecoveredEndNode_{snapshot.row}_{snapshot.col}"
            : snapshot.objectName;

        GameObject recoveredNodeObject = new GameObject(nodeName);
        recoveredNodeObject.transform.SetParent(transform, false);
        recoveredNodeObject.transform.localPosition = snapshot.localPosition;
        recoveredNodeObject.transform.localRotation = snapshot.localRotation;
        recoveredNodeObject.transform.localScale = snapshot.localScale;
        recoveredNodeObject.layer = snapshot.layer;

        if (!string.IsNullOrEmpty(snapshot.tag))
        {
            try
            {
                recoveredNodeObject.tag = snapshot.tag;
            }
            catch (UnityException)
            {
                recoveredNodeObject.tag = "Untagged";
            }
        }

        GridItem recoveredNode = recoveredNodeObject.AddComponent<GridItem>();
        recoveredNode.Initialize(snapshot.row, snapshot.col, snapshot.type);

        RegisterNode(recoveredNode, true);
    }
}
