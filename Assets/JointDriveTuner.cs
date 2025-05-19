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
    public void ApplyDrives(GameObject doll)
    {
        foreach (var cj in doll.GetComponentsInChildren<ConfigurableJoint>())
        {
            string name = cj.gameObject.name.ToLower();
            JointDrive drive;

            if (name.Contains("thigh") || name.Contains("shin"))
            {
                drive = GenerateDrive(springValues.legSpring);
            }
            else if (name.Contains("foot"))
            {
                drive = GenerateDrive(springValues.footSpring);
            }
            else if (name.Contains("chest") || name.Contains("hips"))
            {
                drive = GenerateDrive(springValues.torsoSpring);
            }
            else if (name.Contains("upperarm") || name.Contains("forearm") || name.Contains("hand"))
            {
                drive = GenerateDrive(springValues.armSpring);
            }
            else if (name.Contains("head") || name.Contains("neck"))
            {
                drive = GenerateDrive(springValues.headSpring);
            }
            else
            {
                continue;
            }

            cj.rotationDriveMode = RotationDriveMode.Slerp;
            cj.slerpDrive = drive;
            cj.angularXDrive = drive;
            cj.angularYZDrive = drive;
        }
    }

    private JointDrive GenerateDrive(float spring)
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
