using UnityEngine;
using TMPro;

public class GameTimer : MonoBehaviour
{
    [Header("Timer Settings")]
    public float startTime = 60f; // seconds

    [Header("UI")]
    public TMP_Text timerText; // drag your TMP text here in Inspector

    private float currentTime;
    private bool isRunning = false;
    [SerializeField] public GameObject titlePanel;

    private bool firstStart;

    [SerializeField] public GameObject gameOverPanel;

    void OnEnable()
    {
        // Subscribe to game engine events
        GameEngine.OnDayStarted += HandleDayStarted;
        GameEngine.OnResetAll += HandleResetAll;
        GameEngine.OnDayEnded += HandleDayEnded;
        GameEngine.OnGameOver += HandleGameOver;
    }

    void OnDisable()
    {
        // Unsubscribe to avoid leaks
        GameEngine.OnDayStarted -= HandleDayStarted;
        GameEngine.OnResetAll -= HandleResetAll;
        GameEngine.OnDayEnded -= HandleDayEnded;
        GameEngine.OnGameOver -= HandleGameOver;
    }

    void Start()
    {
        firstStart = true;
        isRunning = false;
        currentTime = 0f;
        gameOverPanel?.SetActive(false);
    }

    void Update()
    {
        if (!isRunning) return;

        currentTime -= Time.deltaTime;

        if (currentTime <= 0 && !firstStart)
        {
            currentTime = 0;
            isRunning = false;

            // Tell the game engine the run should fail (timer ran out).
            if (GameEngine.Instance != null)
                GameEngine.Instance.EndRunFailure("You (Ran out of time, Lost a customer)");
            else
                Debug.LogWarning("[GameTimer] GameEngine instance not found to report timer failure.");

            // The GameEngine.OnGameOver handler will invoke ShowGameOverUI via HandleGameOver.
            return;
        }

        UpdateTimerDisplay();
    }

    // Called by GameEngine when a new day starts
    private void HandleDayStarted(int dayIndex)
    {
        // hide title panel, start timer for the day
        titlePanel?.SetActive(false);
        firstStart = false;
        currentTime = startTime;
        isRunning = true;
        Time.timeScale = 1f; // ensure game is running
        UpdateTimerDisplay();
        Debug.Log($"Timer started for day {dayIndex}");
    }

    // Called by GameEngine when systems should reset (before scene reload or after day end)
    private void HandleResetAll()
    {
        // stop and reset UI; GameEngine will start next day when ready
        isRunning = false;
        currentTime = 0f;
        firstStart = true;

        UpdateTimerDisplay();

        // hide game over (engine or other systems will decide if final game over should show)
        gameOverPanel?.SetActive(false);
        titlePanel?.SetActive(true);

        // ensure normal timeScale (if you want to keep pause, change as needed)
        Time.timeScale = 1f;
    }

    // Optional: if you want to do something immediately when a day ends (UI, animation)
    private void HandleDayEnded(int dayIndex)
    {
        // stop timer while transition occurs
        isRunning = false;

        // If engine reports game over, show game over UI.
        if (GameEngine.IsGameOver)
        {
            ShowGameOverUI();
        }
    }

    void UpdateTimerDisplay()
    {
        // avoid negative display
        float displayTime = Mathf.Max(0f, currentTime);

        // Format as MM:SS
        int minutes = Mathf.FloorToInt(displayTime / 60);
        int seconds = Mathf.FloorToInt(displayTime % 60);
        if (timerText != null)
            timerText.text = $"{minutes:0}:{seconds:00}";
    }

    public float getTime()
    {
        return currentTime;
    }

    // kept public start method for manual start (still valid, but GameEngine will start automatically)
    public void TimerStart()
    {
        titlePanel?.SetActive(false);
        firstStart = false;
        currentTime = startTime;
        isRunning = true;
        UpdateTimerDisplay();
        Debug.Log("Timer start triggered (manual)");
        Time.timeScale = 1f;
    }

    void ShowGameOverUI()
    {
        gameOverPanel?.SetActive(true);
        Time.timeScale = 0f; // freeze game on final game over
    }

    public void ToTitle()
    {
        isRunning = false;
        gameOverPanel.SetActive(false);
        titlePanel.SetActive(true);
        Time.timeScale = 0f;
    }

    private void HandleGameOver(string message)
    {
        // ensure timer stops
        isRunning = false;

        // Do not set any text here — GameOverMenu will handle the message on the game-over canvas.
        ShowGameOverUI();
    }

    // Optional: public methods to control timer externally
    public void PauseTimer() => isRunning = false;
    public void ResumeTimer() => isRunning = true;
    public bool GetTimer() { return isRunning; }
    public void AddTime(float amount) => currentTime += amount;
}
