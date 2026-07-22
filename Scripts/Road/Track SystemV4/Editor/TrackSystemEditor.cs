#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(TrackSystem))]
public class TrackSystemEditor : Editor
{
    int  selectedPoint = -1;
    bool autoRegen     = false;

    // ════════════════════════════════════════
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var ts = (TrackSystem)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("── Генерація ──", EditorStyles.boldLabel);

        Btn("① Sample Spline",       ()=>ts.DoSampleSpline(),     Color.white);
        Btn("② Generate Road",        ()=>ts.DoGenerateRoad(),      Color.white);
        Btn("③ Generate Shoulders",   ()=>ts.DoGenerateShoulders(), Color.white);
        Btn("④ Generate Barriers",    ()=>ts.DoGenerateBarriers(),  Color.white);
        Btn("⑤ Generate Colliders",   ()=>ts.DoGenerateColliders(), Color.white);
        EditorGUILayout.Space(4);
        Btn("⚡  Generate ALL", ()=>ts.DoGenerateAll(), new Color(0.4f,1f,0.4f), 42);
        Btn("✕  Clear ALL",    ()=>ts.DoClearAll(),    new Color(1f,0.4f,0.4f), 28);

        EditorGUILayout.Space(10);
        autoRegen = EditorGUILayout.Toggle("Авто-перегенерація", autoRegen);
        if (autoRegen)
            EditorGUILayout.HelpBox("Авто-перегенерація активна", MessageType.Warning);

        // ── Control Profile editor ──
        DrawControlProfileEditor(ts);

        if (ts.samples.Count > 0)
            EditorGUILayout.HelpBox($"Samples: {ts.samples.Count}", MessageType.None);
        else
            EditorGUILayout.HelpBox("Натисни '⚡ Generate ALL' щоб почати", MessageType.Warning);

