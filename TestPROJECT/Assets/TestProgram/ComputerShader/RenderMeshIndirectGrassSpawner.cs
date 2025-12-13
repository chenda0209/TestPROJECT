using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class RenderMeshIndirectGrassSpawner : MonoBehaviour
{
    public GameObject grassPrefab;
    private Mesh grassMesh;
    private RenderParams renderParams;
    private Material grassMaterial;
    private Camera cam;


    public ComputeShader cullComputerShader;// 主要GPU着色器，用于剔除摄像机和Hi-z剔除
    private ComputeBuffer frustumBuffer;// 常量BUFFER，用于传输摄像机六面体
    private RenderTexture resultTexture;
    private ComputeBuffer computeBuffer;// RW读写着色器，基本ComputeBuffer用来传输所有物体的原始数据和包围盒
    private ComputeBuffer appendBuffer; // Append Buffer
    // ArgumentsBuffer 的数据结构（C# 数组）
    private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
    private GraphicsBuffer argsBuffer;// 用来RenderMeshIndirect的接口特殊ComputeBuffer，他是这么说的，我也没搞明白具体差别，反正必须要使用这个类别
    private int kernelHandle;// 获取 Compute Shader 中特定函数的唯一标识符（ID或句柄），以便 CPU 能够准确地告诉 GPU “请运行这个函数”。
    private uint threadGroupSizeX;
    private uint threadGroupSizeY;
    public int instanceCount = 100000;
    private static readonly int OriginalGrassDataID = Shader.PropertyToID("_GrassDataBuffer"); // 假设 HLSL 变量名是 _OriginalGrassData
    private static readonly int VisibleGrassDataID = Shader.PropertyToID("_VisibleIndexBuffer"); // 假设 HLSL 变量名是 _OriginalGrassData
    // 全局存储视锥体平面的 Vector4 数组
    private Vector4[] frustumPlanesArray = new Vector4[6];
    private static readonly int FrustumPlanesID = Shader.PropertyToID("_FrustumPlanes"); // 缓存 Shader ID

    [StructLayout(LayoutKind.Sequential)]
    public struct GrassData
    {
        public Vector4 worldPos;
        public Vector4 r;
        public Matrix4x4 worldMatrix;
    }
    // 确保内存布局是连续的、顺序的
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

        // 收集 Mesh 和 Material
        grassMesh = grassPrefab.GetComponent<MeshFilter>().sharedMesh;
        grassMaterial = grassPrefab.GetComponent<MeshRenderer>().sharedMaterial;
        // 3. 检查 Instancing 启用状态
        if (!grassMaterial.enableInstancing)
        {
            // 启用 Instancing（如果材质的Shader支持且 Inspector 中未勾选）
            grassMaterial.enableInstancing = true;
            Debug.LogWarning("GPU Instancing was automatically enabled on the material.");
        }
        renderParams = new(grassMaterial);
        renderParams.camera = cam;

        // 获取 Compute Shader 中特定函数的唯一标识符（ID或句柄），以便 CPU 能够准确地告诉 GPU “请运行这个函数”。
        kernelHandle = cullComputerShader.FindKernel("CSMain");
        // 获取线程组尺寸（仅执行这一次）
        cullComputerShader.GetKernelThreadGroupSizes(kernelHandle, out threadGroupSizeX, out threadGroupSizeY, out _);

        // 声明并计算computeBuffer大小,这里计算的是物体的数据结构*数量,在GPU Memory显存内划分并创建了一块区域，用于存储。
        // ComputeBuffer 是 Unity 提供的一种 GPU 资源。它的主要目的是在 CPU 和 GPU 之间高效地传递大量数据。
        // 由于 GPU 访问显存比访问系统内存（RAM）快得多，所以将数据放在 ComputeBuffer 中（即在显存中）是实现高性能渲染和计算的关键。
        // Shader 通过一个特殊的输入（通常是 StructuredBuffer，StructuredBuffer的类型通常与ComputeBufferType对应）来访问这些数据。
        // 如果不使用computeBuffer，也可以使用rendertexture传入数据，一个是SetBuff一个是SetTexture。
        int grassStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(GrassData));
        computeBuffer = new ComputeBuffer(instanceCount, grassStride, ComputeBufferType.Default);

        // 声明并计算frustumBuffer大小，这里传入6个简单的摄像机六面用于剔除，所以使用常量Cbuffer。
        // int frustumStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(FrustumData));
        // frustumBuffer = new ComputeBuffer(1, frustumStride, ComputeBufferType.Constant);

        // 可见物体的buffer
        int visibleIndexStride = sizeof(uint);
        appendBuffer = new ComputeBuffer(instanceCount, visibleIndexStride, ComputeBufferType.Append);

        args[0] = grassMesh.GetIndexCount(0);
        argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 5, sizeof(uint));
        argsBuffer.SetData(args);


        // 如果要传入RT当数据，就这么声明
        if (resultTexture == null)
        {
            // 宽度、高度、深度（通常为0）、格式（ARGB32 对应 float4）
            resultTexture = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32);
            // **最关键的设置**: 必须启用随机写入才能被 Compute Shader 写入
            resultTexture.enableRandomWrite = true;
            // 创建 GPU 资源
            resultTexture.Create();
        }


        // 创建要渲染物体的数据数组
        GrassData[] initialData = new GrassData[instanceCount];
        for (int i = 0; i < instanceCount; i++)
        {
            // **!!! 您的自定义生成逻辑 !!!**
            // 示例：在 (0, 0, 0) 周围随机生成草地
            Vector3 randomPos = new Vector3(Random.Range(-50f, 50f), 0f, Random.Range(-50f, 50f));
            // float randomRadius = Random.Range(0.5f, 1.5f);

            // 假设您只关心位置，世界矩阵可以从位置、旋转、缩放计算
            Quaternion randomRot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            Vector3 randomScale = Vector3.one * Random.Range(0.1f, 0.2f);
            Matrix4x4 matrix = Matrix4x4.TRS(randomPos, randomRot, randomScale);

            // 填充 GrassData 结构体
            initialData[i] = new GrassData
            {
                worldPos = new Vector4(randomPos.x, randomPos.y, randomPos.z, 1f),
                r = new Vector4(0.05f, 0f, 0f, 0f), // 将半径存储在 Vector4.x
                worldMatrix = matrix
            };
        }
        // 传输物体数据！CPU只需要一次调用。
        computeBuffer.SetData(initialData);
        // 传输数据后设置computershader，并标记BUFFER的数据名字进行计算。
        cullComputerShader.SetBuffer(kernelHandle, OriginalGrassDataID, computeBuffer);

        // uint[] visibleNum = new uint[instanceCount];
        // // 传输需要剔除数据！CPU只需要一次调用。
        // appendBuffer.SetData(visibleNum);
        // 传输数据后设置computershader，并标记BUFFER的数据名字进行计算。
        cullComputerShader.SetBuffer(kernelHandle, VisibleGrassDataID, appendBuffer);
        cullComputerShader.SetInt("_InstanceCount", instanceCount);
    }


    /// <summary>
    /// 计算摄像机视锥体平面，并立即通过全局 Uniform 数组传输给所有 Shader。
    /// </summary>
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
        Shader.SetGlobalVectorArray(FrustumPlanesID, frustumPlanesArray);
    }

    void Update()
    {
        appendBuffer.SetCounterValue(0);
        PrepareAndSetFrustumPlanes();
        // 2. 【修正 4】调度 Compute Shader (使用一维调度)
        uint totalGroupSize = threadGroupSizeX * threadGroupSizeY;
        int threadGroupsX = Mathf.CeilToInt((float)instanceCount / totalGroupSize);
        cullComputerShader.Dispatch(kernelHandle, threadGroupsX, 1, 1); // Z 必须是 1
        GraphicsBuffer.CopyCount(appendBuffer, argsBuffer, sizeof(uint));
    }

    void LateUpdate()
    {
        Graphics.RenderMeshIndirect(renderParams, grassMesh, argsBuffer, 1);
    }
    void OnDisable()
    {
        computeBuffer.Dispose();
        appendBuffer.Dispose();
        argsBuffer.Dispose();
    }
}
