using UnityEngine;
using TMPro; // 如果使用 TextMeshPro 控件，需要这个命名空间

public class FPSDisplay : MonoBehaviour
{
    // 拖拽 UGUI Text 或 TextMeshProUGUI 组件到 Inspector 中
    [SerializeField]
    private TextMeshProUGUI fpsText;

    // FPS 更新的频率（例如每 0.5 秒更新一次，避免每帧抖动）
    private float refreshRate = 0.2f;
    private float timer;
    void Awake()
    {
        // 1. **强制关闭 V-Sync**：这是最常见的阻碍
        // 0 = 关闭 V-Sync，允许帧率超过屏幕刷新率（或达到最大可能）
        QualitySettings.vSyncCount = 0;

        // 2. **设置目标帧率为最大**
        // -1 = 让游戏以最快速度渲染
        // 或者强制设置一个高值，例如 120
        Application.targetFrameRate = 120; // 尝试设置一个明确的高值

        Debug.Log("帧率设置尝试：V-Sync = 0, TargetFPS = 120");
    }
    private void Update()
    {
        // 累加时间
        timer += Time.deltaTime;

        // 达到刷新间隔后更新 FPS
        if (timer >= refreshRate)
        {
            // FPS = 1 / 帧耗时
            float fps = 1f / Time.unscaledDeltaTime;

            // 格式化输出到文本组件
            // Mathf.RoundToInt 将浮点数转换为整数
            fpsText.text = $"FPS: {Mathf.RoundToInt(fps)}";

            // 重置计时器
            timer = 0f;
        }
    }
}