using UnityEngine;

public class WasdMove : MonoBehaviour
{
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] bool useWorldSpace = true;

    void Update()
    {
        float x = 0f;
        float z = 0f;

        if (Input.GetKey(KeyCode.W)) z += 1f;
        if (Input.GetKey(KeyCode.S)) z -= 1f;
        if (Input.GetKey(KeyCode.A)) x -= 1f;
        if (Input.GetKey(KeyCode.D)) x += 1f;

        Vector3 dir = new Vector3(x, 0f, z);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        Vector3 delta = dir * moveSpeed * Time.deltaTime;
        if (useWorldSpace)
            transform.position += delta;
        else
            transform.Translate(delta, Space.Self);
    }
}
