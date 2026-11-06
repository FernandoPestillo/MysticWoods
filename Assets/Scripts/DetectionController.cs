using System.Collections.Generic;
using UnityEngine;

public class DetectionController : MonoBehaviour
{
    public string _tagTargetDetection = "Player";
    public GameObject parentGameObject;
    public List<Collider2D> detectedObjs = new List<Collider2D>();

    public Collider2D Target
    {
        get
        {
            detectedObjs.RemoveAll(collider => collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy);
            foreach (var collider in detectedObjs)
            {
                var damageable = collider.GetComponentInParent<IDamageable>();
                if (damageable == null || damageable.Targetable) return collider;
            }
            return null;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Sword triggers must not become pursuit targets or duplicate the player.
        if (!collision.isTrigger && collision.CompareTag(_tagTargetDetection) && !detectedObjs.Contains(collision))
            detectedObjs.Add(collision);
    }

    private void OnTriggerExit2D(Collider2D collision) => detectedObjs.Remove(collision);
    private void OnDisable() => detectedObjs.Clear();
}
