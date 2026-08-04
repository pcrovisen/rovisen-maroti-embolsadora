using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ModbusServer.StateMachine
{
    internal abstract class Machine
    {
        // How long to wait before re-launching a task that faulted, so a device
        // that is down is not hammered once per step.
        protected const int RetryDelayMs = 100;

        public string Name { get; protected set; }
        public object State { get; protected set; }

        protected Stopwatch StateTime = Stopwatch.StartNew();

        private readonly object initState;

        // Deliberately not called `Log`: subclasses declare their own
        // `static readonly ILog Log` and an inherited member with the same name
        // would hide it (CS0108).
        private readonly ILog machineLog;

        protected Machine(object initState, string identifier = "")
        {
            this.initState = initState;
            Name = this.GetType().Name + identifier;
            machineLog = LogManager.GetLogger(this.GetType());
            Status.Instance.StateMachine.Machines[Name] = initState;
            Status.Instance.StateMachine.MachinesStates[Name] = new Dictionary<int, string>();
            Reset();
            GetStatesEnum();
        }

        public virtual void Reset()
        {
            State = initState;
            StateTime.Restart();
            // Same publication as NextState: without it the HMIs kept showing the
            // state the machine was in before the reset until its next transition.
            Status.Instance.StateMachine.Machines[Name] = State;
        }

        protected void NextState(object nextState)
        {
            State = nextState;
            StateTime.Restart();
            Status.Instance.StateMachine.Machines[Name] = State;
        }

        public void Remove()
        {
            Status.Instance.StateMachine.Machines.Remove(Name);
            Status.Instance.StateMachine.MachinesStates.Remove(Name);
        }

        public abstract void Step();

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

        public void GetStatesEnum()
        {
            Status.Instance.StateMachine.Updated = true;
            var res = this.GetType().GetNestedType("States", BindingFlags.NonPublic | BindingFlags.Public);

            int i = 0;
            foreach (object val in Enum.GetValues(res))
            {
                Status.Instance.StateMachine.MachinesStates[Name][i++] = val.ToString();
            }
        }
    }
}
