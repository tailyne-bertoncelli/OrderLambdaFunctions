﻿namespace CreatePedidoFunction.Entities
{
    public class OrderItem
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}