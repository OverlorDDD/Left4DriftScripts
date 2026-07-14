using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Характеристики авто")]
    public CarStats stats;

    [Header("Прив'язка до сплайну")]
    public SplineContainer trackSpline;

    [Header("Візуальний кузов")]
    public Transform carBodyVisual;
    [Range(1f, 10f)] public float bodyRollAmount   = 2f;
    [Range(1f, 20f)] public float bodyRollSpeed    = 8f;
    [Range(0f, 40f)] public float maxBodyBankAngle = 20f;

    [Header("Колеса")]
    public float     wheelSpinSpeed  = 800f;
    public Transform frontLeftWheel;
    public Transform frontRightWheel;
    public Transform rearLeftWheel;
    public Transform rearRightWheel;

    [Header("Ефекти")]
    public ParticleSystem leftSmoke;
    public ParticleSystem rightSmoke;
    public TrailRenderer  leftSkid;
    public TrailRenderer  rightSkid;
    public ParticleSystem boostFireLeft;
    public ParticleSystem boostFireRight;

    // ── Стан фізики ──
    bool    canMove          = true;
    bool    drifting;
    bool    isReversing;
    bool    grounded;
    float   currentSpeed;
    float   driftAmount;
    float   currentDriftAngle;
    float   weightRoll;

    // ── Стан бусту ──
    bool    boosting;
    bool    boostReady;
    float   shiftHoldTime;
    float   boostTimer;

    // ── Таймери ──
    float   offTrackTimer;
    float   wallTimer;

    // ── Поверхня ──
    SurfaceType currentSurface    = SurfaceType.Asphalt;
    float       surfaceGripMult   = 1f;
    float       surfaceSpeedMult  = 1f;
    bool        surfaceIsSlippery = false;
    SurfaceType surfaceOverride     = SurfaceType.Asphalt;
    bool        hasSurfaceOverride  = false;

    // ── Земля ──
    Vector3 groundNormal = Vector3.up;
    const float RAY_DIST = 1.5f;
    const float RAY_OFFSET_Y = 0.3f;

    // ── Сплайн ──
    float currentSplineT = 0f;

    // ── Залежності ──
    Rigidbody          rb;
    RaceManager        raceManager;
    PlayerInputHandler inputHandler;

    // ── Публічний API ──
    public bool  IsBoosting()      => boosting;
    public bool  IsDrifting()      => drifting;
    public bool  IsBoostReady()    => boostReady;
    public bool  IsGrounded()      => grounded;
    public float GetDriftAmount()  => driftAmount;
    public float GetCurrentSpeed() => currentSpeed;
    public float GetMaxSpeed()     => stats != null ? stats.maxSpeed * (boosting ? stats.boostSpeedMultiplier : 1f) : 35f;
    public float GetBoostCharge()  => stats != null ? Mathf.Clamp01(shiftHoldTime / stats.boostChargeTime) : 0f;
    public void  SetCanMove(bool v) => canMove = v;

    // ════════════════════════════════════════
   void Start()
{
    rb           = GetComponent<Rigidbody>();
    raceManager  = FindAnyObjectByType<RaceManager>();
    inputHandler = GetComponent<PlayerInputHandler>();

    rb.centerOfMass           = new Vector3(0f, 2f, 0f);
    rb.linearDamping          = 0.02f;  // трохи лінійного демпфінгу — гасить мікровіщання
    rb.angularDamping         = 4f;     // було 3 — трохи більше
    rb.interpolation          = RigidbodyInterpolation.Interpolate;
    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    rb.maxAngularVelocity     = 6f;     // було 8

    if (stats == null)
        Debug.LogWarning("CarController: CarStats не призначений!");
}

    // ════════════════════════════════════════
    void FixedUpdate()
    {
        if (stats == null) return;
        if (raceManager != null && !raceManager.raceStarted) return;

        float dt = Time.fixedDeltaTime;
        currentSpeed = rb.linearVelocity.magnitude;

        // ── Вхід ──
        ReadInput(out float moveInput, out float turnInput, out bool shiftHeld);

        // ── Таймери ──
        if (wallTimer     > 0) wallTimer     -= dt;
        if (offTrackTimer > 0) offTrackTimer -= dt;
        if (boostTimer    > 0) boostTimer    -= dt;
        else                   boosting       = false;

        // ── Земля і поверхня ──
        DetectGround();
        ApplyGravityAndDownforce();

        // ── Вирівнювання з дорогою ──
        AlignToRoadSurface();

        // ── Напрямок ──
        isReversing = Vector3.Dot(rb.linearVelocity, transform.forward) < -0.5f;

        // ── Сплайн ──
        if (trackSpline != null)
            UpdateSplineProjection();

        // ── Системи ──
        HandleBoostCharge(shiftHeld, dt);

        if (wallTimer <= 0 && canMove)
            HandleMovement(moveInput, dt);

        ApplyOffTrackPenalty();

        if (canMove)
            HandleSteering(turnInput, dt);

        HandleGrip();
        HandleTraction(dt);

        // ── Вага ──
        float targetWeightRoll = -turnInput
            * Mathf.Clamp01(currentSpeed / stats.maxSpeed)
            * stats.weightTransfer;
        weightRoll = Mathf.Lerp(
            weightRoll, targetWeightRoll, stats.suspensionStiffness * dt);

        // ── Обмеження швидкості ──
        float speedCap = (boosting ? stats.maxSpeed * stats.boostSpeedMultiplier : stats.maxSpeed)
                         * surfaceSpeedMult;
        if (currentSpeed > speedCap)
            rb.linearVelocity = rb.linearVelocity.normalized * speedCap;

        // ── Візуал ──
        UpdateBodyVisual(turnInput);
        UpdateWheels(moveInput, turnInput);
        UpdateEffects();
    }

    // ════════════════════════════════════════
    //  ВХІД
    // ════════════════════════════════════════
    void ReadInput(out float move, out float turn, out bool shift)
    {
        if (inputHandler != null)
        {
            move     = inputHandler.ThrottleInput;
            turn     = inputHandler.SteerInput;
            drifting = inputHandler.DriftInput;
            shift    = inputHandler.BoostInput;
            return;
        }

        if (Keyboard.current != null)
        {
            move  = Keyboard.current.wKey.isPressed ?  1f :
                    Keyboard.current.sKey.isPressed ? -1f : 0f;
            turn  = Keyboard.current.dKey.isPressed ?  1f :
                    Keyboard.current.aKey.isPressed ? -1f : 0f;
            drifting = Keyboard.current.spaceKey.isPressed;
            shift    = Keyboard.current.leftShiftKey.isPressed;
            return;
        }

        move = turn = 0f; shift = drifting = false;
    }

    // ════════════════════════════════════════
    //  ВИЯВЛЕННЯ ЗЕМЛІ
    //  4 промені з кутів — середнє нормаль і поверхня
    // ════════════════════════════════════════
    // ════════════════════════════════════════
