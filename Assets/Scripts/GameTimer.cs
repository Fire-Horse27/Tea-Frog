using UnityEngine;
using TMPro; // only needed if using TextMeshPro

public class GameTimer : MonoBehaviour
{
    [Header("Timer Settings")]
    public float startTime = 60f; // seconds

    [Header("UI")]
    public TMP_Text timerText; // drag your TMP text here in Inspector

    private float currentTime;
    private bool isRunning = true;
    [SerializeField] public GameObject titlePanel;

    void Start()
    {
        titlePanel.SetActive(false);
        currentTime = startTime;
        UpdateTimerDisplay();
    }

    void Update()
    {
        if (!isRunning) return;

        currentTime -= Time.deltaTime;

        if (currentTime <= 0)
        {
            currentTime = 0;
            isRunning = false;
            GameOver();
        }

        UpdateTimerDisplay();
    }

    void UpdateTimerDisplay()
    {
        // Format as MM:SS
        int minutes = Mathf.FloorToInt(currentTime / 60);
        int seconds = Mathf.FloorToInt(currentTime % 60);
        timerText.text = $"{minutes:00}:{seconds:00}";
    }

    [SerializeField] public GameObject gameOverPanel;


    void GameOver()
    {
        isRunning = false;
        gameOverPanel.SetActive(true);
        Time.timeScale = 0f; // freezes game physics/updates
    }

    public void ToTitle()
    {
        isRunning = false;
        gameOverPanel.SetActive(false);
        titlePanel.SetActive(true);
        Time.timeScale = 0f;
    }

    // Optional: public methods to control timer externally
    public void PauseTimer() => isRunning = false;
    public void ResumeTimer() => isRunning = true;
    public void AddTime(float amount) => currentTime += amount;
}
