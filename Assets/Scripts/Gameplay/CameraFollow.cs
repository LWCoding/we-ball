using UnityEngine;

namespace WeBall.Gameplay
{
    /// <summary>
    /// Smoothly follows the local player's ball.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Vector2 offset = new(0f, 1.5f);
        [SerializeField] float smoothTime = 0.15f;

        Transform target;
        Vector3 velocity;

        public void SetTarget(Transform newTarget) => target = newTarget;

        void LateUpdate()
        {
            if (target == null) return;

            Vector3 goal = new(target.position.x + offset.x, target.position.y + offset.y, transform.position.z);
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);
        }
    }
}
