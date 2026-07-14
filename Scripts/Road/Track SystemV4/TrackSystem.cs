using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Collections.Generic;

[RequireComponent(typeof(SplineContainer))]
public class TrackSystem : MonoBehaviour
{
    [Header("Профілі")]
    public TrackZoneDefinition    zoneDefinition;
    public TrackSurfaceProperties surfaceProperties;
    public TrackControlProfile    controlProfile;   // ← один профіль замість двох

    [Header("Якість")]
    [Range(100, 3000)] public int sampleCount = 600;

    [Header("Сектори-коллайдери")]
    [Range(20, 500)] public int  colliderSectorCount = 150;
    public float                 colliderHeight      = 0.4f;

    [Header("Shoulder (пагорб)")]
    public float shoulderSlopeHeight = 3f;
    public float shoulderFlatWidth   = 2f;

    [Header("Бар'єри")]
    public float    barrierHeight    = 1.8f;
    public float    barrierThickness = 0.5f;
    public Material barrierMaterial;

    [System.Serializable]
    public class BarrierOverride
    {
        public string  label         = "Override";
        [Range(0f,1f)] public float fromT          = 0f;
        [Range(0f,1f)] public float toT            = 0.1f;
        public float   heightOverride = 1.8f;
        public float   extraOffset    = 0f;
        public bool    hideLeft       = false;
        public bool    hideRight      = false;
    }
    [Header("Ручні overrides бар'єрів")]
    public List<BarrierOverride> barrierOverrides = new List<BarrierOverride>();

    [System.Serializable]
    public class SectorSurfaceOverride
    {
        [Range(0f,1f)] public float fromT   = 0f;
        [Range(0f,1f)] public float toT     = 1f;
        public SurfaceType          surface = SurfaceType.Asphalt;
    }
    public List<SectorSurfaceOverride> surfaceOverrides = new List<SectorSurfaceOverride>();

    [HideInInspector] public List<TrackSample> samples = new List<TrackSample>();

    SplineContainer splineContainer;
    GameObject roadRoot, shoulderRoot, barrierRoot, colliderRoot;

    // ════════════════════════════════════════
    //  ПУБЛІЧНЕ API
    // ════════════════════════════════════════

    public void DoSampleSpline()
    {
        splineContainer = GetComponent<SplineContainer>();
        if (splineContainer == null) { Debug.LogError("SplineContainer не знайдений!"); return; }

        samples.Clear();
        var spline    = splineContainer.Spline;
        float totalLen = spline.GetLength();

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            spline.Evaluate(t, out float3 pos, out float3 tangent, out float3 _);

            Vector3 fwd = ((Vector3)tangent).normalized;

            // Базові вектори (без banking)
            Vector3 baseRight = Vector3.Cross(Vector3.up, fwd).normalized;

            // Параметри з control profile
            var eval = controlProfile != null
                ? controlProfile.Evaluate(t)
                : new TrackControlProfile.EvalResult { halfWidth = 5f };

            // Banking — повертаємо right і up навколо forward
            Quaternion bankRot = Quaternion.AngleAxis(eval.bankAngle, fwd);
            Vector3 right = bankRot * baseRight;
            Vector3 up    = bankRot * Vector3.up;

            // Висота — завжди по world up (щоб горки не залежали від banking)
            Vector3 p = (Vector3)pos + Vector3.up * eval.heightOffset;

            samples.Add(new TrackSample
            {
                position           = p,
                forward            = fwd,
                right              = right,
                up                 = up,
                t                  = t,
                totalWidth         = eval.halfWidth * 2f,
                bankAngle          = eval.bankAngle,
                distanceAlongTrack = t * totalLen
            });
        }

