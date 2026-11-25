using UnityEngine;

namespace MysticWoods.Gameplay.Enemies
{
    /// <summary>
    /// Ground warning shapes drawn under characters: a sector (melee), a lane (lunge) or a ring
    /// (slam). Amber while preparing, red while dangerous. Pure presentation, no colliders.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyTelegraph : MonoBehaviour
    {
        private const int Segments = 32;
        private static readonly Color Warning = new Color(1f, .7f, .16f);
        private static readonly Color Danger = new Color(1f, .22f, .1f);
        [SerializeField] private Material material;
        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private Transform root;
        private readonly Vector3[] vertices = new Vector3[2 + (Segments + 1) * 3];
        private readonly Color[] colors = new Color[2 + (Segments + 1) * 3];
        private int[] sectorTriangles, laneTriangles, ringTriangles;

        public bool Visible => meshRenderer != null && meshRenderer.enabled;

        public void Initialise(Material telegraphMaterial, int sortingLayer, int sortingOrder)
        {
            material = telegraphMaterial;
            if (root != null) return;
            root = new GameObject("Attack telegraph").transform;
            root.SetParent(transform, false);
            meshRenderer = root.gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.sortingLayerID = sortingLayer;
            meshRenderer.sortingOrder = sortingOrder;
            meshRenderer.enabled = false;
            mesh = new Mesh { name = "Enemy telegraph" };
            mesh.MarkDynamic();
            root.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            mesh.vertices = vertices;
            BuildTriangles();
        }

        private void BuildTriangles()
        {
            // Sector / ring: centre fan (vertex 0 + rim 1..S+1) and a thick border strip.
            int border = Segments + 2;
            sectorTriangles = new int[Segments * 9];
            for (int i = 0; i < Segments; i++)
            {
                int t = i * 9, a = border + i * 2;
                sectorTriangles[t] = 0; sectorTriangles[t + 1] = i + 1; sectorTriangles[t + 2] = i + 2;
                sectorTriangles[t + 3] = a; sectorTriangles[t + 4] = a + 1; sectorTriangles[t + 5] = a + 2;
                sectorTriangles[t + 6] = a + 1; sectorTriangles[t + 7] = a + 3; sectorTriangles[t + 8] = a + 2;
            }
            ringTriangles = sectorTriangles;
            // Lane: two quads (fill + leading edge) using the first 8 vertices.
            laneTriangles = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 };
        }

        public void Hide() { if (meshRenderer != null) meshRenderer.enabled = false; }

        /// <param name="progress">0..1 preparation progress; the fill grows with it.</param>
        public void Sector(Vector2 origin, Vector2 direction, float radius, float arc, float progress, bool active)
        {
            if (mesh == null) return;
            Color tint = active ? Danger : Warning;
            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            arc = Mathf.Min(arc, 360f);
            vertices[0] = Local(origin);
            colors[0] = Alpha(tint, .05f);
            float fill = active ? radius : Mathf.Lerp(radius * .35f, radius, progress);
            for (int i = 0; i <= Segments; i++)
            {
                float angle = (baseAngle - arc * .5f + arc * i / Segments) * Mathf.Deg2Rad;
                var ray = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                int b = Segments + 2 + i * 2;
                vertices[i + 1] = Local(origin + ray * fill);
                colors[i + 1] = Alpha(tint, .16f + progress * .22f + (active ? .15f : 0f));
                vertices[b] = Local(origin + ray * radius);
                vertices[b + 1] = Local(origin + ray * (radius - .045f));
                colors[b] = colors[b + 1] = Alpha(tint, .7f + progress * .3f);
            }
            Show(sectorTriangles);
        }

        public void Ring(Vector2 center, float radius, float progress, bool active) =>
            Sector(center, Vector2.right, radius, 360f, progress, active);

        public void Lane(Vector2 origin, Vector2 direction, float length, float width, float progress, bool active)
        {
            if (mesh == null) return;
            Color tint = active ? Danger : Warning;
            Vector2 side = new Vector2(-direction.y, direction.x) * (width * .5f);
            float fill = Mathf.Lerp(.2f, 1f, active ? 1f : progress) * length;
            Vector2 end = origin + direction * fill;
            Vector2 tip = origin + direction * length;
            vertices[0] = Local(origin - side); vertices[1] = Local(origin + side);
            vertices[2] = Local(end + side); vertices[3] = Local(end - side);
            vertices[4] = Local(tip - side); vertices[5] = Local(tip + side);
            vertices[6] = Local(tip + side - direction * .06f); vertices[7] = Local(tip - side - direction * .06f);
            for (int i = 0; i < 4; i++) colors[i] = Alpha(tint, .14f + progress * .2f);
            for (int i = 4; i < 8; i++) colors[i] = Alpha(tint, .85f);
            Show(laneTriangles);
        }

        private void Show(int[] triangles)
        {
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            meshRenderer.enabled = true;
        }

        private Vector3 Local(Vector2 world) => root.InverseTransformPoint(world);
        private static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, Mathf.Clamp01(a));

        private void OnDisable() => Hide();
        private void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
