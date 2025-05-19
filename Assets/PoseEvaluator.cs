using System.Collections.Generic;
using UnityEngine;

public class PoseEvaluator
{
    private readonly Dictionary<string, Quaternion> referencePose;

    public PoseEvaluator(Dictionary<string, Quaternion> capturedReference)
    {
        referencePose = capturedReference;
    }

    public float EvaluatePose(Dictionary<string, ConfigurableJoint> jointMap)
    {
        float totalReward = 0f;
        int count = 0;

        foreach (var kv in jointMap)
        {
            if (!referencePose.TryGetValue(kv.Key, out var reference)) continue;

            Quaternion current = kv.Value.transform.localRotation;
            float angle = QuaternionAngle(current, reference);

            float falloff = Mathf.Exp(-Mathf.Pow(angle / 45f, 2f));
            float reward = Mathf.Clamp01(1f - falloff);

            totalReward += reward;
            count++;
        }

        return count > 0 ? totalReward / count : 0f;
    }

    private float QuaternionAngle(Quaternion a, Quaternion b)
    {
        float dot = Mathf.Clamp01(Quaternion.Dot(a, b));
        return Mathf.Acos(dot) * 2f * Mathf.Rad2Deg;
    }
}