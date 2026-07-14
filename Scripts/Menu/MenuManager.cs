// MenuManager.cs
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;


public class MenuManager : MonoBehaviour
{
    [Header("UI")]
    public PlayerSlotUI[] playerSlots;       // 4 слоти, перетягни в Inspector
    public Button         startButton;
    public TextMeshProUGUI hintText;

    [Header("Назва сцени гонки")]
    public string raceSceneName = "CarSelect";

    // Відстежуємо які пристрої вже зайняті
    List<InputDevice> claimedDevices = new List<InputDevice>();
    bool keyboardClaimed = false;

    void Start()
{
    for (int i = 0; i < playerSlots.Length; i++)
        playerSlots[i].Init(i);

    startButton.interactable = false;

    startButton.onClick.AddListener(StartRace);

    hintText.text =
        "Press any key or gamepad button";
}

    System.Collections.IEnumerator ListenForJoins()
    {
        while (true)
        {
            // Перевіряємо всі пристрої кожен кадр
            
            yield return null;
        }
    }

   void Update()
{
    CheckKeyboard();
    CheckGamepads();
    CheckStartRaceInput();
}
void CheckStartRaceInput()
{
    if (!startButton.interactable)
        return;

    if (Keyboard.current != null &&
        Keyboard.current.enterKey.wasPressedThisFrame)
    {
        StartRace();
        return;
    }

    foreach (var gamepad in Gamepad.all)
    {
        if (gamepad.startButton.wasPressedThisFrame)
        {
            StartRace();
            return;
        }
    }
}


void CheckKeyboard()
{
    if (GameData.Instance == null)
        return;

    if (keyboardClaimed)
        return;

    if (Keyboard.current.anyKey.wasPressedThisFrame)
    {
        int slot = GetNextFreeSlot();

        if (slot == -1)
            return;

        keyboardClaimed = true;

        ClaimSlot(
            slot,
            "Keyboard",
            Keyboard.current,
            true
        );
    }
}

void CheckGamepads()
{
    if (GameData.Instance == null) return;

    foreach (var gamepad in Gamepad.all)
    {
        if (claimedDevices.Contains(gamepad)) continue;

        if (
            gamepad.buttonSouth.wasPressedThisFrame ||
            gamepad.buttonNorth.wasPressedThisFrame ||
            gamepad.buttonEast.wasPressedThisFrame ||
            gamepad.buttonWest.wasPressedThisFrame ||
            gamepad.startButton.wasPressedThisFrame
        )
        {
            int slot = GetNextFreeSlot();
            if (slot == -1) return;

            

            ClaimSlot(
                slot,
                gamepad.displayName,
                gamepad,
                false
            );
        }
    }
}

    void ClaimSlot(int slotIndex, string deviceName, InputDevice device, bool isKeyboard)
    {
        // Оновлюємо GameData
        var slot = GameData.Instance.GetSlot(slotIndex);
        slot.isActive   = true;
        slot.deviceName = device.deviceId.ToString();
        slot.isKeyboard = isKeyboard;

        // Запам'ятовуємо пристрій
        if (!isKeyboard) claimedDevices.Add(device);

        // Оновлюємо UI
        playerSlots[slotIndex].SetActive(deviceName);

        // Активуємо кнопку старту якщо є хоч один гравець
        startButton.interactable = GameData.Instance.ActivePlayerCount() > 0;

        Debug.Log($"Slot {slotIndex + 1} claimed by {deviceName}");
    }

    int GetNextFreeSlot()
    {
        for (int i = 0; i < GameData.Instance.maxPlayers; i++)
            if (!GameData.Instance.GetSlot(i).isActive) return i;
        return -1;
    }

    

    public void StartRace()
    {
        SceneManager.LoadScene(raceSceneName);
    }
}