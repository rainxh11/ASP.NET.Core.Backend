using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using DicomServer.Models;
using System.Collections.Generic;

namespace DicomServer.Hubs;
public class RefreshSocket : Hub
{
    public async Task Send(IEnumerable<RisImagingStudy> studies)
    {
        await Clients.All.SendAsync("Refresh", studies);
    }
}
