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

    private Light flashlightLight;

    void Start()
    {
        GameObject go = new GameObject("FlashlightLight");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        flashlightLight = go.AddComponent<Light>();
        flashlightLight.type = LightType.Spot;
        flashlightLight.range = range;
        flashlightLight.spotAngle = spotAngle;
        flashlightLight.intensity = intensity;
        flashlightLight.color = color;
        flashlightLight.shadows = LightShadows.None;

        flashlightLight.enabled = startOn;
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            flashlightLight.enabled = !flashlightLight.enabled;
    }
}
