using System;
using UnityEngine;
using UnityEngine.UI;

namespace MysticWoods.UI
{
    public sealed class MenuCommandsController : MonoBehaviour
    {
        [SerializeField] private Button backButton;
        public Button BackButton => backButton;
        public event Action Closed;
        public void Close() => Closed?.Invoke();
    }
}
