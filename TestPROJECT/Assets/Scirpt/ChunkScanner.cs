using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrassSlotManager : MonoBehaviour
{
    public Transform player;
    public float chunkSize = 32f;
    public int radius = 5; // 左右各5格，一共 11x11=121 格

    // 核心仓库：记录哪些坐标占了哪个显存坑位 (Slot)
    private Dictionary<Vector2Int, int> chunkToSlot = new Dictionary<Vector2Int, int>();
    private Stack<int> freeSlots = new Stack<int>();

    private int maxSlots = 121;

    void Start()
    {
        // 1. 初始化 121 个空的显存坑位索引
        for (int i = 0; i < maxSlots; i++) freeSlots.Push(i);
    }

    void Update()
    {
        if (player == null) return;

        // 2. 计算玩家当前的格子坐标 (绝对地理位置)
        int pX = Mathf.FloorToInt(player.position.x / chunkSize);
        int pZ = Mathf.FloorToInt(player.position.z / chunkSize);
        Vector2Int currentCoord = new Vector2Int(pX, pZ);

        HashSet<Vector2Int> wantedChunks = new HashSet<Vector2Int>();

        // 3. 暴力扫描周围 11x11 的格子，管你镜头看哪，全都得要！
        for (int x = -radius; x <= radius; x++)
        {
            for (int z = -radius; z <= radius; z++)
            {
                Vector2Int coord = new Vector2Int(pX + x, pZ + z);
                wantedChunks.Add(coord);

                // 如果是新进来的格子，且还有空车位
                if (!chunkToSlot.ContainsKey(coord) && freeSlots.Count > 0)
                {
                    int assignedSlot = freeSlots.Pop();
                    chunkToSlot.Add(coord, assignedSlot);

                    // --- 这里触发你的 Compute Shader 写入逻辑 ---
                    // InitChunkGrass(coord, assignedSlot); 
                    Debug.Log($"新格子 {coord} 进入，分配显存坑位: {assignedSlot}");
                }
            }
        }

        // 4. 清理离开 11x11 范围的旧格子
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
            freeSlots.Push(recycledSlot); // 回收车位
            chunkToSlot.Remove(coord);
            Debug.Log($"格子 {coord} 离开，回收显存坑位: {recycledSlot}");
        }
    }

    void OnDrawGizmos()
    {
        // 这里的 Gizmos 才是你想要的：人周围一圈整整齐齐的黄框
        if (player == null) return;
        foreach (var pair in chunkToSlot)
        {
            Vector2Int coord = pair.Key;
            Vector3 center = new Vector3(coord.x * chunkSize + chunkSize * 0.5f, 0, coord.y * chunkSize + chunkSize * 0.5f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(center, new Vector3(chunkSize, 1, chunkSize));

            // 玩家脚下永远是绿色
            Gizmos.color = Color.green;
            Vector3 pCenter = new Vector3(Mathf.FloorToInt(player.position.x / chunkSize) * chunkSize + chunkSize * 0.5f, 0, Mathf.FloorToInt(player.position.z / chunkSize) * chunkSize + chunkSize * 0.5f);
            Gizmos.DrawCube(pCenter, new Vector3(chunkSize, 1f, chunkSize));
        }
    }
}