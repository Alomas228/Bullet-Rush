using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFollow : MonoBehaviour
{
    [System.Serializable]
    public struct PersistedPose
    {
        public Vector3 position;
        public Quaternion rotation;
        public bool hasPose;
    }

    public static PersistedPose SavedPose;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPendingPose()
    {
        SavedPose = default;
    }

    public event System.Action OnReachedMenu;

    public bool IsTransitioning => transitioning;

    [Header("Follow Target")]
    [SerializeField] private Transform target;

    [Header("Gameplay")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -8f);
    [SerializeField] private float followSpeed = 10f;

    [Header("Menu")]
    [SerializeField] private Vector3 menuPosition = new Vector3(0f, 40f, -20f);
    [SerializeField] private Vector3 menuLookAt = Vector3.zero;
    [SerializeField] private float transitionSpeed = 3.5f;

    [Header("Screen Shake")]
    [SerializeField] private float shakeAmplitude = 0.35f;
    [SerializeField] private float shakeDecay = 3f;
    [SerializeField] private float maxShakeRotation = 3f;

    [Header("Mouse Parallax")]
    [Tooltip("Сила смещения камеры в сторону курсора.")]
    [SerializeField] private float mouseParallaxStrength = 1.2f;

    [Tooltip("Скорость плавного подтягивания параллакса к курсору.")]
    [SerializeField] private float parallaxSmoothSpeed = 12f;

    [Tooltip("Расстояние до цели, ниже которого камера ставится точно и «замирает». 0 = не замораживать.")]
    [SerializeField] private float freezeDistanceThreshold = 0.001f;

    [Tooltip("Разница поворота до цели, ниже которой камера ставится точно и «замирает».")]
    [SerializeField] private float freezeAngleThreshold = 0.1f;

    [Tooltip("Порог «замирания» параллакса: насколько он уже близок к целевому значению.")]
    [SerializeField] private float parallaxFreezeThreshold = 0.001f;

    [Tooltip("Сила «подхлёста» камеры за быстро движущейся мышью (0 = выключено).")]
    [SerializeField] private float mouseVelocityKick = 0.25f;

    [Tooltip("Сколько времени камера должна быть почти неподвижна, прежде чем «замереть».")]
    [SerializeField] private float freezeIdleTime = 0.5f;

    private bool followTarget;
    private bool transitioning;
    private bool menuReported;
    private Vector3 transitionOrigin;
    private Quaternion transitionOriginRotation;
    private Vector3 transitionOriginParallax;
    private float transitionElapsed;
    private float transitionDuration;
    private bool transitionInitialized;
    private Camera viewCamera;
    private float gameplayFieldOfView;
    private float transitionOriginFov;

    private float trauma;
    private float fireTrauma;
    private float shakePhase;
    private Vector3 basePosition;
    private Quaternion baseRotation;
    private bool baseInitialized;

    // База тряски с учётом параллакса мыши.
    // Без неё тряска возвращала камеру к центру, игнорируя сдвиг за курсором,
    // и камера «дёргалась» между параллаксом и базой.
    private Vector3 shakeBasePosition;

    private Vector3 smoothedParallax;
    private float freezeTimer;
    private Vector3 cachedParallaxOffset;

    public void AddShake(float amount)
    {
        if (amount <= 0f) return;
        if (SettingsManager.Instance != null &&
            !SettingsManager.Instance.ScreenShakeEnabled)
            return;

        // Damage, explosions and impacts refresh the stronger impulse;
        // multiple simultaneous hits must not stack into a violent shake.
        trauma = Mathf.Max(trauma, Mathf.Min(amount, .78f));
    }

    public void AddFireShake(float amount)
    {
        if (amount <= 0f)
            return;

        if (SettingsManager.Instance != null &&
            !SettingsManager.Instance.FireShakeEnabled)
            return;

        fireTrauma = Mathf.Min(fireTrauma + amount, .42f);
    }

    private void Start()
    {
        EnsureViewCamera();
        GameStateManager instance = GameStateManager.Instance;

        bool isPlaying =
            instance != null &&
            instance.CurrentState == GameState.Playing;

        if (SavedPose.hasPose)
        {
            transform.SetPositionAndRotation(SavedPose.position, SavedPose.rotation);
            basePosition = transform.position;
            baseRotation = transform.rotation;
            baseInitialized = true;
            shakeBasePosition = transform.position;
            followTarget = false;
            transitioning = true;
        }
        else if (isPlaying)
        {
            // A scene may already have received Playing before Start. Always
            // enter gameplay from the menu framing, even on a restarted scene.
            MoveToMenuPositionImmediate();
            transitioning = true;
        }
        else
        {
            MoveToMenuPositionImmediate();
        }

        SavedPose = default;
        menuReported = false;

        if (instance != null)
            instance.OnGameStateChanged += HandleStateChanged;
    }

    private void OnDestroy()
    {
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.OnGameStateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(GameState state)
    {
        switch (state)
        {
            case GameState.Menu:
                followTarget = false;
                transitioning = true;
                transitionInitialized = false;
                trauma = 0f;
                fireTrauma = 0f;
                menuReported = false;
                break;

            case GameState.Playing:
                followTarget = false;
                transitioning = true;
                transitionInitialized = false;
                menuReported = false;
                break;

            case GameState.GameOver:
                break;
        }
    }

    private void LateUpdate()
    {
        RefreshParallaxOffsetOncePerFrame();

        if (transitioning)
        {
            UpdateTransition();
            ApplyShakeOverBase();
            return;
        }

        if (followTarget && target != null)
        {
            UpdateFollowBase();
            ApplyShakeOverBase();
            return;
        }

        ReportMenuSettledIfNeeded();
    }

    // Позиция и скорость мыши читаются один раз за кадр
    // и переиспользуются в FreezeIfSettled и ApplyMouseParallaxToPosition.
    private void RefreshParallaxOffsetOncePerFrame()
    {
        if (Mouse.current == null || MobileControls.IsTouchDevice)
        {
            cachedParallaxOffset = Vector3.zero;
            return;
        }

        Vector2 mousePos =
            Mouse.current.position.ReadValue();

        float normalizedX =
            (mousePos.x / Screen.width) * 2f - 1f;

        float normalizedY =
            (mousePos.y / Screen.height) * 2f - 1f;

        if (mouseVelocityKick > 0f)
        {
            // Подхлёст от скорости мыши: камера «летит» за быстрым
            // движением курсора и плавно успокаивается после остановки.
            Vector2 delta =
                Mouse.current.delta.ReadValue();

            normalizedX +=
                Mathf.Clamp(
                    delta.x / Screen.width,
                    -1f,
                    1f
                ) * mouseVelocityKick;

            // По вертикали подхлёст слабее — амплитуда и так занижена
            normalizedY +=
                Mathf.Clamp(
                    delta.y / Screen.height,
                    -1f,
                    1f
                ) * mouseVelocityKick * 0.5f;
        }

        cachedParallaxOffset =
            new Vector3(
                normalizedX,
                normalizedY * 0.5f,
                0f
            ) * mouseParallaxStrength;
    }

    private void EnsureBaseInitialized()
    {
        if (baseInitialized)
            return;

        basePosition = transform.position;
        baseRotation = transform.rotation;
        baseInitialized = true;
    }

    private void UpdateFollowBase()
    {
        EnsureBaseInitialized();

        Vector3 targetPos = GetGameplayPosition();

        basePosition = Vector3.Lerp(
            basePosition,
            targetPos,
            followSpeed * Time.deltaTime
        );

        Quaternion targetRotation =
            GetGameplayLookRotation();

        baseRotation = Quaternion.Slerp(
            baseRotation,
            targetRotation,
            followSpeed * Time.deltaTime
        );

        FreezeIfSettled(
            targetPos,
            targetRotation
        );

        transform.position = basePosition;
        ApplyMouseParallaxToPosition();
        transform.rotation = baseRotation;

        shakeBasePosition = transform.position;
    }

    // Когда игрок и мышь почти не двигаются,
    // камера ставится точно и перестаёт «дышать» каждый кадр —
    // тени и рендер не пересчитываются.
    private void FreezeIfSettled(
        Vector3 targetPos,
        Quaternion targetRotation)
    {
        if (freezeDistanceThreshold <= 0f)
            return;

        // Без задержки — мгновенная фиксация (старое поведение)
        if (freezeIdleTime <= 0f)
        {
            basePosition = targetPos;
            baseRotation = targetRotation;
            return;
        }

        if (Vector3.Distance(
                basePosition,
                targetPos
            ) > freezeDistanceThreshold)
        {
            freezeTimer = 0f;
            return;
        }

        if (Quaternion.Angle(
                baseRotation,
                targetRotation
            ) > freezeAngleThreshold)
        {
            freezeTimer = 0f;
            return;
        }

        if (Vector3.Distance(
                smoothedParallax,
                GetMouseParallaxOffset()
            ) > parallaxFreezeThreshold)
        {
            freezeTimer = 0f;
            return;
        }

        // Некоторое время камера продолжает «дышать» рядом с целью,
        // а затем уже замирает — без резкой остановки.
        freezeTimer += Time.deltaTime;

        if (freezeTimer < freezeIdleTime)
            return;

        basePosition = targetPos;
        baseRotation = targetRotation;
    }

    private Quaternion GetGameplayLookRotation()
    {
        if (target == null)
            return baseRotation;

        Vector3 destination =
            target.position + offset;

        return Quaternion.LookRotation(
            target.position - destination
        );
    }

    private Vector3 GetGameplayPosition()
    {
        Vector3 destination = target.position + offset;
        if (RamLiftArena.Active != null)
            destination = RamLiftArena.Active.CameraPosition(destination, offset,
                Screen.width / (float)Mathf.Max(Screen.height, 1));
        return destination;
    }

    private Vector3 GetMouseParallaxOffset()
    {
        return cachedParallaxOffset;
    }

    private void ApplyMouseParallaxToPosition()
    {
        Vector3 parallaxTarget =
            GetMouseParallaxOffset();

        float smooth =
            parallaxSmoothSpeed * Time.deltaTime;

        smoothedParallax =
            Vector3.Lerp(
                smoothedParallax,
                parallaxTarget,
                smooth
            );

        if (Vector3.Distance(
                smoothedParallax,
                parallaxTarget
            ) <= parallaxFreezeThreshold)
        {
            smoothedParallax =
                parallaxTarget;
        }

        if (smoothedParallax == Vector3.zero)
            return;

        transform.position += smoothedParallax;
    }

    private void ApplyShakeOverBase()
    {
        if (trauma <= 0f && fireTrauma <= 0f)
            return;

        trauma = Mathf.Max(
            0f,
            trauma - shakeDecay * Time.unscaledDeltaTime
        );
        fireTrauma = Mathf.Max(0f, fireTrauma - shakeDecay * 1.55f * Time.unscaledDeltaTime);

        // Use the dominant channel, never their sum. Smooth noise avoids
        // the alternating-frame pops caused by independent random offsets.
        float dominant = Mathf.Max(trauma, fireTrauma);
        float strength = dominant * dominant;
        shakePhase += Time.unscaledDeltaTime * 19f;

        Vector3 offset =
            new Vector3(
                ShakeNoise(12.7f),
                ShakeNoise(47.1f) * .35f,
                ShakeNoise(81.4f)
            ) *
            shakeAmplitude *
            strength;

        float rotation =
            ShakeNoise(113.9f) *
            maxShakeRotation *
            strength;

        transform.position =
            shakeBasePosition + offset;

        transform.rotation =
            Quaternion.Euler(
                rotation,
                rotation * 0.5f,
                0f
            ) *
            baseRotation;
    }

    private float ShakeNoise(float offset) => Mathf.PerlinNoise(shakePhase, offset) * 2f - 1f;

    private void ReportMenuSettledIfNeeded()
    {
        if (menuReported)
            return;

        bool isMenu = GameStateManager.Instance != null
            && GameStateManager.Instance.CurrentState == GameState.Menu;

        if (!isMenu)
            return;

        menuReported = true;

        OnReachedMenu?.Invoke();
    }

    private void UpdateTransition()
    {
        bool isMenu = GameStateManager.Instance != null
            && GameStateManager.Instance.CurrentState == GameState.Menu;

        Vector3 destination;
        Quaternion targetRotation;

        if (isMenu)
        {
            GetMenuPose(out destination, out targetRotation);
        }
        else
        {
            if (target == null)
                return;

            destination = GetGameplayPosition();
            targetRotation = GetGameplayLookRotation();
        }

        EnsureBaseInitialized();
        if (!transitionInitialized)
        {
            transitionInitialized = true;
            transitionElapsed = 0f;
            transitionOrigin = basePosition;
            transitionOriginRotation = baseRotation;
            transitionOriginParallax = smoothedParallax;
            transitionDuration = GetTransitionDuration(destination, targetRotation);
            EnsureViewCamera();
            transitionOriginFov = viewCamera != null ? viewCamera.fieldOfView : 60f;
        }

        // Scene activation can produce a very long first frame. Do not let a
        // single long delta consume the whole animation and teleport the camera.
        float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
        transitionElapsed = Mathf.Min(transitionElapsed + dt, transitionDuration);
        float t = Mathf.Clamp01(transitionElapsed / transitionDuration);
        // Match the old fast initial response, then ease into the exact final
        // pose. Unlike the old distance cutoff, this has no final position snap.
        float remaining = 1f - t;
        float ease = 1f - remaining * remaining * remaining * remaining;
        if (viewCamera != null && !viewCamera.orthographic)
            viewCamera.fieldOfView = Mathf.Lerp(transitionOriginFov, TargetFieldOfView(isMenu), ease);
        basePosition = Vector3.Lerp(transitionOrigin, destination, ease);
        baseRotation = Quaternion.Slerp(transitionOriginRotation, targetRotation, ease);

        // Parallax is blended once per frame (also on the final frame), then
        // handed off continuously to normal following rather than being reset.
        if (isMenu)
            smoothedParallax = Vector3.Lerp(transitionOriginParallax, Vector3.zero, ease);
        else
        {
            float blend = 1f - Mathf.Exp(-parallaxSmoothSpeed * dt);
            smoothedParallax = Vector3.Lerp(smoothedParallax, GetMouseParallaxOffset(), blend);
        }
        transform.SetPositionAndRotation(basePosition + smoothedParallax, baseRotation);
        shakeBasePosition = transform.position;

        if (t < 1f) return;
        transitioning = false;
        transitionInitialized = false;
        followTarget = !isMenu;
        if (isMenu && !menuReported)
        {
            menuReported = true;
            OnReachedMenu?.Invoke();
        }
    }

    private float GetTransitionDuration(Vector3 destination, Quaternion rotation)
    {
        float distance = Vector3.Distance(basePosition, destination);
        float angle = Quaternion.Angle(baseRotation, rotation);
        float speed = Mathf.Max(.1f, transitionSpeed);
        float duration = Mathf.Log(1f + Mathf.Max(distance, angle * .12f) / .1f) / speed;
        return Mathf.Clamp(duration, .65f, 2.5f);
    }

    public void MoveToMenuPositionImmediate()
    {
        EnsureViewCamera();
        GetMenuPose(out Vector3 position, out Quaternion rotation);
        transform.SetPositionAndRotation(position, rotation);
        if(viewCamera != null && !viewCamera.orthographic) viewCamera.fieldOfView = TargetFieldOfView(true);

        followTarget = false;
        transitioning = false;
        transitionInitialized = false;
        trauma = 0f;
        fireTrauma = 0f;

        smoothedParallax = Vector3.zero;
        shakeBasePosition = transform.position;
        basePosition = transform.position;
        baseRotation = transform.rotation;
        baseInitialized = true;
    }

    // Station changes are camera-only: do not trigger the gameplay/menu arrival event.
    public void MoveToCampView()
    {
        if (GameStateManager.Instance == null ||
            GameStateManager.Instance.CurrentState != GameState.Menu) return;
        followTarget = false;
        transitioning = true;
        transitionInitialized = false;
    }

    private void GetMenuPose(out Vector3 position, out Quaternion rotation)
    {
        if (ArcadeMenuSkin.CampMenuLayoutEnabled && target != null)
        {
            ArcadeMenuSkin.GetHubCameraPose(target, out position, out rotation);
            return;
        }

        position = menuPosition;
        rotation = Quaternion.LookRotation(menuLookAt - menuPosition);
    }

    public void MoveToGameplayPositionImmediate()
    {
        EnsureViewCamera();
        if(viewCamera != null && !viewCamera.orthographic) viewCamera.fieldOfView = gameplayFieldOfView;
        if (target != null)
        {
            transform.position = target.position + offset;

            Vector3 lookDir = target.position - transform.position;
            transform.rotation = Quaternion.LookRotation(lookDir);
        }

        followTarget = true;
        transitioning = false;
        transitionInitialized = false;

        smoothedParallax = Vector3.zero;
        shakeBasePosition = transform.position;
        basePosition = transform.position;
        baseRotation = transform.rotation;
        baseInitialized = true;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void EnsureViewCamera()
    {
        if(viewCamera != null) return;
        viewCamera = GetComponent<Camera>();
        if(viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
        if(viewCamera != null) gameplayFieldOfView = viewCamera.fieldOfView;
    }

    private float TargetFieldOfView(bool menu)
    {
        return menu && ArcadeMenuSkin.CampMenuLayoutEnabled
            ? ArcadeMenuSkin.GetHubFieldOfView()
            : gameplayFieldOfView;
    }

    public float EstimateMenuTransitionTime()
    {
        if (transitionSpeed <= 0f)
            return 0f;

        GetMenuPose(out Vector3 destination, out _);
        float distance = Vector3.Distance(transform.position, destination);

        const float threshold = 0.1f;

        if (distance <= threshold)
            return 0f;

        return
            Mathf.Log(distance / threshold) /
            transitionSpeed;
    }
}
