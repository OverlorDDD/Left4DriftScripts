using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Collections.Generic;

[RequireComponent(typeof(SplineContainer))]
public class RoadBarriers : MonoBehaviour
{
    [Header("Розміри")]
    public float roadWidth     = 20f;
    public float barrierWidth  = 12f;
    public float barrierHeight = 5f;
    public float barrierAngle  = 40f;

    [Header("Сегменти")]
    public int   segments         = 300;
    public float minSegmentLength = 0.3f;
    public int   sectionCount     = 30;

    [Header("Матеріал")]
    public Material barrierMaterial;

    [Header("Ручне виключення секцій")]
    public int[] hiddenSectionsLeft  = new int[0];
    public int[] hiddenSectionsRight = new int[0];

    Transform leftParent;
    Transform rightParent;

    [ContextMenu("Generate Barriers")]
    public void GenerateBarriers()
    {
        var splineContainer = GetComponent<SplineContainer>();
        Spline spline = splineContainer.Spline;

        Cleanup("BarriersLeft");
        Cleanup("BarriersRight");

        // Окрема вибірка точок для кожної сторони —
        // щоб відфільтрувати перехрестя незалежно
        var ptsLeft  = SampleSplineForSide(spline, -1);
        var ptsRight = SampleSplineForSide(spline,  1);

        leftParent  = CreateSectionedBarrier(ptsLeft,  -1, "BarriersLeft",  hiddenSectionsLeft);
        rightParent = CreateSectionedBarrier(ptsRight,  1, "BarriersRight", hiddenSectionsRight);
    }

    // ═══════════════════════════════════════
    //  Збір точок з фільтрацією перехрестя
    // ═══════════════════════════════════════
    List<(Vector3 pos, Vector3 right)> SampleSplineForSide(Spline spline, int side)
    {
        var result = new List<(Vector3, Vector3)>();
        Vector3 prevForward = Vector3.forward;
        Vector3 prevOuter   = Vector3.one * float.MaxValue;

        float rad      = barrierAngle * Mathf.Deg2Rad;
        float horzStep = Mathf.Cos(rad) * barrierWidth;
        float halfRoad = roadWidth * 0.5f;

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            spline.Evaluate(t, out float3 pos, out float3 tangent, out float3 _);

            Vector3 localPos = transform.InverseTransformPoint((Vector3)pos);
            Vector3 fwd      = ((Vector3)math.normalizesafe(tangent));
            Vector3 right    = Vector3.Cross(Vector3.up, fwd).normalized;

            // Мінімальна дистанція
            if (result.Count > 0)
            {
                var (lastPos, _) = result[result.Count - 1];
                if (Vector3.Distance(localPos, lastPos) < minSegmentLength) continue;
            }

            Vector3 inner = localPos + right * side * halfRoad;
            Vector3 outer = inner + right * side * horzStep;

            if (result.Count > 1)
            {
                // Перевірка 1: різкий розворот сплайна
                float dotFwd = Vector3.Dot(fwd.normalized, prevForward.normalized);
                if (dotFwd < 0.15f)
                {
                    prevForward = fwd;
                    prevOuter   = outer;
                    continue;
                }

                // Перевірка 2: outer точка зайшла назад — це і є перехрестя
                Vector3 outerDelta = outer - prevOuter;
                float   dotOuter   = Vector3.Dot(outerDelta, prevForward);
                if (dotOuter < 0f)
                {
                    prevForward = fwd;
                    prevOuter   = outer;
                    continue;
                }
            }

            prevForward = fwd;
            prevOuter   = outer;
            result.Add((localPos, right));
        }

