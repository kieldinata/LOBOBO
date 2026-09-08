using UnityEngine;

public class SpriteShadow : MonoBehaviour
{
    [SerializeField] private Material litMaterial;

    void Awake()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (SpriteRenderer sr in renderers)
        {
            if (litMaterial != null)
            {
                sr.material = litMaterial;
            }
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            sr.receiveShadows = true;
        }
    }
}