using UnityEngine;

public class KettleFunction : MonoBehaviour
{
    public SpriteRenderer targetRenderer; // assign in Inspector (or GetComponent in Start)
    public Sprite[] sprites;              // assign PNGs (sprites) in Inspector
    private int currentIndex = 0;

    // Example called by trigger or other events
    public void NextSprite()
    {
        if (sprites == null || sprites.Length == 0) return;
        currentIndex = (currentIndex + 1) % sprites.Length;
        targetRenderer.sprite = sprites[currentIndex];
    }
}
