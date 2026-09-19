using System;
using UnityEngine;
using LateSubmission.Attention;

namespace LateSubmission.Interaction
{
    /// <summary>
    /// Story notes and environmental logs reader.
    /// Opens the note reading UI panel upon interaction and emits slight noise.
    /// </summary>
    public class InspectNote : MonoBehaviour, IInteractable
    {
        [Header("Note Content")]
        [SerializeField] private string _noteTitle = "Security Memo";
        [TextArea(4, 10)]
        [SerializeField] private string _noteBody = "Block C closes 9 PM sharp per new policy.";

        [Header("Audio")]
        [SerializeField] private AudioClip _paperRustleSfx;

        public static event Action<string, string> OnOpenNoteReader;

        public string NoteTitle => _noteTitle;
        public string NoteBody => _noteBody;

        public string GetInteractionText()
        {
            return $"Read {_noteTitle} [E]";
        }

        public bool CanInteract(Interactor interactor)
        {
            return true;
        }

        public void Interact(Interactor interactor)
        {
            if (_paperRustleSfx != null)
            {
                AudioSource.PlayClipAtPoint(_paperRustleSfx, transform.position);
            }

            AttentionManager.Emit(transform.position, 1.0f, AttentionType.Interaction);
            OnOpenNoteReader?.Invoke(_noteTitle, _noteBody);
        }
    }
}