        return result;
    }

    // ═══════════════════════════════════════
    //  Секціонований бар'єр
    // ═══════════════════════════════════════
    Transform CreateSectionedBarrier(
        List<(Vector3 pos, Vector3 right)> pts,
        int side, string parentName, int[] hidden)
    {
        GameObject parent = new GameObject(parentName);
        parent.transform.SetParent(transform);
        parent.transform.localPosition = Vector3.zero;
        parent.transform.localRotation = Quaternion.identity;

        var hiddenSet = new HashSet<int>(hidden);

        int total      = pts.Count - 1;
        int ptsPerSec  = Mathf.Max(2, total / sectionCount);

        float rad      = barrierAngle * Mathf.Deg2Rad;
        float horzStep = Mathf.Cos(rad) * barrierWidth;
        float vertStep = Mathf.Sin(rad) * barrierHeight;
        float halfRoad = roadWidth * 0.5f;

        for (int sec = 0; sec < sectionCount; sec++)
        {
            int startIdx = sec * ptsPerSec;
            int endIdx   = (sec == sectionCount - 1)
                ? pts.Count - 1
                : Mathf.Min(startIdx + ptsPerSec, pts.Count - 1);

            if (hiddenSet.Contains(sec))
            {
                var ph = new GameObject($"Section_{sec}_HIDDEN");
                ph.transform.SetParent(parent.transform);
                continue;
            }

            int ptCount = endIdx - startIdx + 1;
            if (ptCount < 2) continue;

            Vector3[] verts = new Vector3[ptCount * 2];
            Vector2[] uvs   = new Vector2[ptCount * 2];
            int[]     tris  = new int[(ptCount - 1) * 6];

            float   cumLen = 0f;
            Vector3 prevI  = Vector3.zero;

            for (int i = 0; i < ptCount; i++)
            {
                var (pos, right) = pts[startIdx + i];
                Vector3 inner = pos + right * side * halfRoad;
                Vector3 outer = inner
                    + right * side * horzStep
                    + Vector3.up  * vertStep;

                verts[i * 2]     = inner;
                verts[i * 2 + 1] = outer;

                if (i > 0) cumLen += Vector3.Distance(inner, prevI);
                float uvY = cumLen * 0.1f;
                uvs[i * 2]     = new Vector2(0, uvY);
                uvs[i * 2 + 1] = new Vector2(1, uvY);
                prevI = inner;
            }

            for (int i = 0; i < ptCount - 1; i++)
            {
                int vi = i * 2, ti = i * 6;
                if (side == -1)
                {
                    tris[ti]   = vi;   tris[ti+1] = vi+1; tris[ti+2] = vi+2;
                    tris[ti+3] = vi+1; tris[ti+4] = vi+3; tris[ti+5] = vi+2;
                }
                else
                {
                    tris[ti]   = vi;   tris[ti+1] = vi+2; tris[ti+2] = vi+1;
                    tris[ti+3] = vi+1; tris[ti+4] = vi+2; tris[ti+5] = vi+3;
                }
            }

            Mesh mesh = new Mesh();
            mesh.name      = $"BarrierMesh_{sec}";
            mesh.vertices  = verts;
            mesh.uv        = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject secObj = new GameObject($"Section_{sec}");
            secObj.transform.SetParent(parent.transform);
            secObj.transform.localPosition = Vector3.zero;
            secObj.transform.localRotation = Quaternion.identity;
            secObj.tag = "Barrier";

            secObj.AddComponent<MeshFilter>().sharedMesh       = mesh;
            secObj.AddComponent<MeshRenderer>().sharedMaterial =
                barrierMaterial ?? new Material(Shader.Find("Standard"));
            secObj.AddComponent<MeshCollider>().sharedMesh     = mesh;
        }
        CreateEdgeWall(pts, side, parent.transform);


        return parent.transform;
    }

    void CreateEdgeWall(
    List<(Vector3 pos, Vector3 right)> pts,
    int side, Transform parent)
{
    if (pts.Count < 2) return;

    float rad      = barrierAngle * Mathf.Deg2Rad;
    float horzStep = Mathf.Cos(rad) * barrierWidth;
    float vertStep = Mathf.Sin(rad) * barrierHeight;
    float halfRoad = roadWidth * 0.5f;
    float wallH    = 4f;

    // ── Фільтруємо точки так само як в SampleSplineForSide ──
    // але тепер перевіряємо позицію edgeBase а не outer
    var filtered    = new List<Vector3>();
    Vector3 prevEdge    = Vector3.one * float.MaxValue;
    Vector3 prevForward = Vector3.forward;

    for (int i = 0; i < pts.Count; i++)
    {
        var (pos, right) = pts[i];

        Vector3 inner    = pos + right * side * halfRoad;
        Vector3 edgeBase = inner
            + right * side * horzStep
            + Vector3.up * vertStep;

        // Напрямок від попередньої edge точки до поточної
        if (filtered.Count > 1)
        {
            Vector3 edgeDelta = edgeBase - prevEdge;

            // Якщо edge точка рухається назад — пропускаємо
            if (Vector3.Dot(edgeDelta, prevForward) < 0f)
            {
                // Оновлюємо forward але не додаємо точку
                if (i < pts.Count - 1)
                {
                    var (nextPos, nextRight) = pts[i + 1];
                    prevForward = (nextPos - pos).normalized;
                }
                continue;
            }
        }

        // Оновлюємо напрямок через різницю між центральними точками
        if (filtered.Count > 0 && i > 0)
        {
            var (prevPos, _) = pts[i - 1];
            Vector3 centerDelta = pos - prevPos;
            if (centerDelta != Vector3.zero)
                prevForward = centerDelta.normalized;
        }

        prevEdge = edgeBase;
        filtered.Add(edgeBase);
    }

    if (filtered.Count < 2) return;

    // ── Будуємо меш з відфільтрованих точок ──
    Vector3[] verts = new Vector3[filtered.Count * 2];
    int[]     tris  = new int[(filtered.Count - 1) * 12];

    for (int i = 0; i < filtered.Count; i++)
    {
        verts[i * 2]     = filtered[i];
        verts[i * 2 + 1] = filtered[i] + Vector3.up * wallH;
    }

    for (int i = 0; i < filtered.Count - 1; i++)
    {
        int vi = i * 2;
        int ti = i * 12;

        tris[ti]    = vi;   tris[ti+1] = vi+1; tris[ti+2] = vi+2;
        tris[ti+3]  = vi+1; tris[ti+4] = vi+3; tris[ti+5] = vi+2;

        tris[ti+6]  = vi;   tris[ti+7] = vi+2; tris[ti+8]  = vi+1;
        tris[ti+9]  = vi+1; tris[ti+10]= vi+2; tris[ti+11] = vi+3;
    }

    Mesh mesh = new Mesh();
    mesh.name      = "EdgeWallMesh";
    mesh.vertices  = verts;
    mesh.triangles = tris;
    mesh.RecalculateNormals();
    mesh.RecalculateBounds();

    GameObject wall = new GameObject("EdgeWall");
    wall.transform.SetParent(parent);
    wall.transform.localPosition = Vector3.zero;
    wall.transform.localRotation = Quaternion.identity;
    wall.tag = "Barrier";

    wall.AddComponent<MeshCollider>().sharedMesh = mesh;
}

    void Cleanup(string n)
    {
        Transform t = transform.Find(n);
        if (t != null) DestroyImmediate(t.gameObject);
    }
}