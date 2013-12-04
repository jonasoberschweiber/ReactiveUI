using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using System.Text;

namespace ReactiveUI.WinForms
{
    public interface ICommand 
    {
        bool CanExecute(object p);
        void Execute(object p);
        event EventHandler CanExecuteChanged;
    }

    public interface IReactiveCommand : ICommand, IObservable<object>, IHandleObservableErrors
    {
        IObservable<bool> CanExecuteObservable { get; }
    }

    public interface IReactiveAsyncCommand : IReactiveCommand 
    {
        IObservable<int> ItemsInflight { get; }

        ISubject<Unit> AsyncStartedNotification { get; }

        ISubject<Unit> AsyncCompletedNotification { get; }
    }

    public interface INavigateCommand : IReactiveCommand { }

    public interface IRoutingState : IReactiveNotifyPropertyChanged {
        ReactiveCollection<IRoutableViewModel> NavigationStack { get; }

        IReactiveCommand NavigateBack { get; }

        INavigateCommand Navigate { get; }

        INavigateCommand NavigateAndReset { get; }
    }

    public interface IRoutableViewModel : IReactiveNotifyPropertyChanged {
        string UrlPathSegment { get; }

        IScreen HostScreen { get; }
    }

    public interface IScreen {
        IRoutingState Router { get; }
    }

    public class ViewContractAttribute : Attribute {
        public string Contract { get; set; }
    }

    public static class ObservableUtils {
        public static IConnectableObservable<T> PermaRef<T>(this IConnectableObservable<T> This) {
            This.Connect();
            return This;
        }
    }
}
