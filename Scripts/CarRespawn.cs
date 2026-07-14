using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class CarRespawn : MonoBehaviour
{
    [Header("Posилання")]
    public SplineContainer trackSpline;

    [Header("Висота відновлення над трасою")]
    public float respawnHeightOffset = 1.5f;

    [Header("Мерехтіння")]
    public float flickerDuration  = 1.5f;
    public float flickerSpeed     = 8f;

    CarController carController;
    Rigidbody     rb;
    List<Renderer> renderers = new List<Renderer>();
    bool isRespawning = false;

    void Start()
    {
        carController = GetComponent<CarController>();
        rb            = GetComponent<Rigidbody>();

        // Збираємо всі рендерери авто
        renderers.AddRange(GetComponentsInChildren<Renderer>());
    }

    void Update()
    {
        if (isRespawning) return;

        bool respawnPressed =
            (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ||
            (Gamepad.current  != null && Gamepad.current.buttonNorth.wasPressedThisFrame); // Y

        if (respawnPressed)
            StartCoroutine(DoRespawn());
    }

    public void TriggerRespawn() => StartCoroutine(DoRespawn());

    IEnumerator DoRespawn()
    {
        isRespawning = true;
        carController?.SetCanMove(false);

        // ── Знаходимо найближчу точку сплайну ──
        Vector3 bestPos     = transform.position;
        Vector3 bestForward = transform.forward;
        float   bestDist    = float.MaxValue;

        var spline   = trackSpline.Spline;
        int steps    = 200;

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            spline.Evaluate(t, out float3 pt, out float3 fwd, out float3 _);

            float dist = Vector3.SqrMagnitude(transform.position - (Vector3)pt);
            if (dist < bestDist)
            {
                bestDist    = dist;
                bestPos     = (Vector3)pt;
                bestForward = ((Vector3)fwd).normalized;
            }
        }

        // ── Мерехтіння opacity ──
        yield return StartCoroutine(Flicker());

        // ── Телепортація ──
        rb.linearVelocity  = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        transform.position = bestPos + Vector3.up * respawnHeightOffset;
        transform.rotation = Quaternion.LookRotation(bestForward, Vector3.up);

        // ── Відновлення ──
        yield return new WaitForSeconds(0.3f);
        carController?.SetCanMove(true);
        isRespawning = false;
    }

    IEnumerator Flicker()
    {
        float elapsed = 0f;
        while (elapsed < flickerDuration)
        {
            elapsed += Time.deltaTime;
            float t       = elapsed / flickerDuration;
            float alpha   = Mathf.Abs(Mathf.Sin(elapsed * flickerSpeed * Mathf.PI));
            // Затухає: на початку миготить швидко, до кінця стабілізується
            float finalA  = Mathf.Lerp(alpha, 1f, t);

            foreach (var r in renderers)
            {
                foreach (var mat in r.materials)
                {
                    if (mat.HasProperty("_BaseColor"))
                    {
                        Color c = mat.GetColor("_BaseColor");
                        c.a = finalA;
                        mat.SetColor("_BaseColor", c);
                    }
                }
            }
            yield return null;
        }

        // Повертаємо повну непрозорість
        foreach (var r in renderers)
            foreach (var mat in r.materials)
                if (mat.HasProperty("_BaseColor"))
                {
                    Color c = mat.GetColor("_BaseColor");
                    c.a = 1f;
                    mat.SetColor("_BaseColor", c);
                }
    }
}