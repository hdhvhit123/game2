using UnityEngine;

/// <summary>
/// Hiển thị sợi dây trói giữa 2 người chơi. Lực kéo thật nằm ở SemiActiveRagdollController (tether);
/// script này chỉ vẽ.
/// Sửa so với bản cũ:
///  - Bước thời gian cố định (dây không còn giật/nổ khi FPS tụt).
///  - Va chạm: 1 Raycast/node/bước thay vì 12 lần SphereCast/node/frame (~264 SphereCast mỗi frame).
///  - Khởi tạo dây giữa 2 đầu, tự gắn vào 2 người chơi khi họ được spawn.
///  - Dây đổi màu và mảnh lại khi bị kéo căng.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class VerletRopeRenderer : MonoBehaviour
{
    public Transform anchorA;
    public Transform anchorB;
    [SerializeField] private bool autoBindPlayers = true;
    [SerializeField] private Vector3 playerAnchorOffset = new Vector3(0f, 0.9f, 0f);

    [Header("Simulation")]
    [SerializeField] private int segmentCount = 24;
    [SerializeField, Tooltip("Dài hơn tetherLength của người chơi một chút để dây võng tự nhiên")]
    private float totalRopeLength = 4.5f;
    [SerializeField] private int solverIterations = 10;
    [SerializeField] private Vector3 gravity = new Vector3(0f, -19.6f, 0f);
    [SerializeField, Range(0.9f, 1f)] private float damping = 0.985f;
    [SerializeField] private float fixedStep = 1f / 60f;
    [SerializeField] private int maxStepsPerFrame = 3;

    [Header("Collision")]
    [SerializeField] private LayerMask collisionMask;
    [SerializeField] private float ropeRadius = 0.06f;

    [Header("Look")]
    [SerializeField] private Color slackColor = new Color(0.87f, 0.69f, 0.42f);
    [SerializeField] private Color tautColor = new Color(1f, 0.25f, 0.18f);
    [SerializeField] private float slackWidth = 0.09f;
    [SerializeField] private float tautWidth = 0.055f;

    private RopeSolver _solver;
    private LineRenderer _line;
    private float _accumulator;
    private bool _initialized;
    private Transform _boundA, _boundB;

    private void Awake()
    {
        _line = GetComponent<LineRenderer>();
        _solver = new RopeSolver(segmentCount, totalRopeLength);
        _line.positionCount = _solver.NodeCount;
        _line.useWorldSpace = true;
        _line.numCapVertices = Mathf.Max(_line.numCapVertices, 4);
        _line.numCornerVertices = Mathf.Max(_line.numCornerVertices, 2);
        _line.enabled = false;
    }

    private void LateUpdate()
    {
        TryAutoBind();
        if (anchorA == null || anchorB == null)
        {
            _line.enabled = false;
            _initialized = false;
            return;
        }

        Vector3 a = anchorA.position, b = anchorB.position;

        // Lần đầu, hoặc khi người chơi bị dịch chuyển (respawn) thì đặt lại dây.
        if (!_initialized || (_solver.Positions[0] - a).sqrMagnitude > 9f || (_solver.Positions[_solver.NodeCount - 1] - b).sqrMagnitude > 9f)
        {
            _solver.Reset(a, b);
            _accumulator = 0f;
            _initialized = true;
        }

        _accumulator = Mathf.Min(_accumulator + Time.deltaTime, fixedStep * maxStepsPerFrame);
        while (_accumulator >= fixedStep)
        {
            _accumulator -= fixedStep;
            _solver.Integrate(fixedStep, gravity, damping);
            _solver.SolveConstraints(a, b, solverIterations);
            ResolveGroundCollision();
        }

        _line.enabled = true;
        _line.SetPositions(_solver.Positions);

        float t = Mathf.InverseLerp(0.8f, 1f, RopeSolver.Tension(a, b, totalRopeLength));
        Color c = Color.Lerp(slackColor, tautColor, t);
        _line.startColor = c;
        _line.endColor = c;
        _line.widthMultiplier = Mathf.Lerp(slackWidth, tautWidth, t);
    }

    private void ResolveGroundCollision()
    {
        if (collisionMask.value == 0) return;
        Vector3[] p = _solver.Positions;
        for (int i = 1; i < _solver.NodeCount - 1; i++)
        {
            if (Physics.Raycast(p[i] + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 0.5f + ropeRadius,
                                collisionMask, QueryTriggerInteraction.Ignore))
            {
                _solver.PushAbove(i, hit.point.y, ropeRadius);
            }
        }
    }

    private void TryAutoBind()
    {
        if (!autoBindPlayers) return;
        var players = SemiActiveRagdollController.All;
        if (players.Count < 2)
        {
            if (_boundA != null || _boundB != null) { anchorA = anchorB = null; _boundA = _boundB = null; }
            return;
        }

        Transform pa = players[0] != null ? players[0].transform : null;
        Transform pb = players[1] != null ? players[1].transform : null;
        if (pa == _boundA && pb == _boundB) return;

        _boundA = pa;
        _boundB = pb;
        anchorA = GetOrCreateAnchor(pa);
        anchorB = GetOrCreateAnchor(pb);
        _initialized = false;
    }

    private Transform GetOrCreateAnchor(Transform player)
    {
        if (player == null) return null;
        Transform existing = player.Find("RopeAnchor");
        if (existing != null) return existing;
        var go = new GameObject("RopeAnchor");
        go.transform.SetParent(player, false);
        go.transform.localPosition = playerAnchorOffset;
        return go.transform;
    }
}
