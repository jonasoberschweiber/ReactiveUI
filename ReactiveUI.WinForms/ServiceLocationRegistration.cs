using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ReactiveUI.WinForms {
    public class ServiceLocationRegistration : IWantsToRegisterStuff {

        public void Register() {
            RxApp.Register(typeof(CommandBinderImplementation), typeof(ICommandBinderImplementation));
            RxApp.Register(typeof(CreatesCommandBindingViaEvent), typeof(ICreatesCommandBinding));
            RxApp.Register(typeof(CreatesCommandBindingWithEnabledViaEvent), typeof(ICreatesCommandBinding));
        }

    }
}
