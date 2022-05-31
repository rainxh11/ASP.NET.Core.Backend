using System;
using System.Collections.Generic;
using UATL.MailSystem.Common.Models;
using HashCode = Invio.Hashing.HashCode;

namespace UATL.MailSystem.Common.EqualityComparer;

public class AttachementEqualityComparer : IEqualityComparer<Attachment>
{
    public bool Equals(Attachment x, Attachment y)
    {
        return string.Equals(x.MD5, y.MD5, StringComparison.OrdinalIgnoreCase) && x.FileSize == y.FileSize;
    }

    public int GetHashCode(Attachment obj)
    {
        return HashCode.From(obj.MD5, obj.FileSize);
    }
}