//  GROUND DETECTION — anti-sink + 4-wheel landing
// ════════════════════════════════════════
void DetectGround()
{
    Vector3[] origins = {
        transform.position + transform.right *  0.5f + transform.forward *  0.9f + Vector3.up * 0.1f,
        transform.position - transform.right *  0.5f + transform.forward *  0.9f + Vector3.up * 0.1f,
        transform.position + transform.right *  0.5f - transform.forward *  0.8f + Vector3.up * 0.1f,
        transform.position - transform.right *  0.5f - transform.forward *  0.8f + Vector3.up * 0.1f,
    };

    int     hitCount   = 0;
    Vector3 normalSum  = Vector3.zero;
    float   avgPen     = 0f; // середнє "провалювання" в землю
    SurfaceType detected = SurfaceType.Asphalt;

    foreach (var origin in origins)
    {
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1.8f))
        {
            hitCount++;
            normalSum += hit.normal;

            // Провалювання: скільки нижче ніж повинен бути корпус
            float pen = (origin.y - 0.1f) - hit.point.y;
            if (pen < 0f) avgPen += pen; // від'ємне = занурений

            var trig = hit.collider.GetComponent<TrackSurfaceTrigger>();
            if (trig != null) detected = trig.surfaceType;
        }
    }

    bool wasGrounded = grounded;
    grounded     = hitCount >= 2;
    groundNormal = grounded ? (normalSum / hitCount).normalized : Vector3.up;

    // ── Anti-sink: виштовхуємо машину з поверхні якщо застрягла ──
    if (grounded && avgPen < -0.05f)
    {
        rb.position += Vector3.up * Mathf.Abs(avgPen) * 0.8f;
    }

    // ── Приземлення: якщо щойно торкнулись землі з великою швидкістю ──
    if (!wasGrounded && grounded)
    {
        float impactSpeed = Mathf.Abs(rb.linearVelocity.y);
        if (impactSpeed > 3f)
        {
            // Squash ефект: різко притискуємо до землі
            rb.AddForce(groundNormal * -impactSpeed * 2f, ForceMode.VelocityChange);
        }
    }

    if (grounded && !hasSurfaceOverride) ApplySurface(detected);
    
}

    void ApplySurface(SurfaceType type)
    {
        if (type == currentSurface) return;
        currentSurface = type;

        (surfaceGripMult, surfaceSpeedMult, surfaceIsSlippery) = type switch
        {
            SurfaceType.Asphalt => (1.00f, 1.00f, false),
            SurfaceType.Grass   => (0.80f, 0.85f, false),
            SurfaceType.Dirt    => (0.70f, 0.75f, false),
            SurfaceType.Mud     => (0.40f, 0.50f, false),
            SurfaceType.Sand    => (0.60f, 0.65f, false),
            SurfaceType.Ice     => (0.15f, 1.00f, true),
            SurfaceType.Snow    => (0.50f, 0.80f, true),
            SurfaceType.Water   => (0.30f, 0.60f, false),
            SurfaceType.Boost   => (1.00f, 1.20f, false),
            _                   => (1.00f, 1.00f, false)
        };
    }

    // ════════════════════════════════════════
    //  ГРАВІТАЦІЯ І DOWNFORCE
    // ════════════════════════════════════════
   void ApplyGravityAndDownforce()
{
    if (!grounded)
    {
        // В повітрі — важке падіння. Множник росте з часом польоту (відчуття ваги)
        rb.AddForce(Vector3.down * stats.extraGravity, ForceMode.Acceleration);
        // Гасимо горизонтальне обертання в польоті
        rb.angularVelocity = Vector3.Lerp(
            rb.angularVelocity, Vector3.up * rb.angularVelocity.y, 0.15f);
    }
    else
    {
        float df = (2f + currentSpeed * 0.15f) * stats.downforce;
        rb.AddForce(-groundNormal * df, ForceMode.Acceleration);
    }
}

    // ════════════════════════════════════════
    //  ВИРІВНЮВАННЯ З ДОРОГОЮ (фізичний торк)
    //  Замість av.x = av.z = 0 — пружинний торк до groundNormal
    //  Дозволяє нормально їздити по banking і пагорбах
    //  Запобігає перекиданню носом при зіткненнях
    // ════════════════════════════════════════
   void AlignToRoadSurface()
{
    Vector3 currentUp  = transform.up;
    Vector3 targetUp   = groundNormal;
    Vector3 torqueAxis = Vector3.Cross(currentUp, targetUp);
    float   tiltAngle  = torqueAxis.magnitude;

    if (tiltAngle > 0.005f)
    {
        float spring = grounded ? 12f : 4f;
        rb.AddTorque(torqueAxis.normalized * tiltAngle * spring, ForceMode.Acceleration);
    }

    Vector3 av = transform.InverseTransformDirection(rb.angularVelocity);
    av.x = Mathf.Lerp(av.x, 0f, grounded ? 0.35f : 0.08f);
    av.z = Mathf.Lerp(av.z, 0f, grounded ? 0.35f : 0.08f);
    rb.angularVelocity = transform.TransformDirection(av);
}
    // ════════════════════════════════════════
    //  ПОЗИЦІЯ НА СПЛАЙНІ (прогресивний пошук)
    // ════════════════════════════════════════
    void UpdateSplineProjection()
    {
        var   spline      = trackSpline.Spline;
        float bestT       = currentSplineT;
        float bestDist    = float.MaxValue;
        float searchRange = 0.08f;
        const int steps   = 16;

        for (int i = 0; i <= steps; i++)
        {
            float testT = currentSplineT - searchRange + (2f * searchRange * i / steps);
            // Зациклений трек
            if (testT < 0f) testT += 1f;
            if (testT > 1f) testT -= 1f;

            spline.Evaluate(testT, out float3 pt, out float3 _, out float3 _);
            float dist = Vector3.SqrMagnitude(transform.position - (Vector3)pt);

            if (dist < bestDist)
            {
                bestDist = dist;
                bestT    = testT;
            }
        }

        currentSplineT = bestT;
    }

    // Повертає forward сплайну в поточній позиції
    Vector3 GetSplineForward()
    {
        if (trackSpline == null) return transform.forward;
        trackSpline.Spline.Evaluate(
            currentSplineT, out float3 _, out float3 fwd, out float3 _);
        return ((Vector3)fwd).normalized;
    }

    // ════════════════════════════════════════
    //  РУХ
    // ════════════════════════════════════════
    void HandleMovement(float moveInput, float dt)
    {
        float forwardDot      = Vector3.Dot(rb.linearVelocity.normalized, transform.forward);
        float effectiveMaxSpd = stats.maxSpeed * surfaceSpeedMult;

        if (moveInput > 0f)
        {
            float accel  = stats.acceleration * (boosting ? stats.boostSpeedMultiplier : 1f);
            float ratio  = Mathf.Clamp01(currentSpeed / effectiveMaxSpd);
            float torque = Mathf.Lerp(1f, 0f, Mathf.Pow(ratio, stats.accelerationCurveSharpness));

            rb.AddForce(transform.forward * accel * torque, ForceMode.Acceleration);

            // Легка корекція в напрямку сплайну
            // (сильніша при бусті, майже непомітна — просто вирівнює машину)
            if (trackSpline != null)
            {
                Vector3 splineFwd    = GetSplineForward();
                Vector3 correction   = splineFwd - transform.forward;
                float   corrStrength = boosting
                    ? stats.boostSplineCorrection
                    : stats.boostSplineCorrection * 0.15f;
                rb.AddForce(correction * accel * corrStrength, ForceMode.Acceleration);
            }
        }
        else if (moveInput < 0f)
        {
            if (forwardDot > 0.2f)
            {
                // Гальмування
                rb.AddForce(-rb.linearVelocity.normalized * stats.brakingForce,
                            ForceMode.Acceleration);
            }
            else
            {
                // Задній хід
                float sf = Mathf.Clamp01(1f - currentSpeed / stats.reverseMaxSpeed);
                rb.AddForce(-transform.forward * stats.reverseAcceleration * sf,
                            ForceMode.Acceleration);
            }
        }
        else
        {
            // Engine braking
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, stats.engineBraking);
        }

        // Втрата швидкості в повороті (тільки поза дрифтом)
        if (!drifting && currentSpeed > 3f)
        {
            float alignment    = Vector3.Dot(rb.linearVelocity.normalized, transform.forward);
            float misalignment = 1f - Mathf.Clamp01(alignment);
            rb.linearVelocity *= 1f - misalignment * stats.corneringSpeedLoss * dt * 60f;
        }
    }

    // ════════════════════════════════════════
    //  ПОВОРОТИ
    // ════════════════════════════════════════
    void HandleSteering(float turnInput, float dt)
    {
        if (currentSpeed < 0.5f) return;

        float speedPercent  = Mathf.Clamp01(currentSpeed / stats.maxSpeed);
        float steeringLimit = Mathf.Lerp(
            stats.lowSpeedSteering,
            stats.highSpeedSteering,
            Mathf.Pow(speedPercent, 0.6f));

        if (drifting) steeringLimit *= 1.5f;

        float amount = turnInput * stats.steeringPower * steeringLimit * dt;
        if (isReversing) amount *= -1f;

        rb.MoveRotation(rb.rotation * Quaternion.Euler(0, amount, 0));
    }

    // ════════════════════════════════════════
    //  ЗЧЕПЛЕННЯ
    // ════════════════════════════════════════
    void HandleGrip()
    {
        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        driftAmount = localVel.x;

        float targetGrip;
        if (drifting)
        {
            float slide  = Mathf.Clamp01(Mathf.Abs(localVel.x) / 15f);
            targetGrip   = Mathf.Lerp(stats.driftGripMax, stats.driftGripMin, slide);
        }
        else
        {
            targetGrip = stats.normalGrip * surfaceGripMult;
        }

        // Льод — мінімальне зчеплення
        if (surfaceIsSlippery)
            targetGrip *= 0.25f;

        float desired  = localVel.x * targetGrip;
        localVel.x = Mathf.MoveTowards(localVel.x, desired, 18f * Time.fixedDeltaTime);
        rb.linearVelocity = transform.TransformDirection(localVel);

        // Кут дрифту для візуалу
        float dt = Time.fixedDeltaTime;
        currentDriftAngle = drifting
            ? Mathf.Lerp(currentDriftAngle, driftAmount, 5f * dt)
            : Mathf.Lerp(currentDriftAngle, 0f,          6f * dt);
    }

    // ════════════════════════════════════════
    //  ТЯГА
    // ════════════════════════════════════════
    void HandleTraction(float dt)
{
    if (currentSpeed < 1f) return;

    Vector3 targetVel  = transform.forward * currentSpeed;
    // Було: correction * traction(0.95) — занадто сильно, викликало тряску
    // Стало: м'який Lerp
    float   factor     = drifting
        ? stats.driftTraction * dt
        : stats.traction      * dt;

    rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVel, factor);
}

    // ════════════════════════════════════════
    //  БУСТ
    // ════════════════════════════════════════
    void HandleBoostCharge(bool shiftHeld, float dt)
    {
        if (boosting) return;

        if (shiftHeld)
        {
            shiftHoldTime += dt;
            if (shiftHoldTime >= stats.boostChargeTime && !boostReady)
                boostReady = true;
        }
        else
        {
            if (boostReady)
            {
                boosting      = true;
                boostTimer    = stats.boostDuration;
                boostReady    = false;
                shiftHoldTime = 0f;

                // Плавний імпульс замість різкого VelocityChange
                StartCoroutine(SmoothBoostLaunch());
            }
            shiftHoldTime = 0f;
        }
    }

    // Плавне прискорення бусту за 0.3 сек (пункт 7)
    IEnumerator SmoothBoostLaunch()
    {
        float elapsed  = 0f;
        float duration = 0.3f;
        float total    = stats.boostLaunchImpulse;

        while (elapsed < duration)
        {
            elapsed += Time.fixedDeltaTime;
            float t     = elapsed / duration;
            // Easing out: сильно на початку, затухає до кінця
            float force = total * (1f - t) * (1f - t) * 6f * Time.fixedDeltaTime;
            rb.AddForce(transform.forward * force, ForceMode.VelocityChange);
            yield return new WaitForFixedUpdate();
        }
    }

    // ════════════════════════════════════════
    //  ШТРАФ ЗА УЗБІЧЧЯ/БАР'ЄР
    // ════════════════════════════════════════
    void ApplyOffTrackPenalty()
    {
        if (offTrackTimer > 0)
            rb.linearVelocity *= 0.94f;
    }

    // ════════════════════════════════════════
    //  ВІЗУАЛ КУЗОВА
    //  Дрифт — сильніший нахил (пункт 8)
    //  Banking дороги через groundNormal (пункт 5)
    // ════════════════════════════════════════
    void UpdateBodyVisual(float turnInput)
    {
        if (carBodyVisual == null) return;

        float speedFactor = Mathf.Clamp01(currentSpeed / stats.maxSpeed);

        // Нахил від рульового
        float steerRoll = -turnInput * bodyRollAmount * speedFactor;

        // Сильніший нахил від дрифту (пункт 8: "більше нахилявся")
        float driftRoll = -(currentDriftAngle / 12f) * bodyRollAmount * 4f;

        // Перенесення ваги
        float weightRollDeg = weightRoll * 200f;

        float targetRoll = steerRoll + driftRoll + weightRollDeg;

        // ── Banking дороги ──
        // Конвертуємо groundNormal в кут відносно кузова
        Vector3 localNormal  = transform.InverseTransformDirection(groundNormal);
        float   bankAngle    = Mathf.Atan2(localNormal.x, localNormal.y) * Mathf.Rad2Deg;
        float   clampedBank  = Mathf.Clamp(bankAngle, -maxBodyBankAngle, maxBodyBankAngle);

        // При більшій швидкості — більше слідуємо за нахилом дороги
        targetRoll += Mathf.Lerp(0f, clampedBank, speedFactor * 0.7f);

        // ── Тремтіння при бусті ──
        float shakeY = 0f;
        if (boosting && boostTimer > 0)
        {
            float shake = Mathf.Sin(Time.time * 28f) * 0.035f * (boostTimer / stats.boostDuration);
            targetRoll += shake * 20f;
            shakeY      = shake;

            if (boostFireLeft  != null && !boostFireLeft.isPlaying)  boostFireLeft.Play();
            if (boostFireRight != null && !boostFireRight.isPlaying) boostFireRight.Play();
        }
        else
        {
            if (boostFireLeft  != null && boostFireLeft.isPlaying)  boostFireLeft.Stop();
            if (boostFireRight != null && boostFireRight.isPlaying) boostFireRight.Stop();
        }

        // Застосовуємо
        Vector3 euler     = carBodyVisual.localEulerAngles;
        float currentRoll = euler.z > 180f ? euler.z - 360f : euler.z;
        float newRoll     = Mathf.Lerp(currentRoll, targetRoll, bodyRollSpeed * Time.fixedDeltaTime);
        carBodyVisual.localEulerAngles = new Vector3(euler.x, euler.y, newRoll);

        Vector3 lp = carBodyVisual.localPosition;
        lp.y = Mathf.Lerp(lp.y, shakeY, 12f * Time.fixedDeltaTime);
        carBodyVisual.localPosition = lp;
    }
    public void OverrideSurface(SurfaceType type)
{
    hasSurfaceOverride = true;
    surfaceOverride    = type;
    ApplySurface(type);
}

