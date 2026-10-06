using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using TMPro;
using LateSubmission.AI;
using LateSubmission.Player;

namespace LateSubmission.UI
{
    /// <summary>
    /// Displays the Demo Completion / Paywall screen when the monster is defeated on Floor 02.
    /// Shows the "To unlock the full game pay Rs. 1500" message with the UPI QR code.
    /// </summary>
    public class DemoCompletionUI : MonoBehaviour
    {
        [Header("QR Code Sprite")]
        [SerializeField] private Sprite _qrCodeSprite;

        [Header("Audio")]
        [SerializeField] private AudioClip _victoryStinger;

        public static DemoCompletionUI Instance { get; private set; }

        private Canvas _canvas;
        private GameObject _panel;
        private bool _hasShown = false;

        private void Awake()
        {
            Instance = this;
            BossMonsterController.OnBossDefeated += HandleBossDefeated;

            if (_qrCodeSprite == null)
            {
                // Runtime fallback: load from Resources folder (works in standalone builds)
                var tex = Resources.Load<Texture2D>("Paywall_QRCode");
                if (tex != null)
                {
                    _qrCodeSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
#if UNITY_EDITOR
                else
                {
                    _qrCodeSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Textures/Paywall_QRCode.jpg");
                }
#endif
            }
        }

        private void OnDestroy()
        {
            BossMonsterController.OnBossDefeated -= HandleBossDefeated;
        }

        private void HandleBossDefeated()
        {
            if (_hasShown) return;
            _hasShown = true;
            StartCoroutine(ShowCompletionRoutine());
        }

        private IEnumerator ShowCompletionRoutine()
        {
            // Allow monster collapse animation and death sound to finish
            yield return new WaitForSeconds(1.8f);

            BuildAndShowUI();
        }

        [ContextMenu("Test Show Completion UI")]
        public void BuildAndShowUI()
        {
            if (_panel != null)
            {
                _panel.SetActive(true);
                return;
            }

            // Create Canvas if not already attached
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null)
            {
                var canvasGo = new GameObject("Canvas_DemoCompletion");
                _canvas = canvasGo.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 999;
                canvasGo.AddComponent<CanvasScaler>();
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            // Ensure UI EventSystem is running
            EnsureEventSystem();

            // Unlock and reveal mouse cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Stop player movement
            var fps = Object.FindFirstObjectByType<FPSController>();
            if (fps != null)
            {
                fps.enabled = false;
            }

            // Fullscreen dark backdrop
            _panel = new GameObject("Panel_Paywall");
            _panel.transform.SetParent(_canvas.transform, false);
            var panelRect = _panel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;

            var bgImage = _panel.AddComponent<Image>();
            bgImage.color = new Color(0.04f, 0.05f, 0.07f, 0.94f);

            // Container Card
            var cardGo = new GameObject("Card_Content");
            cardGo.transform.SetParent(_panel.transform, false);
            var cardRect = cardGo.AddComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(580f, 780f);
            cardRect.anchoredPosition = Vector2.zero;

            var cardImg = cardGo.AddComponent<Image>();
            cardImg.color = new Color(0.09f, 0.11f, 0.14f, 0.98f);

            // Title
            var titleGo = new GameObject("Text_Title");
            titleGo.transform.SetParent(cardGo.transform, false);
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -22f);
            titleRect.sizeDelta = new Vector2(-40f, 60f);

            var titleText = titleGo.AddComponent<TextMeshProUGUI>();
            titleText.text = "LATE SUBMISSION";
            titleText.fontSize = 32;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = new Color(1f, 0.78f, 0.15f, 1f);

            // Subtitle
            var subGo = new GameObject("Text_Subtitle");
            subGo.transform.SetParent(cardGo.transform, false);
            var subRect = subGo.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0f, 1f);
            subRect.anchorMax = new Vector2(1f, 1f);
            subRect.pivot = new Vector2(0.5f, 1f);
            subRect.anchoredPosition = new Vector2(0f, -70f);
            subRect.sizeDelta = new Vector2(-40f, 40f);

            var subText = subGo.AddComponent<TextMeshProUGUI>();
            subText.text = "Prof. Anish Mondal Defeated - Chapter 1 Complete";
            subText.fontSize = 17;
            subText.alignment = TextAlignmentOptions.Center;
            subText.color = new Color(0.85f, 0.88f, 0.92f, 0.9f);

            // Paywall Message Banner
            var payMsgGo = new GameObject("Text_PayMessage");
            payMsgGo.transform.SetParent(cardGo.transform, false);
            var payMsgRect = payMsgGo.AddComponent<RectTransform>();
            payMsgRect.anchorMin = new Vector2(0f, 1f);
            payMsgRect.anchorMax = new Vector2(1f, 1f);
            payMsgRect.pivot = new Vector2(0.5f, 1f);
            payMsgRect.anchoredPosition = new Vector2(0f, -112f);
            payMsgRect.sizeDelta = new Vector2(-30f, 48f);

