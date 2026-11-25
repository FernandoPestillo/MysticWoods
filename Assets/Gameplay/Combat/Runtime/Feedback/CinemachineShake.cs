using Unity.Cinemachine;
using UnityEngine;

namespace MysticWoods.Gameplay.Combat
{
    /// <summary>Applies <see cref="CameraShake"/> as a position correction, so follow logic is untouched.</summary>
    [AddComponentMenu("Mystic Woods/Combat Camera Shake")]
    public sealed class CinemachineShake : CinemachineExtension
    {
        [SerializeField, Min(0f)] private float maxOffset = .32f;
        [SerializeField, Min(.1f)] private float frequency = 22f;
        private float seed;

        protected override void Awake()
        {
            base.Awake();
            seed = Random.value * 100f;
        }

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize) return;
            float trauma = CameraShake.Sample();
            if (trauma <= 0f) return;
            float shake = trauma * trauma * maxOffset;
            float t = Time.unscaledTime * frequency;
            state.PositionCorrection += new Vector3(
                (Mathf.PerlinNoise(seed, t) - .5f) * 2f * shake,
                (Mathf.PerlinNoise(seed + 17.3f, t) - .5f) * 2f * shake, 0f);
        }
    }
}
