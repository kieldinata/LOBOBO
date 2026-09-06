using UnityEngine;
using UnityEngine.InputSystem;

public class Flashlight : MonoBehaviour
{
    [Header("Setting")]
    public Key toggleKey = Key.F;
    public float range = 30f;
    public float spotAngle = 45f;
    public float intensity = 10f;
    public Color color = Color.white;
    public bool startOn = false;

    [Header("Sway")]
    [Tooltip("Semakin kecil = semakin lambat follow = semakin berguncang.")]
    public float smoothSpeed = 8f;

    private Light flashlightLight;
    private Transform flashlightTransform;
    private Quaternion delayedRotation;

    void Start()
    {
        GameObject go = new GameObject("FlashlightLight");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        flashlightTransform = go.transform;
        delayedRotation = transform.parent.rotation;
        flashlightLight = go.AddComponent<Light>();
        flashlightLight.type = LightType.Spot;
        flashlightLight.range = range;
        flashlightLight.spotAngle = spotAngle;
        flashlightLight.intensity = intensity;
        flashlightLight.color = color;
        flashlightLight.shadows = LightShadows.Soft;

        flashlightLight.enabled = startOn;
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            flashlightLight.enabled = !flashlightLight.enabled;

        if (flashlightTransform != null)
        {
            Quaternion targetWorldRot = transform.parent.rotation * Quaternion.Euler(transform.eulerAngles.x, 0f, 0f);
            delayedRotation = Quaternion.Slerp(
                delayedRotation,
                targetWorldRot,
                Time.deltaTime * smoothSpeed
            );
            flashlightTransform.rotation = delayedRotation;
        }
    }
}
