namespace Sample.Components.CourierActivities
{
    using System;
    using System.Threading.Tasks;
    using MassTransit;
    using Microsoft.Extensions.Logging;


    public class PaymentActivity(ILogger<PaymentActivity> logger) :
        IActivity<PaymentArguments, PaymentLog>
    {
        private readonly ILogger<PaymentActivity> _logger = logger;

        static readonly Random _random = new Random();

        public async Task<ExecutionResult> Execute(ExecuteContext<PaymentArguments> context)
        {
            _logger.LogInformation("[PaymentActivity]");
            
            string cardNumber = context.Arguments.CardNumber;
            if (string.IsNullOrEmpty(cardNumber))
                throw new ArgumentNullException(nameof(cardNumber));

            await Task.Delay(1000);
            await Task.Delay(_random.Next(10000));

            if (cardNumber.StartsWith("5999"))
            {
                _logger.LogInformation("[PaymentActivity] InvalidOperationException");
                //return context.Faulted(new InvalidOperationException("The card number was invalid"));
                throw new InvalidOperationException("The card number was invalid");
            }

            return context.Completed(new {AuthorizationCode = "77777"});
        }

        public async Task<CompensationResult> Compensate(CompensateContext<PaymentLog> context)
        {
            await Task.Delay(100);

            return context.Compensated();
        }
    }
}