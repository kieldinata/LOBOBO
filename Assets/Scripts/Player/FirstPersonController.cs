using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(200)]
public class FirstPersonController : MonoBehaviour
{
    public CharacterController controller;
    public Transform cameraTransform;
    public StructureGenerator structure;

    [Header("Movement")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float gravity = -9.81f;

    [Header("Look")]
    public float sensitivity = 2f;

    [Header("Head Bob")]
    public float bobFrequency = 10f;
    public float bobAmplitude = 0.05f;
    public float bobSideAmplitude = 0.03f;

    [Header("Spawn")]
    public bool autoPositionAtSpawn = true;
    public float eyeHeight = 1.7f;

    private float pitch;
    private float verticalVelocity;
    private Vector3 cameraBasePos;

    void Start()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        controller.center = new Vector3(0f, controller.height / 2f, 0f);

        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null) rend.enabled = false;

        if (cameraTransform != null)
        {
            cameraBasePos = new Vector3(0f, eyeHeight, 0f);
            cameraTransform.localPosition = cameraBasePos;
            cameraTransform.localRotation = Quaternion.identity;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (autoPositionAtSpawn && structure != null && structure.SpawnRoom != null)
        {
            Vector3 spawnPos = structure.SpawnPosition;
            spawnPos.y += eyeHeight;
            transform.position = spawnPos;
        }
    }

    void Update()
    {
        HandleLook();
        HandleMove();
        HandleHeadBob();
    }

    private void HandleLook()
    {
        if (!Mouse.current.enabled) return;

        Vector2 delta = Mouse.current.delta.ReadValue();
        float mouseX = delta.x * sensitivity * 0.1f;
        float mouseY = delta.y * sensitivity * 0.1f;

        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, -90f, 90f);

        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        transform.Rotate(Vector3.up, mouseX);
    }

    private void HandleMove()
    {
        if (controller == null) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        Vector2 input = new Vector2(
            (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
            (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f)
        );

        if (input.sqrMagnitude > 1f)
            input.Normalize();

        float speed = keyboard.leftShiftKey.isPressed ? sprintSpeed : walkSpeed;

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        Vector3 move = forward * input.y + right * input.x;
        move *= speed;

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;

        controller.Move(move * Time.deltaTime);
    }

    private void HandleHeadBob()
    {
        if (cameraTransform == null) return;

        bool moving = controller != null && controller.isGrounded && controller.velocity.sqrMagnitude > 0.1f;

        if (moving)
        {
            float t = Time.time * bobFrequency;
            float x = Mathf.Sin(t * 0.5f) * bobSideAmplitude;
            float y = Mathf.Abs(Mathf.Sin(t)) * bobAmplitude;
            cameraTransform.localPosition = new Vector3(cameraBasePos.x + x, cameraBasePos.y + y, cameraBasePos.z);
        }
        else
        {
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, cameraBasePos, Time.deltaTime * 10f);
        }
    }
}
