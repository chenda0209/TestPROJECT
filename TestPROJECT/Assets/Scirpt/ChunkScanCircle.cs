using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChunkScanCircle : MonoBehaviour
{
    public Transform player;
    public float chunkSize = 32f;
    public float viewRadius = 150f; // 实际扫描的圆形半径（米）

    private Dictionary<Vector2Int, int> chunkToSlot = new Dictionary<Vector2Int, int>();
    private Stack<int> freeSlots = new Stack<int>();
    private List<Vector2Int> circleOffsetTable = new List<Vector2Int>(); // 预计算的圆形偏移表
    
    private Vector2Int lastPlayerCoord = new Vector2Int(-999, -999); // 用于跨界检测
    private int maxSlots = 200; // 根据半径覆盖面积适当调大

    void Start()
    {
        // 1. 预计算圆形偏移表 (只在启动时做一次)
        PrecomputeCircleOffsets();

        // 2. 初始化显存坑位
        maxSlots = circleOffsetTable.Count + 10; // 稍微多备几个防止边界闪烁
        for (int i = 0; i < maxSlots; i++) freeSlots.Push(i);
    }

    void PrecomputeCircleOffsets()
    {
        circleOffsetTable.Clear();
        // 计算半径覆盖的最大格子跨度
        int range = Mathf.CeilToInt(viewRadius / chunkSize);
        
        for (int x = -range; x <= range; x++)
        {
            for (int z = -range; z <= range; z++)
            {
                // 计算格子中心点距离原点的距离 (使用平方比较，避开开根号计算)
                float distSq = (x * chunkSize) * (x * chunkSize) + (z * chunkSize) * (z * chunkSize);
                if (distSq <= viewRadius * viewRadius)
                {
                    circleOffsetTable.Add(new Vector2Int(x, z));
                }
            }
        }
        Debug.Log($"圆形扫描初始化完成，覆盖 Chunk 数量: {circleOffsetTable.Count}");
    }

    void Update()
    {
        if (player == null) return;

        // 3. 跨界检测：只有玩家跨过格子边界才执行大数据量更新
        int pX = Mathf.FloorToInt(player.position.x / chunkSize);
        int pZ = Mathf.FloorToInt(player.position.z / chunkSize);
        Vector2Int currentCoord = new Vector2Int(pX, pZ);

        if (currentCoord != lastPlayerCoord)
        {
            UpdateChunks(currentCoord);
            lastPlayerCoord = currentCoord;
        }
    }

    void UpdateChunks(Vector2Int centerCoord)
    {
        HashSet<Vector2Int> wantedChunks = new HashSet<Vector2Int>();

        // 4. 使用预存的偏移表快速定位目标格子 (这里只有加法，没有距离计算！)
        foreach (var offset in circleOffsetTable)
        {
            Vector2Int coord = centerCoord + offset;
            wantedChunks.Add(coord);

            if (!chunkToSlot.ContainsKey(coord) && freeSlots.Count > 0)
            {
                int assignedSlot = freeSlots.Pop();
                chunkToSlot.Add(coord, assignedSlot);
                // InitChunkGrass(coord, assignedSlot); 
            }
        }

        // 5. 增量卸载旧格子
        List<Vector2Int> toRemove = new List<Vector2Int>();
        foreach (var activeCoord in chunkToSlot.Keys)
        {
            if (!wantedChunks.Contains(activeCoord))
            {
                toRemove.Add(activeCoord);
            }
        }

        foreach (var coord in toRemove)
        {
            int recycledSlot = chunkToSlot[coord];
            freeSlots.Push(recycledSlot);
            chunkToSlot.Remove(coord);
        }
    }

    void OnDrawGizmos()
    {
        if (player == null) return;

        // 画出理想的圆形扫描范围（黄色圆圈）
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(player.position, viewRadius);

        // 画出当前实际激活的格子（绿色框）
        foreach (var pair in chunkToSlot)
        {
            Vector2Int coord = pair.Key;
            Vector3 center = new Vector3(coord.x * chunkSize + chunkSize * 0.5f, 0, coord.y * chunkSize + chunkSize * 0.5f);
            
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(center, new Vector3(chunkSize * 0.9f, 0.2f, chunkSize * 0.9f));
        }
    }
}