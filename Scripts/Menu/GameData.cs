// GameData.cs
using UnityEngine;
using System.Collections.Generic;

public class GameData : MonoBehaviour
{
    public static GameData Instance { get; private set; }

    [System.Serializable]
public class PlayerCarChoice
{
    public int carIndex   = 0; // який CarData обраний
    public int colorIndex = 0; // який колір обраний
    
}

public List<PlayerCarChoice> playerCarChoices = new List<PlayerCarChoice>();

    [System.Serializable]
    public class PlayerSlot
    {
        public int    slotIndex;
        public bool   isActive;
        public string deviceName;   // "Keyboard" або "Gamepad 1" etc
         public bool   isKeyboard;
       
    }

    public List<PlayerSlot> connectedPlayers = new List<PlayerSlot>();
    public int maxPlayers = 4;

    void Awake()
    {
        // Singleton — існує тільки один екземпляр між сценами
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Ініціалізуємо 4 порожніх слоти
        for (int i = 0; i < maxPlayers; i++)
        {
            connectedPlayers.Add(new PlayerSlot
            {
                slotIndex  = i,
                isActive   = false,
                deviceName = "",
                isKeyboard = false
            });
        }
        for (int i = 0; i < maxPlayers; i++)
    playerCarChoices.Add(new PlayerCarChoice());
    }

    public PlayerSlot GetSlot(int index) => connectedPlayers[index];

    public int ActivePlayerCount()
    {
        int count = 0;
        foreach (var slot in connectedPlayers)
            if (slot.isActive) count++;
        return count;
    }
}