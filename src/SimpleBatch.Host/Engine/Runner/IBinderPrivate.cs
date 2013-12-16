﻿

namespace Microsoft.WindowsAzure.Jobs
{
    // Private extensions to binder, used internally and not exposed to public simple batch model binders. 
    internal interface IBinderPrivate
    {
        INotifyNewBlob NotifyNewBlob { get; }
    }
}