namespace KlingerExchange.Matching.Domain;

public sealed class PriceLevel
{
    public decimal Price { get; }
    public LinkedList<Order> Orders { get; }
    public decimal TotalQuantity { get; private set; }

    public PriceLevel(decimal price)
    {
        Price = price;
        Orders = new LinkedList<Order>();
        TotalQuantity = 0;
    }

    public void AddOrder(Order order)
    {
        Orders.AddLast(order);
        TotalQuantity += order.LeavesQty;
    }

    public bool RemoveOrder(long orderId)
    {
        var node = Orders.First;
        while (node != null)
        {
            if (node.Value.OrderId == orderId)
            {
                TotalQuantity -= node.Value.LeavesQty;
                Orders.Remove(node);
                return true;
            }
            node = node.Next;
        }
        return false;
    }

    public void UpdateOrderFill(long orderId, decimal fillQty)
    {
        var node = Orders.First;
        while (node != null)
        {
            if (node.Value.OrderId == orderId)
            {
                TotalQuantity -= fillQty;
                var updatedOrder = node.Value.WithFill(fillQty);
                node.Value = updatedOrder;

                // Remove if fully filled
                if (updatedOrder.IsFilled)
                {
                    Orders.Remove(node);
                }
                return;
            }
            node = node.Next;
        }
    }

    public bool ApplyFillToFirst(decimal fillQty, out Order originalOrder, out Order updatedOrder, out bool removed)
    {
        var node = Orders.First;
        if (node == null)
        {
            originalOrder = default;
            updatedOrder = default;
            removed = false;
            return false;
        }

        originalOrder = node.Value;
        TotalQuantity -= fillQty;
        updatedOrder = originalOrder.WithFill(fillQty);

        if (updatedOrder.IsFilled)
        {
            Orders.RemoveFirst();
            removed = true;
        }
        else
        {
            node.Value = updatedOrder;
            removed = false;
        }

        return true;
    }

    public bool IsEmpty => Orders.Count == 0;
}
