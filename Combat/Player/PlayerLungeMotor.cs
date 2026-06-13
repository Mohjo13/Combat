using System.Collections;
using UnityEngine;

public class PlayerLungeMotor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterController controller;
    [SerializeField] private Transform cameraObject;

    [Header("Attack Lunge")]
    [SerializeField] private float lungeDistance = 1.25f;
    [SerializeField] private float heavyLungeMultiplier = 1.25f;
    [SerializeField] private float lungeDuration = 0.12f;
    [SerializeField] private float lungeInputDeadzone = 0.1f;
    [SerializeField] private bool rotateTowardLungeDirection = true;

    [Header("Gap Closer")]
    [SerializeField] private float gapCloserStopDistance = 0.5f;

    [Header("Follow Through")]
    [SerializeField] private float followThroughStopDistance = 0.6f;
    [SerializeField] private float followThroughSpeedMultiplier = 3f;

    private Vector2 latestMoveInput;
    private Vector3 latestMoveWorldDirection;

    private Coroutine attackLungeRoutine;
    private Coroutine gapCloseRoutine;
    private Coroutine followThroughRoutine;

    private AttackTypeHandler attackTypeHandler;

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        if (cameraObject == null && Camera.main != null)
            cameraObject = Camera.main.transform;

        attackTypeHandler = GetComponent<AttackTypeHandler>();
    }

    private void OnEnable()
    {
        CombatEvents.OnPlayerAttacked += HandlePlayerAttacked;
        CombatEvents.OnGapCloserHit += HandleGapCloserHit;
        CombatEvents.OnEnemyKnockedBack += HandleEnemyKnockedBack;
    }

    private void OnDisable()
    {
        CombatEvents.OnPlayerAttacked -= HandlePlayerAttacked;
        CombatEvents.OnGapCloserHit -= HandleGapCloserHit;
        CombatEvents.OnEnemyKnockedBack -= HandleEnemyKnockedBack;
    }

    public void CacheMoveInputDirection(Vector2 directions)
    {
        latestMoveInput = directions;

        if (cameraObject == null)
            return;

        Vector3 camForward = cameraObject.forward;
        camForward.y = 0f;
        camForward.Normalize();

        Vector3 camRight = cameraObject.right;
        camRight.y = 0f;
        camRight.Normalize();

        Vector3 moveDirection = camForward * directions.y + camRight * directions.x;
        moveDirection.y = 0f;

        if (moveDirection.sqrMagnitude > 0.01f)
        {
            latestMoveWorldDirection = moveDirection.normalized;
        }
    }

    private void HandlePlayerAttacked(AttackType type)
    {
        StopAllLunges();

        Vector3 lungeDirection = GetAttackLungeDirection();

        if (lungeDirection.sqrMagnitude < 0.001f)
            return;

        if (rotateTowardLungeDirection)
            RotateTowardWorldDirection(lungeDirection);

        CombatEvents.RaisePlayerLungeStarted();

        float finalDistance = lungeDistance;

        AttackData data = attackTypeHandler != null ? attackTypeHandler.CurrentAttackData : null;

        if (data != null && data.useCustomLunge)
            finalDistance *= data.customLungeMultiplier;
        else if (type == AttackType.Heavy)
            finalDistance *= heavyLungeMultiplier;

        attackLungeRoutine = StartCoroutine(
            DirectionalLungeRoutine(lungeDirection, finalDistance, lungeDuration)
        );
    }

    private Vector3 GetAttackLungeDirection()
    {
        bool hasInput = latestMoveInput.sqrMagnitude > lungeInputDeadzone * lungeInputDeadzone
                        && latestMoveWorldDirection.sqrMagnitude > 0.001f;

        // Prefer an explicit lock-on, fall back to the best target in view
        PlayerRotationTargetLock targetLock = PlayerRotationTargetLock.instance;
        Transform lockedTarget = (targetLock != null && targetLock.lockedOnTarget)
            ? targetLock.lockedTarget
            : targetLock?.GetBestTarget();

        if (lockedTarget != null)
        {
            Vector3 toEnemy = lockedTarget.position - transform.position;
            toEnemy.y = 0f;

            if (toEnemy.sqrMagnitude > 0.001f)
            {
                toEnemy.Normalize();

                // If the player has no input, or their input is within the snap threshold,
                // redirect toward the enemy. Otherwise honour their intended direction.
                if (!hasInput || Vector3.Angle(latestMoveWorldDirection, toEnemy) <= 22.5f)
                    return toEnemy;

                return latestMoveWorldDirection.normalized;
            }
        }

        // No locked target � fall back to input then transform forward
        if (hasInput)
            return latestMoveWorldDirection.normalized;

        Vector3 forward = transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude > 0.001f)
            return forward.normalized;

        return Vector3.zero;
    }

    private IEnumerator DirectionalLungeRoutine(Vector3 direction, float distance, float duration)
    {
        if (controller == null)
            yield break;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            yield break;

        direction.Normalize();

        float elapsed = 0f;

        while (elapsed < duration)
        {
            float currentT = Mathf.Clamp01(elapsed / duration);
            float nextT = Mathf.Clamp01((elapsed + Time.deltaTime) / duration);

            float currentEase = 1f - Mathf.Pow(1f - currentT, 2f);
            float nextEase = 1f - Mathf.Pow(1f - nextT, 2f);

            float deltaDistance = (nextEase - currentEase) * distance;

            controller.Move(direction * deltaDistance);

            elapsed += Time.deltaTime;
            yield return null;
        }

        attackLungeRoutine = null;
    }

    private void HandleGapCloserHit(GameObject enemy, Vector3 hitPosition)
    {
        if (enemy == null)
            return;

        AttackTypeHandler attackTypeHandler = GetComponentInChildren<AttackTypeHandler>();

        if (attackTypeHandler == null || attackTypeHandler.CurrentAttackData == null)
            return;

        AttackData data = attackTypeHandler.CurrentAttackData;

        if (gapCloseRoutine != null)
            StopCoroutine(gapCloseRoutine);

        gapCloseRoutine = StartCoroutine(
            GapCloseLungeRoutine(
                enemy.transform,
                data.gapCloserLungeDistance,
                data.gapCloserLungeDuration,
                data.gapCloserLungeDelay
            )
        );
    }

    private IEnumerator GapCloseLungeRoutine(
        Transform target,
        float maxLungeDistance,
        float duration,
        float delay
    )
    {
        if (target == null)
            yield break;

        EnemyAI enemy = target.GetComponent<EnemyAI>();

        if (enemy != null && enemy.CurrentState == AgentState.Dead)
            yield break;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (target == null)
            yield break;

        if (enemy != null && enemy.CurrentState == AgentState.Dead)
            yield break;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        if (toTarget.magnitude <= gapCloserStopDistance)
            yield break;

        RotateTowardWorldDirection(toTarget);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (target == null)
                yield break;

            if (enemy != null && enemy.CurrentState == AgentState.Dead)
                yield break;

            Vector3 currentToTarget = target.position - transform.position;
            currentToTarget.y = 0f;

            float distance = currentToTarget.magnitude;

            if (distance <= gapCloserStopDistance)
                yield break;

            Vector3 direction = currentToTarget.normalized;

            float remainingDistance = distance - gapCloserStopDistance;
            float currentMaxDistance = Mathf.Min(remainingDistance, maxLungeDistance);

            float t = Mathf.Clamp01(elapsed / duration);
            float easeT = 1f - Mathf.Pow(1f - t, 2f);

            float speed = currentMaxDistance / duration;
            speed *= 1f - easeT * 0.5f;

            controller.Move(direction * speed * Time.deltaTime);

            elapsed += Time.deltaTime;
            yield return null;
        }

        gapCloseRoutine = null;
    }

    private void HandleEnemyKnockedBack(Transform enemy, float duration, float force)
    {
        Transform lockedTarget = PlayerRotationTargetLock.instance.GetBestTarget();

        if (lockedTarget == null || lockedTarget != enemy)
            return;

        if (followThroughRoutine != null)
            StopCoroutine(followThroughRoutine);

        followThroughRoutine = StartCoroutine(
            FollowThroughRoutine(enemy, duration)
        );
    }

    private IEnumerator FollowThroughRoutine(Transform target, float duration)
    {
        if (target == null)
            yield break;

        EnemyAI enemy = target.GetComponent<EnemyAI>();

        float elapsed = 0f;
        Vector3 lastEnemyPosition = target.position;

        while (elapsed < duration)
        {
            if (target == null)
                yield break;

            if (enemy != null && enemy.CurrentState == AgentState.Dead)
                yield break;

            Vector3 enemyDelta = target.position - lastEnemyPosition;
            enemyDelta.y = 0f;

            lastEnemyPosition = target.position;

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;

            float distance = toTarget.magnitude;

            if (distance > followThroughStopDistance)
            {
                Vector3 gapClose = toTarget.normalized * (distance - followThroughStopDistance);
                Vector3 movement = enemyDelta + gapClose * followThroughSpeedMultiplier * Time.deltaTime;

                controller.Move(movement);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        followThroughRoutine = null;
    }

    private void RotateTowardWorldDirection(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        transform.rotation = Quaternion.LookRotation(direction.normalized);
    }

    public void StopAllLunges()
    {
        if (attackLungeRoutine != null)
        {
            StopCoroutine(attackLungeRoutine);
            attackLungeRoutine = null;
        }

        if (gapCloseRoutine != null)
        {
            StopCoroutine(gapCloseRoutine);
            gapCloseRoutine = null;
        }

        if (followThroughRoutine != null)
        {
            StopCoroutine(followThroughRoutine);
            followThroughRoutine = null;
        }
    }
}