// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.TestCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.IO;
using System.Reflection;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Config;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class ToolingTests
    {
        [Fact]
        public async Task Test()
        {
            MyProg prog = new MyProg();
            var activator = new FakeActivator();
            activator.Add(prog);

            JobHostConfiguration config = TestHelpers.NewConfig<MyProg>(activator);
            
            var ext = new TestExtension();

            var exts = config.GetExtensions();
            exts.RegisterExtension<IExtensionConfigProvider>(ext);

            var tooling = await config.GetToolingAsync();

            // Callable
            
            var host = new TestJobHost<MyProg>(config);
            host.Call("Test");

            // Fact that we registered a Widget converter is enough to add the assembly 
            Assembly asm = tooling.TryResolveAssembly(typeof(Widget).Assembly.GetName().Name);
            Assert.Same(asm, typeof(Widget).Assembly);

            // check with full name 
            Assembly asm2= tooling.TryResolveAssembly(typeof(Widget).Assembly.GetName().FullName);
            Assert.Same(asm2, typeof(Widget).Assembly);

            var attrType = tooling.GetAttributeTypeFromName("Test");
            Assert.Equal(typeof(TestAttribute), attrType);

            // JObject --> Attribute 
            var attrs = tooling.GetAttributes(attrType, JObject.FromObject(new { Flag = "xyz" }));
            TestAttribute attr = (TestAttribute)attrs[0];
            Assert.Equal("xyz", attr.Flag);

            // Getting default type. 
            var defaultType = tooling.GetDefaultType(attr, FileAccess.Read, typeof(object));
            Assert.Equal(typeof(JObject), defaultType);

            Assert.Throws<InvalidOperationException>(() => tooling.GetDefaultType(attr, FileAccess.Write, typeof(object)));
        }

        static T GetAttr<T>(ITooling tooling, object obj) where T : Attribute
        {
            var attributes = tooling.GetAttributes(typeof(T), JObject.FromObject(obj));
            var attr = (T)attributes[0];

            return attr;
        }

        // Test builint attributes (blob, table, queue) 
        //[Fact]
        public async Task Builtins()
        {
            JobHostConfiguration config = TestHelpers.NewConfig();

            var ext = new TestExtension();

            // Still need to call Add on the default since that provides a means to pass in config. 
            var exts = config.GetExtensions();
            exts.RegisterExtension<IExtensionConfigProvider>(new DefaultBindingProvider());

            var tooling = await config.GetToolingAsync();

            Dictionary<string, Type> builtins = new Dictionary<string, Type>
            {
                {  "Blob", typeof(BlobAttribute) },
                {  "BlobTrigger", typeof(BlobTriggerAttribute) },
                {  "Table", typeof(TableAttribute) },
                {  "Queue", typeof(QueueAttribute) },
                {  "QueueTrigger", typeof(QueueTriggerAttribute) }
            };
            foreach (var kv in builtins)
            {
                var typeName = kv.Key;
                var expectedType = kv.Value;

                var actualType = tooling.GetAttributeTypeFromName(typeName);
                Assert.Equal(expectedType, actualType);
            }
        }


        [Fact]
        public async Task AttrBuilder()
        {
            JobHostConfiguration config = TestHelpers.NewConfig();
            var tooling = await config.GetToolingAsync();


            // Blob 
            var blobAttr = GetAttr<BlobAttribute>(tooling, new { path = "x" } );
            Assert.Equal("x", blobAttr.BlobPath);
            Assert.Equal(null, blobAttr.Access);

            blobAttr = GetAttr<BlobAttribute>(tooling, new { path = "x", direction="in" });
            Assert.Equal("x", blobAttr.BlobPath);
            Assert.Equal(FileAccess.Read, blobAttr.Access);

            blobAttr = GetAttr<BlobAttribute>(tooling, new { Path = "x", Direction="out" });
            Assert.Equal("x", blobAttr.BlobPath);
            Assert.Equal(FileAccess.Write, blobAttr.Access);

            blobAttr = GetAttr<BlobAttribute>(tooling, new { path = "x", direction = "inout" });
            Assert.Equal("x", blobAttr.BlobPath);
            Assert.Equal(FileAccess.ReadWrite, blobAttr.Access);

            {
                var attributes = tooling.GetAttributes(typeof(BlobAttribute), JObject.FromObject(
                new
                {
                    path = "x",
                    direction = "in",
                    connection = "cx1"
                })); 

                Assert.Equal(2, attributes.Length);
                blobAttr = (BlobAttribute)attributes[0];
                var storageAttr = (StorageAccountAttribute)attributes[1];

                Assert.Equal("x", blobAttr.BlobPath);
                Assert.Equal(FileAccess.Read, blobAttr.Access);

                Assert.Equal("cx1", storageAttr.Account);
            }

            var blobTriggerAttr = GetAttr<BlobTriggerAttribute>(tooling, new { path = "x" });
            Assert.Equal("x", blobTriggerAttr.BlobPath);

            // Queue 
            var queueAttr = GetAttr<QueueAttribute>(tooling, new { QueueName = "q1" });
            Assert.Equal("q1", queueAttr.QueueName);

            var queueTriggerAttr = GetAttr<QueueTriggerAttribute>(tooling, new { QueueName = "q1" });
            Assert.Equal("q1", queueTriggerAttr.QueueName);
            
            // Table
            var tableAttr = GetAttr<TableAttribute>(tooling, new { TableName = "t1" });
            Assert.Equal("t1", tableAttr.TableName);

            tableAttr = GetAttr<TableAttribute>(tooling, new { TableName = "t1", partitionKey ="pk", Filter="f1" });
            Assert.Equal("t1", tableAttr.TableName);
            Assert.Equal("pk", tableAttr.PartitionKey);
            Assert.Equal(null, tableAttr.RowKey);
            Assert.Equal("f1", tableAttr.Filter);
        }


        [Binding]
        public class TestAttribute : Attribute
        {
            public TestAttribute(string flag)
            {
                this.Flag = flag;
            }
            public string Flag { get; set; }
        }

        public class Widget
        {
            public string Value;
        }

        public class TestExtension : IExtensionConfigProvider            
        {  
            public void Initialize(ExtensionConfigContext context)
            {
                context.AddBindingRule<TestAttribute>().
                    BindToInput<Widget>(Builder);

                var cm = context.Converters;
                cm.AddConverter<Widget, JObject>(widget => JObject.FromObject(widget));                
            }

            Widget Builder(TestAttribute input)
            {
                return new Widget { Value = input.Flag };
            }
        }

        public class MyProg
        {
            public string _value;
            public void Test([Test("f1")] Widget w)
            {
                _value = w.Value;
            }
        }
    }
}
