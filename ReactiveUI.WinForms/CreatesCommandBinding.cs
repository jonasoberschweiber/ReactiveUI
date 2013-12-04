using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace ReactiveUI.WinForms {
    public interface ICreatesCommandBinding
    {
        /// <summary>
        /// Returns a positive integer when this class supports 
        /// BindCommandToObject for this particular Type. If the method
        /// isn't supported at all, return a non-positive integer. When multiple
        /// implementations return a positive value, the host will use the one
        /// which returns the highest value. When in doubt, return '2' or '0'
        /// </summary>
        /// <param name="type">The type to query for.</param>
        /// <param name="hasEventTarget">If true, the host intends to use a custom
        /// event target.</param>
        /// <returns>A positive integer if BCTO is supported, zero or a negative
        /// value otherwise</returns>
        int GetAffinityForObject(Type type, bool hasEventTarget);

        /// <summary>
        /// Bind an ICommand to a UI object, in the "default" way. The meaning 
        /// of this is dependent on the implementation. Implement this if you
        /// have a new type of UI control that doesn't have 
        /// Command/CommandParameter like WPF or has a non-standard event name
        /// for "Invoke".
        /// </summary>
        /// <param name="command">The command to bind</param>
        /// <param name="target">The target object, usually a UI control of 
        /// some kind</param>
        /// <param name="commandParameter">An IObservable source whose latest 
        /// value will be passed as the command parameter to the command. Hosts
        /// will always pass a valid IObservable, but this may be 
        /// Observable.Empty</param>
        /// <returns>An IDisposable which will disconnect the binding when 
        /// disposed.</returns>
        IDisposable BindCommandToObject(ICommand command, object target, IObservable<object> commandParameter);

        /// <summary>
        /// Bind an ICommand to a UI object to a specific event. This event may
        /// be a standard .NET event, or it could be an event derived in another
        /// manner (i.e. in MonoTouch).
        /// </summary>
        /// <param name="command">The command to bind</param>
        /// <param name="target">The target object, usually a UI control of 
        /// some kind</param>
        /// <param name="commandParameter">An IObservable source whose latest 
        /// value will be passed as the command parameter to the command. Hosts
        /// will always pass a valid IObservable, but this may be 
        /// Observable.Empty</param>
        /// <param name="eventName">The event to bind to.</param>
        /// <returns></returns>
        /// <returns>An IDisposable which will disconnect the binding when 
        /// disposed.</returns>
        IDisposable BindCommandToObject<TEventArgs>(ICommand command, object target, IObservable<object> commandParameter, string eventName) where TEventArgs : EventArgs;
    }

    public class CreatesCommandBindingWithEnabledViaEvent : ICreatesCommandBinding {
        static readonly List<Tuple<string, Type>> eventsToBind = new List<Tuple<string, Type>>() {
            Tuple.Create("Click", typeof(EventArgs))
        };

        static readonly List<string> enabledFields = new List<string>() {
            "Enabled"
        };

        public int GetAffinityForObject(Type type, bool hasEventTarget) {
            if (!(typeof(Control).IsAssignableFrom(type))) return 0;

            if (hasEventTarget) return 5;

            return eventsToBind.SelectMany(t => enabledFields.Select(f => Tuple.Create(t.Item1, t.Item2, f))).Any(x => {
                var ei = type.GetEvent(x.Item1, BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance);
                var ef = type.GetProperty(x.Item3, BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance);
                return ei != null && ef != null;
            }) ? 4 : 0;
        }

        public IDisposable BindCommandToObject(ICommand command, object target, IObservable<object> commandParameter) {
            const BindingFlags bf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy;

            var type = target.GetType();
            var eventInfo = eventsToBind
                .Select(x => new { EventInfo = type.GetEvent(x.Item1, bf), Args = x.Item2 })
                .FirstOrDefault(x => x.EventInfo != null);

            if (eventInfo == null) return null;

            var mi = GetType().GetMethods().First(x => x.Name == "BindCommandToObject" && x.IsGenericMethod);
            mi = mi.MakeGenericMethod(eventInfo.Args);

            return (IDisposable)mi.Invoke(this, new[] { command, target, commandParameter, eventInfo.EventInfo.Name });
        }

        public IDisposable BindCommandToObject<TEventArgs>(ICommand command, object target, IObservable<object> commandParameter, string eventName)
            where TEventArgs : EventArgs
        {
            var ret = new CompositeDisposable();

            object latestParameter = null;
            bool useEventArgsInstead = false;

            var type = target.GetType();
            var enabledField = enabledFields
                .Select(x => type.GetProperty(x))
                .FirstOrDefault(x => x != null);

            if (enabledField != null) {
                if (!(command is ReactiveCommand) || !(target is Control)) {
                    throw new Exception("Binding to Enabled is only available for ReactiveCommands");
                }
                var rc = command as ReactiveCommand;
                var ctl = target as Control;
                if (ctl.IsHandleCreated) {
                    ctl.Invoke(new MethodInvoker(() => {
                        enabledField.SetValue(target, rc.CanExecute(null), new object[] { });
                    }));
                } else {
                    ctl.HandleCreated += (o, e) => {
                        enabledField.SetValue(target, rc.CanExecute(null), new object[] { });
                    };
                }
                ret.Add(rc.CanExecuteObservable.Subscribe(b => {
                    if (!ctl.IsHandleCreated) // The control/the control's handle might not exist anymore.
                        return;
                    ctl.Invoke(new MethodInvoker(() => {
                        enabledField.SetValue(target, b, new object[] { });
                    }));
                }));
            }

            // NB: This is a bit of a hack - if commandParameter isn't specified,
            // it will default to Observable.Empty. We're going to use termination
            // of the commandParameter as a signal to use EventArgs.
            ret.Add(commandParameter.Subscribe(
                x => latestParameter = x,
                () => useEventArgsInstead = true));

            var evt = Observable.FromEventPattern<TEventArgs>(target, eventName);
            ret.Add(evt.Subscribe(ea => {
                if (command.CanExecute(useEventArgsInstead ? ea : latestParameter)) {
                    command.Execute(useEventArgsInstead ? ea : latestParameter);
                }
            }));

            return ret;
        }
    }

    public class CreatesCommandBindingViaEvent : ICreatesCommandBinding
    {
        // NB: These are in priority order
        static readonly List<Tuple<string, Type>> defaultEventsToBind = new List<Tuple<string, Type>>() {
            Tuple.Create("Click", typeof(EventArgs))
        };

        public int GetAffinityForObject(Type type, bool hasEventTarget)
        {
            if (hasEventTarget) return 5;

            return defaultEventsToBind.Any(x => {
                var ei = type.GetEvent(x.Item1, BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance);
                return ei != null;
            }) ? 3 : 0;
        }

        public IDisposable BindCommandToObject(ICommand command, object target, IObservable<object> commandParameter)
        {
            const BindingFlags bf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy;

            var type = target.GetType();
            var eventInfo = defaultEventsToBind
                .Select(x => new { EventInfo = type.GetEvent(x.Item1, bf), Args = x.Item2 })
                .FirstOrDefault(x => x.EventInfo != null);

            if (eventInfo == null) return null;

            var mi = GetType().GetMethods().First(x => x.Name == "BindCommandToObject" && x.IsGenericMethod);
            mi = mi.MakeGenericMethod(eventInfo.Args);

            return (IDisposable)mi.Invoke(this, new[] {command, target, commandParameter, eventInfo.EventInfo.Name});
        }

        public IDisposable BindCommandToObject<TEventArgs>(ICommand command, object target, IObservable<object> commandParameter, string eventName)
            where TEventArgs : EventArgs
        {
            var ret = new CompositeDisposable();

            object latestParameter = null;
            bool useEventArgsInstead = false;

            // NB: This is a bit of a hack - if commandParameter isn't specified,
            // it will default to Observable.Empty. We're going to use termination
            // of the commandParameter as a signal to use EventArgs.
            ret.Add(commandParameter.Subscribe(
                x => latestParameter = x,
                () => useEventArgsInstead = true));

            var evt = Observable.FromEventPattern<TEventArgs>(target, eventName);
            ret.Add(evt.Subscribe(ea => {
                if (command.CanExecute(useEventArgsInstead ? ea : latestParameter)) {
                    command.Execute(useEventArgsInstead ? ea : latestParameter);
                }
            }));

            return ret;
        }
    }
}
