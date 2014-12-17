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
    public class ParallelIPChecker : IIPChecker
    {
        private ConcurrentQueue<CheckingResult> _httpQueue = new ConcurrentQueue<CheckingResult>();
        private List<CheckingResult> _result = new List<CheckingResult>();
        private Thread _availabilityCheckerThread;
        private Thread _httpCheckerThread;

        private AutoResetEvent _httpEvent = new AutoResetEvent(false);

        private bool _availabilityFinished = false;

        private bool _interrupt = false;

        private bool _finished = false;
        private TaskCompletionSource<List<CheckingResult>> _tcs;
        public Task<List<CheckingResult>> CheckIpRange(IPAddress from, IPAddress to)
        {
            long start = IpConverter.IPAddressToLong(from);
            long end = IpConverter.IPAddressToLong(to);

            if (start > end) throw new InvalidOperationException("Start > End");
            
            _tcs = new TaskCompletionSource<List<CheckingResult>>();

            Task.Factory.StartNew(() =>
            {
                for (long i = start; i <= end; i++)
                {
                    var item = new CheckingResult { Ip = IpConverter.LongToString(i) };
                    _result.Add(item);
                }

                _availabilityCheckerThread = new Thread(CheckAvailability);
                _availabilityCheckerThread.Start();

                _httpCheckerThread = new Thread(CheckHttp);
                _httpCheckerThread.Start();

                _availabilityCheckerThread.Join();
                _httpCheckerThread.Join();

                _finished = true;

                _tcs.SetResult(_result);
            });

            return _tcs.Task;
        }

        private void CheckHttp()
        {
            bool usePort = !string.IsNullOrEmpty(Settings.Default.HttpCheckPort);

            while (true)
            {
                _httpEvent.WaitOne(100);

                if (_interrupt) return;

                while (!_httpQueue.IsEmpty)
                {
                    if (_interrupt) return;

                    CheckingResult check;
                    if (_httpQueue.TryDequeue(out check))
                    {
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
                }

                if (_availabilityFinished || _interrupt) break;
            }
        }

        private void CheckAvailability()
        {
            var ping = new Ping();

            foreach (var check in _result)
            {
                if (_interrupt) return;

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
            if (!_finished)
            {
                _tcs.SetCanceled();

                _interrupt = true;
            }
        }
    }

   
}
