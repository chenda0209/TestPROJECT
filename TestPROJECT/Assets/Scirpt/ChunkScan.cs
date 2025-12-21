using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChunkScan : MonoBehaviour
{
    public Transform player;
    public float chunkSize = 32f;
    public int radius = 5; // 左右各5格，一共 11x11=121 格

    private Dictionary<Vector2Int, int> chunkToSlot = new Dictionary<Vector2Int, int>();
    private Stack<int> freeSlots = new Stack<int>();

    // 【优化点1】记录上一次的坐标，避免每帧暴力计算
    private Vector2Int lastPlayerCoord = new Vector2Int(-99999, -99999);
    private int maxSlots = 121;

    void Start()
    {
        for (int i = 0; i < maxSlots; i++) freeSlots.Push(i);

        // 初始执行一次，确保出生点周围有草
        if (player != null) UpdateChunks(GetPlayerCoord());
    }

    // 获取玩家当前所在的格子坐标
    Vector2Int GetPlayerCoord()
    {
        return new Vector2Int(Mathf.FloorToInt(player.position.x / chunkSize), Mathf.FloorToInt(player.position.z / chunkSize));
    }

    void Update()
    {
        if (player == null) return;

        Vector2Int currentCoord = GetPlayerCoord();

        // 【优化点2】只有跨格时才跑逻辑
        if (currentCoord != lastPlayerCoord)
        {
            UpdateChunks(currentCoord);
            lastPlayerCoord = currentCoord;
        }
    }

    void UpdateChunks(Vector2Int pCoord)
    {
        // 使用 HashSet 记录当前这一圈“应该存在”的坐标
        HashSet<Vector2Int> wantedChunks = new HashSet<Vector2Int>();

        // 1. 确定哪些格子需要被保留或新建
        for (int x = -radius; x <= radius; x++)
        {
            for (int z = -radius; z <= radius; z++)
            {
                Vector2Int coord = new Vector2Int(pCoord.x + x, pCoord.y + z);
                wantedChunks.Add(coord);

                // 如果是新进入范围的格子
                if (!chunkToSlot.ContainsKey(coord))
                {
                    if (freeSlots.Count > 0)
                    {
                        int assignedSlot = freeSlots.Pop();
                        chunkToSlot.Add(coord, assignedSlot);
                        // --- 这里调用你的渲染初始化 ---
                        // InitChunkGrass(coord, assignedSlot);
                    }
                }
            }
        }

        // 2. 找出哪些格子已经不在范围内了
        // 这里不能在 foreach 字典时删除，所以用一个临时列表记录
        List<Vector2Int> toRemove = new List<Vector2Int>();
        foreach (var activeCoord in chunkToSlot.Keys)
        {
            if (!wantedChunks.Contains(activeCoord))
            {
                toRemove.Add(activeCoord);
            }
        }

        // 3. 统一回收
        foreach (var coord in toRemove)
        {
            int recycledSlot = chunkToSlot[coord];
            freeSlots.Push(recycledSlot);
            chunkToSlot.Remove(coord);
            // --- 这里可以调用显存清理逻辑 ---
        }
    }

    void OnDrawGizmos()
    {
        if (player == null) return;

        // 绘制激活中的格子
        foreach (var pair in chunkToSlot)
        {
            Vector2Int coord = pair.Key;
            Vector3 center = new Vector3(coord.x * chunkSize + chunkSize * 0.5f, 0, coord.y * chunkSize + chunkSize * 0.5f);

            // 默认用黄色代表“加载中”
            Gizmos.color = Color.yellow;

            // 如果正是玩家脚下的格子，用绿色
            Vector2Int pCoord = GetPlayerCoord();
            if (coord == pCoord) Gizmos.color = Color.green;

            Gizmos.DrawWireCube(center, new Vector3(chunkSize * 0.95f, 0.5f, chunkSize * 0.95f));
        }
    }
}




