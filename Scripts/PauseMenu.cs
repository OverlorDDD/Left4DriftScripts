using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class PauseMenu : MonoBehaviour
{
    [Header("UI")]
    public GameObject pausePanel;
    public Button     resumeButton;
    public Button     restartButton;
    public Button     mainMenuButton;
    public string     mainMenuScene = "MainMenu";

    bool isPaused = false;

    void Start()
    {
        pausePanel.SetActive(false);
        resumeButton.onClick.AddListener(Resume);
        restartButton.onClick.AddListener(Restart);
        mainMenuButton.onClick.AddListener(GoToMenu);
    }

    void Update()
    {
        bool pausePressed =
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current  != null && Gamepad.current.startButton.wasPressedThisFrame);

        if (pausePressed)
        {
            if (isPaused) Resume();
            else          Pause();
        }
    }

    public void Pause()
    {
        isPaused           = true;
        Time.timeScale     = 0f;
        pausePanel.SetActive(true);
        // Показуємо курсор
        Cursor.lockState   = CursorLockMode.None;
        Cursor.visible     = true;
    }

    public void Resume()
    {
        isPaused           = false;
        Time.timeScale     = 1f;
        pausePanel.SetActive(false);
        Cursor.lockState   = CursorLockMode.Locked;
        Cursor.visible     = false;
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuScene);
    }

    void OnDestroy()
    {
        Time.timeScale = 1f; // Безпека — якщо сцена розвантажилась під час паузи
    }
}