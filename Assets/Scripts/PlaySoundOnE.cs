using UnityEngine;

public class AreaSoundTrigger : MonoBehaviour
{
    private AudioSource audioSource;
    private bool isInArea = false;
    //public AudioClip soundToPlay; // Optional: If you want to use PlayOneShot

    void Start()
    {
        Debug.Log("AreaSoundTrigger initialized.");
        // Get the AudioSource component attached to this GameObject
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            Debug.LogError("AudioSource component not found on this GameObject!");
        }
        // Optional: assign the clip to the AudioSource if using Play()
        // audioSource.clip = soundToPlay; 
    }

    void Update()
    {
        // Check if the player is in the area AND the 'E' key is pressed down
        if (isInArea && Input.GetKeyDown(KeyCode.E))
        {
            // Play the assigned clip without stopping any sound currently playing
            audioSource.PlayOneShot(audioSource.clip);
        }
    }

    // Called when the Player enters the trigger area
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isInArea = true;
            Debug.Log("Player entered the sound trigger area.");
        }
    }

    // Called when the Player exits the trigger area
    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isInArea = false;
            Debug.Log("Player exited the sound trigger area.");
        }
    }
}
