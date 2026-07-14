using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;

public class RaceStarter : MonoBehaviour
{
    [Header("Машини")]
    public CarData[] availableCars;

    [Header("Точки старту")]
    public Transform[] spawnPoints;

    [Header("Префаб камери з HUD")]
    public GameObject playerCameraRigPrefab;

    [Header("RaceManager")]
    public RaceManager raceManager;

    int totalActivePlayers = 0;
    

    void Start()
    {
        if (GameData.Instance != null)
        {
            foreach (var slot in GameData.Instance.connectedPlayers)
                if (slot.isActive) totalActivePlayers++;
        }

        if (totalActivePlayers == 0) totalActivePlayers = 1;

        if (GameData.Instance == null)
        {
            SpawnPlayer(0, null, true, availableCars[0], 0);
            return;
        }

        bool isFirst = true;
        foreach (var slot in GameData.Instance.connectedPlayers)
        {
            
            if (!slot.isActive) continue;

            InputDevice device = null;
            if (!slot.isKeyboard)
            {
                foreach (var gp in Gamepad.all)
                {
                    
                    if (gp.displayName == slot.deviceName)
                    {
                        device = gp;
                        break;
                    }
                }
            }

            var carChoice = GameData.Instance.playerCarChoices[slot.slotIndex];
            CarData chosenCar = availableCars[carChoice.carIndex];

            SpawnPlayer(slot.slotIndex, device, slot.isKeyboard,
                        chosenCar, carChoice.colorIndex, isFirst);
            isFirst = false;
        }
    }

    void SpawnPlayer(int slotIndex, InputDevice device, bool isKeyboard,
                     CarData carData, int colorIndex, bool isFirst = true)
    {
        
        if (slotIndex >= spawnPoints.Length) return;

        // ── Машина ──
        GameObject car = Instantiate(
            carData.carPrefab,
            spawnPoints[slotIndex].position,
            spawnPoints[slotIndex].rotation);

        car.name = $"PlayerCar_P{slotIndex + 1}";

        var colorChanger = car.GetComponentInChildren<CarColorChanger>();
        if (colorChanger != null) colorChanger.ApplyColor(colorIndex, carData);

        var controller = car.GetComponentInChildren<CarController>();
        if (controller != null)
        {
            if (carData.stats != null)
    controller.stats = carData.stats;
            
             controller.trackSpline = FindFirstObjectByType<SplineContainer>();
        }

       // ── Вхід ──
var playerInput = car.GetComponentInChildren<PlayerInput>();

if (playerInput != null)
{
    if (device != null)
    {
        playerInput.SwitchCurrentControlScheme("Gamepad", device);
    }
    else if (isKeyboard)
    {
        playerInput.SwitchCurrentControlScheme("Keyboard&Mouse", Keyboard.current);
    }
}

        var handler = car.GetComponentInChildren<PlayerInputHandler>();
        if (handler != null)
            handler.ForceKeyboard = isKeyboard && device == null;
            Debug.Log($"Player {slotIndex}  isKeyboard={isKeyboard}  device={device}  ForceKeyboard={handler.ForceKeyboard}");

        // ── Камера і HUD ──
        GameObject cameraRig = Instantiate(playerCameraRigPrefab);
        cameraRig.name = $"PlayerCamera_P{slotIndex + 1}";

        if (handler != null)
    handler.ForceKeyboard = isKeyboard && device == null;

       

        var cam = car.GetComponentInChildren<Camera>();
        if (cam != null)
        {
            cam.rect  = SplitScreenLayout.GetViewport(slotIndex, totalActivePlayers);
            cam.depth = slotIndex;
        }

        var hud = cameraRig.GetComponent<PlayerHUD>();
        if (hud != null)
        {
            hud.Init(cam);
            

            var boostUI = cameraRig.GetComponent<BoostUI>();
            if (boostUI != null) boostUI.carController = controller;
        }

        // ── RaceManager ──
        if (raceManager != null && controller != null)
            raceManager.AddPlayer(controller, isKeyboard, hud, isFirst);
    }
}