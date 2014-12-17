using System.Net;
using System.Net.NetworkInformation;

namespace TestTask.Model
{
    public class CheckingResult
    {
        public string Ip { get; set; }

        public IPStatus? IPStatus { get; set; }

        public HttpStatusCode? HttpStatusCode { get; set; }
    }
}
