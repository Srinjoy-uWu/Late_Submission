using UnityEngine;
using UnityEngine.AI;
using LateSubmission.Core;
using LateSubmission.Weapon;

namespace LateSubmission.AI
{
    /// <summary>
    /// Master controller for 'The Late One'.
    /// Manages NavMeshAgent, perception sensors, FSM states, and laser staggering.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(EntityPerception))]
    public class EntityController : MonoBehaviour, IStaggerable
    {
        [Header("Movement Speeds")]
        [SerializeField] private float _patrolSpeed = 1.8f;
        [SerializeField] private float _investigateSpeed = 3.2f;
        [SerializeField] private float _huntSpeed = 5.0f;

        [Header("Waypoints")]
        [SerializeField] private Transform[] _waypoints;

        [Header("Audio & Reactions")]
        [SerializeField] private AudioClip _staggerScreechSfx;
        [SerializeField] private AudioClip _huntBreathingSfx;
        [SerializeField] private AudioSource _audioSource;

        public NavMeshAgent Agent { get; private set; }
        public EntityPerception Perception { get; private set; }
        public StateMachine StateMachine { get; private set; }
        public Transform[] Waypoints => _waypoints;

        public float PatrolSpeed => _patrolSpeed;
        public float InvestigateSpeed => _investigateSpeed;
        public float HuntSpeed => _huntSpeed;

        private void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            Perception = GetComponent<EntityPerception>();
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();

            StateMachine = new StateMachine();
        }

        private void Start()
        {
            // Start in Patrol state by default
            StateMachine.ChangeState(new EntityPatrolState(this));
        }

        private void Update()
        {
            StateMachine.Tick();
        }

        public void Stagger(float duration = 3.0f)
        {
            if (_staggerScreechSfx != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_staggerScreechSfx);
            }

            StateMachine.ChangeState(new EntityStaggerState(this, duration));
        }

        public void SetHuntAudio(bool active)
        {
            if (_audioSource != null && _huntBreathingSfx != null)
            {
                if (active && !_audioSource.isPlaying)
                {
                    _audioSource.clip = _huntBreathingSfx;
                    _audioSource.loop = true;
                    _audioSource.Play();
                }
                else if (!active && _audioSource.clip == _huntBreathingSfx)
                {
                    _audioSource.Stop();
                }
            }
        }
    }
}
