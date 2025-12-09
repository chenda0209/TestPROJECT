using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor.Rendering.Universal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class FlickerRendererFeature : ScriptableRendererFeature
{
    private FlickerRenderPass flickerRenderPass;
    public RenderPassEvent renderPassEvent;
    public Material material;
    private Flicker flicker;
    public override void Create()
    {
        flickerRenderPass = new(material)
        {
            renderPassEvent = renderPassEvent
        };
    }
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // cameraType == CameraType.Game CameraType.SceneView：用于判断是否是游戏画面（排除辅助功能）。
        // renderType == CameraRenderType.Base：CameraRenderType.Overlay用于判断是否是 主渲染层（排除 UI、辅助覆盖层）。

        flicker = VolumeManager.instance.stack.GetComponent<Flicker>();
        
        // renderingData.postProcessingEnabled IS SETTING AND CAMERA'S POSTPROCESSING SET
        if (flicker.IsActive() && renderingData.postProcessingEnabled) 
        {
            flickerRenderPass.Setup(flicker);
            renderer.EnqueuePass(flickerRenderPass);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}

public class FlickerRenderPass : ScriptableRenderPass
{
    private Material material;
    private Flicker flicker;
    // private RTHandle flickerRTHandle;
    public FlickerRenderPass(Material mat)
    {
        material = mat;
    }
    public void Setup(Flicker volume)
    {
        flicker = volume;
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        // RenderingUtils.ReAllocateIfNeeded(flickerRTHandle, des, FilterMode.Bilinear, TextureWrapMode.Clamp);
        // ConfigureTarget
    }
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        // RenderTextureDescriptor des = renderingData.cameraData.cameraTargetDescriptor;
        // RenderingUtils.ReAllocateIfNeeded(ref flickerRTHandle, des);
    }
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (material == null) return;

        CommandBuffer cmd = CommandBufferPool.Get("Custom PostProcessing Flicker");

        material.SetColor("_Color", flicker.color.value);
        material.SetFloat("_Range", flicker.range.value);
        material.SetFloat("_Center", flicker.center.value);
        material.SetFloat("_Speed", flicker.speed.value);
        // CoreUtils.SetRenderTarget(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle);
        cmd.Blit(null, renderingData.cameraData.renderer.cameraColorTargetHandle, material);

        // 4. 执行 CommandBuffer 
        context.ExecuteCommandBuffer(cmd);
        // 5. 释放 CommandBuffer 
        CommandBufferPool.Release(cmd);
    }


}
