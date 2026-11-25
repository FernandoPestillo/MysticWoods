using UnityEngine;

namespace MysticWoods.Gameplay.Combat
{
    /// <summary>Fading silhouettes left behind during dodges and dashes. Pooled per owner.</summary>
    [DisallowMultipleComponent]
    public sealed class AfterImage : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer source;
        [SerializeField] private Color tint = new Color(.55f, .85f, 1f, .55f);
        [SerializeField, Min(.02f)] private float interval = .045f;
        [SerializeField, Min(.05f)] private float lifetime = .22f;
        [SerializeField, Range(1, 16)] private int poolSize = 8;
        [SerializeField] private Material ghostMaterial;
        private SpriteRenderer[] ghosts;
        private float[] born;
        private int next;
        private float emitUntil;
        private float nextAt;

        public bool Emitting => Time.time < emitUntil;

        public void Bind(SpriteRenderer renderer, Material material)
        {
            source = renderer;
            ghostMaterial = material;
        }

        public void Emit(float duration)
        {
            emitUntil = Mathf.Max(emitUntil, Time.time + duration);
            nextAt = Mathf.Min(nextAt, Time.time);
        }

        private void EnsurePool()
        {
            if (ghosts != null) return;
            ghosts = new SpriteRenderer[poolSize];
            born = new float[poolSize];
            var root = new GameObject("After images").transform;
            root.SetParent(null, false);
            root.gameObject.hideFlags = HideFlags.DontSave;
            for (int i = 0; i < poolSize; i++)
            {
                var ghost = new GameObject("Ghost").AddComponent<SpriteRenderer>();
                ghost.transform.SetParent(root, false);
                if (ghostMaterial != null) ghost.sharedMaterial = ghostMaterial;
                ghost.enabled = false;
                ghosts[i] = ghost;
                born[i] = -10f;
            }
        }

        private void LateUpdate()
        {
            if (source == null) return;
            if (Emitting && Time.time >= nextAt && source.sprite != null)
            {
                EnsurePool();
                nextAt = Time.time + interval;
                var ghost = ghosts[next];
                born[next] = Time.time;
                next = (next + 1) % ghosts.Length;
                ghost.sprite = source.sprite;
                ghost.flipX = source.flipX;
                ghost.sortingLayerID = source.sortingLayerID;
                ghost.sortingOrder = source.sortingOrder - 1;
                ghost.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                ghost.transform.localScale = source.transform.lossyScale;
                ghost.enabled = true;
            }
            if (ghosts == null) return;
            for (int i = 0; i < ghosts.Length; i++)
            {
                if (!ghosts[i].enabled) continue;
                float age = (Time.time - born[i]) / lifetime;
                if (age >= 1f) { ghosts[i].enabled = false; continue; }
                var color = tint;
                color.a *= 1f - age;
                ghosts[i].color = color;
            }
        }

        private void OnDestroy()
        {
            if (ghosts == null || ghosts.Length == 0 || ghosts[0] == null) return;
            Destroy(ghosts[0].transform.parent.gameObject);
        }
    }
}
