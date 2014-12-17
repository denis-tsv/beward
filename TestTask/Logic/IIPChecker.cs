using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using TestTask.Model;

namespace TestTask.Logic
{
    public interface IIPChecker : IDisposable
    {
        Task<List<CheckingResult>> CheckIpRange(IPAddress from, IPAddress to, int port); 
    }
}
