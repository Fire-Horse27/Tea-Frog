using UnityEngine;
using UnityEngine.SceneManagement;

public class Menu : MonoBehaviour
{
    public GameTimer timer;
    public GameObject titlePanel;

    void Start()
    {
        titlePanel.SetActive(true);
    }

    public void StartGame()
    {
        timer.TimerStart();
    }

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