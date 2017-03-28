// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Host.Config
{
    /// <summary>
    /// Context object passed to <see cref="IExtensionConfigProvider"/> instances when
    /// they are initialized.
    /// </summary>
    public class ExtensionConfigContext
    {
        // List of actions to flush from the fluent configuration. 
        private List<Action> _updates = new List<Action>();

        /// <summary>
        /// Gets or sets the <see cref="JobHostConfiguration"/>.
        /// </summary>
        public JobHostConfiguration Config { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="TraceWriter"/>.
        /// </summary>
        public TraceWriter Trace { get; set; }

        /// <summary>
        /// Expose converter manager for adding custom converters in the bindings. 
        /// </summary>
        public IConverterManager Converters
        {
            get { return this.Config.ConverterManager; }
        }

        /// <summary>
        /// Add a binding rule for the given attribute
        /// </summary>
        /// <typeparam name="TAttribute"></typeparam>
        /// <returns></returns>
        public FluentBindingRule<TAttribute> AddBindingRule<TAttribute>() where TAttribute : Attribute
        {
            var fluent = new FluentBindingRule<TAttribute>(this.Config);
            _updates.Add(fluent.Flush);
            return fluent;
        }

        internal void Flush()
        {
            foreach (var func in _updates)
            {
                func();
            }
            _updates.Clear();
        }
    }    
}
