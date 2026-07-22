using UnityEngine;
using System.Collections.Generic;

public class TrackRecorder : MonoBehaviour
{
    public Transform car;
    public float minDistance = 1f; // мін. дистанція між точками
    
    List<Vector3> points = new List<Vector3>();
    LineRenderer line;

    void Start()
    {
        line = gameObject.AddComponent<LineRenderer>();
        line.startWidth = 0.5f;
        line.endWidth = 0.5f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.useWorldSpace = true;
    }

    void Update()
    {
        Vector3 pos = car.position;
        pos.y = 0.05f; // трохи над землею

        if (points.Count == 0 || Vector3.Distance(points[^1], pos) > minDistance)
        {
            points.Add(pos);
            line.positionCount = points.Count;
            line.SetPositions(points.ToArray());
        }
    }

    [ContextMenu("Export points to console")]
    void ExportPoints()
    {
        foreach (var p in points)
            Debug.Log($"new Vector3({p.x}f, {p.y}f, {p.z}f),");
    }
}