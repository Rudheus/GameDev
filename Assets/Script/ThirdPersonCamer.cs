using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Orbit")]
    public float distance = 3f;
    public float height = 2f;
    public float mouseSensitivity = 3f;

    [Header("Vertical Clamp")]
    public float minVerticalAngle = -20f;
    public float maxVerticalAngle = 60f;

    [Header("Look")]
    public float lookHeight = 1f;

    private float yaw = 0f;     // rotasi horizontal (Mouse X)
    private float pitch = 10f;  // rotasi vertical (Mouse Y)

    void LateUpdate()
    {
        if (target == null) return;

        yaw   += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        pitch  = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 offset = rotation * new Vector3(0f, height, -distance);

        transform.position = target.position + offset;
        transform.LookAt(target.position + Vector3.up * lookHeight);
    }
}
