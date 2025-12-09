using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HpBar : MonoBehaviour
{
    [SerializeReference] Slider currentHpSlider;
    [SerializeReference] Slider deleyHpSlider;
    [SerializeReference] CanvasGroup canvasGroup;

    public float subtractSpeed = 1;
    public float fadeTime = 1;
    public float activeTime = 4;

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
            Show();
            StopCoroutine(nameof(ConutToClose));
            StopCoroutine(nameof(HpBarFade));
            StartCoroutine(nameof(ConutToClose));
        }
        if (deleyHpSlider.value > currentHpSlider.value)
            StartCoroutine(nameof(HpBarDeley));
    }

    public void Fade()
    {
        StartCoroutine(nameof(HpBarFade));
    }
    public void Show()
    {
        canvasGroup.alpha = 1;
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

    IEnumerator ConutToClose()
    {
        float count = activeTime;
        while (count > 0)
        {
            count -= Time.deltaTime;
            if (count <= 0) Fade();
            yield return null;
        }
    }
    // 血条淡出

    IEnumerator HpBarFade()
    {
        float count = fadeTime;
        while (count > 0)
        {
            count -= Time.deltaTime;
            canvasGroup.alpha = count;
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
            Show();
        }
    }

}
