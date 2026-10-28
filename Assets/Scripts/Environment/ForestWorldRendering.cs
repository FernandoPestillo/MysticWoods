using UnityEngine;

namespace MysticWoods.Environment
{
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public sealed class ForestWorldRendering : MonoBehaviour
    {
        private void OnEnable()
        {
            var cameraComponent = GetComponent<Camera>();
            cameraComponent.transparencySortMode = TransparencySortMode.CustomAxis;
            cameraComponent.transparencySortAxis = Vector3.up;
        }
    }
}