        Debug.Log($"✅ Sampled {samples.Count} points | {totalLen:F1}m");
    }

    public void DoGenerateRoad()
    {
        if (samples.Count == 0) DoSampleSpline();
        if (!CheckZoneDef()) return;
        EnsureRoot(ref roadRoot, "TrackMesh_Road");
        ClearChildren(roadRoot);

        foreach (var zone in zoneDefinition.zones)
        {
            if (!zone.generateMesh)            continue;
            if (zone.type == ZoneType.Barrier) continue;
            if (zone.type == ZoneType.Shoulder)continue;
            BuildRoadSide(zone, roadRoot, -1, zone.name + "_Left");
            BuildRoadSide(zone, roadRoot,  1, zone.name + "_Right");
        }
    }

    public void DoGenerateShoulders()
    {
        if (samples.Count == 0) DoSampleSpline();
        if (!CheckZoneDef()) return;
        EnsureRoot(ref shoulderRoot, "TrackMesh_Shoulders");
        ClearChildren(shoulderRoot);

        foreach (var zone in zoneDefinition.zones)
        {
            if (zone.type != ZoneType.Shoulder) continue;
            if (!zone.generateMesh)             continue;
            BuildShoulderSide(zone, shoulderRoot, -1, zone.name + "_Left");
            BuildShoulderSide(zone, shoulderRoot,  1, zone.name + "_Right");
        }
    }

    public void DoGenerateBarriers()
    {
        if (samples.Count == 0) DoSampleSpline();
        EnsureRoot(ref barrierRoot, "TrackBarriers");
        ClearChildren(barrierRoot);

        TrackZoneDefinition.Zone shoulderZone = null;
        if (zoneDefinition != null)
            foreach (var z in zoneDefinition.zones)
                if (z.type == ZoneType.Shoulder) { shoulderZone = z; break; }

        BuildBarrierColliders(-1, shoulderZone);
        BuildBarrierColliders( 1, shoulderZone);
    }

    public void DoGenerateColliders()
    {
        if (samples.Count == 0) DoSampleSpline();
        EnsureRoot(ref colliderRoot, "TrackColliders");
        ClearChildren(colliderRoot);
        BuildSectorColliders();
    }

    public void DoGenerateAll()
    {
        DoSampleSpline();
        DoGenerateRoad();
        DoGenerateShoulders();
        DoGenerateBarriers();
        DoGenerateColliders();
        Debug.Log("✅ Track generation complete!");
    }

    public void DoClearAll()
    {
        DestroyRoot(ref roadRoot,     "TrackMesh_Road");
        DestroyRoot(ref shoulderRoot, "TrackMesh_Shoulders");
        DestroyRoot(ref barrierRoot,  "TrackBarriers");
        DestroyRoot(ref colliderRoot, "TrackColliders");
        samples.Clear();
        Debug.Log("🗑 Cleared.");
    }

    // ════════════════════════════════════════
    //  ДОРОГА
    // ════════════════════════════════════════
    void BuildRoadSide(TrackZoneDefinition.Zone zone, GameObject root, int side, string name)
    {
        int n     = samples.Count;
        var verts = new Vector3[n * 2];
        var uvs   = new Vector2[n * 2];
        var tris  = new int[n * 6];
        float uvLen = 0f;

        for (int i = 0; i < n; i++)
        {
            var s  = samples[i];
            float hw = s.totalWidth * 0.5f;

            // Banking враховується через s.right і s.up
            Vector3 p0 = s.position + s.right * (side * zone.innerRadius) + s.up * 0.005f;
            Vector3 p1 = s.position + s.right * (side * hw)               + s.up * 0.005f;

            verts[i*2]   = transform.InverseTransformPoint(p0);
            verts[i*2+1] = transform.InverseTransformPoint(p1);

            if (i > 0) uvLen += Vector3.Distance(samples[i].position, samples[i-1].position);
            uvs[i*2]   = new Vector2(0f, uvLen * 0.1f);
            uvs[i*2+1] = new Vector2(1f, uvLen * 0.1f);
        }

        FillQuadStripClosed(tris, n, side);
        CreateMeshObject(root, name, verts, uvs, tris, zone.material, false);
    }

    // ════════════════════════════════════════
    //  SHOULDER — плавний пагорб, банкінг-коректний
    // ════════════════════════════════════════
    void BuildShoulderSide(TrackZoneDefinition.Zone zone, GameObject root, int side, string name)
    {
        int n   = samples.Count;
        int vps = 6;
        var verts = new Vector3[n * vps];
        var uvs   = new Vector2[n * vps];
        var tris  = new int[n * (vps - 1) * 6 * 2];
        float uvLen = 0f;

        for (int i = 0; i < n; i++)
        {
            var   s     = samples[i];
            float hw    = s.totalWidth * 0.5f;
            float sw    = zone.outerRadius;

            // 6 точок: дуже плавний вихід з краю дороги
            // s.up враховує banking — схил перпендикулярний банкованій дорозі
            Vector3 p0 = s.position + s.right * (side * hw);
            Vector3 p1 = p0 + s.right * (side * sw * 0.15f) + s.up * (shoulderSlopeHeight * 0.02f);
            Vector3 p2 = p1 + s.right * (side * sw * 0.20f) + s.up * (shoulderSlopeHeight * 0.12f);
            Vector3 p3 = p2 + s.right * (side * sw * 0.25f) + s.up * (shoulderSlopeHeight * 0.35f);
            Vector3 p4 = p3 + s.right * (side * sw * 0.20f) + s.up * (shoulderSlopeHeight * 0.45f);
            Vector3 p5 = p4 + s.right * (side * sw * 0.20f) + s.up * (shoulderSlopeHeight * 0.06f)
                           + s.right * (side * shoulderFlatWidth);

            p0 += s.up * (-0.02f);
            p1 += s.up * (-0.01f);

            int b = i * vps;
            verts[b+0] = transform.InverseTransformPoint(p0);
            verts[b+1] = transform.InverseTransformPoint(p1);
            verts[b+2] = transform.InverseTransformPoint(p2);
            verts[b+3] = transform.InverseTransformPoint(p3);
            verts[b+4] = transform.InverseTransformPoint(p4);
            verts[b+5] = transform.InverseTransformPoint(p5);

            if (i > 0) uvLen += Vector3.Distance(samples[i].position, samples[i-1].position);
            float uvV = uvLen * 0.05f; // V — вздовж треку

// U — поперек схилу (0 = край дороги, 1 = верх полиці)
// Замість фіксованих 0, 0.33, 0.66, 1 — рахуємо реальну довжину профілю
float[] profileU = new float[vps];
profileU[0] = 0f;

// Рахуємо реальну довжину між точками профілю
Vector3[] profile = { p0, p1, p2, p3, p4, p5 };
float profileLen = 0f;
for (int v = 1; v < vps; v++)
    profileLen += Vector3.Distance(profile[v-1], profile[v]);

float accumulated = 0f;
for (int v = 1; v < vps; v++)
{
    accumulated += Vector3.Distance(profile[v-1], profile[v]);
    profileU[v] = accumulated / (profileLen + 0.0001f);
}

for (int v = 0; v < vps; v++)
    uvs[b + v] = new Vector2(profileU[v], uvV);
        }

        int ti = 0;
        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            int a = i * vps, bIdx = next * vps;
            for (int v = 0; v < vps - 1; v++)
            {
                int a0=a+v, a1=a+v+1, b0=bIdx+v, b1=bIdx+v+1;
                if (side==-1)
                {
                    tris[ti++]=a0; tris[ti++]=b0; tris[ti++]=a1;
                    tris[ti++]=a1; tris[ti++]=b0; tris[ti++]=b1;
                    tris[ti++]=a1; tris[ti++]=b0; tris[ti++]=a0;
                    tris[ti++]=b1; tris[ti++]=b0; tris[ti++]=a1;
                }
                else
                {
                    tris[ti++]=a0; tris[ti++]=a1; tris[ti++]=b0;
                    tris[ti++]=a1; tris[ti++]=b1; tris[ti++]=b0;
                    tris[ti++]=b0; tris[ti++]=a1; tris[ti++]=a0;
                    tris[ti++]=b0; tris[ti++]=b1; tris[ti++]=a1;
                }
            }
        }

        CreateMeshObject(root, name, verts, uvs, tris, zone.material, true);
    }

    // ════════════════════════════════════════
    //  БАР'ЄРИ — прості BoxColliders
    // ════════════════════════════════════════
    void BuildBarrierColliders(int side, TrackZoneDefinition.Zone shoulderZone)
    {
        int step = Mathf.Max(1, samples.Count / colliderSectorCount);

        for (int i = 0; i < samples.Count; i += step)
        {
            int next = (i + step) % samples.Count;
            var sCurr = samples[i];
            var sNext = samples[next];

            BarrierOverride ov = GetBarrierOverride(sCurr.t);
            if (ov != null && (side == -1 ? ov.hideLeft : ov.hideRight)) continue;

            float h     = ov != null ? ov.heightOverride : barrierHeight;
            float extra = ov != null ? ov.extraOffset    : 0f;

            Vector3 posA = GetBarrierEdgePos(sCurr, side, shoulderZone, extra);
            Vector3 posB = GetBarrierEdgePos(sNext, side, shoulderZone, extra);

            Vector3 dir    = (posB - posA).normalized;
            if (dir.sqrMagnitude < 0.001f) dir = sCurr.forward;

            float   len    = Vector3.Distance(posA, posB) + 0.15f;
            Vector3 center = Vector3.Lerp(posA, posB, 0.5f) + Vector3.up * (h * 0.5f);

            var go = new GameObject($"Barrier_{(side==-1?"L":"R")}_{i:000}");
            go.transform.SetParent(barrierRoot.transform);
            go.transform.position = center;
            go.transform.rotation = Quaternion.LookRotation(dir, sCurr.up);
            go.tag = "Barrier";

            var bc = go.AddComponent<BoxCollider>();
            bc.size   = new Vector3(barrierThickness, h, len);
            bc.center = Vector3.zero;

            // Рендерер тільки якщо є матеріал
            if (barrierMaterial != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh     = CreateBoxMesh(barrierThickness, h, len);
                go.AddComponent<MeshRenderer>().sharedMaterial = barrierMaterial;
            }
        }
    }

    Vector3 GetBarrierEdgePos(TrackSample s, int side,
                              TrackZoneDefinition.Zone shoulderZone, float extra)
    {
        if (shoulderZone != null)
            return GetShoulderFlatEdge(s, side, shoulderZone) + s.right * (side * extra);
        return s.position + s.right * (side * (s.totalWidth * 0.5f + 1f + extra));
    }

    // ════════════════════════════════════════
    //  КОЛАЙДЕРИ СЕКТОРІВ
    // ════════════════════════════════════════
    void BuildSectorColliders()
    {
        int step = Mathf.Max(1, samples.Count / colliderSectorCount);
        for (int i = 0; i < samples.Count; i += step)
        {
            int prev = (i - 1 + samples.Count) % samples.Count;
            int next = (i + step) % samples.Count;
            var sCurr = samples[i];
            var sNext = samples[next];
            var sPrev = samples[prev];

            float hw  = sCurr.totalWidth * 0.5f;
            Vector3 dir = (sNext.position - sPrev.position).normalized;
            if (dir.sqrMagnitude < 0.001f) dir = sCurr.forward;

            var go = new GameObject($"Sector_{i:000}_{GetSurfaceAtT(sCurr.t)}");
            go.transform.SetParent(colliderRoot.transform);
            go.transform.position = Vector3.Lerp(sCurr.position, sNext.position, 0.5f);
            go.transform.rotation = Quaternion.LookRotation(dir, sCurr.up);

            var bc = go.AddComponent<BoxCollider>();
            bc.size = new Vector3(hw * 2f, colliderHeight,
                Vector3.Distance(sCurr.position, sNext.position) + 0.15f);

            var trig = go.AddComponent<TrackSurfaceTrigger>();
            trig.surfaceType = GetSurfaceAtT(sCurr.t);
        }
    }

    // ════════════════════════════════════════
    //  ХЕЛПЕРИ
    // ════════════════════════════════════════

    // Крайня точка плоскої полиці Shoulder — з урахуванням banking
    Vector3 GetShoulderFlatEdge(TrackSample s, int side, TrackZoneDefinition.Zone sh)
    {
        float hw    = s.totalWidth * 0.5f;
        float sw    = sh.outerRadius;
        Vector3 p0 = s.position + s.right * (side * hw);
        Vector3 p1 = p0 + s.right * (side * sw * 0.05f);
        Vector3 p2 = p1 + s.right * (side * sw * 0.75f) + s.up * shoulderSlopeHeight;
        return  p2 + s.right * (side * shoulderFlatWidth);
    }

    BarrierOverride GetBarrierOverride(float t)
    {
        foreach (var ov in barrierOverrides)
            if (t >= ov.fromT && t <= ov.toT) return ov;
        return null;
    }

    SurfaceType GetSurfaceAtT(float t)
    {
        foreach (var ov in surfaceOverrides)
            if (t >= ov.fromT && t <= ov.toT) return ov.surface;
        return SurfaceType.Asphalt;
    }

    public float GetRoadHalfWidth() =>
        controlProfile != null ? controlProfile.Evaluate(0.5f).halfWidth : 5f;

    Mesh CreateBoxMesh(float w, float h, float d)
    {
        var m = new Mesh();
        float hw=w*.5f, hh=h*.5f, hd=d*.5f;
        m.vertices = new Vector3[]
        {
            new(-hw,-hh,-hd),new(hw,-hh,-hd),new(hw,hh,-hd),new(-hw,hh,-hd),
            new(-hw,-hh, hd),new(hw,-hh, hd),new(hw,hh, hd),new(-hw,hh, hd)
        };
        m.triangles = new int[]
        {
            0,2,1,0,3,2, 4,5,6,4,6,7,
            0,1,5,0,5,4, 2,3,7,2,7,6,
            0,4,7,0,7,3, 1,2,6,1,6,5
        };
        m.RecalculateNormals();
        return m;
    }

    void FillQuadStripClosed(int[] tris, int n, int side)
    {
        for (int i = 0; i < n; i++)
        {
            int next=(i+1)%n, ti=i*6, vi=i*2, vni=next*2;
            if (side==-1) { tris[ti]=vi; tris[ti+1]=vi+1; tris[ti+2]=vni; tris[ti+3]=vi+1; tris[ti+4]=vni+1; tris[ti+5]=vni; }
            else          { tris[ti]=vi; tris[ti+1]=vni;  tris[ti+2]=vi+1;tris[ti+3]=vi+1; tris[ti+4]=vni;   tris[ti+5]=vni+1; }
        }
    }

    GameObject CreateMeshObject(GameObject root, string meshName,
                                Vector3[] verts, Vector2[] uvs, int[] tris,
                                Material mat, bool addCollider)
    {
        Transform ex = root.transform.Find(meshName);
        GameObject go = ex != null ? ex.gameObject : new GameObject(meshName);
        if (ex == null)
        {
            go.transform.SetParent(root.transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale    = Vector3.one;
        }

        MeshFilter   mf = go.GetComponent<MeshFilter>();   if (mf==null) mf=go.AddComponent<MeshFilter>();
        MeshRenderer mr = go.GetComponent<MeshRenderer>(); if (mr==null) mr=go.AddComponent<MeshRenderer>();

        var mesh = new Mesh
        {
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
            name = meshName, vertices = verts, uv = uvs, triangles = tris
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mf.sharedMesh     = mesh;
        mr.sharedMaterial = mat;

        if (addCollider)
        {
            MeshCollider mc=go.GetComponent<MeshCollider>(); if(mc==null) mc=go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
        }
        return go;
    }

    bool CheckZoneDef() { if (zoneDefinition!=null) return true; Debug.LogError("Zone Definition!"); return false; }
    void EnsureRoot(ref GameObject r,string name){if(r!=null)return;Transform f=transform.Find(name);if(f!=null){r=f.gameObject;return;}r=new GameObject(name);r.transform.SetParent(transform);r.transform.localPosition=Vector3.zero;r.transform.localRotation=Quaternion.identity;r.transform.localScale=Vector3.one;}
    void ClearChildren(GameObject r){if(r==null)return;while(r.transform.childCount>0)DestroyImmediate(r.transform.GetChild(0).gameObject);}
    void DestroyRoot(ref GameObject r,string name){if(r!=null){DestroyImmediate(r);r=null;return;}Transform f=transform.Find(name);if(f!=null)DestroyImmediate(f.gameObject);}
}