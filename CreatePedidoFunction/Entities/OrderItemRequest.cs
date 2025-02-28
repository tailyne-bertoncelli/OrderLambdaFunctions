using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreatePedidoFunction.Entities
{
    public class OrderItemRequest
    {
        public string Produto { get; set; }
        public int Quantidade { get; set; }
        public decimal Preco { get; set; }
    }
}
