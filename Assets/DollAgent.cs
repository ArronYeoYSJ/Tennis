using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine;
using System.Collections.Generic;

public class DollAgent : Agent
{
    [Header("Setup")]
    public GameObject dollPrefab;
    public GameObject floor;
    public GameObject start;
    public Vector3 startCoordOffset;

    [Header("Reward & Bias")]
    public float fallenThreshold = 1f;
    public LayerMask ground;
    
    [Range(0.01f, 1f)]
    [Tooltip("Adjusts the balance between favouring rewards for posture (0f) and verticality (1f)")]
    public float verticalityBias = 0.5f;
    public float heightRewardMulti = 1f;

    [Header("Action Control")]

    public bool noActionTest = false;

    [Header("Debug")]
    public bool logRewards;
    public int logFrequency = 100;

    private GameObject dollInstance;
    private BodyController bodyController;
    private Dictionary<string, Quaternion> referencePose = new();
    private Dictionary<string, ConfigurableJoint> jointMap = new();
    private JointDriveTuner jointDriveTuner;
    private RewardEvaluator rewardEvaluator;



    private const int EpisodeWindowSize = 1000;
    private static int[] recentEpisodeLengths = new int[EpisodeWindowSize];
    private static int episodeIndex = 0;
    private static int totalRecordedEpisodes = 0;
    private static float rollingAverageLength = 0f;

    private bool poseCaptured = false;

    public int falls = 0;
    public int survivals = 0;

    private Vector3 normalizedDelta;
    private Vector3 currentEularJointRotation;

    private Vector3 footTargets;




    public override void OnEpisodeBegin()
    {

        ///creSate the doll/agent, destroy old one if already existing
        if (dollInstance != null) Destroy(dollInstance);
        dollInstance = Instantiate(dollPrefab, start.transform.position + startCoordOffset, Quaternion.identity, transform);

        ///get the created agents body controller script.

        jointDriveTuner = this.transform.GetComponent<JointDriveTuner>();
        bodyController = dollInstance.GetComponent<BodyController>();

        if (jointDriveTuner != null)
        {
            jointDriveTuner.ApplyDrives(dollInstance);
        }
        else { Debug.LogError("jointDriveTuner not found"); }

        ///if first run, read the transforms of all objects containing jooints into the dictionary for later freference
        /// may cause issues if trying to mod prefab- during training
        if (!poseCaptured)
        {
            this.referencePose.Clear();

            foreach (var jt in bodyController.targetJoints.Values)
            {
                //Debug.Log("capturing ref rotation");
                referencePose[jt.joint.name] = jt.joint.transform.localRotation;
                // Debug.Log(referencePose[jt.joint.name]);
            }

            poseCaptured = true;
        }
        /// creates a measure of the starting positions of the feet relative to each other for later scoring


        float idealShinLength = bodyController.rightShin.transform.position.y - bodyController.rightFoot.transform.position.y;
        float idealThighLength = bodyController.rightThigh.transform.position.y - bodyController.rightShin.transform.position.y;
        Vector3 rightFootStartPos = bodyController.rightFoot.transform.position;
        Vector3 leftFootStartPos = bodyController.leftFoot.transform.position;
        Vector3 startingFootDelta = bodyController.leftFoot.transform.InverseTransformPoint(rightFootStartPos);

        footTargets = new Vector3(
            startingFootDelta.x,
            startingFootDelta.y,
            startingFootDelta.z
            );

        //init the helper classes
        rewardEvaluator = new RewardEvaluator(bodyController, floor, ground, fallenThreshold, heightRewardMulti, idealShinLength, idealThighLength);

    }
    

    public override void CollectObservations(VectorSensor sensor)
    {
        ///<summary> captures quaternion rotation data (4 floats) and angular velocity ( 3 flaots) for each joint
        /// (14 total jointsd) for 98 total data points per step </summary>
        foreach (var jt in bodyController.targetJoints.Values)
        {
            var rot = jt.joint.transform.rotation;
            sensor.AddObservation(rot.x);
            sensor.AddObservation(rot.y);
            sensor.AddObservation(rot.z);
            sensor.AddObservation(rot.w);

            var rb = jt.joint.GetComponent<Rigidbody>();
            sensor.AddObservation(rb != null ? rb.angularVelocity : Vector3.zero);
        }

        sensor.AddObservation(bodyController.head.transform.position.y);
        Rigidbody headRB = bodyController.head.GetComponent<Rigidbody>();
        sensor.AddObservation(headRB.velocity);
        Rigidbody hipsRB = bodyController.hips.GetComponent<Rigidbody>();
        sensor.AddObservation(hipsRB.velocity);
        sensor.AddObservation(bodyController.hips.transform.position.y);
        Vector3 offset = bodyController.hips.transform.position - bodyController.chest.transform.position;
        sensor.AddObservation(offset);

        Vector3 centerOfMass = bodyController.ComputeCenterOfMass();
        sensor.AddObservation(centerOfMass);
    }


