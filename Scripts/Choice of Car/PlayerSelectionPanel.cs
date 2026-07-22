using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class PlayerSelectionPanel : MonoBehaviour
{
    [Header("Player")]
    public int playerIndex;

    public bool IsConfirmed { get; private set; }

    public PlayerSelectionManager manager;

    [Header("Cars")]
    public CarData[] availableCars;

    [Header("Preview")]
    public Transform carDisplayPivot;
    public float rotationSpeed = 20f;
    public Vector3 displayOffset;

    [Header("UI")]
    public TextMeshProUGUI playerLabel;
    public TextMeshProUGUI carNameText;

    public Slider speedBar;
    public Slider accelerationBar;
    public Slider handlingBar;
    public Slider driftBar;

    public Image colorPreviewCircle;
    public TextMeshProUGUI colorNameText;

    [Header("Керування стіком (антидубль)")]
    public float stickThreshold = 0.6f;
    public float stickCooldown  = 0.25f;

    [HideInInspector]
    public bool ready;

    int currentCarIndex;
    int currentColorIndex;

    public InputDevice device;
    public bool isKeyboard;

    GameObject previewCar;
    CarColorChanger colorChanger;

    bool  stickArmedRight = true;
    bool  stickArmedLeft  = true;
    float stickCooldownTimer = 0f;

    public int CurrentCarIndex   => currentCarIndex;
    public int CurrentColorIndex => currentColorIndex;

    // ════════════════════════════════════════
    //  АТОМАРНА зміна гравця — playerIndex + device одночасно.
    //  Це саме той метод який шукає PlayerSelectionManager.
    // ════════════════════════════════════════
    public void ShowPlayer(int index, InputDevice newDevice, bool keyboard)
    {
        playerIndex = index;
        device      = newDevice;
        isKeyboard  = keyboard;

        IsConfirmed = false;
        currentCarIndex   = 0;
        currentColorIndex = 0;

        stickArmedRight = true;
        stickArmedLeft  = true;
        stickCooldownTimer = 0f;

        SpawnPreview();
        RefreshUI();
    }

    // Залишено для сумісності зі старими викликами
    public void SetDevice(InputDevice newDevice, bool keyboard)
    {
        device     = newDevice;
        isKeyboard = keyboard;
    }

    public void Initialize(InputDevice device, bool keyboard)
    {
        SetDevice(device, keyboard);
        SpawnPreview();
        RefreshUI();
    }

    void Update()
    {
        if (previewCar != null)
            previewCar.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

        if (IsConfirmed) return;
        if (device == null) return;

        if (stickCooldownTimer > 0f)
            stickCooldownTimer -= Time.deltaTime;

        if (device is Gamepad gamepad)
        {
            float stickX = gamepad.leftStick.x.ReadValue();

            if (stickCooldownTimer <= 0f)
            {
                if (stickX > stickThreshold && stickArmedRight)
                {
                    NextCar();
                    stickArmedRight    = false;
                    stickCooldownTimer = stickCooldown;
                }
                else if (stickX < -stickThreshold && stickArmedLeft)
                {
                    PrevCar();
                    stickArmedLeft     = false;
                    stickCooldownTimer = stickCooldown;
                }
            }

            if (stickX < stickThreshold * 0.5f)  stickArmedRight = true;
            if (stickX > -stickThreshold * 0.5f) stickArmedLeft  = true;

            if (gamepad.dpad.right.wasPressedThisFrame) NextCar();
            if (gamepad.dpad.left.wasPressedThisFrame)  PrevCar();

            if (gamepad.rightShoulder.wasPressedThisFrame) NextColor();
            if (gamepad.leftShoulder.wasPressedThisFrame)  PrevColor();

            if (gamepad.buttonSouth.wasPressedThisFrame) Confirm();
        }
        else if (device is Keyboard)
        {
            if (Keyboard.current.dKey.wasPressedThisFrame) NextCar();
            if (Keyboard.current.aKey.wasPressedThisFrame) PrevCar();

            if (Keyboard.current.eKey.wasPressedThisFrame) NextColor();
            if (Keyboard.current.qKey.wasPressedThisFrame) PrevColor();

            if (Keyboard.current.enterKey.wasPressedThisFrame) Confirm();
        }
    }

    //------------------------------------------

    public void NextCar()
    {
        currentCarIndex++;
        if (currentCarIndex >= availableCars.Length) currentCarIndex = 0;
        currentColorIndex = 0;
        SpawnPreview();
        RefreshUI();
    }

    public void PrevCar()
    {
        currentCarIndex--;
        if (currentCarIndex < 0) currentCarIndex = availableCars.Length - 1;
        currentColorIndex = 0;
        SpawnPreview();
        RefreshUI();
    }

    //------------------------------------------

    public void NextColor()
    {
        var data = availableCars[currentCarIndex];
        currentColorIndex++;
        if (currentColorIndex >= data.colorVariants.Length) currentColorIndex = 0;
        colorChanger.ApplyColor(currentColorIndex, data);
        RefreshColor();
    }

    public void PrevColor()
    {
        var data = availableCars[currentCarIndex];
        currentColorIndex--;
        if (currentColorIndex < 0) currentColorIndex = data.colorVariants.Length - 1;
        colorChanger.ApplyColor(currentColorIndex, data);
        RefreshColor();
    }

    //------------------------------------------

    void SpawnPreview()
    {
        StopAllCoroutines();
        if (previewCar != null) Destroy(previewCar);

        CarData data = availableCars[currentCarIndex];
        previewCar = Instantiate(data.carPrefab);
        previewCar.transform.SetParent(carDisplayPivot, false);
        previewCar.transform.localPosition = displayOffset;
        previewCar.transform.localRotation = Quaternion.identity;
        previewCar.transform.localScale    = Vector3.one * 1.5f;

        foreach (var rb in previewCar.GetComponentsInChildren<Rigidbody>())
        {
            rb.isKinematic = true;
            rb.useGravity  = false;
        }

        foreach (var cam in previewCar.GetComponentsInChildren<Camera>())
            cam.enabled = false;

        foreach (var a in previewCar.GetComponentsInChildren<AudioListener>())
            a.enabled = false;

        var controller = previewCar.GetComponentInChildren<CarController>();
        if (controller != null) controller.enabled = false;

        var input = previewCar.GetComponentInChildren<PlayerInputHandler>();
        if (input != null) input.enabled = false;

        var pi = previewCar.GetComponentInChildren<PlayerInput>();
        if (pi != null) pi.enabled = false;

        colorChanger = previewCar.GetComponentInChildren<CarColorChanger>();
        colorChanger.ApplyColor(currentColorIndex, data);
    }

    //------------------------------------------

    void RefreshUI()
    {
        var data = availableCars[currentCarIndex];

        if (playerLabel != null)  playerLabel.text = $"PLAYER {playerIndex + 1}";
        if (carNameText != null)  carNameText.text = data.carName;

        speedBar.value        = data.speedRating / 10f;
        accelerationBar.value = data.accelerationRating / 10f;
        handlingBar.value     = data.handlingRating / 10f;
        driftBar.value        = data.driftRating / 10f;

        RefreshColor();
    }

    void RefreshColor()
    {
        var data = availableCars[currentCarIndex];

        if (colorNameText != null)
            colorNameText.text = data.colorNames[currentColorIndex];

        if (colorPreviewCircle != null)
        {
            Material mat = data.colorVariants[currentColorIndex];
            Color col = Color.white;

            if (mat.HasProperty("_BaseColor")) col = mat.GetColor("_BaseColor");
            else if (mat.HasProperty("_Color")) col = mat.GetColor("_Color");

            col.a = 1f;
            colorPreviewCircle.color = col;
        }
    }

    //------------------------------------------

    public void Confirm()
    {
        IsConfirmed = true;

        GameData.Instance.playerCarChoices[playerIndex].carIndex   = currentCarIndex;
        GameData.Instance.playerCarChoices[playerIndex].colorIndex = currentColorIndex;

        manager.PlayerConfirmed(playerIndex);
        Debug.Log($"Player {playerIndex + 1} READY");
    }
}