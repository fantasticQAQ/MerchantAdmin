using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MerchantAdmin.Shared.EventBus.RabbitMQ
{
    public class EventBusOptions
    {
        public string SubscriptionClientName { get; set; }
        public int RetryCount { get; set; } = 10;
    }
}
