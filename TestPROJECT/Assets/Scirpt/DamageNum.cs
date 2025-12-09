using System.Collections;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class DamageNum : MonoBehaviour
{
    [SerializeReference] RectTransform rectTransform;
    [SerializeReference] RectTransform textRectTransform;
    [SerializeReference] TMP_Text text;
    Vector3 startPoint;
    void OnEnable()
    {
        textRectTransform.localPosition = Vector3.zero;
        textRectTransform.localScale = Vector3.one;
        textRectTransform.DOScale(1.3f, 0.3f);
        textRectTransform.DOPunchAnchorPos(new Vector2(0, 50), .4f, 1, 0).OnComplete(() => gameObject.SetActive(false));// 对象池
    }

    public void SetDamage(Vector3 pos, float damage)
    {
        startPoint = pos;
        text.text = NumToSprite((int)damage);
    }
    public void SetDamage(Vector3 pos, int damage)
    {
        startPoint = pos;
        text.text = NumToSprite(damage);
    }


    string NumToSprite(int num)
    {
        string numString = num.ToString();
        StringBuilder resultBuilder = new StringBuilder();
        foreach (char digitChar in numString)
        {
            // 检查字符是否是数字
            if (char.IsDigit(digitChar))
            {
                resultBuilder.Append($"<sprite name=\"{digitChar}\">");
            }
            else if (digitChar == '-' && num < 0)
            {
                // 如果需要为负号添加特定的Sprite标签
                // 请确保您的Sprite Asset中有一个名为"Default"的Sprite
                resultBuilder.Append("<sprite name=\"Default\">");
            }
        }
        return resultBuilder.ToString();
    }

    void LateUpdate()
    {
        rectTransform.position = RectTransformUtility.WorldToScreenPoint(Camera.main, startPoint);
        // 如果坐标转化有问题
        // RectTransformUtility.ScreenPointToLocalPointInRectangle()
    }

}
