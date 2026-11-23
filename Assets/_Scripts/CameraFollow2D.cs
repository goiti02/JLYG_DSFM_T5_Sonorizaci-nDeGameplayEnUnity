using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    [Header("Objetivo a seguir")]
    public Transform target;

    [Header("Ajustes de seguimiento")]
    public float smoothTime = 0.3f; 
    public Vector3 offset = new Vector3(0f, 0f, -10f); 

    private Vector3 _velocity = Vector3.zero; 

    void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 targetPosition = target.position + offset;

        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _velocity, smoothTime);
    }
}
