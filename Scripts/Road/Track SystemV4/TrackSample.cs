using UnityEngine;

public struct TrackSample
{
    public Vector3    position;      // центр сплайну
    public Vector3    forward;       // напрямок руху
    public Vector3    right;         // правий вектор
    public Vector3    up;            // нормаль
    public float      t;             // параметр 0..1 вздовж сплайну
    public float      totalWidth;    // повна ширина дороги в цій точці
    public float      bankAngle;     // нахил (banking)
    public float      distanceAlongTrack; // метри від старту
}