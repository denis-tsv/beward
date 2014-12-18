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
    public class SequentalIpChecker : IIPChecker
    {
        private readonly List<CheckingResult> _availabilityQueue = new List<CheckingResult>();
        private readonly List<CheckingResult> _httpQueue = new List<CheckingResult>();
        private Task<List<CheckingResult>> _task;
        private CancellationTokenSource _cancelTokenSource;
        private int _port;
        public Task<List<CheckingResult>> CheckIpRange(IPAddress from, IPAddress to, int port)
        {
            long start = IpConverter.IPAddressToLong(from);
            long end = IpConverter.IPAddressToLong(to);
            _port = port;

            if (start > end) throw new InvalidOperationException("Start > End");

            _cancelTokenSource = new CancellationTokenSource();

            _task = Task.Factory.StartNew(() =>
            {
                FillQueue(start, end, _cancelTokenSource.Token);

                ProcessAvailability(_cancelTokenSource.Token);

                ProcessHttp(_cancelTokenSource.Token);

                return _availabilityQueue;
            }, _cancelTokenSource.Token);

            return _task;
        }

        private void FillQueue(long start, long end, CancellationToken cancellationToken)
        {
            for (long i = start; i <= end; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _availabilityQueue.Add(new CheckingResult
                {
                    Ip = IpConverter.LongToString(i)
                });
            }
        }

        private void ProcessHttp(CancellationToken cancellationToken)
        {
            foreach (var check in _httpQueue)
            {
                cancellationToken.ThrowIfCancellationRequested();

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

        private void ProcessAvailability(CancellationToken cancellationToken)
        {
            var ping = new Ping();
            foreach (var item in _availabilityQueue)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var task = ping.SendPingAsync(item.Ip);
                    task.Wait(cancellationToken);
                    item.IPStatus = task.Result.Status;

                    if (item.IPStatus == IPStatus.Success)
                    {
                        _httpQueue.Add(item);
                    }
                }
                catch (Exception ex)
                {
                    item.Error = ex.Message;
                }

            }
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
