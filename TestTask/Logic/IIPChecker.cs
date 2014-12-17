using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace TestTask
{
    public interface IIPChecker : IDisposable
    {
        Task<List<CheckingResult>> CheckIpRange(IPAddress from, IPAddress to);
    }
}
