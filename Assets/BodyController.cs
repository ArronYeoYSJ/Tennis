using UnityEngine;
using System.Collections.Generic;
using System;

public class BodyController : MonoBehaviour
{
    public Dictionary<string, JointTarget> targetJoints { get; private set; } = new();

    public GameObject hips, leftFoot, rightFoot, leftShin, rightShin, leftThigh, rightThigh, chest, head;

    

    void Awake()
    {
        var joints = GetComponentsInChildren<ConfigurableJoint>();

        foreach (var joint in joints)
        {
            Collider col;
            Boolean foot = false;
            if (joint.name.Contains("foot"))
            {
                col = GetComponentInChildren<Collider>();
                foot = true;
            }
            else
            {
                col = joint.GetComponent<Collider>();
            }
            
            var jt = new JointTarget
            {
                joint = joint,
                thetaDampening = 1f,
                initialLocalRotation = joint.transform.localRotation,
                collider = col,
                allowGroundContact = foot
            };

            targetJoints[joint.name] = jt;

            // autofill key parts list
            switch (joint.name)
            {
                case "B-hips": hips = joint.gameObject; break;
                case "B-chest": chest = joint.gameObject; break;
                case "B-head": head = joint.gameObject; break;
                case "B-foot.L": leftFoot = joint.gameObject; break;
                case "B-foot.R": rightFoot = joint.gameObject; break;
                case "B-shin.L": leftShin = joint.gameObject; break;
                case "B-shin.R": rightShin = joint.gameObject; break;
                case "B-thigh.L": leftThigh = joint.gameObject; break;
                case "B-thigh.R": rightThigh = joint.gameObject; break;
            }
        }
    }
    ///old testing code, too jittery

    // void FixedUpdate()
    // {
    //     foreach (var jt in targetJoints.Values)
    //     {
    //         jt.joint.targetRotation = jt.previousLocalRotationTarget * jt.joint.transform.localRotation * Quaternion.Inverse(jt.initialLocalRotation);
    //         jt.previousLocalRotationTarget = jt.joint.targetRotation;
    //     }
    // }


    /// <summary>
    /// Computes the weighted center of mass of the entire doll, based on Rigidbody mass.
    /// </summary>
    public Vector3 ComputeCenterOfMass()
    {
        Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>();
        if (rbs.Length == 0) return transform.position;

        Vector3 comSum = Vector3.zero;
        float totalMass = 0f;

        foreach (var rb in rbs)
        {
            comSum += rb.worldCenterOfMass * rb.mass;
            totalMass += rb.mass;
        }

        return totalMass > 0f ? comSum / totalMass : transform.position;
    }




    [System.Serializable]
    public class JointTarget
    {
        public ConfigurableJoint joint;
        public float thetaDampening;

        public Quaternion initialLocalRotation;
        [HideInInspector]
        public Quaternion previousLocalRotationTarget;

        public Collider collider;
        public Boolean allowGroundContact;
    }
}
