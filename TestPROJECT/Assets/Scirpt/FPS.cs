using UnityEngine;
using TMPro;
using UnityEngine.Profiling;
using Unity.Profiling; // 用于 ProfilerRecorder
using System.Collections.Generic;

public class FPSDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text fpsText, memoryText, cpuText, gpuText, dispPlayText;
    [SerializeField] private float refreshRate = 0.5f;
    private float timer;

    // --- 核心修复：ProfilerRecorder 需要配合 ProfilerCategory 使用 ---
    private ProfilerRecorder drRecorder;
    private ProfilerRecorder vertRecorder;

    void Awake()
    {
        // 1. 关闭 V-Sync
        QualitySettings.vSyncCount = 0;
        // 2. 设置目标帧率
        Application.targetFrameRate = 120; 
        Debug.Log("帧率设置尝试：V-Sync = 0, TargetFPS = 120");
    }

    // --- 核心修复：ProfilerRecorder 的启动和关闭 ---
    void OnEnable()
    {
        // 在真机上，通过这种方式手动开启渲染计数器的监听
        drRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        vertRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
    }

    void OnDisable()
    {
        // 必须销毁，否则打包后会造成内存泄漏
        drRecorder.Dispose();
        vertRecorder.Dispose();
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= refreshRate)
        {
            float fps = 1f / Time.unscaledDeltaTime;
            fpsText.text = $"FPS: {Mathf.RoundToInt(fps)}";
            timer = 0f;

            DisplayMemory();

            // CPU & GPU 时间统计
            FrameTimingManager.CaptureFrameTimings();
            FrameTiming[] timings = new FrameTiming[1];
            uint count = FrameTimingManager.GetLatestTimings(1, timings);

            if (count > 0)
            {
                cpuText.text = $"CPU: {timings[0].cpuFrameTime:F2}ms";
                gpuText.text = $"GPU: {timings[0].gpuFrameTime:F2}ms";
            }

            int dc = 0;
            long verts = 0; // 顶点数建议用 long，防止溢出

#if UNITY_EDITOR
            // 编辑器下直接拿 UnityStats
            dc = UnityEditor.UnityStats.drawCalls;
            verts = UnityEditor.UnityStats.vertices;
#else
            // 真机下：使用 .LastValue 拿到刚才记录的值
            if (drRecorder.IsRunning) dc = (int)drRecorder.LastValue;
            if (vertRecorder.IsRunning) verts = vertRecorder.LastValue;
#endif
            dispPlayText.text = $"DrawCalls: {dc}\n" + $"Vertex: {FormatNumber((int)verts)}";
        }
    }

    void DisplayMemory()
    {
        int totalSystemMemory = SystemInfo.systemMemorySize;
        long allocatedMemory = Profiler.GetTotalAllocatedMemoryLong() / 1048576;
        long reservedMemory = Profiler.GetTotalReservedMemoryLong() / 1048576;
        long monoMemory = Profiler.GetMonoUsedSizeLong() / 1048576;

        memoryText.text = $"SystemMemory: {totalSystemMemory} MB\n" +
                          $"UnityReserved: {reservedMemory} MB\n" +
                          $"UnityAllocated: {allocatedMemory} MB\n" +
                          $"Mono: {monoMemory} MB";
    }

    string FormatNumber(int num)
    {
        if (num >= 1000000) return (num / 1000000f).ToString("F2") + "M";
        if (num >= 1000) return (num / 1000f).ToString("F1") + "K";
        return num.ToString();
    }
}