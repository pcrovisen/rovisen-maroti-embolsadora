using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ModbusServer.StateMachine
{
    /// <summary>
    /// Base of every cooperative state machine in the service. Subclasses declare
    /// their own state enum and pass it as <typeparamref name="TState"/>:
    ///
    /// <code>internal class PalletEntry : Machine&lt;PalletEntry.States&gt;</code>
    ///
    /// The state used to be typed `object`, which meant every read was a
    /// `(States)State` cast, every transition boxed, and nothing checked that a
    /// machine only ever moved to one of its own states. The enum was found by
    /// reflecting for a nested type literally named `States`, so renaming it
    /// crashed the service at startup — that was a documented hard rule purely
    /// because the base class could not see the type. It can now.
    /// </summary>
    internal abstract class Machine<TState> where TState : struct, Enum
    {
        // How long to wait before re-launching a task that faulted, so a device
        // that is down is not hammered once per step.
        protected const int RetryDelayMs = 100;

        public string Name { get; protected set; }
        public TState State { get; protected set; }

        protected Stopwatch StateTime = Stopwatch.StartNew();

        private readonly TState initState;

        // Deliberately not called `Log`: subclasses declare their own
        // `static readonly ILog Log` and an inherited member with the same name
        // would hide it (CS0108).
        private readonly ILog machineLog;

        protected Machine(TState initState, string identifier = "")
        {
            this.initState = initState;
            Name = this.GetType().Name + identifier;
            machineLog = LogManager.GetLogger(this.GetType());
            Status.Instance.StateMachine.Machines[Name] = StateValue(initState);
            Status.Instance.StateMachine.MachinesStates[Name] = new Dictionary<int, string>();
            Reset();
            PublishStateNames();
        }

        public virtual void Reset()
        {
            State = initState;
            StateTime.Restart();
            // Same publication as NextState: without it the HMIs kept showing the
            // state the machine was in before the reset until its next transition.
            Status.Instance.StateMachine.Machines[Name] = StateValue(State);
        }

        protected void NextState(TState nextState)
        {
            State = nextState;
            StateTime.Restart();
            Status.Instance.StateMachine.Machines[Name] = StateValue(State);
        }

        public void Remove()
        {
            Status.Instance.StateMachine.Machines.Remove(Name);
            Status.Instance.StateMachine.MachinesStates.Remove(Name);
        }

        public abstract void Step();

        // Publishes value -> name so the HMIs can show "Waiting" instead of 0.
        // Keyed by the enum's numeric value, which is how the HMI looks it up
        // (States[machine][MachineState[machine]]); the previous version keyed by
        // declaration position, identical only while every enum stays contiguous
        // and zero-based.
        private void PublishStateNames()
        {
            Status.Instance.StateMachine.Updated = true;
            var names = Status.Instance.StateMachine.MachinesStates[Name];
            foreach (TState value in Enum.GetValues(typeof(TState)))
            {
                names[StateValue(value)] = value.ToString();
            }
        }

        private static int StateValue(TState state)
        {
            return Convert.ToInt32(state);
        }

        // The retry idiom every machine uses to poll the I/O it started: returns
        // true once `task` has finished successfully, re-launches it through
        // `restart` after RetryDelayMs if it faulted, and returns false while it is
        // still running. The caller does nothing on false and stays in the state.
        //
        // Written by hand at ~20 sites before this, several of which forgot the
        // IsFaulted check and read .Result on a faulted task — which throws
        // AggregateException out of Step() and, from the main loop, takes the
        // service down (MainProcess exits and the service manager restarts it).
        protected bool TryComplete(ref Task task, Func<Task> restart, string what)
        {
            if (task == null || !task.IsCompleted)
            {
                return false;
            }
            if (task.IsFaulted || task.IsCanceled)
            {
                if (ShouldRetry(task, what))
                {
                    task = restart();
                }
                return false;
            }
            return true;
        }

        // Same, for a task that carries a value.
        protected bool TryComplete<T>(ref Task<T> task, Func<Task<T>> restart, string what, out T result)
        {
            result = default(T);
            if (task == null || !task.IsCompleted)
            {
                return false;
            }
            if (task.IsFaulted || task.IsCanceled)
            {
                if (ShouldRetry(task, what))
                {
                    task = restart();
                }
                return false;
            }
            result = task.Result;
            return true;
        }

        // Logs the real exception — the hand-written copies mostly logged only a
        // fixed message — and restarts the back-off clock.
        private bool ShouldRetry(Task faulted, string what)
        {
            if (StateTime.ElapsedMilliseconds <= RetryDelayMs)
            {
                return false;
            }
            machineLog.ErrorFormat("{0} failed. Retrying. Error: {1}",
                what, faulted.Exception?.GetBaseException().Message ?? "cancelled");
            StateTime.Restart();
            return true;
        }
    }
}
