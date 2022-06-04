using Refit;
using ReniwnMailServiceApi.Models;

namespace ReniwnMailServiceApi.Services;

public interface IMyReniwnApiClient
{
    [Post("/api/v1/users")]
    Task<ReniwnResponse> CreateUser(
        [Query] string label,
        [Query] string api_token,
        [Query] string nickname,
        [Query] string email,
        [Query] string password,
        [Query] string? phone = ""
    );
}