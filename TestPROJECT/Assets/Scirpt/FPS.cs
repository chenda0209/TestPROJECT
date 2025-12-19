using UnityEngine;
using TMPro;
using UnityEngine.Profiling; // 如果使用 TextMeshPro 控件，需要这个命名空间

public class FPSDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text fpsText, memoryText, cpuText, gpuText, dispPlayText;
    // FPS 更新的频率（例如每 0.5 秒更新一次，避免每帧抖动）
    [SerializeField] private float refreshRate = 0.5f;
    private float timer;


    // 用于真机获取
    private Recorder drRecorder;
    private Recorder vertRecorder;

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

    void Start()
    {
        drRecorder = Recorder.Get("Render.DrawCalls");
        vertRecorder = Recorder.Get("Render.Vertices");
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

            DisplayMemory();



            // 捕获最近一帧的时间戳
            FrameTimingManager.CaptureFrameTimings();

            // 获取第一条记录
            FrameTiming[] timings = new FrameTiming[1];
            uint count = FrameTimingManager.GetLatestTimings(1, timings);

            if (count > 0)
            {
                double cpuTime = timings[0].cpuFrameTime;
                double gpuTime = timings[0].gpuFrameTime; // 这里的单位直接就是 ms
                cpuText.text = $"CPU: {cpuTime:F2}ms";
                gpuText.text = $"GPU: {gpuTime:F2}ms";
            }


            int dc = 0;
            int verts = 0;
// #if UNITY_EDITOR
//             // 编辑器下最准的统计
//             dc = UnityEditor.UnityStats.drawCalls;
//             verts = UnityEditor.UnityStats.vertices;
// #else
//             // 真机下尝试获取
//             if (drRecorder.isValid) dc = (int)drRecorder.systemMemorySize;
//             if (vertRecorder.isValid) verts = (int)vertRecorder.systemMemorySize;
// #endif
            dispPlayText.text = $"DrawCalls: {dc}\n" + $"顶点: {FormatNumber(verts)}";
        }
    }


    void DisplayMemory()
    {
        // 1. 系统总物理内存 (MB)
        int totalSystemMemory = SystemInfo.systemMemorySize;

        // 2. Unity 当前已分配的内存 (转换为 MB)
        long allocatedMemory = Profiler.GetTotalAllocatedMemoryLong() / 1048576;

        // 3. Unity 预留的内存总额 (已分配 + 未分配但占用的，转换为 MB)
        long reservedMemory = Profiler.GetTotalReservedMemoryLong() / 1048576;

        // 4. Mono 堆内存 (托管对象占用)
        long monoMemory = Profiler.GetMonoUsedSizeLong() / 1048576;

        memoryText.text = $"系统总内存: {totalSystemMemory} MB\n" +
                          $"Unity 预留: {reservedMemory} MB\n" +
                          $"Unity 已用: {allocatedMemory} MB\n" +
                          $"堆内存 (Mono): {monoMemory} MB";
    }


    string FormatNumber(int num)
    {
        if (num >= 1000000) return (num / 1000000f).ToString("F2") + "M";
        if (num >= 1000) return (num / 1000f).ToString("F1") + "K";
        return num.ToString();
    }

}