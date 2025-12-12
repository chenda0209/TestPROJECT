using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

public class RenderMeshIndirectGrassSpawner : MonoBehaviour
{
    public GameObject grassPrefab;
    private Mesh grassMesh;
    private RenderParams renderParams;
    private Material grassMaterial;



    public ComputeShader cullComputerShader;// 主要GPU着色器，用于剔除摄像机和Hi-z剔除
    private ComputeBuffer frustumBuffer;// 常量BUFFER，用于传输摄像机六面体
    private ComputeBuffer computeBuffer;// RW读写着色器，基本ComputeBuffer用来传输所有物体的原始数据和包围盒
    private GraphicsBuffer graphicsBuffer;// 用来RenderMeshIndirect的接口特殊ComputeBuffer，他是这么说的，我也没搞明白具体差别，反正必须要使用这个类别
    private int kernelHandle;// 获取 Compute Shader 中特定函数的唯一标识符（ID或句柄），以便 CPU 能够准确地告诉 GPU “请运行这个函数”。
    private uint threadGroupSizeX;
    private uint threadGroupSizeY;
    public int instanceCount = 100000;

    [StructLayout(LayoutKind.Sequential)]
    public struct GrassData
    {
        public Vector3 worldPos;
        public Matrix4x4 worldMatrix;
        public float r;
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


        // 获取 Compute Shader 中特定函数的唯一标识符（ID或句柄），以便 CPU 能够准确地告诉 GPU “请运行这个函数”。
        kernelHandle = cullComputerShader.FindKernel("CSMain");
        // 获取线程组尺寸（仅执行这一次）
        cullComputerShader.GetKernelThreadGroupSizes(kernelHandle, out threadGroupSizeX, out threadGroupSizeY, out _);

        // 声明并计算computeBuffer大小,这里计算的是物体的数据结构*数量,在GPU Memory显存内划分并创建了一块区域，用于存储
        // ComputeBuffer 是 Unity 提供的一种 GPU 资源。它的主要目的是在 CPU 和 GPU 之间高效地传递大量数据，
        // 由于 GPU 访问显存比访问系统内存（RAM）快得多，所以将数据放在 ComputeBuffer 中（即在显存中）是实现高性能渲染和计算的关键。
        // Shader 通过一个特殊的输入（通常是 StructuredBuffer，StructuredBuffer的类型通常与ComputeBufferType对应）来访问这些数据。
        int grassStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(GrassData));
        computeBuffer = new ComputeBuffer(instanceCount, grassStride, ComputeBufferType.Default);

        // 声明并计算frustumBuffer大小，这里传入6个简单的摄像机六面用于剔除，所以使用常量Cbuffer
        frustumBuffer = new ComputeBuffer(instanceCount, grassStride, ComputeBufferType.Constant);

    }

    void Update()
    {





        // Graphics.RenderMeshIndirect(renderParams, grassMesh, graphicsBuffer);
    }

    void OnDisable()
    {

    }
}
