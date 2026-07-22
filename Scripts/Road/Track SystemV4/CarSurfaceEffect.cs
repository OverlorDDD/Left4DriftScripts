using UnityEngine;
using System.Collections;

public class CarSurfaceEffect : MonoBehaviour
{
    CarController controller;
    // Зв'язок з внутрішніми полями CarController через рефлексію або публічні методи
    // Простіший варіант — публічний метод в CarController:

    void Awake() => controller = GetComponent<CarController>();

    public void ApplyTemporarySurface(SurfaceType type, float duration)
    {
        StopAllCoroutines();
        StartCoroutine(TempSurface(type, duration));
    }

    IEnumerator TempSurface(SurfaceType type, float duration)
    {
        controller.OverrideSurface(type);
        yield return new WaitForSeconds(duration);
        controller.ClearSurfaceOverride();
    }
}