        SceneView.RepaintAll();
    }

    void Btn(string label, System.Action a, Color col, int h=26)
    {
        GUI.backgroundColor = col;
        if (GUILayout.Button(label, GUILayout.Height(h))) { a?.Invoke(); EditorUtility.SetDirty((TrackSystem)target); }
        GUI.backgroundColor = Color.white;
    }

    // ════════════════════════════════════════
    //  Control Profile: список точок в Inspector
    // ════════════════════════════════════════
    void DrawControlProfileEditor(TrackSystem ts)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("── Control Profile ──", EditorStyles.boldLabel);

        if (ts.controlProfile == null)
        {
            EditorGUILayout.HelpBox("Призначте TrackControlProfile", MessageType.Info);
            return;
        }

        var profile = ts.controlProfile;
        var points  = profile.points;

        EditorGUILayout.HelpBox(
            "У Scene View:\n" +
            "• Клікни на кільце щоб виділити точку\n" +
            "• Жовті стрілки ◄► — ширина\n" +
            "• Сині стрілки ▲▼ — висота\n" +
            "• Зелені стрілки ↔ — позиція вздовж сплайну\n" +
            "• Фіолетовий диск — нахил (banking)",
            MessageType.Info);

        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Заголовок точки
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = (i == selectedPoint)
                ? new Color(1f, 0.9f, 0.2f) : new Color(0.9f, 0.9f, 0.9f);
            if (GUILayout.Button(
                $"[{i}] {(string.IsNullOrEmpty(p.label) ? $"t={p.t:F2}" : p.label)}",
                GUILayout.Height(22)))
                selectedPoint = (selectedPoint == i) ? -1 : i;

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("✕", GUILayout.Width(24)))
            {
                Undo.RecordObject(profile, "Remove Control Point");
                points.RemoveAt(i);
                if (selectedPoint >= i) selectedPoint--;
                EditorUtility.SetDirty(profile);
                break;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            // Поля редагування (завжди видимі)
            EditorGUI.BeginChangeCheck();
            p.label        = EditorGUILayout.TextField("Label",       p.label);
            p.t            = EditorGUILayout.Slider("Position (t)",   p.t, 0f, 1f);
            p.halfWidth    = EditorGUILayout.Slider("Half Width (m)",  p.halfWidth, 1f, 50f);
            p.heightOffset = EditorGUILayout.FloatField("Height (m)", p.heightOffset);
            p.bankAngle    = EditorGUILayout.Slider("Bank Angle °",   p.bankAngle, -45f, 45f);
            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(profile);

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(4);
        GUI.backgroundColor = new Color(0.5f, 0.85f, 1f);
        if (GUILayout.Button("+ Додати контрольну точку"))
        {
            Undo.RecordObject(profile, "Add Control Point");
            points.Add(new TrackControlProfile.ControlPoint
                { t = 0.5f, halfWidth = 5f, label = $"Point {points.Count}" });
            EditorUtility.SetDirty(profile);
        }
        GUI.backgroundColor = Color.white;
    }

    // ════════════════════════════════════════
    //  SCENE VIEW
    // ════════════════════════════════════════
    void OnSceneGUI()
    {
        var ts = (TrackSystem)target;
        if (ts == null || ts.samples == null || ts.samples.Count < 2) return;
        if (ts.controlProfile == null) return;

        bool changed = false;

        var profile = ts.controlProfile;
        var points  = profile.points;

        for (int i = 0; i < points.Count; i++)
        {
            var p      = points[i];
            var sample = SampleAtT(ts.samples, p.t);
            bool isSel = (i == selectedPoint);

            // ── Кільця зон навколо сплайну ──
            DrawZoneRings(ts, sample, p, isSel);

            // ── Клік на центр для виділення ──
            float btnSz = HandleUtility.GetHandleSize(sample.position) * 0.25f;
            Handles.color = isSel ? Color.white : new Color(1f,1f,1f,0.6f);
            if (Handles.Button(sample.position, Quaternion.LookRotation(sample.forward, sample.up),
                btnSz, btnSz, Handles.SphereHandleCap))
            {
                selectedPoint = (selectedPoint == i) ? -1 : i;
                Repaint();
            }

            if (!isSel) continue;

            // ════ HANDLES ДЛЯ ВИДІЛЕНОЇ ТОЧКИ ════

            float arrowSz = HandleUtility.GetHandleSize(sample.position) * 0.8f; // великі стрілки

            // ── ШИРИНА — жовті стрілки ліво/право ──
            float hw = p.halfWidth;
            Handles.color = new Color(1f, 0.85f, 0f, 0.95f);

            // Ліва стрілка
            Vector3 leftBase = sample.position - sample.right * hw;
            EditorGUI.BeginChangeCheck();
            Vector3 newLeft = Handles.Slider(leftBase - sample.right * arrowSz * 0.2f,
                -sample.right, arrowSz, Handles.ArrowHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(profile, "Width");
                float delta = Vector3.Dot(leftBase - newLeft, sample.right);
                p.halfWidth = Mathf.Max(1f, Mathf.Round((hw + delta) * 4f) / 4f);
                EditorUtility.SetDirty(profile); changed = true;
            }

            // Права стрілка
            Vector3 rightBase = sample.position + sample.right * hw;
            EditorGUI.BeginChangeCheck();
            Vector3 newRight = Handles.Slider(rightBase + sample.right * arrowSz * 0.2f,
                sample.right, arrowSz, Handles.ArrowHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(profile, "Width");
                float delta = Vector3.Dot(newRight - rightBase, sample.right);
                p.halfWidth = Mathf.Max(1f, Mathf.Round((hw + delta) * 4f) / 4f);
                EditorUtility.SetDirty(profile); changed = true;
            }

            // ── ВИСОТА — сині стрілки вгору/вниз ──
            Handles.color = new Color(0.35f, 0.55f, 1f, 0.95f);
            Vector3 heightBase = sample.position + sample.up * p.heightOffset;

            // Стрілка вгору
            EditorGUI.BeginChangeCheck();
            Vector3 newUp = Handles.Slider(heightBase + Vector3.up * arrowSz * 0.2f,
                Vector3.up, arrowSz, Handles.ArrowHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(profile, "Height");
                float delta = newUp.y - (heightBase + Vector3.up * arrowSz * 0.2f).y;
                p.heightOffset = Mathf.Round((p.heightOffset + delta) * 4f) / 4f;
                EditorUtility.SetDirty(profile); changed = true;
            }

            // Стрілка вниз
            EditorGUI.BeginChangeCheck();
            Vector3 newDown = Handles.Slider(heightBase - Vector3.up * arrowSz * 0.2f,
                -Vector3.up, arrowSz, Handles.ArrowHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(profile, "Height");
                float delta = (heightBase - Vector3.up * arrowSz * 0.2f).y - newDown.y;
                p.heightOffset = Mathf.Round((p.heightOffset - delta) * 4f) / 4f;
                EditorUtility.SetDirty(profile); changed = true;
            }

            // ── ПОЗИЦІЯ ВЗДОВЖ СПЛАЙНУ — зелені стрілки ──
            Handles.color = new Color(0.3f, 1f, 0.4f, 0.95f);
            float trackLen = ts.samples[ts.samples.Count - 1].distanceAlongTrack;

            // Вперед
            EditorGUI.BeginChangeCheck();
            Vector3 fwdBase = sample.position + sample.forward * arrowSz * 1.2f;
            Vector3 newFwd  = Handles.Slider(fwdBase, sample.forward, arrowSz * 0.8f,
                Handles.ArrowHandleCap, 0f);
            if (EditorGUI.EndChangeCheck() && trackLen > 0f)
            {
                Undo.RecordObject(profile, "Move Along");
                p.t = Mathf.Clamp01(p.t + Vector3.Dot(newFwd - fwdBase, sample.forward) / trackLen);
                EditorUtility.SetDirty(profile); changed = true;
            }

            // Назад
            EditorGUI.BeginChangeCheck();
            Vector3 backBase = sample.position - sample.forward * arrowSz * 1.2f;
            Vector3 newBack  = Handles.Slider(backBase, -sample.forward, arrowSz * 0.8f,
                Handles.ArrowHandleCap, 0f);
            if (EditorGUI.EndChangeCheck() && trackLen > 0f)
            {
                Undo.RecordObject(profile, "Move Along");
                p.t = Mathf.Clamp01(p.t - Vector3.Dot(backBase - newBack, sample.forward) / trackLen);
                EditorUtility.SetDirty(profile); changed = true;
            }

            // ── BANKING — фіолетовий диск ──
            // Диск показує поточний нахил. Точка на ободі — handle для зміни кута.
            Vector3 bankUp    = sample.up; // вже враховує banking з останнього DoSampleSpline
            Quaternion bankDiscRot = Quaternion.LookRotation(sample.forward, bankUp);

            Handles.color = new Color(0.8f, 0.35f, 1f, 0.9f);

            EditorGUI.BeginChangeCheck();
            Quaternion newBankRot = Handles.Disc(
                bankDiscRot,
                sample.position,
                sample.forward,
                hw * 1.25f,
                false, 1f);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(profile, "Bank Angle");
                // Витягуємо кут нахилу з нової ротації диску
                Vector3 rotatedUp = newBankRot * Vector3.up;

Vector3 baseRight = Vector3.Cross(Vector3.up, sample.forward).normalized;

float newBank = Mathf.Atan2(
    Vector3.Dot(rotatedUp, baseRight),
    Vector3.Dot(rotatedUp, Vector3.up)) * Mathf.Rad2Deg;
                p.bankAngle = Mathf.Clamp(newBank, -45f, 45f);
                EditorUtility.SetDirty(profile); changed = true;
            }

            // Підпис
            Handles.Label(
                sample.position + sample.up * (hw * 1.4f + 1f),
                $"[{i}] {p.label}\n" +
                $"W:{p.halfWidth*2f:F1}m  H:{p.heightOffset:+0.0;-0.0}m  B:{p.bankAngle:+0.0;-0.0}°\n" +
                $"t={p.t:F3}",
                new GUIStyle
                {
                    normal    = { textColor = Color.white },
                    fontSize  = 11,
                    fontStyle = FontStyle.Bold
                });
        }

        // Авто-перегенерація
        if (changed && autoRegen)
        {
            ts.DoSampleSpline();
            ts.DoGenerateRoad();
            ts.DoGenerateShoulders();
            EditorUtility.SetDirty(ts);
        }
    }

    // ════════════════════════════════════════
    //  Кільця зон навколо точки
    // ════════════════════════════════════════
    void DrawZoneRings(TrackSystem ts, TrackSample sample,
                       TrackControlProfile.ControlPoint p, bool isSel)
    {
        float hw      = p.halfWidth;
        float alpha   = isSel ? 1f : 0.5f;

        // Кільце дороги (біле)
        Handles.color = new Color(1f, 1f, 1f, alpha);
        Handles.DrawWireDisc(sample.position, sample.forward, hw);

        // Лінія ширини всередині кільця — показує banking
        Handles.color = new Color(1f, 1f, 1f, alpha * 0.7f);
        Handles.DrawLine(
            sample.position - sample.right * hw,
            sample.position + sample.right * hw, isSel ? 2.5f : 1f);

        if (ts.zoneDefinition != null)
        {
            foreach (var zone in ts.zoneDefinition.zones)
            {
                if (zone.type == ZoneType.Shoulder)
                {
                    // Кільце shoulder (зелене)
                    float shR = hw + zone.outerRadius;
                    Handles.color = new Color(0.3f, 0.9f, 0.3f, alpha * 0.7f);
                    Handles.DrawWireDisc(sample.position, sample.forward, shR);

                    // Кільце barrier (червоне)
                    float barR = shR + ts.shoulderFlatWidth;
                    Handles.color = new Color(1f, 0.25f, 0.25f, alpha * 0.8f);
                    Handles.DrawWireDisc(sample.position, sample.forward, barR);
                    break;
                }
            }
        }

        // Banking кут — пунктир від центру до правого краю (показує нахил)
        if (Mathf.Abs(p.bankAngle) > 0.5f)
        {
            Handles.color = new Color(0.8f, 0.35f, 1f, alpha * 0.7f);
            Handles.DrawDottedLine(
                sample.position,
                sample.position + sample.right * hw * 1.1f, 3f);
        }
    }

    // ════════════════════════════════════════
    TrackSample SampleAtT(List<TrackSample> samples, float t)
    {
        int idx = Mathf.Clamp(Mathf.RoundToInt(t * (samples.Count-1)), 0, samples.Count-1);
        return samples[idx];
    }
}
#endif