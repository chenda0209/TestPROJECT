using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIMiniMap : MonoBehaviour
{
    [SerializeReference] private Transform player;
    [SerializeReference] private Transform leftBottomMark;
    [SerializeReference] private Transform rightUpMark;
    [SerializeReference] private RawImage miniMap;
    [SerializeReference] private Image pointer;
    [SerializeReference][Range(0f, 1f)] private float sizeDelta = 0;
    private void OnEnable()
    {

    }

    /// <summary>
    /// 传入玩家和地图定位的物体坐标（一般使用地图中的空物体或者标记物体做标注）
    /// </summary>
    /// <param name="player"></param>
    /// <param name="leftBottomMark">左下</param>
    /// <param name="rightUpMark">右上</param>
    public void InitializingMiniMap(Transform player, Transform leftBottomMark, Transform rightUpMark)
    {
        this.player = player;
        this.leftBottomMark = leftBottomMark;
        this.rightUpMark = rightUpMark;
    }

    public void SetSizeDelta(float val)
    {
        sizeDelta = val;
    }

    //编辑模式
    private void OnValidate()
    {
        if (player || leftBottomMark || leftBottomMark)
        {
            float uvX = (player.position.x - leftBottomMark.position.x) / (rightUpMark.position.x - leftBottomMark.position.x) - 0.5f * (1 - sizeDelta);
            float uvY = (player.position.z - leftBottomMark.position.z) / (rightUpMark.position.z - leftBottomMark.position.z) - 0.5f * (1 - sizeDelta);
            miniMap.uvRect = new Rect(uvX, uvY, 1 - sizeDelta, 1 - sizeDelta);
        }

    }

    private void LateUpdate()
    {
        if (player || leftBottomMark || leftBottomMark)
        {
            pointer.rectTransform.eulerAngles = new(0, 0, -player.eulerAngles.y);

            float uvX = (player.position.x - leftBottomMark.position.x) / (rightUpMark.position.x - leftBottomMark.position.x) - 0.5f * (1 - sizeDelta);
            float uvY = (player.position.z - leftBottomMark.position.z) / (rightUpMark.position.z - leftBottomMark.position.z) - 0.5f * (1 - sizeDelta);
            miniMap.uvRect = new Rect(uvX, uvY, 1 - sizeDelta, 1 - sizeDelta);
        }

    }
}
