using MongoDB.Entities;

namespace QuranSchool.Models;

public class ClientEntity : Entity, ITenant
{
    public string TenantId { get; set; }
}