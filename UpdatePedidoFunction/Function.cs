using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using MySqlConnector;
using Newtonsoft.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace UpdatePedidoFunction;

public class Function
{
    private string connectionString = Environment.GetEnvironmentVariable("MYSQL_CONECTION");
    public async Task<APIGatewayProxyResponse> UpdateOrder(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            var body = JsonConvert.DeserializeObject<UpdateRequest>(request.Body);

            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            string checkSql = "SELECT COUNT(*) FROM `Order` WHERE Id = @OrderId";
            await using var checkCmd = new MySqlCommand(checkSql, connection);
            checkCmd.Parameters.AddWithValue("@OrderId", body.Id);

            var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
            if (count == 0)
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = 404,
                    Body = "Pedido não encontrado."
                };
            }

            var statusValidos = new HashSet<string> (StringComparer.OrdinalIgnoreCase) { "PENDENTE", "PROCESSANDO", "ENVIADO", "CANCELADO" };
            if (!statusValidos.Contains(body.Status))
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = 404,
                    Body = "Status inválido!\n" +
                           "Status válidos: PENDENTE, PROCESSANDO, ENVIADO, CANCELADO"
                };
            }

            string sql = @"UPDATE `Order` SET Status=@Status, UpdateAt=NOW()
            WHERE Id = @OrderId;";

            await using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@OrderId", body.Id);
            cmd.Parameters.AddWithValue("@Status", body.Status.ToUpper());
            await cmd.ExecuteNonQueryAsync();

            string order = @"
            SELECT 
                o.Id AS PedidoId, o.Cliente, o.Email, o.Total, o.Status, o.CreateAt, o.UpdateAt,
                i.Id AS ItemId, i.Name AS Produto, i.Quantity, i.Price
            FROM `Order` o
            LEFT JOIN `OrderItem` i ON o.Id = i.OrderId
            WHERE o.Id = @OrderId;";

            await using var cmdOrder = new MySqlCommand(order, connection);
            cmdOrder.Parameters.AddWithValue("@OrderId", body.Id);
            await using var reader = await cmdOrder.ExecuteReaderAsync();

            Dictionary<string, object> pedido = null;
            var itens = new List<Dictionary<string, object>>();

            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(reader.GetOrdinal("ItemId")))
                {
                    itens.Add(new Dictionary<string, object>
                    {
                        ["Id"] = reader.GetInt32("ItemId"),
                        ["Produto"] = reader.GetString("Produto"),
                        ["Quantidade"] = reader.GetInt32("Quantity"),
                        ["PrecoUnitario"] = reader.GetDecimal("Price")
                    });
                }

                if (pedido == null)
                {
                    pedido = new Dictionary<string, object>
                    {
                        ["Id"] = reader.GetInt32("PedidoId"),
                        ["Cliente"] = reader.GetString("Cliente"),
                        ["Email"] = reader.GetString("Email"),
                        ["Total"] = reader.GetDecimal("Total"),
                        ["Status"] = reader.GetString("Status"),
                        ["DataCriacao"] = reader.GetDateTime("CreateAt"),
                        ["DataAlteracao"] = reader.GetDateTime("UpdateAt"),
                        ["Itens"] = itens
                    };
                }
            }

            if (pedido == null)
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = 404,
                    Body = "Pedido não encontrado."
                };
            }

            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = JsonConvert.SerializeObject(pedido),
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
            context.Logger.LogError($"Erro ao buscar pedido: {ex.Message}");
            return new APIGatewayProxyResponse
            {
                StatusCode = 500,
                Body = $"Erro ao processar a requisição: {ex.Message}"
            };
        }
    }
}
