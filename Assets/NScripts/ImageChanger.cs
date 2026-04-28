using UnityEngine;
using UnityEngine.UI;

public class ImageChanger : MonoBehaviour
{
    public Image targetImage;
    public Button button;   // 버튼 추가

    public Sprite[] images;
    public int currentIndex = 0;

    void Start()
    {
        // 이미지 불러오기
        images = Resources.LoadAll<Sprite>("image");

        if (images.Length > 0)
        {
            targetImage.sprite = images[currentIndex];
        }

        // 🔥 버튼 클릭 시 ChangeImage 연결
        button.onClick.AddListener(ChangeImage);
    }

    public void ChangeImage()
    {
        currentIndex = (currentIndex + 1) % images.Length;
        targetImage.sprite = images[currentIndex];
    }
}