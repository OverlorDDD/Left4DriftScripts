#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TrackSurfacePatches))]
public class TrackSurfacePatchesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var tp = (TrackSurfacePatches)target;

        EditorGUILayout.Space(10);

        GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
        if (GUILayout.Button("🎨  Generate Patches", GUILayout.Height(36)))
        {
            tp.GeneratePatches();
            EditorUtility.SetDirty(tp);
        }

        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("✕  Clear Patches", GUILayout.Height(26)))
        {
            tp.ClearPatches();
            EditorUtility.SetDirty(tp);
        }
        GUI.backgroundColor = Color.white;

        SceneView.RepaintAll();
    }

    void OnSceneGUI()
    {
        var tp = (TrackSurfacePatches)target;
        if (tp.trackSystem == null || tp.trackSystem.samples == null || tp.trackSystem.samples.Count == 0)
            return;

        var samples = tp.trackSystem.samples;

        foreach (var patch in tp.patches)
        {
            int idx = Mathf.Clamp(
                Mathf.RoundToInt(patch.centerT * (samples.Count - 1)), 0, samples.Count - 1);
            var sample = samples[idx];

            Vector3 center = sample.position + sample.right * patch.sideOffset + sample.up * 0.05f;

            // Показуємо контур плями прямо в Scene View — превʼю ДО генерації
            Handles.color = new Color(0.3f, 0.8f, 1f, 0.8f);

            Vector3 p1 = center + sample.right * (patch.width * 0.5f)  + sample.forward * (patch.length * 0.5f);
            Vector3 p2 = center - sample.right * (patch.width * 0.5f)  + sample.forward * (patch.length * 0.5f);
            Vector3 p3 = center - sample.right * (patch.width * 0.5f)  - sample.forward * (patch.length * 0.5f);
            Vector3 p4 = center + sample.right * (patch.width * 0.5f)  - sample.forward * (patch.length * 0.5f);

            Handles.DrawSolidRectangleWithOutline(
                new Vector3[] { p1, p2, p3, p4 },
                new Color(0.3f, 0.8f, 1f, 0.25f),
                new Color(0.3f, 0.8f, 1f, 1f));

            Handles.Label(center + Vector3.up * 0.5f,
                $"{patch.label}\n{patch.surface}",
                new GUIStyle { normal = { textColor = Color.white }, fontStyle = FontStyle.Bold });
        }
    }
}
#endif