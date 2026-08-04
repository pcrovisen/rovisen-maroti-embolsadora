using System;

namespace ModbusServer.StateMachine
{
    /// <summary>
    /// Marks a state that means something has gone wrong and, usually, that an
    /// operator has to act — as opposed to a state that is merely waiting.
    ///
    /// Read by <c>WebHMI/devserver/generate_transitions.mjs</c>, which puts the
    /// flag in <c>transitions.json</c> so the diagnostics page can draw those
    /// states differently. Terminal states need no marker: the generator derives
    /// them from the graph (no outgoing transition).
    ///
    /// Applied to the enum member, not the class:
    /// <code>
    /// public enum States
    /// {
    ///     Waiting,
    ///     [FaultState] Failed,
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class FaultStateAttribute : Attribute
    {
    }
}
