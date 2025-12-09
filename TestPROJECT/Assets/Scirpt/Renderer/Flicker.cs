using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

[VolumeComponentMenu("Custom/Flicker")]
public class Flicker : VolumeComponent, IPostProcessComponent
{
    public ColorParameter color = new ColorParameter(Color.white);
    public ClampedFloatParameter range = new ClampedFloatParameter(0f, 0f, 1f);
    public ClampedFloatParameter center = new ClampedFloatParameter(0f, 0f, .5f);
    public ClampedFloatParameter speed = new ClampedFloatParameter(0f, 0f, 1f);

    // VolumeComponent switching controls whether "AddRenderPasses" should inject a Pass.
    // "Active" cannot be used as a criterion and is unrelated to whether it is checked (whether it is Override is also unrelated).

    public bool IsActive() => active && color.overrideState;
    public bool IsTileCompatible() => false;

}


