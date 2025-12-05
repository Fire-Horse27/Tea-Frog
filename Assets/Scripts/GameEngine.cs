using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameEngine : MonoBehaviour
{
    // Inspector: set number of days and customers per day (length should be numberOfDays)
    [SerializeField, Tooltip("Total number of playable days.")]
    private int numberOfDays = 3;

    [SerializeField, Tooltip("Customers required to finish each day. Length should match numberOfDays.")]
    private int[] customersPerDay = new int[] { 5, 7, 10 };

    // Public static so other scripts can directly read the current day (1-based). 0 before game start.
    public static int CurrentDay { get; private set; } = 0;

    // Public static so other scripts can check if the game is finished
    public static bool IsGameOver { get; private set; } = false;

    // Events other scripts can subscribe to
    // int dayIndex (1-based)
    public static event Action<int> OnDayStarted;
    // int dayIndex (1-based)
    public static event Action<int> OnDayEnded;
    // int dayIndex (1-based), int customersServed, int customersNeeded
    public static event Action<int, int, int> OnCustomerServedChanged;
    // fired when a reset is requested (before scene reload if used)
    public static event Action OnResetAll;

    // fired when the run ends (win or loss). string = displayed message
    public static event Action<string> OnGameOver;
    public static string GameOverMessage { get; private set; } = "";

    // internal state for this run
    private int customersServedThisDay = 0;

    // singleton convenience (optional)
    public static GameEngine Instance { get; private set; }

    private void Awake()
    {
        // naive singleton guard (keeps first instance)
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // optional: keep across reloads if you want global manager
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // basic validation and fixes
        if (numberOfDays < 1) numberOfDays = 1;

        if (customersPerDay == null || customersPerDay.Length < numberOfDays)
        {
            // ensure we have at least numberOfDays entries
            var newArr = new int[numberOfDays];
            for (int i = 0; i < numberOfDays; i++)
            {
                newArr[i] = (customersPerDay != null && i < customersPerDay.Length && customersPerDay[i] > 0)
                    ? customersPerDay[i]
                    : 5; // default fallback
            }

            customersPerDay = newArr;
        }

        // initialize game state
        ResetGameState();
    }

    /// <summary>
    /// Call this when a customer is successfully served.
    /// </summary>
    public void RegisterCustomerServed()
    {
        if (IsGameOver || CurrentDay <= 0) return;

        customersServedThisDay++;
        OnCustomerServedChanged?.Invoke(CurrentDay, customersServedThisDay, customersPerDay[CurrentDay - 1]);

        if (customersServedThisDay >= customersPerDay[CurrentDay - 1])
            EndCurrentDay();
    }

    /// <summary>
    /// Starts the next day (increments CurrentDay). If all days completed, triggers game over.
    /// </summary>
    private void StartNextDay()
    {
        if (IsGameOver) return;

        CurrentDay++;

        if (CurrentDay > numberOfDays)
        {
            // completed all days -> player won the run
            IsGameOver = true;
            GameOverMessage = "You Win!";
            Debug.Log("Game over: all days completed. Player wins.");
            OnGameOver?.Invoke(GameOverMessage);
            // also trigger reset so UI/system can clear if needed
            RequestResetAll();
            return;
        }

        customersServedThisDay = 0;
        Debug.Log($"Day {CurrentDay} started. Customers required: {customersPerDay[CurrentDay - 1]}");
        OnDayStarted?.Invoke(CurrentDay);
        OnCustomerServedChanged?.Invoke(CurrentDay, customersServedThisDay, customersPerDay[CurrentDay - 1]);
    }

    /// <summary>
    /// Ends the current day and triggers reset logic for the next day.
    /// </summary>
    private void EndCurrentDay()
    {
        Debug.Log($"Day {CurrentDay} ended. Served {customersServedThisDay}/{customersPerDay[CurrentDay - 1]} customers.");
        OnDayEnded?.Invoke(CurrentDay);

        // Reset systems (either via event or scene reload) then proceed to next day or game over.
        RequestResetAll();

        // Small delay before starting next day might be desired; here we start immediately.
        // If you want a delay, replace this with a coroutine and yield for a transition.
        StartNextDay();
    }

    /// <summary>
    /// Requests all systems reset. Triggers OnResetAll. If configured, will reload the active scene.
    /// </summary>
    private void RequestResetAll()
    {
        Debug.Log("Requesting reset of all systems.");
        OnResetAll?.Invoke();
    }

    /// <summary>
    /// Fully reset game state to pre-start. Useful for restarting the run.
    /// </summary>
    public void ResetGameState()
    {
        CurrentDay = 0;
        IsGameOver = false;
        customersServedThisDay = 0;
    }

    /// <summary>
    /// Example helper: get customers required for given day index (1-based).
    /// </summary>
    public int GetCustomersRequiredForDay(int dayIndex)
    {
        if (dayIndex < 1 || dayIndex > customersPerDay.Length) return 0;
        return customersPerDay[dayIndex - 1];
    }

    /// <summary>
    /// Exposed for debug/testing to force end the current day.
    /// </summary>
    public void ForceEndDay()
    {
        if (CurrentDay > 0 && !IsGameOver)
            EndCurrentDay();
    }

    /// <summary>
    /// Immediately end the run as a failure with a message (eg. timer ran out or unserved customer).
    /// </summary>
    public void EndRunFailure(string message)
    {
        if (IsGameOver) return;
        IsGameOver = true;
        GameOverMessage = message ?? "You Lost";
        Debug.Log($"Game over (failure): {GameOverMessage}");
        OnGameOver?.Invoke(GameOverMessage);

        // Ask systems to reset / cleanup. This also allows UI to show proper panels.
        RequestResetAll();
    }

    /// <summary>
    /// Begins the full game run manually (used by Menu / Info Panels).
    /// Resets the game state and starts Day 1.
    /// </summary>
    public void StartRun()
    {
        ResetGameState();   // ensure a clean start
        StartNextDay();     // kicks off Day 1 and triggers OnDayStarted
    }
}
