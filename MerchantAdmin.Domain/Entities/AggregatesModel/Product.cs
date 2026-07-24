using MerchantAdmin.Domain.Exceptions;
using MerchantAdmin.Domain.Seedwork;
using MerchantAdmin.Ordering.Domain.Seedwork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MerchantAdmin.Domain.Entities.AggregatesModel
{
    public class Product : Entity, IAggregateRoot
    {
        public string Name { get; private set; }
        public decimal Price { get; private set; }
        public decimal Stock { get; private set; }

        protected Product() { }

        public Product(string name, decimal price, decimal stock)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new DomainException("名称不能为空");
            if (price < 0) throw new DomainException("价格不能为负数");
            if (stock < 0) throw new DomainException("库存不能为负数");
            Name = name;
            Price = price;
            Stock = stock;
        }

        public void ReduceStock(decimal qty)
        {
            if (qty <= 0) throw new DomainException("数量必须大于0");
            if (Stock < qty) throw new DomainException("库存不足");

            Stock -= qty;
        }

        public void IncreaseStock(decimal qty)
        {
            Stock += qty;
        }
    }
}
