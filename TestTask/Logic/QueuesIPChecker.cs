using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using TestTask.Model;
using TestTask.Properties;

namespace TestTask.Logic
{
    public class QueuesIPChecker : IIPChecker
    {
        private ConcurrentQueue<CheckingResult> _httpQueue = new ConcurrentQueue<CheckingResult>();
        private List<CheckingResult> _result = new List<CheckingResult>();
        private int _port;
        private AutoResetEvent _httpEvent = new AutoResetEvent(false);

        private bool _availabilityFinished = false;

        private CancellationTokenSource _cancelTokenSource;
        private Task<List<CheckingResult>> _task;
        public Task<List<CheckingResult>> CheckIpRange(IPAddress from, IPAddress to, int port)
        {
            long start = IpConverter.IPAddressToLong(from);
            long end = IpConverter.IPAddressToLong(to);
            _port = port;

            if (start > end) throw new InvalidOperationException("Start > End");

            _cancelTokenSource = new CancellationTokenSource();
        
            _task = Task.Factory.StartNew(() =>
            {
                FillQueue(start, end);

                var availabilityTask = Task.Factory.StartNew(CheckAvailability);
                var httpTask = Task.Factory.StartNew(CheckHttp);

                Task.WaitAll(availabilityTask, httpTask);

                return _result;
            });

            return _task;
        }

        private void FillQueue(long start, long end)
        {
            for (long i = start; i <= end; i++)
            {
                _cancelTokenSource.Token.ThrowIfCancellationRequested();

                var item = new CheckingResult {Ip = IpConverter.LongToString(i)};
                _result.Add(item);
            }
        }

        private void CheckHttp()
        {
            while (!_availabilityFinished)
            {
                _cancelTokenSource.Token.ThrowIfCancellationRequested();

                _httpEvent.WaitOne(100);

                while (!_httpQueue.IsEmpty)
                {
                    _cancelTokenSource.Token.ThrowIfCancellationRequested();

                    CheckingResult check;
                    if (_httpQueue.TryDequeue(out check))
                    {
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
                }
            }
        }

        private void CheckAvailability()
        {
            var ping = new Ping();

            foreach (var check in _result)
            {
                _cancelTokenSource.Token.ThrowIfCancellationRequested();

                var pingReply = ping.Send(check.Ip);
                check.IPStatus = pingReply.Status;

                if (check.IPStatus == IPStatus.Success)
                {
                    _httpQueue.Enqueue(check);
                    _httpEvent.Set();
                }
            }

            _availabilityFinished = true;
        }
        
        public void Dispose()
        {
            if (!_task.IsCompleted)
            {
                _cancelTokenSource.Cancel();
            }
        }
    }

   
}
