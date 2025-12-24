using System.Collections.Generic;
using UnityEngine;

public class InstancedGrassSpawner : MonoBehaviour
{
    // --- 外部参数 ---
    public GameObject grassPrefab;
    public float spacing = 0.1f;
    [Range(1, 1000000)] // 限制输入范围，防止意外
    public int num = 10000;
    public float radius;
    // --- Instancing 核心数据 ---
    private Mesh grassMesh;
    private Material grassMaterial;
    private Matrix4x4[] instanceMatrices;

    // Graphics.DrawMeshInstanced 的限制：一个 Draw Call 最多 1023 个实例
    private const int MAX_INSTANCES_PER_CALL = 1023;
    private int instanceCount;
    private RenderParams renderParams;
    void Start()
    {
        // 1. 参数校验与初始化
        if (grassPrefab == null || grassPrefab.GetComponent<MeshFilter>() == null || grassPrefab.GetComponent<MeshRenderer>() == null)
        {
            Debug.LogError("Grass prefab must have a MeshFilter and MeshRenderer.");
            return;
        }

        // 2. 收集 Mesh 和 Material
        grassMesh = grassPrefab.GetComponent<MeshFilter>().sharedMesh;
        // 确保使用 Material.GetInstancedMaterial() 或 sharedMaterial。
        // 为了确保 Instancing 启用，我们直接获取 sharedMaterial 并确保其设置正确。
        grassMaterial = grassPrefab.GetComponent<MeshRenderer>().sharedMaterial;

        // 3. 检查 Instancing 启用状态
        if (!grassMaterial.enableInstancing)
        {
            // 启用 Instancing（如果材质的Shader支持且 Inspector 中未勾选）
            grassMaterial.enableInstancing = true;
            Debug.LogWarning("GPU Instancing was automatically enabled on the material.");
        }
        renderParams = new(grassMaterial);

        // 4. 计算并存储所有实例的变换矩阵
        GenerateMatrices(num, radius);

        // 销毁预制件，因为我们不再需要它在场景中的实例
        // 注意：如果你需要草地具有碰撞体或 MonoBehaviour，Instancing 方法不适用。
        // Destroy(grassPrefab);
    }
    private Plane[] frustumPlanes;
    private List<Matrix4x4> visibleMatrices = new List<Matrix4x4>();

    void Update()
    {
        // 在 Update 中更新视锥体平面（通常只需每帧更新一次）
        frustumPlanes = GeometryUtility.CalculateFrustumPlanes(Camera.main);
    }
    // --- 矩阵生成函数 ---
    public void GenerateMatrices(int totalNum, float radius)
    {
        if (totalNum <= 0) return;

        instanceCount = totalNum;
        instanceMatrices = new Matrix4x4[instanceCount];

        Vector3 spawnerPosition = transform.position;
        int currentCount = 0;

        // 我们使用一个循环来尝试放置实例，直到达到所需的 totalNum
        // 使用 'while' 循环，直到 currentCount 达到 totalNum
        while (currentCount < totalNum)
        {
            // 1. 随机生成一个位于 [-Radius, Radius] 范围内的点 (X, Z)
            float x = Random.Range(-radius, radius);
            float z = Random.Range(-radius, radius);

            Vector2 randomPoint = new Vector2(x, z);

            // 2. 核心：拒绝采样 (Rejection Sampling)
            // 检查这个点是否在圆形区域内
            if (randomPoint.magnitude <= radius)
            {
                // 3. 构造位置和旋转
                // 这里的 y 轴通常保持不变，或者使用 Raycast 来贴合地形高度
                Vector3 position = spawnerPosition + new Vector3(x, 0, z);
                Quaternion randomRotation = Quaternion.Euler(new Vector3(0, Random.Range(0, 360), 0));
                float scaleX = Random.Range(0.05f, 0.2f);
                float scaleY = Random.Range(0.05f, 0.2f);
                float scaleZ = Random.Range(0.05f, 0.2f);

                Vector3 randomScale = new Vector3(scaleX, scaleY, scaleZ);
                // 4. 构造矩阵
                Matrix4x4 matrix = Matrix4x4.TRS(position, randomRotation, randomScale);

                // 5. 存储矩阵并递增计数
                instanceMatrices[currentCount] = matrix;
                currentCount++;
            }

            // 注意：为防止无限循环，如果实例数量非常少，可能需要设置一个最大尝试次数。
            // 但对于数万个实例的草地，拒绝采样通常是高效的。
        }

        Debug.Log($"Generated {instanceMatrices.Length} instance matrices inside a circle with radius {radius}.");
    }

    // --- 绘制函数 ---
    void LateUpdate()
    {
        if (grassMesh == null || grassMaterial == null || instanceMatrices == null || instanceMatrices.Length == 0) return;

        // 计算需要的 Draw Call 次数
        int numInstances = instanceMatrices.Length;
        int numChunks = Mathf.CeilToInt((float)numInstances / MAX_INSTANCES_PER_CALL); // MAX_INSTANCES_PER_CALL = 1023

        // 循环绘制 Chunks
        for (int i = 0; i < numChunks; i++)
        {
            int startIdx = i * MAX_INSTANCES_PER_CALL;
            int count = Mathf.Min(MAX_INSTANCES_PER_CALL, numInstances - startIdx);

            // ----------------------------------------------------
            // ✅ 关键修正：手动创建子数组
            // 1. 创建一个临时数组，大小等于当前块的实例数
            Matrix4x4[] tempMatrices = new Matrix4x4[count];

            // 2. 将 instanceMatrices 数组中从 startIdx 开始的 count 个元素
            //    复制到 tempMatrices 数组中
            System.Array.Copy(instanceMatrices, startIdx, tempMatrices, 0, count);
            // ----------------------------------------------------

            // 📢 核心调用：Graphics.DrawMeshInstanced
            Graphics.RenderMeshInstanced(
                renderParams,
                grassMesh,
                0, // Submesh index
                tempMatrices
            );
        }
    }

    private void OnDisable()
    {
        // 确保在禁用时清理数据，但由于我们没有使用 Compute Buffer，此处可以省略复杂的清理。
    }
}