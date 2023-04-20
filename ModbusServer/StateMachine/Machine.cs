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
        public string Name { get; protected set; }
        public object State { get; protected set; }

        protected Stopwatch StateTime = Stopwatch.StartNew();

        private readonly object initState;

        protected Machine(object initState, string identifier = "")
        {
            this.initState = initState;
            Name = this.GetType().Name + identifier;
            Status.Instance.StateMachine.Machines[Name] = initState;
            Status.Instance.StateMachine.MachinesStates[Name] = new Dictionary<int, string>();
            Reset();
            GetStatesEnum();
        }

        public virtual void Reset()
        {
            State = initState;
            StateTime.Restart();
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
