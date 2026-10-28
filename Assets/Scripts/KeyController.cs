using UnityEngine;

public class KeyController : MonoBehaviour
{
    public enum KeyType { Silver, Gold }
    public KeysManager KeysManager;
    [SerializeField] private KeyType keyType = KeyType.Silver;
    private bool collected;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collected || !collision.gameObject.CompareTag("Player")) return;
        collected = true;
        if (keyType == KeyType.Gold) KeysManager.EncontrarChaveOuro();
        else KeysManager.EncontrarChavePrata();
        Destroy(gameObject);
    }
}
