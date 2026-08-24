using System;
using UnityEngine;

namespace QuietStatic
{
    /// <summary>
    /// Marks a serialized command-channel field that must be assigned for the component
    /// to participate in the project's canonical cross-scene command pipeline.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class RequiredCommandChannelAttribute : PropertyAttribute
    {
        /// <summary>Creates a required channel marker for a caller or receiver field.</summary>
        /// <param name="isReceiver">
        /// True when the owning component is the persistent adapter that consumes this channel.
        /// </param>
        public RequiredCommandChannelAttribute(bool isReceiver = false)
        {
            IsReceiver = isReceiver;
        }

        /// <summary>Whether the owning component receives commands from the assigned channel.</summary>
        public bool IsReceiver { get; }
    }
}
