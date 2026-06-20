using System;
using QuietStatic.Input;
using QuietStatic.Toolkit.Core;
using UnityEngine;

namespace QuietStatic
{
    /// <summary>
    /// Stores the latest captured input state for global access.
    ///
    /// This class does not read from Unity's Input System directly.
    /// Instead, input reader components update this manager every frame.
    ///
    /// Responsibilities:
    /// - Store gameplay movement input
    /// - Store gameplay look input
    /// - Store gameplay action button states
    /// - Store UI navigation input
    /// - Store UI pointer/click/scroll input
    /// - Expose input events such as pause, character switching, submit, cancel, and skip
    ///
    /// This should live in the persistent System scene.
    /// </summary>
    public class GameInputManager : ToolkitSingleton<GameInputManager>,
            IMoveInputSource,
            ILookInputSource,
            IInteractInputSource
    {
        /////////////////////////////////////////////////////////////////////////////////////////
        //
        //                          GAMEPLAY INPUT STATE
        //
        /////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Move direction in forward/backward and left/right.
        /// </summary>
        public Vector2 Move { get; private set; }

        /// <summary>
        /// Look direction in up/down and left/right.
        /// </summary>
        public Vector2 Look { get; private set; }

        /// <summary>
        /// Whether sprint is currently being held.
        /// </summary>
        public bool Sprint { get; private set; }

        /// <summary>
        /// Whether interact was pressed this frame.
        /// </summary>
        public bool Interact { get; private set; }

        /// <summary>
        /// Whether pause was pressed this frame.
        /// </summary>
        public bool Pause { get; private set; }

        /// <summary>
        /// Whether a jump press is waiting to be consumed.
        /// </summary>
        private bool jumpQueued;

        private bool interactQueued;

        [SerializeField] private float interactBufferDuration = 0.15f;
        private float interactQueuedTime;

        /////////////////////////////////////////////////////////////////////////////////////////
        //
        //                          UI INPUT STATE
        //
        /////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// UI navigation direction, usually from keyboard, controller d-pad, or left stick.
        /// </summary>
        public Vector2 UINavigate { get; private set; }

        /// <summary>
        /// UI pointer position, usually from mouse or touchscreen.
        /// </summary>
        public Vector2 UIPoint { get; private set; }

        /// <summary>
        /// UI scroll wheel input.
        /// </summary>
        public Vector2 UIScrollWheel { get; private set; }

        /// <summary>
        /// Whether UI submit was pressed this frame.
        /// </summary>
        public bool UISubmit { get; private set; }

        /// <summary>
        /// Whether UI cancel was pressed this frame.
        /// </summary>
        public bool UICancel { get; private set; }

        /// <summary>
        /// Whether UI click was pressed this frame.
        /// </summary>
        public bool UIClick { get; private set; }

        /// <summary>
        /// Whether UI skip was pressed this frame.
        /// 
        /// Useful for cutscenes, typewriter text, and dialogue.
        /// </summary>
        public bool UISkip { get; private set; }


        /////////////////////////////////////////////////////////////////////////////////////////
        //
        //                          UI EVENTS
        //
        /////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Fired when the pause action is performed.
        /// </summary>
        public event Action OnPause;

        /// <summary>
        /// Fired when the UI submit action is performed.
        /// </summary>
        public event Action OnUISubmit;

        /// <summary>
        /// Fired when the UI cancel action is performed.
        /// </summary>
        public event Action OnUICancel;

        /// <summary>
        /// Fired when the UI click action is performed.
        /// </summary>
        public event Action OnUIClick;

        /// <summary>
        /// Fired when the UI skip action is performed.
        /// </summary>
        public event Action OnUISkip;

        private void Update()
        {
            if (!interactQueued)
            {
                return;
            }

            if (Time.time - interactQueuedTime > interactBufferDuration)
            {
                ClearInteractInput();
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////
        //
        //                          GAMEPLAY INPUT METHODS
        //
        /////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Updates movement-related input values.
        /// </summary>
        /// <param name="move">
        /// Current movement input.
        /// </param>
        /// <param name="look">
        /// Current look input.
        /// </param>
        /// <param name="sprint">
        /// Whether sprint is currently held.
        /// </param>
        public void SetMovementInput(Vector2 move, Vector2 look, bool sprint)
        {
            Move = move;
            Look = look;
            Sprint = sprint;
        }

        /// <summary>
        /// Updates whether interact was pressed this frame.
        /// </summary>
        /// <param name="interact">
        /// Whether interact was pressed this frame.
        /// </param>
        public void SetInteractInput(bool interact)
        {
            Interact = interact;
        }

        /// <summary>
        /// Queues a jump input so movement code can consume it once.
        /// </summary>
        public void QueueJump()
        {
            jumpQueued = true;
        }

        /// <summary>
        /// Returns whether jump was queued, then clears the queued jump.
        /// </summary>
        /// <returns>
        /// True if jump was queued. Otherwise, false.
        /// </returns>
        public bool ConsumeJump()
        {
            bool jumped = jumpQueued;
            jumpQueued = false;
            return jumped;
        }

        /// <summary>
        /// Queues an interact input so interaction code can consume it once.
        ///
        /// The input only remains valid briefly. This prevents an old interact press
        /// from firing later when the player walks into an interactable radius.
        /// </summary>
        public void QueueInteract()
        {
            Interact = true;
            interactQueued = true;
            interactQueuedTime = Time.time;
        }

        /// <summary>
        /// Returns whether interact was queued and still valid, then clears it.
        ///
        /// If the interact input is too old, this clears it and returns false.
        /// </summary>
        public bool ConsumeInteract()
        {
            if (!interactQueued)
            {
                return false;
            }

            bool isExpired = Time.time - interactQueuedTime > interactBufferDuration;

            if (isExpired)
            {
                ClearInteractInput();
                return false;
            }

            ClearInteractInput();
            return true;
        }

        /// <summary>
        /// Clears queued and one-frame interact input.
        /// </summary>
        private void ClearInteractInput()
        {
            Interact = false;
            interactQueued = false;
            interactQueuedTime = 0f;
        }

        /// <summary>
        /// Clears one-frame gameplay input values at the end of the frame.
        /// </summary>
        public void ClearFrameInput()
        {
            Pause = false;
        }

        /// <summary>
        /// Raises the pause event.
        /// </summary>
        public void RaisePause()
        {
            Pause = true;
            OnPause?.Invoke();
        }

        /////////////////////////////////////////////////////////////////////////////////////////
        //
        //                          UI INPUT METHODS
        //
        /////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Updates continuous UI input values.
        /// </summary>
        /// <param name="navigate">
        /// Current UI navigation input.
        /// </param>
        /// <param name="point">
        /// Current UI pointer position.
        /// </param>
        /// <param name="scrollWheel">
        /// Current UI scroll wheel input.
        /// </param>
        public void SetUIInput(Vector2 navigate, Vector2 point, Vector2 scrollWheel)
        {
            UINavigate = navigate;
            UIPoint = point;
            UIScrollWheel = scrollWheel;
        }

        /// <summary>
        /// Raises the UI submit event.
        /// </summary>
        public void RaiseUISubmit()
        {
            UISubmit = true;
            OnUISubmit?.Invoke();
        }

        /// <summary>
        /// Raises the UI cancel event.
        /// </summary>
        public void RaiseUICancel()
        {
            UICancel = true;
            OnUICancel?.Invoke();
        }

        /// <summary>
        /// Raises the UI click event.
        /// </summary>
        public void RaiseUIClick()
        {
            UIClick = true;
            OnUIClick?.Invoke();
        }

        /// <summary>
        /// Raises the UI skip event.
        /// </summary>
        public void RaiseUISkip()
        {
            UISkip = true;
            OnUISkip?.Invoke();
        }

        /// <summary>
        /// Clears one-frame UI input values at the end of the frame.
        /// </summary>
        public void ClearUIFrameInput()
        {
            UISubmit = false;
            UICancel = false;
            UIClick = false;
            UISkip = false;
        }

        /// <summary>
        /// Clears all continuous gameplay input.
        ///
        /// Useful when switching away from gameplay input so the player does not
        /// keep moving from stale input values.
        /// </summary>
        public void ClearGameplayInput()
        {
            Move = Vector2.zero;
            Look = Vector2.zero;
            Sprint = false;
            Pause = false;
            jumpQueued = false;

            ClearInteractInput();
        }

        /// <summary>
        /// Clears all continuous UI input.
        ///
        /// Useful when switching away from UI input so menus or cutscenes do not
        /// keep reading stale input values.
        /// </summary>
        public void ClearUIInput()
        {
            UINavigate = Vector2.zero;
            UIPoint = Vector2.zero;
            UIScrollWheel = Vector2.zero;
            UISubmit = false;
            UICancel = false;
            UIClick = false;
            UISkip = false;
        }

        /// <summary>
        /// Clears all stored gameplay and UI input.
        ///
        /// Useful during scene transitions, cutscene starts, cutscene ends,
        /// pause transitions, or game-over transitions.
        /// </summary>
        public void ClearAllInput()
        {
            ClearGameplayInput();
            ClearUIInput();
        }
    }
}