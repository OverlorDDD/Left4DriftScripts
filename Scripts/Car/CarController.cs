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

    [Header("Прив'язка до сплайну (автоматично знаходиться якщо порожньо)")]
    public SplineContainer trackSpline;

    [Header("Підвіска (пружина-демпфер на 4 кутах)")]
    public float suspensionRestLength = 0.5f;
    public float suspensionSpring     = 120f;
    public float suspensionDamper     = 12f;
    public float wheelRadius          = 0.35f;

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
    bool  canMove = true;
    bool  drifting;
    bool  isReversing;
    bool  grounded;
    float currentSpeed;
    float driftAmount;
    float currentDriftAngle;
    float weightRoll;

    // ── Стан бусту ──
    bool  boosting;
    bool  boostReady;
    float shiftHoldTime;
    float boostTimer;

    // ── Таймери ──
    float offTrackTimer;
    float wallTimer;

    // ── Поверхня ──
    SurfaceType currentSurface     = SurfaceType.Asphalt;
    float       surfaceGripMult    = 1f;
    float       surfaceSpeedMult   = 1f;
    bool        surfaceIsSlippery  = false;
    SurfaceType surfaceOverride    = SurfaceType.Asphalt;
    bool        hasSurfaceOverride = false;

    // ── Земля (з підвіски) ──
    Vector3 groundNormal = Vector3.up;
    float[] prevCompression = new float[4];

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

        // Авто-пошук сплайну — не треба вручну перетягувати в префаб
        if (trackSpline == null)
            trackSpline = FindAnyObjectByType<SplineContainer>();

        // Нормальний центр мас — трохи нижче геометричного центру
        rb.centerOfMass           = new Vector3(0f, -0.3f, 0f);
        rb.linearDamping          = 0.02f;
        rb.angularDamping         = 1.5f;
        rb.interpolation          = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.maxAngularVelocity     = 10f;

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

        // ── Підвіска: земля + гравітація + вирівнювання — все в одному місці ──
        ApplySuspension();

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
    //  ПІДВІСКА — ключовий фікс тряски
    //  4 рейкасти по кутах машини, кожен штовхає силою
    //  через AddForceAtPosition. Ніякого rb.position!
    //  Це природньо дає і поштовх вгору, і вирівнюючий торк одночасно.
    // ════════════════════════════════════════
    void ApplySuspension()
    {
        Vector3[] points =
        {
            transform.position + transform.right *  0.6f + transform.forward *  0.9f,
            transform.position - transform.right *  0.6f + transform.forward *  0.9f,
            transform.position + transform.right *  0.6f - transform.forward *  0.8f,
            transform.position - transform.right *  0.6f - transform.forward *  0.8f,
        };

        int     hitCount  = 0;
        Vector3 normalSum = Vector3.zero;
        SurfaceType detected = SurfaceType.Asphalt;

        float maxRayDist = suspensionRestLength + wheelRadius + suspensionRestLength;

        for (int i = 0; i < 4; i++)
        {
            Vector3 origin = points[i] + transform.up * (suspensionRestLength * 0.5f);

            if (Physics.Raycast(origin, -transform.up, out RaycastHit hit, maxRayDist))
            {
                float currentLength = hit.distance - wheelRadius;
                float compression   = Mathf.Clamp01(1f - currentLength / suspensionRestLength);

                float compressionVel = (compression - prevCompression[i]) / Time.fixedDeltaTime;
                prevCompression[i]   = compression;

                float springForce = compression * suspensionSpring;
                float damperForce = compressionVel * suspensionDamper;
                float totalForce  = Mathf.Max(0f, springForce + damperForce);

                // Сила саме в точці контакту — дає і підйом, і вирівнювання одночасно
                rb.AddForceAtPosition(hit.normal * totalForce, points[i], ForceMode.Force);

                hitCount++;
                normalSum += hit.normal;

                var trig = hit.collider.GetComponent<TrackSurfaceTrigger>();
                if (trig != null) detected = trig.surfaceType;
            }
            else
            {
                prevCompression[i] = 0f;
            }
        }

        grounded     = hitCount >= 2;
        groundNormal = grounded ? (normalSum / hitCount).normalized : Vector3.up;

        if (grounded && !hasSurfaceOverride) ApplySurface(detected);

        // В повітрі — звичайна гравітація (підвіска сама тримає на землі)
        if (!grounded)
        {
            rb.AddForce(Vector3.down * stats.extraGravity, ForceMode.Acceleration);

            // Дуже слабке гасіння обертання в польоті — не заважає красивому падінню
            Vector3 av = transform.InverseTransformDirection(rb.angularVelocity);
            av.x = Mathf.Lerp(av.x, 0f, 0.04f);
            av.z = Mathf.Lerp(av.z, 0f, 0.04f);
            rb.angularVelocity = transform.TransformDirection(av);
        }
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
                rb.AddForce(-rb.linearVelocity.normalized * stats.brakingForce,
                            ForceMode.Acceleration);
            }
            else
            {
                float sf = Mathf.Clamp01(1f - currentSpeed / stats.reverseMaxSpeed);
                rb.AddForce(-transform.forward * stats.reverseAcceleration * sf,
                            ForceMode.Acceleration);
            }
        }
        else
        {
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, stats.engineBraking);
        }

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
            float slide = Mathf.Clamp01(Mathf.Abs(localVel.x) / 15f);
            targetGrip  = Mathf.Lerp(stats.driftGripMax, stats.driftGripMin, slide);
        }
        else
        {
            targetGrip = stats.normalGrip * surfaceGripMult;
        }

        if (surfaceIsSlippery)
            targetGrip *= 0.25f;

        float desired = localVel.x * targetGrip;
        localVel.x = Mathf.MoveTowards(localVel.x, desired, 18f * Time.fixedDeltaTime);
        rb.linearVelocity = transform.TransformDirection(localVel);

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

        Vector3 targetVel = transform.forward * currentSpeed;
        float   factor    = drifting ? stats.driftTraction * dt : stats.traction * dt;

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

                StartCoroutine(SmoothBoostLaunch());
            }
            shiftHoldTime = 0f;
        }
    }

    IEnumerator SmoothBoostLaunch()
    {
        float elapsed  = 0f;
        float duration = 0.3f;
        float total    = stats.boostLaunchImpulse;

        while (elapsed < duration)
        {
            elapsed += Time.fixedDeltaTime;
            float t     = elapsed / duration;
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
    // ════════════════════════════════════════
    void UpdateBodyVisual(float turnInput)
    {
        if (carBodyVisual == null) return;

        float speedFactor = Mathf.Clamp01(currentSpeed / stats.maxSpeed);

        float steerRoll     = -turnInput * bodyRollAmount * speedFactor;
        float driftRoll     = -(currentDriftAngle / 12f) * bodyRollAmount * 4f;
        float weightRollDeg = weightRoll * 200f;

        float targetRoll = steerRoll + driftRoll + weightRollDeg;

        Vector3 localNormal = transform.InverseTransformDirection(groundNormal);
        float   bankAngle   = Mathf.Atan2(localNormal.x, localNormal.y) * Mathf.Rad2Deg;
        float   clampedBank = Mathf.Clamp(bankAngle, -maxBodyBankAngle, maxBodyBankAngle);

        targetRoll += Mathf.Lerp(0f, clampedBank, speedFactor * 0.7f);

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

        Vector3 euler     = carBodyVisual.localEulerAngles;
        float currentRoll = euler.z > 180f ? euler.z - 360f : euler.z;
        float newRoll     = Mathf.Lerp(currentRoll, targetRoll, bodyRollSpeed * Time.fixedDeltaTime);
        carBodyVisual.localEulerAngles = new Vector3(euler.x, euler.y, newRoll);

        Vector3 lp = carBodyVisual.localPosition;
        lp.y = Mathf.Lerp(lp.y, shakeY, 12f * Time.fixedDeltaTime);
        carBodyVisual.localPosition = lp;
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
            pushDir.y = Mathf.Max(pushDir.y, 0.1f);
            rb.AddForce(pushDir.normalized * 5f, ForceMode.VelocityChange);
        }
    }
}