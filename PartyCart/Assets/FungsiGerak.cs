using UnityEngine;

public class FungsiGerak : MonoBehaviour
{
    public float speed = 10f;
    public float turnSpeed = 100f;

    void Update()
    {
        // Mengambil input dari tombol W/S atau Panah Atas/Bawah
        float moveInput = Input.GetAxis("Vertical");
        // Mengambil input dari tombol A/D atau Panah Kiri/Kanan
        float turnInput = Input.GetAxis("Horizontal");

        // Menggerakkan mobil maju/mundur
        transform.Translate(Vector3.forward * moveInput * speed * Time.deltaTime);

        // Membuat mobil berbelok (hanya saat mobil sedang maju/mundur)
        if (moveInput != 0)
        {
            float direction = Mathf.Sign(moveInput); // Mengecek arah maju/mundur
            transform.Rotate(Vector3.up * turnInput * turnSpeed * Time.deltaTime * direction);
        }
    }
}