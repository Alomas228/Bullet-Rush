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

    private float trauma;
    private Vector3 basePosition;
    private Quaternion baseRotation;
    private bool baseInitialized;

    // База тряски с учётом параллакса мыши.
    // Без неё тряска возвращала камеру к центру, игнорируя сдвиг за курсором,
    // и камера «дёргалась» между параллаксом и базой.
    private Vector3 shakeBasePosition;

    private Vector3 smoothedParallax;
    private float freezeTimer;

    public void AddShake(float amount)
    {
        if (SettingsManager.Instance != null &&
            !SettingsManager.Instance.ScreenShakeEnabled)
            return;

        trauma = Mathf.Min(trauma + amount, 1f);
    }

    public void AddFireShake(float amount)
    {
        if (amount <= 0f)
            return;

        if (SettingsManager.Instance != null &&
            !SettingsManager.Instance.FireShakeEnabled)
            return;

        trauma = Mathf.Min(trauma + amount, 1f);
    }

    private void Start()
    {
        GameStateManager instance = GameStateManager.Instance;

        bool isPlaying =
            instance != null &&
            instance.CurrentState == GameState.Playing;

        if (isPlaying)
        {
            MoveToGameplayPositionImmediate();
            SavedPose = default;
        }
        else
        {
            if (SavedPose.hasPose)
            {
                transform.position = SavedPose.position;
                transform.rotation = SavedPose.rotation;
                transitioning = true;
            }
            else
            {
                MoveToMenuPositionImmediate();
            }
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
                menuReported = false;
                break;

            case GameState.Playing:
                transitioning = true;
                menuReported = false;
                break;

            case GameState.GameOver:
                break;
        }
    }

    private void LateUpdate()
    {
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

        Vector3 targetPos =
            target.position + offset;

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

    private Vector3 GetMouseParallaxOffset()
    {
        if (Mouse.current == null)
            return Vector3.zero;

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

        return new Vector3(
            normalizedX,
            normalizedY * 0.5f,
            0f
        ) * mouseParallaxStrength;
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
        if (trauma <= 0f)
            return;

        trauma = Mathf.Max(
            0f,
            trauma - shakeDecay * Time.unscaledDeltaTime
        );

        float strength = trauma * trauma;

        Vector3 offset =
            new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f)
            ) *
            shakeAmplitude *
            strength;

        float rotation =
            Random.Range(-1f, 1f) *
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
            destination = menuPosition;
            targetRotation = Quaternion.LookRotation(
                menuLookAt - menuPosition
            );
        }
        else
        {
            if (target == null)
                return;

            destination = target.position + offset;
            Vector3 lookDir = target.position - destination;
            targetRotation = Quaternion.LookRotation(lookDir);
        }

        EnsureBaseInitialized();

        basePosition = Vector3.Lerp(
            basePosition,
            destination,
            transitionSpeed * Time.unscaledDeltaTime
        );

        baseRotation = Quaternion.Slerp(
            baseRotation,
            targetRotation,
            transitionSpeed * Time.unscaledDeltaTime
        );

        transform.position = basePosition;
        transform.rotation = baseRotation;

        if (!isMenu)
            ApplyMouseParallaxToPosition();

        shakeBasePosition = transform.position;

        float distance = Vector3.Distance(
            basePosition,
            destination
        );

        if (distance < 0.1f)
        {
            basePosition = destination;
            baseRotation = targetRotation;
            transitioning = false;

            if (isMenu)
            {
                transform.position = basePosition;
                transform.rotation = baseRotation;
                menuReported = true;
                OnReachedMenu?.Invoke();
            }
            else
            {
                transform.position = basePosition;
                transform.rotation = baseRotation;
                ApplyMouseParallaxToPosition();
                followTarget = true;
            }

            shakeBasePosition = transform.position;
        }
    }

    public void MoveToMenuPositionImmediate()
    {
        transform.position = menuPosition;

        transform.rotation = Quaternion.LookRotation(
            menuLookAt - menuPosition
        );

        followTarget = false;
        transitioning = false;

        smoothedParallax = Vector3.zero;
        shakeBasePosition = transform.position;
    }

    public void MoveToGameplayPositionImmediate()
    {
        if (target != null)
        {
            transform.position = target.position + offset;

            Vector3 lookDir = target.position - transform.position;
            transform.rotation = Quaternion.LookRotation(lookDir);
        }

        followTarget = true;
        transitioning = false;

        smoothedParallax = Vector3.zero;
        shakeBasePosition = transform.position;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public float EstimateMenuTransitionTime()
    {
        if (transitionSpeed <= 0f)
            return 0f;

        float distance = Vector3.Distance(
            transform.position,
            menuPosition
        );

        const float threshold = 0.1f;

        if (distance <= threshold)
            return 0f;

        return
            Mathf.Log(distance / threshold) /
            transitionSpeed;
    }
}
