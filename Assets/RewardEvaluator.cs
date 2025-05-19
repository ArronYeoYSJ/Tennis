using UnityEngine;

public class RewardEvaluator
{
    private readonly BodyController body;
    private readonly GameObject floor;
    private readonly float fallenThreshold;
    private readonly float heightRewardMulti;

    private readonly float idealShinLength;
    private readonly float idealThighLength;

    [Header("Reward Weights")]
    public float headWeight = 1.5f;
    public float chestWeight = 1f;
    public float hipsWeight = 1.0f;
    public float thighWeight = .75f;
    public float shinWeight = .75f;
    public float hipToFootWeight = 0.5f;

    public RewardEvaluator(BodyController body, GameObject floor, float fallenThreshold, float heightRewardMulti,float idealShinLength, float idealThighLength)
    {
        this.body = body;
        this.floor = floor;
        this.fallenThreshold = fallenThreshold;
        this.heightRewardMulti = heightRewardMulti;
        this.idealShinLength = idealShinLength;
        this.idealThighLength = idealThighLength;
    }

    public bool HasFallen()
    {
        float head = body.head.transform.position.y;
        float chest = body.chest.transform.position.y;
        float hips = body.hips.transform.position.y;

        return head <= fallenThreshold || chest <= fallenThreshold || hips <= fallenThreshold;
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
            reward -= (0.8f-normalized) * hipToFootWeight;

        reward += normalized * hipToFootWeight;

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
