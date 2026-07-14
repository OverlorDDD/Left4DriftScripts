using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BoostUI : MonoBehaviour
{
    [Header("Посилання")]
    public CarController carController;

    [Header("Заповнення смужки")]
    public Image boostFill;         // BoostBarFill

    [Header("Фон смужки")]
    public Image boostBackground;   // BoostBarBackground

    [Header("Текст стану")]
    public TextMeshProUGUI boostLabel; // "BOOST" / "READY!" / "ACTIVE"

    [Header("Кольори")]
    public Color colorCharging = new Color(1f, 0.8f, 0f);    // жовтий — заряджається
    public Color colorReady    = new Color(0f, 1f, 0.4f);    // зелений — готовий
    public Color colorActive   = new Color(1f, 0.4f, 0f);    // помаранчевий — активний
    public Color colorEmpty    = new Color(0.4f, 0.4f, 0.4f); // сірий — порожній

    [Header("Анімація пульсації")]
    public float pulseSpeed     = 4f;   // швидкість пульсації коли готовий
    public float pulseIntensity = 0.15f;

    // ── приватні ──
    float displayedCharge; // плавне відображення заряду

    void Update()
    {
        if (carController == null) return;

        float charge    = carController.GetBoostCharge(); // 0..1
        bool  isReady   = carController.IsBoostReady();
        bool  isBoosting = carController.IsBoosting();

        // ── Плавна зміна заповнення ──
        float targetCharge = isBoosting ? 1f : charge;
        displayedCharge = Mathf.Lerp(displayedCharge, targetCharge, Time.deltaTime * 8f);
        boostFill.fillAmount = displayedCharge;

        // ── Колір і текст залежно від стану ──
        if (isBoosting)
        {
            // Буст активний — пульсуючий помаранчевий
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed * 2f) * pulseIntensity;
            boostFill.color = colorActive * pulse;
            if (boostLabel != null) boostLabel.text = "BOOST!";
        }
        else if (isReady)
        {
            // Готовий до активації — пульсуючий зелений
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseIntensity;
            Color c = Color.Lerp(colorReady, Color.white, (pulse - 1f) / pulseIntensity);
            boostFill.color = c;
            if (boostLabel != null) boostLabel.text = "READY!";
        }
        else if (charge > 0f)
        {
            // Заряджається — жовтий
            boostFill.color = colorCharging;
            if (boostLabel != null) boostLabel.text = "BOOST";
        }
        else
        {
            // Порожній
            boostFill.color = colorEmpty;
            if (boostLabel != null) boostLabel.text = "BOOST";
        }

        // ── Фон трохи світлішає коли заряджається ──
        if (boostBackground != null)
        {
            float bgAlpha = Mathf.Lerp(0.6f, 0.9f, charge);
            Color bg = boostBackground.color;
            bg.a = bgAlpha;
            boostBackground.color = bg;
        }
    }
}