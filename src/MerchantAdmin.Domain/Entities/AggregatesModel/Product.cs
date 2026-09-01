
namespace MerchantAdmin.Domain.Entities.AggregatesModel
{
    public class Product : Entity, IAggregateRoot
    {
        public string Name { get; private set; }
        public decimal Price { get; private set; }
        public decimal Stock { get; private set; }
        public bool IsActive { get; private set; }

        protected Product() { }

        public Product(string name, decimal price, decimal stock)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new DomainException("名称不能为空");
            if (price < 0) throw new DomainException("价格不能为负数");
            if (stock < 0) throw new DomainException("库存不能为负数");
            Name = name;
            Price = price;
            Stock = stock;
            IsActive = true;
        }

        /// <summary>编辑商品名称与价格。</summary>
        public void UpdateInfo(string name, decimal price)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new DomainException("名称不能为空");
            if (price < 0) throw new DomainException("价格不能为负数");

            Name = name;
            Price = price;
        }

        /// <summary>手动调整库存（正数补货，负数扣减），结果不能为负。</summary>
        public void AdjustStock(decimal delta)
        {
            var newStock = Stock + delta;
            if (newStock < 0) throw new DomainException("库存不能为负数");

            Stock = newStock;
        }

        /// <summary>上架 / 下架。</summary>
        public void SetActive(bool active) => IsActive = active;

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
