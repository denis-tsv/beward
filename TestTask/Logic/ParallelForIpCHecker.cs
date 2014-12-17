using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using TestTask.Model;
using TestTask.Properties;

namespace TestTask.Logic
{
    public class ParallelForIPChecker : IIPChecker
    {
        private TaskCompletionSource<List<CheckingResult>> _tcs;
        private readonly List<CheckingResult> _result = new List<CheckingResult>();
        private readonly List<CheckingResult> _httpList = new List<CheckingResult>();
        private bool _interrupted = false;
        private ParallelLoopResult? _currentLoopResult;

        public void Dispose()
        {
            if (_currentLoopResult.HasValue && !_currentLoopResult.Value.IsCompleted)
            {
                _interrupted = true;
                while (!_currentLoopResult.Value.IsCompleted) ;
                _tcs.SetCanceled();
            }
        }

        public Task<List<CheckingResult>> CheckIpRange(IPAddress @from, IPAddress to)
        {
            long start = IpConverter.IPAddressToLong(from);
            long end = IpConverter.IPAddressToLong(to);

            if (start > end) throw new InvalidOperationException("Start > End");

            _tcs = new TaskCompletionSource<List<CheckingResult>>();

            Task.Factory.StartNew(() =>
            {
                _currentLoopResult = Parallel.For(start, end + 1, CreateCheckingResult);

                _currentLoopResult = Parallel.ForEach(_result, CheckAvailability);

                _currentLoopResult = Parallel.ForEach(_httpList, CheckHttp);

                _currentLoopResult = null;

                _tcs.SetResult(_result);
            });

            return _tcs.Task;
        }

        private void CreateCheckingResult(long ip, ParallelLoopState state)
        {
            if (_interrupted) state.Stop();

            var item = new CheckingResult { Ip = IpConverter.LongToString(ip) };
            
            lock (_result)// TODO replace lock by ConcurrentQueue?
            {
                _result.Add(item);   
            }
        }

        private void CheckHttp(CheckingResult check, ParallelLoopState state)
        {
            if (_interrupted) state.Stop();

            bool usePort = !string.IsNullOrEmpty(Settings.Default.HttpCheckPort);

            string address;
            if (usePort)
            {
                var adresswithport = string.Format("{0}:{1}", check.Ip, Settings.Default.HttpCheckPort);
                address = string.Format(Settings.Default.HttpCheckAddressFormat, adresswithport);
            }
            else
            {
                address = string.Format(Settings.Default.HttpCheckAddressFormat, check.Ip);
            }

            var request = WebRequest.Create(address);
            try
            {
                var response = (HttpWebResponse)request.GetResponse();
                check.HttpStatusCode = response.StatusCode;
            }
            catch (WebException we)
            {
                var response = we.Response as HttpWebResponse;
                if (response != null)
                {
                    check.HttpStatusCode = response.StatusCode;
                }
            }
        }

        private void CheckAvailability(CheckingResult check, ParallelLoopState state)
        {
            if (_interrupted) state.Stop();

            var ping = new Ping();

            var pingReply = ping.Send(check.Ip);
            check.IPStatus = pingReply.Status;

            if (check.IPStatus == IPStatus.Success)
            {
                lock (_httpList)// TODO replace lock by ConcurrentQueue?
                {
                    _httpList.Add(check);    
                }
            }
        }
    }
}
