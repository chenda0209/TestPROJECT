using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UICanvasAttackArrowManager : MonoBehaviour
{
    public Transform player;//测试
    [SerializeField] AttackArrow attackArrow;

    /// <summary>
    /// 创建受击指示器
    /// </summary>
    /// <param name="p">玩家坐标</param>
    /// <param name="t">攻击者坐标</param>
    public void CreateAttackArrow(Vector3 playerPosition, Vector3 targetPosition)
    {
        AttackArrow arrow = Instantiate(attackArrow, transform);
        arrow.SetPosition(playerPosition, targetPosition);
    }
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

            if (Physics.Raycast(ray, out hit, float.MaxValue))
            {
                // 击中点的世界坐标
                Vector3 worldPoint = hit.point;

                // 被击中的 GameObject
                GameObject hitObject = hit.collider.gameObject;

                Debug.Log($"射线击中物体: {hitObject.name}");
                Debug.Log($"击中的世界坐标是: {worldPoint}");

                CreateAttackArrow(player.position, worldPoint);
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
