using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine;
using System.Collections.Generic;
using System;


public class DollAgent : Agent
{
    [Header("Setup")]
    public GameObject dollPrefab;
    public GameObject floor;
    public GameObject start;
    public GameObject goal;
    private float[][] goalBounds;
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
    private Dictionary<string, ArticulationBody> jointMap = new();
    private JointDriveTuner jointDriveTuner;
    private RewardEvaluator rewardEvaluator;


    //refers to the total number of previous episodes to avg to get avg length
    private const int episodeWindowSize = 100;
    private static int[] recentEpisodeLengths = new int[episodeWindowSize];
    private static int episodeIndex = 0;
    private static int totalRecordedEpisodes = 0;
    private static float rollingAverageLength = 0f;

    private bool poseCaptured = false;

    public int falls = 0;
    public int survivals = 0;

    private Vector3 normalizedDelta;
    private Vector3 currentEularJointRotation;

    private Vector3 footTargets;


    public float moveRewardScale = 0.01f;
    public float reachReward = 2f;
    public float reachThreshold = 1f;
    private Vector3 prevAgentPos;
    private Vector3 newAgentPos;
    private Vector3 prevGoalPos;



    private enum Foot { None, Left, Right }
    private Foot   lastFootContact = Foot.None;
    private bool   lastLeftTouch   = false;
    private bool   lastRightTouch  = false;

    public float alternatingStepReward = 0.05f;
    public float idealStepLength = 0.3f;
    private Vector3 lastLeftLandingPos;
    private Vector3 lastRightLandingPos;
    public float minStepLength = 0.2f;
    public float sameFootPenalty = -0.02f;
    private float stepTimerL = 0f;
    private float stepTimerR = 0f;


    public void Start()
    {
        floor = gameObject.transform.parent.Find("Floor").transform.gameObject;
        start = gameObject.transform.parent.Find("Start").transform.gameObject;
        goal = gameObject.transform.parent.Find("Goal").transform.gameObject;
        Collider floorCol = floor.transform.GetComponent<BoxCollider>();
        goalBounds = new float[][] {
            new float[] { floorCol.bounds.min.x - floorCol.bounds.center.x, floorCol.bounds.max.x - floorCol.bounds.center.x},
            new float[] { floorCol.bounds.min.z - floorCol.bounds.center.z, floorCol.bounds.max.z - floorCol.bounds.center.z}
        };
    }

