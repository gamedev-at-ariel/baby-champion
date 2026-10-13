using BabyChampion.Simulation;
using UnityEngine;
using UnityEngine.AI;

namespace BabyChampion.Game
{
    /// <summary>
    /// The placeholder parent: walks on the NavMesh and shows the current action with simple
    /// procedural poses (sitting, lying, kneeling, swaying, eating). Driven by <see cref="HouseGame"/>.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class ParentAvatar : MonoBehaviour
    {
        [SerializeField] Transform body;
        [SerializeField] Transform torso;
        [SerializeField] Transform hand;
        [SerializeField] Transform arms;
        [SerializeField] Transform shoulder;
        [SerializeField] float walkSpeed = 9f;
        [SerializeField] float lowEnergySpeedFactor = 0.75f;

        NavMeshAgent _agent;
        Vector3 _torsoRest, _handRest;
        float _stuckTime;
        Vector3 _lastPosition;

        public Transform Arms => arms;
        public Transform Shoulder => shoulder;
        public NavMeshAgent Agent => _agent != null ? _agent : _agent = GetComponent<NavMeshAgent>();
        public bool Walking { get; private set; }
        /// <summary>Set when the last walk could not reach its destination.</summary>
        public bool WalkFailed { get; private set; }
        public Vector3 Destination { get; private set; }

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _torsoRest = torso != null ? torso.localPosition : Vector3.zero;
            _handRest = hand != null ? hand.localPosition : Vector3.zero;
        }

        public void SetLowEnergy(bool low) => Agent.speed = walkSpeed * (low ? lowEnergySpeedFactor : 1f);

        /// <summary>Starts walking; returns false when there is no path.</summary>
        public bool WalkTo(Vector3 destination)
        {
            WalkFailed = false;
            if (!NavMesh.SamplePosition(destination, out var hit, 2f, NavMesh.AllAreas))
            {
                WalkFailed = true;
                return false;
            }
            Destination = hit.position;
            if (!Agent.isOnNavMesh) Agent.Warp(transform.position);
            Agent.isStopped = false;
            if (!Agent.SetDestination(hit.position))
            {
                WalkFailed = true;
                return false;
            }
            Walking = true;
            _stuckTime = 0;
            _lastPosition = transform.position;
            return true;
        }

        public void StopWalking()
        {
            Walking = false;
            if (Agent.isOnNavMesh) Agent.ResetPath();
        }

        public void Teleport(Vector3 position, float yaw)
        {
            StopWalking();
            Agent.Warp(position);
            transform.rotation = Quaternion.Euler(0, yaw, 0);
        }

        public void Face(Vector3 target)
        {
            var d = target - transform.position;
            d.y = 0;
            if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(d);
        }

        void Update()
        {
            if (!Walking) return;
            if (Agent.pathPending) return;
            if (Agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                Walking = false;
                WalkFailed = true;
                return;
            }
            if (Agent.remainingDistance <= Agent.stoppingDistance + 0.05f)
            {
                Walking = false;
                return;
            }
            // A blocked agent counts as arrived when it is close, failed otherwise.
            if ((transform.position - _lastPosition).sqrMagnitude < 0.0001f) _stuckTime += Time.deltaTime;
            else _stuckTime = 0;
            _lastPosition = transform.position;
            if (_stuckTime > 1.5f)
            {
                Walking = false;
                WalkFailed = Vector3.Distance(transform.position, Destination) > 1.2f;
                StopWalking();
            }
        }

        /// <summary>Places the body for the current activity. <paramref name="station"/> may be null.</summary>
        public void Show(ParentPose pose, ActionType? action, Interactable station)
        {
            if (body == null) return;
            float t = Time.time;
            var use = station != null && station.usePoint != null ? station.usePoint : null;

            switch (pose)
            {
                case ParentPose.Sit when use != null:
                    body.SetPositionAndRotation(use.position + Vector3.down * 0.45f, use.rotation);
                    break;
                case ParentPose.Lie when use != null:
                    body.SetPositionAndRotation(use.position + Vector3.up * 0.25f, use.rotation * Quaternion.Euler(-90f, 0f, 0f));
                    break;
                case ParentPose.Kneel:
                    body.localPosition = new Vector3(0, -0.5f, 0);
                    body.localRotation = Quaternion.identity;
                    break;
                default:
                    body.localPosition = Vector3.zero;
                    body.localRotation = action == ActionType.Soothe || action == ActionType.PutBabyToSleep
                        ? Quaternion.Euler(0, 0, Mathf.Sin(t * 3f) * 7f) // rocking
                        : Quaternion.identity;
                    break;
            }

            if (torso != null)
                torso.localPosition = _torsoRest + (Walking ? Vector3.up * Mathf.Abs(Mathf.Sin(t * 10f)) * 0.06f : Vector3.zero);

            if (hand != null)
            {
                var offset = Vector3.zero;
                switch (action)
                {
                    case ActionType.Eat: offset = new Vector3(-0.1f, 0.45f + Mathf.Sin(t * 6f) * 0.15f, -0.1f); break;
                    case ActionType.PrepareBottle:
                    case ActionType.ChangeDiaper: offset = new Vector3(Mathf.Sin(t * 5f) * 0.15f, 0, 0.2f); break;
                    case ActionType.Burp: offset = new Vector3(-0.1f, 0.35f + Mathf.Abs(Mathf.Sin(t * 8f)) * 0.08f, -0.05f); break;
                }
                hand.localPosition = _handRest + offset;
            }
        }
    }
}
