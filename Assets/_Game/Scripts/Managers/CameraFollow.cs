using UnityEngine;

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

    private bool followTarget;
    private bool transitioning;
    private bool menuReported;

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
            return;
        }

        if (followTarget && target != null)
        {
            Vector3 targetPos = target.position + offset;
            transform.position = Vector3.Lerp(
                transform.position,
                targetPos,
                followSpeed * Time.deltaTime
            );
            return;
        }

        ReportMenuSettledIfNeeded();
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

        transform.position = Vector3.Lerp(
            transform.position,
            destination,
            transitionSpeed * Time.unscaledDeltaTime
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            transitionSpeed * Time.unscaledDeltaTime
        );

        float distance = Vector3.Distance(
            transform.position,
            destination
        );

        if (distance < 0.1f)
        {
            transform.position = destination;
            transform.rotation = targetRotation;
            transitioning = false;

            if (isMenu)
            {
                menuReported = true;
                OnReachedMenu?.Invoke();
            }
            else
            {
                followTarget = true;
            }
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
