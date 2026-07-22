using UnityEngine;
using UnityEngine.InputSystem;

// Цей скрипт — міст між Input System і CarController.
// Один екземпляр = один гравець = один геймпад або клавіатура.
[RequireComponent(typeof(CarController))]
public class PlayerInputHandler : MonoBehaviour
{
    [Header("Індекс гравця (0 = перший, 1 = другий...)")]
    public int playerIndex = 0;

    // Поточні значення вводу — читаються CarController
    public float ThrottleInput { get; private set; }
    public float SteerInput    { get; private set; }
    public bool  DriftInput    { get; private set; }
    public bool  BoostInput    { get; private set; }
    public bool ForceKeyboard = false;

    PlayerInput      playerInput;
    CarController    car;

    // Input Actions
    InputAction throttleAction;
    InputAction brakeAction;
    InputAction steerAction;
    InputAction driftAction;
    InputAction boostAction;

    void Awake()
    {
        car         = GetComponent<CarController>();
        playerInput = GetComponent<PlayerInput>();

        // Отримуємо actions по імені
        throttleAction = playerInput.actions["Throttle"];
        brakeAction    = playerInput.actions["Brake"];
        steerAction    = playerInput.actions["Steer"];
        driftAction    = playerInput.actions["Drift"];
        boostAction    = playerInput.actions["Boost"];
    }

    void Update()
    {

         if (ForceKeyboard)
    {
         if (Keyboard.current == null) return;
        ThrottleInput = 0f;
        if (Keyboard.current.wKey.isPressed) ThrottleInput =  1f;
        if (Keyboard.current.sKey.isPressed) ThrottleInput = -1f;

        SteerInput = 0f;
        if (Keyboard.current.aKey.isPressed) SteerInput = -1f;
        if (Keyboard.current.dKey.isPressed) SteerInput =  1f;

        DriftInput = Keyboard.current.spaceKey.isPressed;
        BoostInput = Keyboard.current.leftShiftKey.isPressed;
        return;
    }
        // Throttle: RT дає 0..1, W дає 0 або 1
        float throttle = throttleAction.ReadValue<float>();

        // Brake/Reverse: LB або S
        float brake = brakeAction.ReadValue<float>();

        // Підсумковий moveInput: вперед мінус назад
        ThrottleInput = throttle - brake;

        // Steer: лівий стік X або A/D
        SteerInput = steerAction.ReadValue<float>();

        // Drift: кнопка A або Space
        DriftInput = driftAction.IsPressed();

        // Boost charge: LT або Shift
        BoostInput = boostAction.ReadValue<float>() > 0.1f;
    }
}