using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

[Serializable]
public class AtmosphereSceneData
{
    public string id;
    public string message;
    public string bgImage;
    public string bgColor;
    public string overlayColor;
    public string textColor;
}

[Serializable]
public class AtmosphereSceneCollection
{
    public AtmosphereSceneData[] scenes;
}

public class AtmosphereSceneGenerator : MonoBehaviour
{
    [Header("JSON Source")]
    [SerializeField] private bool useRemoteJson = false;
    [SerializeField] private string remoteJsonUrl = "http://127.0.0.1:8000/scenes.json";
    [SerializeField] private string localJsonFileName = "scenes.json";

    [Header("UI References")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image overlayImage;
    [SerializeField] private TMP_Text mainText;
    [SerializeField] private Button generateButton;

    [Header("Transition")]
    [SerializeField] private float transitionDuration = 2.5f;

    private AtmosphereSceneCollection sceneCollection;
    private int currentIndex = -1;

    private void Start()
    {
        if (generateButton != null)
        {
            generateButton.onClick.AddListener(OnClickGenerate);
        }
    }

    private void OnClickGenerate()
    {
        StartCoroutine(LoadAndApplyScene());
    }

    private IEnumerator LoadAndApplyScene()
    {
        string uri = GetJsonUri();

        using (UnityWebRequest request = UnityWebRequest.Get(uri))
        {
            yield return request.SendWebRequest();
            string jsonText = request.downloadHandler.text;

            sceneCollection = JsonUtility.FromJson<AtmosphereSceneCollection>(jsonText);

            if (sceneCollection == null || sceneCollection.scenes == null || sceneCollection.scenes.Length == 0)
                yield break;

            currentIndex = (currentIndex + 1) % sceneCollection.scenes.Length;
            AtmosphereSceneData nextScene = sceneCollection.scenes[currentIndex];

            yield return StartCoroutine(ApplyScene(nextScene));
        }
    }

    private string GetJsonUri()
    {
        if (useRemoteJson && !string.IsNullOrWhiteSpace(remoteJsonUrl))
        {
            return remoteJsonUrl;
        }

        string fullPath = Path.Combine(Application.streamingAssetsPath, localJsonFileName);
        return new Uri(fullPath).AbsoluteUri;
    }

    private IEnumerator ApplyScene(AtmosphereSceneData data)
    {
        if (mainText != null)
        {
            mainText.text = data.message;
        }

        if (backgroundImage != null && !string.IsNullOrWhiteSpace(data.bgImage))
        {
            Sprite loadedSprite = Resources.Load<Sprite>($"Backgrounds/{data.bgImage}");

            if (loadedSprite != null)
            {
                backgroundImage.sprite = loadedSprite;
                backgroundImage.preserveAspect = false;
            }
        }

        Color startBg = backgroundImage != null ? backgroundImage.color : Color.white;
        Color startOverlay = overlayImage != null ? overlayImage.color : Color.clear;
        Color startText = mainText != null ? mainText.color : Color.white;

        Color targetBg = ParseHtmlColor(data.bgColor, startBg);
        Color targetOverlay = ParseHtmlColor(data.overlayColor, startOverlay);
        Color targetText = ParseHtmlColor(data.textColor, startText);

        float time = 0f;

        while (time < transitionDuration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / transitionDuration);

            if (backgroundImage != null)
                backgroundImage.color = Color.Lerp(startBg, targetBg, t);

            if (overlayImage != null)
                overlayImage.color = Color.Lerp(startOverlay, targetOverlay, t);

            if (mainText != null)
                mainText.color = Color.Lerp(startText, targetText, t);

            yield return null;
        }

        if (backgroundImage != null) backgroundImage.color = targetBg;
        if (overlayImage != null) overlayImage.color = targetOverlay;
        if (mainText != null) mainText.color = targetText;
    }

    private Color ParseHtmlColor(string htmlColor, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(htmlColor))
            return fallback;

        if (ColorUtility.TryParseHtmlString(htmlColor, out Color parsed))
            return parsed;

        return fallback;
    }
}