using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[RequireComponent(typeof(SplineContainer))]
public class CheckpointPlacer : MonoBehaviour
{
    public int checkpointCount = 10;
    public GameObject checkpointPrefab; // префаб з Checkpoint скриптом і колайдером-тригером
    public float checkpointWidth = 8f;  // має збігатись з roadWidth
    public Transform checkpointsParent; // куди складати створені чекпоінти

    [ContextMenu("Place Checkpoints")]
    void PlaceCheckpoints()
    {
        var splineContainer = GetComponent<SplineContainer>();
        Spline spline = splineContainer.Spline;

        // Видалити старі чекпоінти якщо є
        if (checkpointsParent != null)
        {
            for (int i = checkpointsParent.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(checkpointsParent.GetChild(i).gameObject);
            }
        }

        for (int i = 0; i < checkpointCount; i++)
        {
            float t = (float)i / checkpointCount; // рівномірно по довжині [0..1)

            spline.Evaluate(t, out float3 pos, out float3 tangent, out float3 up);

            Vector3 worldPos = transform.TransformPoint(pos);
            Vector3 forward = transform.TransformDirection(((Vector3)tangent).normalized);

            GameObject cp = checkpointPrefab != null
                ? (GameObject)Instantiate(checkpointPrefab)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);

            cp.name = $"Checkpoint{i + 1}";
            cp.transform.position = worldPos;

            // Поворот чекпоінта перпендикулярно до руху треку
            cp.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            if (checkpointsParent != null)
                cp.transform.SetParent(checkpointsParent);

            // Налаштувати тригер-колайдер під ширину дороги
            BoxCollider box = cp.GetComponent<BoxCollider>();
            if (box == null) box = cp.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(checkpointWidth, 5f, 1f);

            // Прописати checkpointIndex якщо є скрипт Checkpoint
            Checkpoint checkpointScript = cp.GetComponent<Checkpoint>();
            if (checkpointScript != null)
                checkpointScript.checkpointIndex = i;
        }

        Debug.Log($"Placed {checkpointCount} checkpoints along the spline.");
    }
}