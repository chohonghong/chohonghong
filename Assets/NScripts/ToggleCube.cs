using UnityEngine;

public class ToggleCube : MonoBehaviour
{
    private Renderer cubeRenderer;

    void Start()
    {
        cubeRenderer = GetComponent<Renderer>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            cubeRenderer.enabled = !cubeRenderer.enabled;
        }
    }
}