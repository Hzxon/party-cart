using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target; // Objek yang akan diikuti (mobil)
    public Vector3 offset = new Vector3(0, 3, -6); // Posisi kamera (Y: atas, Z: belakang)
    public float followSpeed = 10f;

    void LateUpdate()
    {
        if (target == null) return;

        // Menghitung posisi yang seharusnya ditempati kamera (di belakang mobil)
        Vector3 targetPosition = target.position + target.TransformDirection(offset);
        
        // Membuat pergerakan kamera mulus
        transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);
        
        // Memaksa kamera untuk selalu menatap ke arah mobil
        transform.LookAt(target);
    }
}