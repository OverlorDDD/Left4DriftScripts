using UnityEngine;

public struct TrackSample
{
    public Vector3    position;
    public Vector3    forward;
    public Vector3    right;
    public Vector3    up;
    public float      t;
    public float      totalWidth;
    public float      bankAngle;
    public float      distanceAlongTrack;
    public float      laneCount;   // ← додай цей рядок якщо його немає
}