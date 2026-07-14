// CarData.cs
using UnityEngine;

[CreateAssetMenu(fileName = "NewCar", menuName = "Racing/Car Data")]
public class CarData : ScriptableObject
{
    [Header("Інфо")]
    public string carName = "Sport Car";
    public CarStats stats;
    public GameObject carPrefab; // повний префаб машини з CarController

    [Header("Кольори (текстури кузова)")]
    public Material[] colorVariants; // різні матеріали кольору
    public string[]   colorNames;    // "Red", "Blue", "Yellow" — для UI

    [Header("Характеристики (для показу в UI, 0-10)")]
    [Range(0, 10)] public float speedRating        = 5f;
    [Range(0, 10)] public float accelerationRating  = 5f;
    [Range(0, 10)] public float handlingRating      = 5f;
    [Range(0, 10)] public float driftRating         = 5f;

    [Header("Реальні значення фізики (опційно — перевизначають дефолтні)")]
    public float maxSpeed     = 35f;
    public float acceleration = 30f;
    public float steeringPower = 98.5f;
}