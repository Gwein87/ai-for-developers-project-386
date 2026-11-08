using System.Net;
using System.Text;

namespace HexletCalendar.Api.Tests;

public sealed class ContractTests
{
    [Theory]
    [InlineData("{\"id\":0,\"name\":\"ok\",\"description\":\"ok\",\"durationMinutes\":30}", HttpStatusCode.Created)]
    [InlineData("{\"id\":1,\"name\":\"ok\",\"durationMinutes\":30}", HttpStatusCode.Created)]
    [InlineData("{\"id\":1,\"name\":\"ok\",\"description\":\"ok\",\"durationMinutes\":16}", HttpStatusCode.Created)]
    [InlineData("{\"id\":1,\"name\":\"ok\",\"description\":\"ok\",\"durationMinutes\":30,\"extra\":1}", HttpStatusCode.Created)]
    [InlineData("{}", HttpStatusCode.Accepted)]
    public async Task ValidatorRejectsIncorrectResponsesAndUndeclaredStatus(string json, HttpStatusCode status)
    {
        using var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        Assert.NotEmpty(await ApiContract.ValidateAsync(response, "/api/event-types", "post"));
    }
}
