
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
    private readonly float dollHeight;

    [Header("Reward Weights")]
    public float headWeight = 1.5f;
    public float chestWeight = 1f;
    public float hipsWeight = 1.0f;
    public float thighWeight = .75f;
    public float shinWeight = .75f;
    public float hipToFootWeight = 0.5f;

    private float floorSphere = 0.02f;

    public RewardEvaluator(BodyController body, GameObject floor, LayerMask floorLayer, float fallenThreshold, float heightRewardMulti, float idealShinLength, float idealThighLength, float dollHeight)
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
                fallen = checkCollider(jt.collider);
                if (fallen)
                {
                    return true;
                }
            }
        }
        return checkCollider(body.hips.GetComponent<BoxCollider>());
        
    }

    private bool checkCollider(Collider collider)
    {
        Vector3 point = collider.bounds.center;
        point.y = collider.bounds.min.y;

        Vector3 closest = collider.ClosestPoint(point);

        if (Physics.CheckSphere(point, floorSphere, floorLayer))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public float Evaluate()
    {
        float reward = 0f;

        float head = body.head.transform.position.y - floor.transform.position.y;
        float normalHead = head / dollHeight;

        if (normalHead >= 0.9f)
        {
            reward += headWeight * heightRewardMulti;
        }
        else
        {
            float sharpDrop = Mathf.Exp(-5f * (0.9f - normalHead));
            reward += sharpDrop * headWeight * heightRewardMulti;
        }


        Vector3 chestUp = body.chest.transform.up;
        float angleFromUpright = Vector3.Angle(chestUp, Vector3.up);

        float chestVerticalityReward = Mathf.Clamp01(Mathf.Exp(-Mathf.Pow(angleFromUpright / 20f, 2))) * chestWeight;

        reward += chestVerticalityReward;


        // Leg straightness
        float shinL = body.leftShin.transform.position.y - body.leftFoot.transform.position.y;
        float shinR = body.rightShin.transform.position.y - body.rightFoot.transform.position.y;
        float shinAvg = (shinL + shinR) * 0.5f / idealShinLength;

        float thighL = body.leftThigh.transform.position.y - body.leftShin.transform.position.y;
        float thighR = body.rightThigh.transform.position.y - body.rightShin.transform.position.y;
        float thighAvg = (thighL + thighR) * 0.5f / idealThighLength;

        reward += shinWeight * Mathf.Clamp01(shinAvg) * 0.5f;
        reward += thighWeight * Mathf.Clamp01(thighAvg) * 0.5f;


        return reward;
    }
}
