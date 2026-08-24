using System;

namespace QuietStatic.Toolkit.SceneFlow
{
    /// <summary>Reason a scene transition did not complete successfully.</summary>
    public enum SceneTransitionFailure
    {
        /// <summary>No terminal result has been produced.</summary>
        Unknown,

        /// <summary>The transition completed successfully.</summary>
        None,

        EmptyTarget,
        AlreadyTransitioning,
        LoadFailed,
        ActivationFailed,
    }

    /// <summary>Terminal result produced for every scene-transition request.</summary>
    public readonly struct SceneTransitionResult
    {
        /// <summary>Creates an uncorrelated transition result.</summary>
        public SceneTransitionResult(
            string destination,
            SceneTransitionFailure failure,
            string message = "")
            : this(destination, failure, message, null)
        {
        }

        /// <summary>Creates a transition result correlated to its originating request.</summary>
        public SceneTransitionResult(
            string destination,
            SceneTransitionFailure failure,
            string message,
            SceneTransitionRequest request)
        {
            Destination = destination ?? string.Empty;
            Failure = failure;
            Message = message ?? string.Empty;
            Request = request;
        }

        /// <summary>
        /// Exact request that produced this result, or null for legacy and
        /// manually constructed uncorrelated results.
        /// </summary>
        public SceneTransitionRequest Request { get; }

        /// <summary>Requested destination scene name.</summary>
        public string Destination { get; }

        /// <summary>
        /// Failure category, <see cref="SceneTransitionFailure.None"/> after
        /// success, or <see cref="SceneTransitionFailure.Unknown"/> when the
        /// result has not been initialized.
        /// </summary>
        public SceneTransitionFailure Failure { get; }

        /// <summary>Human-readable failure detail, empty after success.</summary>
        public string Message { get; }

        /// <summary>Whether the destination became the active scene.</summary>
        public bool Succeeded => Failure == SceneTransitionFailure.None;

        /// <summary>Creates a successful result.</summary>
        public static SceneTransitionResult Success(string destination) =>
            new(destination, SceneTransitionFailure.None);

        /// <summary>Creates a successful result correlated to its request.</summary>
        public static SceneTransitionResult Success(
            string destination,
            SceneTransitionRequest request) =>
            new(destination, SceneTransitionFailure.None, string.Empty, request);

        /// <summary>Creates a failed result.</summary>
        public static SceneTransitionResult Failed(
            string destination,
            SceneTransitionFailure failure,
            string message) =>
            new(destination, failure, message);

        /// <summary>Creates a failed result correlated to its request.</summary>
        public static SceneTransitionResult Failed(
            string destination,
            SceneTransitionFailure failure,
            string message,
            SceneTransitionRequest request) =>
            new(destination, failure, message, request);
    }
}
