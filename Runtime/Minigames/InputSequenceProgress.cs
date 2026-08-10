namespace QuietStatic.Toolkit.Minigames
{
    /// <summary>Determines what a wrong input does to an active sequence.</summary>
    public enum WrongInputResponse
    {
        Ignore,
        ResetSequence,
        FailMinigame
    }

    /// <summary>Result produced after an input is submitted to a sequence.</summary>
    public enum InputSequenceResult
    {
        Correct,
        Completed,
        Incorrect,
        Reset,
        Failed
    }

    /// <summary>
    /// Tracks sequence progress independently from Unity input and presentation.
    /// </summary>
    public sealed class InputSequenceProgress
    {
        /// <summary>Current zero-based position in the sequence.</summary>
        public int CurrentIndex { get; private set; }

        /// <summary>Whether the sequence is currently accepting inputs.</summary>
        public bool IsActive { get; private set; }

        /// <summary>Starts or restarts the sequence from its first input.</summary>
        public void Start()
        {
            CurrentIndex = 0;
            IsActive = true;
        }

        /// <summary>Stops the sequence and clears its progress.</summary>
        public void Stop()
        {
            CurrentIndex = 0;
            IsActive = false;
        }

        /// <summary>
        /// Submits whether the received input matches the current step.
        /// </summary>
        public InputSequenceResult Submit(
            bool isCorrect,
            int sequenceLength,
            WrongInputResponse wrongInputResponse)
        {
            if (!IsActive)
            {
                return InputSequenceResult.Incorrect;
            }

            if (!isCorrect)
            {
                return HandleIncorrectInput(wrongInputResponse);
            }

            CurrentIndex++;
            if (CurrentIndex < sequenceLength)
            {
                return InputSequenceResult.Correct;
            }

            IsActive = false;
            return InputSequenceResult.Completed;
        }

        private InputSequenceResult HandleIncorrectInput(WrongInputResponse response)
        {
            switch (response)
            {
                case WrongInputResponse.ResetSequence:
                    CurrentIndex = 0;
                    return InputSequenceResult.Reset;
                case WrongInputResponse.FailMinigame:
                    IsActive = false;
                    return InputSequenceResult.Failed;
                default:
                    return InputSequenceResult.Incorrect;
            }
        }
    }
}
