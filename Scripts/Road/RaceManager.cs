using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class RaceManager : MonoBehaviour
{
    [Header("Налаштування")]
    public int totalCheckpoints = 4;
    public int totalLaps        = 3;

    [Header("Perfect Start")]
    public float perfectStartWindow = 0.4f;
    public float perfectStartBonus  = 6f;

    [HideInInspector]
    public CarController playerCar;

    List<PlayerRaceState> players = new List<PlayerRaceState>();
    public bool raceStarted { get; private set; }

    // ── Стан гравця ──
    class PlayerRaceState
    {
        public CarController controller;
        public PlayerHUD     hud;
        public bool          isKeyboard;
        public int           currentLap             = 1;
        public int           nextExpectedCheckpoint = 0;
        public bool          allCheckpointsPassed   = false;
        public bool          raceFinished           = false;
        public float         raceTime               = 0f;
        public bool          perfectStartAvailable  = false;
        public float         perfectStartTimer      = 0f;
        public bool          earlyThrottle          = false;
        public bool          countdownActive        = false;
        public Coroutine     messageCoroutine;
    }

    // Для сумісності зі старим CarController
    public bool allCheckpointsPassed =>
        players.Count > 0 && players[0].allCheckpointsPassed;

    // ════════════════════════════════════════
    public void AddPlayer(CarController controller, bool isKeyboard,
                          PlayerHUD hud, bool isFirst)
    {
        var state = new PlayerRaceState
        {
            controller = controller,
            hud        = hud,
            isKeyboard = isKeyboard
        };
        players.Add(state);

        controller.SetCanMove(false);
        ShowControlsHint(state);

        if (isFirst)
            StartCoroutine(CountdownCoroutine());

            if (isFirst)
{
    playerCar = controller;
}
    }

    // ════════════════════════════════════════
    //  ВІДЛІК
    // ════════════════════════════════════════
    IEnumerator CountdownCoroutine()
    {
        foreach (var p in players) p.countdownActive = true;

        foreach (int n in new[] { 3, 2, 1 })
        {
            // Показуємо всім одночасно
            foreach (var p in players)
            {
                if (p.hud?.startCountdownText != null)
                {
                    p.hud.startCountdownText.gameObject.SetActive(true);
                    p.hud.startCountdownText.text = n.ToString();
                    p.hud.startCountdownText.color = new Color(1f, 0.85f, 0f);
                    p.hud.startCountdownText.transform.localScale = Vector3.one * 2.2f;
                }
            }

            float t = 0f;
            while (t < 0.75f)
            {
                t += Time.deltaTime;
                float scale = Mathf.Lerp(2.2f, 0.7f, Mathf.Pow(t / 0.75f, 0.4f));
                foreach (var p in players)
                    if (p.hud?.startCountdownText != null)
                        p.hud.startCountdownText.transform.localScale = Vector3.one * scale;
                yield return null;
            }
        }

        // GO!
        foreach (var p in players)
        {
            p.controller.SetCanMove(true);
            p.countdownActive       = false;
            p.perfectStartAvailable = true;
            p.perfectStartTimer     = perfectStartWindow;

            if (p.hud?.startCountdownText != null)
            {
                p.hud.startCountdownText.text  = "GO!";
                p.hud.startCountdownText.color = new Color(0.2f, 1f, 0.3f);
                p.hud.startCountdownText.transform.localScale = Vector3.one * 2.8f;
            }
        }

        raceStarted = true;

        float gt = 0f;
        while (gt < 0.6f)
        {
            gt += Time.deltaTime;
            float scale = Mathf.Lerp(2.8f, 1f, Mathf.Pow(gt / 0.6f, 0.4f));
            foreach (var p in players)
                if (p.hud?.startCountdownText != null)
                    p.hud.startCountdownText.transform.localScale = Vector3.one * scale;
            yield return null;
        }

        foreach (var p in players)
            if (p.hud?.startCountdownText != null)
                p.hud.startCountdownText.gameObject.SetActive(false);
    }

    // ════════════════════════════════════════
    void Update()
    {
        foreach (var p in players)
        {
            if (p.raceFinished) continue;
            HandlePerfectStart(p);
            if (!raceStarted) continue;
            p.raceTime += Time.deltaTime;
            UpdateTimerUI(p);
            UpdateSpeedUI(p);
        }
    }

    // ════════════════════════════════════════
    //  PERFECT START
    // ════════════════════════════════════════
    void HandlePerfectStart(PlayerRaceState p)
    {
        if (p.controller == null) return;

        bool throttle = p.isKeyboard
            ? (Keyboard.current != null && Keyboard.current.wKey.isPressed)
            : (Gamepad.current  != null && Gamepad.current.rightTrigger.ReadValue() > 0.5f);

        if (p.countdownActive && throttle) p.earlyThrottle = true;
        if (!p.perfectStartAvailable) return;

        p.perfectStartTimer -= Time.deltaTime;

        if (p.earlyThrottle)      { p.perfectStartAvailable = false; return; }
        if (throttle)             { ApplyPerfectStart(p); p.perfectStartAvailable = false; return; }
        if (p.perfectStartTimer <= 0f) p.perfectStartAvailable = false;
    }

    void ApplyPerfectStart(PlayerRaceState p)
    {
        var rb = p.controller.GetComponent<Rigidbody>();
        if (rb != null)
            rb.AddForce(p.controller.transform.forward * perfectStartBonus,
                ForceMode.VelocityChange);
        ShowMessage(p, "PERFECT START!", new Color(0.2f, 1f, 0.3f));
    }

    // ════════════════════════════════════════
    //  ЧЕКПОІНТИ
    // ════════════════════════════════════════
    public void CheckpointPassed(CarController caller, int index)
    {
        var p = FindPlayer(caller);
        if (p == null || p.raceFinished || !raceStarted) return;
        if (index != p.nextExpectedCheckpoint) return;

        p.nextExpectedCheckpoint++;
        ShowMessage(p, $"Checkpoint {index + 1}/{totalCheckpoints}",
            new Color(1f, 0.9f, 0.2f));

        if (p.nextExpectedCheckpoint >= totalCheckpoints)
            p.allCheckpointsPassed = true;
    }

    public void CompleteLap(CarController caller)
    {
        var p = FindPlayer(caller);
        if (p == null || p.raceFinished || !raceStarted) return;
        if (!p.allCheckpointsPassed) return;

        p.allCheckpointsPassed   = false;
        p.nextExpectedCheckpoint = 0;

        if (p.currentLap >= totalLaps)
        {
            p.raceFinished = true;

            // ── ФІНІШ ──
            if (p.hud?.finishText != null)
            {
                p.hud.finishText.gameObject.SetActive(true);
                p.hud.finishText.text =
                    $"FINISH!\n{FormatTime(p.raceTime)}";
            }
            return;
        }

        p.currentLap++;
        if (p.hud?.lapText != null)
            p.hud.lapText.text = $"Lap {p.currentLap}/{totalLaps}";

        ShowMessage(p, $"Lap {p.currentLap}/{totalLaps}",
            new Color(1f, 0.9f, 0.2f));
    }

    // Фолбек для одного гравця (старий API CarController)
    public void CheckpointPassed(int index)
    {
        if (players.Count > 0) CheckpointPassed(players[0].controller, index);
    }

    public void CompleteLap()
    {
        if (players.Count > 0) CompleteLap(players[0].controller);
    }

    // ════════════════════════════════════════
    //  UI
    // ════════════════════════════════════════
    void UpdateSpeedUI(PlayerRaceState p)
    {
        if (p.hud?.speedText == null || p.controller == null) return;

        var rb = p.controller.GetComponent<Rigidbody>();
        if (rb == null) return;

        float kmh = rb.linearVelocity.magnitude * 3.6f;
        p.hud.speedText.text = $"{Mathf.RoundToInt(kmh)} km/h";

        p.hud.speedText.color = kmh < 40 ? Color.white
                              : kmh < 80 ? Color.yellow
                              : Color.red;

        p.hud.speedText.transform.localScale = p.controller.IsBoosting()
            ? Vector3.one * 1.3f
            : Vector3.Lerp(p.hud.speedText.transform.localScale,
                Vector3.one, Time.deltaTime * 8f);
    }

    void UpdateTimerUI(PlayerRaceState p)
    {
        if (p.hud?.timerText == null) return;
        p.hud.timerText.text = FormatTime(p.raceTime);
    }

    // ════════════════════════════════════════
    //  ПОВІДОМЛЕННЯ
    // ════════════════════════════════════════
    void ShowMessage(PlayerRaceState p, string message, Color color)
    {
        if (p.hud?.checkpointText == null) return;
        if (p.messageCoroutine != null) StopCoroutine(p.messageCoroutine);
        p.messageCoroutine = StartCoroutine(MessageCoroutine(p, message, color));
    }

    IEnumerator MessageCoroutine(PlayerRaceState p, string msg, Color color)
    {
        var txt = p.hud.checkpointText;
        txt.text  = msg;
        txt.gameObject.SetActive(true);
        txt.transform.localScale = Vector3.one * 1.8f;

        color.a = 0f;
        txt.color = color;

        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            float progress = t / 0.25f;
            color.a = progress;
            txt.color = color;
            txt.transform.localScale = Vector3.Lerp(
                Vector3.one * 1.8f, Vector3.one, progress);
            yield return null;
        }

        yield return new WaitForSeconds(0.8f);

        t = 0f;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            color.a = 1f - t / 0.35f;
            txt.color = color;
            yield return null;
        }

        txt.gameObject.SetActive(false);
    }

    void ShowControlsHint(PlayerRaceState p)
    {
        if (p.hud?.controlsText == null) return;
        p.hud.controlsText.gameObject.SetActive(true);
        p.hud.controlsText.text = p.isKeyboard
            ? "W — Accelerate\nS — Brake\nA/D — Steer\nSPACE — Drift\nSHIFT — Boost"
            : "RT — Accelerate\nLT — Brake\nStick — Steer\nA — Drift\nLB — Boost";
        StartCoroutine(HideAfterDelay(p.hud.controlsText, 5f));
    }

    IEnumerator HideAfterDelay(TextMeshProUGUI txt, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (txt != null) txt.gameObject.SetActive(false);
    }

    // ════════════════════════════════════════
    PlayerRaceState FindPlayer(CarController c)
    {
        foreach (var p in players)
            if (p.controller == c) return p;
        return null;
    }

    string FormatTime(float time)
    {
        int m  = Mathf.FloorToInt(time / 60f);
        int s  = Mathf.FloorToInt(time % 60f);
        int ms = Mathf.FloorToInt((time * 100f) % 100f);
        return $"{m:00}:{s:00}.{ms:00}";
    }

    // Для прямого тестування сцени без меню
    public void SetPlayerCar(CarController car, bool isKeyboard)
    {
        var hud = FindAnyObjectByType<PlayerHUD>();
        if (hud != null)
            AddPlayer(car, isKeyboard, hud, players.Count == 0);
    }
}