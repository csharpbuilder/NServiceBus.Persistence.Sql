﻿using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Persistence.Sql;
using NUnit.Framework;

[TestFixture]
public class When_outbox_disabled_and_different_persistence_used_for_sagas : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_not_enable_synchronized_storage_session()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<EndpointWithDummySaga>(bb => bb.When(s => s.SendLocal(new MyMessage())))
            .Done(c => c.Done)
            .Run()
            .ConfigureAwait(false);

        Assert.IsTrue(context.Done);
        Assert.IsFalse(context.SessionCreated);
        StringAssert.StartsWith("Cannot access the SQL synchronized storage session", context.ExceptionMessage);
    }

    public class Context : ScenarioContext
    {
        public bool SessionCreated { get; set; }
        public bool Done { get; set; }
        public string ExceptionMessage { get; set; }
    }

    public class EndpointWithDummySaga : EndpointConfigurationBuilder
    {
        public EndpointWithDummySaga()
        {
            EndpointSetup<DefaultServer>(c =>
            {
                c.UsePersistence<InMemoryPersistence, StorageType.Sagas>();
            });
        }

        public class MyMessageHandler : IHandleMessages<MyMessage>
        {
            public Context Context { get; set; }
            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                try
                {
                    var session = context.SynchronizedStorageSession.SqlPersistenceSession();
                    Context.SessionCreated = session != null;
                }
                catch (Exception e)
                {
                    Context.ExceptionMessage = e.Message;
                }
                Context.Done = true;
                return Task.FromResult(0);
            }
        }

        //This saga is not used but required to activate the saga feature
        public class DummySaga : SqlSaga<DummySagaData>, IAmStartedByMessages<DummyMessage>
        {
            protected override string CorrelationPropertyName => "Dummy";
            protected override void ConfigureMapping(IMessagePropertyMapper mapper)
            {
                mapper.ConfigureMapping<DummyMessage>(m => null);
            }

            public Task Handle(DummyMessage message, IMessageHandlerContext context)
            {
                throw new System.NotImplementedException();
            }
        }

        public class DummySagaData : ContainSagaData
        {
            public string Dummy { get; set; }
        }
    }

    public class DummyMessage : IMessage
    {
    }

    public class MyMessage : IMessage
    {
    }
}