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

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.isKinematic = true;
            _rb.interpolation = RigidbodyInterpolation.None;
        }

        private void Start()
        {
            SetPoints(PatrollingPlatformSpawner.PointA, PatrollingPlatformSpawner.PointB);
        }

        public void Tick(uint tick)
        {
            if (pointA == null || pointB == null)
                return;
    
            // Distance between points, used to convert speed in units/sec to t/sec
            float distance = Vector3.Distance(pointA.position, pointB.position);
            if (distance < 0.01f)
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

            // Interpolate position on the line (purely deterministic math)
            Vector3 oldPos = _rb.position;
            Quaternion oldRot = _rb.rotation;

            Vector3 newPos = Vector3.Lerp(pointA.position, pointB.position, _t);
            Quaternion newRot = oldRot;

            if (orientToPath)
            {
                Vector3 moveDir = (newPos - oldPos);
                if (moveDir.sqrMagnitude > 0.000001f)
                {
                    newRot = Quaternion.LookRotation(moveDir.normalized, Vector3.up);
                }
            }

            // Drive a kinematic body deterministically; physics will
            // handle passengers based on this swept motion.
            _rb.MovePosition(newPos);
            _rb.MoveRotation(newRot);
        }

        public void SetPoints(Transform a, Transform b)
        {
            pointA = a;
            pointB = b;
            _t = 0f;
            _movingForward = true;

            if (a != null)
            {
                _rb.position = a.position;
                _rb.rotation = Quaternion.identity;
            }
        }
    
        #region State
        public IState GetCurrentState()
        {
            return new PatrollingPlatformState()
            {
                Position = _rb.position,
                Rotation = _rb.rotation.eulerAngles,
                Time = _t,
                Direction = _movingForward
            };
        }

        public void UpdateState(IState state)
        {
            PatrollingPlatformState platformState = (PatrollingPlatformState) state;

            _rb.position = platformState.Position;
            _rb.rotation = Quaternion.Euler(platformState.Rotation);
            _t = platformState.Time;
            _movingForward = platformState.Direction;
        }

        public void ApplyState(IState state)
        {
            PatrollingPlatformState platformState = (PatrollingPlatformState) state;

            _rb.position = platformState.Position;
            _rb.rotation = Quaternion.Euler(platformState.Rotation);
            _t = platformState.Time;
            _movingForward = platformState.Direction;
        }

        public void ApplyPartialState(IState state, uint part)
        {
            throw new System.NotImplementedException();
        }
        #endregion
    
        protected override bool IsPredicted() => true;
    }
}