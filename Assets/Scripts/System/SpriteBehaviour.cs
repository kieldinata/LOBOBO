using UnityEngine;

public class SpriteBehaviour : MonoBehaviour
{
    private void LateUpdate(){
        Vector3 cameraPos = Camera.main.transform.position;
        cameraPos.y = transform.position.y;
        transform.LookAt(cameraPos);
        transform.Rotate(0, 180, 0);
    }
}