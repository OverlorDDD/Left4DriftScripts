using UnityEngine;

[CreateAssetMenu(fileName = "CarStats", menuName = "Racing/Car Stats")]
public class CarStats : ScriptableObject
{
    [Header("UI Ratings (0-10 для меню вибору авто)")]
    [Range(0,10)] public float speedRating        = 5f;
    [Range(0,10)] public float accelerationRating = 5f;
    [Range(0,10)] public float handlingRating     = 5f;
    [Range(0,10)] public float driftRating        = 5f;

    [Header("Двигун")]
    [Range(10f,  80f)] public float maxSpeed                   = 35f;
    [Range(5f,   50f)] public float acceleration               = 30f;
    [Range(0.5f,  5f)] public float accelerationCurveSharpness = 2.5f;
    [Range(10f, 150f)] public float brakingForce               = 80f;
    [Range(5f,   60f)] public float reverseAcceleration        = 60f;
    [Range(5f,   25f)] public float reverseMaxSpeed            = 15f;
    [Range(0f,  0.2f)] public float engineBraking              = 0.04f;

    [Header("Керування")]
    [Range(50f, 200f)] public float steeringPower      = 98.5f;
    [Range(0f,    1f)] public float lowSpeedSteering   = 0.30f;
    [Range(0f,    1f)] public float highSpeedSteering  = 0.65f;
    [Range(10f,  50f)] public float wheelTurnAngle     = 35f;
    [Range(0f,    2f)] public float corneringSpeedLoss = 0.50f;

    [Header("Зчеплення")]
    [Range(0.5f,  1f)] public float normalGrip    = 0.96f;
    [Range(0.5f,  1f)] public float traction      = 0.95f;

    [Header("Дрифт")]
    [Range(0.1f,  1f)] public float driftGripMin  = 0.55f;
    [Range(0.5f,  1f)] public float driftGripMax  = 0.88f;
    [Range(0f,  0.5f)] public float driftTraction = 0.08f;

    [Header("Буст")]
    [Range(1f,   10f)] public float boostChargeTime        = 3f;
    [Range(0.5f,  5f)] public float boostDuration          = 2.5f;
    [Range(1.1f,  2f)] public float boostSpeedMultiplier   = 1.45f;
    [Range(0f,   20f)] public float boostLaunchImpulse     = 10f;
    [Range(0f,    1f)] public float boostSplineCorrection  = 0.25f;

    [Header("Фізика")]
    [Range(0f,  80f)] public float extraGravity        = 35f;
    [Range(0f,  20f)] public float downforce           = 5f;
    [Range(0f,   1f)] public float weightTransfer      = 0.08f;
    [Range(1f,  20f)] public float suspensionStiffness = 8f;
}