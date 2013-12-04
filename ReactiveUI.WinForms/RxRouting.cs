using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;

namespace ReactiveUI.WinForms {
    public static class RxRouting {
        public static Func<string, string> ViewModelToViewFunc { get; set; }

        static RxRouting() {
            ViewModelToViewFunc = (vm) => InterfaceifyTypeName(vm.Replace("ViewModel", "View"));
        }

        public static IViewFor ResolveView<T>(T viewModel)
            where T : class {

            // Given IFooBarViewModel (whose name we derive from T), we'll look
            // for a few things:
            // * IFooBarView that implements IViewFor
            // * IViewFor<IFooBarViewModel>
            // * IViewFor<FooBarViewModel>
            var attrs = viewModel.GetType().GetCustomAttributes(typeof(ViewContractAttribute), true);
            string key = null;

            if (attrs.Any()) {
                key = ((ViewContractAttribute)attrs.First()).Contract;
            }

            var typeToFind = ViewModelToViewFunc(viewModel.GetType().AssemblyQualifiedName);
            try {
                var type = Reflection.ReallyFindType(typeToFind, false);
                if (type != null) {
                    var ret = RxApp.GetService(type, key) as IViewFor;
                    if (ret != null) return ret;
                }
            } catch (Exception ex) {
                LogHost.Default.DebugException("Couldn't instantiate " + typeToFind, ex);
            }

            var viewType = typeof(IViewFor<>);

            try {
                var ifn = InterfaceifyTypeName(viewModel.GetType().AssemblyQualifiedName);
                var type = Reflection.ReallyFindType(ifn, false);

                if (type != null) {
                    var ret = RxApp.GetService(viewType.MakeGenericType(type), key) as IViewFor;
                    if (ret != null) return ret;
                }
            } catch (Exception ex) {
                LogHost.Default.DebugException("Couldn't instantiate View via pure interface type", ex);
            }

            return (IViewFor)RxApp.GetService(viewType.MakeGenericType(viewModel.GetType()), key);
        }

        static string InterfaceifyTypeName(string typeName) {
            var typeVsAssembly = typeName.Split(',');
            var parts = typeVsAssembly[0].Split('.');
            parts[parts.Length - 1] = "I" + parts[parts.Length - 1];

            var newType = string.Join(".", parts, 0, parts.Length);
            return newType + "," + string.Join(",", typeVsAssembly.Skip(1));
        }
    }

    public static class RoutableViewModelMixin {
        public static IObservable<Unit> NavigateToMe(this IRoutableViewModel This) {
            return This.HostScreen.Router.ViewModelObservable()
                .Where(x => x == This)
                .Select(_ => Unit.Default);
        }

        public static IObservable<Unit> NavigatedFromMe(this IRoutableViewModel This) {
            return This.HostScreen.Router.ViewModelObservable()
                .Where(x => x != This)
                .Select(_ => Unit.Default);
        }

        public static IDisposable WhenNavigatedTo(this IRoutableViewModel This, Func<IDisposable> onNavigatedTo) {
            IDisposable inner = null;
            var router = This.HostScreen.Router;
            return router.NavigationStack.CollectionCountChanged.Subscribe(_ => {
                if (router.GetCurrentViewModel() == This) {
                    if (inner != null) inner.Dispose();
                    inner = onNavigatedTo();
                } else {
                    if (inner != null) {
                        inner.Dispose();
                        inner = null;
                    }
                }
            });
        }
    }
}
