using UnityEngine;

namespace MysticWoods.Gameplay.Player
{
    public enum PlayerFacing { Down, Right, Up, Left }
    public enum PlayerMotionState { Idle, Walk }

    public static class PlayerDirection
    {
        // Keep the previous axis on near-diagonals, but always respect the motion's sign.
        public static PlayerFacing Resolve(Vector2 motion, PlayerFacing previous, float hysteresis)
        {
            if (motion.sqrMagnitude < .000001f) return previous;
            if (Mathf.Abs(motion.y) <= .0001f) return motion.x > 0 ? PlayerFacing.Right : PlayerFacing.Left;
            if (Mathf.Abs(motion.x) <= .0001f) return motion.y > 0 ? PlayerFacing.Up : PlayerFacing.Down;
            float difference = Mathf.Abs(motion.x) - Mathf.Abs(motion.y);
            bool horizontal = Mathf.Abs(difference) <= hysteresis
                ? previous == PlayerFacing.Left || previous == PlayerFacing.Right : difference > 0;
            if (horizontal && Mathf.Abs(motion.x) > .0001f)
                return motion.x > 0 ? PlayerFacing.Right : PlayerFacing.Left;
            return motion.y > 0 ? PlayerFacing.Up : PlayerFacing.Down;
        }
    }
}
