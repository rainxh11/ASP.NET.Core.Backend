using MongoDB.Entities;
using System;
using System.IO;

namespace UATL.MailSystem.Common.Models;

public class Attachment : FileEntity, ICreatedOn, IModifiedOn
{
    public string Name { get; set; }
    public string Extension
    {
        get
        {
            try
            {
                return Path.GetExtension(Name).TrimStart('.');
            }
            catch
            {
                return "";
            }
        }
    }

    public string ContentType { get; set; }
    public AccountBase UploadedBy { get; set; }
    private bool IsEncrypted { get; set; } = false;
    public DateTime CreatedOn { get; set; }
    public DateTime ModifiedOn { get; set; }

    public bool Compare(string md5, long fileSize)
    {
        return MD5 == md5 && FileSize == fileSize;
    }
}