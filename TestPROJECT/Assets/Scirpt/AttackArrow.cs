using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class AttackArrow : MonoBehaviour
{
    private Vector3 player;
    private Vector3 target;
    private Camera cam;
    [SerializeReference] private Image arrow;
    [SerializeReference] private float fadeTime;
    void OnEnable()
    {
        cam = Camera.main;
        arrow.color = new Color(1, 1, 1, 1);
        arrow.DOFade(0, fadeTime).OnComplete(() => gameObject.SetActive(false));// 对象池
    }
    
    /// <summary>
    /// 设置坐标
    /// </summary>
    /// <param name="p">玩家坐标</param>
    /// <param name="t">攻击者坐标</param>
    public void SetPosition(Vector3 p, Vector3 t)
    {
        player = p;
        target = t;
    }
    void LateUpdate()
    {
        var a = Quaternion.LookRotation(cam.transform.forward).eulerAngles.y;
        Vector3 dir = (target - player).normalized;
        var b = Quaternion.LookRotation(dir).eulerAngles.y;
        arrow.rectTransform.eulerAngles = new Vector3(0, 0, a - b);
    }

}
