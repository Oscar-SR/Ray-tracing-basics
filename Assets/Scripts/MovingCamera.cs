using UnityEngine;

public class MovingCamera : MonoBehaviour
{
    [SerializeField] private const float shiftMultiplier = 2.0f;
    [SerializeField] private const float sensitivity = 2.0f;
    [SerializeField] private const float movementSpeed = 10.0f;

    private float yaw = 0.0f;
    private float pitch = 0.0f;

    void Update()
    {
        if (!Cursor.visible)
        {
            // Mouse look
            yaw += Input.GetAxis("Mouse X") * sensitivity;
            pitch -= Input.GetAxis("Mouse Y") * sensitivity;
            pitch = Mathf.Clamp(pitch, -89f, 89f); // Avoid the complete vertical rotation

            transform.eulerAngles = new Vector3(pitch, yaw, 0.0f);
        }

        // Move
        float currentSpeed = movementSpeed;
        if (Input.GetKey(KeyCode.LeftShift)) currentSpeed *= shiftMultiplier;

        Vector3 direction = new Vector3(
            Input.GetAxis("Horizontal"), // A/D
            0f,
            Input.GetAxis("Vertical")    // W/S
        );

        Vector3 move = transform.TransformDirection(direction) * currentSpeed * Time.deltaTime;

        // Up / Down (Q/E)
        if (Input.GetKey(KeyCode.E)) move.y += currentSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.Q)) move.y -= currentSpeed * Time.deltaTime;

        transform.position += move;

        // Enable cursor
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Lock cursor again with left click
        if (Input.GetMouseButtonDown(0) && Cursor.visible)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
