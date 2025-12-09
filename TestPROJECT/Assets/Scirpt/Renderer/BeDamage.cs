using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BeDamage : MonoBehaviour
{
    public SkinnedMeshRenderer[] meshes;
    [ColorUsage(false, true)] public Color behitColor;
    MaterialPropertyBlock materialPropertyBlock;
    void OnEnable()
    {
        materialPropertyBlock = new();
        materialPropertyBlock.SetColor("_EmissionColor", behitColor);
        foreach (var mesh in meshes)
        {
            mesh.SetPropertyBlock(null);
            // mesh.material.SetColor("_EmissionColor", new Color(10,10,10));
        }
    }
    void Behit()
    {
        StopCoroutine(nameof(Bink));
        StartCoroutine(nameof(Bink));
    }
    IEnumerator Bink()
    {
        foreach (var mesh in meshes)
        {
            mesh.SetPropertyBlock(materialPropertyBlock);
            // mesh.material.SetColor("_Emission", behitColor);
        }
        yield return new WaitForSecondsRealtime(0.2f);
        foreach (var mesh in meshes)
        {
            mesh.SetPropertyBlock(null);
            // mesh.material.SetColor("_Emission", Color.clear);
        }
    }

    void Update()
    {
        if(Input.GetKeyDown(KeyCode.C)) Behit();
    }
}
