using MongoDB.Entities;

namespace QuranSchool.Models;

public class ClientFile : FileEntity, ITenant
{
    public string TenantId { get; set; }
}