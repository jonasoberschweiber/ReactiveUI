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
}
