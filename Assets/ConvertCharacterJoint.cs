using UnityEngine;

public class ConvertCharacterJoint : MonoBehaviour
{
    public void Convert()
{
    CharacterJoint characterJoint = GetComponent<CharacterJoint>();
    if (characterJoint == null) return;

    ConfigurableJoint cj = gameObject.AddComponent<ConfigurableJoint>();

    cj.connectedBody = characterJoint.connectedBody;
    cj.anchor = characterJoint.anchor;
    cj.connectedAnchor = characterJoint.connectedAnchor;
    cj.axis = characterJoint.axis;
    cj.secondaryAxis = characterJoint.swingAxis;

    cj.xMotion = ConfigurableJointMotion.Locked;
    cj.yMotion = ConfigurableJointMotion.Locked;
    cj.zMotion = ConfigurableJointMotion.Locked;

    cj.angularXMotion = ConfigurableJointMotion.Limited;
    cj.angularYMotion = ConfigurableJointMotion.Limited;
    cj.angularZMotion = ConfigurableJointMotion.Limited;

    cj.lowAngularXLimit = new SoftJointLimit { limit = characterJoint.lowTwistLimit.limit };
    cj.highAngularXLimit = new SoftJointLimit { limit = characterJoint.highTwistLimit.limit };
    cj.angularYLimit = new SoftJointLimit { limit = characterJoint.swing1Limit.limit };
    cj.angularZLimit = new SoftJointLimit { limit = characterJoint.swing2Limit.limit };

    // Add drives for control
    JointDrive drive = new JointDrive
    {
        positionSpring = 1000f,
        positionDamper = 100f,
        maximumForce = 10000f
    };
    cj.angularXDrive = drive;
    cj.angularYZDrive = drive;
    cj.slerpDrive = drive;
    cj.rotationDriveMode = RotationDriveMode.Slerp;

    DestroyImmediate(characterJoint);
}

}