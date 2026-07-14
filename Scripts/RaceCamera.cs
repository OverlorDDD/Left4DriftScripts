using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RaceCamera : MonoBehaviour
{
    [Header("Посилання")]
    public Transform     carTransform;
    public Rigidbody     carRb;
    public CarController carController;
    public Volume        postProcessVolume;

    [Header("Базова позиція")]
    public float distance   = 6f;
    public float height     = 2f;
    public float lookHeight = 0.8f;

    [Header("Плавність")]
    public float positionSmooth = 6f;
    public float rotationSmooth = 5f;

    [Header("Look Ahead")]
    public float lookAheadAmount = 2.5f;
    public float lookAheadSmooth = 3f;

    [Header("Нахил в поворотах")]
    public float tiltAmount = 4f;
    public float tiltSmooth = 4f;

    [Header("Lag при прискоренні")]
    public float lagDistance = 1.2f;
    public float lagSmooth   = 5f;

    [Header("FOV")]
    public float baseFov   = 70f;
    public float maxFov    = 92f;
    public float fovSmooth = 5f;

    [Header("Буст — зарядка (камера наближається)")]
    public float boostChargeZoomIn = 1.5f;
    public float boostChargeSmooth = 3f;

    [Header("Буст — старт (камера відлітає)")]
    public float boostLaunchPullback = 4f;
    public float boostLaunchDuration = 0.4f;
    public float boostLaunchSmooth   = 12f;

    [Header("Буст — тремтіння камери")]
    public float boostShakeAmplitude = 0.15f;
    public float boostShakeFrequency = 30f;
    public float boostShakeDuration  = 0.5f;

    [Header("Post FX")]
    public float maxVignetteIntensity   = 0.45f;
    public float maxAberrationIntensity = 0.6f;
    public float maxBlurStrength        = 0.15f;
    public float effectsSmooth          = 4f;

    

    // ── приватні ──
    Camera              cam;
    Vignette            vignette;
    ChromaticAberration aberration;
    MotionBlur          motionBlur;
    LensDistortion      lensDistortion;

    Vector3 lookAheadPos;
    Vector3 smoothVelocity;
    float   currentTilt;
    float   currentLag;
    float   currentDistanceOffset;

    float boostLaunchTimer;
    float boostShakeTimer;
    bool  wasBoostingLastFrame;

    // ════════════════════════════════════════
    void Start()
    {
        cam = GetComponent<Camera>();

        if (postProcessVolume != null)
        {
            postProcessVolume.profile.TryGet(out vignette);
            postProcessVolume.profile.TryGet(out aberration);
            postProcessVolume.profile.TryGet(out motionBlur);
            postProcessVolume.profile.TryGet(out lensDistortion);
        }
    }

    // ════════════════════════════════════════
    void LateUpdate()
    {
        if (carTransform == null || carRb == null || carController == null) return;

        float dt          = Time.deltaTime;
        float speed       = carRb.linearVelocity.magnitude;
        float speedFactor = Mathf.Clamp01(speed / 35f);
        bool  isBoosting  = carController.IsBoosting();
        float boostCharge = carController.GetBoostCharge();

        // ── Детект старту бусту — один кадр ──
        if (isBoosting && !wasBoostingLastFrame)
        {
            boostLaunchTimer = boostLaunchDuration;
            boostShakeTimer  = boostShakeDuration;
        }
        wasBoostingLastFrame = isBoosting;

        if (boostLaunchTimer > 0) boostLaunchTimer -= dt;
        if (boostShakeTimer  > 0) boostShakeTimer  -= dt;

        // ════════════
        //  ДИСТАНЦІЯ
        // ════════════
        float targetOffset = 0f;

        // Зарядка — наближаємось
        if (!isBoosting && boostCharge > 0f)
            targetOffset -= boostCharge * boostChargeZoomIn;

        // Старт бусту — різкий відліт назад
        if (boostLaunchTimer > 0f)
            targetOffset += boostLaunchPullback * (boostLaunchTimer / boostLaunchDuration);

        float smoothSpeed = boostLaunchTimer > 0f ? boostLaunchSmooth : boostChargeSmooth;
        currentDistanceOffset = Mathf.Lerp(currentDistanceOffset, targetOffset, smoothSpeed * dt);

        // ════════════
        //  ТРЕМТІННЯ
        // ════════════
        Vector3 shakeOffset = Vector3.zero;
        if (boostShakeTimer > 0f)
        {
            float t  = boostShakeTimer / boostShakeDuration;
            shakeOffset = new Vector3(
                Mathf.Sin(Time.time * boostShakeFrequency)        * boostShakeAmplitude * t,
                Mathf.Sin(Time.time * boostShakeFrequency * 1.3f) * boostShakeAmplitude * t * 0.5f,
                0f);
        }

        // ════════════
        //  ПОЗИЦІЯ
        // ════════════
        float ahead = Mathf.Clamp01(speed / 35f);
        lookAheadPos = Vector3.Lerp(
            lookAheadPos,
            carTransform.forward * lookAheadAmount * ahead,
            lookAheadSmooth * dt);

        float accel      = Vector3.Dot(carRb.linearVelocity, carTransform.forward);
        float targetLag  = Mathf.Clamp01(accel / 35f) * lagDistance;
        currentLag       = Mathf.Lerp(currentLag, targetLag, lagSmooth * dt);

        Vector3 desiredPos =
            carTransform.position
            - carTransform.forward * (distance + currentLag + currentDistanceOffset)
            + Vector3.up * height;

        transform.position = Vector3.SmoothDamp(
            transform.position, desiredPos, ref smoothVelocity, 1f / positionSmooth)
            + shakeOffset;

        // ════════════
        //  РОТАЦІЯ
        // ════════════
        Vector3 lookTarget =
            carTransform.position + lookAheadPos + Vector3.up * lookHeight;

        Quaternion desiredRot = Quaternion.LookRotation(lookTarget - transform.position);

        Vector3 localVel = carTransform.InverseTransformDirection(carRb.linearVelocity);
        float   side     = Mathf.Clamp(localVel.x / 35f, -1f, 1f);
        currentTilt      = Mathf.Lerp(currentTilt, -side * tiltAmount, tiltSmooth * dt);

        desiredRot *= Quaternion.Euler(0, 0, currentTilt);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, rotationSmooth * dt);

        // ════════════
        //  FOV
        // ════════════
        float driftFactor = Mathf.Clamp01(Mathf.Abs(localVel.x) / 10f);
        float targetFov   = Mathf.Lerp(baseFov, maxFov, speedFactor) + driftFactor * 4f;

        if (!isBoosting && boostCharge > 0.3f)
            targetFov -= boostCharge * 5f;                                    // звужується при зарядці
        if (boostLaunchTimer > 0f)
            targetFov += (boostLaunchTimer / boostLaunchDuration) * 8f;      // удар при старті

        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, fovSmooth * dt);

        // ════════════
        //  POST FX
        // ════════════
        UpdatePostFX(speedFactor, driftFactor, isBoosting);
    }

    // ════════════════════════════════════════
    void UpdatePostFX(float speedFactor, float driftFactor, bool isBoosting)
    {
        float dt = Time.deltaTime;

        // Vignette
        float tVig = Mathf.Max(
            Mathf.Lerp(0.2f, maxVignetteIntensity, speedFactor),
            driftFactor * 0.4f);
        if (isBoosting) tVig = Mathf.Max(tVig, 0.5f);
        if (vignette != null)
            vignette.intensity.Override(
                Mathf.Lerp(vignette.intensity.value, tVig, effectsSmooth * dt));

        // Chromatic Aberration — спалах при старті бусту
        float tAber = Mathf.Max(driftFactor * maxAberrationIntensity, speedFactor * 0.2f);
        if (boostShakeTimer > 0f)
            tAber = Mathf.Max(tAber, (boostShakeTimer / boostShakeDuration) * 0.8f);
        if (aberration != null)
            aberration.intensity.Override(
                Mathf.Lerp(aberration.intensity.value, tAber, effectsSmooth * dt));

        // Motion Blur
        float tBlur = Mathf.Lerp(0f, maxBlurStrength, speedFactor * speedFactor);
        if (motionBlur != null)
            motionBlur.intensity.Override(
                Mathf.Lerp(motionBlur.intensity.value, tBlur, effectsSmooth * dt));

        // Lens Distortion — різкий удар при буст-старті
        float tDist = -(driftFactor * 8f + speedFactor * 5f);
        if (boostLaunchTimer > 0f)
            tDist -= (boostLaunchTimer / boostLaunchDuration) * 15f;
        if (lensDistortion != null)
            lensDistortion.intensity.Override(
                Mathf.Lerp(lensDistortion.intensity.value, tDist, effectsSmooth * dt));
    }
}