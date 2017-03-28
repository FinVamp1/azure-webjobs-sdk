// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.Cancellation;
using Microsoft.Azure.WebJobs.Host.Bindings.Data;
using Microsoft.Azure.WebJobs.Host.Bindings.Runtime;
using Microsoft.Azure.WebJobs.Host.Bindings.StorageAccount;
using Microsoft.Azure.WebJobs.Host.Blobs;
using Microsoft.Azure.WebJobs.Host.Blobs.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Queues;
using Microsoft.Azure.WebJobs.Host.Queues.Bindings;
using Microsoft.Azure.WebJobs.Host.Tables;

namespace Microsoft.Azure.WebJobs.Host.Indexers
{
    // Extension representing  builtin types. 
    internal class DefaultBindingProvider : IExtensionConfigProvider
    {
        // $$$ for full consistency, this should be in the Initialize method. 
        public static IBindingProvider Create(
            INameResolver nameResolver,
            IConverterManager converterManager,
            IStorageAccountProvider storageAccountProvider,
            IExtensionTypeLocator extensionTypeLocator,
            IContextGetter<IMessageEnqueuedWatcher> messageEnqueuedWatcherGetter,
            IContextGetter<IBlobWrittenWatcher> blobWrittenWatcherGetter,
            IExtensionRegistry extensions)            
        {
            List<IBindingProvider> innerProviders = new List<IBindingProvider>();

            if (converterManager == null)
            {
                converterManager = new ConverterManager();
            }

            // Wire up new bindings 
            var ruleQueueOutput = QueueBindingProvider.Build(storageAccountProvider, messageEnqueuedWatcherGetter, nameResolver, converterManager);
            innerProviders.Add(ruleQueueOutput);

            innerProviders.Add(new BlobAttributeBindingProvider(nameResolver, storageAccountProvider, extensionTypeLocator, blobWrittenWatcherGetter));
            innerProviders.Add(TableAttributeBindingProvider.Build(nameResolver, converterManager, storageAccountProvider, extensions));

            // add any registered extension binding providers
            foreach (IBindingProvider provider in extensions.GetExtensions(typeof(IBindingProvider)))
            {
                innerProviders.Add(provider);
            }

            innerProviders.Add(new CloudStorageAccountBindingProvider(storageAccountProvider));
            innerProviders.Add(new CancellationTokenBindingProvider());

            // The TraceWriter binder handles all remaining TraceWriter/TextWriter parameters. It must come after the
            // Blob binding provider; otherwise bindings like Do([Blob("a/b")] TextWriter blob) wouldn't work.
            innerProviders.Add(new TraceWriterBindingProvider());

            ContextAccessor<IBindingProvider> bindingProviderAccessor = new ContextAccessor<IBindingProvider>();
            innerProviders.Add(new RuntimeBindingProvider(bindingProviderAccessor));
            innerProviders.Add(new DataBindingProvider());

            IBindingProvider bindingProvider = new CompositeBindingProvider(innerProviders);
            bindingProviderAccessor.SetValue(bindingProvider);
            return bindingProvider;
        }

        // $$$
        public void Initialize(ExtensionConfigContext context)
        {
            throw new NotImplementedException();
        }
#if false
        // $$$
        protected internal Task InitializeAsync(JobHostConfiguration config, JObject hostMetadata)
        {
            if (hostMetadata == null)
            {
                return Task.FromResult(0);
            }
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            // $$$ We should be albe to use some reflection to collapse this. 
            // $$$ Copied from Script c:\dev\afunc\script2\src\webjobs.script\binding\webjobscorescriptbindingprovider.cs
            JObject configSection = (JObject)hostMetadata["queues"];
            JToken value = null;
            if (configSection != null)
            {
                if (configSection.TryGetValue("maxPollingInterval", out value))
                {
                    config.Queues.MaxPollingInterval = TimeSpan.FromMilliseconds((int)value);
                }
                if (configSection.TryGetValue("batchSize", out value))
                {
                    config.Queues.BatchSize = (int)value;
                }
                if (configSection.TryGetValue("maxDequeueCount", out value))
                {
                    config.Queues.MaxDequeueCount = (int)value;
                }
                if (configSection.TryGetValue("newBatchThreshold", out value))
                {
                    config.Queues.NewBatchThreshold = (int)value;
                }
                if (configSection.TryGetValue("visibilityTimeout", out value))
                {
                    config.Queues.VisibilityTimeout = TimeSpan.Parse((string)value, CultureInfo.InvariantCulture);
                }
            }

            // Apply Blobs configuration
            config.Blobs.CentralizedPoisonQueue = true;   // TEMP : In the next release we'll remove this and accept the core SDK default
            configSection = (JObject)hostMetadata["blobs"];
            value = null;
            if (configSection != null)
            {
                if (configSection.TryGetValue("centralizedPoisonQueue", out value))
                {
                    config.Blobs.CentralizedPoisonQueue = (bool)value;
                }
            }

            return Task.FromResult(0);
        }
#endif
    }
}
