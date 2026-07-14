using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[RequireComponent(typeof(SplineContainer))]
public class RoadMeshGenerator : MonoBehaviour
{
    public float roadWidth = 8f;
    public int segmentsPerUnit = 2; // щільність мешу

    SplineContainer splineContainer;

    [ContextMenu("Generate Road")]
    void GenerateRoad()
    {
        splineContainer = GetComponent<SplineContainer>();
        Spline spline = splineContainer.Spline;

        float length = spline.GetLength();
        int segments = Mathf.Max(2, Mathf.RoundToInt(length * segmentsPerUnit));

        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[segments * 6];

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;

            // Позиція і напрямок на сплайні
            spline.Evaluate(t, out float3 pos, out float3 tangent, out float3 up);

            Vector3 forward = ((Vector3)tangent).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            Vector3 worldPos = transform.InverseTransformPoint(pos);

            // Ліва і права точки дороги
            vertices[i * 2]     = worldPos - right * roadWidth * 0.5f;
            vertices[i * 2 + 1] = worldPos + right * roadWidth * 0.5f;

            uvs[i * 2]     = new Vector2(0, t * length * 0.25f);
            uvs[i * 2 + 1] = new Vector2(1, t * length * 0.25f);
        }

        // Трикутники
        for (int i = 0; i < segments; i++)
        {
            int vi = i * 2;
            int ti = i * 6;

            triangles[ti]     = vi;
            triangles[ti + 1] = vi + 2;
            triangles[ti + 2] = vi + 1;

            triangles[ti + 3] = vi + 1;
            triangles[ti + 4] = vi + 2;
            triangles[ti + 5] = vi + 3;
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var mf = GetComponent<MeshFilter>();
        if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        var mr = GetComponent<MeshRenderer>();
        if (mr == null) gameObject.AddComponent<MeshRenderer>();

        var mc = GetComponent<MeshCollider>();
        if (mc == null) mc = gameObject.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
    }
}