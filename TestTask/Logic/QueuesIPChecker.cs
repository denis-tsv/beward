using System;
using System.Collections.Concurrent;
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
            var cancelToken = _cancelTokenSource.Token;
        
            _task = Task.Factory.StartNew(() =>
            {
                FillQueue(start, end);

                var availabilityTask = Task.Factory.StartNew(() => CheckAvailability(cancelToken), cancelToken);
                var httpTask = Task.Factory.StartNew(() => CheckHttp(cancelToken), cancelToken);

                Task.WaitAll(availabilityTask, httpTask);

                return _result;
            }, cancelToken);

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

        private void CheckHttp(CancellationToken cancellationToken)
        {
            while (!_availabilityFinished)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _httpEvent.WaitOne(100);

                while (!_httpQueue.IsEmpty)
                {
                    cancellationToken.ThrowIfCancellationRequested();

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

                        using (var client = new HttpClient())
                        {
                            try
                            {
                                var task = client.GetAsync(address, _cancelTokenSource.Token);
                                task.Wait(cancellationToken);
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
                }
            }
        }

        private void CheckAvailability(CancellationToken cancellationToken)
        {
            var ping = new Ping();

            foreach (var check in _result)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var task = ping.SendPingAsync(check.Ip);
                    task.Wait(cancellationToken);
                    check.IPStatus = task.Result.Status;

                    if (check.IPStatus == IPStatus.Success)
                    {
                        _httpQueue.Enqueue(check);
                        _httpEvent.Set();
                    }
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

            _availabilityFinished = true;
        }
        
        public void Dispose()
        {
            if (_task != null && !_task.IsCompleted)
            {
                _cancelTokenSource.Cancel();
            }
        }
    }

   
}
