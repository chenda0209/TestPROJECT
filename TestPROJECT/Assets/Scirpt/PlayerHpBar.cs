using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHpBar : MonoBehaviour
{
    [SerializeReference] Slider currentHpSlider;
    [SerializeReference] Slider deleyHpSlider;

    public float subtractSpeed = 1;

    
    /// <summary> 
    /// 重置HPbar
    /// </summary> 
    public void ResetHp(float maxhp)
    {
        currentHpSlider.maxValue = maxhp;
        currentHpSlider.value = maxhp;
        currentHpSlider.minValue = 0;
        deleyHpSlider.maxValue = maxhp;
        deleyHpSlider.value = maxhp;
        deleyHpSlider.minValue = 0;
    }

    // 扣血 
    public void SubtractHp(float val)
    {
        if (currentHpSlider.value > 0)
        {
            currentHpSlider.value -= val;
        }
        if (deleyHpSlider.value > currentHpSlider.value)
            StartCoroutine(nameof(HpBarDeley));
    }


    // 血条红线
    IEnumerator HpBarDeley()
    {
        float count = 0;
        while (deleyHpSlider.value > currentHpSlider.value)
        {
            count += Time.deltaTime;
            // deleyHpSlider.value -= math.lerp(deleyHpSlider.value, currentHpSlider.value, count);
            deleyHpSlider.value -= count * subtractSpeed;
            yield return null;
        }
    }


    private void OnDisable()
    {
        StopCoroutine(nameof(HpBarDeley));
    }

    // 测试
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            SubtractHp(Random.Range(0, 50));
        }
        if (Input.GetKeyDown(KeyCode.G))
        {
            ResetHp(100);

        }
    }
}
