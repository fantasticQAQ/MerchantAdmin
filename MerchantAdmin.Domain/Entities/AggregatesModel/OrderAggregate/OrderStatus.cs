using System.Text.Json.Serialization;

namespace MerchantAdmin.Domain.Entities.AggregatesModel.OrderAggregate;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrderStatus
{
    Created = 1,
    Paid = 2,
    Cancelled = 3,
}