    void FixedUpdate()
    {
        stepTimerR += Time.deltaTime;
        stepTimerL += Time.deltaTime;
    }



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
            jointDriveTuner.ApplyDrives(bodyController);
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
            }

            poseCaptured = true;
        }
        /// creates a measure of the starting positions of the feet relative to each other for later scoring


        float idealShinLength = bodyController.rightShin.transform.position.y - bodyController.rightFoot.transform.position.y;
        float idealThighLength = bodyController.rightThigh.transform.position.y - bodyController.rightShin.transform.position.y;
        float dollHeight = bodyController.head.transform.position.y - bodyController.leftFoot.transform.position.y;
        Vector3 rightFootStartPos = bodyController.rightFoot.transform.position;
        Vector3 leftFootStartPos = bodyController.leftFoot.transform.position;
        // Calculate the right foot's position relative to the left foot in the left foot's local coordinate space
        Vector3 startingFootDelta = bodyController.leftFoot.transform.InverseTransformPoint(rightFootStartPos);

        footTargets = new Vector3(
            startingFootDelta.x,
            startingFootDelta.y,
            startingFootDelta.z
            );

        //init the helper classes
        rewardEvaluator = new RewardEvaluator(bodyController, floor, ground, fallenThreshold, heightRewardMulti, idealShinLength, idealThighLength, dollHeight);


        prevAgentPos = dollInstance.transform.position;
        newAgentPos = prevAgentPos;
        MoveGoalToNewLocation(newAgentPos);
        prevGoalPos = goal.transform.position;

        lastFootContact = Foot.None;
        lastLeftTouch = false;
        lastRightTouch = false;


    }

    private bool IsFootTouching(GameObject foot)
    {
        var collider = foot.GetComponentInChildren<BoxCollider>();
        Vector3 contactPoint = collider.bounds.center;
        contactPoint.y = collider.bounds.min.y; // bottom of the foot
        return Physics.CheckSphere(contactPoint, 0.02f, ground);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        //21 obs
        CollectOncePerStepObservations(sensor);

        //14 X 16 = 224
        foreach (var jt in bodyController.targetJoints.Values)
        {   //16 float obs
            CollectBodyPartObservations(sensor, jt);
            CollectJointObservations(sensor, jt);
        }

        // TOTAL OBS 224 + 21 = 245
    }
    
    // 12 observation floats
    private void CollectBodyPartObservations(VectorSensor sensor, BodyController.JointTarget part)
    {
        if (part != null)
        {
            sensor.AddObservation(part.rb.jointPosition[0]);
            sensor.AddObservation(part.rb.jointPosition[1]);
            sensor.AddObservation(part.rb.jointPosition[2]);
            sensor.AddObservation(part.rb.velocity);
            sensor.AddObservation(bodyController.spine.transform.localPosition - part.rb.transform.localPosition);
            sensor.AddObservation(part.rb.jointVelocity[0]);
            sensor.AddObservation(part.rb.jointVelocity[1]);
            sensor.AddObservation(part.rb.jointVelocity[2]);
        }
    }

    //4 observation floats
    private void CollectJointObservations(VectorSensor sensor, BodyController.JointTarget jt)
    {

        if (jt != null)
        {
            sensor.AddObservation(jt.joint.xDrive.stiffness / jt.baseStrength); // Normalized spring value
            sensor.AddObservation(jt.rotation01); // target rotations as percentage of range
            sensor.AddObservation((jt.rotation01 - jt.oldRotation01).magnitude);
        }
    }

    //21 obs floats
    private void CollectOncePerStepObservations(VectorSensor sensor)
    {
        ArticulationBody hipsRB = bodyController.hips.GetComponent<ArticulationBody>();
        sensor.AddObservation(hipsRB.velocity);
        sensor.AddObservation(bodyController.hips.transform.position.y);
        sensor.AddObservation(bodyController.hips.transform.up);
        sensor.AddObservation(bodyController.chest.transform.up); //10

        sensor.AddObservation(IsFootTouching(bodyController.leftFoot) ? 1f : 0f);
        sensor.AddObservation(IsFootTouching(bodyController.rightFoot) ? 1f : 0f);
        Vector3 rightFootPos = bodyController.rightFoot.transform.position;
        sensor.AddObservation(bodyController.leftFoot.transform.InverseTransformPoint(rightFootPos)); //15

        Vector3 centerOfMass = bodyController.ComputeCenterOfMass();
        sensor.AddObservation(centerOfMass);

        sensor.AddObservation(bodyController.spine.transform.position); //21

        Vector3 toGoal = goal.transform.position - bodyController.chest.transform.position;
        sensor.AddObservation(toGoal);

        float forwardDot = Vector3.Dot(bodyController.chest.transform.forward, toGoal.normalized);
        sensor.AddObservation(forwardDot);

        float rightDot = Vector3.Dot(bodyController.chest.transform.right, toGoal.normalized);
        sensor.AddObservation(rightDot);

        float forwardVel = Vector3.Dot(bodyController.hips.GetComponent<ArticulationBody>().velocity, toGoal.normalized);
        sensor.AddObservation(forwardVel);

        float leftDisp = Vector3.Distance(bodyController.leftFoot.transform.position, lastLeftLandingPos);
        float rightDisp = Vector3.Distance(bodyController.rightFoot.transform.position, lastRightLandingPos);
        sensor.AddObservation(leftDisp);
        sensor.AddObservation(rightDisp);
        sensor.AddObservation(stepTimerL);
        sensor.AddObservation(stepTimerR);
        

    }


    public override void OnActionReceived(ActionBuffers actions)
    {
        ApplyActions(actions);

        //checks if any of the dolls key body parts are below a certain distance from the floor
        if (rewardEvaluator.HasFallen())
        {
            AddReward(-5f);
            trackEpisodeAvgLength();
            EndEpisode();
            falls++;
            return;
        }

        RewardAndLog();
        GoalMovementReward();

    }


    private void GoalMovementReward()
    {

        bool leftTouch = IsFootTouching(bodyController.leftFoot);
        bool rightTouch = IsFootTouching(bodyController.rightFoot);

        bool newLeft = leftTouch && !lastLeftTouch;
        bool newRight = rightTouch && !lastRightTouch;

        float progressTowardGoal = CalculateProgress();

        if (newLeft)
        {
            stepTimerL = 0f;
            Vector3 thisLanding = bodyController.leftFoot.transform.position;
            float stepLen = Vector3.Distance(thisLanding, lastLeftLandingPos);

            if (lastFootContact == Foot.Right && stepLen > 0.05f)
            {
                AddReward(alternatingStepReward * Mathf.Clamp((stepLen / idealStepLength) - minStepLength, -minStepLength, 1));
                lastFootContact = Foot.Left;
                lastLeftLandingPos = thisLanding;
            }
            else if (lastFootContact == Foot.Left && stepLen > 0.05f)
            {
                AddReward(sameFootPenalty);
                lastFootContact = Foot.Left;
                lastLeftLandingPos = thisLanding;
            }


        }
        else if (newRight)
        {
            stepTimerR = 0f;
            Vector3 thisLanding = bodyController.rightFoot.transform.position;
            float stepLen = Vector3.Distance(thisLanding, lastRightLandingPos);

            if (lastFootContact == Foot.Left && stepLen > 0.05f)
            {
                AddReward(alternatingStepReward * Mathf.Clamp((stepLen / idealStepLength) - minStepLength, -minStepLength, 1));
                lastFootContact = Foot.Right;
                lastRightLandingPos = thisLanding;
            }
            else if (lastFootContact == Foot.Right && stepLen > 0.05f)
            {
                AddReward(sameFootPenalty);
                lastFootContact = Foot.Right;
                lastRightLandingPos = thisLanding;
            }
        }
        lastLeftTouch = leftTouch;
        lastRightTouch = rightTouch;
        
    }

    private float CalculateProgress()
    {
        float oldDist = Vector3.Distance(prevAgentPos, prevGoalPos);
        float newDist = Vector3.Distance(dollInstance.transform.position, goal.transform.position);
        float delta = oldDist - newDist;
        newAgentPos = dollInstance.transform.position;

        if (newDist < reachThreshold)
        {
            AddReward(reachReward);
            MoveGoalToNewLocation(newAgentPos);
            prevAgentPos = dollInstance.transform.position;
        }
        return delta;
    }

    private void MoveGoalToNewLocation(Vector3 agentPos)
    {
        // Generate a random position within the goal bounds
        Vector2 rand;

        Vector3 newGoalPos;
        // Ensure the new goal position is not too close to the agent
        do
        {
            rand = new Vector2(
                UnityEngine.Random.Range(goalBounds[0][0] + 2, goalBounds[0][1] - 2),
                UnityEngine.Random.Range(goalBounds[1][0] + 2, goalBounds[1][1] - 2)
            );
            newGoalPos = new Vector3(
                floor.transform.position.x + rand.x,
                goal.transform.position.y,
                floor.transform.position.z + rand.y
            );
        } while (Vector3.Distance(agentPos, newGoalPos) < reachThreshold * 5f);

        goal.transform.position = newGoalPos;
        prevGoalPos = newGoalPos;
    }

    private void RewardAndLog()
    {
        //Penalty if feet cross over
        Vector3 rightFootPos = bodyController.rightFoot.transform.position;

        Vector3 localFootDistance = bodyController.leftFoot.transform.InverseTransformPoint(rightFootPos);

        float footReward = 0f;
        if (localFootDistance.x < 0f)
        {
            footReward -= 0.3f;
        }
        else
        {
            footReward += 0.3f - Mathf.Abs(footTargets.x - localFootDistance.x);
        }
        //penalise jumping
        if (!(IsFootTouching(bodyController.leftFoot) || IsFootTouching(bodyController.rightFoot)))
        {
            footReward -= 0.5f;
        }



        //hardcoded values created similar rewards during sim
        float poseScore = 0f;
        foreach (var jt in bodyController.targetJoints.Values)
        {
            Quaternion current = jt.joint.transform.localRotation;
            Quaternion reference = jt.initialLocalRotation;
            float angle = Quaternion.Angle(current, reference);

            float falloff = Mathf.Exp(-Mathf.Pow(angle / 45f, 2f));
            float reward = Mathf.Clamp01(falloff);

            poseScore += reward / bodyController.targetJoints.Count;
        }


        float poseReward = poseScore * (2f - verticalityBias);
        float postureReward = rewardEvaluator.Evaluate() * verticalityBias;

        AddReward(footReward);
        AddReward(poseReward);
        AddReward(postureReward);

        float angularPenalty = 0f;
        foreach (var jt in bodyController.targetJoints.Values)
        {
            angularPenalty += jt.rb.angularVelocity.magnitude;
        }

        if (angularPenalty < 20.0f)
        {
            float angReward = (20f - angularPenalty) * 0.05f;
            AddReward(angReward);
            angularPenalty = angReward;
        }
        else
        {
            angularPenalty *= -0.005f;
            AddReward(angularPenalty);
        }

        float jerkiness = 0f;
        foreach (var jt in bodyController.targetJoints.Values)
        {
            jerkiness += (jt.rotation01 - jt.oldRotation01).magnitude;
        }
        AddReward(-jerkiness * 0.001f);

        if (StepCount % 100 == 0)
        {
            Debug.Log("adding 100 bonus");
            AddReward(GetCumulativeReward() * 0.01f);
        }

        if (StepCount >= MaxStep - 1)
        {
            survivals++;
            trackEpisodeAvgLength();
            AddReward(GetCumulativeReward() * 0.02f);  // bonus for survival
        }


        if (logRewards && StepCount % logFrequency == 0)
        {
            Debug.Log($"[Reward] pose: {poseReward:F2} | posture: {postureReward:F2} | foot: {footReward:F2} | angular penalty: {angularPenalty:F2}");
            Debug.Log($"Survivals: {survivals}, Falls: {falls}");
        }
    }


    private void ApplyActions(ActionBuffers actions)
    {
        int i = 0;


        foreach (var jt in bodyController.targetJoints.Values)
        {
            Vector3 deltaEuler = Vector3.zero;
            Vector3 target = Vector3.zero;
            if (!noActionTest)
            {

                var x_drive = jt.joint.xDrive;
                var y_drive = jt.joint.yDrive;
                var z_drive = jt.joint.zDrive;

                //allows the agent to moderate the strength of each joint by +/- 99%
                float newForce = jt.baseStrength + actions.ContinuousActions[i++] * 0.99f * jt.baseStrength ;
                var newDrive = jointDriveTuner.GenerateDrive(newForce);
                jointDriveTuner.SetNewDrives(jt, newDrive);

                jt.oldRotation01 = jt.rotation01;

                jt.rotation01 = new Vector3(
                    Mathf.Clamp((actions.ContinuousActions[i++] + 1f) * 0.5f, 0.01f, 0.99f),
                    Mathf.Clamp((actions.ContinuousActions[i++] + 1f) * 0.5f, 0.01f, 0.99f),
                    Mathf.Clamp((actions.ContinuousActions[i++] + 1f) * 0.5f, 0.01f, 0.99f)
                );

                deltaEuler = new Vector3(
                    Mathf.Lerp(x_drive.lowerLimit, x_drive.upperLimit, jt.rotation01.x),
                    Mathf.Lerp(y_drive.lowerLimit, y_drive.upperLimit, jt.rotation01.y),
                    Mathf.Lerp(z_drive.lowerLimit, z_drive.upperLimit, jt.rotation01.z)
                );

                target = deltaEuler;
            }
            else
            {
                break;
            }

            var new_x_drive = jt.joint.xDrive;
            var new_y_drive = jt.joint.yDrive;
            var new_z_drive = jt.joint.zDrive;

            new_x_drive.target = target.x;
            new_y_drive.target = target.y;
            new_z_drive.target = target.z;

            jt.joint.xDrive = new_x_drive;
            jt.joint.yDrive = new_y_drive;
            jt.joint.zDrive = new_z_drive;

        }
    }


    /// <summary>
    /// dictionary containing a map of joint name, to save lookups during simulation.
    /// sped up sim time by 3x
    /// </summary>

    private Dictionary<string, ArticulationBody> GetJointMap()
    {
        foreach (var jt in bodyController.targetJoints.Values)
            jointMap[jt.joint.name] = jt.joint;
        return jointMap;
    }

    private void trackEpisodeAvgLength(){
        if (StepCount > 0)
        {
            // Store episode length in circular buffer
            recentEpisodeLengths[episodeIndex % episodeWindowSize] = StepCount;
            episodeIndex++;
            totalRecordedEpisodes = Mathf.Min(episodeIndex, episodeWindowSize);

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
