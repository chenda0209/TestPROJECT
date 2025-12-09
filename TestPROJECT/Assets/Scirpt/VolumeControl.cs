using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class VolumeControl : MonoBehaviour
{
    [SerializeReference] Volume volume;
    private Flicker flicker;
    void Start()
    {
        // 尝试从 Profile 中获取目标 Volume Component
        if (volume.profile.TryGet(out Flicker flicker))
        {
            this.flicker = flicker;
            Debug.Log("Flicker component successfully referenced.");
        }
        else
        {
            Debug.LogError("Flicker component not found in the assigned profile.");
        }
    }

    public void FlickerActive(bool val)
    {
        flicker.color.overrideState = val;
    }


    void Update()
    {
        //测试
        if (Input.GetKeyDown(KeyCode.V))
        {
            if (flicker.color.overrideState) flicker.color.overrideState = false;
            else flicker.color.overrideState = true;
        }
    }
}
