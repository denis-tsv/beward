using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using TestTask.Model;
using TestTask.Properties;

namespace TestTask.Logic
{
    public class ParallelForIPChecker : IIPChecker
    {
        private readonly List<CheckingResult> _result = new List<CheckingResult>();
        private readonly List<CheckingResult> _httpList = new List<CheckingResult>();
        private Task<List<CheckingResult>> _task;
        private CancellationTokenSource _cancelTokenSource;
        private int _port;

        public void Dispose()
        {
            if (!_task.IsCompleted)
            {
                _cancelTokenSource.Cancel();
            }
        }

        public Task<List<CheckingResult>> CheckIpRange(IPAddress from, IPAddress to, int port)
        {
            long start = IpConverter.IPAddressToLong(from);
            long end = IpConverter.IPAddressToLong(to);
            _port = port;

            if (start > end) throw new InvalidOperationException("Start > End");

            _cancelTokenSource = new CancellationTokenSource();
            
            _task = Task.Factory.StartNew(() =>
            {
                //it is possible to put cancellation token into ParallelOptions, but it is unable to take it from ParallelOptions, so I will use field
                Parallel.For(start, end + 1, CreateCheckingResult);

                Parallel.ForEach(_result, CheckAvailability);

                Parallel.ForEach(_httpList, CheckHttp);

                return _result;
            }, _cancelTokenSource.Token);

            return _task;
        }

        private void CreateCheckingResult(long ip, ParallelLoopState state)
        {
            _cancelTokenSource.Token.ThrowIfCancellationRequested(); 

            var item = new CheckingResult { Ip = IpConverter.LongToString(ip) };
            
            lock (_result)
            {
                _result.Add(item);   
            }
        }

        private void CheckHttp(CheckingResult check, ParallelLoopState state)
        {
            _cancelTokenSource.Token.ThrowIfCancellationRequested();  
            
            string address;
            if (_port != 0)
            {
                var adresswithport = string.Format("{0}:{1}", check.Ip, _port);
                address = string.Format(Settings.Default.HttpCheckAddressFormat, adresswithport);
            }
            else
            {
                address = string.Format(Settings.Default.HttpCheckAddressFormat, check.Ip);
            }

            using (var client = new HttpClient())
            {
                try
                {
                    var task = client.GetAsync(address, _cancelTokenSource.Token);
                    task.Wait();
                    check.HttpStatusCode = task.Result.StatusCode;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    check.Error = ex.Message;
                }
            }
        }

        private void CheckAvailability(CheckingResult check, ParallelLoopState state)
        {
            _cancelTokenSource.Token.ThrowIfCancellationRequested(); 
            
            var ping = new Ping();

            var pingReply = ping.Send(check.Ip);
            check.IPStatus = pingReply.Status;//TODO implement cancellation by Cancellation token

            if (check.IPStatus == IPStatus.Success)
            {
                lock (_httpList)
                {
                    _httpList.Add(check);    
                }
            }
        }
    }
}
