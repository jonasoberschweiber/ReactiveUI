using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.Serialization;
using System.Text;

namespace ReactiveUI.WinForms {
    [DataContract]
    public class RoutingState : ReactiveObject, IRoutingState {
        [field: IgnoreDataMember]
        bool _rxObjectsSetup = false;

        [IgnoreDataMember]
        ReactiveCollection<IRoutableViewModel> _NavigationStack;

        [DataMember]
        public ReactiveCollection<IRoutableViewModel> NavigationStack {
            get { return _NavigationStack; }
            protected set { _NavigationStack = value; }
        }

        [IgnoreDataMember]
        public IReactiveCommand NavigateBack { get; protected set; }

        [IgnoreDataMember]
        public INavigateCommand Navigate { get; protected set; }

        [IgnoreDataMember]
        public INavigateCommand NavigateAndReset { get; protected set; }

        public RoutingState() {
            _NavigationStack = new ReactiveCollection<IRoutableViewModel>();
            SetupRx();
        }

        [OnDeserialized]
        void SetupRx(StreamingContext sc) {
            SetupRx();
        }

        void SetupRx() {
            if (_rxObjectsSetup) return;

            NavigateBack = new ReactiveCommand(
                NavigationStack.CollectionCountChanged.StartWith(_NavigationStack.Count).Select(x => x > 1));
            NavigateBack.Subscribe(_ =>
                NavigationStack.RemoveAt(NavigationStack.Count - 1));

            Navigate = new NavigationReactiveCommand();
            Navigate.Subscribe(x => {
                var vm = x as IRoutableViewModel;
                if (vm == null)
                    throw new Exception("Navigate must be called on an IRoutableViewModel");
                NavigationStack.Add(vm);
            });

            NavigateAndReset = new NavigationReactiveCommand();
            NavigateAndReset.Subscribe(x => {
                NavigationStack.Clear();
                Navigate.Execute(x);
            });

            _rxObjectsSetup = true;
        }
    }

    class NavigationReactiveCommand : ReactiveCommand, INavigateCommand { }

    public static class RoutingStateMixins {
        public static string GetUrlForCurrentRoute(this IRoutingState This) {
            return "app://" + string.Join("/", This.NavigationStack.Select(x => x.UrlPathSegment));
        }

        public static T FindViewModelInStack<T>(this IRoutingState This)
            where T : IRoutableViewModel {
            return This.NavigationStack.Reverse().OfType<T>().FirstOrDefault();
        }

        public static IRoutableViewModel GetCurrentViewModel(this IRoutingState This) {
            return This.NavigationStack.LastOrDefault();
        }

        public static IObservable<IRoutableViewModel> ViewModelObservable(this IRoutingState This) {
            return This.NavigationStack.CollectionCountChanged
                .Select(_ => This.GetCurrentViewModel())
                .StartWith(This.GetCurrentViewModel());
        }

        public static void Go<T>(this INavigateCommand This, string key = null)
            where T : IRoutableViewModel {
            This.Execute(RxApp.GetService<T>(key));
        }

        public static IReactiveCommand NavigateCommandFor<T>(this IRoutingState This) 
            where T : IRoutableViewModel {
            var ret = new ReactiveCommand(This.Navigate.CanExecuteObservable);
            ret.Select(_ => (IRoutableViewModel)RxApp.GetService<T>()).InvokeCommand(This.Navigate);
            return ret;
        }
    }
}
