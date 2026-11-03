using UnityEngine;

public class Camera_Holder : MonoBehaviour
{
    [SerializeField] Transform cam_pos;
    
    void Update()
    {
        transform.position = cam_pos.position;
    }
}
