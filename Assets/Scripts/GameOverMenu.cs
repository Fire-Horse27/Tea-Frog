using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverMenu : MonoBehaviour
{
    public GameTimer timer;

    public void RestartGame()
    {
        Time.timeScale = 1f; // Unpause time
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void TitleScreen()
    {
        timer.ToTitle();
    }

    public void QuitGame()
    {
        Debug.Log("Quit Game");
        Application.Quit();
    }
}