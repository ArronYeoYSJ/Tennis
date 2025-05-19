using UnityEditor;
using UnityEngine;

public class JointDriveTuner : MonoBehaviour
{
    [Header("Spring Settings")]
    [SerializeField]
    SpringValues springValues;

    [Header("Critical Damping Factor (0.8-5)")]
    [Range(0.8f, 5f)]
    public float criticalFactor = 1.0f;

    [Header("Max Force Scalar (e.g. 3-5)")]
    public float maxForceScalar = 4.0f;

    [ContextMenu("Apply Drive Settings")]
    public void ApplyDrives(BodyController body)
    {
        foreach (var jt in  body.targetJoints.Values)
        {
            string name = jt.joint.gameObject.name.ToLower();

            JointDrive drive;

            if (name.Contains("thigh") || name.Contains("shin"))
            {
                drive = GenerateDrive(springValues.legSpring);
                jt.baseStrength = drive.positionSpring;
            }
            else if (name.Contains("foot"))
            {
                drive = GenerateDrive(springValues.footSpring);
                jt.baseStrength = drive.positionSpring;
            }
            else if (name.Contains("chest") || name.Contains("hips"))
            {
                drive = GenerateDrive(springValues.torsoSpring);
                jt.baseStrength = drive.positionSpring;
            }
            else if (name.Contains("upperarm") || name.Contains("forearm") || name.Contains("hand"))
            {
                drive = GenerateDrive(springValues.armSpring);
                jt.baseStrength = drive.positionSpring;
            }
            else if (name.Contains("head") || name.Contains("neck"))
            {
                drive = GenerateDrive(springValues.headSpring);
                jt.baseStrength = drive.positionSpring;
            }
            else
            {
                continue;
            }

            jt.joint.slerpDrive = drive;
            jt.joint.angularXDrive = drive;
            jt.joint.angularYZDrive = drive;
            jt.joint.rotationDriveMode = RotationDriveMode.Slerp;
        }
    }

    public JointDrive GenerateDrive(float spring)
    {
        float damper = criticalFactor * 2 * Mathf.Sqrt(spring);
        float maxForce = spring * maxForceScalar;

        return new JointDrive
        {
            positionSpring = spring,
            positionDamper = damper,
            maximumForce = maxForce
        };
    }

    [System.Serializable]
    class SpringValues{
    public float legSpring = 20000;
    public float footSpring = 12000f;
    public float torsoSpring = 8500f;
    public float armSpring = 5000f;
    public float headSpring = 3200f;
    }
}
