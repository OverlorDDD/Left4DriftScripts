using UnityEngine;

[RequireComponent(typeof(CarController))]
public class TrackSurfaceEffect : MonoBehaviour
{
    public TrackSurfaceProperties surfaceProperties;

    CarController car;
    Rigidbody     rb;
    SurfaceType   currentSurface = SurfaceType.Asphalt;

    // Поточні множники — CarController читає їх через геттери
    public float GripMultiplier    { get; private set; } = 1f;
    public float SpeedMultiplier   { get; private set; } = 1f;
    public float TractionMultiplier{ get; private set; } = 1f;
    public bool  IsSlippery        { get; private set; } = false;
    public float BoostMultiplier   { get; private set; } = 1f;

    void Awake()
    {
        car = GetComponent<CarController>();
        rb  = GetComponent<Rigidbody>();
    }

    void OnCollisionStay(Collision collision)
    {
        var trigger = collision.gameObject.GetComponent<TrackSurfaceTrigger>();
        if (trigger == null) return;

        if (trigger.surfaceType != currentSurface)
        {
            currentSurface = trigger.surfaceType;
            ApplySurface(currentSurface);
        }
    }

    void OnCollisionExit(Collision collision)
    {
        // Якщо злетіли з дороги — асфальт за замовчуванням
        if (collision.gameObject.GetComponent<TrackSurfaceTrigger>() != null)
        {
            currentSurface = SurfaceType.Asphalt;
            ApplySurface(currentSurface);
        }
    }

    void ApplySurface(SurfaceType type)
    {
        if (surfaceProperties == null) return;

        var props = surfaceProperties.Get(type);
        GripMultiplier     = props.gripMultiplier;
        SpeedMultiplier    = props.speedMultiplier;
        TractionMultiplier = props.tractionMultiplier;
        IsSlippery         = props.isSlippery;
        BoostMultiplier    = props.boostMultiplier;

        // Якщо вода або багнюка — гасимо швидкість
        if (props.slowsDown)
            rb.linearVelocity *= props.speedMultiplier;
    }

    public SurfaceType GetCurrentSurface() => currentSurface;
}