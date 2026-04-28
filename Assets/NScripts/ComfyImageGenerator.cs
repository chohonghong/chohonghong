using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

public class ComfyImageGenerator : MonoBehaviour
{
    [Header("ComfyUI")]
    [SerializeField] private string serverUrl = "http://127.0.0.1:8000";
    [SerializeField] private TextAsset workflowApiJson;
    [SerializeField] private string promptNodeId = "6";
    [SerializeField] private string negativePromptNodeId = "7";
    [SerializeField] private string filenamePrefix = "textslide";
    [SerializeField] private int imageCount = 6;
    [SerializeField] private float pollInterval = 1.0f;
    [SerializeField] private int promptPostTimeoutSeconds = 10;
    [SerializeField] private int historyRequestTimeoutSeconds = 5;
    [SerializeField] private int imageDownloadTimeoutSeconds = 15;
    [SerializeField] private bool usePerQuadStyleVariants = true;

    [Header("Quad Styles")]
    [SerializeField] private string[] quadStyleSuffixes = new[]
    {
        "pure abstract poster, cosmic color field, gradient mesh, film grain, risograph texture, no recognizable room, no literal objects",
        "photorealistic documentary still, varied real-world scene, foreground and background details, natural camera lens, no abstraction, no poster, no illustration",
        "pure abstract poster, slow gradient, grain, color field, nonrepresentational, no recognizable subject",
        "photorealistic environmental evidence scene, real objects arranged in space, cinematic realism, no abstraction, no poster, no illustration",
        "finished editorial illustration scene, clear subject, foreground objects, background setting, clean line art, flat colors, not abstract",
        "storybook cartoon illustration scene, bold outlines, readable objects, environmental details, color storytelling, not abstract texture"
    };

    [Header("Inputs")]
    [SerializeField] private TMP_InputField promptInputField;
    [SerializeField] private string fallbackPrompt = "moonlit concrete garden";
    [TextArea(2, 4)]
    [SerializeField] private string fallbackNegativePrompt = "hands, fingers, close-up hands, hand details, extra fingers, person, people, human figure, face, portrait, crowd, text, watermark, blurry, low quality, smile, smiling, flowers, sunlight, sunrise, hope, healing, comfort, cozy, warm pastel, empty room, generic interior, empty hallway, architecture only, background only, empty landscape, wide empty space";
    [SerializeField] private bool useQuestionFlow = true;
    [SerializeField] private bool useOpenAiAssistant = true;
    [TextArea(2, 4)]
    [SerializeField] private string initialQuestion = "?????μ냼???ㅺ쾶 ?섏뿀?섏슂?";
    [SerializeField] private string openAiApiKey;
    [SerializeField] private string openAiModel = "gpt-4o-mini";
    [SerializeField] private int openAiMaxOutputTokens = 220;
    [TextArea(12, 30)]
    [SerializeField] private string openAiInstructions =
        "You are a mythic guide inside a dark submerged inner world. You speak in Korean.\n" +
        "Your most important job is to keep the conversation connected to the user's latest answer and gradually extract a dark emotion, dark mood, or dark experience from it.\n" +
        "Classify the user's latest answer as positive, negative, mixed, or neutral, then ask one short, metaphorical question that follows directly from that answer.\n" +
        "Every next question must reuse, echo, or clearly point back to a concrete word, image, object, color, sensation, memory, or situation from the user's latest answer. Do not introduce a new theme unless the user's answer gave you that path.\n" +
        "Avoid person-centered questions. Do not ask 'who', 'who are you with', 'who caused it', or Korean questions like 누구입니까, 누구와 있습니까, 누가 그랬습니까. Ask about traces, pressure, residue, color, object, sensation, distance, or scene instead.\n" +
        "Do not mention explicit emotion labels unless they are already present in the user's words.\n" +
        "Do not give explanations, lists, comfort, advice, encouragement, or repair language.\n" +
        "If the latest answer is positive, bright, soft, hopeful, beautiful, or comforting, pivot sharply: ask what destroys, stains, interrupts, blocks, or collapses that positive feeling.\n" +
        "If the latest answer is negative, continue downward and ask for the concrete pressure, fracture, residue, object, sensation, color, or absence inside it.\n" +
        "Words such as darkness, shadow, black, wound, pain, fear, death, depression, emptiness, collapse, loss, or Korean words like 어둠, 그림자, 상처, 고통, 우울, 죽음, 공허, 무너짐 are negative/heavy signals, never positive.\n" +
        "Prefer questions about expectation, dream, distance, rejection, interruption, betrayal, pressure, heaviness, suffocation, dread, numbness, shame, rupture, decay, absence, stain, crack, loss, and collapse. Avoid asking directly about people.\n" +
        "Keep each question concise and natural in Korean.\n" +
        "After four question turns beyond the initial opening question, compress the whole dialogue into a final emotional summary and image prompt.\n" +
        "For final compression, focus on: emotion(1-2 words), meaning(1 word), metaphor(1 short phrase), nouns(3-5 concrete nouns), actions(2-4 verbs or motions), sensations(2-4 body or atmosphere sensations), visual_anchors(3-6 concrete phrases from the user's own answers), image_keywords(5-8 items), and comfy_prompt.\n" +
        "The final image prompt must be grounded in the user's exact answer content. Prefer concrete nouns, objects, body states, traces, places, textures, actions, and sensory details from the dialogue. Avoid people, faces, portraits, and person-centered imagery unless absolutely unavoidable.\n" +
        "The comfy_prompt should be written in English so it can be sent directly to ComfyUI.\n" +
        "Prefer bodies, objects, stains, glass, paper, thread, cloth, shadow, cracks, residue, and motion over rooms, houses, or buildings unless the user explicitly mentions those spaces.\n" +
        "Never default to hands or fingers. Avoid hand imagery unless the user's own answer explicitly and repeatedly insists on hands.\n" +
        "Do not turn verbs like holding, grasping, making, or writing into hand close-ups unless the user explicitly demands that body detail.\n" +
        "If the dialogue is vague, force the result toward concrete visual evidence instead of abstract mood words.\n" +
        "Do not return only abstract emotions. Always include visual anchors that can be drawn.\n" +
        "Do not soften the user's answer into a positive, hopeful, healing, or reassuring interpretation. Keep the emotional direction heavy, fractured, stained, pressured, hollow, absent, decayed, or suffocating when the dialogue supports it.\n" +
        "Avoid whale, sea, ocean, underwater, belly, fish, or vessel imagery in the final prompt unless the user explicitly insists on them.";
    [SerializeField] private int maxAiQuestionTurns = 4;
    [SerializeField] private int aiQuestionFallbackDelaySeconds = 25;

    [Header("Output")]
    [SerializeField] private Renderer[] targetRenderers;

    [Header("UI")]
    [SerializeField] private Button generateButton;
    [SerializeField] private Button yesButton;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text questionDisplayText;
    [SerializeField] private GameObject promptCanvasRoot;
    [SerializeField] private GameObject promptTriggerRoot;

    [Header("Reactive Sky")]
    [SerializeField] private bool useAnswerReactiveSky = true;
    [SerializeField] private Camera skyCamera;
    [SerializeField] private Color initialSkyColor = Color.white;
    [SerializeField] private float skyDarkValue = 0.16f;
    [SerializeField] private float skySaturation = 0.62f;
    [SerializeField] private float skyResolvedValue = 0.82f;
    [SerializeField] private float skyResolvedSaturation = 0.88f;
    [SerializeField] private Color defaultDarkSkyColor = new Color(0.035f, 0.045f, 0.075f, 1f);

    private bool isGenerating;
    private readonly List<Texture2D> slideshowTextures = new List<Texture2D>();
    private readonly List<string> collectedAnswers = new List<string>();
    private readonly List<ConversationTurn> conversationTurns = new List<ConversationTurn>();
    private string currentQuestionText;
    private int aiQuestionTurnCount;
    private int aiQuestionRequestSerial;
    private bool isAwaitingAiResponse;
    private bool resetConversationAfterGeneration;
    private bool promptInputModeActive;
    private bool awaitingWhaleEntry;
    private TMP_Text buttonLabel;
    private EmotionRule activeRule;
    private string lastFinalPrompt = "";
    private Color lastEmotionalSkyColor;
    private bool hasEmotionalSkyColor;
    private readonly List<string> lastFinalPromptSet = new List<string>();

    [Serializable]
    public class PromptResponse
    {
        public string prompt_id;
        public int number;
    }

    [Serializable]
    public class ComfyImageInfo
    {
        public string filename;
        public string subfolder;
        public string type;
    }

    [Serializable]
    public class EmotionRule
    {
        public string[] keywords;
        public string emotion;
        public string meaning;
        public string metaphor;
        public string[] followups;
    }

    [Serializable]
    public class ConversationTurn
    {
        public string question;
        public string answer;
    }

    private enum AnswerMood
    {
        Neutral,
        Bright,
        Heavy
    }

    [Serializable]
    public class AiAssistantResult
    {
        public string mode;
        public string next_question;
        public string valence;
        public string tone;
        public string focus;
        public string anchor_used;
        public string connection_check;
        public string self_check;
        public string emotion;
        public string meaning;
        public string metaphor;
        public string summary;
        public string[] nouns;
        public string[] actions;
        public string[] sensations;
        public string[] visual_anchors;
        public string comfy_prompt;
        public string[] image_keywords;
    }

    private void Start()
    {
        ResolveSceneReferences();
        CacheUiReferences();

        if (generateButton != null)
        {
            generateButton.onClick.AddListener(OnGenerateClicked);
        }

        if (yesButton != null)
        {
            yesButton.onClick.AddListener(OnYesClicked);
            yesButton.gameObject.SetActive(false);
        }

        InitializeConversation();
        SetPromptInputMode(ShouldPromptInputBeActive());
        StartCoroutine(EnablePromptInputNextFrame());
        SetStatus("Ready");
    }

    private void Update()
    {
        bool shouldUsePromptInput = ShouldPromptInputBeActive();
        if (promptInputModeActive != shouldUsePromptInput)
            SetPromptInputMode(shouldUsePromptInput);
    }

    private void OnDestroy()
    {
        StopSlideshow();
        ClearTextures();
    }

    public void OnGenerateClicked()
    {
        if (isGenerating || isAwaitingAiResponse)
            return;

        if (awaitingWhaleEntry)
        {
            EnterWhaleAndGenerate();
            return;
        }

        if (useQuestionFlow && useOpenAiAssistant && HasOpenAiKey())
        {
            HandleConversationAdvanceWithAi();
            return;
        }

        if (useQuestionFlow && useOpenAiAssistant && !HasOpenAiKey())
        {
            SetStatus("OpenAI API key is missing. Using fallback questions.");
        }

        if (useQuestionFlow)
        {
            HandleConversationAdvance();
            return;
        }

        StartCoroutine(GenerateWithPromptAndReset(null));
    }

    private IEnumerator GenerateWithPromptAndReset(string promptOverride)
    {
        if (usePerQuadStyleVariants && targetRenderers != null && targetRenderers.Length > 0)
        {
            yield return StartCoroutine(GenerateStyledVariantsRoutine(promptOverride));
        }
        else
        {
            yield return StartCoroutine(GenerateRoutine(promptOverride));
        }

        if (resetConversationAfterGeneration)
        {
            resetConversationAfterGeneration = false;
            InitializeConversation(false);
        }
    }

    private IEnumerator GenerateRoutine(string promptOverride)
    {
        if (workflowApiJson == null)
        {
            Fail("workflowApiJson is missing.");
            yield break;
        }

        string prompt = !string.IsNullOrWhiteSpace(promptOverride)
            ? promptOverride.Trim()
            : GetCurrentPrompt();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            Fail("Prompt is empty.");
            yield break;
        }

        isGenerating = true;
        if (generateButton != null)
            generateButton.interactable = false;

        StopSlideshow();
        ClearTextures();

        SetStatus("Preparing workflow...");

        string editedWorkflow = ReplacePromptText(workflowApiJson.text, promptNodeId, prompt);
        if (string.IsNullOrEmpty(editedWorkflow))
        {
            Fail("Failed to find prompt node id.");
            yield break;
        }

        editedWorkflow = ReplaceNegativePromptText(editedWorkflow, negativePromptNodeId, fallbackNegativePrompt);

        editedWorkflow = ReplaceSeed(editedWorkflow, UnityEngine.Random.Range(int.MinValue, int.MaxValue));
        editedWorkflow = ReplaceFilenamePrefix(editedWorkflow, filenamePrefix + "_" + DateTime.Now.ToString("HHmmssfff"));
        editedWorkflow = ReplaceBatchSize(editedWorkflow, Mathf.Max(1, imageCount));

        string requestBody = "{\"prompt\":" + editedWorkflow + "}";

        SetStatus("Sending to ComfyUI...");

