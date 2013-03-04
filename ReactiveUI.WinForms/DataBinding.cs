using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;

namespace ReactiveUI.WinForms {
    public static class DataBinding {
        public static IDisposable BindData<TView, TViewModel, TProp, TControl>(
                this TView view,
                TViewModel viewModel,
                Expression<Func<TViewModel, TProp>> propertyName,
                Expression<Func<TView, TControl>> controlName)
            where TViewModel : class, INotifyPropertyChanged
            where TView : class, IViewFor<TViewModel> {

            var getter = Reflection.GetValueFetcherForProperty(view.GetType(), 
                Reflection.SimpleExpressionToPropertyName(controlName));
            return internalBindData<TView, TViewModel, TProp>(view, viewModel, propertyName, getter);
        }

        private static IDisposable internalBindData<TView, TViewModel, TProp>(
                TView view,
                TViewModel viewModel,
                Expression<Func<TViewModel, TProp>> propertyName,
                Func<object, object> viewPropGetter)
            where TViewModel : class, INotifyPropertyChanged
            where TView : class, IViewFor<TViewModel> {

            var propName = Reflection.SimpleExpressionToPropertyName(propertyName);
            return CreatesDataBinding.BindPropertyToObject(viewModel, propName, viewPropGetter(view));
        }
    }

    class CreatesDataBinding {
        static readonly MemoizingMRUCache<Type, ICreatesDataBinding> bindDataCache =
            new MemoizingMRUCache<Type, ICreatesDataBinding>((t, _) => {
                return RxApp.GetAllServices<ICreatesDataBinding>()
                    .Aggregate(Tuple.Create(0, (ICreatesDataBinding)null), (acc, x) => {
                        int score = x.GetAffinityForObject(t);
                        return (score > acc.Item1) ? Tuple.Create(score, x) : acc;
                    }).Item2;
            }, 50);

        public static IDisposable BindPropertyToObject(INotifyPropertyChanged source, string propertyName, object target) {
            var binder = default(ICreatesDataBinding);
            var type = target.GetType();

            lock (bindDataCache) {
                binder = bindDataCache.Get(type);
            }

            if (binder == null) {
                throw new Exception(string.Format("Couldn't find a Data Binder for {0}", type.FullName));
            }

            var ret = binder.BindPropertyToObject(source, propertyName, target);
            if (ret == null) {
                throw new Exception(string.Format("Couldn't bind Data Binder for {0}", type.FullName));
            }

            return ret;
        }
    }
}
