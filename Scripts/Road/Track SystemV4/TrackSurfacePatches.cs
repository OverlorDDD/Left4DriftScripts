using UnityEngine;
using System.Collections.Generic;

public class TrackSurfacePatches : MonoBehaviour
{
    [System.Serializable]
    public class SurfacePatch
    {
        public string label = "Puddle";
        [Range(0f, 1f)] public float centerT   = 0.5f;
        public float   length      = 5f;
        public float   width       = 3f;
        public float   sideOffset  = 0f;
        public SurfaceType surface = SurfaceType.Water;

        [Header("Сила ефекту (-1 = використати значення за замовчуванням для типу)")]
        [Tooltip("0 = машина ковзає як по льоду, 1 = нормальне зчеплення")]
        public float gripOverride  = -1f;
        [Tooltip("Множник максимальної швидкості на цій ділянці")]
        public float speedOverride = -1f;
        [Tooltip("Сила поштовху вперед — тільки для Boost")]
        public float boostForceOverride = -1f;

        [Header("Візуал (необов'язково — інакше стандартний колір)")]
        public Material customMaterial;
    }

    [Header("Джерело даних треку")]
    public TrackSystem trackSystem;

    [Header("Список плям")]
    public List<SurfacePatch> patches = new List<SurfacePatch>();

    [Header("Прозорість плями за замовчуванням")]
    [Range(0.3f, 1f)] public float defaultAlpha = 0.8f;

    GameObject patchRoot;

    // ════════════════════════════════════════
    public void GeneratePatches()
    {
        if (trackSystem == null)
            trackSystem = FindAnyObjectByType<TrackSystem>();

        if (trackSystem == null || trackSystem.samples == null || trackSystem.samples.Count == 0)
        {
            Debug.LogError("TrackSurfacePatches: TrackSystem не знайдений або не згенерований.");
            return;
        }

        EnsureRoot();
        ClearChildren();

        var samples = trackSystem.samples;

        foreach (var patch in patches)
        {
            int idx = Mathf.Clamp(
                Mathf.RoundToInt(patch.centerT * (samples.Count - 1)), 0, samples.Count - 1);
            var sample = samples[idx];

            Vector3 center = sample.position
                           + sample.right * patch.sideOffset
                           + sample.up * 0.015f;

            var go = new GameObject($"Patch_{patch.label}_{patch.surface}");
            go.transform.SetParent(patchRoot.transform);
            go.transform.position = center;
            go.transform.rotation = Quaternion.LookRotation(sample.forward, sample.up);

            BuildVisual(go, patch);
            BuildCollider(go, patch);
        }

        Debug.Log($"✅ Створено {patches.Count} плям поверхонь.");
    }

    void BuildVisual(GameObject go, SurfacePatch patch)
    {
        float hw = patch.width  * 0.5f;
        float hl = patch.length * 0.5f;

        var mesh = new Mesh();
        mesh.vertices = new Vector3[]
        {
            new(-hw, 0, -hl), new(hw, 0, -hl), new(hw, 0, hl), new(-hw, 0, hl)
        };
        mesh.uv        = new Vector2[] { new(0, 0), new(1, 0), new(1, 1), new(0, 1) };
        mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();

        Material mat;
        if (patch.customMaterial != null)
        {
            mat = patch.customMaterial;
        }
        else
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            Color c = GetDefaultColor(patch.surface);
            c.a = defaultAlpha;
            mat.SetColor("_BaseColor", c);
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        mr.sharedMaterial = mat;
    }

    void BuildCollider(GameObject go, SurfacePatch patch)
    {
        var bc = go.AddComponent<BoxCollider>();
        bc.size   = new Vector3(patch.width, 0.06f, patch.length);
        bc.center = new Vector3(0, 0.03f, 0);

        var trig = go.AddComponent<TrackSurfaceTrigger>();
        trig.surfaceType     = patch.surface;
        trig.useCustomValues = true;

        trig.customGrip  = patch.gripOverride  > 0f ? patch.gripOverride  : GetDefaultGrip(patch.surface);
        trig.customSpeed = patch.speedOverride > 0f ? patch.speedOverride : GetDefaultSpeed(patch.surface);
        trig.customBoostForce = patch.boostForceOverride > 0f
            ? patch.boostForceOverride
            : GetDefaultBoostForce(patch.surface);
        trig.customSlippery = patch.surface == SurfaceType.Ice || patch.surface == SurfaceType.Snow;
    }

    // ── Значення за замовчуванням для кожного типу ──
    float GetDefaultGrip(SurfaceType type) => type switch
    {
        SurfaceType.Ice   => 0.05f,  // майже нульове зчеплення — машина ковзає як по льоду
        SurfaceType.Snow  => 0.35f,
        SurfaceType.Mud   => 0.35f,  // сильно гальмує керованість
        SurfaceType.Grass => 0.65f,  // легке гальмування
        SurfaceType.Sand  => 0.5f,
        SurfaceType.Water => 0.4f,
        _                 => 1f
    };

    float GetDefaultSpeed(SurfaceType type) => type switch
    {
        SurfaceType.Ice   => 1.0f,   // не гальмує швидкість, тільки зчеплення
        SurfaceType.Snow  => 0.75f,
        SurfaceType.Mud   => 0.45f,  // помітно сповільнює
        SurfaceType.Grass => 0.8f,
        SurfaceType.Sand  => 0.6f,
        SurfaceType.Water => 0.55f,
        SurfaceType.Boost => 1.3f,
        _                 => 1f
    };

    float GetDefaultBoostForce(SurfaceType type) =>
        type == SurfaceType.Boost ? 25f : 0f; // було занадто слабко — тепер відчутний поштовх

    Color GetDefaultColor(SurfaceType type) => type switch
    {
        SurfaceType.Water => new Color(0.15f, 0.55f, 0.9f),
        SurfaceType.Mud   => new Color(0.35f, 0.22f, 0.08f),
        SurfaceType.Ice   => new Color(0.75f, 0.92f, 1f),
        SurfaceType.Snow  => new Color(0.95f, 0.95f, 1f),
        SurfaceType.Sand  => new Color(0.85f, 0.75f, 0.45f),
        SurfaceType.Grass => new Color(0.25f, 0.6f, 0.2f),
        SurfaceType.Dirt  => new Color(0.45f, 0.32f, 0.18f),
        SurfaceType.Boost => new Color(1f, 0.6f, 0f),
        _                 => Color.magenta
    };

    public void ClearPatches()
    {
        ClearChildren();
    }

    void EnsureRoot()
    {
        if (patchRoot != null) return;
        Transform found = transform.Find("GeneratedPatches");
        if (found != null) { patchRoot = found.gameObject; return; }

        patchRoot = new GameObject("GeneratedPatches");
        patchRoot.transform.SetParent(transform);
        patchRoot.transform.localPosition = Vector3.zero;
    }

    void ClearChildren()
    {
        EnsureRoot();
        while (patchRoot.transform.childCount > 0)
            DestroyImmediate(patchRoot.transform.GetChild(0).gameObject);
    }
}