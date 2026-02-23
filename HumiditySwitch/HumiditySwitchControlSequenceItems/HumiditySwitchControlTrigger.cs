using Newtonsoft.Json;
using NINA.Core.Locale;
using NINA.Core.Model;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Equipment.MySwitch;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.SequenceItem.Switch;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ChrisDowd.NINA.HumiditySwitchControl.HumiditySwitchControlTestCategory {
    /// <summary>
    /// This Class shows the basic principle on how to add a new Sequence Trigger to the N.I.N.A. sequencer via the plugin interface
    /// For ease of use this class inherits the abstract SequenceTrigger which already handles most of the running logic, like logging, exception handling etc.
    /// A complete custom implementation by just implementing ISequenceTrigger is possible too
    /// The following MetaData can be set to drive the initial values
    /// --> Name - The name that will be displayed for the item
    /// --> Description - a brief summary of what the item is doing. It will be displayed as a tooltip on mouseover in the application
    /// --> Icon - a string to the key value of a Geometry inside N.I.N.A.'s geometry resources
    ///
    /// If the item has some preconditions that should be validated, it shall also extend the IValidatable interface and add the validation logic accordingly.
    /// </summary>
    [ExportMetadata("Name", "Humidity Switch Control")]
    [ExportMetadata("Description", "This trigger will turn on and off a switch type based on the humidity")]
    [ExportMetadata("Icon", "Plugin_Test_SVG")]
    [ExportMetadata("Category", "Switch")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class HumiditySwitchControlTrigger : SequenceTrigger, IValidatable {
        /// <summary>
        /// The constructor marked with [ImportingConstructor] will be used to import and construct the object
        /// General device interfaces can be added to the constructor parameters and will be automatically injected on instantiation by the plugin loader
        /// </summary>
        /// <remarks>
        /// Available interfaces to be injected:
        ///     - IProfileService,
        ///     - ICameraMediator,
        ///     - ITelescopeMediator,
        ///     - IFocuserMediator,
        ///     - IFilterWheelMediator,
        ///     - IGuiderMediator,
        ///     - IRotatorMediator,
        ///     - IFlatDeviceMediator,
        ///     - IWeatherDataMediator,
        ///     - IImagingMediator,
        ///     - IApplicationStatusMediator,
        ///     - INighttimeCalculator,
        ///     - IPlanetariumFactory,
        ///     - IImageHistoryVM,
        ///     - IDeepSkyObjectSearchVM,
        ///     - IDomeMediator,
        ///     - IImageSaveMediator,
        ///     - ISwitchMediator,
        ///     - ISafetyMonitorMediator,
        ///     - IApplicationMediator
        ///     - IApplicationResourceDictionary
        ///     - IFramingAssistantVM
        ///     - IList<IDateTimeProvider>
        /// </remarks>
        /// 

        private readonly IWeatherDataMediator weatherDataMediator;
        private readonly ISwitchMediator switchMediator;

        // Validation
        private readonly List<string> issues = new List<string>();
        private List<string> lastIssuesSnapshot = new List<string>();

        private ReadOnlyCollection<IWritableSwitch> writableSwitches;

        private short switchIndex;

        private IWritableSwitch selectedSwitch;

        private double currentHumidityValue = 0.0;
        private int humidityThreshold = 50;
        private bool isSwitchOn;

        [ImportingConstructor]
        public HumiditySwitchControlTrigger(IWeatherDataMediator weather, ISwitchMediator switches) {
            weatherDataMediator = weather;
            switchMediator = switches;

            WritableSwitches = new ReadOnlyCollection<IWritableSwitch>(CreateDummyList());
            SelectedSwitch = WritableSwitches.First();
            Value = 0;

            IncreaseValueCommand = new DelegateCommand(() => {
                if (Value <= 95) {
                    Value += 5;
                }
            });

            DecreaseValueCommand = new DelegateCommand(() => {
                if (Value >= 5) {
                    Value -= 5;
                }
            });
        }

        private HumiditySwitchControlTrigger(HumiditySwitchControlTrigger cloneMe) : this(cloneMe.weatherDataMediator, cloneMe.switchMediator) {
            CopyMetaData(cloneMe);
        }

        private void AfterClone(HumiditySwitchControlTrigger clone) {
            clone.SwitchIndex = SwitchIndex;
        }

        [JsonProperty]
        public double Value {
            get => value;
            set {
                var clampedValue = Math.Max(0, Math.Min(100, Math.Round(value / 5.0) * 5));
                if (Math.Abs(this.value - clampedValue) > double.Epsilon) {
                    this.value = clampedValue;
                    RaisePropertyChanged();
                }
            }
        }

        private double value;

        [JsonProperty]
        public int HumidityThreshold {
            get => humidityThreshold;
            set {
                var clampedValue = Math.Max(0, Math.Min(100, value));
                if (humidityThreshold != clampedValue) {
                    humidityThreshold = clampedValue;
                    RaisePropertyChanged();
                }
            }
        }

        [JsonIgnore]
        public bool IsSwitchOn {
            get => isSwitchOn;
            private set {
                if (isSwitchOn != value) {
                    isSwitchOn = value;
                    RaisePropertyChanged();
                }
            }
        }

        [JsonIgnore]
        public ICommand IncreaseValueCommand { get; }

        [JsonIgnore]
        public ICommand DecreaseValueCommand { get; }

        public override object Clone() {
            return new HumiditySwitchControlTrigger(weatherDataMediator, switchMediator) {
                Icon = Icon,
                Name = Name,
                Category = Category,
                Description = Description,
                SwitchIndex = SwitchIndex,
                Value = Value,
                HumidityThreshold = HumidityThreshold
            };
        }

        /// <summary>
        /// The actual running logic for when the trigger should run
        /// </summary>
        /// <param name="context"></param>
        /// <param name="progress"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public override Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token) {
            Notification.ShowSuccess("Trigger was fired");
            return Task.CompletedTask;
        }

        /// <summary>
        /// This method will be evaluated to see if the trigger should be executed.
        /// When true - the Execute method will be called
        /// Skipped otherwise
        ///
        /// For this example the trigger will fire when the random number generator generates an even number
        /// </summary>
        /// <param name="previousItem"></param>
        /// <param name="nextItem"></param>
        /// <returns></returns>
        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            IsSwitchOn = currentHumidityValue > HumidityThreshold;
            return IsSwitchOn;
        }

        public IList<string> Issues => issues;

        /// <summary>
        /// This string will be used for logging
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(HumiditySwitchControlTrigger)}";
        }

        bool IValidatable.Validate() {
            issues.Clear();

            var weatherInfo = weatherDataMediator.GetInfo();
            if(weatherInfo?.Connected != true) {
                issues.Add("Weather Not Connected");
            } else {
                if(weatherInfo.Humidity <= 0.0 || weatherInfo.Humidity > 100.0 ) {
                    issues.Add("No Humidity Data");
                } else {
                    currentHumidityValue = weatherInfo.Humidity;
                    IsSwitchOn = currentHumidityValue > HumidityThreshold;
                }
            }

            var info = switchMediator.GetInfo();
            if (info?.Connected != true) {
                //When switch gets disconnected the real list will be changed to the dummy list
                if (!(WritableSwitches.FirstOrDefault() is DummySwitch)) {
                    WritableSwitches = new ReadOnlyCollection<IWritableSwitch>(CreateDummyList());
                }

                issues.Add("Switch Not Connected");
            } else {
                if (WritableSwitches.Count > 0) {
                    //When switch gets connected the dummy list will be changed to the real list
                    if (WritableSwitches.FirstOrDefault() is DummySwitch) {
                        WritableSwitches = info.WritableSwitches;

                        if (switchIndex >= 0 && WritableSwitches.Count > switchIndex) {
                            SelectedSwitch = WritableSwitches[switchIndex];
                        } else {
                            SelectedSwitch = null;
                        }
                    }
                } else {
                    SelectedSwitch = null;
                    issues.Add("No Writable Switch");
                }
            }

            if (switchIndex >= 0 && WritableSwitches.Count > switchIndex) {
                if (WritableSwitches[switchIndex] != SelectedSwitch) {
                    SelectedSwitch = WritableSwitches[switchIndex];
                }
            }

            var s = SelectedSwitch;

            if (s == null) {
                issues.Add(string.Format("No Switch Selected"));
            } else {
                if (Value < s.Minimum || Value > s.Maximum)
                    issues.Add(string.Format("Invalid Switch Value. Expected range {0} to {1} with step {2}.", s.Minimum, s.Maximum, s.StepSize));
            }

            bool changed = !issues.SequenceEqual(lastIssuesSnapshot);
            if (changed) {
                lastIssuesSnapshot = new List<string>(issues);
                RaisePropertyChanged(nameof(Issues));
            }

            return issues.Count == 0;
        }
            


        public ReadOnlyCollection<IWritableSwitch> WritableSwitches {
            get => writableSwitches;
            set {
                writableSwitches = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public short SwitchIndex {
            get => switchIndex;
            set {
                if (value > -1) {
                    switchIndex = value;
                    RaisePropertyChanged();
                }
            }
        }

        private IList<IWritableSwitch> CreateDummyList() {
            var dummySwitches = new List<IWritableSwitch>();
            for (short i = 0; i < 20; i++) {
                dummySwitches.Add(new DummySwitch((short)(i + 1)));
            }
            return dummySwitches;
        }

        [JsonIgnore]
        public IWritableSwitch SelectedSwitch {
            get => selectedSwitch;
            set {
                selectedSwitch = value;
                SwitchIndex = (short)(WritableSwitches?.IndexOf(selectedSwitch) ?? -1);
                RaisePropertyChanged();
            }
        }


    }

    internal sealed class DelegateCommand : ICommand {
        private readonly Action execute;

        public DelegateCommand(Action executeAction) {
            execute = executeAction;
        }

        public event EventHandler CanExecuteChanged {
            add { }
            remove { }
        }

        public bool CanExecute(object parameter) {
            return true;
        }

        public void Execute(object parameter) {
            execute();
        }
    }
}
