using UnityEditor;
using UnityEngine;

public class JointDriveTuner : MonoBehaviour
{
    [Header("Spring Settings")]
    [SerializeField]
    SpringValues springValues;

    [Header("Damping %")]
    [Range(0, 100)]
    public float dampingFactor = 10;

    [Header("Max Force Scalar")]
    
    [Range(1.0f,5f)]
    public float maxForceScalar = 4.0f;

    [ContextMenu("Apply Drive Settings")]
    public void ApplyDrives(BodyController body)
    {
        foreach (var jt in body.targetJoints.Values)
        {
            string name = jt.joint.gameObject.name.ToLower();

            JointDrive drive;

            if (name.Contains("thigh") || name.Contains("shin"))
            {
                drive = GenerateDrive(springValues.legSpring);
                jt.baseStrength = springValues.legSpring;
            }
            else if (name.Contains("foot"))
            {
                drive = GenerateDrive(springValues.footSpring);
                jt.baseStrength = springValues.footSpring;
            }
            else if (name.Contains("chest"))
            {
                drive = GenerateDrive(springValues.torsoSpring);
                jt.baseStrength = springValues.torsoSpring;
            }
            else if (name.Contains("upperarm") || name.Contains("forearm"))
            {
                drive = GenerateDrive(springValues.armSpring);
                jt.baseStrength = springValues.armSpring;
            }
            else if (name.Contains("hand"))
            {
                drive = GenerateDrive(springValues.armSpring * 0.3f);
                jt.baseStrength = springValues.armSpring;
            }
            else if (name.Contains("head"))
            {
                drive = GenerateDrive(springValues.headSpring);
                jt.baseStrength = springValues.headSpring;
            }
            else
            {
                continue;
            }


            SetNewDrives(jt, drive);
            
        }
    }

    public JointDrive GenerateDrive(float spring)
    {
        float maxForce = spring * maxForceScalar;

        return new JointDrive
        {
            positionSpring = spring,
            maximumForce = maxForce
        };
    }

    public void SetNewDrives(BodyController.JointTarget jt, JointDrive drive)
    {
        var x_drive = jt.joint.xDrive;
        var y_drive = jt.joint.yDrive;
        var z_drive = jt.joint.zDrive;

        x_drive.stiffness = drive.positionSpring;
        y_drive.stiffness = drive.positionSpring;
        z_drive.stiffness = drive.positionSpring;

        z_drive.damping = jt.baseStrength * dampingFactor / 100;
        y_drive.damping = jt.baseStrength * dampingFactor / 100;
        x_drive.damping = jt.baseStrength * dampingFactor / 100;

        x_drive.forceLimit = drive.maximumForce;
        y_drive.forceLimit = drive.maximumForce;
        z_drive.forceLimit = drive.maximumForce;

        jt.joint.xDrive = x_drive;
        jt.joint.yDrive = y_drive;
        jt.joint.zDrive = z_drive;
    }

    [System.Serializable]
    class SpringValues{
    public float legSpring = 20000f;
    public float footSpring = 12000f;
    public float torsoSpring = 8500f;
    public float armSpring = 5000f;
    public float headSpring = 3200f;
    }
}
