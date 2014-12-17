using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Practices.Prism.Mvvm;
using Microsoft.Practices.Prism.ViewModel;
using ReactiveUI;
using TestTask.Logic;
using TestTask.Model;

namespace TestTask.ViewModel
{
    public class MainWindowViewModel : BindableBase, INotifyDataErrorInfo
    {
        #region Fields

        private bool _isBusy;
        private ReactiveCommand<object> _startQueuesCheckingCommand;
        private ReactiveCommand<object> _startParallelForCheckingCommand;
        private ReactiveCommand<object> _startSequentalCheckingCommand;
        private ReactiveCommand<object> _cancelCommand;
        private IIPChecker _ipChecker;
        private List<CheckingResult> _checkingResults;
        private string _fromIp;
        private string _toIp;
        private TimeSpan _parallelForTime;
        private TimeSpan _sequentalTime;
        private TimeSpan _queuesTime;
        private ErrorsContainer<ValidationResult> _errorsContainer;

        #endregion

        #region Properties

        [Required]
        [IPAddress]
        public string FromIp
        {
            get { return _fromIp; }
            set
            {
                SetProperty(ref _fromIp, value);
                ValidateProperty(value);
            }
        }

        [Required]
        [IPAddress]
        public string ToIp
        {
            get { return _toIp; }
            set
            {
                SetProperty(ref _toIp, value);
                ValidateProperty(value);
            }
        }

        public bool IsBusy
        {
            get { return _isBusy; }
            private set { SetProperty(ref _isBusy, value); }
        }

        public TimeSpan QueuesTime
        {
            get { return _queuesTime; }
            private set { SetProperty(ref _queuesTime, value); }
        }

        public TimeSpan ParallelForTime
        {
            get { return _parallelForTime; }
            private set { SetProperty(ref _parallelForTime, value); }
        }

        public TimeSpan SequentalTime
        {
            get { return _sequentalTime; }
            private set { SetProperty(ref _sequentalTime, value); }
        }

        public List<CheckingResult> CheckingResults
        {
            get { return _checkingResults; }
            private set { SetProperty(ref _checkingResults, value); }
        }

        protected ErrorsContainer<ValidationResult> ErrorsContainer
        {
            get { return _errorsContainer ?? (_errorsContainer = new ErrorsContainer<ValidationResult>(OnErrorsChanged)); }
        }

        #endregion

        #region Commands

        public ICommand StartSequentalCheckingCommand
        {
            get
            {
                if (_startSequentalCheckingCommand == null)
                {
                    var canExecute = this.WhenAny(vm => vm.IsBusy, vm => vm.FromIp, vm => vm.ToIp, vm => vm.HasErrors, (busy, from, to, errors) => busy.Value == false && from.Value != null && to.Value != null && !errors.Value);
                    _startSequentalCheckingCommand = ReactiveCommand.Create(canExecute);
                    _startSequentalCheckingCommand.Subscribe(_ => OnStartSequentalChecking());
                }
                return _startSequentalCheckingCommand;
            }
        }

        public ICommand StartParallelForCheckingCommand
        {
            get
            {
                if (_startParallelForCheckingCommand == null)
                {
                    var canExecute = this.WhenAny(vm => vm.IsBusy, vm => vm.FromIp, vm => vm.ToIp, vm => vm.HasErrors, (busy, from, to, errors) => busy.Value == false && from.Value != null && to.Value != null && !errors.Value);
                    _startParallelForCheckingCommand = ReactiveCommand.Create(canExecute);
                    _startParallelForCheckingCommand.Subscribe(_ => OnStartParallelForChecking());
                }
                return _startParallelForCheckingCommand;
            }
        }
       
        public ICommand StartQueuesCheckingCommand
        {
            get
            {
                if (_startQueuesCheckingCommand == null)
                {
                    var canExecute = this.WhenAny(vm => vm.IsBusy, vm => vm.FromIp, vm => vm.ToIp, vm => vm.HasErrors, (busy, from, to, errors) => busy.Value == false && from.Value != null && to.Value != null && !errors.Value);
                    _startQueuesCheckingCommand = ReactiveCommand.Create(canExecute);
                    _startQueuesCheckingCommand.Subscribe(_ => OnStartQueuesChecking());
                }
                return _startQueuesCheckingCommand;
            }
        }

       
        public ICommand CancelCommand
        {
            get
            {
                if (_cancelCommand == null)
                {
                    var canExecute = this.WhenAny(vm => vm.IsBusy, (busy) => busy.Value);
                    _cancelCommand = ReactiveCommand.Create(canExecute);
                    _cancelCommand.Subscribe(_ => OnCancel());
                }
                return _cancelCommand;
            }
        }

        #endregion

        #region Methods
        private void OnCancel()
        {
            _ipChecker.Dispose();
            _ipChecker = null;

            IsBusy = false;
        }

        private async void OnStartQueuesChecking()
        {
            var startTime = DateTime.Now;

            _ipChecker = new ParallelIPChecker();

            await StartChecking();

            QueuesTime = DateTime.Now - startTime;
        }

        private async void OnStartSequentalChecking()
        {
            var startTime = DateTime.Now;

            _ipChecker = new SequentalIpChecker();

            await StartChecking();

            SequentalTime = DateTime.Now - startTime;
        }

        private async void OnStartParallelForChecking()
        {
            var startTime = DateTime.Now;

            _ipChecker = new ParallelForIPChecker(); 

            await StartChecking();

            ParallelForTime = DateTime.Now - startTime;
        }
        
        private async Task StartChecking()
        {
            IsBusy = true;

            CheckingResults = null;

            try
            {   
                CheckingResults = await _ipChecker.CheckIpRange(IPAddress.Parse(FromIp), IPAddress.Parse(ToIp));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ValidateProperty(object value, [CallerMemberName] string propertyName = null)
        {
            var res = new List<ValidationResult>();
            var validationresult = Validator.TryValidateProperty(value, new ValidationContext(this, null, null) { MemberName = propertyName }, res);
            ErrorsContainer.SetErrors(propertyName, res);
            HasErrors = validationresult;
        }

        #endregion

        #region INotifyDataErrorInfo implementation

        public IEnumerable GetErrors(string propertyName)
        {
            return ErrorsContainer.GetErrors(propertyName);
        }

        public bool HasErrors 
        {
            get { return ErrorsContainer.HasErrors; }
            private set { OnPropertyChanged(() => HasErrors); }
        }

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        private void OnErrorsChanged(string propertyName)
        {
            if (ErrorsChanged != null) ErrorsChanged(this, new DataErrorsChangedEventArgs(propertyName));
        }

        #endregion
    }
}
