// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Triggers;
using static Microsoft.Azure.WebJobs.Host.Bindings.BindingFactory;

namespace Microsoft.Azure.WebJobs.Host.Config
{
    /// <summary>
    /// Helpers for adding binding rules to a given attribute.
    /// </summary>
    /// <typeparam name="TAttribute"></typeparam>
    public class FluentBindingRule<TAttribute>
        where TAttribute : Attribute
    {
        private readonly JobHostConfiguration _parent;

        private List<IBindingProvider> _binders = new List<IBindingProvider>();

        internal FluentBindingRule(JobHostConfiguration parent)
        {
            _parent = parent;
        }

        /// <summary>
        /// Bind an attribute to the given input, using the converter manager. 
        /// </summary>
        /// <typeparam name="TType"></typeparam>
        /// <param name="builderInstance"></param>
        /// <returns></returns>
        public FluentBindingRule<TAttribute> BindToInput<TType>(IConverter<TAttribute, TType> builderInstance)
        {
            var bf = _parent.BindingFactory;
            var rule = bf.BindToInput<TAttribute, TType>(builderInstance);
            _binders.Add(rule);
            return this;
        }

        /// <summary>
        /// Bind an attribute to the given input, using the converter manager. 
        /// </summary>
        /// <typeparam name="TType"></typeparam>
        /// <param name="builderInstance"></param>
        /// <returns></returns>
        public FluentBindingRule<TAttribute> BindToInput<TType>(IAsyncConverter<TAttribute, TType> builderInstance)
        {
            var bf = _parent.BindingFactory;

            var pm = PatternMatcher.New(builderInstance);
            var rule = new BindToInputBindingProvider<TAttribute, TType>(bf.NameResolver, bf.ConverterManager, pm);
            _binders.Add(rule);
            return this;
        }

        /// <summary>
        /// General rule for binding to an generic input type for a given attribute. 
        /// </summary>
        /// <typeparam name="TType">The user type must be compatible with this type for the binding to apply.</typeparam>
        /// <param name="builderType">A that implements IConverter for the target parameter. 
        /// This will get instantiated with the appropriate generic args to perform the builder rule.</param>
        /// <param name="constructorArgs">constructor arguments to pass to the typeBuilder instantiation. This can be used 
        /// to flow state (like configuration, secrets, etc) from the configuration to the specific binding</param>
        /// <returns>A binding rule.</returns>
        public FluentBindingRule<TAttribute> BindToInput<TType>(
            Type builderType,
            params object[] constructorArgs)
        {
            var bf = _parent.BindingFactory;
            var rule = bf.BindToInput<TAttribute, TType>(builderType, constructorArgs);
            _binders.Add(rule);
            return this;
        }

        /// <summary>
        /// Bind an attribute to the given input, using the supplied delegate to build the input from an resolved 
        /// instance of the attribute. 
        /// </summary>
        /// <typeparam name="TType"></typeparam>
        /// <param name="builder"></param>
        /// <returns></returns>
        public FluentBindingRule<TAttribute> BindToInput<TType>(Func<TAttribute, TType> builder)
        {
            var builderInstance = new DelegateConverterBuilder<TAttribute, TType> { BuildFromAttribute = builder };
            return this.BindToInput<TType>(builderInstance);
        }

        /// <summary>
        /// Add a general binder.
        /// </summary>
        /// <param name="binder"></param>
        /// <returns></returns>
        public FluentBindingRule<TAttribute> Bind(IBindingProvider binder)
        {
            _binders.Add(binder);
            return this;
        }

        /// <summary>
        /// Setup a trigger binding for this attribute
        /// </summary>
        /// <param name="trigger"></param>
        public void BindToTrigger(ITriggerBindingProvider trigger)
        {
            if (_binders.Count > 0)
            {
                throw new InvalidOperationException($"The same attribute can't be bound to trigger and non-trigger bindings");
            }
            IExtensionRegistry extensions = _parent.GetService<IExtensionRegistry>();
            extensions.RegisterExtension<ITriggerBindingProvider>(trigger);
        }

        // Called by infrastructure after the extension is invoked.
        // This applies all changes accumulated on the fluent object. 
        internal void Flush()
        {
            IExtensionRegistry extensions = _parent.GetService<IExtensionRegistry>();
            extensions.RegisterBindingRules<TAttribute>(_binders.ToArray());
            _binders.Clear();
        }
    }
}
