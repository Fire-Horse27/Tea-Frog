using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    public Transform target;      // the player
    public float smoothSpeed = 5f;
    public Vector3 offset;        // adjust this in the Inspector

    void LateUpdate()
    {
        if (!target) return;

        Vector3 desiredPosition = target.position + offset;
        Vector3 smoothed = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.position = new Vector3(smoothed.x, smoothed.y, transform.position.z);
    }
}
