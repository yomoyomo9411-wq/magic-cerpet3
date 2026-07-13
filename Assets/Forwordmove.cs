using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    public float forwardSpeed = 10f;
    public float sideSpeed = 5f;

    void Update()
    {
        transform.Translate(Vector3.forward * forwardSpeed * Time.deltaTime);

        float input = Input.GetAxis("Horizontal");
        transform.Translate(Vector3.right * input * sideSpeed * Time.deltaTime);
    }
}