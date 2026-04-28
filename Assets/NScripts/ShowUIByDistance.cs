using UnityEngine;

public class ShowUIByDistance : MonoBehaviour
{
    public Transform player;
    public GameObject uiCanvas;
    public float activeDistance = 3f;

    [SerializeField] private MouseLook mouseLook;

    private bool isOpen = false;

    void Update()
    {
        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= activeDistance && !isOpen)
        {
            uiCanvas.SetActive(true);
            isOpen = true;

            if (mouseLook != null)
                mouseLook.UnlockCursor();
        }
        else if (distance > activeDistance && isOpen)
        {
            uiCanvas.SetActive(false);
            isOpen = false;

            if (mouseLook != null)
                mouseLook.LockCursor();
        }
    }
}