public void ClearSurfaceOverride()
{
    hasSurfaceOverride = false;
    ApplySurface(SurfaceType.Asphalt);
}


    // ════════════════════════════════════════
    //  КОЛЕСА
    // ════════════════════════════════════════
    void UpdateWheels(float move, float turn)
    {
        if (frontLeftWheel == null) return;

        float spin = move * wheelSpinSpeed * Time.deltaTime * (boosting ? 1.5f : 1f);
        frontLeftWheel.Rotate(spin,  0, 0);
        frontRightWheel.Rotate(spin, 0, 0);
        rearLeftWheel.Rotate(spin,   0, 0);
        rearRightWheel.Rotate(spin,  0, 0);

        float angle = turn * stats.wheelTurnAngle * (drifting ? 1.3f : 1f);
        frontLeftWheel.localRotation  = Quaternion.Euler(
            frontLeftWheel.localRotation.eulerAngles.x,  angle, 0);
        frontRightWheel.localRotation = Quaternion.Euler(
            frontRightWheel.localRotation.eulerAngles.x, angle, 0);
    }

    // ════════════════════════════════════════
    //  ЕФЕКТИ
    // ════════════════════════════════════════
    void UpdateEffects()
    {
        bool showSmoke = drifting && Mathf.Abs(driftAmount) > 1f && currentSpeed > 8f;

        static void SetSmoke(ParticleSystem ps, bool play)
        {
            if (ps == null) return;
            if (play  && !ps.isPlaying) ps.Play();
            if (!play &&  ps.isPlaying) ps.Stop();
        }

        SetSmoke(leftSmoke,  showSmoke);
        SetSmoke(rightSmoke, showSmoke);

        bool makeSkid = drifting && Mathf.Abs(driftAmount) > 2f && currentSpeed > 10f;
        if (leftSkid  != null) leftSkid.emitting  = makeSkid;
        if (rightSkid != null) rightSkid.emitting = makeSkid;
    }

    // ════════════════════════════════════════
    //  КОЛІЗІЇ
    // ════════════════════════════════════════
    void OnTriggerEnter(Collider other)
    {
        Checkpoint cp = other.GetComponent<Checkpoint>();
        if (cp != null) raceManager?.CheckpointPassed(this, cp.checkpointIndex);

        FinishLine fl = other.GetComponent<FinishLine>();
        if (fl != null) raceManager?.CompleteLap(this);
    }

    void OnCollisionEnter(Collision collision)
    {
        wallTimer = 0.3f;

        if (collision.gameObject.CompareTag("Barrier"))
        {
            offTrackTimer = 2.5f;
            Vector3 pushDir = collision.contacts[0].normal;
            // Ніколи не штовхаємо вниз — запобігає перекиданню носом
            pushDir.y = Mathf.Max(pushDir.y, 0.1f);
            rb.AddForce(pushDir.normalized * 5f, ForceMode.VelocityChange);
        }
    }
}