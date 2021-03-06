﻿namespace NServiceBus.AcceptanceTests.Sagas
{
    using System;
    using System.Threading.Tasks;
    using System.Transactions;
    using AcceptanceTesting;
    using EndpointTemplates;
    using NUnit.Framework;
    using Persistence.Sql;
    using Pipeline;

    public class When_invoking_a_saga : NServiceBusAcceptanceTest
    {
        [Test]
#if NET452
        [TestCase(TransportTransactionMode.TransactionScope)] //Uses TransactionScope to ensure exactly-once
#endif
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)] //Uses shared DbConnection/DbTransaction to ensure exactly-once
        [TestCase(TransportTransactionMode.ReceiveOnly)] //Uses the Outbox to ensure exactly-once
        public async Task Should_rollback_saga_data_changes_when_transport_transaction_is_rolled_back(TransportTransactionMode transactionMode)
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<EndpointThatHostsASaga>(
                    b => b.When((session, ctx) => session.SendLocal(new SagaMessage()
                    {
                        Id = ctx.TestRunId
                    })).DoNotFailOnErrorMessages().CustomConfig(c =>
                    {
                        if (transactionMode == TransportTransactionMode.ReceiveOnly)
                        {
                            c.EnableOutbox();
                        }
                        else
                        {
                            c.ConfigureTransport().Transactions(transactionMode);
                        }
                    }))
                .Done(c => c.ReplyReceived)
                .Run();

            Assert.True(context.ReplyReceived);
            Assert.IsFalse(context.TransactionEscalatedToDTC);
            Assert.AreEqual(2, context.SagaInvocationCount, "Saga handler should be called twice");
            Assert.AreEqual(1, context.SagaCounterValue, "Saga value should be incremented only once");
        }

        public class Context : ScenarioContext
        {
            public int SagaCounterValue { get; set; }
            public int SagaInvocationCount { get; set; }
            public bool TransactionEscalatedToDTC { get; set; }
            public bool ReplyReceived { get; set; }
        }

        public class EndpointThatHostsASaga : EndpointConfigurationBuilder
        {
            public EndpointThatHostsASaga()
            {
                EndpointSetup<DefaultServer>(config =>
                {
                    config.Pipeline.Register(new BehaviorThatThrowsAfterFirstMessage.Registration());
                    var recoverability = config.Recoverability();
                    recoverability.Immediate(settings =>
                    {
                        settings.NumberOfRetries(1);
                    });
                });
            }

            public class TestSaga : SqlSaga<TestSaga.SagaData>, IAmStartedByMessages<SagaMessage>
            {
                public Context TestContext { get; set; }

                public async Task Handle(SagaMessage message, IMessageHandlerContext context)
                {
                    if (message.Id != TestContext.TestRunId)
                    {
                        return;
                    }

                    Data.TestRunId = message.Id;
                    Data.Counter += 1;

                    TestContext.SagaCounterValue = Data.Counter;
                    TestContext.SagaInvocationCount++;

                    await context.SendLocal(new ReplyMessage
                    {
                        Id = message.Id
                    });
                }

                protected override void ConfigureMapping(IMessagePropertyMapper mapper)
                {
                    mapper.ConfigureMapping<SagaMessage>(m => m.Id);
                }

                protected override string CorrelationPropertyName => "TestRunId";
                public class SagaData : ContainSagaData
                {
                    public virtual Guid TestRunId { get; set; }
                    public virtual int Counter { get; set; }
                }
            }

            public class Handler : IHandleMessages<ReplyMessage>
            {
                public Context TestContext { get; set; }

                public Task Handle(ReplyMessage message, IMessageHandlerContext context)
                {
                    if (TestContext.TestRunId == message.Id)
                    {
                        TestContext.ReplyReceived = true;
                    }

                    return Task.FromResult(0);
                }
            }

            class BehaviorThatThrowsAfterFirstMessage : Behavior<IIncomingLogicalMessageContext>
            {
                public Context TestContext { get; set; }

                public override async Task Invoke(IIncomingLogicalMessageContext context, Func<Task> next)
                {
                    await next();

                    if (TestContext.SagaInvocationCount == 1)
                    {
                        TestContext.TransactionEscalatedToDTC = Transaction.Current.TransactionInformation.DistributedIdentifier != Guid.Empty;

                        throw new SimulatedException();
                    }
                }

                public class Registration : RegisterStep
                {
                    public Registration() : base("BehaviorThatThrowsAfterFirstMessage", typeof(BehaviorThatThrowsAfterFirstMessage), "BehaviorThatThrowsAfterFirstMessage")
                    {
                    }
                }
            }
        }

        public class SagaMessage : IMessage
        {
            public Guid Id { get; set; }
        }

        public class ReplyMessage : IMessage
        {
            public Guid Id { get; set; }
        }
    }
}