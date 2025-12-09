using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Laser : MonoBehaviour
{
    [SerializeReference] LineRenderer line;
    [SerializeField] float test;
    Vector3 lineRange;HpBar asd;
    void Start()
    {
        lineRange = line.GetPosition(1);
    }
    void Update()
    {
        line.transform.eulerAngles += test * Time.deltaTime * new Vector3(0, 1, 0);
        lineRange += 0 * Time.deltaTime * new Vector3(0, 0, 1);
        line.SetPosition(1, lineRange);
    }
}
