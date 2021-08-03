using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DashFire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace eCommerceAutomation.Scrapper
{
    public class ScrapperJob : Job
    {
        public override JobInformation JobInformation => JobInformationBuilder.CreateInstance()
            .RegistrationRequired()
            .SetDescription("Fetch contents of Url, Telegram and Instagram posts and publish into the queue.")
            .SetDisplayName("eCommerce Scrapper Job")
            .SetSystemName(nameof(ScrapperJob))
            .Build();

        private readonly ILogger<ScrapperJob> _logger;
        private readonly IOptions<DashOptions> _options;
        private readonly IOptions<ApplicationOptions> _applicationOptions;
        private readonly FetcherService _fetcherService;

        private readonly IConnection _connection;
        private readonly IModel _channel;

        public ScrapperJob(ILogger<ScrapperJob> logger, IOptions<DashOptions> options, IOptions<ApplicationOptions> applicationOptions, FetcherService fetcherService)
        {
            _logger = logger;
            _options = options;
            _applicationOptions = applicationOptions;

            var factory = new ConnectionFactory() { Uri = new Uri(_options.Value.RabbitMqConnectionString) };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.ExchangeDeclare(_applicationOptions.Value.ExchangeName, "headers", true);

            // Request
            _channel.QueueDeclare(queue: _applicationOptions.Value.RequestQueueName,
                                     durable: true,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);
            _channel.QueueBind(_applicationOptions.Value.RequestQueueName, _applicationOptions.Value.ExchangeName, string.Empty, new Dictionary<string, object>()
            {
                {
                    "type", "request"
                }
            });

            // Response
            _channel.QueueDeclare(queue: _applicationOptions.Value.ResponseQueueName,
                                     durable: true,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);
            _channel.QueueBind(_applicationOptions.Value.ResponseQueueName, _applicationOptions.Value.ExchangeName, string.Empty, new Dictionary<string, object>()
            {
                {
                    "type", "response"
                }
            });
            _fetcherService = fetcherService;
        }

        protected override async Task StartInternallyAsync(CancellationToken cancellationToken)
        {
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async(model, ea) =>
            {
                await ConsumerReceivedAsync(model, ea);
            };

            _channel.BasicConsume(_applicationOptions.Value.RequestQueueName, false, consumer);

            do
            {
                try
                {
                    await Task.Delay(int.MaxValue, cancellationToken);
                }
                catch
                {
                    // ignored
                }
            } while (!cancellationToken.IsCancellationRequested);
        }

        private async Task ConsumerReceivedAsync(object sender, BasicDeliverEventArgs ea)
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            var request = JsonSerializer.Deserialize<Models.Request>(message);
            await ProcessRequestAsync(request, ea);
        }

        private async Task ProcessRequestAsync(Models.Request request, BasicDeliverEventArgs ea)
        {
            switch (request.Type)
            {
                case Constants.RequestType.Website:
                    try
                    {
                        var content = await _fetcherService.GetUrlContentAsync(request.Address);
                        PublishResult(new Models.Response()
                        {
                            Content = content,
                            RequestId = request.RequestId
                        });

                        _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    }
                    catch { }
                    break;
            }
        }

        private void PublishResult(Models.Response response)
        {
            var properties = _channel.CreateBasicProperties();
            properties.Persistent = false;
            properties.Headers = new Dictionary<string, object>()
            {
                {
                    "type", "response"
                }
            };
            var messageBodyBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response));
            _channel.BasicPublish(_applicationOptions.Value.ExchangeName, "", properties, messageBodyBytes);
        }
    }
}
