using UnityEngine;

[RequireComponent(typeof(CarController))]
public class PlayerInputController : MonoBehaviour
{
    private CarController car;

    private void Awake()
    {
        car = GetComponent<CarController>();
    }

    private void FixedUpdate()
    {
        float steer = Input.GetAxis("Horizontal");
        float throttle = Mathf.Max(0, Input.GetAxis("Vertical"));
        float brake = Mathf.Max(0, -Input.GetAxis("Vertical"));

        car.Move(steer, throttle, brake);
    }
}
