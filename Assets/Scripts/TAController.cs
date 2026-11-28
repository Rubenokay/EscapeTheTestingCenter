using UnityEngine;

public class TAController : MonoBehaviour
{
    [Header("Patrol")]
    public Transform[] patrolPoints;
    public float patrolSpeed = 2f;
    public float waypointWaitTime = 1.5f;
    public float waypointArrivalDistance = 0.35f;

    [Header("Detection")]
    public Transform player;
    public float detectionRadius = 8f;
    public float fieldOfView = 110f;
    public float eyeHeight = 1.5f;
    public float detectionTime = 1.25f;
    public float detectionLossTime = 1.5f;
    public float catchDistance = 1.35f;
    public float chaseSpeed = 3.5f;

    [Header("Visual")]
    public Transform visionIndicator;

    private int currentWaypoint;
    private float waitTimer;
    private float detectionProgress;
    private float lostTimer;
    private Vector3 lastKnownPlayerPosition;
    private bool isCaught;

    private void Start()
    {
        FindPlayer();

        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            Debug.LogWarning("TAController needs at least one patrol point.", this);
        }
    }

    private void Update()
    {
        if (isCaught)
            return;

        if (player == null)
        {
            FindPlayer();
            return;
        }

        bool playerVisible = CanDetectPlayer();

        if (playerVisible)
        {
            lastKnownPlayerPosition = player.position;
            lostTimer = 0f;
            detectionProgress += Time.deltaTime / detectionTime;

            if (detectionProgress >= 1f)
            {
                CatchPlayer();
                return;
            }

            ChasePlayer();
        }
        else
        {
            if (detectionProgress > 0f)
            {
                detectionProgress -= Time.deltaTime / detectionLossTime;
                detectionProgress = Mathf.Max(0f, detectionProgress);
            }

            if (lostTimer > 0f)
            {
                lostTimer -= Time.deltaTime;
                if (lostTimer <= 0f)
                    lostTimer = 0f;

                ChasePosition(lastKnownPlayerPosition);
            }
            else
            {
                Patrol();
            }
        }

        Vector3 horizontalPosition = new Vector3(player.position.x, transform.position.y, player.position.z);
        if (Vector3.Distance(transform.position, horizontalPosition) <= catchDistance)
        {
            CatchPlayer();
        }

        UpdateVisionIndicator();
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject == null)
            playerObject = GameObject.Find("Player Capsule(Temp)");

        if (playerObject != null)
            player = playerObject.transform;
    }

    private bool CanDetectPlayer()
    {
        Vector3 taPosition = transform.position + Vector3.up * eyeHeight;
        Vector3 toPlayer = player.position - taPosition;
        float distance = toPlayer.magnitude;

        if (distance > detectionRadius)
            return false;

        float angle = Vector3.Angle(transform.forward, toPlayer);
        if (angle > fieldOfView * 0.5f)
            return false;

        if (Physics.Raycast(taPosition, toPlayer.normalized, out RaycastHit hit, detectionRadius))
        {
            if (hit.transform != player && !hit.transform.IsChildOf(player))
                return false;
        }

        return true;
    }

    private void Patrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
            return;

        Transform target = patrolPoints[currentWaypoint];
        if (target == null)
        {
            currentWaypoint = (currentWaypoint + 1) % patrolPoints.Length;
            return;
        }

        Vector3 targetPosition = new Vector3(target.position.x, transform.position.y, target.position.z);
        if (MoveTowards(targetPosition, patrolSpeed))
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= waypointWaitTime)
            {
                waitTimer = 0f;
                currentWaypoint = (currentWaypoint + 1) % patrolPoints.Length;
            }
        }
        else
        {
            waitTimer = 0f;
        }
    }

    private void ChasePlayer()
    {
        lostTimer = detectionLossTime;
        ChasePosition(player.position);
    }

    private void ChasePosition(Vector3 position)
    {
        Vector3 targetPosition = new Vector3(position.x, transform.position.y, position.z);
        MoveTowards(targetPosition, chaseSpeed);
    }

    private bool MoveTowards(Vector3 targetPosition, float speed)
    {
        Vector3 direction = targetPosition - transform.position;
        float distance = direction.magnitude;

        if (distance <= waypointArrivalDistance)
            return true;

        direction.Normalize();
        transform.position += direction * speed * Time.deltaTime;

        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(direction),
                360f * Time.deltaTime);
        }

        return false;
    }

    private void CatchPlayer()
    {
        if (isCaught)
            return;

        isCaught = true;
        detectionProgress = 1f;

        GameManager gameManager = FindAnyObjectByType<GameManager>();
        if (gameManager != null)
            gameManager.GameOver();
    }

    private void UpdateVisionIndicator()
    {
        if (visionIndicator == null)
            return;

        float normalizedProgress = Mathf.Clamp01(detectionProgress);
        visionIndicator.localScale = new Vector3(
            1f,
            Mathf.Lerp(0.05f, 1f, normalizedProgress),
            1f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, catchDistance);

        if (patrolPoints == null)
            return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            if (patrolPoints[i] == null)
                continue;

            Gizmos.DrawSphere(patrolPoints[i].position, 0.18f);

            if (patrolPoints.Length > 1)
            {
                Transform next = patrolPoints[(i + 1) % patrolPoints.Length];
                if (next != null)
                    Gizmos.DrawLine(patrolPoints[i].position, next.position);
            }
        }
    }
}
