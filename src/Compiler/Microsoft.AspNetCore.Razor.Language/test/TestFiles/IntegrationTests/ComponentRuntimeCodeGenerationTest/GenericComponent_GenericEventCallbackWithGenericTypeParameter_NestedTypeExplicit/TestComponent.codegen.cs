﻿// <auto-generated/>
#pragma warning disable 1591
namespace Test
{
    #line default
    using global::System;
    using global::System.Collections.Generic;
    using global::System.Linq;
    using global::System.Threading.Tasks;
    using global::Microsoft.AspNetCore.Components;
#nullable restore
#line 2 "x:\dir\subdir\Test\TestComponent.cshtml"
using Test;

#line default
#line hidden
#nullable disable
    #nullable restore
    public partial class TestComponent<
#nullable restore
#line 1 "x:\dir\subdir\Test\TestComponent.cshtml"
TChild

#line default
#line hidden
#nullable disable
    > : global::Microsoft.AspNetCore.Components.ComponentBase
    #nullable disable
    {
        #pragma warning disable 1998
        protected override void BuildRenderTree(global::Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder __builder)
        {
            __builder.OpenComponent<global::Test.MyComponent<TChild>>(0);
            __builder.AddComponentParameter(1, "MyEvent", global::Microsoft.AspNetCore.Components.CompilerServices.RuntimeHelpers.TypeCheck<global::Microsoft.AspNetCore.Components.EventCallback<TChild>>(global::Microsoft.AspNetCore.Components.EventCallback.Factory.Create<TChild>(this, 
#nullable restore
#line 3 "x:\dir\subdir\Test\TestComponent.cshtml"
                                     (TChild x) => {}

#line default
#line hidden
#nullable disable
            )));
            __builder.CloseComponent();
        }
        #pragma warning restore 1998
    }
}
#pragma warning restore 1591
