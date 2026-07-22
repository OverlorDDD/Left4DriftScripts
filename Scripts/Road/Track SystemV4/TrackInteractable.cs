using UnityEngine;

public enum TrackObjectType
{
    Decoration,  // без ефекту
    BoostRing,   // буст при проїзді
    Coin,        // монета, дає очки
    Banana,      // слизько, як лід
    MudPatch,    // уповільнює
    BreakableWall, // розбивається при ударі
    Ramp,        // трамплін
    SpeedStrip,  // смуга швидкості
}

public class TrackInteractable : MonoBehaviour
{
    [Header("Тип об'єкта")]
    public TrackObjectType objectType = TrackObjectType.Decoration;

    [Header("Ефект")]
    public float effectStrength    = 1f;    // сила ефекту (множник)
    public float effectDuration    = 2f;    // тривалість
    public bool  oneTimeUse        = true;  // зникає після першого зіткнення
    public float respawnDelay      = 5f;    // час до відновлення (0 = не відновлюється)

    [Header("Розбивається")]
    public float breakableHealth   = 1f;
    public float fadeOutDuration   = 0.8f;

    [Header("Візуал")]
    public ParticleSystem collectEffect;
    public AudioClip      collectSound;

    bool isActive = true;
    AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!isActive) return;

        CarController car = other.GetComponentInParent<CarController>();
        if (car == null) return;

        ApplyEffect(car);

        if (collectEffect != null)
            collectEffect.Play();
        if (collectSound != null && audioSource != null)
            audioSource.PlayOneShot(collectSound);

        if (oneTimeUse)
        {
            isActive = false;
            if (respawnDelay > 0f)
                StartCoroutine(RespawnAfter(respawnDelay));
            else
                StartCoroutine(FadeAndDestroy());
        }
    }

    void OnCollisionEnter(Collision col)
    {
        if (!isActive) return;
        if (objectType != TrackObjectType.BreakableWall) return;

        CarController car = col.gameObject.GetComponentInParent<CarController>();
        if (car == null) return;

        breakableHealth -= car.GetCurrentSpeed() * 0.1f;
        if (breakableHealth <= 0f)
        {
            isActive = false;
            StartCoroutine(FadeAndDestroy());
        }
    }

    void ApplyEffect(CarController car)
    {
        switch (objectType)
        {
            case TrackObjectType.BoostRing:
                // Надсилаємо імпульс вперед
                var rb = car.GetComponent<Rigidbody>();
                if (rb != null)
                    rb.AddForce(car.transform.forward * 15f * effectStrength,
                        ForceMode.VelocityChange);
                break;

            case TrackObjectType.Coin:
                // Знайди свій GameManager або RaceManager
                Debug.Log($"Coin collected by {car.name}!");
                break;

            case TrackObjectType.Banana:
                var slipEffect = car.GetComponent<CarSurfaceEffect>();
                if (slipEffect != null) slipEffect.ApplyTemporarySurface(SurfaceType.Ice, effectDuration);
                break;

            case TrackObjectType.MudPatch:
                var mudEffect = car.GetComponent<CarSurfaceEffect>();
                if (mudEffect != null) mudEffect.ApplyTemporarySurface(SurfaceType.Mud, effectDuration);
                break;

            case TrackObjectType.SpeedStrip:
                var rb2 = car.GetComponent<Rigidbody>();
                if (rb2 != null)
                    rb2.AddForce(car.transform.forward * 8f * effectStrength,
                        ForceMode.VelocityChange);
                break;
        }
    }

    System.Collections.IEnumerator FadeAndDestroy()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        float elapsed = 0f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - elapsed / fadeOutDuration;
            // Мерехтіння перед зникненням
            float flicker = Mathf.Abs(Mathf.Sin(elapsed * 20f)) * alpha;
            foreach (var r in renderers)
                foreach (var mat in r.materials)
                    if (mat.HasProperty("_BaseColor"))
                    {
                        Color c = mat.GetColor("_BaseColor"); c.a = flicker;
                        mat.SetColor("_BaseColor", c);
                    }
            yield return null;
        }
        gameObject.SetActive(false);
    }

    System.Collections.IEnumerator RespawnAfter(float delay)
    {
        // Ховаємо
        foreach (var r in GetComponentsInChildren<Renderer>())
            r.enabled = false;
        foreach (var c in GetComponents<Collider>())
            c.enabled = false;

        yield return new WaitForSeconds(delay);

        // Показуємо
        foreach (var r in GetComponentsInChildren<Renderer>())
            r.enabled = true;
        foreach (var c in GetComponents<Collider>())
            c.enabled = true;
        isActive = true;
    }
}