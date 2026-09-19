using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace LateSubmission.Core
{
    /// <summary>
    /// Smooth screen fade to black for checkpoint transitions between scenes.
    /// </summary>
    public class SceneFader : MonoBehaviour
    {
        public static SceneFader Instance { get; private set; }

        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _defaultFadeDuration = 1.2f;
        [SerializeField] private bool _fadeInOnStart = true;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponentInChildren<CanvasGroup>();
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = false;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
        }

        private void Start()
        {
            if (_fadeInOnStart && _canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                StartCoroutine(FadeRoutine(1f, 0f, _defaultFadeDuration, null));
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_fadeInOnStart && _canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                FadeIn(_defaultFadeDuration);
            }
        }

        public void FadeIn(float duration = 1.0f, Action onComplete = null)
        {
            StartCoroutine(FadeRoutine(1f, 0f, duration, onComplete));
        }

        public void FadeOut(float duration = 1.0f, Action onComplete = null)
        {
            StartCoroutine(FadeRoutine(0f, 1f, duration, onComplete));
        }

        public void FadeOutAndLoadScene(string sceneName, float duration = 1.2f)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = true;
            }

            StartCoroutine(FadeRoutine(0f, 1f, duration, () =>
            {
                if (!string.IsNullOrEmpty(sceneName))
                {
                    SceneManager.LoadScene(sceneName);
                }
            }));
        }

        private IEnumerator FadeRoutine(float startAlpha, float targetAlpha, float duration, Action onComplete)
        {
            if (_canvasGroup == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            float elapsed = 0f;
            _canvasGroup.alpha = startAlpha;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
                yield return null;
            }

            _canvasGroup.alpha = targetAlpha;
            if (targetAlpha <= 0.01f)
            {
                _canvasGroup.blocksRaycasts = false;
            }

            onComplete?.Invoke();
        }
    }
}
