using UnityEngine;
using System.Collections.Generic;

public class TrackObjectPlacer : MonoBehaviour
{
    [System.Serializable]
    public class TrackObject
    {
        public string    label       = "Object";
        public GameObject prefab;
        [Range(0f, 1f)] public float t          = 0f;  // позиція вздовж треку (0-1)
        public float     sideOffset  = 0f;              // метри вліво(-)/вправо(+) від центру
        public float     heightOffset = 0.5f;           // висота над дорогою
        public float     yRotation   = 0f;              // додатковий поворот
        public bool      alignToTrack = true;           // повернути по напрямку руху
    }

    [Header("Посилання на трек")]
    public TrackSystem trackSystem;

    [Header("Об'єкти для розстановки")]
    public List<TrackObject> objects = new List<TrackObject>();

    // Контейнер для створених об'єктів
    GameObject placedRoot;

    public void PlaceAll()
    {
        if (trackSystem == null || trackSystem.samples == null || trackSystem.samples.Count == 0)
        {
            Debug.LogError("TrackObjectPlacer: Спочатку згенеруй трек (Sample Spline)!");
            return;
        }

        ClearAll();

        if (placedRoot == null)
        {
            Transform existing = transform.Find("PlacedObjects");
            if (existing != null)
                placedRoot = existing.gameObject;
            else
            {
                placedRoot = new GameObject("PlacedObjects");
                placedRoot.transform.SetParent(transform);
                placedRoot.transform.localPosition = Vector3.zero;
            }
        }

        int placed = 0;
        foreach (var obj in objects)
        {
            if (obj.prefab == null)
            {
                Debug.LogWarning($"TrackObjectPlacer: '{obj.label}' — prefab не призначений!");
                continue;
            }

            var sample = GetSampleAtT(obj.t);

            Vector3 pos = sample.position
                        + sample.right  * obj.sideOffset
                        + sample.up     * obj.heightOffset;

            Quaternion baseRot = obj.alignToTrack
                ? Quaternion.LookRotation(sample.forward, sample.up)
                : Quaternion.identity;

            Quaternion rot = baseRot * Quaternion.Euler(0f, obj.yRotation, 0f);

            var instance = Instantiate(obj.prefab, pos, rot, placedRoot.transform);
            instance.name = $"{obj.label}_{obj.t:F2}";
            placed++;
        }

        Debug.Log($"✅ Placed {placed}/{objects.Count} objects along track.");
    }

    public void ClearAll()
    {
        if (placedRoot != null)
        {
            while (placedRoot.transform.childCount > 0)
                DestroyImmediate(placedRoot.transform.GetChild(0).gameObject);
        }
        else
        {
            Transform existing = transform.Find("PlacedObjects");
            if (existing != null)
            {
                placedRoot = existing.gameObject;
                while (placedRoot.transform.childCount > 0)
                    DestroyImmediate(placedRoot.transform.GetChild(0).gameObject);
            }
        }
    }

    TrackSample GetSampleAtT(float t)
    {
        var s = trackSystem.samples;
        int idx = Mathf.RoundToInt(t * (s.Count - 1));
        return s[Mathf.Clamp(idx, 0, s.Count - 1)];
    }
}