using MediatR;
using MicroRabbit.Domain.Core.Bus;
using MicroRabbit.Domain.Core.Commands;
using MicroRabbit.Domain.Core.Events;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicroRabbit.Infra.Bus;

public sealed class RabbitMQBus : IEventBus
{
    private readonly IMediator _mediator;
    private readonly Dictionary<string, List<Type>> _handlers;
    private readonly List<Type> _eventTypes;

    public RabbitMQBus(IMediator mediator)
    {
        _mediator = mediator;
        _handlers = new Dictionary<string, List<Type>>();
        _eventTypes = new List<Type>();
    }

    public Task SendCommand<T>(T command) where T : Command
    {
       return _mediator.Send(command);
    }

    public void Publish<T>(T @event) where T : Event
    {
        // Cria uma factory para conexão
        var factory = new ConnectionFactory() { HostName = "localhost" };

        // Cria a conexão
        using (var connection = factory.CreateConnection())
        // Abre um canal
        using (var channel = connection.CreateModel())
        {
            // Obtem nome do evento
            var eventName = @event.GetType().Name;

            // Declara uma fila
            channel.QueueDeclare(eventName, false, false, false, null);

            // Converte mensagem para JSON
            var message = JsonConvert.SerializeObject(@event);
            var body = Encoding.UTF8.GetBytes(message);

            // Publica a mensagem
            channel.BasicPublish("", eventName, null, body);
        }
    }

    public void Subscribe<T, TH>()
        where T : Event
        where TH : IEventHandler<T>
    {
        var eventName = typeof(T).Name;
        var handlerType = typeof(TH);

        // Verifica se eventTypes possui um evento com o tipo de T, se não, o adiciona
        if (!_eventTypes.Contains(typeof(T)))
        {
            _eventTypes.Add(typeof(T));
        }

        // Verifica se handlers tem as chaves de eventName
        if (!_handlers.ContainsKey(eventName))
        {
            _handlers.Add(eventName, new List<Type>());
        }

        // Verifica se handlers já tem o handlerType
        if (_handlers[eventName].Any(s => s.GetType() == handlerType))
        {
            throw new ArgumentException($"Handler type {handlerType.Name} already is registered for '{eventName}'", nameof(handlerType));
        }

        // Adiciona o handlerType a handlers
        _handlers[eventName].Add(handlerType);

        StartBasicConsume<T>();
    }

    private void StartBasicConsume<T>() where T : Event
    {
        // Cria factory de conexão
        var factory = new ConnectionFactory()
        {
            HostName = "localhost",
            DispatchConsumersAsync = true,
        };

        // Cria conexão
        var connection = factory.CreateConnection();
        // Abre um canal
        var channel = connection.CreateModel();

        var eventName = typeof(T).Name;

        // Declara a fila
        channel.QueueDeclare(eventName, false, false, false, null);

        // Cria consumer
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += Consumer_Received;

        // Consome as mensagens da fila
        channel.BasicConsume(eventName, true, consumer);
    }

    private async Task Consumer_Received(object sender, BasicDeliverEventArgs e)
    {
        var eventName = e.RoutingKey;
        var message = Encoding.UTF8.GetString(e.Body.ToArray());

        try
        {
            await ProcessEvent(eventName, message).ConfigureAwait(false);
        }
        catch (Exception ex)
        {

        }
    }

    private async Task ProcessEvent(string eventName, string message)
    {
        if (_handlers.ContainsKey(eventName))
        {
            var subscriptions = _handlers[eventName];
            foreach ( var subscription in subscriptions) 
            {
                var handler = Activator.CreateInstance(subscription);
                if (handler is null) continue;
                var eventType = _eventTypes.SingleOrDefault(t => t.Name == eventName);
                var @event = JsonConvert.DeserializeObject(message, eventType);
                var concreteType = typeof(IEventHandler<>).MakeGenericType(eventType);
                await (Task)concreteType.GetMethod("Handle").Invoke(handler, new object[] { @event });
            }
        }
    }
}
