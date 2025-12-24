using UnityEngine;
using System.Collections.Generic;

public class BonesTools : MonoBehaviour
{
    [Header("配置引用")]
    public GameObject targetCharacter; // 人物B
    public GameObject clothingPrefab;  // 衣服A

    [ContextMenu("执行一键换装")]
    public void StartDressing()
    {
        if (targetCharacter == null || clothingPrefab == null)
        {
            Debug.LogError("请先指定人物和衣服！");
            return;
        }

        // 1. 实例化衣服并获取其 SMR
        GameObject clothesInst = Instantiate(clothingPrefab);
        clothesInst.name = clothingPrefab.name; // 去掉 (Clone) 字样
        SkinnedMeshRenderer[] clothesSMRs = clothesInst.GetComponentsInChildren<SkinnedMeshRenderer>();

        // 2. 扫描人物B的所有骨骼，建立快速索引
        Dictionary<string, Transform> charBoneDict = new Dictionary<string, Transform>();
        foreach (Transform t in targetCharacter.GetComponentsInChildren<Transform>())
        {
            if (!charBoneDict.ContainsKey(t.name)) charBoneDict.Add(t.name, t);
        }

        // 3. 记录旧骨骼根节点（准备稍后删除）
        // 通常 FBX 里的骨架根叫 Hips, Root, 或者跟模型名一样
        HashSet<Transform> oldSkeletons = new HashSet<Transform>();

        // 4. 对衣服的每一块网格进行重定向
        foreach (var smr in clothesSMRs)
        {
            Transform[] oldBones = smr.bones;
            Transform[] newBones = new Transform[oldBones.Length];

            for (int i = 0; i < oldBones.Length; i++)
            {
                // 收集旧骨头，用于后续清理
                if (oldBones[i] != null) oldSkeletons.Add(GetRootmostParent(oldBones[i], clothesInst.transform));

                // 获取映射后的名字（处理名字不一样的情况）
                string mappedName = GetMappedName(oldBones[i].name);

                if (charBoneDict.TryGetValue(mappedName, out Transform targetBone))
                {
                    newBones[i] = targetBone;
                }
                else
                {
                    newBones[i] = oldBones[i];
                    Debug.LogWarning($"未匹配到骨骼: {oldBones[i].name} -> {mappedName}");
                }
            }

            smr.bones = newBones;
            
            // 重新定向 RootBone
            if (smr.rootBone != null && charBoneDict.TryGetValue(GetMappedName(smr.rootBone.name), out Transform newRoot))
            {
                smr.rootBone = newRoot;
            }

            // 将 SMR 节点移动到人物 B 下，方便管理
            smr.transform.SetParent(targetCharacter.transform);
        }

        // 5. 清理 A 留下的“废弃骨架”
        foreach (var skeleton in oldSkeletons)
        {
            if (skeleton != null) DestroyImmediate(skeleton.gameObject);
        }

        // 6. 最后删除衣服实例的空外壳
        DestroyImmediate(clothesInst);

        Debug.Log("<color=green>换装成功！衣服已绑定，冗余骨骼已清理。</color>");
    }

    // 处理名字不一致的映射逻辑
    string GetMappedName(string originalName)
    {
        switch (originalName)
        {
            case "low_arm_L": return "LeftForearm"; // 举例：衣服名 -> 人物名
            case "low_arm_R": return "RightForearm";
            // 如果名字一样，就不用写，走 default
            default: return originalName;
        }
    }

    // 辅助函数：寻找骨架的最顶层父级（避开删错衣服网格节点）
    Transform GetRootmostParent(Transform bone, Transform limit)
    {
        while (bone.parent != null && bone.parent != limit)
        {
            bone = bone.parent;
        }
        return bone;
    }
}