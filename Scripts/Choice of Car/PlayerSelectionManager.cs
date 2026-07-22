using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class PlayerSelectionManager : MonoBehaviour
{
    public PlayerSelectionPanel[] players;

    [Header("Race Scene")]
    public string raceScene = "MainTrack";

    int activePlayers = 0;
    HashSet<int> confirmedIndices = new HashSet<int>();

    void Start()
    {
        int gamepadIndex = 0;
        activePlayers = 0;
        confirmedIndices.Clear();

        // спочатку вимикаємо ВСІ панелі
        for (int i = 0; i < players.Length; i++)
            players[i].gameObject.SetActive(false);

        // Кожен активний слот отримує СВОЮ панель одночасно —
        // всі гравці обирають машину паралельно, а не по черзі
        for (int i = 0; i < GameData.Instance.connectedPlayers.Count; i++)
        {
            var slot = GameData.Instance.connectedPlayers[i];
            if (!slot.isActive) continue;
            if (i >= players.Length) break;

            players[i].gameObject.SetActive(true);
            players[i].manager = this;

            if (slot.isKeyboard)
            {
                players[i].ShowPlayer(i, Keyboard.current, true);
            }
            else
            {
                if (gamepadIndex >= Gamepad.all.Count)
                {
                    Debug.LogWarning("Not enough gamepads!");
                    continue;
                }
                players[i].ShowPlayer(i, Gamepad.all[gamepadIndex], false);
                gamepadIndex++;
            }

            activePlayers++;
        }
    }

    // ── Кожна панель незалежна; менеджер лише рахує хто вже підтвердив ──
    public void PlayerConfirmed(int playerIndex)
    {
        confirmedIndices.Add(playerIndex);
        Debug.Log($"Player {playerIndex + 1} READY ({confirmedIndices.Count}/{activePlayers})");

        if (confirmedIndices.Count >= activePlayers)
        {
            Debug.Log("ALL PLAYERS READY");
            SceneManager.LoadScene(GameData.Instance.selectedTrackScene);
        }
    }
}