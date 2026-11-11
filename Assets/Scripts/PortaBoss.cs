using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class PortaBoss : MonoBehaviour
{
    public KeysManager KeysManager;
    [Header("Opening")]
    [SerializeField] private Transform leftDoor;
    [SerializeField] private Transform rightDoor;
    [SerializeField, Min(.01f)] private float openingDuration = .75f;
    [SerializeField, Min(.01f)] private float closingDuration = .5f;
    [Header("Visibility near the gate")]
    [SerializeField] private SpriteRenderer gateFrame;
    [SerializeField, Range(0f, 1f)] private float insideAlpha = .25f;
    [SerializeField, Min(.01f)] private float fadeDuration = .25f;
    [SerializeField] private Vector2 insideDirection = Vector2.up;
    [SerializeField] private float passageOffset = -.6f;
    [SerializeField] private Vector2 occlusionPadding = new Vector2(.35f, .6f);
    // The seal keeps its collider; this controller supplies its visible closing animation.
    [SerializeField] private SpriteRenderer[] arenaSealVisuals = new SpriteRenderer[0];
    private BoxCollider2D barrier;
    private SpriteRenderer[] visuals;
    private Color[] originalColors;
    private bool[] sealWasEnabled;
    private Transform player;
    private bool unlocked;
    private float opening;
    private float opacity = 1f;
    private Vector3 leftScale;
    private Vector3 rightScale;

    private void Awake()
    {
        barrier = GetComponent<BoxCollider2D>();
        visuals = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[visuals.Length];
        for (int i = 0; i < visuals.Length; i++) originalColors[i] = visuals[i].color;
        sealWasEnabled = new bool[arenaSealVisuals.Length];
        for (int i = 0; i < arenaSealVisuals.Length; i++)
            if (arenaSealVisuals[i] != null) sealWasEnabled[i] = arenaSealVisuals[i].enabled;
        if (leftDoor != null) leftScale = leftDoor.localScale;
        if (rightDoor != null) rightScale = rightDoor.localScale;
        var controller = FindAnyObjectByType<PlayerController>();
        if (controller != null) player = controller.transform;
        if (KeysManager == null) KeysManager = FindAnyObjectByType<KeysManager>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        var controller = collision.collider.GetComponentInParent<PlayerController>();
        if (controller == null || unlocked || Time.timeScale <= 0f) return;
        player = controller.transform;
        if (KeysManager == null) return;
        if (KeysManager.chavesOuro <= 0)
        {
            KeysManager.AvisarPortaoTrancado();
            return;
        }
        KeysManager.UsarChaveOuro();
        unlocked = true;
    }

    private void LateUpdate()
    {
        bool sealedArena = false;
        foreach (var seal in arenaSealVisuals)
        {
            if (seal == null) continue;
            sealedArena |= seal.gameObject.activeInHierarchy;
            // Avoid drawing a second, opaque gate on top of the animated one.
            seal.enabled = false;
        }
        bool shouldOpen = unlocked && !sealedArena;
        float duration = shouldOpen ? openingDuration : closingDuration;
        opening = Mathf.MoveTowards(opening, shouldOpen ? 1f : 0f, Time.deltaTime / Mathf.Max(.01f, duration));
        float width = Mathf.Lerp(1f, .02f, Mathf.SmoothStep(0f, 1f, opening));
        PoseDoor(leftDoor, leftScale, width);
        PoseDoor(rightDoor, rightScale, width);
        // Unlock once; the separate arena seal still blocks retreat during the boss fight.
        if (unlocked && opening >= 1f) barrier.enabled = false;

        bool occludingPlayer = false;
        if (unlocked && player != null && gateFrame != null)
        {
            Bounds area = gateFrame.bounds;
            area.Expand(new Vector3(Mathf.Max(0, occlusionPadding.x) * 2f, Mathf.Max(0, occlusionPadding.y) * 2f, 0));
            Vector3 position = player.position;
            occludingPlayer = position.x >= area.min.x && position.x <= area.max.x &&
                position.y >= area.min.y && position.y <= area.max.y &&
                Vector2.Dot((Vector2)(position - transform.position), insideDirection.normalized) >= passageOffset;
        }
        opacity = Mathf.MoveTowards(opacity, occludingPlayer ? insideAlpha : 1f, Time.deltaTime / Mathf.Max(.01f, fadeDuration));
        for (int i = 0; i < visuals.Length; i++)
        {
            if (visuals[i] == null) continue;
            Color color = originalColors[i];
            color.a *= opacity;
            visuals[i].color = color;
        }
    }

    private void PoseDoor(Transform door, Vector3 scale, float width)
    {
        if (door == null) return;
        door.localScale = new Vector3(scale.x * width, scale.y, scale.z);
        var renderer = door.GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.enabled = opening < 1f;
    }

    private void OnDisable()
    {
        opacity = 1f;
        if (visuals == null) return;
        for (int i = 0; i < visuals.Length; i++)
            if (visuals[i] != null) visuals[i].color = originalColors[i];
        for (int i = 0; i < arenaSealVisuals.Length; i++)
            if (arenaSealVisuals[i] != null) arenaSealVisuals[i].enabled = sealWasEnabled[i];
    }
}