    public override void OnActionReceived(ActionBuffers actions)
    {
        ApplyActions(actions);

        //checks if any of the dolls key body parts are below a certain distance from the floor
        if (rewardEvaluator.HasFallen())
        {
            AddReward(-10f);
            trackEpisodeAvgLength();
            EndEpisode();
            falls++;
            return;
        }

        //Penalty if feet cross over
        Vector3 rightFootPos = bodyController.rightFoot.transform.position;
        Vector3 leftFootPos = bodyController.leftFoot.transform.position;

        Vector3 localFootDistance = bodyController.leftFoot.transform.InverseTransformPoint(rightFootPos);

        float footReward = 0f;
        //feet crossed / too close
        if (localFootDistance.x < 0.05f)
        {
            footReward -= 1f;
        }
        else if (localFootDistance.x <= footTargets.x * 1.2f)
        {
            footReward += 0.02f;
        }
        else
        {
            footReward -= localFootDistance.x - (1.2f * footTargets.x);
        }

        //penalty if feet too far front to back

        if (Mathf.Abs(localFootDistance.x) > footTargets.z + 0.2f)
        {
            footReward += -(Mathf.Abs(localFootDistance.z) - (footTargets.z + 0.2f));
        }
        else
        {
            footReward += 0.02f;
        }
       
        footReward = Mathf.Clamp(footReward, -1f, 0.2f);

        //hardcoded values created similar rewards during sim
        float poseScore = 0f;
        foreach (var jt in bodyController.targetJoints.Values)
        {
            Quaternion current = jt.joint.transform.localRotation;
            Quaternion reference = jt.initialLocalRotation;
            float angle = Quaternion.Angle(current, reference);

            float falloff = Mathf.Exp(-Mathf.Pow(angle / 45f, 2f));
            float reward = Mathf.Clamp01(1f - falloff);

            poseScore += reward / bodyController.targetJoints.Count;
        }


        float poseReward = poseScore * (1f - verticalityBias);
        float postureReward = rewardEvaluator.Evaluate() * verticalityBias * 0.5f;

        AddReward(footReward);
        AddReward(poseReward);
        AddReward(postureReward);

        float angularPenalty = 0f;
        foreach (var jt in bodyController.targetJoints.Values)
        {
            var rb = jt.joint.GetComponent<Rigidbody>();
            angularPenalty += rb.angularVelocity.magnitude;
        }

        if (angularPenalty < 20.0f)
        {
            AddReward(0.01f); // bonus for calm pose
        }
        else
        {
            AddReward(-0.001f * angularPenalty);
        }

        if (StepCount >= MaxStep - 1)
        {
            survivals++;
            trackEpisodeAvgLength();
            SetReward(GetCumulativeReward() * 1.1f);  // bonus for survival
        }


        if (logRewards && StepCount % logFrequency == 0)
        {
            Debug.Log($"[Reward] pose: {poseReward:F2} | posture: {postureReward:F2}");
            Debug.Log($"Survivals: {survivals}, Falls: {falls}");
            Debug.Log(angularPenalty);
        }
        
    }
    

    private void ApplyActions(ActionBuffers actions)
    {
        int i = 0;

        foreach (var jt in bodyController.targetJoints.Values)
        {
            // string jointName = jt.joint.name;
            // Vector3 axisMask = GetAxisMask(jointName);

            float x = 0f;
            float y = 0f;
            float z = 0f;

            if (noActionTest)
            {
                x = 0.5f;
                y = 0.5f;
                z = 0.5f;
            }
            else
            {
                x = (actions.ContinuousActions[i++] + 0.96f) * 0.5f + 0.01f;
                y = (actions.ContinuousActions[i++] + 0.96f) * 0.5f + 0.01f;
                z = (actions.ContinuousActions[i++] + 0.96f) * 0.5f + 0.01f;
            }

            Vector3 deltaEuler = new Vector3(

                Mathf.Lerp(jt.joint.lowAngularXLimit.limit, jt.joint.highAngularXLimit.limit, x),
                Mathf.Lerp(-jt.joint.angularYLimit.limit, jt.joint.angularYLimit.limit, y),
                Mathf.Lerp(-jt.joint.angularZLimit.limit, jt.joint.angularZLimit.limit, z)

            );
            // normalizedDelta = new Vector3(

            // Mathf.InverseLerp(jt.joint.lowAngularXLimit.limit, jt.joint.highAngularXLimit.limit, deltaEuler.x),
            // Mathf.InverseLerp(-jt.joint.angularYLimit.limit, jt.joint.angularYLimit.limit, deltaEuler.y),
            // Mathf.InverseLerp(-jt.joint.angularZLimit.limit, jt.joint.angularZLimit.limit, deltaEuler.z)

            // );

            jt.joint.targetRotation = Quaternion.Euler(deltaEuler.x, deltaEuler.y, deltaEuler.z);
            // jt.joint.targetRotation = jt.previousLocalRotationTarget * jt.joint.transform.localRotation * Quaternion.Inverse(jt.initialLocalRotation);
            // jt.previousLocalRotationTarget = jt.joint.targetRotation;
            //currentEularJointRotation = new Vector3(deltaEuler.x, deltaEuler.y, deltaEuler.z);

        }
    }


