using UnityEngine;
using TMPro;

public class GameOverMenu : MonoBehaviour
{
    [Header("UI")]
    public GameObject gameOverPanel;   // assign your Game Over canvas/panel
    public TMP_Text messageText;       // assign the TMP text inside that panel

    void OnEnable()
    {
        GameEngine.OnGameOver += HandleGameOver;
        GameEngine.OnDayStarted += HandleDayStarted;
    }

    void OnDisable()
    {
        GameEngine.OnGameOver -= HandleGameOver;
        GameEngine.OnDayStarted -= HandleDayStarted;
    }
    private void HandleDayStarted(int dayIndex)
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        Time.timeScale = 1f;
    }


    private void HandleGameOver(string message)
    {
        // Show panel and write the message (message comes from GameEngine.EndRunFailure or win)
        if (messageText != null) messageText.text = message;

        if (gameOverPanel != null) gameOverPanel.SetActive(true);

        // Freeze the game-time if desired
        Time.timeScale = 0f;
    }

    // Optional helper called from UI button to return to title or restart
    public void ToTitle()
    {
        Time.timeScale = 1f;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        // Optionally reset engine state or load title scene
        if (GameEngine.Instance != null) GameEngine.Instance.ResetGameState();
    }
}
