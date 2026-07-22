using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class CarRespawn : MonoBehaviour
{
    [Header("Джерела позиції (автоматично знаходяться)")]
    public TrackSystem      trackSystem;   // пріоритетне джерело — враховує висоту/banking
    public SplineContainer  trackSpline;   // фолбек якщо TrackSystem не знайдено

    [Header("Позиція відновлення")]
    public float respawnHeightOffset = 0.5f;
    [Tooltip("Наскільки далі вперед по треку (0-1) відносно найближчої точки")]
    public float aheadOffsetT = 0.004f;

    [Header("Мерехтіння (тільки візуальний ефект ПІСЛЯ телепорту)")]
    public float flickerDuration = 0.6f;
    public float flickerSpeed    = 10f;

    CarController  carController;
    Rigidbody      rb;
    List<Renderer> renderers = new List<Renderer>();
    bool isRespawning = false;

    void Start()
    {
        carController = GetComponent<CarController>();
        rb            = GetComponent<Rigidbody>();

        if (trackSystem == null)
            trackSystem = FindAnyObjectByType<TrackSystem>();
        if (trackSpline == null)
            trackSpline = FindAnyObjectByType<SplineContainer>();

        renderers.AddRange(GetComponentsInChildren<Renderer>());
    }

    void Update()
    {
        if (isRespawning) return;

        bool respawnPressed =
            (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ||
            (Gamepad.current  != null && Gamepad.current.buttonNorth.wasPressedThisFrame);

        if (respawnPressed)
            TriggerRespawn();
    }

    public void TriggerRespawn()
    {
        if (isRespawning) return;

        if (trackSystem == null && trackSpline == null)
        {
            Debug.LogWarning("CarRespawn: не знайдено ні TrackSystem, ні SplineContainer.");
            return;
        }

        StartCoroutine(DoRespawn());
    }

    IEnumerator DoRespawn()
    {
        isRespawning = true;
        carController?.SetCanMove(false);

        Vector3 respawnPos;
        Vector3 respawnFwd;
        Vector3 respawnUp;

        // ── Пріоритет: TrackSystem.samples (враховує висоту і banking) ──
        if (trackSystem != null && trackSystem.samples != null && trackSystem.samples.Count > 1)
        {
            var samples = trackSystem.samples;

            int   bestIdx  = 0;
            float bestDist = float.MaxValue;

            for (int i = 0; i < samples.Count; i++)
            {
                float dist = Vector3.SqrMagnitude(transform.position - samples[i].position);
                if (dist < bestDist) { bestDist = dist; bestIdx = i; }
            }

            int aheadCount = Mathf.RoundToInt(aheadOffsetT * samples.Count);
            int targetIdx  = (bestIdx + aheadCount) % samples.Count;

            var s = samples[targetIdx];
            respawnPos = s.position;
            respawnFwd = s.forward;
            respawnUp  = s.up;
        }
        else
        {
            // ── Фолбек: сирий сплайн ──
            var spline   = trackSpline.Spline;
            float bestT  = 0f, bestDist = float.MaxValue;

            for (int i = 0; i <= 200; i++)
            {
                float t = (float)i / 200;
                spline.Evaluate(t, out Unity.Mathematics.float3 pt, out Unity.Mathematics.float3 _, out Unity.Mathematics.float3 _);
                float dist = Vector3.SqrMagnitude(transform.position - (Vector3)pt);
                if (dist < bestDist) { bestDist = dist; bestT = t; }
            }

            float targetT = Mathf.Repeat(bestT + aheadOffsetT, 1f);
            spline.Evaluate(targetT, out Unity.Mathematics.float3 fPos, out Unity.Mathematics.float3 fFwd, out Unity.Mathematics.float3 _);
            respawnPos = (Vector3)fPos;
            respawnFwd = ((Vector3)fFwd).normalized;
            respawnUp  = Vector3.up;
        }

        // ── Миттєвий телепорт — без затримки, гравець одразу відчуває реакцію ──
        rb.linearVelocity  = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        transform.position = respawnPos + respawnUp * respawnHeightOffset;
        transform.rotation = Quaternion.LookRotation(respawnFwd, respawnUp);

        // ── Коротке мерехтіння ПІСЛЯ телепорту — суто косметика ──
        yield return StartCoroutine(Flicker());

        carController?.SetCanMove(true);
        isRespawning = false;
    }

    IEnumerator Flicker()
    {
        float elapsed = 0f;
        while (elapsed < flickerDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(
                Mathf.Abs(Mathf.Sin(elapsed * flickerSpeed * Mathf.PI)),
                1f, elapsed / flickerDuration);

            SetAlpha(alpha);
            yield return null;
        }
        SetAlpha(1f);
    }

    void SetAlpha(float a)
    {
        foreach (var r in renderers)
        {
            if (r == null) continue;
            foreach (var mat in r.materials)
                if (mat.HasProperty("_BaseColor"))
                {
                    Color c = mat.GetColor("_BaseColor");
                    c.a = a;
                    mat.SetColor("_BaseColor", c);
                }
        }
    }
}