    // private void SetTargetRotation(ConfigurableJoint joint, Quaternion targetRotation, Quaternion startRotation)
    // {
    //     Quaternion deltaRotation = Quaternion.Inverse(targetRotation) * startRotation;

    //     // Calculate rotations for converting from joint space to local space and back.
    //     Vector3 jointX = joint.axis;
    //     Vector3 jointZ = Vector3.Cross(jointX, joint.secondaryAxis).normalized;
    //     Vector3 jointY = Vector3.Cross(jointZ, jointX).normalized;

    //     Quaternion localToJointSpace = Quaternion.LookRotation(jointZ, jointY);
    //     Quaternion jointToLocalSpace = Quaternion.Inverse(localToJointSpace);

    //     // Convert the local space rotation into a joint space rotation.
    //     joint.targetRotation = jointToLocalSpace * deltaRotation * localToJointSpace;

    // }

//     private void SetTargetRotation(ConfigurableJoint joint, Quaternion actionGeneratedRotationDelta, Quaternion referenceTransformLocalRotation)
    // {
    //     // actionGeneratedRotationDelta is Quaternion.Euler(deltaEuler) from ApplyActions
    //     // referenceTransformLocalRotation is referencePose[joint.name]

    //     // Calculate the new desired local rotation for the joint's transform by applying the delta:
    //     // Corrected logic: apply actionGeneratedRotationDelta to referenceTransformLocalRotation
    //     Quaternion newDesiredTransformLocalRotation = referenceTransformLocalRotation * actionGeneratedRotationDelta;

    //     // Calculate rotations for converting from joint space to local space and back.
    //     // This part of your logic for joint space conversion seems structurally correct
    //     // assuming joint.axis and joint.secondaryAxis are set up as intended.
    //     Vector3 jointX = joint.axis;
    //     Vector3 jointZ = Vector3.Cross(jointX, joint.secondaryAxis).normalized;
    //     Vector3 jointY = Vector3.Cross(jointZ, jointX).normalized;

    //     Quaternion localToJointSpace = Quaternion.LookRotation(jointZ, jointY);
    //     Quaternion jointToLocalSpace = Quaternion.Inverse(localToJointSpace);

    //     // Convert the new desired local transform rotation into a joint space rotation for the ConfigurableJoint
    //     joint.targetRotation = jointToLocalSpace * newDesiredTransformLocalRotation * localToJointSpace;
    // }


    /// <summary>
    /// dictionary containing a map of joint name, to save lookups during simulation.
    /// sped up sim time by 3x
    /// </summary>

    private Dictionary<string, ConfigurableJoint> GetJointMap()
    {
        foreach (var jt in bodyController.targetJoints.Values)
            jointMap[jt.joint.name] = jt.joint;
        return jointMap;
    }

    ///<summary>
    /// Determines the axis mask for each joint based on its name. Returns a mask for limiting rotation in (usually)
    /// impossible axiis for human movement
    /// </summary>
    private Vector3 GetAxisMask(string jointName)
    {
        if (jointName.Contains("head")) return new Vector3(0.5f,0.5f,0.2f);
        if (jointName.Contains("upper")) return new Vector3(0.5f,0.5f,1f);        
        if (jointName.Contains("forearm")) return new Vector3(1, 0.3f, 0.8f);
        if (jointName.Contains("chest")) return new Vector3(0.5f, 0.5f, 0.5f);
        if (jointName.Contains("shin")) return new Vector3(1, 0.1f, 0.1f);
        if (jointName.Contains("thigh")) return new Vector3(1, 0.6f, 0.8f);
        if (jointName.Contains("foot")) return new Vector3(1, 0.6f, 0.35f);
        return Vector3.one;
    }

    // private Vector3 NormalizeEuler(Vector3 euler)
    // {
    //     return new Vector3(
    //         NormalizeAngle(euler.x),
    //         NormalizeAngle(euler.y),
    //         NormalizeAngle(euler.z)
    //     );
    // }

    // private float NormalizeAngle(float angle)
    // {
    //     angle %= 360f;
    //     if (angle > 180f) angle -= 360f;
    //     if (angle < -180f) angle += 360f;
    //     return angle;
    // }

    private void trackEpisodeAvgLength(){
        if (StepCount > 0)
        {
            // Store episode length in circular buffer
            recentEpisodeLengths[episodeIndex % EpisodeWindowSize] = StepCount;
            episodeIndex++;
            totalRecordedEpisodes = Mathf.Min(episodeIndex, EpisodeWindowSize);

            // Recompute rolling average
            int sum = 0;
            for (int i = 0; i < totalRecordedEpisodes; i++)
                sum += recentEpisodeLengths[i];

            rollingAverageLength = (float)sum / totalRecordedEpisodes;

            if (episodeIndex % 100 == 0)
                Debug.Log($"[Stats] Rolling Avg Episode Length (last {totalRecordedEpisodes}): {rollingAverageLength:F1}");
        }

    }
}
