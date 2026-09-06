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
    public HeadBobParams idleBob = new HeadBobParams { frequency = 1f, amplitude = 0.02f, sideAmplitude = 0.015f, lerpSpeed = 10f };
    public HeadBobParams walkBob = new HeadBobParams { frequency = 5f, amplitude = 0.05f, sideAmplitude = 0.03f, lerpSpeed = 0f };
    public HeadBobParams sprintBob = new HeadBobParams { frequency = 12f, amplitude = 0.07f, sideAmplitude = 0.04f, lerpSpeed = 0f };

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

        bool moving = controller != null && controller.isGrounded && controller.velocity.sqrMagnitude > 0.1f;
        bool sprinting = moving && Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
        HandleHeadBob(sprinting ? sprintBob : moving ? walkBob : idleBob);
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

    private void HandleHeadBob(HeadBobParams bob)
    {
        if (cameraTransform == null) return;

        float t = Time.time * bob.frequency;
        float x = Mathf.Sin(t * 0.5f) * bob.sideAmplitude;
        float y = Mathf.Abs(Mathf.Sin(t)) * bob.amplitude;
        Vector3 bobPos = new Vector3(cameraBasePos.x + x, cameraBasePos.y + y, cameraBasePos.z);

        cameraTransform.localPosition = bob.lerpSpeed > 0f
            ? Vector3.Lerp(cameraTransform.localPosition, bobPos, Time.deltaTime * bob.lerpSpeed)
            : bobPos;
    }

    [System.Serializable]
    public struct HeadBobParams
    {
        public float frequency;
        public float amplitude;
        public float sideAmplitude;
        public float lerpSpeed;
    }
}
