using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.VisualScripting;
using UnityEngine;

public class ChunkScanCircle : MonoBehaviour
{
    public Transform player;
    public ComputeShader cs;
    public Vector4 threshold; // lod远近设置
    private Vector4[] frustumPlanesArray = new Vector4[6];
    private Camera cam;
    private int instanceCountChunk;
    private int grassEachMeter = 4;
    private int chunkSize = 32;// 每块chunk分区大小
    private float viewRadius = 128; // 实际扫描的圆形半径（米）
    private float terrainSize = 1024; // 每块terrain的大小

    private Dictionary<Vector2Int, int> chunkToSlot = new Dictionary<Vector2Int, int>();
    private Dictionary<Vector2Int, Terrain> terrainGrid = new Dictionary<Vector2Int, Terrain>();

    private Stack<int> freeSlots = new Stack<int>();
    private List<Vector2Int> circleOffsetTable = new List<Vector2Int>(); // 预计算的圆形偏移表

    private Vector2Int lastPlayerChunk = new Vector2Int(-999, -999); // 用于跨界检测
    private int maxSlots = 200; // 根据半径覆盖面积适当调大

    [StructLayout(LayoutKind.Sequential)]
    public struct GrassData { public Vector3 worldPos; public float rotation; public float scale; };
    private GraphicsBuffer grassDataBuffer;
    private int generateGrassHandle;
    private int FrustumCullingHandle;

    private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
    public GameObject Lod0grassPrefab, Lod1grassPrefab, Lod2grassPrefab;
    private Mesh lod0grassMesh, lod1grassMesh, Lod2grassMesh;
    private RenderParams lod0renderParams, lod1renderParams, lod2renderParams;
    private Material lod0grassMaterial, lod1grassMaterial, lod2grassMaterial;

    private GraphicsBuffer lod0Buffer, lod1Buffer, lod2Buffer; // Append Buffer
    private GraphicsBuffer lod0argsBuffer, lod1argsBuffer, lod2argsBuffer;// 用来RenderMeshIndirect的接口特殊ComputeBuffer，他是这么说的，我也没搞明白具体差别，反正必须要使用这个类别
    private static readonly int FrustumPlanesID = Shader.PropertyToID("_FrustumPlanes"); // 缓存 Shader ID
    private static readonly int GrassDataBufferID = Shader.PropertyToID("_GrassDataBuffer");
    private static readonly int lod0BufferID = Shader.PropertyToID("_Lod0Buffer");
    private static readonly int lod1BufferID = Shader.PropertyToID("_Lod1Buffer");
    private static readonly int lod2BufferID = Shader.PropertyToID("_Lod2Buffer");

