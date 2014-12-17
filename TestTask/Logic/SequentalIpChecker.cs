using System;
using System.Collections.Generic;
using System.Net;
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
        public Task<List<CheckingResult>> CheckIpRange(IPAddress from, IPAddress to)
        {
            long start = IpConverter.IPAddressToLong(from);
            long end = IpConverter.IPAddressToLong(to);

            if (start > end) throw new InvalidOperationException("Start > End");

            _cancelTokenSource = new CancellationTokenSource();

            _task = Task.Factory.StartNew(() =>
            {
                FillQueue(start, end);

                ProcessAvailability();

                ProcessHttp();

                return _availabilityQueue;
            });

            return _task;
        }

        private void FillQueue(long start, long end)
        {
            for (long i = start; i <= end; i++)
            {
                _cancelTokenSource.Token.ThrowIfCancellationRequested();

                _availabilityQueue.Add(new CheckingResult
                {
                    Ip = IpConverter.LongToString(i)
                });
            }
        }

        private void ProcessHttp()
        {
            bool usePort = !string.IsNullOrEmpty(Settings.Default.HttpCheckPort);
            foreach (var item in _httpQueue)
            {
                _cancelTokenSource.Token.ThrowIfCancellationRequested();

                string address;
                if (usePort)
                {
                    var adresswithport = string.Format("{0}:{1}", item.Ip, Settings.Default.HttpCheckPort);
                    address = string.Format(Settings.Default.HttpCheckAddressFormat, adresswithport);
                }
                else
                {
                    address = string.Format(Settings.Default.HttpCheckAddressFormat, item.Ip);
                }

                var request = WebRequest.Create(address);
                try
                {
                    var response = (HttpWebResponse)request.GetResponse();
                    item.HttpStatusCode = response.StatusCode;
                }
                catch (WebException we)
                {
                    item.HttpStatusCode = ((HttpWebResponse)we.Response).StatusCode;
                }

            }
        }

        private void ProcessAvailability()
        {
            var ping = new Ping();
            foreach (var item in _availabilityQueue)
            {
                _cancelTokenSource.Token.ThrowIfCancellationRequested();

                var res = ping.Send(item.Ip);
                item.IPStatus = res.Status;

                if (res.Status == IPStatus.Success)
                {
                    _httpQueue.Add(item);
                }
            }
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
