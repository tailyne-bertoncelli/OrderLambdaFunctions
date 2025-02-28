using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using MySqlConnector;
using Newtonsoft.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace DeletePedidoFunction;

public class Function
{
    private string connectionString = Environment.GetEnvironmentVariable("MYSQL_CONECTION");
    public async Task<APIGatewayProxyResponse> DeleteOrder(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            if (!request.QueryStringParameters.TryGetValue("id", out string orderIdStr) || !int.TryParse(orderIdStr, out int orderId))
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = 400,
                    Body = "ID do pedido é inválido ou não foi fornecido."
                };
            }

            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            string checkSql = "SELECT COUNT(*) FROM `Order` WHERE Id = @OrderId";
            await using var checkCmd = new MySqlCommand(checkSql, connection);
            checkCmd.Parameters.AddWithValue("@OrderId", orderId);

            var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
            if (count == 0)
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = 404,
                    Body = "Pedido não encontrado."
                };
            }

            string deleteItemsSql = "DELETE FROM OrderItem WHERE OrderId = @OrderId";
            string deleteOrderSql = "DELETE FROM `Order` WHERE Id = @OrderId";

            await using var cmdItems = new MySqlCommand(deleteItemsSql, connection);
            cmdItems.Parameters.AddWithValue("@OrderId", orderId);
            await cmdItems.ExecuteNonQueryAsync();

            await using var cmdOrder = new MySqlCommand(deleteOrderSql, connection);
            cmdOrder.Parameters.AddWithValue("@OrderId", orderId);
            await cmdOrder.ExecuteNonQueryAsync();

            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = $"Pedido {orderId} deletado com sucesso."
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
