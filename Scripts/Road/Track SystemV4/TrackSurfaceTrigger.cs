using UnityEngine;

public class TrackSurfaceTrigger : MonoBehaviour
{
    public SurfaceType surfaceType = SurfaceType.Asphalt;

    [Header("Кастомні параметри (заповнюються TrackSurfacePatches)")]
    public bool  useCustomValues  = false;
    public float customGrip       = 1f;   // 0 = повне ковзання, 1 = нормальне зчеплення
    public float customSpeed      = 1f;   // множник макс. швидкості на цій ділянці
    public float customBoostForce = 20f;  // сила поштовху якщо surfaceType == Boost
    public bool  customSlippery   = false;
}