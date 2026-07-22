using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "TrackControlProfile", menuName = "Track/Control Profile")]
public class TrackControlProfile : ScriptableObject
{
    [System.Serializable]
    public class ControlPoint
    {
        public string             label        = "";
        [Range(0f, 1f)]
        public float              t            = 0f;
        [Range(1f, 50f)]
        public float              halfWidth    = 5f;
        public float              heightOffset = 0f;
        [Range(-45f, 45f)]
        public float              bankAngle    = 0f;

        [Header("Кількість смуг (для текстури дороги)")]
        [Tooltip("1, 2 або 4 — плавно блендиться в шейдері дороги між сусідніми точками")]
        [Range(1, 4)]
        public int laneCount = 1;
    }

    public List<ControlPoint> points = new List<ControlPoint>
    {
        new ControlPoint { t = 0f, halfWidth = 5f, label = "Start", laneCount = 1 },
        new ControlPoint { t = 1f, halfWidth = 5f, label = "End",   laneCount = 1 }
    };

    public struct EvalResult
    {
        public float halfWidth;
        public float heightOffset;
        public float bankAngle;
        public float laneCount; // вже інтерпольоване, дробове значення 1-4
    }

    public EvalResult Evaluate(float t)
    {
        if (points == null || points.Count == 0)
            return new EvalResult { halfWidth = 5f, laneCount = 1f };

        var sorted = new List<ControlPoint>(points);
        sorted.Sort((a, b) => a.t.CompareTo(b.t));

        if (points.Count == 1 || t <= sorted[0].t)
            return ToResult(sorted[0]);
        if (t >= sorted[sorted.Count - 1].t)
            return ToResult(sorted[sorted.Count - 1]);

        for (int i = 0; i < sorted.Count - 1; i++)
        {
            if (t >= sorted[i].t && t <= sorted[i + 1].t)
            {
                float lt = (t - sorted[i].t) / (sorted[i + 1].t - sorted[i].t);
                lt = lt * lt * (3f - 2f * lt); // smoothstep

                return new EvalResult
                {
                    halfWidth    = Mathf.Lerp(sorted[i].halfWidth,    sorted[i+1].halfWidth,    lt),
                    heightOffset = Mathf.Lerp(sorted[i].heightOffset, sorted[i+1].heightOffset, lt),
                    bankAngle    = Mathf.Lerp(sorted[i].bankAngle,    sorted[i+1].bankAngle,    lt),
                    laneCount    = Mathf.Lerp(sorted[i].laneCount,    sorted[i+1].laneCount,    lt)
                };
            }
        }
        return new EvalResult { halfWidth = 5f, laneCount = 1f };
    }

    static EvalResult ToResult(ControlPoint p) => new EvalResult
    {
        halfWidth    = p.halfWidth,
        heightOffset = p.heightOffset,
        bankAngle    = p.bankAngle,
        laneCount    = p.laneCount
    };
}