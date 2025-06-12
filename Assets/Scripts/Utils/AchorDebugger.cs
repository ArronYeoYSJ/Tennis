using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class AnchorDebugger : MonoBehaviour
{
    public BodyController bc;
    public List<ArticulationBody> joints;

    void Awake()
    {
        foreach (var jt in bc.targetJoints.Values)
        {
            joints.Add(jt.joint);
        }
    }
    
    // void OnDrawGizmos()
    // {
        
    //     Gizmos.color = Color.red;
    //     foreach (var j in joints)
    //     {
    //         if (j.connectedBody == null) continue;
    //         // child pivot in world space
    //         Vector3 childPivot = j.transform.position;
    //         // connectedAnchor in world space
    //         Vector3 worldCA = j.connectedBody.transform.TransformPoint(j.connectedAnchor);
    //         // draw spheres and a line
    //         Gizmos.DrawWireSphere(childPivot, 0.02f);
    //         Gizmos.DrawSphere(worldCA, 0.015f);
    //         Gizmos.DrawLine(childPivot, worldCA);
    //     }
    // }
}