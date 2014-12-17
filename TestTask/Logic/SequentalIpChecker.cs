using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using TestTask.IPChecker;
using TestTask.Properties;

namespace TestTask
{
    public class SequentalIpChecker : IIPChecker
    {
        private readonly List<CheckingResult> _availabilityQueue = new List<CheckingResult>();
        private readonly List<CheckingResult> _httpQueue = new List<CheckingResult>();

        private bool _started = false;
        private TaskCompletionSource<List<CheckingResult>> _tcs;
        public Task<List<CheckingResult>> CheckIpRange(IPAddress from, IPAddress to)
        {
            long start = IpConverter.IPAddressToLong(from);
            long end = IpConverter.IPAddressToLong(to);

            if (start > end) throw new InvalidOperationException("Start > End");
            _started = true;

            _tcs = new TaskCompletionSource<List<CheckingResult>>();

            Task.Factory.StartNew(() =>
            {
                for (long i = start; i <= end; i++)
                {
                    _availabilityQueue.Add(new CheckingResult
                    {
                        Ip = IpConverter.LongToString(i)
                    });
                }

                ProcessAvailability();
                ProcessHttp();

                _tcs.SetResult(_availabilityQueue);
            });

            return _tcs.Task;
        }

        private void ProcessHttp()
        {
            bool usePort = !string.IsNullOrEmpty(Settings.Default.HttpCheckPort);
            foreach (var item in _httpQueue)
            {
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
            if (_started)
            {
                _tcs.SetCanceled();
            }
        }
    }


}
