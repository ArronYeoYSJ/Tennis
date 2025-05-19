
using UnityEngine;

public class RewardEvaluator
{
    private readonly BodyController body;
    private readonly GameObject floor;
    private LayerMask floorLayer;
    private readonly float fallenThreshold;
    private readonly float heightRewardMulti;

    private readonly float idealShinLength;
    private readonly float idealThighLength;

    [Header("Reward Weights")]
    public float headWeight = 1.5f;
    public float chestWeight = 1f;
    public float hipsWeight = 3.0f;
    public float thighWeight = .75f;
    public float shinWeight = .75f;
    public float hipToFootWeight = 0.5f;

    private float floorSphere = 0.02f;

    public RewardEvaluator(BodyController body, GameObject floor, LayerMask floorLayer, float fallenThreshold, float heightRewardMulti, float idealShinLength, float idealThighLength)
    {
        this.body = body;
        this.floor = floor;
        this.fallenThreshold = fallenThreshold;
        this.heightRewardMulti = heightRewardMulti;
        this.idealShinLength = idealShinLength;
        this.idealThighLength = idealThighLength;
        this.floorLayer = floorLayer;
    }

    public bool HasFallen()
    {
        bool fallen = false;
        foreach (var jt in body.targetJoints.Values)
        {
            if (!jt.allowGroundContact)
            {
                Vector3 point = jt.collider.bounds.center;
                point.y = jt.collider.bounds.min.y;

                Vector3 closest = jt.collider.ClosestPoint(point);

                if (Physics.CheckSphere(point, floorSphere, floorLayer))
                {
                    fallen = true;
                    break;
                }
            }
        }
        return fallen;
    }

    public float Evaluate()
    {
        float reward = 0f;

        float head = body.head.transform.position.y - floor.transform.position.y;
        float chest = body.chest.transform.position.y - floor.transform.position.y;
        float hips = body.hips.transform.position.y - floor.transform.position.y;

        reward += Mathf.Clamp01((head - fallenThreshold) / 0.5f) * heightRewardMulti * headWeight;
        reward += Mathf.Clamp01((chest - fallenThreshold) / 0.5f) * heightRewardMulti * chestWeight;
        reward += Mathf.Clamp01((hips - fallenThreshold) / 0.5f) * heightRewardMulti * hipsWeight;
        

        // Hip to foot distance (encourages straight legs)
        float avgFoot = (body.leftFoot.transform.position.y + body.rightFoot.transform.position.y) * 0.5f;
        float hipToFoot = hips - avgFoot;
        float normalized = Mathf.Clamp01(hipToFoot / 0.9f);

        if (normalized < 0.8f)
        {
            reward -= (0.8f - normalized) * hipToFootWeight;
        } else {
            reward += normalized * hipToFootWeight;
        }

        // Leg straightness
        float shinL = body.leftShin.transform.position.y - body.leftFoot.transform.position.y;
        float shinR = body.rightShin.transform.position.y - body.rightFoot.transform.position.y;
        float shinAvg = (shinL + shinR) * 0.5f / idealShinLength;

        float thighL = body.leftThigh.transform.position.y - body.leftShin.transform.position.y;
        float thighR = body.rightThigh.transform.position.y - body.rightShin.transform.position.y;
        float thighAvg = (thighL + thighR) * 0.5f / idealThighLength;

        reward += shinWeight * Mathf.Clamp01(shinAvg);
        reward += thighWeight * Mathf.Clamp01(thighAvg);

        return reward;
    }
}
