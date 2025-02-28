using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using MySqlConnector;
using Newtonsoft.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ListPedidosFunction;

public class Function
{
    private string connectionString = Environment.GetEnvironmentVariable("MYSQL_CONECTION");
    public async Task<APIGatewayProxyResponse> ListOrders(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            string sql = @"
                        SELECT 
                            o.Id AS PedidoId, o.Cliente, o.Email, o.Total, o.Status, o.CreateAt, o.UpdateAt,
                            i.Id AS ItemId, i.Name AS Produto, i.Quantity, i.Price
                        FROM `Order` o
                        LEFT JOIN `OrderItem` i ON o.Id = i.OrderId
                        ORDER BY o.Id;"; 

            await using var cmd = new MySqlCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync();

            var pedidos = new List<Dictionary<string, object>>();
            var pedidosMap = new Dictionary<int, Dictionary<string, object>>();

            while (await reader.ReadAsync())
            {
                int pedidoId = reader.GetInt32("PedidoId");

                if (!pedidosMap.TryGetValue(pedidoId, out var pedido))
                {
                    pedido = new Dictionary<string, object>
                    {
                        ["Id"] = pedidoId,
                        ["Cliente"] = reader.GetString("Cliente"),
                        ["Email"] = reader.GetString("Email"),
                        ["Total"] = reader.GetDecimal("Total"),
                        ["Status"] = reader.GetString("Status"),
                        ["DataCriacao"] = reader.GetDateTime("CreateAt"),
                        ["DataAlteracao"] = reader.GetDateTime("UpdateAt"),
                        ["Itens"] = new List<Dictionary<string, object>>() 
                    };

                    pedidosMap[pedidoId] = pedido;
                    pedidos.Add(pedido);
                }

                if (!reader.IsDBNull(reader.GetOrdinal("ItemId")))
                {
                    var item = new Dictionary<string, object>
                    {
                        ["Id"] = reader.GetInt32("ItemId"),
                        ["Produto"] = reader.GetString("Produto"),
                        ["Quantidade"] = reader.GetInt32("Quantity"),
                        ["PrecoUnitario"] = reader.GetDecimal("Price")
                    };

                    ((List<Dictionary<string, object>>)pedido["Itens"]).Add(item);
                }
            }

            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = JsonConvert.SerializeObject(pedidos),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }
        catch (MySqlException ex)
        {
            context.Logger.LogError($"Erro no MySQL: {ex.Message}");
            return new APIGatewayProxyResponse { StatusCode = 500, Body = $"Erro no banco: {ex.Message}" };
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Erro ao buscar pedidos: {ex.Message}");
            return new APIGatewayProxyResponse { StatusCode = 500, Body = $"Erro ao buscar pedidos: {ex.Message}" };
        }
    } 
}
