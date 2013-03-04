using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;

namespace ReactiveUI.WinForms {
    public static class EventHelper {
        public static void SubscribeOnce<TObject, TDelegate, TEventArgs>(object obj, string evtName, Action<TEventArgs> handler)
            where TEventArgs : EventArgs {

            var obs = Observable.FromEventPattern<TDelegate, TEventArgs>(obj, "HandleCreated");
            obs.Take(1).Subscribe(e => {
                handler(e.EventArgs);
            });
        }
    }
}
