using System.Collections.Generic;
using Common;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Common
{
    // ボタンイベント管理の基底クラス（コントローラー/キーボード対応）.
    public abstract class ButtonEventer : MonoBehaviour
    {
        // イベンターの活性化条件.
        private bool eventerEnable = true;

        public void EnableEventer()
        {
            eventerEnable = true;
        }

        public void DisableEventer()
        {
            eventerEnable = false;
        }

        // Animatorトリガーハッシュ.
        private static readonly int Highlighted = Animator.StringToHash("Highlighted");
        private static readonly int Selected = Animator.StringToHash("Selected");
        private static readonly int Normal = Animator.StringToHash("Normal");
        private static readonly int Pressed = Animator.StringToHash("Pressed");

        protected InputSystem_Actions action;
        protected virtual Button[][] buttons { get; set; }
        private Button previousButton;

        private HashSet<Button> alreadyButtonEvent;

        private int currentIndex_x = 0;
        private int currentIndex_y = 0;

        // 入力連打防止用クールダウン.
        private float navigateCooldown = 0.2f;
        private float lastNavigateTime = 0f;

        // ボタンクリッククールダウン.
        private float buttonCooldown = 0.1f;
        private float lastButtonTime = 0f;

        public void Awake()
        {
            alreadyButtonEvent = new HashSet<Button>();
            action = InputSystemActionsManager.Instance().GetInputSystem_Actions();

            Init();

            if (buttons == null || buttons.Length == 0) return;

            // 全ボタンにイベントを設定.
            foreach (var row in buttons)
            {
                foreach (var button in row)
                {
                    if (button == null) continue;
                    if (!alreadyButtonEvent.Contains(button))
                    {
                        alreadyButtonEvent.Add(button);
                        AddHoverEvents(button);
                        AddButtonEvent(button);
                        // TimeScale無視モードに設定.
                        Animator animator = button.GetComponent<Animator>();
                        if (animator != null)
                        {
                            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                        }
                    }
                }
            }

            // 最初のボタンを選択.
            if (buttons[currentIndex_y][currentIndex_x] != null)
            {
                SelectButton(buttons[currentIndex_y][currentIndex_x]);
            }
        }

        public void Update()
        {
            CursolUpdate();
        }

        // ボタンの格納とイベント設定を行う（継承先で実装）.
        protected virtual void Init()
        {
        }

        // ボタンにクリックイベントを追加.
        public void AddButtonEvent(Button target)
        {
            target.onClick.AddListener(() =>
            {
                if (eventerEnable && Time.unscaledTime - lastButtonTime >= buttonCooldown)
                {
                    lastButtonTime = Time.unscaledTime;
                    ButtonEvents(target);
                }
            });
        }

        // 実行処理（OnClick用、継承先で実装）.
        protected virtual void ButtonEvents(Button button)
        {
        }

        // カーソル更新処理（コントローラー対応）.
        private void CursolUpdate()
        {
            if (buttons == null || buttons.Length == 0) return;

            Vector2 nav = action.UI.Navigate.ReadValue<Vector2>();

            // 時間経過チェック（入力連打防止）.
            if (Time.unscaledTime - lastNavigateTime < navigateCooldown) return;

            if (nav.y > 0.5f)
            {
                // 上方向.
                currentIndex_y = (currentIndex_y - 1 + buttons.Length) % buttons.Length;
                SelectButton(buttons[currentIndex_y][currentIndex_x]);
                lastNavigateTime = Time.unscaledTime;
            }
            else if (nav.y < -0.5f)
            {
                // 下方向.
                currentIndex_y = (currentIndex_y + 1) % buttons.Length;
                SelectButton(buttons[currentIndex_y][currentIndex_x]);
                lastNavigateTime = Time.unscaledTime;
            }

            if (action.UI.Submit.WasPressedThisFrame())
            {
                if (Time.unscaledTime - lastButtonTime >= buttonCooldown)
                {
                    lastButtonTime = Time.unscaledTime;
                    ButtonEvents(buttons[currentIndex_y][currentIndex_x]);
                }
            }
        }

        // ボタンの選択状態を設定.
        private void SelectButton(Button button)
        {
            if (button == null) return;

            // 前のボタンをNormalに戻す.
            if (previousButton != null && previousButton != button)
            {
                Animator prevAnimator = previousButton.GetComponent<Animator>();
                if (prevAnimator != null)
                {
                    prevAnimator.SetTrigger(Normal);
                }
            }

            // 現在のボタンを選択状態に.
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(button.gameObject);
            }
            button.Select();

            Animator animator = button.GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetTrigger(Highlighted);
            }

            previousButton = button;
        }

        // ボタンのホバーイベントを設定.
        protected void AddHoverEvents(Button target)
        {
            Animator animator = target.gameObject.GetComponent<Animator>();
            EventTrigger trigger = target.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = target.gameObject.AddComponent<EventTrigger>();
            }

            // PointerEnter.
            EventTrigger.Entry entryEnter = new EventTrigger.Entry();
            entryEnter.eventID = EventTriggerType.PointerEnter;
            entryEnter.callback.AddListener((eventData) =>
            {
                // マウスで選択時、他のボタンをリセット.
                foreach (var row in buttons)
                {
                    foreach (var button in row)
                    {
                        if (button == null || button == target) continue;
                        Animator btnAnimator = button.GetComponent<Animator>();
                        if (btnAnimator != null)
                        {
                            btnAnimator.SetTrigger(Normal);
                        }
                    }
                }
                if (animator != null) animator.SetTrigger(Highlighted);
            });
            trigger.triggers.Add(entryEnter);

            // PointerUp.
            EventTrigger.Entry entryUp = new EventTrigger.Entry();
            entryUp.eventID = EventTriggerType.PointerUp;
            entryUp.callback.AddListener((eventData) =>
            {
                if (animator != null)
                {
                    animator.SetTrigger(Highlighted);
                    animator.ResetTrigger(Pressed);
                }
            });
            trigger.triggers.Add(entryUp);

            // PointerDown.
            EventTrigger.Entry entryDown = new EventTrigger.Entry();
            entryDown.eventID = EventTriggerType.PointerDown;
            entryDown.callback.AddListener((eventData) =>
            {
                if (animator != null) animator.SetTrigger(Pressed);
            });
            trigger.triggers.Add(entryDown);

            // PointerExit.
            EventTrigger.Entry entryExit = new EventTrigger.Entry();
            entryExit.eventID = EventTriggerType.PointerExit;
            entryExit.callback.AddListener((eventData) =>
            {
                if (animator != null)
                {
                    animator.ResetTrigger(Highlighted);
                    animator.SetTrigger(Normal);
                }
            });
            trigger.triggers.Add(entryExit);
        }

        private void OnDestroy()
        {
            // 全ボタンのリスナーを解除.
            if (buttons != null)
            {
                foreach (var row in buttons)
                {
                    foreach (var button in row)
                    {
                        if (button != null)
                        {
                            button.onClick.RemoveAllListeners();
                        }
                    }
                }
            }
        }
    }
}
