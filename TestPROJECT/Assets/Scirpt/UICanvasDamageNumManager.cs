using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
public class UICanvasDamageNumManager : MonoBehaviour
{
    [SerializeReference] DamageNum damageNumNormal;
    [SerializeReference] DamageNum damageNumCritical;
    // [SerializeReference] Transform damageNumParent;

    /// <summary>
    /// 生成伤害字体
    /// </summary>
    /// <param name="pos">生成伤害字体的世界坐标</param>
    /// <param name="damage">伤害值</param>
    /// <param name="isCritical">是否致命</param>
    public void CreateNum(Vector3 pos, float damage, bool isCritical)
    {
        DamageNum num = isCritical ? Instantiate(damageNumCritical, transform) : Instantiate(damageNumNormal, transform);
        num.SetDamage(pos, damage);
    }
    public void CreateNum(Vector3 pos, float damage)
    {
        DamageNum num = Instantiate(damageNumNormal, transform);
        num.SetDamage(pos, damage);
    }
    

    
    // 测试
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // ---------------------------------------------
            // 步骤 1: 将鼠标的屏幕坐标转换成一条射线 (Ray)
            // Input.mousePosition 返回 (x, y, 0) 的屏幕像素坐标
            // ---------------------------------------------
            Vector2 centerScreen = new Vector2(Screen.width / 2, Screen.height / 2);
            // Ray ray = Camera.main.ScreenPointToRay(centerScreen);
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            // 用于存储射线击中信息的结果对象
            RaycastHit hit;

            // 设置射线最大检测距离 (可选，Physics.Raycast默认检测无限远)
            float maxDistance = 1000f;
            float randomnum = Random.Range(0, 9999);
            // ---------------------------------------------
            // 步骤 2: 发射射线并检测是否击中物体
            // ---------------------------------------------
            // Physics.Raycast(射线, out 击中信息, 距离, 过滤层级)
            if (Physics.Raycast(ray, out hit, maxDistance))
            {
                // 击中点的世界坐标
                Vector3 worldPoint = hit.point;

                // 被击中的 GameObject
                GameObject hitObject = hit.collider.gameObject;

                Debug.Log($"射线击中物体: {hitObject.name}");
                Debug.Log($"击中的世界坐标是: {worldPoint}");

                CreateNum(worldPoint, randomnum, true);
                // 可以在这里执行如：选中单位、移动角色、破坏物体等操作
            }
            else
            {
                // 射线没有击中任何带有 Collider 的物体
                Debug.Log("射线没有击中任何物体。");
            }
        }
        if (Input.GetMouseButtonDown(1))
        {
            // ---------------------------------------------
            // 步骤 1: 将鼠标的屏幕坐标转换成一条射线 (Ray)
            // Input.mousePosition 返回 (x, y, 0) 的屏幕像素坐标
            // ---------------------------------------------
            Vector2 centerScreen = new Vector2(Screen.width / 2, Screen.height / 2);
            Ray ray = Camera.main.ScreenPointToRay(centerScreen);

            // 用于存储射线击中信息的结果对象
            RaycastHit hit;

            // 设置射线最大检测距离 (可选，Physics.Raycast默认检测无限远)
            float maxDistance = 1000f;
            float randomnum = Random.Range(0, 9999);
            // ---------------------------------------------
            // 步骤 2: 发射射线并检测是否击中物体
            // ---------------------------------------------
            // Physics.Raycast(射线, out 击中信息, 距离, 过滤层级)
            if (Physics.Raycast(ray, out hit, maxDistance))
            {
                // 射线击中了物体！

                // 击中点的世界坐标
                Vector3 worldPoint = hit.point;

                // 被击中的 GameObject
                GameObject hitObject = hit.collider.gameObject;

                Debug.Log($"射线击中物体: {hitObject.name}");
                Debug.Log($"击中的世界坐标是: {worldPoint}");

                CreateNum(worldPoint, randomnum);
                // 可以在这里执行如：选中单位、移动角色、破坏物体等操作
            }
            else
            {
                // 射线没有击中任何带有 Collider 的物体
                Debug.Log("射线没有击中任何物体。");
            }
        }
    }
}
