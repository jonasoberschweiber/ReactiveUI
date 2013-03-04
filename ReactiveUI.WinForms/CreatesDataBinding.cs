using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Windows.Forms;

namespace ReactiveUI.WinForms {
    public interface ICreatesDataBinding {
        int GetAffinityForObject(Type type, string targetPropertyName);

        IDisposable BindPropertyToObject(INotifyPropertyChanged source, string propertyName, object target, string targetPropertyName);
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

    public class CreatesWinFormsDataBinding : ICreatesDataBinding {
        public int GetAffinityForObject(Type type, string targetPropertyName) {
            return typeof(Control).IsAssignableFrom(type) ? 2 : 0;
        }

        public IDisposable BindPropertyToObject(INotifyPropertyChanged source, string propertyName, object target, string targetPropertyName) {
            var ctl = target as Control;
            var binding = ctl.DataBindings.Add(targetPropertyName, source, propertyName);
            return Disposable.Create(() => {
                ctl.DataBindings.Remove(binding);
            });
        }
    }

    public class CreatesTextBoxDataBinding : ICreatesDataBinding {
        public int GetAffinityForObject(Type type, string targetPropertyName) {
            return (type == typeof(TextBox) && targetPropertyName == "Text") ? 5 : 0;
        }

        public IDisposable BindPropertyToObject(INotifyPropertyChanged source, string propertyName, object target, string targetPropertyName) {
            return CreatesEventDataBinding.BindPropertyToObjectWithInvoke(source, propertyName, target as Control,
                (t, v) => { (target as TextBox).Text = v as string; },
                t => Observable.FromEventPattern<EventHandler, EventArgs>(h => h.Invoke, h => (t as TextBox).TextChanged += h, h => (t as TextBox).TextChanged -= h),
                () => "",
                t => (t as TextBox).Text);
        }
    }
}
