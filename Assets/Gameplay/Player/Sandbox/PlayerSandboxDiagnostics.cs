using UnityEngine;

namespace MysticWoods.Gameplay.Player.Sandbox
{
    public sealed class PlayerSandboxDiagnostics : MonoBehaviour
    {
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private PlayerAnimationController presentation;
        private GUIStyle style;
        private void OnGUI()
        {
            if (movement == null || presentation == null) return;
            if (style == null) style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 16, padding = new RectOffset(18,18,12,12) };
            GUI.Box(new Rect(18,18,490,125), "MYSTIC WOODS · LABORATÓRIO DO PERSONAGEM\n" +
                "WASD / Setas: mover   |   Shift: correr\n" +
                "Estado: " + presentation.State + "    Direção: " + presentation.Facing + "\n" +
                "Velocidade: " + movement.Velocity.magnitude.ToString("F2") + " u/s    Trocas de estado: " + presentation.StateChanges + "\n" +
                "Base isolada · sem combate, vida ou dependências do mapa", style);
        }
    }
}
