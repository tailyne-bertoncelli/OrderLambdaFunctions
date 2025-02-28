using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using CreatePedidoFunction.Entities;
using MySqlConnector;
using Newtonsoft.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace CreatePedidoFunction;

public class Function
{
    private string connectionString = Environment.GetEnvironmentVariable("MYSQL_CONECTION");
    public string FunctionHandler(string input, ILambdaContext context)
    {
        return input.ToUpper();
    }

    public async Task<APIGatewayProxyResponse> CreateOrder(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            var pedido = JsonConvert.DeserializeObject<OrderRequest>(request.Body);

            var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            string sql = @"INSERT INTO `Order` (Cliente, Email, Total, Status, CreateAt, UpdateAt)
                            VALUES (@Cliente, @Email, @Total, @Status, @CreateAt, @UpdateAt);
                            SELECT LAST_INSERT_ID();";

            var cmdOrder = new MySqlCommand(sql, connection);
            cmdOrder.Parameters.AddWithValue("@Cliente", pedido.Cliente);
            cmdOrder.Parameters.AddWithValue("@Email", pedido.Email);
            cmdOrder.Parameters.AddWithValue("@Total", 0);
            cmdOrder.Parameters.AddWithValue("@Status", pedido.Status);
            cmdOrder.Parameters.AddWithValue("@CreateAt", DateTime.UtcNow);
            cmdOrder.Parameters.AddWithValue("@UpdateAt", DateTime.UtcNow);

            var orderId = await cmdOrder.ExecuteScalarAsync();

            decimal valorTotal = 0;
            foreach (var item in pedido.Itens) {
                string sqlItens = @"INSERT INTO OrderItem
                                    (OrderId, Name, Quantity, Price)
                                    VALUES(@OrderId, @Name, @Quantity, @Price);";

                var cmdIten = new MySqlCommand(sqlItens, connection);
                cmdIten.Parameters.AddWithValue("@OrderId", orderId);
                cmdIten.Parameters.AddWithValue("@Name", item.Produto);
                cmdIten.Parameters.AddWithValue("@Quantity", item.Quantidade);
                cmdIten.Parameters.AddWithValue("@Price", item.Preco);

                valorTotal += (item.Preco * item.Quantidade);

                await cmdIten.ExecuteNonQueryAsync();
            }

            pedido.Id = orderId.ToString();
            pedido.Total = valorTotal; 
            var updateOrder = new MySqlCommand($"UPDATE `Order` SET Total={valorTotal} WHERE Id={orderId};", connection);
            await updateOrder.ExecuteNonQueryAsync();

            return new APIGatewayProxyResponse
            {
                StatusCode = 201,
                Body = JsonConvert.SerializeObject(new { message = "Pedido criado!", pedido }),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }
        catch (MySqlException ex) 
        {
            context.Logger.LogError($"Erro no MySQL: {ex.Message}");
            return new APIGatewayProxyResponse { StatusCode = 500, Body = $"Erro no banco: {ex.Message}" };
        }
        catch (Exception ex) {
            context.Logger.LogError($"Erro ao criar pedido: {ex.Message}");
            return new APIGatewayProxyResponse { StatusCode = 500, Body = "Erro ao processar pedido" };
        }
    } 
}
