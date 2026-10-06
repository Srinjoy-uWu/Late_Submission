using System;
using UnityEngine;
using LateSubmission.Inventory;
using LateSubmission.Interaction;

namespace LateSubmission.Objectives
{
    /// <summary>
    /// Coordinates progression objectives across Floor 1 and Floor 2.
    /// Broadcasts updates to the HUD.
    /// </summary>
    public class ObjectiveManager : MonoBehaviour
    {
        public static ObjectiveManager Instance { get; private set; }

        [SerializeField] private ObjectiveType _currentObjective = ObjectiveType.FindFacultyKey;

        public ObjectiveType CurrentObjective => _currentObjective;

        public event Action<string> OnObjectiveUpdated;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnItemAdded += HandleItemAdded;

                // Sync objective when returning to Floor 01 corridor from Security Room or Cabin 104
                if ((InventoryManager.Instance.HasItemType(ItemType.Floor02Key) ||
                     InventoryManager.Instance.HasItemType(ItemType.AssignmentCoverSheet)) &&
                    _currentObjective < ObjectiveType.ProceedToFloor2)
                {
                    _currentObjective = ObjectiveType.ProceedToFloor2;
                }
                else if (InventoryManager.Instance.HasItemType(ItemType.FacultyKey) &&
                         _currentObjective < ObjectiveType.UnlockFacultyCabin)
                {
                    _currentObjective = ObjectiveType.UnlockFacultyCabin;
                }
            }
            Workbench.OnLaserAssembled += HandleLaserAssembled;

            UpdateObjectiveText();
        }

        private void OnDestroy()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnItemAdded -= HandleItemAdded;
            }
            Workbench.OnLaserAssembled -= HandleLaserAssembled;
        }

        public void SetObjective(ObjectiveType newObjective)
        {
            _currentObjective = newObjective;
            UpdateObjectiveText();
        }

        public void SetCustomObjective(string customText)
        {
            OnObjectiveUpdated?.Invoke(customText);
        }

        /// <summary>Causes the current objective text to be re-broadcast. Call after subscribing to OnObjectiveUpdated.</summary>
        public void RefreshObjectiveText()
        {
            UpdateObjectiveText();
        }

        private void HandleItemAdded(ItemData item)
        {
            if (item.ItemType == ItemType.FacultyKey && _currentObjective == ObjectiveType.FindFacultyKey)
            {
                SetObjective(ObjectiveType.UnlockFacultyCabin);
            }
            else if (item.ItemType == ItemType.AssignmentCoverSheet || item.ItemType == ItemType.Floor02Key)
            {
                SetObjective(ObjectiveType.ProceedToFloor2);
            }
            else if (InventoryManager.Instance.HasAllLaserComponents() && _currentObjective == ObjectiveType.CollectLaserComponents)
            {
                SetObjective(ObjectiveType.AssembleLaser);
            }
        }

        private void HandleLaserAssembled()
        {
            SetObjective(ObjectiveType.EscapeFloor2);
        }

        private void UpdateObjectiveText()
        {
            string text = _currentObjective switch
            {
                ObjectiveType.EnterBuilding => "Enter the Academic Block",
                ObjectiveType.FindFacultyKey => "Locate the Faculty Cabin Key at Security Desk",
                ObjectiveType.UnlockFacultyCabin => "Unlock Faculty Cabin 104 in the Faculty Wing",
                ObjectiveType.CollectCoverSheet => "Retrieve the Assignment Cover Sheet from the desk",
                ObjectiveType.ProceedToFloor2 => "Take the Stairwell to Floor 2 (Mechatronics & EC Labs)",
                ObjectiveType.CollectLaserComponents => "Search Mechatronics & EC Labs for Light Gun Components",
                ObjectiveType.AssembleLaser => "Assemble the Light Gun at the Central Workbench",
                ObjectiveType.EscapeFloor2 => "Repel the Entity and Reach the Floor 3 Stairwell",
                ObjectiveType.Completed => "Vertical Slice Complete",
                _ => ""
            };

            OnObjectiveUpdated?.Invoke(text);
        }
    }
}
