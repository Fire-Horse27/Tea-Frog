using UnityEngine;

public class DoorAnimation : MonoBehaviour
{
    [SerializeField] private SpriteRenderer door1;
    [SerializeField] private SpriteRenderer door2;

    [SerializeField] private Sprite doorClose;
    [SerializeField] private Sprite doorOpen;

    public void DoorOpen()
    {
        door1.sprite = doorOpen;
        door1.flipY = false;

        door2.sprite = doorOpen;
        door2.flipY = true;
    }

    public void DoorClose()
    {
        door1.sprite = doorClose;
        door1.flipY = false;

        door2.sprite = doorClose;
        door2.flipY = true;
    }
}