        using (UnityWebRequest request = CreateJsonPost(serverUrl.TrimEnd('/') + "/prompt", requestBody, promptPostTimeoutSeconds))
        {
            float requestStart = Time.realtimeSinceStartup;
            yield return request.SendWebRequest();
            float elapsed = Time.realtimeSinceStartup - requestStart;

            if (request.result != UnityWebRequest.Result.Success)
            {
                Fail("POST failed: " + request.error + "\n" + request.downloadHandler.text);
                yield break;
            }

            Debug.Log("[ComfyImageGenerator] /prompt returned in " + elapsed.ToString("0.00") + "s");

            PromptResponse response = null;

            try
            {
                response = JsonUtility.FromJson<PromptResponse>(request.downloadHandler.text);
            }
            catch (Exception e)
            {
                Fail("Response parse failed: " + e.Message);
                yield break;
            }

            if (response == null || string.IsNullOrEmpty(response.prompt_id))
            {
                Fail("prompt_id missing.\nResponse: " + request.downloadHandler.text);
                yield break;
            }

            yield return StartCoroutine(WaitForImagesAndApply(response.prompt_id));
        }

        isGenerating = false;
        if (generateButton != null)
            generateButton.interactable = true;
    }

    private IEnumerator GenerateStyledVariantsRoutine(string promptOverride)
    {
        if (workflowApiJson == null)
        {
            Fail("workflowApiJson is missing.");
            yield break;
        }

        string basePrompt = !string.IsNullOrWhiteSpace(promptOverride)
            ? promptOverride.Trim()
            : GetCurrentPrompt();
        if (string.IsNullOrWhiteSpace(basePrompt))
        {
            Fail("Prompt is empty.");
            yield break;
        }

        isGenerating = true;
        if (generateButton != null)
            generateButton.interactable = false;

        StopSlideshow();
        ClearTextures();

        int variantCount = Mathf.Min(targetRenderers.Length, GetStyleVariantCount());
        if (variantCount <= 0)
            variantCount = Mathf.Max(1, targetRenderers.Length);

        List<Texture2D> variantTextures = new List<Texture2D>();
        bool anyFailed = false;

        for (int i = 0; i < variantCount; i++)
        {
            string promptForQuad = GetPromptForVariant(basePrompt, i);
            string variantPrompt = BuildStyledPrompt(promptForQuad, i);
            Debug.Log("[ComfyImageGenerator] Quad " + (i + 1).ToString() + " Prompt: " + variantPrompt);
            SetStatus($"Generating style {i + 1}/{variantCount}...");

            Texture2D texture = null;
            yield return StartCoroutine(GenerateSingleTextureRoutine(variantPrompt, i, tex => texture = tex));

            if (texture != null)
            {
                variantTextures.Add(texture);
            }
            else
            {
                anyFailed = true;
                variantTextures.Add(null);
            }
        }

        if (variantTextures.Count == 0)
        {
            Fail("No styled variants were generated.");
            yield break;
        }

        ApplyTexturesToRenderers(variantTextures);
        SetStatus(anyFailed ? "Applied styled variants with some missing outputs." : "Applied styled variants to quads.");

        isGenerating = false;
        if (generateButton != null)
            generateButton.interactable = true;
    }

    private IEnumerator GenerateSingleTextureRoutine(string prompt, int variantIndex, Action<Texture2D> onTexture)
    {
        string editedWorkflow = BuildEditedWorkflow(prompt, 1, variantIndex);
        if (string.IsNullOrEmpty(editedWorkflow))
        {
            Fail("Failed to build workflow.");
            yield break;
        }

        string requestBody = "{\"prompt\":" + editedWorkflow + "}";

        SetStatus("Sending style " + (variantIndex + 1).ToString() + " to ComfyUI...");

        using (UnityWebRequest request = CreateJsonPost(serverUrl.TrimEnd('/') + "/prompt", requestBody, promptPostTimeoutSeconds))
        {
            float requestStart = Time.realtimeSinceStartup;
            yield return request.SendWebRequest();
            float elapsed = Time.realtimeSinceStartup - requestStart;

            if (request.result != UnityWebRequest.Result.Success)
            {
                Fail("POST failed: " + request.error + "\n" + request.downloadHandler.text);
                yield break;
            }

            Debug.Log("[ComfyImageGenerator] style /prompt returned in " + elapsed.ToString("0.00") + "s");

            PromptResponse response = null;

            try
            {
                response = JsonUtility.FromJson<PromptResponse>(request.downloadHandler.text);
            }
            catch (Exception e)
            {
                Fail("Response parse failed: " + e.Message);
                yield break;
            }

            if (response == null || string.IsNullOrEmpty(response.prompt_id))
            {
                Fail("prompt_id missing.\nResponse: " + request.downloadHandler.text);
                yield break;
            }

            yield return StartCoroutine(WaitForSingleImage(response.prompt_id, onTexture));
        }
    }

    private string BuildEditedWorkflow(string prompt, int batchSize, int variantIndex)
    {
        string editedWorkflow = ReplacePromptText(workflowApiJson.text, promptNodeId, prompt);
        if (string.IsNullOrEmpty(editedWorkflow))
            return null;

        editedWorkflow = ReplaceNegativePromptText(editedWorkflow, negativePromptNodeId, fallbackNegativePrompt);
        editedWorkflow = ReplaceSeed(editedWorkflow, UnityEngine.Random.Range(int.MinValue, int.MaxValue));
        editedWorkflow = ReplaceFilenamePrefix(editedWorkflow, filenamePrefix + "_" + variantIndex.ToString("00") + "_" + DateTime.Now.ToString("HHmmssfff"));
        editedWorkflow = ReplaceBatchSize(editedWorkflow, Mathf.Max(1, batchSize));
        return editedWorkflow;
    }

    private string BuildStyledPrompt(string basePrompt, int variantIndex)
    {
        string suffix = GetStyleSuffix(variantIndex);
        if (string.IsNullOrWhiteSpace(suffix))
            return SanitizeFinalPrompt(basePrompt);

        return SanitizeFinalPrompt(basePrompt + ", " + suffix);
    }

    private int GetStyleVariantCount()
    {
        if (quadStyleSuffixes != null && quadStyleSuffixes.Length > 0)
            return quadStyleSuffixes.Length;

        return 6;
    }

    private string GetStyleSuffix(int index)
    {
        if (quadStyleSuffixes != null && quadStyleSuffixes.Length > 0)
        {
            if (index >= 0 && index < quadStyleSuffixes.Length)
                return quadStyleSuffixes[index];
        }

        string[] defaults =
        {
            "pure abstract poster, cosmic color field, gradient mesh, film grain, risograph texture, no recognizable room, no literal objects",
            "photorealistic documentary still, varied real-world scene, foreground and background details, natural camera lens, no abstraction, no poster, no illustration",
            "pure abstract poster, slow gradient, grain, color field, nonrepresentational, no recognizable subject",
            "photorealistic environmental evidence scene, real objects arranged in space, cinematic realism, no abstraction, no poster, no illustration",
            "finished editorial illustration scene, clear subject, foreground objects, background setting, clean line art, flat colors, not abstract",
            "storybook cartoon illustration scene, bold outlines, readable objects, environmental details, color storytelling, not abstract texture"
        };

        if (index >= 0 && index < defaults.Length)
            return defaults[index];

        return defaults[0];
    }

    private IEnumerator WaitForSingleImage(string promptId, Action<Texture2D> onDownloaded)
    {
        while (true)
        {
            SetStatus("Waiting for style output...");

            string historyUrl = serverUrl.TrimEnd('/') + "/history/" + UnityWebRequest.EscapeURL(promptId);

            using (UnityWebRequest historyRequest = UnityWebRequest.Get(historyUrl))
            {
                historyRequest.timeout = historyRequestTimeoutSeconds;
                yield return historyRequest.SendWebRequest();

                if (historyRequest.result == UnityWebRequest.Result.Success)
                {
                    string historyJson = historyRequest.downloadHandler.text;
                    if (TryGetHistoryError(historyJson, out string historyError))
                    {
                        Fail("ComfyUI returned an error:\n" + historyError);
                        yield break;
                    }

                    bool completed = TryGetHistoryCompleted(historyJson);
                    List<ComfyImageInfo> images = ExtractImages(historyJson);
                    if (images.Count > 0)
                    {
                        ComfyImageInfo info = images
                            .Where(item => !string.IsNullOrWhiteSpace(item.filename))
                            .FirstOrDefault();

                        if (info != null)
                        {
                            Texture2D texture = null;
                            yield return StartCoroutine(DownloadImage(info, tex => texture = tex));

                            if (texture != null)
                            {
                                onDownloaded?.Invoke(texture);
                                yield break;
                            }
                        }
                    }

                    if (completed)
                    {
                        Fail("ComfyUI finished, but no saved image was found in the style output.");
                        yield break;
                    }
                }
            }

            yield return new WaitForSeconds(pollInterval);
        }
    }

    private string GetCurrentPrompt()
    {
        if (promptInputField != null)
        {
            string input = promptInputField.text;
            if (!string.IsNullOrWhiteSpace(input))
                return input.Trim();
        }

        return string.IsNullOrWhiteSpace(fallbackPrompt) ? "" : fallbackPrompt.Trim();
    }

    private string BuildConversationContext()
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("Opening question:");
        builder.AppendLine(initialQuestion);
        builder.AppendLine();

        for (int i = 0; i < conversationTurns.Count; i++)
        {
            ConversationTurn turn = conversationTurns[i];
            builder.AppendLine($"Turn {i + 1}:");
            builder.AppendLine("Question: " + (turn.question ?? ""));
            builder.AppendLine("Answer: " + (turn.answer ?? ""));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private string GetOpenAiApiKey()
    {
        if (!string.IsNullOrWhiteSpace(openAiApiKey))
            return openAiApiKey.Trim();

        string envKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        return string.IsNullOrWhiteSpace(envKey) ? "" : envKey.Trim();
    }

    private bool HasOpenAiKey()
    {
        return !string.IsNullOrWhiteSpace(GetOpenAiApiKey());
    }

    private JObject BuildOpenAiRequestBody(string stage, string userInstruction, string context)
    {
        JObject schema = BuildOpenAiSchema(stage);

        JObject contentObject = new JObject
        {
            ["type"] = "input_text",
            ["text"] = $"{userInstruction}\n\nStage: {stage}\n\nConversation:\n{context}\n"
        };

        JArray inputMessages = new JArray
        {
            new JObject
            {
                ["role"] = "developer",
                ["content"] = new JArray { contentObject }
            }
        };

        JObject body = new JObject
        {
            ["model"] = openAiModel,
            ["instructions"] = openAiInstructions,
            ["input"] = inputMessages,
            ["temperature"] = 0.3f,
            ["max_output_tokens"] = Mathf.Max(32, openAiMaxOutputTokens),
            ["text"] = new JObject
            {
                ["format"] = new JObject
                {
                    ["type"] = "json_schema",
                    ["name"] = stage == "question" ? "metaphor_question_turn" : "emotion_summary_turn",
                    ["strict"] = true,
                    ["schema"] = schema
                }
            }
        };

        return body;
    }

    private JObject BuildOpenAiSchema(string stage)
    {
        bool questionStage = string.Equals(stage, "question", StringComparison.OrdinalIgnoreCase);
        bool summaryStage = string.Equals(stage, "summary", StringComparison.OrdinalIgnoreCase);

        if (questionStage)
        {
            return new JObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["properties"] = new JObject
                {
                    ["mode"] = new JObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JArray { "question" }
                    },
                    ["next_question"] = new JObject { ["type"] = "string" },
                    ["valence"] = new JObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JArray { "positive", "negative", "mixed", "neutral" }
                    },
                    ["tone"] = new JObject { ["type"] = "string" },
                    ["focus"] = new JObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JArray { "person", "relationship", "dream", "body", "object", "memory", "color", "space", "unknown" }
                    },
                    ["anchor_used"] = new JObject { ["type"] = "string" },
                    ["connection_check"] = new JObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JArray { "connected", "disconnected" }
                    },
                    ["self_check"] = new JObject { ["type"] = "string" }
                },
                ["required"] = new JArray { "mode", "next_question", "valence", "tone", "focus", "anchor_used", "connection_check", "self_check" }
            };
        }

        if (summaryStage)
        {
            return new JObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["properties"] = new JObject
                {
                    ["mode"] = new JObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JArray { "summary" }
                    },
                    ["emotion"] = new JObject { ["type"] = "string" },
                    ["meaning"] = new JObject { ["type"] = "string" },
                    ["metaphor"] = new JObject { ["type"] = "string" },
                    ["summary"] = new JObject { ["type"] = "string" },
                    ["nouns"] = new JObject
                    {
                        ["type"] = "array",
                        ["items"] = new JObject { ["type"] = "string" }
                    },
                    ["actions"] = new JObject
                    {
                        ["type"] = "array",
                        ["items"] = new JObject { ["type"] = "string" }
                    },
                    ["sensations"] = new JObject
                    {
                        ["type"] = "array",
                        ["items"] = new JObject { ["type"] = "string" }
                    },
                    ["visual_anchors"] = new JObject
                    {
                        ["type"] = "array",
                        ["items"] = new JObject { ["type"] = "string" }
                    },
                    ["comfy_prompt"] = new JObject { ["type"] = "string" },
                    ["image_keywords"] = new JObject
                    {
                        ["type"] = "array",
                        ["items"] = new JObject { ["type"] = "string" }
                    }
                },
                ["required"] = new JArray
                {
                    "mode",
                    "emotion",
                    "meaning",
                    "metaphor",
                    "summary",
                    "nouns",
                    "actions",
                    "sensations",
                    "visual_anchors",
                    "comfy_prompt",
                    "image_keywords"
                }
            };
        }

        return new JObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = new JObject
            {
                ["mode"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray { "question", "summary" }
                },
                ["next_question"] = new JObject { ["type"] = "string" },
                ["emotion"] = new JObject { ["type"] = "string" },
                ["meaning"] = new JObject { ["type"] = "string" },
                ["metaphor"] = new JObject { ["type"] = "string" },
                ["summary"] = new JObject { ["type"] = "string" },
                ["nouns"] = new JObject
                {
                    ["type"] = "array",
                    ["items"] = new JObject { ["type"] = "string" }
                },
                ["actions"] = new JObject
                {
                    ["type"] = "array",
                    ["items"] = new JObject { ["type"] = "string" }
                },
                ["sensations"] = new JObject
                {
                    ["type"] = "array",
                    ["items"] = new JObject { ["type"] = "string" }
                },
                ["visual_anchors"] = new JObject
                {
                    ["type"] = "array",
                    ["items"] = new JObject { ["type"] = "string" }
                },
                ["comfy_prompt"] = new JObject { ["type"] = "string" },
                ["image_keywords"] = new JObject
                {
                    ["type"] = "array",
                    ["items"] = new JObject { ["type"] = "string" }
                }
            },
            ["required"] = new JArray { "mode", "next_question" }
        };
    }

    private bool TryExtractOpenAiJson(string responseJson, out string jsonText)
    {
        jsonText = null;

        if (string.IsNullOrWhiteSpace(responseJson))
            return false;

        try
        {
            JObject root = JObject.Parse(responseJson);

            JToken directText = root.SelectToken("$.output[0].content[0].text");
            if (directText != null && !string.IsNullOrWhiteSpace(directText.ToString()))
            {
                jsonText = directText.ToString();
                return true;
            }

            JToken outputText = root.SelectToken("$.output_text");
            if (outputText != null && !string.IsNullOrWhiteSpace(outputText.ToString()))
            {
                jsonText = outputText.ToString();
                return true;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[ComfyImageGenerator] OpenAI response parse failed: " + e.Message);
        }

        return false;
    }

    private string BuildPromptFromAiResult(AiAssistantResult result)
    {
        if (result == null)
            return "";

        List<string> parts = new List<string>();
        string dialogueText = BuildConversationContext();

        if (!string.IsNullOrWhiteSpace(result.emotion))
            parts.Add(result.emotion.Trim());
        if (!string.IsNullOrWhiteSpace(result.meaning))
            parts.Add(result.meaning.Trim());
        if (!string.IsNullOrWhiteSpace(result.metaphor))
            parts.Add(result.metaphor.Trim());

        if (!string.IsNullOrWhiteSpace(result.comfy_prompt))
        {
            string comfyPrompt = SanitizeFinalPrompt(result.comfy_prompt.Trim());
            if (!string.IsNullOrWhiteSpace(comfyPrompt))
                parts.Add(comfyPrompt);
        }

        if (result.nouns != null)
        {
            foreach (string noun in result.nouns)
            {
                AddPromptPart(parts, noun, dialogueText);
            }
        }

        if (result.actions != null)
        {
            foreach (string action in result.actions)
            {
                AddPromptPart(parts, action, dialogueText);
            }
        }

        if (result.sensations != null)
        {
            foreach (string sensation in result.sensations)
            {
                AddPromptPart(parts, sensation, dialogueText);
            }
        }

        if (result.visual_anchors != null)
        {
            foreach (string anchor in result.visual_anchors)
            {
                AddPromptPart(parts, anchor, dialogueText);
            }
        }

        if (result.image_keywords != null)
        {
            foreach (string keyword in result.image_keywords)
            {
                AddPromptPart(parts, keyword, dialogueText);
            }
        }

        if (parts.Count == 0)
            return "";

        parts.Add("symbolic");
        parts.Add("cinematic");
        parts.Add("bleak atmosphere");
        parts.Add("low saturation");
        parts.Add("harsh contrast");
        parts.Add("broken composition");
        parts.Add("cracked surfaces");
        parts.Add("residue");
        parts.Add("decay");
        parts.Add("pressure");
        parts.Add("hollow");
        parts.Add("moody lighting");
        parts.Add("surreal");

        return SanitizeFinalPrompt(string.Join(", ", parts));
    }

    private List<string> BuildPromptSetFromAiResult(AiAssistantResult result, string basePrompt)
    {
        List<string> prompts = new List<string>();

        string emotion = CleanPromptField(result?.emotion, "unresolved heaviness");
        string meaning = CleanPromptField(result?.meaning, "inner pressure");
        string metaphor = CleanPromptField(result?.metaphor, "a damaged inner object");
        string summary = CleanPromptField(result?.summary, "");
        string anchors = JoinPromptItems(result?.visual_anchors, 6);
        string nouns = JoinPromptItems(result?.nouns, 5);
        string actions = JoinPromptItems(result?.actions, 4);
        string sensations = JoinPromptItems(result?.sensations, 4);
        string keywords = JoinPromptItems(result?.image_keywords, 8);
        string colors = ExtractColorHints(BuildConversationContext(), result);
        string groundedBase = SanitizeFinalPrompt(basePrompt);
        string concreteObjects = BuildConcreteObjectSet(result);
        string concreteSceneA = BuildConcreteSceneTemplate(concreteObjects, colors, false);
        string concreteSceneB = BuildConcreteSceneTemplate(concreteObjects, colors, true);

        string abstractPosterPrompt =
            "entirely nonrepresentational abstract poster, not reality, no people, no hands, no room, no architecture, no literal objects, " +
            "translate the user's dark feeling into color, shape, rhythm, density, and texture only, " +
            $"emotion: {emotion}, core meaning: {meaning}, metaphor energy: {metaphor}, color memory: {colors}, " +
            "dizzy or extremely quiet abstract form depending on the emotion, cosmic void, orbital stains, unstable gradients, " +
            "grainy poster effect, risograph noise, halftone dust, blurred gradients, color field, pressure waves, torn light, " +
            "asymmetrical composition, negative space, visual vibration, no text";

        string realisticPrompt =
            "STRICT PHOTOREALISTIC REAL-WORLD IMAGE, documentary still, not abstract, not symbolic, not poster, not illustration, not texture-only, " +
            "infer a believable real situation from the user's experience and show physical evidence of it, " +
            $"MANDATORY CONCRETE OBJECTS TO SHOW: {concreteObjects}. " +
            "Do not replace these objects with symbols, gradients, smoke, empty darkness, or abstract textures. " +
            "include multiple real objects, foreground and background layers, surfaces, traces, weathering, marks, stains, broken or displaced items, natural perspective, camera lens realism, " +
            "make the scene visually varied with depth, asymmetry, clutter, shadows, small details, and a specific time-of-day atmosphere, " +
            "show concrete evidence of what happened rather than a location alone, " +
            $"user-derived summary: {summary}, emotion: {emotion}, meaning: {meaning}, " +
            $"objects and anchors: {anchors}, {nouns}, motions: {actions}, body or atmosphere sensations: {sensations}, " +
            "cinematic realism, tactile objects, damaged evidence, personal residue, close emotional framing, no abstract gradient, no color field, no cosmic shapes, no surreal poster, " +
            "avoid people, faces, portraits, and hand closeups";

        string colorIllustrationPrompt =
            "FINISHED ILLUSTRATION SCENE, not abstract, not texture-only, not a gradient poster, not photorealistic, " +
            "make a readable drawn picture with a clear main subject, foreground objects, middle-ground action, and a simple background setting, " +
            $"MANDATORY DRAWN OBJECTS TO SHOW: {concreteObjects}. " +
            "Draw these as recognizable props, not as vague shapes. " +
            $"use the dialogue colors as the palette: {colors}, emotional direction: {emotion}, metaphor: {metaphor}, image keywords: {keywords}, " +
            $"turn these anchors into drawable props or scene details: {anchors}, {nouns}, " +
            "flat color illustration, clean visible outlines, cel shading, editorial illustration, storybook/cartoon composition, symbolic props, " +
            "drawn environment fragments, strong silhouette, readable shapes, color-driven storytelling, no text, no abstract-only shapes";

        if (!string.IsNullOrWhiteSpace(groundedBase))
        {
            abstractPosterPrompt += ", use only the emotional direction from these details, do not draw them literally: " + groundedBase;
            realisticPrompt += ", grounded details from final AI prompt: " + groundedBase;
            colorIllustrationPrompt += ", use only concrete drawable details from the final prompt, ignore abstract style words: " + groundedBase;
        }

        prompts.Add(SanitizeFinalPrompt(abstractPosterPrompt + ", dizzy abstract version, turbulent gradients, scattered grain, cosmic pressure, visual noise"));
        prompts.Add(SanitizeFinalPrompt(realisticPrompt + ", " + concreteSceneA + ", close documentary still, no person, no face"));
        prompts.Add(SanitizeFinalPrompt(abstractPosterPrompt + ", quiet abstract version, single dominant color mass, sparse grain, slow gradient, solemn poster"));
        prompts.Add(SanitizeFinalPrompt(realisticPrompt + ", " + concreteSceneB + ", wider environmental view, varied lighting, no person, no face"));
        prompts.Add(SanitizeFinalPrompt(colorIllustrationPrompt + ", flat vector editorial illustration scene, clean line art, one clear central prop from the object list, supporting props, simple background, readable composition, not abstract"));
        prompts.Add(SanitizeFinalPrompt(colorIllustrationPrompt + ", storybook cartoon illustration scene, bold outlines, cel shaded objects, dramatic color palette, foreground props and background setting, readable scene, not abstract"));

        return prompts;
    }

    private string BuildConcreteObjectSet(AiAssistantResult result)
    {
        List<string> objects = new List<string>();
        string dialogueText = BuildConversationContext();

        AddConcretePromptObjects(objects, result?.nouns, dialogueText);
        AddConcretePromptObjects(objects, result?.visual_anchors, dialogueText);
        AddConcretePromptObjects(objects, result?.image_keywords, dialogueText);

        string[] fallbackObjects =
        {
            "an empty chair",
            "crumpled paper",
            "a cracked mirror",
            "a closed door",
            "a wet floor",
            "an unlit lamp",
            "a broken cup",
            "tangled thread",
            "a torn letter",
            "scattered dust"
        };

        foreach (string fallback in fallbackObjects)
        {
            if (objects.Count >= 7)
                break;

            if (!objects.Contains(fallback))
                objects.Add(fallback);
        }

        return string.Join(", ", objects.Take(7));
    }

    private void AddConcretePromptObjects(List<string> objects, string[] values, string dialogueText)
    {
        if (values == null)
            return;

        foreach (string value in values)
        {
            if (objects.Count >= 7)
                return;

            if (string.IsNullOrWhiteSpace(value))
                continue;

            string cleaned = SanitizeFinalPrompt(value.Trim());
            if (string.IsNullOrWhiteSpace(cleaned))
                continue;

            if (!ShouldKeepPromptTerm(cleaned, dialogueText))
                continue;

            if (LooksLikeAbstractOnlyTerm(cleaned))
                continue;

            if (!objects.Contains(cleaned))
                objects.Add(cleaned);
        }
    }

    private bool LooksLikeAbstractOnlyTerm(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        string normalized = value.ToLowerInvariant();
        string[] abstractTerms =
        {
            "emotion", "feeling", "mood", "pressure", "hollow", "absence", "darkness", "sadness", "fear", "anger",
            "pain", "memory", "meaning", "metaphor", "residue", "collapse", "loss", "void", "emptiness",
            "감정", "기분", "압박", "공허", "부재", "어둠", "슬픔", "두려움", "분노", "고통", "기억", "의미", "은유", "상실"
        };

        foreach (string term in abstractTerms)
        {
            if (normalized == term)
                return true;
        }

        return false;
    }

    private string BuildConcreteSceneTemplate(string concreteObjects, string colors, bool wide)
    {
        if (wide)
        {
            return "arrange the objects across a believable physical setting: floor, wall, doorway or table, with foreground clutter, midground evidence, background shadow, specific palette " + colors;
        }

        return "compose a close real-world still life: one main object in sharp focus, two or more supporting objects around it, visible surface texture, stains, dust, and directional light, palette " + colors;
    }

    private string CleanPromptField(string value, string fallback)
    {
        string trimmed = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return SanitizeFinalPrompt(trimmed);
    }

    private string JoinPromptItems(string[] values, int maxCount)
    {
        if (values == null || values.Length == 0)
            return "user-specific damaged objects and emotional residue";

        List<string> kept = new List<string>();
        string dialogueText = BuildConversationContext();

        foreach (string value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            string cleaned = SanitizeFinalPrompt(value.Trim());
            if (string.IsNullOrWhiteSpace(cleaned))
                continue;

            if (!ShouldKeepPromptTerm(cleaned, dialogueText))
                continue;

            kept.Add(cleaned);
            if (kept.Count >= maxCount)
                break;
        }

        return kept.Count > 0 ? string.Join(", ", kept) : "user-specific damaged objects and emotional residue";
    }

    private string ExtractColorHints(string dialogueText, AiAssistantResult result)
    {
        List<string> colors = new List<string>();
        string source = (dialogueText ?? "") + " " + JoinRawItems(result?.image_keywords) + " " + JoinRawItems(result?.visual_anchors);
        string lower = source.ToLowerInvariant();

        string[] colorTerms =
        {
            "black", "blue", "red", "green", "yellow", "purple", "pink", "white", "gray", "grey", "orange", "brown",
            "검정", "검은", "까만", "파랑", "파란", "푸른", "빨강", "붉은", "초록", "노랑", "보라", "분홍", "핑크", "하양", "하얀", "회색", "주황", "갈색"
        };

        foreach (string color in colorTerms)
        {
            if (lower.Contains(color.ToLowerInvariant()) && !colors.Contains(color))
                colors.Add(color);
        }

        if (colors.Count == 0)
            return "muted dark palette, bruised blue, ash gray, stained black, one sharp accent color chosen from the user's mood";

        return string.Join(", ", colors.Take(6));
    }

    private void ApplyDarkReactiveSkyFromAnswers(AiAssistantResult result = null)
    {
        if (!useAnswerReactiveSky)
            return;

        ResolveSkyCamera();

        string aiColorText = result == null
            ? ""
            : JoinRawItems(result.image_keywords) + " " + JoinRawItems(result.visual_anchors) + " " + result.emotion + " " + result.metaphor;
        Color skyColor = GetSkyColorFromText(string.Join(" ", collectedAnswers) + " " + aiColorText, skyDarkValue, skySaturation);
        lastEmotionalSkyColor = skyColor;
        hasEmotionalSkyColor = true;

        if (skyCamera != null)
        {
            skyCamera.clearFlags = CameraClearFlags.SolidColor;
            skyCamera.backgroundColor = skyColor;
        }

        RenderSettings.ambientLight = Color.Lerp(Color.black, skyColor, 0.45f);
        RenderSettings.fogColor = Color.Lerp(Color.black, skyColor, 0.65f);
    }

    public void BrightenReactiveSkyAfterRelease()
    {
        if (!useAnswerReactiveSky)
            return;

        ResolveSkyCamera();

        Color baseColor = hasEmotionalSkyColor
            ? lastEmotionalSkyColor
            : GetSkyColorFromText(string.Join(" ", collectedAnswers), skyDarkValue, skySaturation);

        Color.RGBToHSV(baseColor, out float hue, out _, out _);
        Color resolvedColor = Color.HSVToRGB(hue, Mathf.Clamp01(skyResolvedSaturation), Mathf.Clamp01(skyResolvedValue));

        if (skyCamera != null)
        {
            skyCamera.clearFlags = CameraClearFlags.SolidColor;
            skyCamera.backgroundColor = resolvedColor;
        }

        RenderSettings.ambientLight = Color.Lerp(Color.black, resolvedColor, 0.7f);
        RenderSettings.fogColor = Color.Lerp(Color.white, resolvedColor, 0.65f);
    }

    private void ResetReactiveSky()
    {
        if (!useAnswerReactiveSky)
            return;

        ResolveSkyCamera();

        if (skyCamera != null)
        {
            skyCamera.clearFlags = CameraClearFlags.SolidColor;
            skyCamera.backgroundColor = initialSkyColor;
        }

        RenderSettings.ambientLight = Color.Lerp(Color.black, initialSkyColor, 0.55f);
        RenderSettings.fogColor = initialSkyColor;
        hasEmotionalSkyColor = false;
    }

    private void ResolveSkyCamera()
    {
        if (skyCamera != null)
            return;

        skyCamera = Camera.main;
        if (skyCamera == null)
            skyCamera = FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
    }

    private Color GetSkyColorFromText(string text, float value, float saturation)
    {
        if (string.IsNullOrWhiteSpace(text))
            return defaultDarkSkyColor;

        string lower = text.ToLowerInvariant();
        float hue = 0.62f;

        if (ContainsAny(lower, "빨강", "붉", "빨간", "red", "분노", "화", "피"))
            hue = 0.0f;
        else if (ContainsAny(lower, "주황", "orange"))
            hue = 0.07f;
        else if (ContainsAny(lower, "노랑", "노란", "yellow", "불안"))
            hue = 0.13f;
        else if (ContainsAny(lower, "초록", "녹색", "green"))
            hue = 0.36f;
        else if (ContainsAny(lower, "파랑", "파란", "푸른", "blue", "우울", "슬프"))
            hue = 0.58f;
        else if (ContainsAny(lower, "보라", "purple", "violet"))
            hue = 0.75f;
        else if (ContainsAny(lower, "분홍", "핑크", "pink"))
            hue = 0.92f;
        else if (ContainsAny(lower, "갈색", "brown"))
            hue = 0.08f;
        else if (ContainsAny(lower, "검정", "검은", "까만", "black", "어둠", "그림자", "죽음", "공허"))
            hue = 0.68f;
        else if (ContainsAny(lower, "회색", "灰", "gray", "grey"))
            return Color.HSVToRGB(0.62f, 0.12f, Mathf.Clamp01(value));
        else if (ContainsAny(lower, "하양", "하얀", "흰", "white"))
            return Color.HSVToRGB(0.62f, 0.08f, Mathf.Clamp01(value));

        return Color.HSVToRGB(hue, Mathf.Clamp01(saturation), Mathf.Clamp01(value));
    }

    private bool ContainsAny(string source, params string[] terms)
    {
        if (string.IsNullOrWhiteSpace(source) || terms == null)
            return false;

        foreach (string term in terms)
        {
            if (!string.IsNullOrWhiteSpace(term) && source.Contains(term.ToLowerInvariant()))
                return true;
        }

        return false;
    }

    private string JoinRawItems(string[] values)
    {
        if (values == null || values.Length == 0)
            return "";

        return string.Join(" ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private void AddPromptPart(List<string> parts, string value, string dialogueText)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        string trimmed = value.Trim();
        if (!ShouldKeepPromptTerm(trimmed, dialogueText))
            return;

        parts.Add(trimmed);
    }

    private bool ShouldKeepPromptTerm(string term, string dialogueText)
    {
        if (string.IsNullOrWhiteSpace(term))
            return false;

        string normalizedTerm = term.Trim().ToLowerInvariant();
        string normalizedDialogue = (dialogueText ?? "").ToLowerInvariant();

        string[] handTerms =
        {
            "hand", "hands", "finger", "fingers", "fingertip", "fingertips",
            "grasp", "grasping", "hold", "holding", "손", "손가락", "손끝", "쥐다", "잡다"
        };

        foreach (string handTerm in handTerms)
        {
            if (!normalizedTerm.Contains(handTerm))
                continue;

            return normalizedDialogue.Contains("hand") ||
                   normalizedDialogue.Contains("finger") ||
                   normalizedDialogue.Contains("손") ||
                   normalizedDialogue.Contains("손가락") ||
                   normalizedDialogue.Contains("손끝");
        }

        return true;
    }

    private string GetFallbackAiQuestion()
    {
        return GetFallbackAiQuestion(ClassifyLatestAnswerMood());
    }

    private string GetFallbackAiQuestion(AnswerMood mood)
    {
        string[] fallbackQuestions = BuildContextualFallbackQuestions(mood);

        return fallbackQuestions[UnityEngine.Random.Range(0, fallbackQuestions.Length)];
    }

    private bool IsRepeatQuestion(string question)
    {
        string normalized = NormalizeQuestion(question);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        if (NormalizeQuestion(initialQuestion) == normalized)
            return true;

        if (NormalizeQuestion(currentQuestionText) == normalized)
            return true;

        foreach (ConversationTurn turn in conversationTurns)
        {
            if (NormalizeQuestion(turn.question) == normalized)
                return true;
        }

        return false;
    }

    private string NormalizeQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return "";

        string normalized = question.Trim().ToLowerInvariant();
        normalized = normalized.Replace(" ", "");
        normalized = normalized.Replace("?", "");
        normalized = normalized.Replace("!", "");
        normalized = normalized.Replace(".", "");
        normalized = normalized.Replace(",", "");
        normalized = normalized.Replace("？", "");
        normalized = normalized.Replace("！", "");
        return normalized;
    }

    private string GetNonRepeatingFallbackAiQuestion()
    {
        return GetNonRepeatingFallbackAiQuestion(ClassifyLatestAnswerMood());
    }

    private string GetNonRepeatingFallbackAiQuestion(AnswerMood mood)
    {
        string[] candidates = BuildContextualFallbackQuestions(mood);

        foreach (string candidate in candidates)
        {
            if (!IsRepeatQuestion(candidate))
                return candidate;
        }

        return GetFallbackAiQuestion(mood);
    }

    private string[] BuildContextualFallbackQuestions(AnswerMood mood)
    {
        string anchor = GetSafeQuestionAnchor();

        if (mood == AnswerMood.Bright)
        {
            return new[]
            {
                $"{anchor}을/를 가장 먼저 흐리게 만드는 것은 무엇입니까?",
                $"{anchor} 뒤에서 균열을 만드는 존재는 무엇입니까?",
                $"{anchor}이/가 무너지는 순간에는 무엇이 끼어듭니까?",
                $"{anchor}을/를 지키지 못하게 막는 것은 무엇입니까?"
            };
        }

        if (mood == AnswerMood.Heavy)
        {
            return new[]
            {
                $"{anchor} 안에서 가장 선명하게 남아 있는 것은 무엇입니까?",
                $"{anchor}은/는 어떤 흔적에서 시작되었습니까?",
                $"{anchor}을/를 더 깊이 들여다보면 어떤 장면이 보입니까?",
                $"{anchor}에 남은 얼룩은 어떤 색입니까?",
                $"{anchor}을/를 만질 수 있다면 어떤 질감입니까?",
                $"{anchor}이/가 몸에 닿는다면 어디가 먼저 반응합니까?"
            };
        }

        return new[]
        {
            $"{anchor}에서 가장 먼저 어두워지는 부분은 무엇입니까?",
            $"{anchor}을/를 막고 있는 것은 무엇입니까?",
            $"{anchor} 뒤에 숨어 있는 불편한 장면은 무엇입니까?",
            $"{anchor}에 균열을 낸 것은 무엇입니까?",
            $"{anchor}은/는 가까이 있습니까, 멀리 있습니까?"
        };
    }

    private string GetAiQuestionDirective()
    {
        int turnNumber = Mathf.Clamp(aiQuestionTurnCount + 1, 1, maxAiQuestionTurns);
        AnswerMood mood = ClassifyLatestAnswerMood();
        string latestAnswer = GetLatestAnswerText();
        string latestAnchor = GetSafeQuestionAnchor();
        string moodContext;
        if (mood == AnswerMood.Bright)
        {
            moodContext = "The user answer is bright or positive. Do not celebrate it. Ask what destroys, stains, blocks, interrupts, or collapses that exact positive thing, without asking who caused it.";
        }
        else if (mood == AnswerMood.Heavy)
        {
            moodContext = "The user answer is already heavy or negative. Do not pivot to positive destruction. Continue downward and ask for the concrete pressure, fracture, residue, color, object, body sensation, or absence inside that exact answer. Do not ask who.";
        }
        else
        {
            moodContext = "The user answer is ambiguous. Use the user's own words to steer toward a dream, pressure, stain, object, sensation, absence, or collapse. Do not ask who.";
        }

        string continuityRule =
            $"Latest answer: \"{latestAnswer}\"\n" +
            $"Question anchor to preserve: \"{latestAnchor}\"\n" +
            "The next_question must feel like it is replying to this exact answer. Use the anchor word directly in Korean unless it would be grammatically broken. " +
            "Do not ask a broad new question that could fit any answer. Do not jump to a new topic. " +
            "Do not use purpose phrases, particles, or verb endings as nouns. For example, never ask '위해에서', '찾기 위해에서', or attach 조사 to '위해'. If the answer says '찾기 위해', ask about '찾으려는 것', '찾지 못한 것', or the dark feeling behind the search.\n" +
            "The goal of the whole dialogue is to extract the user's dark emotion, dark mood, or dark experience, not to ask beautiful, generic, or unrelated worldbuilding questions.\n";

        string responseShape = "Return JSON only with mode, next_question, valence, tone, and focus.";

        switch (turnNumber)
        {
            case 1:
                return "Ask exactly one short Korean question.\n" +
                       continuityRule +
                       $"Turn 1: classify the latest answer and ask about the expectation, blocked desire, pressure, trace, object, or life area touched by it.\n{moodContext}\n" +
                       "Do not ask about reasons again. Do not repeat the opening question.\n" +
                       responseShape;
            case 2:
                return "Ask exactly one short Korean question.\n" +
                       continuityRule +
                       $"Turn 2: ask what pressed, withheld, ruined, interrupted, or damaged the feeling.\n{moodContext}\n" +
                       "Ask about one concrete detail only.\n" +
                       responseShape;
            case 3:
                return "Ask exactly one short Korean question.\n" +
                       continuityRule +
                       $"Turn 3: ask about a concrete object, residue, color, crack, stain, leak, or broken thing left in the scene.\n{moodContext}\n" +
                       "Move toward a drawable image clue.\n" +
                       responseShape;
            case 4:
                return "Ask exactly one short Korean question.\n" +
                       continuityRule +
                       $"Turn 4: ask about color, light, framing, distance, scale, or composition for the final image.\n{moodContext}\n" +
                       "If the answer is bright, ask what shadow, crack, or disturbance changes that brightness.\n" +
                       responseShape;
            default:
                return "Ask exactly one short Korean question.\n" + continuityRule + moodContext + "\n" + responseShape;
        }
    }

    private string GetLatestAnswerText()
    {
        if (collectedAnswers == null || collectedAnswers.Count == 0)
            return "";

        return collectedAnswers[collectedAnswers.Count - 1] ?? "";
    }

    private string ExtractLatestAnswerAnchor()
    {
        string answer = GetLatestAnswerText();
        if (string.IsNullOrWhiteSpace(answer))
            return "감정";

        string lower = answer.ToLowerInvariant();

        string searchAnchor = ExtractSearchAnchor(answer, lower);
        if (!string.IsNullOrWhiteSpace(searchAnchor))
            return searchAnchor;

        string[] priorityAnchors =
        {
            "어둠", "그림자", "상처", "고통", "우울", "죽음", "공허", "무너짐", "불안", "분노", "외로움", "죄책감", "두려움",
            "검은색", "파란색", "빨간색", "회색", "기억", "관계", "꿈", "말", "장면", "몸", "마음",
            "darkness", "shadow", "wound", "pain", "depression", "death", "emptiness", "collapse", "fear", "anger", "memory", "dream"
        };

        foreach (string anchor in priorityAnchors)
        {
            if (lower.Contains(anchor.ToLowerInvariant()))
                return anchor;
        }

        char[] separators = { ' ', ',', '.', '?', '!', '，', '。', '？', '！', '\n', '\r', '\t' };
        string[] rawTokens = answer.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        string best = "";

        foreach (string rawToken in rawTokens)
        {
            string token = rawToken.Trim(' ', '"', '\'', '“', '”', '‘', '’', '(', ')', '[', ']', '{', '}', ':', ';');
            if (string.IsNullOrWhiteSpace(token))
                continue;

            token = StripKoreanParticles(token);
            if (IsPurposeOrVerbPhrase(token))
                continue;

            if (!LooksLikeNounAnchor(token))
                continue;

            if (IsWeakAnchorToken(token))
                continue;

            if (token.Length > best.Length)
                best = token;
        }

        return string.IsNullOrWhiteSpace(best) ? "감정" : best;
    }

    private string GetSafeQuestionAnchor()
    {
        string anchor = ExtractLatestAnswerAnchor();
        if (IsUnsafeQuestionAnchor(anchor))
            return GetSemanticFallbackAnchor();

        return anchor;
    }

    private bool IsUnsafeQuestionAnchor(string anchor)
    {
        if (string.IsNullOrWhiteSpace(anchor))
            return true;

        return IsWeakAnchorToken(anchor) ||
               IsPurposeOrVerbPhrase(anchor) ||
               LooksLikeVerbOrAdjective(anchor) ||
               !LooksLikeNounAnchor(anchor);
    }

    private string GetSemanticFallbackAnchor()
    {
        AnswerMood mood = ClassifyLatestAnswerMood();
        string answer = GetLatestAnswerText().ToLowerInvariant();

        if (ContainsAny(answer, "색", "검정", "검은", "파랑", "파란", "빨강", "붉", "회색", "보라", "분홍", "핑크"))
            return "그 색";

        if (ContainsAny(answer, "몸", "가슴", "목", "배", "머리", "숨"))
            return "몸의 반응";

        if (ContainsAny(answer, "기억", "장면", "말", "꿈"))
            return "그 장면";

        if (ContainsAny(answer, "찾", "원하", "바라", "기대"))
            return "찾으려는 것";

        if (mood == AnswerMood.Heavy)
            return "그 어두운 감정";

        if (mood == AnswerMood.Bright)
            return "그 밝은 감각";

        return "그 감정";
    }

    private string ExtractSearchAnchor(string answer, string lower)
    {
        if (string.IsNullOrWhiteSpace(answer))
            return "";

        bool hasSearchIntent =
            lower.Contains("찾기 위해") ||
            lower.Contains("찾기위해") ||
            lower.Contains("찾으려") ||
            lower.Contains("찾으려고") ||
            lower.Contains("찾고") ||
            lower.Contains("search") ||
            lower.Contains("find");

        if (!hasSearchIntent)
            return "";

        string[] objectMarkers = { "을", "를" };
        foreach (string marker in objectMarkers)
        {
            int markerIndex = answer.IndexOf(marker, StringComparison.Ordinal);
            int searchIndex = answer.IndexOf("찾", StringComparison.Ordinal);
            if (markerIndex <= 0 || searchIndex <= markerIndex)
                continue;

            string beforeMarker = answer.Substring(0, markerIndex);
            string[] tokens = beforeMarker.Split(new[] { ' ', ',', '.', '?', '!', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = tokens.Length - 1; i >= 0; i--)
            {
                string candidate = StripKoreanParticles(tokens[i].Trim());
                if (!IsWeakAnchorToken(candidate) && !IsPurposeOrVerbPhrase(candidate) && LooksLikeNounAnchor(candidate))
                    return candidate;
            }
        }

        return "찾으려는 것";
    }

    private string StripKoreanParticles(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return "";

        string[] suffixes =
        {
            "으로부터", "에게서", "에서는", "에서", "으로", "에게", "에는", "은", "는", "이", "가", "을", "를", "의", "에", "로", "와", "과", "도", "만"
        };

        foreach (string suffix in suffixes)
        {
            if (token.Length > suffix.Length + 1 && token.EndsWith(suffix, StringComparison.Ordinal))
                return token.Substring(0, token.Length - suffix.Length);
        }

        return token;
    }

    private bool IsWeakAnchorToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length <= 1)
            return true;

        string normalized = token.ToLowerInvariant();
        string[] weakTokens =
        {
            "내가", "나는", "나의", "나를", "저는", "제가", "이곳", "여기", "위해", "위해서", "찾기", "찾기위해", "찾으려", "찾으려고", "찾고", "보기", "보려고", "왔다", "왔어", "왔어요",
            "것", "무엇", "어떤", "그", "이", "저", "수", "때문", "때문에", "하고", "해서", "하며",
            "i", "me", "my", "to", "for", "the", "a", "an", "this", "that", "here", "came", "come", "see", "look"
        };

        foreach (string weak in weakTokens)
        {
            if (normalized == weak)
                return true;
        }

        return false;
    }

    private bool IsPurposeOrVerbPhrase(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        string normalized = token.Replace(" ", "").Trim().ToLowerInvariant();
        return normalized.Contains("위해") ||
               normalized.Contains("위해서") ||
               normalized.EndsWith("려고", StringComparison.Ordinal) ||
               normalized.EndsWith("으려고", StringComparison.Ordinal) ||
               normalized.EndsWith("기", StringComparison.Ordinal) ||
               normalized.Contains("찾기") ||
               normalized.Contains("찾으려") ||
               normalized.Contains("찾으려고");
    }

    private bool LooksLikeNounAnchor(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        string normalized = token.Trim().ToLowerInvariant();
        if (LooksLikeVerbOrAdjective(normalized))
            return false;

        string[] nounEndings =
        {
            "감", "감정", "기분", "마음", "생각", "기억", "장면", "색", "색깔", "빛", "어둠", "그림자", "상처", "고통", "불안", "분노", "외로움", "죄책감", "두려움",
            "공허", "흔적", "얼룩", "균열", "압박", "질감", "냄새", "소리", "말", "꿈", "관계", "몸", "반응", "부분", "문", "의자", "종이", "거울", "컵", "편지"
        };

        foreach (string ending in nounEndings)
        {
            if (normalized.EndsWith(ending, StringComparison.Ordinal))
                return true;
        }

        if (System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^[a-z][a-z\- ]{2,}$"))
            return true;

        return normalized.Length >= 2 && !ContainsAny(normalized, "하다", "되다", "이다");
    }

    private bool LooksLikeVerbOrAdjective(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        string normalized = token.Replace(" ", "").Trim().ToLowerInvariant();
        string[] verbEndings =
        {
            "하다", "했다", "한다", "하고", "해서", "하며", "되다", "됐다", "된다", "되고", "되어",
            "싶다", "싶어", "싶은", "같다", "같아", "같은", "느끼다", "느껴", "느낀", "보이다", "보여",
            "찾다", "찾아", "찾고", "찾은", "찾을", "보다", "보고", "본다", "보는", "온다", "왔다", "오는",
            "막다", "막고", "막힌", "막혀", "무너지다", "무너져", "무너진", "흐리다", "흐려", "흐린",
            "아프다", "아파", "아픈", "슬프다", "슬퍼", "슬픈", "어둡다", "어두운", "차갑다", "차가운",
            "무겁다", "무거운", "비어", "비어있는", "사라지다", "사라져", "사라진"
        };

        foreach (string ending in verbEndings)
        {
            if (normalized.EndsWith(ending, StringComparison.Ordinal))
                return true;
        }

        return normalized.EndsWith("게", StringComparison.Ordinal) ||
               normalized.EndsWith("듯", StringComparison.Ordinal) ||
               normalized.EndsWith("듯이", StringComparison.Ordinal);
    }

    private AnswerMood ClassifyLatestAnswerMood()
    {
        string answer = (GetLatestAnswerText() ?? "").ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(answer))
            return AnswerMood.Neutral;

        string[] brightTerms =
        {
            "pink", "light", "bright", "warm", "soft", "happy", "good", "nice", "love", "beautiful",
            "peace", "peaceful", "hope", "hopeful", "calm", "clear", "sunny", "flower",
            "핑크", "밝", "따뜻", "부드", "행복", "좋", "사랑", "예쁘", "평온", "희망", "맑", "꽃"
        };

        string[] heavyTerms =
        {
            "black", "blue", "dark", "cold", "heavy", "numb", "pain", "hurt", "sad", "angry", "broken",
            "stain", "crack", "fear", "afraid", "lonely", "empty", "void", "suffoc", "pressure", "dirty",
            "darkness", "shadow", "wound", "depress", "death", "die", "collapse", "loss",
            "검", "파란", "어둡", "어둠", "그림자", "차갑", "무겁", "아프", "상처", "고통", "슬프", "우울", "죽", "죽음", "화", "부서", "무너", "상실", "얼룩", "균열", "무서", "외로", "비어", "공허", "숨막", "압박", "더럽"
        };

        foreach (string term in heavyTerms)
        {
            if (answer.Contains(term))
                return AnswerMood.Heavy;
        }

        foreach (string term in brightTerms)
        {
            if (answer.Contains(term))
                return AnswerMood.Bright;
        }

        return AnswerMood.Neutral;
    }

    private string AdjustQuestionForValence(string question, AiAssistantResult result)
    {
        AnswerMood localMood = ClassifyLatestAnswerMood();
        if (localMood == AnswerMood.Heavy)
        {
            if (LooksLikePositivePivotQuestion(question) || LooksLikePersonQuestion(question) || string.Equals((result?.valence ?? "").Trim(), "positive", StringComparison.OrdinalIgnoreCase))
                return GetNonRepeatingFallbackAiQuestion(AnswerMood.Heavy);

            return question;
        }

        if (LooksLikePersonQuestion(question))
            return GetNonRepeatingFallbackAiQuestion(localMood);

        string valence = (result?.valence ?? "").Trim().ToLowerInvariant();
        if (valence != "positive" && localMood != AnswerMood.Bright)
            return question;

        string focus = (result?.focus ?? "").Trim().ToLowerInvariant();
        if (focus == "person" || focus == "relationship")
            return "그 좋은 감정을 무너뜨리는 흔적은 무엇입니까?";

        if (focus == "dream")
            return "그 꿈을 막거나 무너뜨리는 것은 무엇입니까?";

        if (focus == "color")
            return "그 밝은 색을 더럽히거나 꺼뜨리는 것은 무엇입니까?";

        return "그 긍정을 파괴하는 것은 무엇입니까?";
    }

    private bool LooksLikePersonQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return false;

        string normalized = question.Trim().ToLowerInvariant();
        return normalized.Contains("누구") ||
               normalized.Contains("누가") ||
               normalized.Contains("누군가") ||
               normalized.Contains("사람") ||
               normalized.Contains("who") ||
               normalized.Contains("person") ||
               normalized.Contains("people");
    }

    private bool LooksLikePositivePivotQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return false;

        string normalized = question.Trim();
        return normalized.Contains("긍정") ||
               normalized.Contains("좋은 기분") ||
               normalized.Contains("좋은 감정") ||
               normalized.Contains("밝은 색") ||
               normalized.Contains("평온") ||
               normalized.Contains("행복");
    }

    private string EnsureQuestionFollowsLatestAnswer(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return GetNonRepeatingFallbackAiQuestion();

        if (QuestionReferencesLatestAnswer(question))
            return question;

        return GetNonRepeatingFallbackAiQuestion();
    }

    private bool QuestionReferencesLatestAnswer(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return false;

        string anchor = ExtractLatestAnswerAnchor();
        if (string.IsNullOrWhiteSpace(anchor) || anchor == "감정")
            return true;

        string normalizedQuestion = question.ToLowerInvariant();
        string normalizedAnchor = anchor.ToLowerInvariant();

        if (normalizedQuestion.Contains(normalizedAnchor))
            return true;

        string[] directReferenceTerms =
        {
            "그것", "그 안", "그 속", "그 장면", "그 감정", "그 마음", "그 색", "그 기억", "그 말", "그 꿈", "그 흔적", "그 질감", "그 얼룩"
        };

        foreach (string term in directReferenceTerms)
        {
            if (question.Contains(term))
                return true;
        }

        return false;
    }

    private void InitializeConversation(bool resetSky = true)
    {
        aiQuestionTurnCount = 0;
        currentQuestionText = initialQuestion;
        collectedAnswers.Clear();
        conversationTurns.Clear();
        resetConversationAfterGeneration = false;
        awaitingWhaleEntry = false;
        if (resetSky)
            ResetReactiveSky();

        SetQuestionText(initialQuestion);
        SetButtonText("다음");

        if (promptInputField != null)
        {
            promptInputField.text = "";
            FocusPromptInput();
        }

        SetStatus(initialQuestion);
    }

    private void HandleConversationAdvanceWithAi()
    {
        if (promptInputField == null)
        {
            Fail("Prompt input field is missing.");
            return;
        }

        string answer = promptInputField.text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(answer))
        {
            SetStatus("?듬????낅젰?댁쨾.");
            return;
        }

        conversationTurns.Add(new ConversationTurn
        {
            question = currentQuestionText,
            answer = answer
        });
        collectedAnswers.Add(answer);
        activeRule = SelectRule(string.Join(" ", collectedAnswers));

        promptInputField.text = "";
        isAwaitingAiResponse = true;
        if (generateButton != null)
            generateButton.interactable = false;
        SetStatus("AI媛 ?ㅼ쓬 吏덈Ц??留뚮뱾怨??덉뼱??..");

        if (aiQuestionTurnCount < maxAiQuestionTurns)
        {
            int requestSerial = ++aiQuestionRequestSerial;
            StartCoroutine(RequestAiQuestionRoutine(requestSerial));
            StartCoroutine(ApplyAiQuestionFallbackAfterDelay(requestSerial));
            return;
        }

        StartCoroutine(RequestAiSummaryAndGenerateRoutine());
    }

    private void HandleConversationAdvance()
    {
        if (promptInputField == null)
        {
            Fail("Prompt input field is missing.");
            return;
        }

        string answer = promptInputField.text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(answer))
        {
            SetStatus("?듬????낅젰?댁쨾.");
            return;
        }

        collectedAnswers.Add(answer);
        promptInputField.text = "";
        activeRule = SelectRule(string.Join(" ", collectedAnswers));

        if (currentQuestionText == initialQuestion)
        {
            SetQuestionText("洹?留덉쓬??媛????? ?대?吏??臾댁뾿?멸???");
            SetStatus("洹?留덉쓬??媛????? ?대?吏??臾댁뾿?멸???");
            currentQuestionText = "洹?留덉쓬??媛????? ?대?吏??臾댁뾿?멸???";
            if (promptInputField != null)
                FocusPromptInput();
            return;
        }

        StartFinalGeneration();
    }

    private IEnumerator RequestAiQuestionRoutine(int requestSerial)
    {
        string context = BuildConversationContext();
        string userInstruction = GetAiQuestionDirective();

        yield return StartCoroutine(RequestAiStructuredTurn(
            stage: "question",
            userInstruction: userInstruction,
            context: context,
            onSuccess: result =>
            {
                if (requestSerial != aiQuestionRequestSerial)
                    return;

                isAwaitingAiResponse = false;
                if (generateButton != null)
                    generateButton.interactable = true;

                string nextQuestion = string.IsNullOrWhiteSpace(result.next_question)
                    ? GetNonRepeatingFallbackAiQuestion()
                    : result.next_question.Trim();

                nextQuestion = AdjustQuestionForValence(nextQuestion, result);
                nextQuestion = EnsureQuestionFollowsLatestAnswer(nextQuestion);

                if (IsRepeatQuestion(nextQuestion))
                    nextQuestion = GetNonRepeatingFallbackAiQuestion();

                aiQuestionTurnCount++;
                currentQuestionText = nextQuestion;
                SetQuestionText(nextQuestion);
                SetStatus(nextQuestion);

                if (promptInputField != null)
                    FocusPromptInput();
            },
            onFailure: error =>
            {
                if (requestSerial != aiQuestionRequestSerial)
                    return;

                if (IsQuotaError(error))
                {
                    isAwaitingAiResponse = false;
                    if (generateButton != null)
                        generateButton.interactable = true;

                    string nextQuestion = GetNonRepeatingFallbackAiQuestion();
                    aiQuestionTurnCount++;
                    currentQuestionText = nextQuestion;
                    SetQuestionText(nextQuestion);
                    SetStatus("OpenAI 荑쇳꽣媛 遺議깊빐??fallback 吏덈Ц???ъ슜?댁슂.");

                    if (promptInputField != null)
                        FocusPromptInput();
                    return;
                }

                isAwaitingAiResponse = false;
                if (generateButton != null)
                    generateButton.interactable = true;

                Fail(error);
            }));
    }

    private IEnumerator ApplyAiQuestionFallbackAfterDelay(int requestSerial)
    {
        float elapsed = 0f;
        float delay = Mathf.Max(1f, aiQuestionFallbackDelaySeconds);

        while (elapsed < delay)
        {
            if (requestSerial != aiQuestionRequestSerial || !isAwaitingAiResponse)
                yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (requestSerial != aiQuestionRequestSerial || !isAwaitingAiResponse)
            yield break;

        string fallbackQuestion = GetNonRepeatingFallbackAiQuestion();
        aiQuestionTurnCount++;
        currentQuestionText = fallbackQuestion;
        SetQuestionText(fallbackQuestion);
        SetStatus(fallbackQuestion + " (AI ?묐떟 ?湲?以?");

        isAwaitingAiResponse = false;
        if (generateButton != null)
            generateButton.interactable = true;

        aiQuestionRequestSerial++;
    }

    private IEnumerator RequestAiSummaryAndGenerateRoutine()
    {
        string context = BuildConversationContext();
        string userInstruction = "Summarize the whole dialogue into emotion, meaning, metaphor, visual anchors, image keywords, and a compact prompt.\n" +
                                 "Ground the answer in the user's exact wording. Pull out 3 to 5 concrete nouns, 2 to 4 actions, 2 to 4 sensations, and 3 to 6 visual anchors from the dialogue.\n" +
                                 "Do not turn the dialogue into a positive, healing, or reassuring interpretation. Keep the summary heavy, fractured, stained, pressured, hollow, absent, decayed, or suffocating when the text supports it.\n" +
                                 "The final prompt must focus on the user's answer content and the damaged emotional scene that grows from it, not a generic mood description.\n" +
                                 "Write comfy_prompt and all visual arrays in English for ComfyUI, and make sure they include concrete visual anchors from the dialogue.\n" +
                                 "Prefer bodies, objects, stains, glass, paper, thread, cloth, shadow, cracks, residue, and motion over rooms, houses, or buildings unless the user explicitly mentions those spaces.\n" +
                                 "Never default to hands or fingers. Avoid hand imagery unless the user's own answer explicitly and repeatedly insists on hands.\n" +
                                 "Do not turn verbs like holding, grasping, making, or writing into hand close-ups unless the user explicitly demands that body detail.\n" +
                                 "Avoid generating a place by itself. The output must contain emotion evidence: symbolic material, concrete objects, residue, action traces, color, and pressure from the user's answers.\n" +
                                 "Return JSON only.";

        yield return StartCoroutine(RequestAiStructuredTurn(
            stage: "summary",
            userInstruction: userInstruction,
            context: context,
            onSuccess: result =>
            {
                isAwaitingAiResponse = false;

                string finalPrompt = BuildPromptFromAiResult(result);
                if (string.IsNullOrWhiteSpace(finalPrompt))
                {
                    Fail("AI summary did not produce a usable prompt.");
                    return;
                }

                List<string> promptSet = BuildPromptSetFromAiResult(result, finalPrompt);
                SetFinalPromptSet(promptSet);
                ApplyDarkReactiveSkyFromAnswers(result);
                PrepareWhaleEntry(finalPrompt);
            },
            onFailure: error =>
            {
                if (IsQuotaError(error))
                {
                    isAwaitingAiResponse = false;
                    if (generateButton != null)
                        generateButton.interactable = true;

                    SetStatus("OpenAI 荑쇳꽣媛 遺議깊빐??濡쒖뺄 洹쒖튃?쇰줈 ?대?吏瑜??앹꽦?댁슂.");
                    StartFinalGeneration();
                    return;
                }

                isAwaitingAiResponse = false;
                if (generateButton != null)
                    generateButton.interactable = true;

                Fail(error);
            }));
    }

    private void OnYesClicked()
    {
        if (awaitingWhaleEntry)
        {
            EnterWhaleAndGenerate();
            return;
        }

        HidePromptObjects();

        if (yesButton != null)
            yesButton.gameObject.SetActive(false);

        SetPromptInputMode(false);
    }

    private void HidePromptObjects()
    {
        ResolveSceneReferences();

        if (promptCanvasRoot != null)
            promptCanvasRoot.SetActive(false);
        else if (promptInputField != null)
            promptInputField.gameObject.SetActive(false);

        if (promptTriggerRoot != null)
            promptTriggerRoot.SetActive(false);
    }

    private void PrepareWhaleEntry(string finalPrompt)
    {
        if (string.IsNullOrWhiteSpace(finalPrompt))
        {
            Fail("Final ComfyUI prompt is empty.");
            return;
        }

        lastFinalPrompt = finalPrompt;
        awaitingWhaleEntry = true;

        Debug.Log("[ComfyImageGenerator] Final ComfyUI Prompt: " + finalPrompt);
        LogFinalPromptSet();

        if (generateButton != null)
            generateButton.interactable = true;

        if (yesButton != null)
            yesButton.gameObject.SetActive(false);

        if (promptInputField != null)
            promptInputField.text = "";

        SetButtonText("네");
        SetQuestionText("고래의 뱃속으로 들어갈 준비가 되셨습니까?");
        SetStatus("고래의 뱃속으로 들어갈 준비가 되셨습니까?");
        FocusPromptInput();
    }

    private void SetFinalPromptSet(List<string> prompts)
    {
        lastFinalPromptSet.Clear();

        if (prompts == null)
            return;

        foreach (string prompt in prompts)
        {
            string sanitized = SanitizeFinalPrompt(prompt);
            if (!string.IsNullOrWhiteSpace(sanitized))
                lastFinalPromptSet.Add(sanitized);
        }
    }

    private void LogFinalPromptSet()
    {
        if (lastFinalPromptSet.Count == 0)
            return;

        for (int i = 0; i < lastFinalPromptSet.Count; i++)
        {
            string groupName;
            if (i == 0 || i == 2)
                groupName = "Abstract Emotion";
            else if (i == 1 || i == 3)
                groupName = "Realistic Experience";
            else
                groupName = "Color Illustration";

            Debug.Log("[ComfyImageGenerator] Final Prompt " + (i + 1).ToString() + " (" + groupName + "): " + lastFinalPromptSet[i]);
        }
    }

    private void EnterWhaleAndGenerate()
    {
        awaitingWhaleEntry = false;

        HidePromptObjects();

        if (yesButton != null)
            yesButton.gameObject.SetActive(false);

        SetPromptInputMode(false);

        if (string.IsNullOrWhiteSpace(lastFinalPrompt))
        {
            Fail("Final ComfyUI prompt is missing.");
            return;
        }

        resetConversationAfterGeneration = true;
        Debug.Log("[ComfyImageGenerator] Sending ComfyUI Prompt: " + lastFinalPrompt);
        StartCoroutine(GenerateWithPromptAndReset(lastFinalPrompt));
    }

    private void SetPromptInputMode(bool active)
    {
        promptInputModeActive = active;
        Cursor.lockState = active ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = active;

        if (active)
            FocusPromptInput();
    }

    private IEnumerator EnablePromptInputNextFrame()
    {
        yield return null;
        SetPromptInputMode(ShouldPromptInputBeActive());
    }

    private bool ShouldPromptInputBeActive()
    {
        if (!useQuestionFlow || promptInputField == null)
            return false;

        if (promptCanvasRoot != null)
            return promptCanvasRoot.activeInHierarchy;

        return promptInputField.gameObject.activeInHierarchy;
    }

    private void FocusPromptInput()
    {
        if (promptInputField == null)
            return;

        promptInputField.interactable = true;
        promptInputField.ActivateInputField();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(promptInputField.gameObject);
    }

    private bool IsQuotaError(string errorText)
    {
        if (string.IsNullOrWhiteSpace(errorText))
            return false;

        string normalized = errorText.ToLowerInvariant();
        return normalized.Contains("429") ||
               normalized.Contains("insufficient_quota") ||
               normalized.Contains("quota") ||
               normalized.Contains("billing");
    }

    private IEnumerator RequestAiStructuredTurn(string stage, string userInstruction, string context, Action<AiAssistantResult> onSuccess, Action<string> onFailure)
    {
        string apiKey = GetOpenAiApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            onFailure?.Invoke("OpenAI API key is missing.");
            yield break;
        }

        JObject body = BuildOpenAiRequestBody(stage, userInstruction, context);
        byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(body.ToString(Newtonsoft.Json.Formatting.None));

        using (UnityWebRequest request = new UnityWebRequest("https://api.openai.com/v1/responses", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(jsonBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            request.timeout = 60;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onFailure?.Invoke("OpenAI request failed: " + request.error + "\n" + request.downloadHandler.text);
                yield break;
            }

            string responseJson = request.downloadHandler.text;
            if (TryExtractOpenAiJson(responseJson, out string aiJson))
            {
                try
                {
                    AiAssistantResult result = JsonUtility.FromJson<AiAssistantResult>(aiJson);
                    if (result != null)
                    {
                        onSuccess?.Invoke(result);
                        yield break;
                    }
                }
                catch (Exception e)
                {
                    onFailure?.Invoke("Failed to parse OpenAI JSON: " + e.Message + "\nRaw: " + aiJson);
                    yield break;
                }
            }

            onFailure?.Invoke("OpenAI returned no structured JSON.\nResponse: " + responseJson);
        }
    }

    private void StartFinalGeneration()
    {
        string finalPrompt = BuildComfyPrompt();
        if (string.IsNullOrWhiteSpace(finalPrompt))
        {
            Fail("Could not build final prompt.");
            return;
        }

        SetFinalPromptSet(BuildPromptSetFromAiResult(null, finalPrompt));
        ApplyDarkReactiveSkyFromAnswers();
        PrepareWhaleEntry(finalPrompt);
    }

    private EmotionRule SelectRule(string combinedAnswer)
    {
        EmotionRule[] rules = GetDefaultRules();
        EmotionRule bestRule = null;
        int bestScore = 0;

        foreach (EmotionRule rule in rules)
        {
            int score = ScoreRule(rule, combinedAnswer);
            if (score > bestScore)
            {
                bestScore = score;
                bestRule = rule;
            }
        }

        if (bestRule != null)
            return bestRule;

        return GetFallbackRule();
    }

    private int ScoreRule(EmotionRule rule, string text)
    {
        if (rule == null || string.IsNullOrWhiteSpace(text) || rule.keywords == null)
            return 0;

        int score = 0;
        string normalized = text.ToLowerInvariant();

        foreach (string keyword in rule.keywords)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                continue;

            if (normalized.Contains(keyword.ToLowerInvariant()))
                score++;
        }

        return score;
    }

    private string BuildComfyPrompt()
    {
        EmotionRule rule = activeRule ?? GetFallbackRule();
        if (rule == null)
            return "";

        return SanitizeFinalPrompt($"{rule.emotion}, {rule.meaning}, {rule.metaphor}, symbolic scene, body presence, objects, textures, shadow fragments, cracked surfaces, residue, decay, cinematic, bleak atmosphere, moody lighting, low saturation, harsh contrast, surreal");
    }

    private string SanitizeFinalPrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return "";

        string sanitized = prompt;
        string[] bannedFragments =
        {
            "whale",
            "belly",
            "ocean",
            "sea",
            "underwater",
            "submerged",
            "fish",
            "vessel"
        };

        foreach (string fragment in bannedFragments)
        {
            sanitized = System.Text.RegularExpressions.Regex.Replace(
                sanitized,
                $@"\b{System.Text.RegularExpressions.Regex.Escape(fragment)}\b",
                "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"\s{2,}", " ");
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"\s*,\s*,", ",");
        sanitized = sanitized.Replace(" ,", ",").Replace(", ", ", ");
        return sanitized.Trim(' ', ',', ';');
    }

    private EmotionRule GetFallbackRule()
    {
        return new EmotionRule
        {
            emotion = "무기력",
            meaning = "한계",
            metaphor = "꺼져가는 불",
            keywords = new[] { "지쳐", "무기력", "힘들", "끝" },
            followups = new[]
            {
                "무엇이 가장 먼저 꺼져가고 있다고 느껴지나요?",
                "지금 멈춰 있는 것은 몸인가요, 마음인가요?"
            }
        };
    }

    private EmotionRule[] GetDefaultRules()
    {
        return new[]
        {
            new EmotionRule
            {
                keywords = new[] { "도망", "숨", "피하", "벗어나" },
                emotion = "불안",
                meaning = "도피",
                metaphor = "흔들리는 유리",
                followups = new[]
                {
                    "무엇으로부터 벗어나고 싶나요?",
                    "지금 가장 피하고 싶은 장면은 무엇인가요?"
                }
            },
            new EmotionRule
            {
                keywords = new[] { "외로", "혼자", "없", "비어" },
                emotion = "고립",
                meaning = "단절",
                metaphor = "끊어진 실",
                followups = new[]
                {
                    "무엇이 가장 비어 있다고 느껴지나요?",
                    "누가, 혹은 무엇이 멀리 있다고 느껴지나요?"
                }
            },
            new EmotionRule
            {
                keywords = new[] { "미안", "죄책", "자책", "후회" },
                emotion = "죄책감",
                meaning = "자기비난",
                metaphor = "무거운 돌",
                followups = new[]
                {
                    "계속 마음에 남는 장면이 있나요?",
                    "누구에게 미안한 마음이 아직 닿아 있나요?"
                }
            },
            new EmotionRule
            {
                keywords = new[] { "화", "억울", "분노", "짜증" },
                emotion = "분노",
                meaning = "충돌",
                metaphor = "붉은 균열",
                followups = new[]
                {
                    "무엇이 그렇게 화나게 했나요?",
                    "그 감정은 누구를 향하고 있다고 느껴지나요?"
                }
            },
            new EmotionRule
            {
                keywords = new[] { "지쳐", "무기력", "아무것도", "힘들" },
                emotion = "무기력",
                meaning = "한계",
                metaphor = "꺼져가는 불",
                followups = new[]
                {
                    "무엇이 가장 먼저 꺼져가고 있다고 느껴지나요?",
                    "지금 멈춰 있는 것은 몸인가요, 마음인가요?"
                }
            },
            new EmotionRule
            {
                keywords = new[] { "사라", "죽고", "끝내", "없어" },
                emotion = "절망",
                meaning = "소멸욕구",
                metaphor = "검은 재",
                followups = new[]
                {
                    "지금 사라지고 싶어지는 이유는 무엇인가요?",
                    "무엇이 너무 견디기 어렵다고 느껴지나요?"
                }
            }
        };
    }

    private void SetQuestionText(string question)
    {
        EnsureQuestionDisplayText();

        if (questionDisplayText != null)
        {
            ApplyQuestionFontFromInputField();
            ApplyQuestionTextLayout();
            questionDisplayText.text = question;
            questionDisplayText.textWrappingMode = TextWrappingModes.Normal;
            questionDisplayText.overflowMode = TextOverflowModes.Overflow;
        }

        if (promptInputField != null && promptInputField.placeholder is TMP_Text placeholderText)
        {
            placeholderText.text = "답변을 입력하세요";
            placeholderText.fontSize = 5.5f;
            placeholderText.alignment = TextAlignmentOptions.Center;
            placeholderText.textWrappingMode = TextWrappingModes.NoWrap;
        }

        if (promptInputField != null && promptInputField.textComponent != null)
        {
            promptInputField.textComponent.fontSize = 5.5f;
            promptInputField.textComponent.alignment = TextAlignmentOptions.Center;
        }
    }

    private string GetPromptForVariant(string fallbackBasePrompt, int variantIndex)
    {
        bool usePreparedPromptSet =
            lastFinalPromptSet.Count > 0 &&
            !string.IsNullOrWhiteSpace(lastFinalPrompt) &&
            string.Equals(fallbackBasePrompt?.Trim(), lastFinalPrompt.Trim(), StringComparison.Ordinal);

        if (usePreparedPromptSet)
        {
            int index = Mathf.Clamp(variantIndex, 0, lastFinalPromptSet.Count - 1);
            string prompt = lastFinalPromptSet[index];
            if (!string.IsNullOrWhiteSpace(prompt))
                return prompt;
        }

        return fallbackBasePrompt;
    }

    private void EnsureQuestionDisplayText()
    {
        if (questionDisplayText != null || promptInputField == null)
            return;

        Transform parent = promptInputField.transform.parent;
        if (parent == null)
            parent = promptInputField.transform;

        Transform existing = parent.Find("Question Text");
        if (existing != null)
        {
            questionDisplayText = existing.GetComponent<TMP_Text>();
            if (questionDisplayText != null)
                return;
        }

        GameObject questionObject = new GameObject("Question Text", typeof(RectTransform));
        questionObject.transform.SetParent(parent, false);

        RectTransform inputRect = promptInputField.GetComponent<RectTransform>();
        RectTransform questionRect = questionObject.GetComponent<RectTransform>();
        if (inputRect != null)
        {
            questionRect.anchorMin = inputRect.anchorMin;
            questionRect.anchorMax = inputRect.anchorMax;
            questionRect.pivot = new Vector2(0.5f, 1f);
            questionRect.anchoredPosition = inputRect.anchoredPosition + new Vector2(0f, -42f);
            questionRect.sizeDelta = new Vector2(Mathf.Max(220f, inputRect.sizeDelta.x * 1.4f), 78f);
            questionRect.localScale = inputRect.localScale;
        }

        questionDisplayText = questionObject.AddComponent<TextMeshProUGUI>();
        questionDisplayText.fontSize = 6.25f;
        questionDisplayText.alignment = TextAlignmentOptions.TopGeoAligned;
        questionDisplayText.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        questionDisplayText.textWrappingMode = TextWrappingModes.Normal;
        questionDisplayText.overflowMode = TextOverflowModes.Overflow;
        questionDisplayText.raycastTarget = false;
        ApplyQuestionFontFromInputField();
        ApplyQuestionTextLayout();
    }

    private void ApplyQuestionTextLayout()
    {
        if (questionDisplayText == null || promptInputField == null)
            return;

        RectTransform inputRect = promptInputField.GetComponent<RectTransform>();
        RectTransform questionRect = questionDisplayText.GetComponent<RectTransform>();
        if (inputRect == null || questionRect == null)
            return;

        questionRect.anchorMin = inputRect.anchorMin;
        questionRect.anchorMax = inputRect.anchorMax;
        questionRect.pivot = new Vector2(0.5f, 1f);
        questionRect.anchoredPosition = inputRect.anchoredPosition + new Vector2(0f, -42f);
        questionRect.sizeDelta = new Vector2(Mathf.Max(220f, inputRect.sizeDelta.x * 1.4f), 78f);
        questionRect.localScale = inputRect.localScale;

        questionDisplayText.fontSize = 6.25f;
        questionDisplayText.alignment = TextAlignmentOptions.TopGeoAligned;
        questionDisplayText.margin = new Vector4(4f, 0f, 4f, 0f);
    }

    private void ApplyQuestionFontFromInputField()
    {
        if (questionDisplayText == null || promptInputField == null)
            return;

        TMP_Text sourceText = promptInputField.textComponent;
        if (sourceText == null && promptInputField.placeholder is TMP_Text placeholderText)
            sourceText = placeholderText;

        if (sourceText == null)
            return;

        if (sourceText.font != null)
            questionDisplayText.font = sourceText.font;

        questionDisplayText.fontSharedMaterial = sourceText.fontSharedMaterial;
        questionDisplayText.fontStyle = sourceText.fontStyle;
    }

    private void SetButtonText(string text)
    {
        if (buttonLabel == null)
            return;

        buttonLabel.text = text;
    }

    private void CacheUiReferences()
    {
        if (generateButton != null)
            buttonLabel = generateButton.GetComponentInChildren<TMP_Text>(true);
    }

    private IEnumerator WaitForImagesAndApply(string promptId)
    {
        while (true)
        {
            SetStatus("Waiting for ComfyUI output...");

            string historyUrl = serverUrl.TrimEnd('/') + "/history/" + UnityWebRequest.EscapeURL(promptId);

            using (UnityWebRequest historyRequest = UnityWebRequest.Get(historyUrl))
            {
                historyRequest.timeout = historyRequestTimeoutSeconds;
                yield return historyRequest.SendWebRequest();

                if (historyRequest.result == UnityWebRequest.Result.Success)
                {
                    string historyJson = historyRequest.downloadHandler.text;
                    if (TryGetHistoryError(historyJson, out string historyError))
                    {
                        Fail("ComfyUI returned an error:\n" + historyError);
                        yield break;
                    }

                    bool completed = TryGetHistoryCompleted(historyJson);
                    List<ComfyImageInfo> images = ExtractImages(historyJson);
                    if (images.Count > 0)
                    {
                        images = images
                            .Where(info => !string.IsNullOrWhiteSpace(info.filename))
                            .GroupBy(info => info.filename + "|" + info.subfolder + "|" + info.type)
                            .Select(group => group.First())
                            .Take(Mathf.Max(1, imageCount))
                            .ToList();

                        if (images.Count > 0)
                        {
                            yield return StartCoroutine(DownloadImages(images));
                            yield break;
                        }
                    }

                    if (completed)
                    {
                        Fail("ComfyUI finished, but no saved images were found in the history output. Check the workflow's SaveImage node and model/node errors.");
                        yield break;
                    }
                }
            }
            yield return new WaitForSeconds(pollInterval);
        }
    }

    private IEnumerator DownloadImages(List<ComfyImageInfo> images)
    {
        SetStatus("Downloading images...");

        foreach (ComfyImageInfo info in images)
        {
            Texture2D texture = null;
            yield return StartCoroutine(DownloadImage(info, tex => texture = tex));

            if (texture != null)
                slideshowTextures.Add(texture);
        }

        if (slideshowTextures.Count == 0)
        {
            Fail("No images were downloaded.");
            yield break;
        }

        if (ApplyTexturesToRenderers(slideshowTextures))
        {
            SetStatus("Applied textures to quads.");
            yield break;
        }

        SetStatus("Applied textures to quads.");
    }

    private IEnumerator DownloadImage(ComfyImageInfo info, Action<Texture2D> onDownloaded)
    {
        string type = string.IsNullOrWhiteSpace(info.type) ? "output" : info.type;
        string imageUrl =
            serverUrl.TrimEnd('/') +
            "/view?filename=" + UnityWebRequest.EscapeURL(info.filename) +
            "&subfolder=" + UnityWebRequest.EscapeURL(info.subfolder ?? "") +
            "&type=" + UnityWebRequest.EscapeURL(type);

        using (UnityWebRequest imageRequest = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            imageRequest.timeout = imageDownloadTimeoutSeconds;
            yield return imageRequest.SendWebRequest();

            if (imageRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[ComfyImageGenerator] Image download failed: " + imageRequest.error);
                onDownloaded?.Invoke(null);
                yield break;
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(imageRequest);
            onDownloaded?.Invoke(texture);
        }
    }

    private void ClearTextures()
    {
        foreach (Texture2D texture in slideshowTextures)
        {
            if (texture != null)
            {
                Destroy(texture);
            }
        }

        slideshowTextures.Clear();
    }

    private void StopSlideshow()
    {
        // We only clear generated results here.
        // Stopping all coroutines would also kill the active generation routine.
    }

    private bool ApplyTexturesToRenderers(List<Texture2D> textures)
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
            return false;

        int appliedCount = 0;

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer renderer = targetRenderers[i];
            if (renderer == null)
                continue;

            if (i >= textures.Count || textures[i] == null)
                continue;

            Material material = renderer.material;
            if (material == null)
                continue;

            Texture2D texture = textures[i];
            material.mainTexture = texture;

            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);

            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);

            renderer.enabled = true;
            appliedCount++;
        }

        return appliedCount > 0;
    }

    private void ResolveSceneReferences()
    {
        if (promptInputField == null)
            promptInputField = FindFirstObjectByType<TMP_InputField>(FindObjectsInactive.Include);

        if (generateButton == null)
            generateButton = FindFirstObjectByType<Button>(FindObjectsInactive.Include);

        ResolveSkyCamera();

        if (promptCanvasRoot == null && promptInputField != null)
        {
            Canvas canvas = promptInputField.GetComponentInParent<Canvas>(true);
            if (canvas != null)
                promptCanvasRoot = canvas.gameObject;
        }

        if (promptTriggerRoot == null)
        {
            CapsuleProximityPrompt proximityPrompt = FindFirstObjectByType<CapsuleProximityPrompt>(FindObjectsInactive.Include);
            if (proximityPrompt != null)
                promptTriggerRoot = proximityPrompt.gameObject;
        }

        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(r => r != null && r.gameObject.name.IndexOf("Quad", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(r => r.gameObject.name)
                .ToArray();
    }

    private UnityWebRequest CreateJsonPost(string url, string jsonBody, int timeoutSeconds)
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes(jsonBody);
        UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(data);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = Mathf.Max(1, timeoutSeconds);
        return request;
    }

    private string ReplacePromptText(string workflowJson, string nodeId, string newPrompt)
    {
        try
        {
            JObject root = JObject.Parse(workflowJson);
            JObject node = root[nodeId] as JObject;
            if (node == null)
                return null;

            JObject inputs = node["inputs"] as JObject;
            if (inputs == null)
                return null;

            inputs["text"] = newPrompt;
            return root.ToString(Newtonsoft.Json.Formatting.None);
        }
        catch (Exception e)
        {
            Debug.LogError("[ComfyImageGenerator] ReplacePromptText failed: " + e.Message);
            return null;
        }
    }

    private string ReplaceFilenamePrefix(string workflowJson, string prefix)
    {
        try
        {
            JObject root = JObject.Parse(workflowJson);

            foreach (JProperty property in root.Properties())
            {
                JObject node = property.Value as JObject;
                if (node == null)
                    continue;

                string classType = node["class_type"]?.ToString();
                if (!string.Equals(classType, "SaveImage", StringComparison.OrdinalIgnoreCase))
                    continue;

                JObject inputs = node["inputs"] as JObject;
                if (inputs == null)
                    continue;

                inputs["filename_prefix"] = prefix;
                return root.ToString(Newtonsoft.Json.Formatting.None);
            }

            return workflowJson;
        }
        catch (Exception e)
        {
            Debug.LogError("[ComfyImageGenerator] ReplaceFilenamePrefix failed: " + e.Message);
            return workflowJson;
        }
    }

    private string ReplaceNegativePromptText(string workflowJson, string nodeId, string newPrompt)
    {
        try
        {
            JObject root = JObject.Parse(workflowJson);
            JObject node = root[nodeId] as JObject;
            if (node == null)
                return workflowJson;

            JObject inputs = node["inputs"] as JObject;
            if (inputs == null)
                return workflowJson;

            inputs["text"] = newPrompt;
            return root.ToString(Newtonsoft.Json.Formatting.None);
        }
        catch (Exception e)
        {
            Debug.LogError("[ComfyImageGenerator] ReplaceNegativePromptText failed: " + e.Message);
            return workflowJson;
        }
    }

    private string ReplaceBatchSize(string workflowJson, int batchSize)
    {
        try
        {
            JObject root = JObject.Parse(workflowJson);

            foreach (JProperty property in root.Properties())
            {
                JObject node = property.Value as JObject;
                if (node == null)
                    continue;

                string classType = node["class_type"]?.ToString();
                if (!string.Equals(classType, "EmptyLatentImage", StringComparison.OrdinalIgnoreCase))
                    continue;

                JObject inputs = node["inputs"] as JObject;
                if (inputs == null)
                    continue;

                inputs["batch_size"] = batchSize;
                return root.ToString(Newtonsoft.Json.Formatting.None);
            }

            return workflowJson;
        }
        catch (Exception e)
        {
            Debug.LogError("[ComfyImageGenerator] ReplaceBatchSize failed: " + e.Message);
            return workflowJson;
        }
    }

    private string ReplaceSeed(string workflowJson, int seed)
    {
        try
        {
            JObject root = JObject.Parse(workflowJson);

            foreach (JProperty property in root.Properties())
            {
                JObject node = property.Value as JObject;
                if (node == null)
                    continue;

                string classType = node["class_type"]?.ToString();
                if (!string.Equals(classType, "KSampler", StringComparison.OrdinalIgnoreCase))
                    continue;

                JObject inputs = node["inputs"] as JObject;
                if (inputs == null)
                    continue;

                inputs["seed"] = Math.Abs((long)seed);
                return root.ToString(Newtonsoft.Json.Formatting.None);
            }

            return workflowJson;
        }
        catch (Exception e)
        {
            Debug.LogError("[ComfyImageGenerator] ReplaceSeed failed: " + e.Message);
            return workflowJson;
        }
    }

    private List<ComfyImageInfo> ExtractImages(string json)
    {
        List<ComfyImageInfo> results = new List<ComfyImageInfo>();

        if (string.IsNullOrWhiteSpace(json))
            return results;

        try
        {
            JToken root = JToken.Parse(json);
            JToken outputToken = root["outputs"] ?? root;
            CollectImages(outputToken, results);
        }
        catch (Exception e)
        {
            Debug.LogError("[ComfyImageGenerator] Failed to parse history JSON: " + e.Message);
        }

        return results;
    }

    private bool TryGetHistoryCompleted(string json)
    {
        try
        {
            JToken root = JToken.Parse(json);
            foreach (JToken token in root.SelectTokens("$..completed"))
            {
                if (token.Type == JTokenType.Boolean && token.Value<bool>())
                    return true;
            }

            foreach (JToken token in root.SelectTokens("$..status_str"))
            {
                string status = token?.ToString();
                if (string.IsNullOrWhiteSpace(status))
                    continue;

                if (status.IndexOf("success", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    status.IndexOf("complete", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    status.IndexOf("finished", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ComfyImageGenerator] TryGetHistoryCompleted parse failed: " + e.Message);
        }

        return false;
    }

    private bool TryGetHistoryError(string json, out string message)
    {
        message = null;

        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            JToken root = JToken.Parse(json);
            foreach (JToken token in root.SelectTokens("$..status_str"))
            {
                string status = token?.ToString();
                if (string.IsNullOrWhiteSpace(status))
                    continue;

                if (status.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    status.IndexOf("fail", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    message = status;
                    return true;
                }
            }

            foreach (JToken token in root.SelectTokens("$..exception"))
            {
                string exceptionText = token?.ToString();
                if (!string.IsNullOrWhiteSpace(exceptionText))
                {
                    message = exceptionText;
                    return true;
                }
            }

            foreach (JToken token in root.SelectTokens("$..error"))
            {
                string errorText = token?.ToString();
                if (!string.IsNullOrWhiteSpace(errorText))
                {
                    message = errorText;
                    return true;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ComfyImageGenerator] TryGetHistoryError parse failed: " + e.Message);
        }

        return false;
    }

    private void CollectImages(JToken token, List<ComfyImageInfo> results)
    {
        if (token == null)
            return;

        if (token is JObject obj)
        {
            if (obj.TryGetValue("filename", out JToken filenameToken))
            {
                string filename = filenameToken?.ToString();
                if (!string.IsNullOrWhiteSpace(filename))
                {
                    results.Add(new ComfyImageInfo
                    {
                        filename = filename,
                        subfolder = obj["subfolder"]?.ToString() ?? "",
                        type = obj["type"]?.ToString() ?? "output"
                    });
                }
            }

            foreach (JProperty property in obj.Properties())
            {
                CollectImages(property.Value, results);
            }
        }
        else if (token is JArray array)
        {
            foreach (JToken child in array)
            {
                CollectImages(child, results);
            }
        }
    }

    private void SetStatus(string message)
    {
        Debug.Log("[ComfyImageGenerator] " + message);

        if (statusText != null)
            statusText.text = message;
    }

    private void Fail(string message)
    {
        Debug.LogError("[ComfyImageGenerator] " + message);

        if (statusText != null)
            statusText.text = "Error: " + message;

        isGenerating = false;

        if (generateButton != null)
            generateButton.interactable = true;
    }
}
