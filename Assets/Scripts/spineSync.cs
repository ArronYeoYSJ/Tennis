using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class spineSync : MonoBehaviour
{
    private GameObject parent;
    // Start is called before the first frame update
    void Start()
    {
        parent = this.transform.parent.gameObject;
        this.transform.position = parent.transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        transform.SetPositionAndRotation(parent.transform.position, quaternion.identity);
    }
}
