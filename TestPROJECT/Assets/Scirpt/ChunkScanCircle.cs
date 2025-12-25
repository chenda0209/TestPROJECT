using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class ChunkScanCircle : MonoBehaviour
{
    public Transform player;
    public ComputeShader cs;
    public Vector4 threshold; // lod远近设置
    private Vector4[] frustumPlanesArray = new Vector4[6];
    private Camera cam;
    private uint instanceCountPerChunk;
    private uint grassEachMeter = 4;
    private uint chunkSize = 32;// 每块chunk分区大小
    public float viewRadius = 164; // 实际扫描的圆形半径（米）,他来自动生成chunk的数量，chunk的数量决定buffer的总大小，所以调整他就行了
    private float terrainSize = 1024; // 每块terrain的大小
    private Dictionary<Vector2Int, int> chunkToSlot = new Dictionary<Vector2Int, int>();
    private Dictionary<Vector2Int, Terrain> terrainGrid = new Dictionary<Vector2Int, Terrain>();
    private Stack<int> freeChunks = new Stack<int>();
    private List<Vector2Int> circleOffsetTable = new List<Vector2Int>(); // 预计算的圆形偏移表
    private Vector2Int lastPlayerChunk = new Vector2Int(-999, -999); // 用于跨界检测
    private int maxChunks = 200; // 根据半径覆盖面积适当调大
    private Bounds[] chunkBounds; // 所有 chunk 的AABB盒子
    private List<int> activeChunkIDs; // 进入摄像机范围的 chunk 数组

    [StructLayout(LayoutKind.Sequential)]
    public struct GrassData { public Vector3 worldPos; public float rotation; public float scale; };
    private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
    public GameObject
        Lod0grassPrefab,
        Lod1grassPrefab,
        Lod2grassPrefab;
    private Mesh
        lod0grassMesh,
        lod1grassMesh,
        Lod2grassMesh;
    private RenderParams
        lod0renderParams,
        lod1renderParams,
        lod2renderParams;
    private Material
        lod0grassMaterial,
        lod1grassMaterial,
        lod2grassMaterial;
    private GraphicsBuffer // Structured Buffer
        grassDataBuffer, // 所有进入 chunk 的物体
        ActiveChunkIDsBuffer; // 单个 chunk 的 AABB
    private GraphicsBuffer  // Append Buffer
        lod0Buffer,
        lod1Buffer,
        lod2Buffer;
    private GraphicsBuffer // 用来 RenderMeshIndirect 的接口特殊 ComputeBuffer，他是这么说的，我也没搞明白具体差别，反正必须要使用这个类别
        lod0argsBuffer,
        lod1argsBuffer,
        lod2argsBuffer;
    private static readonly int
        _FrustumPlanes = Shader.PropertyToID("_FrustumPlanes"),
        _GrassDataBuffer = Shader.PropertyToID("_GrassDataBuffer"),
        _ActiveChunkIDsBuffer = Shader.PropertyToID("_ActiveChunkIDsBuffer"),
        _ActiveChunkIDs = Shader.PropertyToID("_ActiveChunkIDs"),
        _Lod0Buffer = Shader.PropertyToID("_Lod0Buffer"),
        _Lod1Buffer = Shader.PropertyToID("_Lod1Buffer"),
        _Lod2Buffer = Shader.PropertyToID("_Lod2Buffer"),
        _InstanceCount = Shader.PropertyToID("_InstanceCount"),
        _SplatMap = Shader.PropertyToID("_SplatMap"),
        _HeightMap = Shader.PropertyToID("_HeightMap"),
        _TerrainHeight = Shader.PropertyToID("_TerrainHeight"),
        _ChunkBasePos = Shader.PropertyToID("_ChunkBasePos"),
        _TerrainBasePos = Shader.PropertyToID("_TerrainBasePos"),
        _TargetChunkID = Shader.PropertyToID("_TargetChunkID"),
        _Threshold = Shader.PropertyToID("_Threshold"),
        _ActiveChunkCount = Shader.PropertyToID("_ActiveChunkCount"),
        _CameraPos = Shader.PropertyToID("_CameraPos");
    private int
        generateGrassHandle,
        FrustumCullingHandle;

    [StructLayout(LayoutKind.Sequential)]
    struct FrustumData // 摄像机的视锥体由六个平面定义：左、右、上、下、近、远。每个平面都由一个四维向量 $\vec{P} = (A, B, C, D)$ 表示
    {
        public Vector4 LeftPlane;
        public Vector4 RightPlane;
        public Vector4 BottomPlane;
        public Vector4 TopPlane;
        public Vector4 NearPlane;
        public Vector4 FarPlane;
    }
    private void Start()
    {
        if (cam == null) cam = Camera.main;
        else Debug.LogError("Camera is null");
        if (player == null) Debug.LogError("no player, you play a gb");

        // 预计算圆形偏移表 (只在启动时做一次)
        InitializeCircleOffsets();

        // 初始化显存坑位
        maxChunks = circleOffsetTable.Count + 10; // 稍微多备几个防止边界闪烁
        for (int i = 0; i < maxChunks; i++) freeChunks.Push(i);
        chunkBounds = new Bounds[maxChunks];
        activeChunkIDs = new();
        // 写入草的 ComputerBuffer，这是所有草的buffer，他用来写入所有chunk的草的数据，
        // 由于是使用 ComputerShader 来计算并写入草的坐标和其他信息，所以这里buffer不需要进行Setdata
        instanceCountPerChunk = (uint)(chunkSize * chunkSize * grassEachMeter * grassEachMeter);
        int grassStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(GrassData));
        grassDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, (int)(instanceCountPerChunk * maxChunks), grassStride);
        int ActiveChunkIDsStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(uint));
        ActiveChunkIDsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, maxChunks, ActiveChunkIDsStride);

        // 收集 Mesh 和 Material
        lod0grassMesh = Lod0grassPrefab.GetComponent<MeshFilter>().sharedMesh;
        lod1grassMesh = Lod1grassPrefab.GetComponent<MeshFilter>().sharedMesh;
        Lod2grassMesh = Lod2grassPrefab.GetComponent<MeshFilter>().sharedMesh;
        lod0grassMaterial = Lod0grassPrefab.GetComponent<MeshRenderer>().sharedMaterial;
        lod1grassMaterial = Lod1grassPrefab.GetComponent<MeshRenderer>().sharedMaterial;
        lod2grassMaterial = Lod2grassPrefab.GetComponent<MeshRenderer>().sharedMaterial;
        // 检查 Instancing 启用状态
        if (!lod0grassMaterial.enableInstancing)
        {
            // 启用 Instancing（如果材质的 Shader 支持且 Inspector 中未勾选）
            lod0grassMaterial.enableInstancing = true;
            Debug.LogWarning("GPU Instancing was automatically enabled on the material.");
        }
        lod0renderParams = new(lod0grassMaterial);
        if (!lod1grassMaterial.enableInstancing)
        {
            // 启用 Instancing（如果材质的 Shader 支持且 Inspector 中未勾选）
            lod1grassMaterial.enableInstancing = true;
            Debug.LogWarning("GPU Instancing was automatically enabled on the material.");
        }
        lod1renderParams = new(lod1grassMaterial);
        if (!lod2grassMaterial.enableInstancing)
        {
            // 启用 Instancing（如果材质的 Shader 支持且 Inspector 中未勾选）
            lod2grassMaterial.enableInstancing = true;
            Debug.LogWarning("GPU Instancing was automatically enabled on the material.");
        }
        lod2renderParams = new(lod2grassMaterial);

        Bounds playerBounds = new(Vector3.zero, new Vector3(1000, 1f, 1000));
        // !!!!!!!!!!!!!!!!!!!坑，不设置AABB，他就会始终把世界原点当剔除，这个相当于一个初级剔除，然后才是摄像机剔除。
        lod0renderParams.worldBounds = playerBounds;
        lod1renderParams.worldBounds = playerBounds;
        lod2renderParams.worldBounds = playerBounds;

        // 可见物体的 Buffer, 只写模式，在 ComputerShader 中写入，所以这里不需要去SetData
        int visibleIndexStride = sizeof(int);
        lod0Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, (int)(instanceCountPerChunk * maxChunks), visibleIndexStride);//只用SetBBuffer，不需要SetData
        lod1Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, (int)(instanceCountPerChunk * maxChunks), visibleIndexStride);//只用SetBBuffer，不需要SetData
        lod2Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, (int)(instanceCountPerChunk * maxChunks), visibleIndexStride);//只用SetBBuffer，不需要SetData

        args[0] = lod0grassMesh.GetIndexCount(0); // Index Count (不变)
        args[1] = 0; // Instance Count (必须为0，由CopyCount覆盖)
        args[2] = 0; // Start Index（通常是0）
        args[3] = lod0grassMesh.GetBaseVertex(0); // Base Vertex（通常是0）
        args[4] = 0; // Start Instance
        lod0argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 5, sizeof(int));
        lod0argsBuffer.SetData(args);
        lod1argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 5, sizeof(int));
        lod1argsBuffer.SetData(args);
        lod2argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 5, sizeof(int));
        lod2argsBuffer.SetData(args);

        generateGrassHandle = cs.FindKernel("GenerateGrass");
        FrustumCullingHandle = cs.FindKernel("FrustumCulling");

        cs.SetInt(_InstanceCount, (int)(instanceCountPerChunk * maxChunks));//指定绘制数量
        cs.SetVector(_Threshold, threshold);// 远近分界线LOD

        // 传输数据后设置 ComputerShader，并标记 BUFFER 的数据名字进行计算。
        cs.SetBuffer(generateGrassHandle, _GrassDataBuffer, grassDataBuffer);
        cs.SetBuffer(FrustumCullingHandle, _ActiveChunkIDsBuffer, ActiveChunkIDsBuffer);
        cs.SetBuffer(FrustumCullingHandle, _GrassDataBuffer, grassDataBuffer);

        cs.SetBuffer(FrustumCullingHandle, _Lod0Buffer, lod0Buffer);
        cs.SetBuffer(FrustumCullingHandle, _Lod1Buffer, lod1Buffer);
        cs.SetBuffer(FrustumCullingHandle, _Lod2Buffer, lod2Buffer);
        // 设置材质的读写buffer
        lod0grassMaterial.SetBuffer(_GrassDataBuffer, grassDataBuffer);
        lod0grassMaterial.SetBuffer(_Lod0Buffer, lod0Buffer);
        lod1grassMaterial.SetBuffer(_GrassDataBuffer, grassDataBuffer);
        lod1grassMaterial.SetBuffer(_Lod1Buffer, lod1Buffer);
        lod2grassMaterial.SetBuffer(_GrassDataBuffer, grassDataBuffer);
        lod2grassMaterial.SetBuffer(_Lod2Buffer, lod2Buffer);
    }

    /// <summary>
    /// 预先定好chunk，位移
    /// </summary>
    private void InitializeCircleOffsets()
    {
        circleOffsetTable.Clear();
        // 计算半径覆盖的最大格子跨度
        int range = Mathf.CeilToInt(viewRadius / chunkSize);

        for (int x = -range; x <= range; x++)
        {
            for (int z = -range; z <= range; z++)
            {
                // 计算格子中心点距离原点的距离 (使用平方比较，避开开根号计算)
                float distSq = x * chunkSize * x * chunkSize + z * chunkSize * z * chunkSize;
                if (distSq <= viewRadius * viewRadius)
                {
                    circleOffsetTable.Add(new Vector2Int(x, z));
                }
            }
        }
        Debug.Log($"圆形扫描初始化完成，覆盖 Chunk 数量: {circleOffsetTable.Count}");
    }

    private void Update()
    {
        if (player == null) return;
        if (cam == null) return;

        // 摄像机粗剔
        Vector3 center = cam.transform.position;
        Vector3 size = new Vector3(viewRadius * 2, 20, viewRadius * 2);
        Bounds camForward = new Bounds(center, size);
        lod0renderParams.worldBounds = camForward;
        lod1renderParams.worldBounds = camForward;
        lod2renderParams.worldBounds = camForward;

        // 跨界检测：只有玩家跨过格子边界才执行大数据量更新
        Vector2Int currentChunk = new Vector2Int(Mathf.FloorToInt(player.position.x / chunkSize), Mathf.FloorToInt(player.position.z / chunkSize));
        if (currentChunk != lastPlayerChunk)
        {
            InitializeTerrainGrid(); // 更新当前场景的terran，防止有新的terrain刷新  
            UpdateChunks(currentChunk); // 更新chunk
            lastPlayerChunk = currentChunk; // 更新当前位置的chunk
        }
    }

    private void LateUpdate()
    {
        lod0Buffer.SetCounterValue(0);//清除计数
        lod1Buffer.SetCounterValue(0);//清除计数
        lod2Buffer.SetCounterValue(0);//清除计数

        PrepareAndSetFrustumPlanes(); // 传输摄像机信息

        int threadGroups = Mathf.CeilToInt((float)(instanceCountPerChunk * activeChunkIDs.Count) / 64.0f);
        if (activeChunkIDs.Count > 0)
            cs.Dispatch(FrustumCullingHandle, threadGroups, 1, 1); // Z 必须是 1

        GraphicsBuffer.CopyCount(lod0Buffer, lod0argsBuffer, sizeof(uint));
        Graphics.RenderMeshIndirect(lod0renderParams, lod0grassMesh, lod0argsBuffer);

        GraphicsBuffer.CopyCount(lod1Buffer, lod1argsBuffer, sizeof(uint));
        Graphics.RenderMeshIndirect(lod1renderParams, lod1grassMesh, lod1argsBuffer);

        GraphicsBuffer.CopyCount(lod2Buffer, lod2argsBuffer, sizeof(uint));
        Graphics.RenderMeshIndirect(lod2renderParams, Lod2grassMesh, lod2argsBuffer);
    }


    /// <summary>
    /// 更新chunk，同步检测每个chunk下面的terrain，对新加入的chunk传入地形材质分区tex、高度tex等，
    /// 启动GPU对每个新加入的chunk进行Dispatch，把生成的物体数据写入最大的buffer
    /// </summary>
    /// <param name="centerCoord"></param>
    private void UpdateChunks(Vector2Int centerCoord)
    {
        HashSet<Vector2Int> wantedChunks = new HashSet<Vector2Int>();
        // 使用预存的偏移表快速定位目标格子 (这里只有加法，没有距离计算！)
        foreach (var offset in circleOffsetTable)
        {
            Vector2Int coord = centerCoord + offset;
            wantedChunks.Add(coord);
            // 如果周围的格子 chunk 没有这个新的coord，就加入进来，并且同步格子的记录标志号码牌，
            if (!chunkToSlot.ContainsKey(coord) && freeChunks.Count > 0)
            {
                Terrain t = GetTerrainAtPos(coord);
                if (t == null) continue; // 必须判空！
                int assignedChunk = freeChunks.Pop();
                chunkToSlot.Add(coord, assignedChunk);

                Vector3 basePos = new Vector3(coord.x * chunkSize, 0, coord.y * chunkSize);
                cs.SetTexture(generateGrassHandle, _SplatMap, t.terrainData.GetAlphamapTexture(0)); // 地形材质分区，0.a表示第一层、0.c表示第三层、1.a表示第五层
                cs.SetTexture(generateGrassHandle, _HeightMap, t.terrainData.heightmapTexture); // GetHeightmapTexture 是地形生成的实时高度图
                cs.SetFloat(_TerrainHeight, t.terrainData.size.y); // 还需要传一个地形高度，因为高度图里存的是 0-1 的归一化值
                cs.SetVector(_ChunkBasePos, basePos);
                cs.SetVector(_TerrainBasePos, t.transform.position);
                cs.SetInt(_TargetChunkID, assignedChunk);
                cs.Dispatch(generateGrassHandle, 16, 16, 1);

                // 同时把该 chunk 的AB盒信息记录到 chunkBounds，
                InitializeChunkBounds(coord, assignedChunk, t);
            }
        }
        // 增量卸载旧格子
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
            freeChunks.Push(recycledSlot);
            chunkToSlot.Remove(coord);
        }
    }

    void InitializeChunkBounds(Vector2Int coord, int chunkID, Terrain t)
    {
        float terrainMinY = t.transform.position.y;
        float terrainMaxY = terrainMinY + t.terrainData.size.y;

        // 1. 中心点 Y 应该是地形高度的中间值
        float centerY = (terrainMinY + terrainMaxY) * 0.5f;

        Vector3 center = new Vector3(
            coord.x * chunkSize + chunkSize * 0.5f,
            centerY,
            coord.y * chunkSize + chunkSize * 0.5f
        );

        // 2. 高度 size.y 必须足够大！
        // 即使地形高度差是 100 米，你也要加上草本身的高度，甚至多给 20 米余量
        float heightSize = (terrainMaxY - terrainMinY) + 20.0f;

        Vector3 size = new Vector3(chunkSize, heightSize, chunkSize);
        chunkBounds[chunkID] = new Bounds(center, size);
    }


    // CS摄像机剔除
    private void PrepareAndSetFrustumPlanes()
    {
        // 获取摄像机平面，需要确保 'camera' 变量已在类中定义并赋值
        Plane[] cameraPlanes = GeometryUtility.CalculateFrustumPlanes(cam);
        activeChunkIDs.Clear();
        foreach (var pair in chunkToSlot)
        {
            int slotID = pair.Value;
            // 如果该 Chunk 的包围盒在视野内，加入待处理清单，扫描所有chunkBounds，记录哪些 chunk 渲染，记录他的ID
            if (GeometryUtility.TestPlanesAABB(cameraPlanes, chunkBounds[slotID]))
            {
                activeChunkIDs.Add(slotID);
            }
        }

        // 将 Plane[] 转换为 Vector4[] (GPU 友好格式)，C# 的 Plane 结构体已经包含了法线(N)和距离(D)，顺序是固定的。
        // 循环赋值，而不是手动重复赋值 6 次
        for (int i = 0; i < 6; i++)
        {
            // Vector4(N.x, N.y, N.z, D)
            // 注意：Unity 内部的 Plane.distance 是到原点的有符号距离，这正是 Plane.w 需要的值。
            frustumPlanesArray[i] = new Vector4(
                cameraPlanes[i].normal.x,
                cameraPlanes[i].normal.y,
                cameraPlanes[i].normal.z,
                cameraPlanes[i].distance
            );
        }
        // 将数组设置Shader 属性 这是最快、最简洁的传输方式，并没有使用全局Shader.SetGlobalVectorArray(FrustumPlanesID, frustumPlanesArray);
        cs.SetVectorArray(_FrustumPlanes, frustumPlanesArray);
        cs.SetVector(_CameraPos, cam.transform.position);

        // 4. 将筛选后的清单传给 GPU (新增的逻辑)
        if (activeChunkIDs.Count > 0)
        {
            int[] ids = activeChunkIDs.ToArray();
            ActiveChunkIDsBuffer.SetData(ids);
            // cs.SetBuffer(FrustumCullingHandle, _ActiveChunkIDsBuffer, ActiveChunkIDsBuffer);
            // cs.SetInts(_ActiveChunkIDs, ids);
            cs.SetInt(_ActiveChunkCount, activeChunkIDs.Count);
        }
    }

    /// <summary>
    /// 生成一个字典（拿到当前所激活的terrain）
    /// </summary>
    private void InitializeTerrainGrid()
    {
        foreach (var t in Terrain.activeTerrains)
        {
            Vector3 pos = t.transform.position;// 这个pos是 Terrain 块的左下角（最小 X, 最小 Z）。
            // 计算该块的简易坐标索引
            Vector2Int index = new Vector2Int(Mathf.FloorToInt(pos.x / terrainSize), Mathf.FloorToInt(pos.z / terrainSize));
            terrainGrid[index] = t;
        }
    }

    /// <summary>
    /// 输入一个世界坐标，返回terrain
    /// </summary>
    private Terrain GetTerrainAtPos(Vector3 worldPos)
    {
        Vector2Int index = new Vector2Int(Mathf.FloorToInt(worldPos.x / terrainSize), Mathf.FloorToInt(worldPos.z / terrainSize));
        terrainGrid.TryGetValue(index, out Terrain t);
        return t;
    }
    /// <summary>
    /// 输入一个Chunk坐标，返回terrain
    /// </summary>
    private Terrain GetTerrainAtPos(Vector2Int chunk)
    {
        Vector2Int index = new Vector2Int(Mathf.FloorToInt(chunk.x * chunkSize / terrainSize), Mathf.FloorToInt(chunk.y * chunkSize / terrainSize));
        terrainGrid.TryGetValue(index, out Terrain t);
        return t;
    }

    private void OnDrawGizmos()
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

        foreach (var pair in terrainGrid)
        {
            Vector2Int coord = pair.Key;
            Vector3 center = new Vector3(coord.x * terrainSize + terrainSize * 0.5f, 0, coord.y * terrainSize + terrainSize * 0.5f);

            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(center, new Vector3(terrainSize * 0.9f, 0.2f, terrainSize * 0.9f));
        }
        // Gizmos.color = Color.gray;
        // Gizmos.DrawCube(lod0renderParams.worldBounds.center, lod0renderParams.worldBounds.size);
    }

    private void OnDisable()
    {
        grassDataBuffer.Dispose();
        ActiveChunkIDsBuffer.Dispose();
        lod0Buffer.Dispose();
        lod1Buffer.Dispose();
        lod2Buffer.Dispose();
        lod0argsBuffer.Dispose();
        lod1argsBuffer.Dispose();
        lod2argsBuffer.Dispose();
    }
}