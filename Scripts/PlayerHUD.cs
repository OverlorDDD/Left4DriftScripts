using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHUD : MonoBehaviour
{
    public Canvas canvas;

    public TextMeshProUGUI lapText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI speedText;
    public TextMeshProUGUI checkpointText;
    public TextMeshProUGUI finishText;
    public TextMeshProUGUI controlsText;
    public TextMeshProUGUI startCountdownText;

    public void Init(Camera cam)
{
    if (canvas == null)
        canvas = GetComponentInChildren<Canvas>();

    if (canvas == null)
        return;

    canvas.renderMode = RenderMode.ScreenSpaceCamera;
    canvas.worldCamera = cam;
    canvas.planeDistance = 1f;

    canvas.sortingOrder = 10;
}
}