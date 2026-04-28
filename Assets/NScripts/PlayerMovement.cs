using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Move")]
    public float speed = 5f;
    public float gravity = -20f;
    public float jumpForce = 5f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundDistance = 0.25f;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float groundProbeHeight = 2.0f;
    [SerializeField] private float groundProbeDistance = 4.0f;
    [SerializeField] private float fallResetY = -8f;
    [SerializeField] private float maxUngroundedBeforeReset = 2.0f;

    [Header("Walkable Area")]
    [SerializeField] private bool createWalkableStreetCollider = true;
    [SerializeField] private Vector3 walkableStreetCenter = new Vector3(1.3f, -0.05f, 20f);
    [SerializeField] private Vector3 walkableStreetSize = new Vector3(16f, 0.22f, 46f);

    [Header("Torch")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float torchMoveDistance = 0.9f;
    [SerializeField] private float torchMoveDuration = 0.16f;
    [SerializeField] private float burnRadius = 2.2f;
    [SerializeField] private float burnScaleDownDuration = 0.35f;
    [SerializeField] private float torchLightRange = 16.0f;
    [SerializeField] private float torchRestIntensity = 3.0f;
    [SerializeField] private float torchActiveIntensity = 7.0f;
    [SerializeField] private Color torchColor = new Color(1f, 0.55f, 0.15f, 1f);
    [SerializeField] private Color smokeColor = new Color(0.25f, 0.22f, 0.2f, 0.55f);

    [Header("Smoke Ritual")]
    [SerializeField] private Vector3 smokeVolumeCenter = new Vector3(1.3f, 1.7f, 26f);
    [SerializeField] private Vector3 smokeVolumeSize = new Vector3(18f, 4f, 36f);
    [SerializeField] private float smokeRatePerBurnedQuad = 2.0f;
    [SerializeField] private float maxSmokeRate = 18f;
    [SerializeField] private float returnSlideDuration = 1.8f;
    [SerializeField] private float smokeClearDuration = 2.5f;
    [SerializeField] private float finalPushBackDistance = 1.2f;
    [SerializeField] private float finalPushDuration = 0.22f;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;
    private Vector3 lastSafePosition;
    private float ungroundedTimer;

    private Transform torchAnchor;
    private Transform torchVisual;
    private Light torchLight;
    private bool torchBusy;
    private ParticleSystem ambientSmoke;
    private Vector3 returnTargetPosition;
    private bool hasReturnTarget;
    private bool returnRoutineStarted;
    private int burnableQuadCount;
    private readonly HashSet<int> burnedIds = new HashSet<int>();

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        controller = GetComponent<CharacterController>();
        EnsureCameraTransform();
        CreateTorchIfNeeded();
        CaptureReturnTarget();
        CreateAmbientSmokeVolume();
        burnableQuadCount = CountBurnableQuads();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        EnsureCameraTransform();
        CreateTorchIfNeeded();
    }

    void Start()
    {
        if (!Application.isPlaying)
            return;

        controller = GetComponent<CharacterController>();
        EnsureWalkableStreetCollider();
        lastSafePosition = transform.position;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        EnsureCameraTransform();
        CreateTorchIfNeeded();
    }

    void Update()
    {
        if (!Application.isPlaying)
            return;

        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        HandleMovement();

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            ActivateTorch();
        }
    }

    private void HandleMovement()
    {
        isGrounded = CheckGrounded();

        if (isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
            ungroundedTimer = 0f;
            lastSafePosition = transform.position;
        }
        else if (!isGrounded)
        {
            ungroundedTimer += Time.deltaTime;
        }

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 move = (right * h + forward * v).normalized;
        Vector3 horizontalMove = move * speed * Time.deltaTime;
        if (horizontalMove.sqrMagnitude > 0.000001f && HasGroundBelow(transform.position + horizontalMove))
        {
            controller.Move(horizontalMove);
        }

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            velocity.y = jumpForce;
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        isGrounded = CheckGrounded();
        if (isGrounded)
        {
            ungroundedTimer = 0f;
            lastSafePosition = transform.position;
        }

        if (transform.position.y < fallResetY || (ungroundedTimer > maxUngroundedBeforeReset && velocity.y < -1f))
        {
            ResetToLastSafePosition();
        }
    }

    private bool CheckGrounded()
    {
        if (groundCheck != null)
        {
            return Physics.CheckSphere(groundCheck.position, groundDistance, GetGroundMask(), QueryTriggerInteraction.Ignore);
        }

        Vector3 origin = transform.position + Vector3.up * 0.1f;
        return Physics.Raycast(origin, Vector3.down, 1.2f, GetGroundMask());
    }

    private bool HasGroundBelow(Vector3 position)
    {
        Vector3 origin = position + Vector3.up * groundProbeHeight;
        return Physics.Raycast(origin, Vector3.down, groundProbeDistance, GetGroundMask(), QueryTriggerInteraction.Ignore);
    }

    private int GetGroundMask()
    {
        return groundMask.value == 0 ? Physics.DefaultRaycastLayers : groundMask.value;
    }

    private void ResetToLastSafePosition()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        if (controller != null)
            controller.enabled = false;

        transform.position = lastSafePosition;
        velocity = Vector3.zero;
        ungroundedTimer = 0f;

        if (controller != null)
            controller.enabled = true;
    }

    private void EnsureWalkableStreetCollider()
    {
        if (!createWalkableStreetCollider)
            return;

        const string floorName = "RuntimeWalkableStreetCollider";
        if (GameObject.Find(floorName) != null)
            return;

        GameObject floor = new GameObject(floorName);
        floor.transform.position = walkableStreetCenter;
        floor.layer = 0;

        BoxCollider collider = floor.AddComponent<BoxCollider>();
        collider.size = walkableStreetSize;
        collider.center = Vector3.zero;
        collider.isTrigger = false;
    }

    public void ActivateTorch()
    {
        if (!Application.isPlaying)
            return;

        if (!torchBusy)
            StartCoroutine(TorchRoutine());
    }

    private void EnsureCameraTransform()
    {
        if (cameraTransform != null)
            return;

        Camera cam = GetComponentInChildren<Camera>(true);
        if (cam == null)
            cam = Camera.main;

        if (cam != null)
            cameraTransform = cam.transform;
    }

    private void CreateTorchIfNeeded()
    {
        if (!Application.isPlaying)
            return;

        if (cameraTransform == null)
            return;

        Transform existing = cameraTransform.Find("TorchAnchor");
        if (existing != null)
        {
            torchAnchor = existing;
            torchVisual = torchAnchor.Find("TorchVisual");
            torchLight = torchAnchor.GetComponentInChildren<Light>(true);
            ApplyTorchLightSettings(torchRestIntensity);
            return;
        }

        GameObject anchor = new GameObject("TorchAnchor");
        anchor.transform.SetParent(cameraTransform, false);
        anchor.transform.localPosition = new Vector3(0.28f, -0.18f, 0.58f);
        anchor.transform.localRotation = Quaternion.identity;
        torchAnchor = anchor.transform;

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "TorchVisual";
        visual.transform.SetParent(torchAnchor, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = new Vector3(0.16f, 0.16f, 0.62f);

        Collider visualCollider = visual.GetComponent<Collider>();
        if (visualCollider != null)
            DestroySafe(visualCollider);

        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.color = torchColor;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(1f, 0.35f, 0.1f) * 1.8f);
            material.SetFloat("_Glossiness", 0.08f);
            material.SetFloat("_Metallic", 0.0f);
            renderer.material = material;
        }

        torchVisual = visual.transform;

        GameObject lightObject = new GameObject("TorchLight");
        lightObject.transform.SetParent(torchAnchor, false);
        lightObject.transform.localPosition = new Vector3(0f, 0f, 0.42f);
        torchLight = lightObject.AddComponent<Light>();
        torchLight.type = LightType.Point;
        torchLight.color = new Color(1f, 0.5f, 0.15f, 1f);
        ApplyTorchLightSettings(torchRestIntensity);
    }

    private void ApplyTorchLightSettings(float intensity)
    {
        if (torchLight == null)
            return;

        torchLight.range = torchLightRange;
        torchLight.intensity = intensity;
        torchLight.shadows = LightShadows.Soft;
    }

    private void DestroySafe(Object obj)
    {
        if (obj == null)
            return;

        Destroy(obj);
    }

    private IEnumerator TorchRoutine()
    {
        if (torchAnchor == null || torchVisual == null)
            yield break;

        torchBusy = true;

        Vector3 restPos = new Vector3(0.28f, -0.18f, 0.58f);
        Vector3 activePos = restPos + new Vector3(0f, 0f, torchMoveDistance);

        yield return StartCoroutine(MoveTorch(restPos, activePos, torchMoveDuration, true));
        BurnQuadsInRange();
        yield return new WaitForSeconds(0.2f);
        yield return StartCoroutine(MoveTorch(activePos, restPos, torchMoveDuration, false));

        torchBusy = false;
    }

    private IEnumerator MoveTorch(Vector3 from, Vector3 to, float duration, bool brighten)
    {
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            torchAnchor.localPosition = Vector3.Lerp(from, to, t);

            if (torchLight != null)
            {
                float fromIntensity = brighten ? torchRestIntensity : torchActiveIntensity;
                float toIntensity = brighten ? torchActiveIntensity : torchRestIntensity;
                ApplyTorchLightSettings(Mathf.Lerp(fromIntensity, toIntensity, t));
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        torchAnchor.localPosition = to;

        if (torchLight != null)
            ApplyTorchLightSettings(brighten ? torchActiveIntensity : torchRestIntensity);
    }

    private void BurnQuadsInRange()
    {
        if (cameraTransform == null || torchAnchor == null)
            return;

        Vector3 origin = torchAnchor.position + torchAnchor.forward * 0.42f;
        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            GameObject target = renderer.gameObject;
            if (target == null || !target.name.Contains("Quad"))
                continue;

            int id = target.GetInstanceID();
            if (burnedIds.Contains(id))
                continue;

            Vector3 closestPoint = renderer.bounds.center;
            float distance = Vector3.Distance(origin, closestPoint);

            if (distance > burnRadius)
                continue;

            burnedIds.Add(id);
            TryStartFinalPushIfAllQuadsBurned();
            StartCoroutine(BurnQuadRoutine(target, renderer));
        }
    }

    private IEnumerator BurnQuadRoutine(GameObject quadObject, Renderer renderer)
    {
        if (quadObject == null)
            yield break;

        Vector3 worldPos = renderer != null ? renderer.bounds.center : quadObject.transform.position;
        SpawnSmoke(worldPos);

        if (renderer != null)
            renderer.enabled = false;

        Collider[] colliders = quadObject.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            if (collider != null)
                collider.enabled = false;
        }

        float elapsed = 0f;
        Vector3 startScale = quadObject.transform.localScale;
        Vector3 endScale = Vector3.zero;
        float duration = Mathf.Max(0.05f, burnScaleDownDuration);

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            quadObject.transform.localScale = Vector3.Lerp(startScale, endScale, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        quadObject.transform.localScale = endScale;
        quadObject.SetActive(false);
        OnQuadBurnedComplete();
    }

    private void OnQuadBurnedComplete()
    {
        IncreaseAmbientSmoke();

        if (returnRoutineStarted)
            return;

        if (burnableQuadCount <= 0)
            burnableQuadCount = CountBurnableQuads(includeInactive: true);

        TryStartFinalPushIfAllQuadsBurned();
    }

    private void TryStartFinalPushIfAllQuadsBurned()
    {
        if (returnRoutineStarted)
            return;

        if (burnableQuadCount <= 0)
            burnableQuadCount = CountBurnableQuads(includeInactive: true);

        if (burnedIds.Count >= Mathf.Max(1, burnableQuadCount))
            StartCoroutine(FinalPushAndReturnRoutine());
    }

    private int CountBurnableQuads(bool includeInactive = false)
    {
        Renderer[] renderers = includeInactive
            ? FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            : FindObjectsByType<Renderer>(FindObjectsSortMode.None);

        int count = 0;
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null && renderer.gameObject != null && renderer.gameObject.name.Contains("Quad"))
                count++;
        }

        return count;
    }

    private void CaptureReturnTarget()
    {
        CapsuleProximityPrompt prompt = FindFirstObjectByType<CapsuleProximityPrompt>(FindObjectsInactive.Include);
        if (prompt == null)
            return;

        returnTargetPosition = prompt.transform.position;
        hasReturnTarget = true;
    }

    private void CreateAmbientSmokeVolume()
    {
        if (ambientSmoke != null)
            return;

        GameObject smokeRoot = new GameObject("BurnedEmotionSmokeVolume");
        smokeRoot.transform.position = smokeVolumeCenter;

        BoxCollider volume = smokeRoot.AddComponent<BoxCollider>();
        volume.isTrigger = true;
        volume.size = smokeVolumeSize;

        ambientSmoke = smokeRoot.AddComponent<ParticleSystem>();
        ambientSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ambientSmoke.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 6f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.55f, 1.65f);
        main.startColor = new Color(smokeColor.r, smokeColor.g, smokeColor.b, 0.13f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 260;

        var emission = ambientSmoke.emission;
        emission.rateOverTime = 0f;

        var shape = ambientSmoke.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = smokeVolumeSize;

        var noise = ambientSmoke.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.18f;
        noise.scrollSpeed = 0.08f;

        var renderer = ambientSmoke.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader != null)
        {
            renderer.material = new Material(shader);
            renderer.material.color = smokeColor;
        }

        ambientSmoke.Play();
    }

    private void IncreaseAmbientSmoke()
    {
        if (ambientSmoke == null)
            CreateAmbientSmokeVolume();

        if (ambientSmoke == null)
            return;

        var emission = ambientSmoke.emission;
        float currentRate = 0f;
        ParticleSystem.MinMaxCurve rate = emission.rateOverTime;
        currentRate = rate.constant;
        emission.rateOverTime = Mathf.Min(maxSmokeRate, currentRate + smokeRatePerBurnedQuad);

        if (!ambientSmoke.isPlaying)
            ambientSmoke.Play();
    }

    private IEnumerator FinalPushAndReturnRoutine()
    {
        returnRoutineStarted = true;

        yield return StartCoroutine(PushAwayFromBurnedQuads());

        ComfyImageGenerator generator = FindFirstObjectByType<ComfyImageGenerator>(FindObjectsInactive.Include);
        if (generator != null)
            generator.BrightenReactiveSkyAfterRelease();

        yield return StartCoroutine(ClearSmokeAndReturnRoutine());
    }

    private IEnumerator PushAwayFromBurnedQuads()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        Vector3 pushDirection = -transform.forward;
        if (cameraTransform != null)
            pushDirection = -cameraTransform.forward;

        pushDirection.y = 0f;
        if (pushDirection.sqrMagnitude < 0.001f)
            pushDirection = -transform.forward;

        pushDirection.Normalize();

        float elapsed = 0f;
        float duration = Mathf.Max(0.03f, finalPushDuration);
        float totalDistance = Mathf.Max(0f, finalPushBackDistance);
        float movedDistance = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float targetDistance = totalDistance * eased;
            float deltaDistance = targetDistance - movedDistance;
            movedDistance = targetDistance;

            Vector3 delta = pushDirection * deltaDistance;
            if (controller != null && controller.enabled)
                controller.Move(delta);
            else
                transform.position += delta;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator ClearSmokeAndReturnRoutine()
    {

        if (!hasReturnTarget)
            CaptureReturnTarget();

        float elapsed = 0f;
        float duration = Mathf.Max(0.1f, smokeClearDuration);
        float startRate = 0f;

        if (ambientSmoke != null)
            startRate = ambientSmoke.emission.rateOverTime.constant;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            if (ambientSmoke != null)
            {
                var emission = ambientSmoke.emission;
                emission.rateOverTime = Mathf.Lerp(startRate, 0f, t);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (ambientSmoke != null)
        {
            var emission = ambientSmoke.emission;
            emission.rateOverTime = 0f;
            ambientSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (hasReturnTarget)
            yield return StartCoroutine(SlideToPosition(returnTargetPosition));
    }

    private IEnumerator SlideToPosition(Vector3 targetPosition)
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        Vector3 start = transform.position;
        Vector3 end = new Vector3(targetPosition.x, start.y, targetPosition.z);
        float elapsed = 0f;
        float duration = Mathf.Max(0.1f, returnSlideDuration);
        velocity = Vector3.zero;

        while (elapsed < duration)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            Vector3 next = Vector3.Lerp(start, end, t);
            Vector3 delta = next - transform.position;

            if (controller != null && controller.enabled)
                controller.Move(delta);
            else
                transform.position = next;

            elapsed += Time.deltaTime;
            yield return null;
        }

        Vector3 finalDelta = end - transform.position;
        if (controller != null && controller.enabled)
            controller.Move(finalDelta);
        else
            transform.position = end;

        lastSafePosition = transform.position;
        returnRoutineStarted = false;
        burnedIds.Clear();
    }

    private void SpawnSmoke(Vector3 position)
    {
        GameObject smokeRoot = new GameObject("TorchSmoke");
        smokeRoot.transform.position = position;

        ParticleSystem ps = smokeRoot.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
        main.startColor = smokeColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 32;
        main.duration = 1.2f;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 6, 9)
        });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.45f, 0.4f, 0.35f), 0f),
                new GradientColorKey(new Color(0.1f, 0.1f, 0.1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.45f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader != null)
        {
            renderer.material = new Material(shader);
            renderer.material.color = smokeColor;
        }

        ps.Play();
        Destroy(smokeRoot, 2.5f);
    }
}
