// CarColorChanger.cs
using UnityEngine;

public class CarColorChanger : MonoBehaviour
{
    [Header("Меші що змінюють колір")]
    public Renderer[] bodyRenderers; // перетягни Body, Spoiler і т.д.

    public void ApplyColor(Material colorMaterial)
    {
        if (colorMaterial == null) return;

        foreach (var renderer in bodyRenderers)
        {
            if (renderer == null) continue;
            renderer.material = colorMaterial;
        }
    }

    public void ApplyColor(int colorIndex, CarData carData)
    {
        if (carData.colorVariants == null || colorIndex >= carData.colorVariants.Length) return;
        ApplyColor(carData.colorVariants[colorIndex]);
    }
}