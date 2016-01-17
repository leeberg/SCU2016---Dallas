﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Devices.Gpio;
using System.Diagnostics;
using Windows.Devices.I2c;
using Windows.Devices.Enumeration;
using Windows.System.Threading;
using Windows.UI.Core;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.Azure.Devices.Client;
using System.Text;


//using Microsoft.Azure.Devices.Client;


// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace App5
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        I2cDevice sensor;
        private ThreadPoolTimer TPtimer;
        public string GPIOStatus;
        private const int ENVTime = 2000;
        public string Temperature;
        public string Humidity;

        private const string DeviceConnectionString = "Put your STring here :)";


        private const int LED_PIN = 26;
        private GpioPin TrasmitLEDPin;
        private GpioPinValue TrasmitLEDPinValue = GpioPinValue.High;


        private const int SOUND_PIN = 5;
        private GpioPin SoundPin;

        

        //HostName=BergHub1.azure-devices.net;DeviceId=BergDevice2;SharedAccessKey=FkcFl9IwAZEqpPZLyOVeQDUOvacXBYhTh17GwdYqfTQ=

        public string AzureMode { get; private set; }
        public string internetConnected { get; private set; }

        

        class MySensorData
        {
            public String Name { get; set; }
            public String SensorType { get; set; }
            public String TimeStamp { get; set; }
            public String DataValue { get; set; }
            public String UnitOfMeasure { get; set; }
            public String Location { get; set; }
            public String DataType { get; set; }
            public String MeasurementID { get; set; }
        }


        public MainPage()
        {
            this.InitializeComponent();

            AzureMode = "Transmit";
            internetConnected = "True";
            Debug.WriteLine(DateTime.Now + " Main Page Initialization Starting");

            Debug.WriteLine(DateTime.Now + " Init GPIO");
            InitGPIO();

            Debug.WriteLine(DateTime.Now + " Init SPI");
            InitSPI();

            Debug.WriteLine(DateTime.Now + " Initialization Complete");


        }

        private void Timer_Tick(ThreadPoolTimer timer)
        {
            if (GPIOStatus == "Connected")
            {

                // byte[] tempCommand1 = { 0x24 };

                byte[] tempCommand2 = { 0x24, 0x00, 0xFF };

                byte[] tempData = new byte[8];

                try

                {
                    sensor.WriteRead(tempCommand2, tempData);

                    var rawTempReading = tempData[0] << 8;
                    double stemp = rawTempReading;
                    //Debug.WriteLine("RAW : " + stemp.ToString());

                    var tempRatio = rawTempReading / (float)65536;
                    double temperature = (-46.85 + (175.72 * tempRatio)) * 9 / 5 + 32;
                    //  string strtemperature = (temperature.ToString()).Substring(0, 4);
                    string strTemperature = (temperature.ToString()).Substring(0, 4);

                    //var rawTempReading = tempData[0] << 8 | tempData[1];
                    //var tempRatio = rawTempReading / (float)65536;
                    //double temperature = (-46.85 + (175.72 * tempRatio)) * 9 / 5 + 32;


                    var rawHumReading = tempData[3] << 8;// | tempData[4];
                    var humidityRatio = rawHumReading / (float)65536;
                    double humidity = -6 + (125 * humidityRatio);
                    string strHumidity = (humidity.ToString()).Substring(0, 4);

                    // Debug.WriteLine("Temp: " + strTemperature);
                    // Debug.WriteLine("Humidity: " + strHumidity);


                    string strNoisePresence = SoundPin.Read().ToString();
                    if (strNoisePresence == "High")
                    {
                        strNoisePresence = "100";
                    }
                    else
                    {
                        strNoisePresence = "0";
                    }


                    var task = Dispatcher.RunAsync(CoreDispatcherPriority.High, async () =>
                     {
                         Temperature = strTemperature;
                         Humidity = strHumidity;
                         
                         TxtBlockHumidity.Text = Humidity;
                         TxtBlockTemp.Text = Temperature;
                       
                         if (strNoisePresence == "100")
                         {
                             TxtBlockNoise.Text = "Noisy";
                         }
                         else
                         {
                             TxtBlockNoise.Text = "Quiet";
                         }


                         Log_Event(strHumidity, "BergIOTDemo", "Environmental", "Humidity", "%");
                         Log_Event(strTemperature, "BergIOTDemo", "Environmental", "Temperature", "F");

                         if (strNoisePresence == "100")
                         {
                            Log_Event(strNoisePresence, "BergIOTDemo", "Audio", "NoisePresence", "bol");
                         }
                         


                     });

                }
                catch
                {
                    Debug.WriteLine("Bus is mad!");
                }

               

            }
        }

       

        private async Task Log_Event(string DataValue, string Name, string Sensor, string DataType, string UnitOfMeasure)
        {
            //Debug
            DateTime localDate = DateTime.Now;
            System.Diagnostics.Debug.WriteLine("Event: " + Name + "-" + Sensor + "-" + DataValue + "-" + localDate.ToString());
    
            //Flash LED 
            TrasmitLEDPin.Write(GpioPinValue.High);
            await Task.Delay(100);
            TrasmitLEDPin.Write(GpioPinValue.Low);
           
            //to Azure
            if (AzureMode == "Transmit")
                if (internetConnected == "True")
                {
                    {

                        //Init httpClinet:
                        //var httpClient = new HttpClient();

                        System.Diagnostics.Debug.WriteLine("Starting Azure Transmit");


                        MySensorData SensorInstance = new MySensorData();
                        SensorInstance.Name = Name;
                        SensorInstance.SensorType = Sensor;
                        SensorInstance.TimeStamp = DateTime.Now.ToString();
                        SensorInstance.DataValue = DataValue;
                        SensorInstance.DataType = DataType;
                        SensorInstance.UnitOfMeasure = UnitOfMeasure;
                        SensorInstance.MeasurementID = Guid.NewGuid().ToString();
                        SensorInstance.Location = "NULL";

                        string jsoncontent = JsonConvert.SerializeObject(SensorInstance);
                       
                        DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(DeviceConnectionString);

                        string dataBuffer;
                        dataBuffer = jsoncontent;

                        System.Diagnostics.Debug.WriteLine(jsoncontent);


                        Message eventMessage = new Message(Encoding.UTF8.GetBytes(dataBuffer));
                        await deviceClient.SendEventAsync(eventMessage);


                        System.Diagnostics.Debug.WriteLine("Azure Transmit Done");



                    }
                }


        }



        public async void InitSPI()
        {

            String aqs = I2cDevice.GetDeviceSelector("I2C1");
            var deviceInfo = await DeviceInformation.FindAllAsync(aqs);
            sensor = await I2cDevice.FromIdAsync(deviceInfo[0].Id, new I2cConnectionSettings(0x44));

            byte[] resetCommand = { 0x30, 0xA2 };
            sensor.Write(resetCommand);

            TPtimer = ThreadPoolTimer.CreatePeriodicTimer(Timer_Tick, TimeSpan.FromMilliseconds(ENVTime)); // .FromMilliseconds(1000));


        }

        private async void InitGPIO()
        {
            var gpio = GpioController.GetDefault();

            // Show an error if there is no GPIO controller
            if (gpio == null)
            {
                // GpioStatus.Text = "There is no GPIO controller on this device.";
                Debug.WriteLine("There is no GPIO controller on this device.");
                return;
            }

            else
            {

                Debug.WriteLine("GPIO controller - OK!");
                GPIOStatus = "Connected";

                //Setup Status LED
                TrasmitLEDPin = gpio.OpenPin(LED_PIN);
                TrasmitLEDPin.SetDriveMode(GpioPinDriveMode.Output);
                TrasmitLEDPin.Write(GpioPinValue.Low);

                //Setup Sound Pin
                SoundPin = gpio.OpenPin(SOUND_PIN);
                SoundPin.SetDriveMode(GpioPinDriveMode.Input);



            }


        }



    }
}
