using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIDirectionIndication : MonoBehaviour
{
    [SerializeReference] private Transform cam;
    [SerializeReference] private RawImage dir;


    void OnEnable()
    {
        cam = Camera.main.transform;
    }
    void LateUpdate()
    {
        dir.uvRect = new Rect(cam.eulerAngles.y / 360, 0, 1, 1);
    }
}
