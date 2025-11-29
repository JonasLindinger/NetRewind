using System;
using NetRewind.Utils.Simulation;
using NetRewind.Utils.Simulation.State;
using UnityEngine;

namespace _Demo.Scripts.PatrollingPlatform
{
    [RequireComponent(typeof(Rigidbody))]
    public class PatrollingPlatformController : NetObject, ITick, IStateHolder
    {
        [Header("Path")]
        [SerializeField] private Transform pointA;
        [SerializeField] private Transform pointB;
        [Space(5)]
        [Header("Settings")]
        [SerializeField] private float speed = 3f;
        [SerializeField] private bool orientToPath = true;

        private float _t;
        private bool _movingForward = true;
        private Rigidbody _rb;

        private void Start()
        {
            SetPoints(PatrollingPlatformSpawner.PointA, PatrollingPlatformSpawner.PointB);
            
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;
        }

        public void Tick(uint tick)
        {
            if (pointA == null || pointB == null)
                return;

            // Distance between points, used to convert speed in units/sec to t/sec
            float distance = Vector3.Distance(pointA.position, pointB.position);
            if (distance < 0.0001f)
                return;

            float dt = (speed / distance) * Simulation.TimeBetweenTicks;

            if (_movingForward)
            {
                _t += dt;
                if (_t >= 1f)
                {
                    _t = 1f;
                    _movingForward = false; // reached B, go back
                }
            }
            else
            {
                _t -= dt;
                if (_t <= 0f)
                {
                    _t = 0f;
                    _movingForward = true; // reached A, go forward
                }
            }

            // Interpolate position on the line
            Vector3 newPos = Vector3.Lerp(pointA.position, pointB.position, _t);
            Vector3 oldPos = _rb.position;

            // Move via Rigidbody so physics knows about the motion
            _rb.MovePosition(newPos);

            if (orientToPath)
            {
                Vector3 moveDir = (newPos - oldPos);
                if (moveDir.sqrMagnitude > 0.000001f)
                {
                    Quaternion newRot = Quaternion.LookRotation(moveDir.normalized, Vector3.up);
                    _rb.MoveRotation(newRot);
                }
            }

            // No need to set linearVelocity for a kinematic body.
            // The swept movement between oldPos and newPos is what makes
            // other rigidbodies ride along with the platform.
        }

        public void SetPoints(Transform a, Transform b)
        {
            pointA = a;
            pointB = b;
            _t = 0f;
            _movingForward = true;

            if (a != null)
            {
                if (_rb == null) _rb = GetComponent<Rigidbody>();
                _rb.position = a.position;
            }
        }
        
        #region State
        public IState GetCurrentState()
        {
            return new PatrollingPlatformState()
            {
                Position = transform.position,
                Rotation = transform.eulerAngles,
            };
        }

        public void UpdateState(IState state)
        {
            PatrollingPlatformState platformState = (PatrollingPlatformState) state;
            transform.position = platformState.Position;
            transform.eulerAngles = platformState.Rotation;
        }

        public void ApplyState(IState state)
        {
            PatrollingPlatformState platformState = (PatrollingPlatformState) state;
            transform.position = platformState.Position;
            transform.eulerAngles = platformState.Rotation;
        }

        public void ApplyPartialState(IState state, uint part)
        {
            throw new System.NotImplementedException();
        }
        #endregion
        
        protected override bool IsPredicted() => true;
    }
}