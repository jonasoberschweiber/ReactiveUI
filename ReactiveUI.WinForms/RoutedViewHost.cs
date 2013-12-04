using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Windows.Forms;

namespace ReactiveUI.WinForms {
    public class RoutedViewHost : Control, INotifyPropertyChanged {
        IDisposable _inner = null;

        private IRoutingState _Router;
        public IRoutingState Router {
            get { return _Router; }
            set {
                _Router = value;
                if (PropertyChanged != null) {
                    PropertyChanged(this, new PropertyChangedEventArgs("Router"));
                }
            }
        }

        private Control _DefaultContent;
        public Control DefaultContent {
            get { return _DefaultContent; }
            set { _DefaultContent = value; }
        }

        public Control Content {
            get {
                if (Controls.Count == 0)
                    return null;
                return Controls[0];
            }
            set {
                if (this.IsHandleCreated) {
                    this.Invoke(new MethodInvoker(() => {
                        Controls.Clear();
                        if (value != null) {
                            value.Dock = DockStyle.Fill;
                            Controls.Add(value);
                        }
                    }));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void EnsureHandleCreated(Action a) {
            if (this.IsHandleCreated)
                a();
            else
                this.HandleCreated += new EventHandler((_, __) => a());
        }

        public RoutedViewHost() {
            this.WhenAny(x => x.Router.NavigationStack, x => x.Value)
                .SelectMany(x => x.CollectionCountChanged.StartWith(x.Count).Select(_ => x.LastOrDefault()))
                .Subscribe(vm => {
                    if (vm == null) {
                        Content = DefaultContent;
                        return;
                    }


                    // Make sure that the view is created on the UI thread.
                    EnsureHandleCreated(() => {
                        this.Invoke(new MethodInvoker(() => {
                            var view = RxRouting.ResolveView(vm);
                            view.ViewModel = vm;
                            Content = (Control)view;
                        }));
                    });
                }, ex => RxApp.DefaultExceptionHandler.OnNext(ex));
        }
    }
}