            var payText = payMsgGo.AddComponent<TextMeshProUGUI>();
            payText.text = "TO UNLOCK THE FULL GAME PAY RS. 1500";
            payText.fontSize = 20;
            payText.fontStyle = FontStyles.Bold;
            payText.alignment = TextAlignmentOptions.Center;
            payText.color = new Color(1f, 0.35f, 0.35f, 1f);

            // QR Code Image
            var qrGo = new GameObject("Image_QRCode");
            qrGo.transform.SetParent(cardGo.transform, false);
            var qrRect = qrGo.AddComponent<RectTransform>();
            qrRect.anchorMin = new Vector2(0.5f, 0.5f);
            qrRect.anchorMax = new Vector2(0.5f, 0.5f);
            qrRect.sizeDelta = new Vector2(280f, 380f);
            qrRect.anchoredPosition = new Vector2(0f, 15f);

            var qrImage = qrGo.AddComponent<Image>();
            if (_qrCodeSprite != null)
            {
                qrImage.sprite = _qrCodeSprite;
            }
            qrImage.preserveAspect = true;

            // UPI ID Note
            var upiGo = new GameObject("Text_UpiDetails");
            upiGo.transform.SetParent(cardGo.transform, false);
            var upiRect = upiGo.AddComponent<RectTransform>();
            upiRect.anchorMin = new Vector2(0f, 0f);
            upiRect.anchorMax = new Vector2(1f, 0f);
            upiRect.pivot = new Vector2(0.5f, 0f);
            upiRect.anchoredPosition = new Vector2(0f, 90f);
            upiRect.sizeDelta = new Vector2(-40f, 45f);

            var upiText = upiGo.AddComponent<TextMeshProUGUI>();
            upiText.text = "Scan with Paytm / GPay / PhonePe / BHIM\nUPI: <b>8927421754@ptsbi</b> (Srinjoy Das)";
            upiText.fontSize = 14;
            upiText.alignment = TextAlignmentOptions.Center;
            upiText.color = new Color(0.75f, 0.85f, 0.95f, 0.95f);

            // Buttons Bar (Restart Demo / Quit)
            var btnBarGo = new GameObject("Buttons_Bar");
            btnBarGo.transform.SetParent(cardGo.transform, false);
            var btnBarRect = btnBarGo.AddComponent<RectTransform>();
            btnBarRect.anchorMin = new Vector2(0f, 0f);
            btnBarRect.anchorMax = new Vector2(1f, 0f);
            btnBarRect.pivot = new Vector2(0.5f, 0f);
            btnBarRect.anchoredPosition = new Vector2(0f, 22f);
            btnBarRect.sizeDelta = new Vector2(-40f, 52f);

            CreateButton(btnBarGo.transform, "Btn_Restart", "Play Again", new Vector2(-120f, 0f), new Vector2(210f, 46f), () =>
            {
                Time.timeScale = 1f;
                UnityEngine.SceneManagement.SceneManager.LoadScene("Floor01_Main");
            });

            CreateButton(btnBarGo.transform, "Btn_Quit", "Quit Game", new Vector2(120f, 0f), new Vector2(210f, 46f), () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        }

        private void CreateButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var btnGo = new GameObject(name);
            btnGo.transform.SetParent(parent, false);
            var rect = btnGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;

            var img = btnGo.AddComponent<Image>();
            img.color = new Color(0.18f, 0.22f, 0.28f, 1f);

            var btn = btnGo.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.18f, 0.22f, 0.28f, 1f);
            colors.highlightedColor = new Color(0.28f, 0.35f, 0.45f, 1f);
            colors.pressedColor = new Color(0.12f, 0.15f, 0.20f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(btnGo.transform, false);
            var txtRect = txtGo.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.sizeDelta = Vector2.zero;

            var txt = txtGo.AddComponent<TextMeshProUGUI>();
            txt.text = label;
            txt.fontSize = 17;
            txt.fontStyle = FontStyles.Bold;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = Color.white;
        }

        public static void EnsureEventSystem()
        {
            var existingGo = GameObject.Find("EventSystem");
            if (existingGo == null)
            {
                var esAny = Object.FindFirstObjectByType<EventSystem>();
                if (esAny != null) existingGo = esAny.gameObject;
                else existingGo = new GameObject("EventSystem");
            }

            var es = existingGo.GetComponent<EventSystem>();
            if (es == null) es = existingGo.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM
            var sm = existingGo.GetComponent<StandaloneInputModule>();
            if (sm != null) Destroy(sm);

            var im = existingGo.GetComponent<InputSystemUIInputModule>();
            if (im == null) im = existingGo.AddComponent<InputSystemUIInputModule>();
#else
            var sm = existingGo.GetComponent<StandaloneInputModule>();
            if (sm == null) existingGo.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
