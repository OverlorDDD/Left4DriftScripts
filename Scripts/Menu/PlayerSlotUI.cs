// PlayerSlotUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerSlotUI : MonoBehaviour
{
    [Header("UI елементи")]
    public TextMeshProUGUI slotNumberText;  // "P1"
    public TextMeshProUGUI deviceText;      // "Press to join" або "Keyboard"
    public Image           slotBackground;
    public GameObject      readyIcon;
    

    [Header("Кольори")]
    public Color emptyColor  = new Color(1f, 1f, 1f, 0f);
    public Color activeColor = new Color(1f, 1f, 1f, 0f);

    int slotIndex;

    public void Init(int index)
    {
        slotIndex = index;
        slotNumberText.text = $"P{index + 1}";
        SetEmpty();
    }

    public void SetEmpty()
    {
        deviceText.text        = "Press to join";
        slotBackground.color   = emptyColor;
        if (readyIcon != null) readyIcon.SetActive(false);
    }

    public void SetActive(string deviceName)
    {
        deviceText.text        = deviceName;
        slotBackground.color   = activeColor;
        if (readyIcon != null) readyIcon.SetActive(true);
    }
}