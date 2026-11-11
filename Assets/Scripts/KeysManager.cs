using MysticWoods.UI;
using UnityEngine;

public class KeysManager : MonoBehaviour
{
    public int chavesPrata = 0;
    public int chavesOuro = 0;
    [SerializeField] private GameplayNotice notices;

    public void EncontrarChavePrata()
    {
        chavesPrata++;
        Notify("Chave de prata coletada!");
    }

    public void UsarChavePrata() => chavesPrata = Mathf.Max(0, chavesPrata - 1);

    public void EncontrarChaveOuro()
    {
        chavesOuro++;
        Notify("Chave dourada coletada! O portão pode ser aberto.");
    }

    public void UsarChaveOuro() => chavesOuro = Mathf.Max(0, chavesOuro - 1);
    public void AvisarPortaoTrancado() => Notify("Portão trancado. Encontre a chave dourada para entrar.");

    private void Notify(string message)
    {
        if (notices == null) notices = FindAnyObjectByType<GameplayNotice>();
        if (notices != null) notices.Show(message);
    }
}