    [StructLayout(LayoutKind.Sequential)]
    public struct FrustumData // 摄像机的视锥体由六个平面定义：左、右、上、下、近、远。每个平面都由一个四维向量 $\vec{P} = (A, B, C, D)$ 表示
    {
        public Vector4 LeftPlane;
        public Vector4 RightPlane;
        public Vector4 BottomPlane;
        public Vector4 TopPlane;
        public Vector4 NearPlane;
        public Vector4 FarPlane;
    }
    void Start()
    {
        if (cam == null) cam = Camera.main;
        // 1. 预计算圆形偏移表 (只在启动时做一次)
        InitializeCircleOffsets();

        // 生成场景的terrain列表
        InitializeTerrainGrid();
        // 2. 初始化显存坑位
        maxSlots = circleOffsetTable.Count + 10; // 稍微多备几个防止边界闪烁
        for (int i = 0; i < maxSlots; i++) freeSlots.Push(i);

        // 写入草的computerbuffer
        instanceCountChunk = (int)(chunkSize * chunkSize * grassEachMeter * grassEachMeter);
        int grassStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(GrassData));
        grassDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, instanceCountChunk * maxSlots, grassStride);

        // 收集 Mesh 和 Material
        lod0grassMesh = Lod0grassPrefab.GetComponent<MeshFilter>().sharedMesh;
        lod1grassMesh = Lod1grassPrefab.GetComponent<MeshFilter>().sharedMesh;
        Lod2grassMesh = Lod2grassPrefab.GetComponent<MeshFilter>().sharedMesh;
        lod0grassMaterial = Lod0grassPrefab.GetComponent<MeshRenderer>().sharedMaterial;
        lod1grassMaterial = Lod1grassPrefab.GetComponent<MeshRenderer>().sharedMaterial;
        lod2grassMaterial = Lod2grassPrefab.GetComponent<MeshRenderer>().sharedMaterial;
        // 3. 检查 Instancing 启用状态
        if (!lod0grassMaterial.enableInstancing)
        {
            // 启用 Instancing（如果材质的Shader支持且 Inspector 中未勾选）
            lod0grassMaterial.enableInstancing = true;
            Debug.LogWarning("GPU Instancing was automatically enabled on the material.");
        }
        lod0renderParams = new(lod0grassMaterial);
        if (!lod1grassMaterial.enableInstancing)
        {
            // 启用 Instancing（如果材质的Shader支持且 Inspector 中未勾选）
            lod1grassMaterial.enableInstancing = true;
            Debug.LogWarning("GPU Instancing was automatically enabled on the material.");
        }
        lod1renderParams = new(lod1grassMaterial);
        if (!lod2grassMaterial.enableInstancing)
        {
            // 启用 Instancing（如果材质的Shader支持且 Inspector 中未勾选）
            lod2grassMaterial.enableInstancing = true;
            Debug.LogWarning("GPU Instancing was automatically enabled on the material.");
        }
        lod2renderParams = new(lod2grassMaterial);

        Bounds playerBounds = new(Vector3.zero, new Vector3(1000, 1f, 1000));
        // !!!!!!!!!!!!!!!!!!!坑，不设置AABB，他就会始终把世界原点当剔除，这个相当于一个初级剔除，然后才是摄像机剔除。
        lod0renderParams.worldBounds = playerBounds;
        lod1renderParams.worldBounds = playerBounds;
        lod2renderParams.worldBounds = playerBounds;

        // 可见物体的buffer,只读模式，所以不需要去SetData
        int visibleIndexStride = sizeof(int);
        // appendBuffer = new ComputeBuffer(instanceCountChunk, visibleIndexStride, ComputeBufferType.Append);//只用setbuffer，不需要setdata
        lod0Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, instanceCountChunk * maxSlots, visibleIndexStride);//只用setbuffer，不需要setdata
        lod1Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, instanceCountChunk * maxSlots, visibleIndexStride);//只用setbuffer，不需要setdata
        lod2Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, instanceCountChunk * maxSlots, visibleIndexStride);//只用setbuffer，不需要setdata

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

        cs.SetInt("_InstanceCount", instanceCountChunk * maxSlots);//指定绘制数量
        cs.SetVector("_Threshold", threshold);// 远近分界线LOD

        // 传输数据后设置computershader，并标记BUFFER的数据名字进行计算。
        cs.SetBuffer(generateGrassHandle, GrassDataBufferID, grassDataBuffer);
        cs.SetBuffer(FrustumCullingHandle, GrassDataBufferID, grassDataBuffer);

        cs.SetBuffer(FrustumCullingHandle, lod0BufferID, lod0Buffer);
        cs.SetBuffer(FrustumCullingHandle, lod1BufferID, lod1Buffer);
        cs.SetBuffer(FrustumCullingHandle, lod2BufferID, lod2Buffer);
        // 设置材质的读写buffer
        lod0grassMaterial.SetBuffer(GrassDataBufferID, grassDataBuffer);
        lod0grassMaterial.SetBuffer(lod0BufferID, lod0Buffer);
        lod1grassMaterial.SetBuffer(GrassDataBufferID, grassDataBuffer);
        lod1grassMaterial.SetBuffer(lod1BufferID, lod1Buffer);
        lod2grassMaterial.SetBuffer(GrassDataBufferID, grassDataBuffer);
        lod2grassMaterial.SetBuffer(lod2BufferID, lod2Buffer);
    }

    void InitializeCircleOffsets()
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
    public RenderTexture rt;
    void Update()
    {
        if (player == null) return;

        // 跨界检测：只有玩家跨过格子边界才执行大数据量更新
        Vector2Int currentChunk = new Vector2Int(Mathf.FloorToInt(player.position.x / chunkSize), Mathf.FloorToInt(player.position.z / chunkSize));
        if (currentChunk != lastPlayerChunk)
        {
            InitializeTerrainGrid();
            UpdateChunks(currentChunk);
            lastPlayerChunk = currentChunk;
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
                Terrain t = GetTerrainAtPos(coord);
                if (t == null) continue; // 必须判空！
                int assignedSlot = freeSlots.Pop();
                chunkToSlot.Add(coord, assignedSlot);

                Vector3 basePos = new Vector3(coord.x * chunkSize, 0, coord.y * chunkSize);
                // GetHeightmapTexture 是地形生成的实时高度图
                cs.SetTexture(generateGrassHandle, "_HeightMap", t.terrainData.heightmapTexture);
                // 还需要传一个地形高度，因为高度图里存的是 0-1 的归一化值
                cs.SetFloat("_TerrainHeight", t.terrainData.size.y);
                cs.SetTexture(generateGrassHandle, "_SplatMap", t.terrainData.GetAlphamapTexture(0));
                cs.SetVector("_ChunkBasePos", basePos);
                cs.SetVector("_TerrainBasePos", t.transform.position);
                cs.SetInt("_TargetSlotID", assignedSlot);
                cs.Dispatch(generateGrassHandle, 16, 16, 1);
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

    void LateUpdate()
    {

        lod0Buffer.SetCounterValue(0);//清除计数
        lod1Buffer.SetCounterValue(0);//清除计数
        lod2Buffer.SetCounterValue(0);//清除计数
        PrepareAndSetFrustumPlanes();

        int threadGroups = Mathf.CeilToInt((float)(instanceCountChunk * maxSlots) / 64.0f);
        cs.Dispatch(FrustumCullingHandle, threadGroups, 1, 1); // Z 必须是 1

        GraphicsBuffer.CopyCount(lod0Buffer, lod0argsBuffer, sizeof(uint));
        Graphics.RenderMeshIndirect(lod0renderParams, lod0grassMesh, lod0argsBuffer);

        GraphicsBuffer.CopyCount(lod1Buffer, lod1argsBuffer, sizeof(uint));
        Graphics.RenderMeshIndirect(lod1renderParams, lod1grassMesh, lod1argsBuffer);

        GraphicsBuffer.CopyCount(lod2Buffer, lod2argsBuffer, sizeof(uint));
        Graphics.RenderMeshIndirect(lod2renderParams, Lod2grassMesh, lod2argsBuffer);

        uint[] debugArgs = new uint[5];
        lod0argsBuffer.GetData(debugArgs);
        Debug.Log($"GPU 汇报：LOD0 准备绘制的实例数量是: {debugArgs[1]}");
    }

    // CS摄像机剔除
    public void PrepareAndSetFrustumPlanes()
    {
        // 1. 获取摄像机平面
        // 注意：您需要确保 'camera' 变量已在类中定义并赋值
        Plane[] cameraPlanes = GeometryUtility.CalculateFrustumPlanes(cam);
        // 2. 将 Plane[] 转换为 Vector4[] (GPU 友好格式)
        // C# 的 Plane 结构体已经包含了法线(N)和距离(D)，顺序是固定的。
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
        // 3. 将数组设置给全局 Shader 属性
        // 这是最快、最简洁的传输方式
        // Shader.SetGlobalVectorArray(FrustumPlanesID, frustumPlanesArray);
        cs.SetVectorArray(FrustumPlanesID, frustumPlanesArray);
        cs.SetVector("_CameraPos", cam.transform.position);
    }


    /// <summary>
    /// 生成一个字典（当前所激活的terrain）
    /// </summary>
    void InitializeTerrainGrid()
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
    public Terrain GetTerrainAtPos(Vector3 worldPos)
    {
        Vector2Int index = new Vector2Int(Mathf.FloorToInt(worldPos.x / terrainSize), Mathf.FloorToInt(worldPos.z / terrainSize));
        terrainGrid.TryGetValue(index, out Terrain t);
        return t;
    }
    /// <summary>
    /// 输入一个Chunk坐标，返回terrain
    /// </summary>
    public Terrain GetTerrainAtPos(Vector2Int chunk)
    {
        Vector2Int index = new Vector2Int(Mathf.FloorToInt(chunk.x * chunkSize / terrainSize), Mathf.FloorToInt(chunk.y * chunkSize / terrainSize));
        terrainGrid.TryGetValue(index, out Terrain t);
        return t;
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

        foreach (var pair in terrainGrid)
        {
            Vector2Int coord = pair.Key;
            Vector3 center = new Vector3(coord.x * terrainSize + terrainSize * 0.5f, 0, coord.y * terrainSize + terrainSize * 0.5f);

            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(center, new Vector3(terrainSize * 0.9f, 0.2f, terrainSize * 0.9f));
        }
    }

    void OnDisable()
    {
        grassDataBuffer.Dispose();
        lod0Buffer.Dispose();
        lod1Buffer.Dispose();
        lod2Buffer.Dispose();
        lod0argsBuffer.Dispose();
        lod1argsBuffer.Dispose();
        lod2argsBuffer.Dispose();
    }
}