#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TrackObjectPlacer))]
public class TrackObjectPlacerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var tp = (TrackObjectPlacer)target;

        EditorGUILayout.Space(10);
        GUI.backgroundColor = new Color(0.4f, 1f, 0.6f);
        if (GUILayout.Button("📍  Place All Objects", GUILayout.Height(36)))
            tp.PlaceAll();

        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
        if (GUILayout.Button("🗑  Clear Placed Objects", GUILayout.Height(26)))
            tp.ClearAll();

        GUI.backgroundColor = Color.white;
    }

    void OnSceneGUI()
    {
        var tp = (TrackObjectPlacer)target;
        if (tp.trackSystem == null || tp.trackSystem.samples == null
            || tp.trackSystem.samples.Count == 0) return;

        // Показуємо позиції об'єктів у Scene View
        foreach (var obj in tp.objects)
        {
            if (obj.prefab == null) continue;

            var samples = tp.trackSystem.samples;
            int idx     = Mathf.RoundToInt(obj.t * (samples.Count - 1));
            idx         = Mathf.Clamp(idx, 0, samples.Count - 1);
            var sample  = samples[idx];

            Vector3 pos = sample.position
                        + sample.right * obj.sideOffset
                        + sample.up    * obj.heightOffset;

            float sz = HandleUtility.GetHandleSize(pos) * 0.3f;
            Handles.color = new Color(0.2f, 0.8f, 1f, 0.8f);
            Handles.SphereHandleCap(0, pos, Quaternion.identity, sz, EventType.Repaint);
            Handles.DrawDottedLine(sample.position, pos, 3f);
            Handles.Label(pos + Vector3.up * sz * 2f,
                obj.label,
                new GUIStyle { normal = { textColor = new Color(0.2f, 0.8f, 1f) }, fontSize = 10 });
        }
    }
}
#endif