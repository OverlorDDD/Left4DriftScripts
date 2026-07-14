using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class PlayerSelectionManager : MonoBehaviour
{
    public PlayerSelectionPanel[] players;

    [Header("Race Scene")]
    public string raceScene = "MainTrack";

    private int currentSelectingPlayer = 0;
    private int activePlayers = 0;

  public void PlayerConfirmed(int playerIndex)
{
    currentSelectingPlayer++;

    if (currentSelectingPlayer >= activePlayers)
    {
        Debug.Log("ALL PLAYERS READY");
        SceneManager.LoadScene(raceScene);
        return;
    }

    players[0].ShowPlayer(currentSelectingPlayer);

    Debug.Log($"Now selecting Player {currentSelectingPlayer + 1}");
}


  void Start()
{
    int gamepadIndex = 0;

    // спочатку вимикаємо ВСІ панелі
    for (int i = 0; i < players.Length; i++)
        players[i].gameObject.SetActive(false);

    // включаємо тільки тих, хто реально існує у сцені
    for (int i = 0; i < GameData.Instance.connectedPlayers.Count; i++)
    {
        var slot = GameData.Instance.connectedPlayers[i];
        if (!slot.isActive)
    continue;

        if (i >= players.Length)
            break;

        players[i].gameObject.SetActive(slot.isActive);
        players[i].playerIndex = i;

        if (GameData.Instance.connectedPlayers[i].isKeyboard)
        {
            players[i].Initialize(Keyboard.current, true);
        }
        else
        {
            if (gamepadIndex >= Gamepad.all.Count)
            {
                Debug.LogWarning("Not enough gamepads!");
                continue;
            }

            players[i].Initialize(Gamepad.all[gamepadIndex], false);
            gamepadIndex++;
        }
    }
    activePlayers = 0;

foreach (var slot in GameData.Instance.connectedPlayers)
{
    if (slot.isActive)
        activePlayers++;
}
}
}