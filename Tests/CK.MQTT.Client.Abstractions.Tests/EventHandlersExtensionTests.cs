using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using CK.MQTT.Client.Abstractions.Events.Extensions;
using System.Threading.Tasks;
using FluentAssertions;

using static CK.Testing.MonitorTestHelper;
namespace CK.MQTT.Client.Abstractions.Tests
{
    public class EventHandlersExtensionTests
    {
        class Unit
        {
        }
        
        [Test]
        public async Task simple_await_work()
        {


            SequentialEventHandlerSender<object, Unit> eventEmitter = new SequentialEventHandlerSender<object, Unit>();
            var task = eventEmitter.FirstOrDefaultAsync();
            task.IsCompleted.Should().BeFalse();
            task.IsFaulted.Should().BeFalse();
            eventEmitter.Raise( TestHelper.Monitor, this, new Unit() );
            await Task.Yield();
            await Task.Delay( 500 );
            task.IsCompleted.Should().BeTrue();
            task.IsFaulted.Should().BeTrue();
        }
    }
}
