using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using TestTask.IPChecker;
using TestTask.Properties;

namespace TestTask
{
    public class ParallelIpChecker : IIPChecker
    {
        private ConcurrentQueue<CheckingResult> _availabilityQueue = new ConcurrentQueue<CheckingResult>();
        private ConcurrentQueue<CheckingResult> _httpQueue = new ConcurrentQueue<CheckingResult>();
        private Thread _availabilityCheckerThread;
        private Thread _httpCheckerThread;

        private AutoResetEvent _availabilityEvent = new AutoResetEvent(false);
        private AutoResetEvent _httpEvent = new AutoResetEvent(false);

        private bool _diapasonFinished;
        private bool _availabilityFinished = false;

        private bool _interruptAvailabilityChecking = false;
        private bool _interruptHttpChecking = false;

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

                _availabilityCheckerThread = new Thread(CheckAvailability);
                _availabilityCheckerThread.Start();

                _httpCheckerThread = new Thread(CheckHttp);
                _httpCheckerThread.Start();

                var result = new List<CheckingResult>();
                for (long i = start; i <= end; i++)
                {
                    var item = new CheckingResult { Ip = IpConverter.LongToString(i) };
                    result.Add(item);
                    _availabilityQueue.Enqueue(item);
                    _availabilityEvent.Set();
                }
                _diapasonFinished = true;

                _availabilityCheckerThread.Join();
                _httpCheckerThread.Join();

                _tcs.SetResult(result);
            });

            return _tcs.Task;
        }

        private void CheckHttp()
        {
            bool usePort = !string.IsNullOrEmpty(Settings.Default.HttpCheckPort);

            while (true)
            {
                _httpEvent.WaitOne(100);

                if (_interruptHttpChecking) return;

                while (!_httpQueue.IsEmpty)
                {
                    if (_interruptHttpChecking) return;

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

                if (_availabilityFinished || _interruptHttpChecking) break;
            }
        }

        private void CheckAvailability()
        {
            var ping = new Ping();

            while (true)
            {
                _availabilityEvent.WaitOne(100);

                if (_interruptAvailabilityChecking) return;

                while (!_availabilityQueue.IsEmpty)
                {
                    if (_interruptAvailabilityChecking) return;

                    CheckingResult check;
                    if (_availabilityQueue.TryDequeue(out check))
                    {
                        
                        var pingReply = ping.Send(check.Ip);
                        check.IPStatus = pingReply.Status;

                        if (check.IPStatus == IPStatus.Success)
                        {
                            _httpQueue.Enqueue(check);
                            _httpEvent.Set();
                        }
                    }
                }

                if (_diapasonFinished || _interruptAvailabilityChecking) break;
            }
            _availabilityFinished = true;
        }
        
        public void Dispose()
        {
            if (_started)
            {
                _tcs.SetCanceled();

                _interruptAvailabilityChecking = true;
                _interruptHttpChecking = true;
                
                _availabilityCheckerThread.Join();
                _httpCheckerThread.Join();
            }
        }
    }

   
}
