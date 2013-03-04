using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Windows.Forms;

namespace ReactiveUI.WinForms {
    public interface ICreatesDataBinding {
        int GetAffinityForObject(Type type);

        IDisposable BindPropertyToObject(INotifyPropertyChanged source, string propertyName, object target);
    }

    public static class CreatesEventDataBinding {
        public static IDisposable BindPropertyToObject(
            INotifyPropertyChanged source, 
            string propertyName, 
            object target,
            Action<object, object> setTargetValue,
            Func<object, IObservable<object>> createTargetObservable,
            Func<object> getInitialValue,
            Func<object, object> getCurrentValue) {

            var propertyObs = source.ObservableForProperty(new string[] { propertyName });
            propertyObs.Subscribe(e => {
                setTargetValue(target, e.Value);
            });
            var targetObs = createTargetObservable(target);
            var propertySetter = source.GetType().GetProperty(propertyName).GetSetMethod();
            return Observable.Return(getInitialValue()).Concat(targetObs).Subscribe(e => {
                propertySetter.Invoke(source, new object[] { getCurrentValue(target) });
            });
        }

        public static IDisposable BindPropertyToObjectWithInvoke(
            INotifyPropertyChanged source,
            string propertyName,
            Control target,
            Action<object, object> setTargetValue,
            Func<object, IObservable<object>> createTargetObservable,
            Func<object> getInitialValue,
            Func<object, object> getCurrentValue) {

            return BindPropertyToObject(source, propertyName, target,
                (t, v) => {
                    var ctrl = t as Control;
                    if (ctrl.IsHandleCreated) {
                        ctrl.Invoke(new MethodInvoker(() => setTargetValue(t, v)));
                    } else {
                        ctrl.HandleCreated += (o, e) => {
                            setTargetValue(o, v);
                        };
                    }
                },
                createTargetObservable,
                getInitialValue,
                getCurrentValue);
        }
    }

    public class CreatesTextBoxDataBinding : ICreatesDataBinding {
        public int GetAffinityForObject(Type type) {
            return type == typeof(TextBox) ? 5 : 0;
        }

        public IDisposable BindPropertyToObject(INotifyPropertyChanged source, string propertyName, object target) {
            return CreatesEventDataBinding.BindPropertyToObjectWithInvoke(source, propertyName, target as Control,
                (t, v) => { (target as TextBox).Text = v as string; },
                t => Observable.FromEventPattern<EventHandler, EventArgs>(h => h.Invoke, h => (t as TextBox).TextChanged += h, h => (t as TextBox).TextChanged -= h),
                () => "",
                t => (t as TextBox).Text);
        }
    }
}
