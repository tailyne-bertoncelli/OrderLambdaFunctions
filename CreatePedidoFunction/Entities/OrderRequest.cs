using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreatePedidoFunction.Entities
{
    public class OrderRequest
    {
        public string? Id { get; set; }
        public string Cliente { get; set; }
        public string Email { get; set; }
        public List<OrderItemRequest> Itens { get; set; }
        public decimal? Total { get; set; }
        public string Status { get; set; }
    }
}
